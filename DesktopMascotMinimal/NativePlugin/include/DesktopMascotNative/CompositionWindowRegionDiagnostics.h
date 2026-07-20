#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kCompositionWindowRegionApplyMessage =
        0x8000u + 0x49u;
    constexpr std::uint32_t kCompositionWindowRegionRestoreMessage =
        0x8000u + 0x4Au;

    enum class CompositionWindowRegionState : std::int32_t
    {
        NotStarted = 0,
        WaitingForComposition = 1,
        WaitingForMask = 2,
        ApplyRequested = 3,
        VisualTestRunning = 4,
        Completed = 5,
        RestoreRequested = 6,
        Stopped = 7,
        Failed = 8
    };

    enum class CompositionWindowRegionFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        AlphaMaskUnavailable = 3,
        InvalidThreshold = 4,
        ExistingTransparentStyleUnsupported = 5,
        GetInitialExtendedStyleFailed = 6,
        ApplyLayeredStyleFailed = 7,
        GetInitialRegionFailed = 8,
        SnapshotCopyFailed = 9,
        CreateRegionFailed = 10,
        CombineRegionFailed = 11,
        RepresentativeValidationFailed = 12,
        SetWindowRegionFailed = 13,
        VerifyWindowRegionFailed = 14,
        RestoreInitialRegionFailed = 15,
        RestoreInitialStyleFailed = 16,
        RedrawFailed = 17,
        DiagnosticsTimeout = 18,
        ContinuousPresentFailed = 19,
        ShutdownFailed = 20
    };

    void ResetCompositionWindowRegionDiagnostics();
    void SetCompositionWindowRegionUiWindow(void* window);
    void HandleCompositionWindowRegionApplyMessage();
    void HandleCompositionWindowRegionRestoreMessage();
    void StopCompositionWindowRegionDiagnosticsOnUiThread();
    bool ShouldCompositionWindowRegionReturnClient();

    std::int32_t StartCompositionWindowRegionDiagnostics(
        std::int32_t alphaThreshold);
    std::int32_t CompleteCompositionWindowRegionDiagnostics();
    std::int32_t StopCompositionWindowRegionDiagnostics();
    std::int32_t GetCompositionWindowRegionState();
    std::int32_t GetCompositionWindowRegionFailureStage();
    std::int32_t GetCompositionWindowRegionThreshold();
    bool IsCompositionWindowRegionApplied();
    std::int32_t GetCompositionWindowRegionType();
    std::uint32_t GetCompositionWindowRegionRectangleCount();
    std::uint32_t GetCompositionWindowRegionCoveredPixelCount();
    std::uint32_t GetCompositionWindowRegionExcludedPixelCount();
    std::int32_t GetCompositionWindowInitialRegionType();
    bool DidCompositionWindowInitialRegionRestoreSucceed();
    bool IsCompositionWindowRegionApplyRequestPending();
    bool DidCompositionWindowRegionLastApplySucceed();
    std::uint32_t GetCompositionWindowRegionLastWin32Error();
    std::uint64_t GetCompositionWindowRegionPublishedGeneration();
    bool IsCompositionWindowRegionTopLeftInside();
    bool IsCompositionWindowRegionTopRightInside();
    bool IsCompositionWindowRegionBottomLeftInside();
    bool IsCompositionWindowRegionBottomRightInside();
    bool IsCompositionWindowRegionCenterInside();
    std::uint64_t GetCompositionWindowRegionInitialExtendedStyle();
    std::uint64_t GetCompositionWindowRegionDiagnosticExtendedStyle();
    std::uint64_t GetCompositionWindowRegionCurrentExtendedStyle();
    bool DidCompositionWindowRegionStyleRestoreSucceed();
}
