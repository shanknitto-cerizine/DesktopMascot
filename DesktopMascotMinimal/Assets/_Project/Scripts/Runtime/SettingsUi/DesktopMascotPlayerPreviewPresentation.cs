using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class DesktopMascotPlayerPreviewPresentation :
        MonoBehaviour,
        ISettingsPlayerPresentationSource
    {
        private Camera sourceCamera;
        private Camera previewCamera;
        private int presentationFrame = -1;
        private bool initialized;
        private bool disposed;

        public int PresentationFrame => presentationFrame;
        public int PresentationWidth =>
            previewCamera != null ? previewCamera.pixelWidth : 0;
        public int PresentationHeight =>
            previewCamera != null ? previewCamera.pixelHeight : 0;
        public int RenderTextureAllocationCount => 0;
        public bool CleanupComplete => disposed;

        internal bool Initialize(Camera source)
        {
            if (initialized || source == null)
                return false;
            sourceCamera = source;
            previewCamera = gameObject.AddComponent<Camera>();
            previewCamera.CopyFrom(source);
            previewCamera.targetTexture = null;
            previewCamera.enabled = true;
            SyncTransform();
            initialized = true;
            return true;
        }

        private void LateUpdate()
        {
            if (!disposed)
                SyncTransform();
        }

        private void OnPostRender()
        {
            if (!disposed && previewCamera != null)
                presentationFrame = Time.frameCount;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (previewCamera != null)
            {
                previewCamera.enabled = false;
                Destroy(previewCamera);
                previewCamera = null;
            }
            sourceCamera = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void SyncTransform()
        {
            if (sourceCamera == null)
                return;
            transform.SetPositionAndRotation(
                sourceCamera.transform.position,
                sourceCamera.transform.rotation);
            transform.localScale = sourceCamera.transform.lossyScale;
        }
    }
}
