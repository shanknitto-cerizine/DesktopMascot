#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kNativeMascotDragCancelMessage =
        0x8000u + 0x4Fu;
    constexpr std::uint32_t kNativeMascotDragDiagnosticMoveMessage =
        0x8000u + 0x50u;
    constexpr std::uint32_t kNativeMascotDragDiagnosticRestoreMessage =
        0x8000u + 0x51u;

    enum class NativeMascotDragDiagnosticState : std::int32_t
    {
        NotStarted = 0,
        MoveRequested = 1,
        Moved = 2,
        RestoreRequested = 3,
        Completed = 4,
        Failed = 5
    };

    enum class NativeMascotDragFailureStage : std::int32_t
    {
        None = 0,
        WindowUnavailable = 1,
        RuntimeInactive = 2,
        RegionUnavailable = 3,
        CursorQueryFailed = 4,
        WindowRectangleQueryFailed = 5,
        CaptureFailed = 6,
        SetWindowPosFailed = 7,
        DiagnosticAlreadyRunning = 8,
        DiagnosticPostFailed = 9,
        DiagnosticMoveVerificationFailed = 10,
        DiagnosticPresentStopped = 11,
        DiagnosticRegionPublicationStopped = 12,
        DiagnosticCompositionRestarted = 13,
        DiagnosticZOrderChanged = 14,
        DiagnosticActivationChanged = 15,
        DiagnosticRestoreFailed = 16,
        DiagnosticCaptureLeftOwned = 17,
        DiagnosticCompletedDragGenerationMismatch = 18
    };

    void ResetNativeMascotWindowDrag();
    void SetNativeMascotWindowDragUiWindow(void* window);
    bool HandleNativeMascotWindowDragMessage(
        void* window,
        std::uint32_t message,
        std::uintptr_t wParam,
        std::intptr_t lParam,
        std::intptr_t& result);
    void NotifyNativeMascotWindowDragShutdownRequested();
    void StopNativeMascotWindowDragOnUiThread();

    std::int32_t EnableNativeMascotWindowDrag();
    std::int32_t DisableNativeMascotWindowDrag();
    std::int32_t StartNativeMascotWindowDragDiagnostic(
        std::int32_t deltaX,
        std::int32_t deltaY);
    std::int32_t CompleteNativeMascotWindowDragDiagnostic();

    bool IsNativeMascotWindowDragEnabled();
    bool IsNativeMascotWindowDragging();
    bool IsNativeMascotWindowCaptureOwned();
    bool IsNativeMascotWindowAvailableForDrag();
    std::int32_t GetNativeMascotDragDiagnosticState();
    std::int32_t GetNativeMascotDragFailureStage();
    std::uint32_t GetNativeMascotDragStartCount();
    std::uint32_t GetNativeMascotDragMoveCount();
    std::uint32_t GetNativeMascotDragEndCount();
    std::uint32_t GetNativeMascotDragCaptureAcquiredCount();
    std::uint32_t GetNativeMascotDragCaptureReleasedCount();
    std::uint64_t GetNativeMascotCompletedDragGeneration();
    std::uint64_t GetNativeMascotCompletedClickGeneration();
    bool TryGetNativeMascotWindowPosition(
        std::int32_t& x,
        std::int32_t& y);
    std::int32_t GetNativeMascotDragLastWindowX();
    std::int32_t GetNativeMascotDragLastWindowY();
    bool DidNativeMascotDragDiagnosticMoveSucceed();
    bool DidNativeMascotDragDiagnosticSizeRemainUnchanged();
    bool DidNativeMascotDragDiagnosticRegionRemainApplied();
    bool DidNativeMascotDragDiagnosticPresentContinue();
    bool DidNativeMascotDragDiagnosticRegionPublicationContinue();
    bool DidNativeMascotDragDiagnosticAvoidCompositionRestart();
    bool DidNativeMascotDragDiagnosticPreserveZOrder();
    bool DidNativeMascotDragDiagnosticAvoidActivation();
    bool DidNativeMascotDragDiagnosticReleaseCapture();
    bool DidNativeMascotDragDiagnosticRestoreInitialPosition();
}
