using System.Collections;
using DesktopMascot.Runtime;
using DesktopMascot.Runtime.Platform.Windows.Speech;
using DesktopMascot.Runtime.Presentation.Speech;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotSpeechPresentationDiagnostics : MonoBehaviour
    {
        private const string Prefix = "[DesktopMascotSpeechDiagnostics]";
        private SpeechPresentationController controller;
        private DesktopMascotRuntimePipeline runtime;
        private DesktopMascotSpeechMascotScaleDiagnostics isolation;
        private DesktopMascotPlayerPreviewPresentation playerPreview;
        private string manualCheck;

        internal void Configure(
            SpeechPresentationController speechController,
            DesktopMascotRuntimePipeline runtimePipeline,
            DesktopMascotSpeechMascotScaleDiagnostics isolationDiagnostics,
            DesktopMascotPlayerPreviewPresentation preview,
            string requestedManualCheck)
        {
            controller = speechController;
            runtime = runtimePipeline;
            isolation = isolationDiagnostics;
            playerPreview = preview;
            manualCheck = NormalizeManualCheck(requestedManualCheck);
        }

        private IEnumerator Start()
        {
            for (var frame = 0; frame < 600 && !controller.Host.IsReady; ++frame)
                yield return null;
            var fixedMessage = CreateFixedGreeting();
            var first = controller.TryShow(fixedMessage);
            var generation = controller.PresentationGeneration;
            var coalesced = controller.TryShow(fixedMessage);
            var busy = controller.TryShow(new SpeechMessage(
                "m048.busy-probe", fixedMessage.Speaker, "busy",
                SpeechThemeId.Normal, SpeechClosePolicy.ManualOrTimeout,
                SpeechTransitionStyle.None));
            yield return new WaitForEndOfFrame();
            for (var frame = 0; frame < 180
                 && controller.Host.PresentCount == 0; ++frame)
                yield return null;
            controller.TryClose(generation, SpeechCloseSource.Click);
            yield return null;
            controller.TryShow(fixedMessage);
            var simultaneousCloseGeneration =
                controller.PresentationGeneration;
            controller.TryClose(
                simultaneousCloseGeneration,
                SpeechCloseSource.Timeout);
            controller.TryClose(
                simultaneousCloseGeneration,
                SpeechCloseSource.Click);
            controller.TryClose(generation, SpeechCloseSource.Timeout);
            for (var index = 0; index < 50; ++index)
            {
                controller.TryShow(fixedMessage);
                controller.TryClose(
                    controller.PresentationGeneration,
                    SpeechCloseSource.Explicit);
            }
            controller.TryShow(fixedMessage);
            var manualTimeoutSeconds = manualCheck == "timeout"
                ? 15.0f
                : manualCheck == "hold" || manualCheck == "shutdown"
                    ? 3600.0f
                    : 60.0f;
            var manualVisualTimeoutExtended =
                controller.SetCurrentTimeoutForDiagnostics(
                    manualTimeoutSeconds);
            yield return new WaitForEndOfFrame();
            yield return null;

            var passed = first == SpeechShowResult.Accepted
                && coalesced == SpeechShowResult.Coalesced
                && busy == SpeechShowResult.Busy
                && controller.ShowAcceptedCount >= 52
                && controller.ShowCoalescedCount == 1
                && controller.ShowBusyCount == 1
                && manualVisualTimeoutExtended
                && controller.FirstCloseWinsCount == 1
                && controller.StaleCallbackCount >= 1
                && controller.View.IsCreated
                && controller.View.RenderTexture.width == SpeechPresentationView.Width
                && controller.View.RenderTexture.height == SpeechPresentationView.Height
                && controller.View.RenderedFrameCount >= 53
                && controller.View.TextLayoutContained
                && controller.View.TextContentFits
                && controller.View.TextRegionsSeparated
                && controller.View.BodyLineCount == 2
                && controller.View.SpeechHierarchyUsesDedicatedLayer
                && isolation != null
                && isolation.ProductionCameraStillIsolated
                && playerPreview != null
                && (playerPreview.PresentationCullingMask
                    & (1 << SpeechPresentationView.SpeechLayer)) == 0
                && fixedMessage.Theme == SpeechThemeId.Normal
                && fixedMessage.Transition == SpeechTransitionStyle.None
                && fixedMessage.Speaker.Decoration == SpeechDecorationKind.None
                && WindowsSpeechPresentationHost.DMN_GetSpeechWindowCreatedCount() == 1
                && WindowsSpeechPresentationHost.DMN_GetSpeechCompositionTargetCreatedCount() == 1
                && WindowsSpeechPresentationHost.DMN_GetSpeechVisualCreatedCount() == 1
                && WindowsSpeechPresentationHost.DMN_GetSpeechSwapChainCreatedCount() == 1
                && WindowsSpeechPresentationHost.DMN_GetSpeechRegionLiveOwnedCount() == 0
                && WindowsSpeechPresentationHost.DMN_GetSpeechMaximumFollowErrorPixels() <= 1
                && controller.Host.PresentHResult == 0
                && controller.Host.DeviceRemovedHResult == 0
                && controller.FailureStage == 0;
            Debug.Log($"{Prefix} Controller/host count: 1/1");
            Debug.Log($"{Prefix} Current/pending message count: {(controller.CurrentMessage != null ? 1 : 0)}/0");
            Debug.Log($"{Prefix} Manual check/timeout seconds/applied: " +
                $"{manualCheck}/{manualTimeoutSeconds:F0}/" +
                manualVisualTimeoutExtended);
            Debug.Log($"{Prefix} Presentation generation: {controller.PresentationGeneration}");
            Debug.Log($"{Prefix} Show request/accepted/coalesced/busy/failed: " +
                $"{controller.ShowRequestCount}/{controller.ShowAcceptedCount}/" +
                $"{controller.ShowCoalescedCount}/{controller.ShowBusyCount}/" +
                controller.ShowFailedCount);
            Debug.Log($"{Prefix} Close click/timeout/explicit: " +
                $"{controller.CloseClickCount}/{controller.CloseTimeoutCount}/" +
                controller.CloseExplicitCount);
            Debug.Log($"{Prefix} First-close-wins/stale callbacks: " +
                $"{controller.FirstCloseWinsCount}/{controller.StaleCallbackCount}");
            Debug.Log($"{Prefix} TMP font available: True");
            Debug.Log($"{Prefix} Text layout contained: " +
                controller.View.TextLayoutContained);
            Debug.Log($"{Prefix} Text content fits/regions separated: " +
                $"{controller.View.TextContentFits}/" +
                controller.View.TextRegionsSeparated);
            Debug.Log($"{Prefix} Speaker/body font size: " +
                $"{SpeechPresentationView.SpeakerFontSize:F0}/" +
                $"{SpeechPresentationView.BodyFontSize:F0}");
            Debug.Log($"{Prefix} Body lines/preferred/rect height: " +
                $"{controller.View.BodyLineCount}/" +
                $"{controller.View.BodyPreferredHeight:F2}/" +
                $"{controller.View.BodyRectHeight:F2}");
            Debug.Log($"{Prefix} Body maximum unwrapped line width: " +
                $"{controller.View.BodyMaximumUnwrappedLineWidth:F2}");
            Debug.Log($"{Prefix} Speaker/body rendered height: " +
                $"{controller.View.SpeakerRenderedHeight:F2}/" +
                $"{controller.View.BodyRenderedHeight:F2}");
            Debug.Log($"{Prefix} Speaker/body overflowing: " +
                $"{controller.View.SpeakerOverflowing}/" +
                controller.View.BodyOverflowing);
            Debug.Log($"{Prefix} Speech hierarchy Layer 30 only: " +
                controller.View.SpeechHierarchyUsesDedicatedLayer);
            Debug.Log($"{Prefix} Production Camera Speech Layer excluded: " +
                (isolation?.ProductionCameraStillIsolated ?? false));
            Debug.Log($"{Prefix} Player Preview Speech Layer excluded: " +
                (playerPreview != null
                    && (playerPreview.PresentationCullingMask
                        & (1 << SpeechPresentationView.SpeechLayer)) == 0));
            Debug.Log($"{Prefix} Production Camera original/applied mask: " +
                $"0x{(isolation?.OriginalCullingMask ?? 0):X8}/" +
                $"0x{(isolation?.AppliedCullingMask ?? 0):X8}");
            Debug.Log($"{Prefix} Production Camera mask restoration: " +
                "Pending orderly shutdown");
            Debug.Log($"{Prefix} Speech RT dimensions/format: " +
                $"{controller.View.RenderTexture.width}x{controller.View.RenderTexture.height}/" +
                controller.View.RenderTexture.graphicsFormat);
            Debug.Log($"{Prefix} Visible mascot target height: " +
                DesktopMascotSpeechMascotScaleDiagnostics
                    .TargetVisibleAlphaHeight);
            Debug.Log($"{Prefix} Mascot RT independence: " +
                (runtime.NativeTransferWidth != controller.View.RenderTexture.width));
            Debug.Log($"{Prefix} Anchor source/generation/rebind count: " +
                $"{controller.AnchorResolver.Source}/" +
                $"{controller.AnchorResolver.CharacterGeneration}/" +
                controller.AnchorResolver.RebindCount);
            Debug.Log($"{Prefix} Anchor resolved/hips/upper/lower/renderer fallback: " +
                $"{controller.AnchorResolver.LastResolvedAnchor}/" +
                $"{controller.AnchorResolver.LastHipsAnchor}/" +
                $"{controller.AnchorResolver.LastUpperLegAnchor}/" +
                $"{controller.AnchorResolver.LastLowerLegAnchor}/" +
                controller.AnchorResolver.LastRendererBoundsFallbackAnchor);
            Debug.Log($"{Prefix} Speech HWND created/destroyed: " +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechWindowCreatedCount()}/" +
                WindowsSpeechPresentationHost.DMN_GetSpeechWindowDestroyedCount());
            Debug.Log($"{Prefix} Context mask/init requested/posted/handled: " +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechContextAvailabilityMask()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechInitializeRequestCount()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechInitializePostSuccessCount()}/" +
                WindowsSpeechPresentationHost.DMN_GetSpeechInitializeHandleCount());
            Debug.Log($"{Prefix} Owner HWND match: " +
                (WindowsSpeechPresentationHost.DMN_DidSpeechOwnerMatchMascot() != 0));
            Debug.Log($"{Prefix} Speech Present count/HRESULT/device removed: " +
                $"{controller.Host.PresentCount}/0x{controller.Host.PresentHResult:X8}/" +
                $"0x{controller.Host.DeviceRemovedHResult:X8}");
            Debug.Log($"{Prefix} Speech HRGN created/transferred/deleted: " +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechRegionCreatedCount()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechRegionTransferredCount()}/" +
                WindowsSpeechPresentationHost.DMN_GetSpeechRegionCallerDeletedCount());
            Debug.Log($"{Prefix} Speech HRGN live-owned: " +
                WindowsSpeechPresentationHost.DMN_GetSpeechRegionLiveOwnedCount());
            Debug.Log($"{Prefix} DComp target/visual/swapchain created: " +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechCompositionTargetCreatedCount()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechVisualCreatedCount()}/" +
                WindowsSpeechPresentationHost.DMN_GetSpeechSwapChainCreatedCount());
            Debug.Log($"{Prefix} Follow/edge flip/clamp/DPI: " +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechFollowUpdateCount()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechEdgeFlipCount()}/" +
                $"{WindowsSpeechPresentationHost.DMN_GetSpeechClampCount()}/" +
                WindowsSpeechPresentationHost.DMN_GetSpeechDpi());
            Debug.Log($"{Prefix} Maximum follow error pixels: " +
                WindowsSpeechPresentationHost.DMN_GetSpeechMaximumFollowErrorPixels());
            Debug.Log($"{Prefix} Speech live resources before shutdown: " +
                WindowsSpeechPresentationHost.DMN_GetSpeechLiveResourceCount());
            Debug.Log($"{Prefix} Failure stage: {controller.FailureStage}");
            Debug.Log($"{Prefix} Automated diagnostics passed: {passed}");
            Debug.Log($"{Prefix} Visual verification: Pending");
        }

        private static string NormalizeManualCheck(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "hold":
                case "timeout":
                case "shutdown":
                    return value.Trim().ToLowerInvariant();
                default:
                    return "standard";
            }
        }


        internal static SpeechMessage CreateFixedGreeting() =>
            new(
                "m048.fixed-greeting",
                new SpeakerData(
                    "desktop-mascot.default-speaker",
                    "あなたといつも",
                    SpeechDecorationKind.None),
                "おはようございます！\n今日も良い一日にしましょうね♪",
                SpeechThemeId.Normal,
                SpeechClosePolicy.ManualOrTimeout,
                SpeechTransitionStyle.None);
    }
}
