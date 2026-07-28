using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Runtime
{
    internal sealed class DesktopMascotCameraSourcePipeline : IDisposable
    {
        internal const GraphicsFormat TransferFormat =
            GraphicsFormat.B8G8R8A8_SRGB;
        internal const bool UsesShaderSideInversion = false;

        private Camera camera;
        private RenderTexture originalTarget;
        private CameraClearFlags originalClearFlags;
        private Color originalBackground;
        private bool originalHdr;
        private bool originalMsaa;
        private int lastNormalizedFrame = -1;
        private bool disposed;
        private bool cameraTargetTextureRestored;
        private RenderTexture cameraSourceTexture;
        private RenderTexture normalizedTransferTexture;

        internal RenderTexture CameraSourceTexture => cameraSourceTexture;
        internal RenderTexture NormalizedTransferTexture =>
            normalizedTransferTexture;
        internal bool CameraTargetTextureRestored =>
            cameraTargetTextureRestored;

        internal static bool TryCreate(
            Camera sourceCamera,
            int width,
            int height,
            string cameraTextureName,
            string transferTextureName,
            out DesktopMascotCameraSourcePipeline pipeline)
        {
            pipeline = null;
            if (sourceCamera == null || width != 256 || height != 256
                || !SystemInfo.IsFormatSupported(
                    TransferFormat, GraphicsFormatUsage.Render))
                return false;

            var value = new DesktopMascotCameraSourcePipeline();
            try
            {
                value.camera = sourceCamera;
                value.originalTarget = sourceCamera.targetTexture;
                value.originalClearFlags = sourceCamera.clearFlags;
                value.originalBackground = sourceCamera.backgroundColor;
                value.originalHdr = sourceCamera.allowHDR;
                value.originalMsaa = sourceCamera.allowMSAA;

                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    graphicsFormat = TransferFormat,
                    depthStencilFormat =
                        GraphicsFormat.D24_UNorm_S8_UInt,
                    msaaSamples = 1,
                    volumeDepth = 1,
                    dimension = TextureDimension.Tex2D,
                    mipCount = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                value.cameraSourceTexture = new RenderTexture(descriptor)
                {
                    name = cameraTextureName
                };
                value.cameraSourceTexture.Create();
                descriptor.depthStencilFormat = GraphicsFormat.None;
                value.normalizedTransferTexture =
                    new RenderTexture(descriptor)
                    {
                        name = transferTextureName
                    };
                value.normalizedTransferTexture.Create();
                if (!value.cameraSourceTexture.IsCreated()
                    || !value.normalizedTransferTexture.IsCreated())
                {
                    value.Dispose();
                    return false;
                }

                sourceCamera.targetTexture = value.cameraSourceTexture;
                sourceCamera.clearFlags = CameraClearFlags.SolidColor;
                sourceCamera.backgroundColor = new Color(0, 0, 0, 0);
                sourceCamera.allowHDR = false;
                sourceCamera.allowMSAA = false;
                pipeline = value;
                return true;
            }
            catch
            {
                value.Dispose();
                throw;
            }
        }

        internal bool Normalize(int renderedFrame)
        {
            if (disposed || CameraSourceTexture == null
                || NormalizedTransferTexture == null)
                return false;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Assert(
                CameraSourceTexture != NormalizedTransferTexture,
                "Camera source and normalized transfer textures must differ.");
            Debug.Assert(
                !UsesShaderSideInversion,
                "Camera normalization must not be duplicated in a shader.");
            if (lastNormalizedFrame == renderedFrame)
            {
                Debug.LogError(
                    "[DesktopMascotRuntime] Camera normalization was requested more than once for one rendered update.");
                return false;
            }
#endif
            lastNormalizedFrame = renderedFrame;
            // Camera-source normalization is intentionally applied exactly
            // once at this boundary. The downstream D3D12, readback, mask,
            // region, composition, and IMGUI preview orientation behavior
            // remains unchanged.
            Graphics.Blit(
                CameraSourceTexture,
                NormalizedTransferTexture,
                new Vector2(1.0f, -1.0f),
                new Vector2(0.0f, 1.0f));
            return true;
        }

        internal IntPtr GetNormalizedNativeTexturePointer()
        {
            return disposed || NormalizedTransferTexture == null
                ? IntPtr.Zero
                : NormalizedTransferTexture.GetNativeTexturePtr();
        }

        internal bool IsNormalizedTransfer(RenderTexture texture)
        {
            return !disposed
                && ReferenceEquals(texture, NormalizedTransferTexture);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (camera != null)
            {
                camera.targetTexture = originalTarget;
                cameraTargetTextureRestored =
                    camera.targetTexture == originalTarget;
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackground;
                camera.allowHDR = originalHdr;
                camera.allowMSAA = originalMsaa;
            }
            ReleaseTexture(ref normalizedTransferTexture);
            ReleaseTexture(ref cameraSourceTexture);
            camera = null;
            originalTarget = null;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
