#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kCompositionWindowPositionMessage =
        0x8000 + 0x45;

    enum class CompositionWindowPositionState : std::int32_t
    {
        NotStarted = 0,
        Ready = 1,
        MoveRequested = 2,
        MoveMessagePosted = 3,
        MoveMessageReceived = 4,
        SetWindowPosApplied = 5,
        PositionVerified = 6,
        Completed = 7,
        ShutdownRequested = 8,
        Stopped = 9,
        Failed = 10
    };

    enum class CompositionWindowPositionFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        ShutdownAlreadyRequested = 3,
        MoveAlreadyPending = 4,
        PositionOutsideDiagnosticWorkArea = 5,
        PostMessageFailed = 6,
        MessageSequenceMismatch = 7,
        SetWindowPosFailed = 8,
        GetWindowRectFailed = 9,
        PositionVerificationFailed = 10,
        MoveTimeout = 11,
        UnexpectedMoveCount = 12,
        ShutdownFailed = 13
    };

    void ResetCompositionWindowPositionDiagnostics();
    void SetCompositionWindowPositionUiWindow(void* window);
    void HandleCompositionWindowPositionMessage(std::uint32_t sequence);
    void NotifyCompositionWindowPositionShutdownRequested();
    void StopCompositionWindowPositionDiagnosticsOnUiThread();

    std::int32_t StartCompositionWindowPositionDiagnostics();
    std::int32_t RequestCompositionWindowPosition(
        std::int32_t x,
        std::int32_t y);
    std::int32_t GetCompositionWindowPositionState();
    std::int32_t GetCompositionWindowPositionFailureStage();
    std::int32_t GetCompositionWindowRequestedX();
    std::int32_t GetCompositionWindowRequestedY();
    std::int32_t GetCompositionWindowActualX();
    std::int32_t GetCompositionWindowActualY();
    std::uint32_t GetCompositionWindowMoveRequestCount();
    std::uint32_t GetCompositionWindowMoveAppliedCount();
    std::uint32_t GetCompositionWindowMoveRejectedCount();
    std::uint32_t GetCompositionWindowLastSetWindowPosLastError();
    bool IsCompositionWindowMovePending();
    bool DidCompositionWindowLastMoveSucceed();
    std::int32_t GetCompositionWorkAreaLeft();
    std::int32_t GetCompositionWorkAreaTop();
    std::int32_t GetCompositionWorkAreaRight();
    std::int32_t GetCompositionWorkAreaBottom();
    std::int32_t GetCompositionWindowInitialX();
    std::int32_t GetCompositionWindowInitialY();
    std::int32_t GetCompositionWindowInitialWidth();
    std::int32_t GetCompositionWindowInitialHeight();
}
