#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kCompositionClickThroughMessage =
        0x8000u + 0x46u;

    enum class CompositionClickThroughState : std::int32_t
    {
        NotStarted = 0,
        Ready = 1,
        EnableRequested = 2,
        EnableMessagePosted = 3,
        EnableMessageReceived = 4,
        Enabled = 5,
        DisableRequested = 6,
        DisableMessagePosted = 7,
        DisableMessageReceived = 8,
        Disabled = 9,
        Completed = 10,
        ShutdownRequested = 11,
        Stopped = 12,
        Failed = 13
    };

    enum class CompositionClickThroughFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        ShutdownAlreadyRequested = 3,
        RequestAlreadyPending = 4,
        InvalidEnabledValue = 5,
        PostMessageFailed = 6,
        MessageSequenceMismatch = 7,
        GetInitialExtendedStyleFailed = 8,
        SetExtendedStyleFailed = 9,
        GetAppliedExtendedStyleFailed = 10,
        ExtendedStyleVerificationFailed = 11,
        SetWindowPosFrameChangedFailed = 12,
        EnableTimeout = 13,
        DisableTimeout = 14,
        UnexpectedRequestCount = 15,
        UnexpectedAppliedCount = 16,
        ShutdownFailed = 17,
        LayeredWindowClassStyleUnsupported = 18,
        LayeredStyleApplyFailed = 19,
        LayeredTransparentStyleVerificationFailed = 20,
        InitialExtendedStyleRestoreFailed = 21,
        RunInBackgroundVerificationFailed = 22
    };

    void ResetCompositionClickThroughDiagnostics();
    void SetCompositionClickThroughUiWindow(void* window);
    void HandleCompositionClickThroughMessage(std::uint32_t sequence);
    bool ShouldCompositionHitTestBeTransparent();
    void NotifyCompositionClickThroughShutdownRequested();
    void StopCompositionClickThroughDiagnosticsOnUiThread();

    std::int32_t StartCompositionClickThroughDiagnostics();
    std::int32_t RequestCompositionClickThroughEnabled(
        std::int32_t enabled);
    std::int32_t GetCompositionClickThroughState();
    std::int32_t GetCompositionClickThroughFailureStage();
    bool IsCompositionClickThroughEnabled();
    bool IsCompositionClickThroughRequestPending();
    bool DidCompositionClickThroughLastRequestSucceed();
    std::uint32_t GetCompositionClickThroughEnableRequestCount();
    std::uint32_t GetCompositionClickThroughDisableRequestCount();
    std::uint32_t GetCompositionClickThroughAppliedCount();
    std::uint32_t GetCompositionClickThroughRejectedCount();
    std::uint32_t GetCompositionClickThroughLastWin32Error();
    std::uint64_t GetCompositionWindowInitialStyle();
    std::uint64_t GetCompositionWindowCurrentStyle();
    std::uint64_t GetCompositionWindowInitialExtendedStyle();
    std::uint64_t GetCompositionWindowCurrentExtendedStyle();
    std::uint64_t GetCompositionWindowClassStyle();
    bool IsCompositionInitialExtendedStyleRestored();
    std::uint32_t GetCompositionClickThroughHitTestCount();
    std::uint32_t GetCompositionClickThroughTransparentHitTestCount();
    std::uint32_t GetCompositionClickThroughLastRequestThreadId();
    std::uint32_t GetCompositionClickThroughLastAppliedThreadId();
}
