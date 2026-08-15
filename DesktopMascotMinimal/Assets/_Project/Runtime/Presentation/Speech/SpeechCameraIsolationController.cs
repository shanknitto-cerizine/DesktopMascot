using UnityEngine;

namespace DesktopMascot.Runtime.Presentation.Speech
{
    // Sole owner of the production Camera culling-mask mutation for Speech Layer 30.
    internal sealed class SpeechCameraIsolationController : MonoBehaviour
    {
        private Camera targetCamera;
        private int originalCullingMask;
        private int appliedCullingMask;
        private bool configured;
        private bool restoreAttempted;
        private bool restoreSucceeded;

        internal int OriginalCullingMask => originalCullingMask;
        internal int AppliedCullingMask => appliedCullingMask;
        internal bool Configured => configured;
        internal bool RestoreSucceeded => restoreSucceeded;
        internal bool SpeechLayerExcluded => configured
            && (appliedCullingMask & (1 << SpeechPresentationView.SpeechLayer)) == 0;

        internal bool Configure(Camera camera)
        {
            if (configured || restoreAttempted || camera == null)
                return false;

            targetCamera = camera;
            originalCullingMask = camera.cullingMask;
            appliedCullingMask = originalCullingMask
                & ~(1 << SpeechPresentationView.SpeechLayer);
            camera.cullingMask = appliedCullingMask;
            configured = camera.cullingMask == appliedCullingMask;
            if (!configured)
                Restore();
            return configured;
        }

        internal bool Restore()
        {
            if (restoreAttempted)
                return restoreSucceeded;
            restoreAttempted = true;
            if (targetCamera == null)
                return false;
            targetCamera.cullingMask = originalCullingMask;
            restoreSucceeded = targetCamera.cullingMask == originalCullingMask;
            return restoreSucceeded;
        }

        private void OnDestroy() => Restore();
    }
}
