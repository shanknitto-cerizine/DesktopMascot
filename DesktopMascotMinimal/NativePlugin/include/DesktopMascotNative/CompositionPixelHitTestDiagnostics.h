#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kCompositionPixelHitTestApplyMessage =
        0x8000u + 0x47u;
    constexpr std::uint32_t kCompositionPixelHitTestRestoreMessage =
        0x8000u + 0x48u;

    enum class PixelHitTestResult : std::int32_t
    {
        None = 0,
        Transparent = 1,
        Opaque = 2,
        OutsideClient = 3,
        MaskUnavailable = 4,
        CoordinateConversionFailed = 5
    };

    enum class CompositionPixelHitTestState : std::int32_t
    {
        NotStarted = 0,
        WaitingForComposition = 1,
        WaitingForMask = 2,
        ApplyingWindowStyle = 3,
        ReadyForVisualTest = 4,
        VisualTestRunning = 5,
        Completed = 6,
        RestoreRequested = 7,
        Stopped = 8,
        Failed = 9
    };

    enum class CompositionPixelHitTestFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        AlphaMaskUnavailable = 3,
        InvalidThreshold = 4,
        ExistingTransparentStyleUnsupported = 5,
        LayeredWindowClassStyleUnsupported = 6,
        GetInitialExtendedStyleFailed = 7,
        ApplyLayeredStyleFailed = 8,
        VerifyLayeredStyleFailed = 9,
        ScreenToClientFailed = 10,
        SnapshotReadFailed = 11,
        UnexpectedAlphaValue = 12,
        UnexpectedHitTestResult = 13,
        RestoreInitialStyleFailed = 14,
        ContinuousPresentFailed = 15,
        DiagnosticsTimeout = 16,
        ShutdownFailed = 17
    };

    void ResetCompositionPixelHitTestDiagnostics();
    void SetCompositionPixelHitTestUiWindow(void* window);
    void HandleCompositionPixelHitTestApplyMessage();
    void HandleCompositionPixelHitTestRestoreMessage();
    bool TryHandleCompositionPixelHitTest(
        void* window,
        std::intptr_t lParam,
        std::intptr_t& result);
    void StopCompositionPixelHitTestDiagnosticsOnUiThread();

    std::int32_t StartCompositionPixelHitTestDiagnostics(
        std::int32_t alphaThreshold);
    std::int32_t CompleteCompositionPixelHitTestDiagnostics();
    std::int32_t StopCompositionPixelHitTestDiagnostics();
    std::int32_t GetCompositionPixelHitTestState();
    std::int32_t GetCompositionPixelHitTestFailureStage();
    bool IsCompositionPixelHitTestEnabled();
    std::int32_t GetCompositionPixelHitTestThreshold();
    std::uint32_t GetCompositionPixelHitTestTotalCount();
    std::uint32_t GetCompositionPixelHitTestTransparentCount();
    std::uint32_t GetCompositionPixelHitTestOpaqueCount();
    std::uint32_t GetCompositionPixelHitTestOutsideClientCount();
    std::uint32_t GetCompositionPixelHitTestMaskUnavailableCount();
    std::uint32_t GetCompositionPixelHitTestScreenToClientFailureCount();
    std::int32_t GetCompositionPixelHitTestLastX();
    std::int32_t GetCompositionPixelHitTestLastY();
    std::int32_t GetCompositionPixelHitTestLastAlpha();
    std::int32_t GetCompositionPixelHitTestLastResult();
    std::uint64_t GetCompositionPixelHitTestPublishedGeneration();
    std::uint32_t GetCompositionPixelHitTestLastWin32Error();
    bool DidCompositionPixelHitTestStyleRestoreSucceed();
    bool IsCompositionPixelHitTestRequestPending();
    bool DidCompositionPixelHitTestLastRequestSucceed();
    std::uint64_t GetCompositionPixelHitTestInitialExtendedStyle();
    std::uint64_t GetCompositionPixelHitTestDiagnosticExtendedStyle();
    std::uint64_t GetCompositionPixelHitTestCurrentExtendedStyle();
    std::uint64_t GetCompositionPixelHitTestWindowClassStyle();
}
