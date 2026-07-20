#include "DesktopMascotNative/CompositionWindowPositionDiagnostics.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <atomic>
#include <cwchar>

namespace
{
    using DesktopMascotNative::CompositionWindowPositionFailureStage;
    using DesktopMascotNative::CompositionWindowPositionState;

    constexpr std::uint32_t kExpectedMoveCount = 4;
    constexpr UINT kSetWindowPosFlags =
        SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_requestedX{0};
    std::atomic<std::int32_t> g_requestedY{0};
    std::atomic<std::int32_t> g_actualX{0};
    std::atomic<std::int32_t> g_actualY{0};
    std::atomic<std::uint32_t> g_requestCount{0};
    std::atomic<std::uint32_t> g_appliedCount{0};
    std::atomic<std::uint32_t> g_rejectedCount{0};
    std::atomic<std::uint32_t> g_sequence{0};
    std::atomic<std::uint32_t> g_pendingSequence{0};
    std::atomic<std::uint32_t> g_lastSetWindowPosError{0};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_lastMoveSucceeded{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<std::int32_t> g_workAreaLeft{0};
    std::atomic<std::int32_t> g_workAreaTop{0};
    std::atomic<std::int32_t> g_workAreaRight{0};
    std::atomic<std::int32_t> g_workAreaBottom{0};
    std::atomic<std::int32_t> g_initialX{0};
    std::atomic<std::int32_t> g_initialY{0};
    std::atomic<std::int32_t> g_initialWidth{0};
    std::atomic<std::int32_t> g_initialHeight{0};
    std::atomic<bool> g_initialPositionAvailable{false};
    std::atomic<bool> g_workAreaAvailable{false};

    void StoreState(CompositionWindowPositionState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    void Fail(CompositionWindowPositionFailureStage stage)
    {
        std::int32_t expected =
            static_cast<std::int32_t>(
                CompositionWindowPositionFailureStage::None);
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        g_pending.store(false, std::memory_order_release);
        g_lastMoveSucceeded.store(false, std::memory_order_release);
        StoreState(CompositionWindowPositionState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Composition window position "
            L"diagnostics failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool IsRequestableState(std::int32_t state)
    {
        return state
                == static_cast<std::int32_t>(
                    CompositionWindowPositionState::Ready)
            || state
                == static_cast<std::int32_t>(
                    CompositionWindowPositionState::PositionVerified);
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionWindowPositionDiagnostics()
    {
        g_window.store(nullptr);
        g_state.store(0);
        g_failureStage.store(0);
        g_requestedX.store(0);
        g_requestedY.store(0);
        g_actualX.store(0);
        g_actualY.store(0);
        g_requestCount.store(0);
        g_appliedCount.store(0);
        g_rejectedCount.store(0);
        g_sequence.store(0);
        g_pendingSequence.store(0);
        g_lastSetWindowPosError.store(0);
        g_pending.store(false);
        g_lastMoveSucceeded.store(false);
        g_shutdownRequested.store(false);
        g_workAreaLeft.store(0);
        g_workAreaTop.store(0);
        g_workAreaRight.store(0);
        g_workAreaBottom.store(0);
        g_initialX.store(0);
        g_initialY.store(0);
        g_initialWidth.store(0);
        g_initialHeight.store(0);
        g_initialPositionAvailable.store(false);
        g_workAreaAvailable.store(false);
    }

    void SetCompositionWindowPositionUiWindow(void* windowValue)
    {
        const HWND window = static_cast<HWND>(windowValue);
        g_window.store(window, std::memory_order_release);
        if (window == nullptr)
        {
            return;
        }

        RECT rectangle{};
        ::SetLastError(ERROR_SUCCESS);
        if (::GetWindowRect(window, &rectangle) != FALSE)
        {
            g_initialX.store(rectangle.left);
            g_initialY.store(rectangle.top);
            g_initialWidth.store(rectangle.right - rectangle.left);
            g_initialHeight.store(rectangle.bottom - rectangle.top);
            g_actualX.store(rectangle.left);
            g_actualY.store(rectangle.top);
            g_initialPositionAvailable.store(true, std::memory_order_release);
        }

        RECT workArea{};
        ::SetLastError(ERROR_SUCCESS);
        if (::SystemParametersInfoW(
                SPI_GETWORKAREA,
                0,
                &workArea,
                0)
            != FALSE)
        {
            g_workAreaLeft.store(workArea.left);
            g_workAreaTop.store(workArea.top);
            g_workAreaRight.store(workArea.right);
            g_workAreaBottom.store(workArea.bottom);
            g_workAreaAvailable.store(true, std::memory_order_release);
        }
    }

    std::int32_t StartCompositionWindowPositionDiagnostics()
    {
        std::int32_t expected =
            static_cast<std::int32_t>(
                CompositionWindowPositionState::NotStarted);
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    CompositionWindowPositionState::Ready),
                std::memory_order_acq_rel))
        {
            return 0;
        }
        if (GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning)
            || !g_initialPositionAvailable.load(std::memory_order_acquire)
            || !g_workAreaAvailable.load(std::memory_order_acquire))
        {
            Fail(CompositionWindowPositionFailureStage::CompositionNotReady);
            return -1;
        }
        if (g_window.load(std::memory_order_acquire) == nullptr)
        {
            Fail(CompositionWindowPositionFailureStage::WindowUnavailable);
            return -2;
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition window position "
            L"diagnostics started.\n");
        return 1;
    }

    std::int32_t RequestCompositionWindowPosition(
        std::int32_t x,
        std::int32_t y)
    {
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionWindowPositionFailureStage::
                    ShutdownAlreadyRequested);
            return 0;
        }
        if (g_pending.load(std::memory_order_acquire))
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionWindowPositionFailureStage::MoveAlreadyPending);
            return 0;
        }
        const auto currentState = g_state.load(std::memory_order_acquire);
        if (!IsRequestableState(currentState))
        {
            g_rejectedCount.fetch_add(1);
            return 0;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr)
        {
            g_rejectedCount.fetch_add(1);
            Fail(CompositionWindowPositionFailureStage::WindowUnavailable);
            return 0;
        }

        const auto sequence =
            g_sequence.fetch_add(1, std::memory_order_relaxed) + 1;
        if (sequence > kExpectedMoveCount)
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionWindowPositionFailureStage::UnexpectedMoveCount);
            return 0;
        }
        g_requestedX.store(x);
        g_requestedY.store(y);
        g_pendingSequence.store(sequence, std::memory_order_release);
        g_pending.store(true, std::memory_order_release);
        g_lastMoveSucceeded.store(false);
        g_requestCount.fetch_add(1);
        StoreState(CompositionWindowPositionState::MoveRequested);
        if (::PostMessageW(
                window,
                kCompositionWindowPositionMessage,
                static_cast<WPARAM>(sequence),
                0)
            == FALSE)
        {
            g_lastSetWindowPosError.store(::GetLastError());
            g_pending.store(false, std::memory_order_release);
            g_rejectedCount.fetch_add(1);
            Fail(CompositionWindowPositionFailureStage::PostMessageFailed);
            return 0;
        }
        StoreState(CompositionWindowPositionState::MoveMessagePosted);
        return 1;
    }

    void HandleCompositionWindowPositionMessage(std::uint32_t sequence)
    {
        StoreState(CompositionWindowPositionState::MoveMessageReceived);
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            Fail(
                CompositionWindowPositionFailureStage::
                    ShutdownAlreadyRequested);
            return;
        }
        if (sequence == 0
            || sequence
                != g_pendingSequence.load(std::memory_order_acquire))
        {
            Fail(
                CompositionWindowPositionFailureStage::
                    MessageSequenceMismatch);
            return;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(CompositionWindowPositionFailureStage::WindowUnavailable);
            return;
        }

        const auto requestedX = g_requestedX.load();
        const auto requestedY = g_requestedY.load();
        ::SetLastError(ERROR_SUCCESS);
        if (::SetWindowPos(
                window,
                nullptr,
                requestedX,
                requestedY,
                0,
                0,
                kSetWindowPosFlags)
            == FALSE)
        {
            g_lastSetWindowPosError.store(::GetLastError());
            Fail(CompositionWindowPositionFailureStage::SetWindowPosFailed);
            return;
        }
        g_lastSetWindowPosError.store(ERROR_SUCCESS);
        StoreState(CompositionWindowPositionState::SetWindowPosApplied);

        RECT rectangle{};
        ::SetLastError(ERROR_SUCCESS);
        if (::GetWindowRect(window, &rectangle) == FALSE)
        {
            g_lastSetWindowPosError.store(::GetLastError());
            Fail(CompositionWindowPositionFailureStage::GetWindowRectFailed);
            return;
        }
        g_actualX.store(rectangle.left);
        g_actualY.store(rectangle.top);
        const bool verified =
            rectangle.left == requestedX
            && rectangle.top == requestedY
            && rectangle.right - rectangle.left == g_initialWidth.load()
            && rectangle.bottom - rectangle.top == g_initialHeight.load();
        if (!verified)
        {
            Fail(
                CompositionWindowPositionFailureStage::
                    PositionVerificationFailed);
            return;
        }

        g_pending.store(false, std::memory_order_release);
        g_lastMoveSucceeded.store(true, std::memory_order_release);
        const auto applied =
            g_appliedCount.fetch_add(1, std::memory_order_relaxed) + 1;
        wchar_t message[128]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Composition window move %u "
            L"completed.\n",
            applied);
        ::OutputDebugStringW(message);
        if (applied == kExpectedMoveCount)
        {
            StoreState(CompositionWindowPositionState::Completed);
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Composition window position "
                L"diagnostics completed.\n");
        }
        else
        {
            StoreState(CompositionWindowPositionState::PositionVerified);
        }
    }

    void NotifyCompositionWindowPositionShutdownRequested()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        const auto currentState = g_state.load(std::memory_order_acquire);
        if (currentState
                != static_cast<std::int32_t>(
                    CompositionWindowPositionState::NotStarted)
            && currentState
                != static_cast<std::int32_t>(
                    CompositionWindowPositionState::Stopped)
            && currentState
                != static_cast<std::int32_t>(
                    CompositionWindowPositionState::Failed))
        {
            StoreState(CompositionWindowPositionState::ShutdownRequested);
        }
    }

    void StopCompositionWindowPositionDiagnosticsOnUiThread()
    {
        NotifyCompositionWindowPositionShutdownRequested();
        g_pending.store(false, std::memory_order_release);
        g_window.store(nullptr, std::memory_order_release);
        StoreState(CompositionWindowPositionState::Stopped);
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionWindowPositionState, g_state)
    GET_I32(GetCompositionWindowPositionFailureStage, g_failureStage)
    GET_I32(GetCompositionWindowRequestedX, g_requestedX)
    GET_I32(GetCompositionWindowRequestedY, g_requestedY)
    GET_I32(GetCompositionWindowActualX, g_actualX)
    GET_I32(GetCompositionWindowActualY, g_actualY)
    GET_U32(GetCompositionWindowMoveRequestCount, g_requestCount)
    GET_U32(GetCompositionWindowMoveAppliedCount, g_appliedCount)
    GET_U32(GetCompositionWindowMoveRejectedCount, g_rejectedCount)
    GET_U32(
        GetCompositionWindowLastSetWindowPosLastError,
        g_lastSetWindowPosError)
    GET_BOOL(IsCompositionWindowMovePending, g_pending)
    GET_BOOL(DidCompositionWindowLastMoveSucceed, g_lastMoveSucceeded)
    GET_I32(GetCompositionWorkAreaLeft, g_workAreaLeft)
    GET_I32(GetCompositionWorkAreaTop, g_workAreaTop)
    GET_I32(GetCompositionWorkAreaRight, g_workAreaRight)
    GET_I32(GetCompositionWorkAreaBottom, g_workAreaBottom)
    GET_I32(GetCompositionWindowInitialX, g_initialX)
    GET_I32(GetCompositionWindowInitialY, g_initialY)
    GET_I32(GetCompositionWindowInitialWidth, g_initialWidth)
    GET_I32(GetCompositionWindowInitialHeight, g_initialHeight)
#undef GET_I32
#undef GET_U32
#undef GET_BOOL
}
