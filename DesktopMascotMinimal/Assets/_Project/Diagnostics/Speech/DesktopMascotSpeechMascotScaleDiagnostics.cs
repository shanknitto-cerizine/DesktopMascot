using DesktopMascot.Character;
using DesktopMascot.Runtime.Presentation.Speech;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotSpeechMascotScaleDiagnostics : MonoBehaviour
    {
        // Renderer bounds include invisible skinned-mesh padding. The previous
        // 340-pixel target filled the alpha silhouette to both 256-pixel edges.
        // A 260-pixel projected-bounds target calibrates the bundled diagnostic
        // character to about 224 visible pixels, preserving roughly 16 pixels
        // of head/foot safety margin without changing Character scale.
        private const int TargetProjectedHeight = 260;
        internal const int TargetVisibleAlphaHeight = 224;
        private Camera targetCamera;
        private float originalFieldOfView;
        private int originalCullingMask;
        private int appliedCullingMask;
        private int endingCullingMask;
        private bool configured;
        private bool restoreAttempted;
        private bool restoreSucceeded;

        internal int ProjectedHeightBefore { get; private set; }
        internal int ProjectedHeightAfter { get; private set; }
        internal float OriginalFieldOfView => originalFieldOfView;
        internal float AppliedFieldOfView =>
            targetCamera != null ? targetCamera.fieldOfView : 0.0f;
        internal int OriginalCullingMask => originalCullingMask;
        internal int AppliedCullingMask => appliedCullingMask;
        internal int EndingCullingMask => endingCullingMask;
        internal bool SpeechLayerExcluded => configured
            && (appliedCullingMask & (1 << SpeechPresentationView.SpeechLayer)) == 0;
        internal bool ProductionCameraStillIsolated =>
            targetCamera != null
            && targetCamera.cullingMask == appliedCullingMask
            && SpeechLayerExcluded;
        internal bool RestoreAttempted => restoreAttempted;
        internal bool RestoreSucceeded => restoreSucceeded;

        internal bool Configure(
            Camera camera,
            CharacterAssetManager characterManager)
        {
            if (camera == null || camera.orthographic
                || characterManager?.ActiveCharacter?.Root == null)
                return false;

            targetCamera = camera;
            originalFieldOfView = camera.fieldOfView;
            originalCullingMask = camera.cullingMask;
            appliedCullingMask = originalCullingMask
                & ~(1 << SpeechPresentationView.SpeechLayer);
            camera.cullingMask = appliedCullingMask;
            ProjectedHeightBefore = MeasureProjectedHeight(
                camera,
                characterManager.ActiveCharacter.Root);
            if (ProjectedHeightBefore <= 0)
            {
                Restore();
                return false;
            }

            var tangent = Mathf.Tan(originalFieldOfView * 0.5f * Mathf.Deg2Rad);
            var adjustedTangent = tangent
                * ProjectedHeightBefore / TargetProjectedHeight;
            camera.fieldOfView = Mathf.Clamp(
                2.0f * Mathf.Atan(adjustedTangent) * Mathf.Rad2Deg,
                15.0f,
                originalFieldOfView);
            ProjectedHeightAfter = MeasureProjectedHeight(
                camera,
                characterManager.ActiveCharacter.Root);
            configured = camera.cullingMask == appliedCullingMask;
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Diagnostic mascot framing " +
                $"projected height before/after/target: " +
                $"{ProjectedHeightBefore}/{ProjectedHeightAfter}/" +
                TargetProjectedHeight);
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Diagnostic mascot framing " +
                $"FOV before/after: {originalFieldOfView:F2}/" +
                $"{camera.fieldOfView:F2}");
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Production Camera " +
                $"culling mask original/applied: " +
                $"0x{originalCullingMask:X8}/0x{appliedCullingMask:X8}");
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Production Camera " +
                $"Speech Layer excluded: {SpeechLayerExcluded}");
            return configured;
        }

        internal bool Restore()
        {
            if (restoreAttempted)
                return restoreSucceeded;
            restoreAttempted = true;
            if (targetCamera == null)
            {
                endingCullingMask = 0;
                restoreSucceeded = false;
                return false;
            }
            targetCamera.fieldOfView = originalFieldOfView;
            targetCamera.cullingMask = originalCullingMask;
            endingCullingMask = targetCamera.cullingMask;
            restoreSucceeded = endingCullingMask == originalCullingMask
                && Mathf.Approximately(
                    targetCamera.fieldOfView,
                    originalFieldOfView);
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Production Camera " +
                $"culling mask original/ending: " +
                $"0x{originalCullingMask:X8}/0x{endingCullingMask:X8}");
            Debug.Log(
                "[DesktopMascotSpeechDiagnostics] Production Camera " +
                $"culling mask restored: {restoreSucceeded}");
            return restoreSucceeded;
        }

        private void OnDestroy() => Restore();

        private static int MeasureProjectedHeight(Camera camera, GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                return 0;
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; ++index)
                bounds.Encapsulate(renderers[index].bounds);

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = bounds.center + Vector3.Scale(
                    bounds.extents,
                    new Vector3(x, y, z));
                var viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z <= 0.0f)
                    continue;
                minY = Mathf.Min(minY, viewport.y);
                maxY = Mathf.Max(maxY, viewport.y);
            }
            return float.IsFinite(minY) && float.IsFinite(maxY)
                ? Mathf.RoundToInt((maxY - minY) * 256.0f)
                : 0;
        }
    }
}
