using System.Collections;
using DesktopMascot.Runtime;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotSettingsUiPresentationDiagnostics :
        MonoBehaviour
    {
        private const string Prefix =
            "[DesktopMascotSettingsUiPresentationDiagnostics]";

        private SettingsWindowController controller;
        private ISettingsPlayerPresentationSource presentation;
        private DesktopMascotRuntimePipeline runtime;
        private ISettingsPresentationHost presentationHost;

        internal static bool Passed { get; private set; }

        internal void Configure(
            SettingsWindowController value,
            ISettingsPlayerPresentationSource playerPresentation,
            DesktopMascotRuntimePipeline runtimePipeline,
            ISettingsPresentationHost host)
        {
            controller = value;
            presentation = playerPresentation;
            runtime = runtimePipeline;
            presentationHost = host;
            Passed = false;
        }

        private IEnumerator Start()
        {
            for (var frame = 0;
                 frame < 600
                 && (controller == null
                     || controller.LastPresentedFrame < 0);
                 ++frame)
            {
                yield return null;
            }
            if (controller == null || controller.LastPresentedFrame < 0)
            {
                LogFinal(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false);
                yield break;
            }

            var closedBefore =
                controller.PresentationAdvanceWhileClosedCount;
            yield return WaitForFreshFrames(8);
            var freshBeforeOpen =
                controller.PresentationAdvanceWhileClosedCount
                    > closedBefore;

            var openSucceeded =
                presentationHost != null
                && presentationHost.ShowSettings();
            var openFrame = presentation.PresentationFrame;
            var presentBeforeOpen = runtime?.PresentCount ?? 0;
            yield return WaitForFreshFrames(8);
            var playerPreviewPausedWhileOpen =
                presentation.PresentationFrame == openFrame;
            var backgroundOpaque =
                controller.BackgroundOpaque;
            var settingsOnlyRenderingActive =
                presentation.SettingsOnlyRenderingActive;
            var characterHidden =
                presentation.CharacterHiddenFromPlayerSurface;
            var directCompositionContinues =
                runtime != null
                && runtime.PresentCount > presentBeforeOpen;

            controller.Binding.FirstRunCompleted =
                !controller.Binding.FirstRunCompleted;
            controller.Cancel();
            var closedAfterCancel =
                controller.PresentationAdvanceWhileClosedCount;
            yield return WaitForFreshFrames(8);
            var freshAfterCancel =
                controller.PresentationAdvanceWhileClosedCount
                    > closedAfterCancel;

            presentationHost.ShowSettings();
            var reopenedInteractive =
                controller.IsOpen
                && !controller.Binding.IsDirty
                && controller.Binding.Validation.IsValid;
            controller.Close();
            var closedAfterClose =
                controller.PresentationAdvanceWhileClosedCount;
            yield return WaitForFreshFrames(8);
            var freshAfterClose =
                controller.PresentationAdvanceWhileClosedCount
                    > closedAfterClose;
            var fullQualityResolution =
                presentation != null
                && presentation.PresentationWidth == Screen.width
                && presentation.PresentationHeight == Screen.height
                && (Screen.width <= 256
                    && Screen.height <= 256
                    || presentation.PresentationWidth > 256
                    || presentation.PresentationHeight > 256);
            var nativeTransferRemains256 =
                runtime != null
                && runtime.NativeTransferWidth == 256
                && runtime.NativeTransferHeight == 256;
            var noPreviewRenderTextureAllocations =
                presentation != null
                && presentation.RenderTextureAllocationCount == 0;
            var hostInitialized =
                presentationHost != null
                && presentationHost.IsInitialized
                && presentationHost.PresentationFailureStage == 0;

            Passed =
                freshBeforeOpen
                && openSucceeded
                && playerPreviewPausedWhileOpen
                && backgroundOpaque
                && settingsOnlyRenderingActive
                && characterHidden
                && directCompositionContinues
                && freshAfterCancel
                && freshAfterClose
                && reopenedInteractive
                && fullQualityResolution
                && nativeTransferRemains256
                && noPreviewRenderTextureAllocations
                && hostInitialized;
            LogFinal(
                freshBeforeOpen,
                playerPreviewPausedWhileOpen,
                freshAfterCancel,
                freshAfterClose,
                reopenedInteractive,
                backgroundOpaque,
                settingsOnlyRenderingActive,
                characterHidden,
                directCompositionContinues,
                fullQualityResolution,
                nativeTransferRemains256,
                noPreviewRenderTextureAllocations,
                hostInitialized);
        }

        private IEnumerator WaitForFreshFrames(int count)
        {
            for (var frame = 0; frame < count; ++frame)
            {
                yield return new WaitForEndOfFrame();
                controller?.ObservePresentationFrameForDiagnostics();
            }
        }

        private void LogFinal(
            bool freshBeforeOpen,
            bool playerPreviewPausedWhileOpen,
            bool freshAfterCancel,
            bool freshAfterClose,
            bool reopenedInteractive,
            bool backgroundOpaque,
            bool settingsOnlyRenderingActive,
            bool characterHidden,
            bool directCompositionContinues,
            bool fullQualityResolution = false,
            bool nativeTransferRemains256 = false,
            bool noPreviewRenderTextureAllocations = false,
            bool hostInitialized = false)
        {
            Debug.Log(
                $"{Prefix} Fresh before open: {freshBeforeOpen}");
            Debug.Log(
                $"{Prefix} Player preview paused while Settings open: " +
                playerPreviewPausedWhileOpen);
            Debug.Log(
                $"{Prefix} Fresh after Cancel: {freshAfterCancel}");
            Debug.Log(
                $"{Prefix} Fresh after Close: {freshAfterClose}");
            Debug.Log(
                $"{Prefix} Reopened and interactive: {reopenedInteractive}");
            Debug.Log(
                $"{Prefix} Open/closed frame advances: " +
                $"{controller?.PresentationAdvanceWhileOpenCount ?? 0}/" +
                $"{controller?.PresentationAdvanceWhileClosedCount ?? 0}");
            Debug.Log(
                $"{Prefix} Settings presentation host initialized: " +
                hostInitialized);
            Debug.Log(
                $"{Prefix} Settings background opaque: " +
                backgroundOpaque);
            Debug.Log(
                $"{Prefix} Settings-only rendering active: " +
                settingsOnlyRenderingActive);
            Debug.Log(
                $"{Prefix} Character hidden from Player Settings surface: " +
                characterHidden);
            Debug.Log(
                $"{Prefix} DirectComposition mascot continues rendering: " +
                directCompositionContinues);
            Debug.Log(
                $"{Prefix} Close/Cancel count: " +
                $"{controller?.CloseCount ?? 0}/" +
                $"{controller?.CancelCount ?? 0}");
            Debug.Log(
                $"{Prefix} Presentation failure stage: " +
                (presentationHost?.PresentationFailureStage ?? -1));
            Debug.Log(
                $"{Prefix} Player preview resolution: " +
                $"{presentation?.PresentationWidth ?? 0} x " +
                $"{presentation?.PresentationHeight ?? 0}");
            Debug.Log(
                $"{Prefix} Full-quality Player resolution: " +
                fullQualityResolution);
            Debug.Log(
                $"{Prefix} Native transfer remains 256 x 256: " +
                nativeTransferRemains256);
            Debug.Log(
                $"{Prefix} Preview RenderTexture allocations: " +
                $"{presentation?.RenderTextureAllocationCount ?? -1}");
            Debug.Log(
                $"{Prefix} No per-frame preview allocation: " +
                noPreviewRenderTextureAllocations);
            Debug.Log($"{Prefix} Automated test passed: {Passed}");
        }
    }
}
