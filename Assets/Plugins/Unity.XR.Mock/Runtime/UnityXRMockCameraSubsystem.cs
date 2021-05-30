using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.Mock
{
    [Preserve]
    public sealed class UnityXRMockCameraSubsystem : XRCameraSubsystem
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Register()
        {
            // Clone descriptor
            var cinfo = new XRCameraSubsystemCinfo
            {
                id = typeof(UnityXRMockCameraSubsystem).FullName,
                providerType = typeof(MockProvider),
                subsystemTypeOverride = typeof(UnityXRMockCameraSubsystem),
                supportsAverageBrightness = true,
                supportsAverageColorTemperature = true,
                supportsAverageIntensityInLumens = true,
                supportsCameraConfigurations = false,
                supportsCameraImage = true,
                supportsColorCorrection = true,
                supportsDisplayMatrix = true,
                supportsFocusModes = false,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraGrain = true,
                supportsFaceTrackingAmbientIntensityLightEstimation = true,
                supportsFaceTrackingHDRLightEstimation = true,
                supportsWorldTrackingAmbientIntensityLightEstimation = true,
                supportsWorldTrackingHDRLightEstimation = true
            };

            Register(cinfo);
        }

        private class MockProvider : Provider
        {
            private XRCameraParams cameraParams;
            private long? timestamp;
            private Vector2? screenSize;
            private ScreenOrientation? screenOrientation;
            private Material m_CameraMaterial;
            private Texture2D m_texture;
            private static int count = 0;

            [Preserve]
            public MockProvider()
            {
                if (count++ % 3 == 0)
                {
                    //m_CameraMaterial = CreateCameraMaterial("Unlit/ARKitBackground");
                    m_texture = new Texture2D(100, 100, TextureFormat.ARGB32, false);

                    for (int x = 0; x < 100; ++x)
                        for (int y = 0; y < 100; ++y)
                            m_texture.SetPixel(x, y, Color.red);

                    m_texture.Apply();
                }
            }

            public override Material cameraMaterial => OcclusionApi.material;//  m_CameraMaterial;

            public override bool permissionGranted => CameraApi.permissionGranted;

            public override void Start()
            {
                this.ResetState();
                base.Start();
            }

            public override void Stop()
            {
                this.ResetState();
                base.Stop();
            }

            public override void Destroy()
            {
                base.Destroy();
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                var timestamp = CameraApi.timestampNs;
                if (this.cameraParams != cameraParams
                    || this.timestamp != timestamp
                    || this.screenSize != CameraApi.screenSize
                    || this.screenOrientation != CameraApi.screenOrientation)
                {
                    try
                    {
                        var result = new XRCameraFrameMock();

                        void SetProperty<T>(XRCameraFrameProperties prop, T? input, ref T output) where T : struct
                        {
                            if (input.HasValue)
                            {
                                output = input.Value;
                                result.m_Properties = result.m_Properties | prop;
                            }
                        }

                        SetProperty(XRCameraFrameProperties.Timestamp, CameraApi.timestampNs, ref result.m_TimestampNs);
                        SetProperty(XRCameraFrameProperties.AverageBrightness, CameraApi.averageBrightness, ref result.m_AverageColorTemperature);
                        SetProperty(XRCameraFrameProperties.AverageColorTemperature, CameraApi.averageColorTemperature, ref result.m_AverageColorTemperature);
                        SetProperty(XRCameraFrameProperties.ColorCorrection, CameraApi.colorCorrection, ref result.m_ColorCorrection);

                        if (CameraApi.projectionMatrix.HasValue)
                        {
                            Matrix4x4 screenMatrix = Matrix4x4.identity;
                            if (CameraApi.screenSize.HasValue)
                            {
                                var sourceScreenSize = CameraApi.screenSize.Value;
                                var sourceAspect = sourceScreenSize.x / sourceScreenSize.y;
                                var screenAspect = cameraParams.screenWidth / cameraParams.screenHeight;
                                if (sourceAspect < screenAspect)
                                {
                                    screenMatrix.m00 = sourceAspect / screenAspect;
                                }
                                else
                                {
                                    screenMatrix.m11 = screenAspect / sourceAspect;
                                }
                            }

                            result.m_ProjectionMatrix = screenMatrix * CameraApi.projectionMatrix.Value;
                            result.m_Properties = result.m_Properties | XRCameraFrameProperties.ProjectionMatrix;
                        }

                        SetProperty(XRCameraFrameProperties.DisplayMatrix, CameraApi.displayMatrix, ref result.m_DisplayMatrix);
                        SetProperty(XRCameraFrameProperties.AverageIntensityInLumens, CameraApi.averageIntensityInLumens, ref result.m_AverageIntensityInLumens);
                        SetProperty(XRCameraFrameProperties.ExposureDuration, CameraApi.exposureDuration, ref result.m_ExposureDuration);
                        SetProperty(XRCameraFrameProperties.ExposureOffset, CameraApi.exposureOffset, ref result.m_ExposureOffset);
                        SetProperty(XRCameraFrameProperties.MainLightIntensityLumens, CameraApi.mainLightIntensityLumens, ref result.m_MainLightIntensityLumens);
                        SetProperty(XRCameraFrameProperties.MainLightColor, CameraApi.mainLightColor, ref result.m_MainLightColor);
                        SetProperty(XRCameraFrameProperties.MainLightDirection, CameraApi.mainLightDirection, ref result.m_MainLightDirection);
                        SetProperty(XRCameraFrameProperties.AmbientSphericalHarmonics, CameraApi.ambientSphericalHarmonics, ref result.m_AmbientSphericalHarmonics);
                        SetProperty(XRCameraFrameProperties.CameraGrain, CameraApi.cameraGrain, ref result.m_CameraGrain);
                        SetProperty(XRCameraFrameProperties.NoiseIntensity, CameraApi.noiseIntensity, ref result.m_NoiseIntensity);

                        result.m_TrackingState = CameraApi.trackingState;
                        result.m_NativePtr = IntPtr.Zero;

                        result.Convert(out cameraFrame);
                        return true;
                    }
                    finally
                    {
                        this.timestamp = timestamp;
                        this.cameraParams = cameraParams;
                        this.screenSize = CameraApi.screenSize;
                        this.screenOrientation = CameraApi.screenOrientation;
                    }
                }

                cameraFrame = default;
                return false;
            }

            public unsafe override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator)
            {
                var array = new XRTextureDescriptor[] { defaultDescriptor };
                fixed (void* arrayPtr = &array[0])
                {
                    return NativeCopyUtility.PtrToNativeArrayWithDefault(
                        defaultDescriptor,
                        arrayPtr,
                        sizeof(XRTextureDescriptor),
                        array.Length,
                        allocator);
                }

                //return new NativeArray<XRTextureDescriptor>(
                //    new XRTextureDescriptor[]
                //    {
                //        defaultDescriptor,
                //        //new XRTextureDescriptorMock()
                //        //{
                //        //    m_NativeTexture = IntPtr.Zero,// m_texture.GetNativeTexturePtr(),
                //        //    m_PropertyNameId = Shader.PropertyToID("_MainTex"),
                //        //    m_Depth=1,
                //        //    m_Dimension = m_texture.dimension,
                //        //    m_Format = m_texture.format,
                //        //    m_Width = m_texture.width,
                //        //    m_Height = m_texture.height,
                //        //    m_MipmapCount = m_texture.mipmapCount
                //        //}.Convert()
                //    }, allocator);

                //  return base.GetTextureDescriptors(default, allocator);
            }

            private void ResetState()
            {
                this.cameraParams = default;
                this.timestamp = null;
                this.screenSize = null;
                this.screenOrientation = null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRCameraFrameMock
        {
            /// <summary>
            /// The timestamp, in nanoseconds, associated with this frame.
            /// </summary>
            /// <value>
            /// The timestamp, in nanoseconds, associated with this frame.
            /// </value>
            public long m_TimestampNs;

            /// <summary>
            /// The estimated brightness of the scene.
            /// </summary>
            /// <value>
            /// The estimated brightness of the scene.
            /// </value>
            public float m_AverageBrightness;

            /// <summary>
            /// The estimated color temperature of the scene.
            /// </summary>
            /// <value>
            /// The estimated color temperature of the scene.
            /// </value>
            public float m_AverageColorTemperature;

            /// <summary>
            /// The estimated color correction value of the scene.
            /// </summary>
            /// <value>
            /// The estimated color correction value of the scene.
            /// </value>
            public Color m_ColorCorrection;

            /// <summary>
            /// The 4x4 projection matrix for the camera frame.
            /// </summary>
            /// <value>
            /// The 4x4 projection matrix for the camera frame.
            /// </value>
            public Matrix4x4 m_ProjectionMatrix;

            /// <summary>
            /// The 4x4 display matrix for the camera frame.
            /// </summary>
            /// <value>
            /// The 4x4 display matrix for the camera frame.
            /// </value>
            public Matrix4x4 m_DisplayMatrix;

            /// <summary>
            /// The <see cref="TrackingState"/> associated with the camera.
            /// </summary>
            /// <value>
            /// The tracking state associated with the camera.
            /// </value>
            public TrackingState m_TrackingState;

            /// <summary>
            /// A native pointer associated with this frame. The data
            /// pointed to by this pointer is specific to provider implementation.
            /// </summary>
            /// <value>
            /// The native pointer associated with this frame.
            /// </value>
            public IntPtr m_NativePtr;

            /// <summary>
            /// The set of all flags indicating which properties are included in the frame.
            /// </summary>
            /// <value>
            /// The set of all flags indicating which properties are included in the frame.
            /// </value>
            public XRCameraFrameProperties m_Properties;

            /// <summary>
            /// The estimated intensity, in lumens, of the scene.
            /// </summary>
            /// <value>
            /// The estimated intensity, in lumens, of the scene.
            /// </value>
            public float m_AverageIntensityInLumens;

            /// <summary>
            /// The camera exposure duration, in seconds with sub-millisecond precision, of the scene.
            /// </summary>
            /// <value>
            /// The camera exposure duration, in seconds with sub-millisecond precision, of the scene.
            /// </value>
            public double m_ExposureDuration;

            /// <summary>
            /// The camera exposure offset of the scene for lighting scaling
            /// </summary>
            /// <value>
            /// The camera exposure offset of the scene for lighting scaling
            /// </value>
            public float m_ExposureOffset;

            /// <summary>
            /// The estimated, intensity in lumens of the most influential, real-world light in the scene.
            /// </summary>
            /// <value>
            /// The estimated, intensity in lumens of the most influential, real-world light in the scene.
            /// </value>
            public float m_MainLightIntensityLumens;

            /// <summary>
            /// The estimated, color of the most influential, real-world light in the scene.
            /// </summary>
            /// <value>
            /// The estimated, color of the most influential, real-world light in the scene.
            /// </value>
            public Color m_MainLightColor;

            /// <summary>
            /// The estimated direction of the most influential, real-world light in the scene.
            /// </summary>
            /// <value>
            /// The estimated direction of the most influential, real-world light in the scene.
            /// </value>
            public Vector3 m_MainLightDirection;

            /// <summary>
            /// The ambient spherical harmonic coefficients that represent lighting in the real-world.
            /// </summary>
            /// <value>
            /// The ambient spherical harmonic coefficients that represent lighting in the real-world.
            /// </value>
            /// <remarks>
            /// See <see href="https://docs.unity3d.com/ScriptReference/Rendering.SphericalHarmonicsL2.html">here</see> for further details.
            /// </remarks>
            public SphericalHarmonicsL2 m_AmbientSphericalHarmonics;

            /// <summary>
            /// A texture that simulates the camera's noise.
            /// </summary>
            /// <value>
            /// A texture that simulates the camera's noise.
            /// </value>
            public XRTextureDescriptor m_CameraGrain;

            /// <summary>
            /// The level of intensity of camera grain noise in a scene.
            /// </summary>
            /// <value>
            /// The level of intensity of camera grain noise in a scene.
            /// </value>
            public float m_NoiseIntensity;

            public unsafe void Convert(out XRCameraFrame target)
            {
                fixed (XRCameraFrame* targetPtr = &target)
                fixed (XRCameraFrameMock* selfPtr = &this)
                {
                    UnsafeUtility.MemCpy(targetPtr, selfPtr, sizeof(XRCameraFrame));
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XRTextureDescriptorMock
        {
            /// <summary>
            /// A pointer to the native texture object.
            /// </summary>
            /// <value>
            /// A pointer to the native texture object.
            /// </value>
            public IntPtr m_NativeTexture;

            /// <summary>
            /// Specifies the width dimension of the native texture object.
            /// </summary>
            /// <value>
            /// The width of the native texture object.
            /// </value>
            public int m_Width;

            /// <summary>
            /// Specifies the height dimension of the native texture object.
            /// </summary>
            /// <value>
            /// The height of the native texture object.
            /// </value>
            public int m_Height;

            /// <summary>
            /// Specifies the number of mipmap levels in the native texture object.
            /// </summary>
            /// <value>
            /// The number of mipmap levels in the native texture object.
            /// </value>
            public int m_MipmapCount;

            /// <summary>
            /// Specifies the texture format of the native texture object.
            /// </summary>
            /// <value>
            /// The format of the native texture object.
            /// </value>
            public TextureFormat m_Format;

            /// <summary>
            /// Specifies the unique shader property name ID for the material shader texture.
            /// </summary>
            /// <value>
            /// The unique shader property name ID for the material shader texture.
            /// </value>
            /// <remarks>
            /// Use the static method <c>Shader.PropertyToID(string name)</c> to get the unique identifier.
            /// </remarks>
            public int m_PropertyNameId;

            /// <summary>
            /// This specifies the depth dimension of the native texture. For a 3D texture, depth would be greater than zero.
            /// For any other kind of valid texture, depth is one.
            /// </summary>
            /// <value>
            /// The depth dimension of the native texture object.
            /// </value>
            public int m_Depth;

            /// <summary>
            /// Specifies the [texture dimension](https://docs.unity3d.com/ScriptReference/Rendering.TextureDimension.html) of the native texture object.
            /// </summary>
            /// <value>
            /// The texture dimension of the native texture object.
            /// </value>
            public TextureDimension m_Dimension;

            public unsafe XRTextureDescriptor Convert()
            {
                var result = new XRTextureDescriptor();
                Convert(out result);
                return result;
            }

            public unsafe void Convert(out XRTextureDescriptor target)
            {
                fixed (XRTextureDescriptor* targetPtr = &target)
                fixed (XRTextureDescriptorMock* selfPtr = &this)
                {
                    UnsafeUtility.MemCpy(targetPtr, selfPtr, sizeof(XRTextureDescriptor));
                }
            }
        }
    }
}
