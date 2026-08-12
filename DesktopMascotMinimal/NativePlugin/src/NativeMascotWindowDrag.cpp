#include "DesktopMascotNative/NativeMascotWindowDrag.h"

#include "DesktopMascotNative/AnimatedWindowRegionDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/ContinuousCompositionDiagnostics.h"

#include <Windows.h>
#include <windowsx.h>

#include <algorithm>
#include <atomic>
#include <cstdlib>
#include <cwchar>

namespace
{
    using DesktopMascotNative::NativeMascotDragDiagnosticState;
    using DesktopMascotNative::NativeMascotDragFailureStage;

    constexpr UINT kMoveFlags =
        SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<bool> g_pointerPressed{false};
    std::atomic<bool> g_dragging{false};
    std::atomic<bool> g_captureOwned{false};
    std::atomic<std::int32_t> g_diagnosticState{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::uint32_t> g_startCount{0};
    std::atomic<std::uint32_t> g_moveCount{0};
    std::atomic<std::uint32_t> g_endCount{0};
    std::atomic<std::uint32_t> g_captureAcquiredCount{0};
    std::atomic<std::uint32_t> g_captureReleasedCount{0};
    std::atomic<std::uint64_t> g_completedDragGeneration{0};
    std::atomic<std::uint64_t> g_completedClickGeneration{0};
    std::atomic<std::int32_t> g_lastWindowX{0};
    std::atomic<std::int32_t> g_lastWindowY{0};
    POINT g_dragStartCursor{};
    POINT g_latestCursor{};
    RECT g_dragStartWindow{};

    std::atomic<std::int32_t> g_diagnosticDeltaX{0};
    std::atomic<std::int32_t> g_diagnosticDeltaY{0};
    RECT g_diagnosticInitialRectangle{};
    HWND g_diagnosticPreviousZWindow = nullptr;
    HWND g_diagnosticNextZWindow = nullptr;
    HWND g_diagnosticForegroundWindow = nullptr;
    std::uint32_t g_diagnosticInitialPresentCount = 0;
    std::uint64_t g_diagnosticInitialPublishedGeneration = 0;
    std::int32_t g_diagnosticInitialCommitCount = 0;
    std::atomic<bool> g_diagnosticMoveSucceeded{false};
    std::atomic<bool> g_diagnosticSizeUnchanged{false};
    std::atomic<bool> g_diagnosticRegionApplied{false};
    std::atomic<bool> g_diagnosticPresentContinued{false};
    std::atomic<bool> g_diagnosticPublicationContinued{false};
    std::atomic<bool> g_diagnosticNoCompositionRestart{false};
    std::atomic<bool> g_diagnosticZOrderPreserved{false};
    std::atomic<bool> g_diagnosticNoActivation{false};
    std::atomic<bool> g_diagnosticCaptureReleased{false};
    std::atomic<bool> g_diagnosticPositionRestored{false};
    std::atomic<std::uint64_t>
        g_diagnosticInitialCompletedDragGeneration{0};

    void StoreDiagnosticState(NativeMascotDragDiagnosticState state)
    {
        g_diagnosticState.store(
            static_cast<std::int32_t>(state),
            std::memory_order_release);
    }

    void Fail(NativeMascotDragFailureStage stage)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        StoreDiagnosticState(NativeMascotDragDiagnosticState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Native mascot drag failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool IsWindowReady(HWND window)
    {
        return window != nullptr && ::IsWindow(window) != FALSE;
    }

    bool IsPointInsideAppliedRegion(HWND window, POINT clientPoint)
    {
        HRGN region = ::CreateRectRgn(0, 0, 0, 0);
        if (region == nullptr)
        {
            return false;
        }
        const int regionType = ::GetWindowRgn(window, region);
        const bool inside =
            regionType != ERROR
            && regionType != NULLREGION
            && ::PtInRegion(region, clientPoint.x, clientPoint.y) != FALSE;
        ::DeleteObject(region);
        return inside;
    }

    bool HasAppliedRegion(HWND window)
    {
        HRGN region = ::CreateRectRgn(0, 0, 0, 0);
        if (region == nullptr)
        {
            return false;
        }
        const int regionType = ::GetWindowRgn(window, region);
        ::DeleteObject(region);
        return regionType != ERROR && regionType != NULLREGION;
    }

    enum class PointerOperationCompletion
    {
        None,
        NonDragClick,
        Drag
    };

    PointerOperationCompletion EndPointerOperation(
        HWND window,
        bool releaseCapture)
    {
        const bool wasPressed = g_pointerPressed.exchange(false);
        const bool wasDragging = g_dragging.exchange(false);
        g_captureOwned.store(false, std::memory_order_release);
        if (releaseCapture && ::GetCapture() == window)
        {
            ::ReleaseCapture();
            g_captureReleasedCount.fetch_add(1);
        }
        if (wasPressed || wasDragging)
        {
            g_endCount.fetch_add(1);
        }
        if (!wasPressed)
        {
            return PointerOperationCompletion::None;
        }
        return wasDragging
            ? PointerOperationCompletion::Drag
            : PointerOperationCompletion::NonDragClick;
    }

    bool PublishCompletedDrag(HWND window)
    {
        RECT rectangle{};
        if (::GetWindowRect(window, &rectangle) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::WindowRectangleQueryFailed);
            return false;
        }
        g_lastWindowX.store(rectangle.left, std::memory_order_relaxed);
        g_lastWindowY.store(rectangle.top, std::memory_order_relaxed);
        g_completedDragGeneration.fetch_add(1, std::memory_order_release);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Native mascot drag completed.\n");
        return true;
    }

    void HandleButtonDown(HWND window, LPARAM lParam)
    {
        if (!g_enabled.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            return;
        }
        const POINT clientPoint{
            GET_X_LPARAM(lParam),
            GET_Y_LPARAM(lParam)};
        if (!IsPointInsideAppliedRegion(window, clientPoint))
        {
            return;
        }
        if (::GetCursorPos(&g_dragStartCursor) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::CursorQueryFailed);
            return;
        }
        if (::GetWindowRect(window, &g_dragStartWindow) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::WindowRectangleQueryFailed);
            return;
        }
        g_latestCursor = g_dragStartCursor;
        g_lastWindowX.store(g_dragStartWindow.left);
        g_lastWindowY.store(g_dragStartWindow.top);
        g_pointerPressed.store(true, std::memory_order_release);
        g_dragging.store(false, std::memory_order_release);
        ::SetCapture(window);
        if (::GetCapture() != window)
        {
            g_pointerPressed.store(false, std::memory_order_release);
            Fail(NativeMascotDragFailureStage::CaptureFailed);
            return;
        }
        g_captureAcquiredCount.fetch_add(1);
        g_captureOwned.store(true, std::memory_order_release);
    }

    void HandleMouseMove(HWND window)
    {
        if (!g_pointerPressed.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            return;
        }
        POINT cursor{};
        if (::GetCursorPos(&cursor) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::CursorQueryFailed);
            EndPointerOperation(window, true);
            return;
        }
        const int deltaX = cursor.x - g_dragStartCursor.x;
        const int deltaY = cursor.y - g_dragStartCursor.y;
        if (!g_dragging.load(std::memory_order_acquire))
        {
            const int thresholdX =
                std::max(1, ::GetSystemMetrics(SM_CXDRAG));
            const int thresholdY =
                std::max(1, ::GetSystemMetrics(SM_CYDRAG));
            if (std::abs(deltaX) < thresholdX
                && std::abs(deltaY) < thresholdY)
            {
                return;
            }
            g_dragging.store(true, std::memory_order_release);
            g_startCount.fetch_add(1);
        }
        const int x = g_dragStartWindow.left + deltaX;
        const int y = g_dragStartWindow.top + deltaY;
        if (x == g_lastWindowX.load() && y == g_lastWindowY.load())
        {
            return;
        }
        if (::SetWindowPos(window, nullptr, x, y, 0, 0, kMoveFlags) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::SetWindowPosFailed);
            EndPointerOperation(window, true);
            return;
        }
        g_latestCursor = cursor;
        g_lastWindowX.store(x);
        g_lastWindowY.store(y);
        g_moveCount.fetch_add(1);
    }

    bool VerifyRectangleSize(const RECT& value, const RECT& expected)
    {
        return value.right - value.left == expected.right - expected.left
            && value.bottom - value.top == expected.bottom - expected.top;
    }

    void HandleDiagnosticMove(HWND window)
    {
        if (!g_enabled.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            Fail(NativeMascotDragFailureStage::RuntimeInactive);
            return;
        }
        if (!IsWindowReady(window))
        {
            Fail(NativeMascotDragFailureStage::WindowUnavailable);
            return;
        }
        if (!HasAppliedRegion(window))
        {
            Fail(NativeMascotDragFailureStage::RegionUnavailable);
            return;
        }
        if (::GetWindowRect(window, &g_diagnosticInitialRectangle) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::WindowRectangleQueryFailed);
            return;
        }
        g_diagnosticPreviousZWindow = ::GetWindow(window, GW_HWNDPREV);
        g_diagnosticNextZWindow = ::GetWindow(window, GW_HWNDNEXT);
        g_diagnosticForegroundWindow = ::GetForegroundWindow();
        g_diagnosticInitialPresentCount =
            DesktopMascotNative::GetContinuousCompositionPresentCount();
        g_diagnosticInitialPublishedGeneration =
            DesktopMascotNative::GetAnimatedWindowRegionPublishedGeneration();
        g_diagnosticInitialCommitCount =
            DesktopMascotNative::GetDCompCommitCount();
        const int x = g_diagnosticInitialRectangle.left
            + g_diagnosticDeltaX.load();
        const int y = g_diagnosticInitialRectangle.top
            + g_diagnosticDeltaY.load();
        if (::SetWindowPos(window, nullptr, x, y, 0, 0, kMoveFlags) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::SetWindowPosFailed);
            return;
        }
        RECT moved{};
        if (::GetWindowRect(window, &moved) == FALSE)
        {
            Fail(NativeMascotDragFailureStage::WindowRectangleQueryFailed);
            return;
        }
        const bool movedAsRequested =
            moved.left == x && moved.top == y;
        const bool sizeUnchanged =
            VerifyRectangleSize(moved, g_diagnosticInitialRectangle);
        g_diagnosticMoveSucceeded.store(movedAsRequested);
        g_diagnosticSizeUnchanged.store(sizeUnchanged);
        g_lastWindowX.store(moved.left);
        g_lastWindowY.store(moved.top);
        if (!movedAsRequested || !sizeUnchanged)
        {
            Fail(
                NativeMascotDragFailureStage::
                    DiagnosticMoveVerificationFailed);
            return;
        }
        if (!PublishCompletedDrag(window))
        {
            return;
        }
        StoreDiagnosticState(NativeMascotDragDiagnosticState::Moved);
    }

    void HandleDiagnosticRestore(HWND window)
    {
        g_diagnosticRegionApplied.store(HasAppliedRegion(window));
        g_diagnosticPresentContinued.store(
            DesktopMascotNative::GetContinuousCompositionPresentCount()
                > g_diagnosticInitialPresentCount);
        g_diagnosticPublicationContinued.store(
            DesktopMascotNative::GetAnimatedWindowRegionPublishedGeneration()
                > g_diagnosticInitialPublishedGeneration);
        g_diagnosticNoCompositionRestart.store(
            DesktopMascotNative::GetDCompCommitCount()
                == g_diagnosticInitialCommitCount);
        g_diagnosticZOrderPreserved.store(
            ::GetWindow(window, GW_HWNDPREV) == g_diagnosticPreviousZWindow
            && ::GetWindow(window, GW_HWNDNEXT) == g_diagnosticNextZWindow);
        g_diagnosticNoActivation.store(
            ::GetForegroundWindow() == g_diagnosticForegroundWindow);
        EndPointerOperation(window, true);
        g_diagnosticCaptureReleased.store(::GetCapture() != window);
        if (::SetWindowPos(
                window,
                nullptr,
                g_diagnosticInitialRectangle.left,
                g_diagnosticInitialRectangle.top,
                0,
                0,
                kMoveFlags)
            == FALSE)
        {
            Fail(NativeMascotDragFailureStage::DiagnosticRestoreFailed);
            return;
        }
        RECT restored{};
        const bool positionRestored =
            ::GetWindowRect(window, &restored) != FALSE
            && restored.left == g_diagnosticInitialRectangle.left
            && restored.top == g_diagnosticInitialRectangle.top
            && VerifyRectangleSize(restored, g_diagnosticInitialRectangle);
        g_diagnosticPositionRestored.store(positionRestored);
        g_lastWindowX.store(restored.left);
        g_lastWindowY.store(restored.top);

        if (!g_diagnosticPresentContinued.load())
            Fail(NativeMascotDragFailureStage::DiagnosticPresentStopped);
        else if (!g_diagnosticPublicationContinued.load())
            Fail(
                NativeMascotDragFailureStage::
                    DiagnosticRegionPublicationStopped);
        else if (!g_diagnosticNoCompositionRestart.load())
            Fail(
                NativeMascotDragFailureStage::
                    DiagnosticCompositionRestarted);
        else if (!g_diagnosticZOrderPreserved.load())
            Fail(NativeMascotDragFailureStage::DiagnosticZOrderChanged);
        else if (!g_diagnosticNoActivation.load())
            Fail(NativeMascotDragFailureStage::DiagnosticActivationChanged);
        else if (!g_diagnosticCaptureReleased.load())
            Fail(
                NativeMascotDragFailureStage::
                    DiagnosticCaptureLeftOwned);
        else if (g_completedDragGeneration.load(std::memory_order_acquire)
            != g_diagnosticInitialCompletedDragGeneration.load(
                std::memory_order_acquire) + 1)
        {
            Fail(
                NativeMascotDragFailureStage::
                    DiagnosticCompletedDragGenerationMismatch);
        }
        else if (!positionRestored || !g_diagnosticRegionApplied.load())
            Fail(NativeMascotDragFailureStage::DiagnosticRestoreFailed);
        else
        {
            StoreDiagnosticState(
                NativeMascotDragDiagnosticState::Completed);
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Native mascot drag diagnostics "
                L"completed.\n");
        }
    }
}

namespace DesktopMascotNative
{
    void ResetNativeMascotWindowDrag()
    {
        g_window.store(nullptr);
        g_enabled.store(false);
        g_shutdownRequested.store(false);
        g_pointerPressed.store(false);
        g_dragging.store(false);
        g_captureOwned.store(false);
        g_diagnosticState.store(0);
        g_failureStage.store(0);
        g_startCount.store(0);
        g_moveCount.store(0);
        g_endCount.store(0);
        g_captureAcquiredCount.store(0);
        g_captureReleasedCount.store(0);
        g_completedDragGeneration.store(0);
        g_completedClickGeneration.store(0);
        g_lastWindowX.store(0);
        g_lastWindowY.store(0);
        g_diagnosticDeltaX.store(0);
        g_diagnosticDeltaY.store(0);
        g_diagnosticMoveSucceeded.store(false);
        g_diagnosticSizeUnchanged.store(false);
        g_diagnosticRegionApplied.store(false);
        g_diagnosticPresentContinued.store(false);
        g_diagnosticPublicationContinued.store(false);
        g_diagnosticNoCompositionRestart.store(false);
        g_diagnosticZOrderPreserved.store(false);
        g_diagnosticNoActivation.store(false);
        g_diagnosticCaptureReleased.store(false);
        g_diagnosticPositionRestored.store(false);
        g_diagnosticInitialCompletedDragGeneration.store(0);
    }

    void SetNativeMascotWindowDragUiWindow(void* windowValue)
    {
        const HWND window = static_cast<HWND>(windowValue);
        g_window.store(window, std::memory_order_release);
        RECT rectangle{};
        if (window != nullptr
            && ::GetWindowRect(window, &rectangle) != FALSE)
        {
            g_lastWindowX.store(rectangle.left);
            g_lastWindowY.store(rectangle.top);
        }
    }

    bool HandleNativeMascotWindowDragMessage(
        void* windowValue,
        std::uint32_t message,
        std::uintptr_t,
        std::intptr_t lParam,
        std::intptr_t& result)
    {
        const HWND window = static_cast<HWND>(windowValue);
        switch (message)
        {
            case WM_MOUSEACTIVATE:
                if (g_enabled.load(std::memory_order_acquire))
                {
                    result = MA_NOACTIVATE;
                    return true;
                }
                return false;
            case WM_LBUTTONDOWN:
                HandleButtonDown(window, static_cast<LPARAM>(lParam));
                result = 0;
                return true;
            case WM_MOUSEMOVE:
                HandleMouseMove(window);
                result = 0;
                return true;
            case WM_LBUTTONUP:
            {
                const bool normalButtonUp = !g_shutdownRequested.load(
                    std::memory_order_acquire);
                switch (EndPointerOperation(window, true))
                {
                    case PointerOperationCompletion::NonDragClick:
                        if (normalButtonUp)
                        {
                            g_completedClickGeneration.fetch_add(
                                1,
                                std::memory_order_release);
                        }
                        break;
                    case PointerOperationCompletion::Drag:
                        PublishCompletedDrag(window);
                        break;
                    case PointerOperationCompletion::None:
                        break;
                }
                result = 0;
                return true;
            }
            case WM_CANCELMODE:
                EndPointerOperation(window, true);
                result = 0;
                return true;
            case WM_CAPTURECHANGED:
                g_captureOwned.store(false, std::memory_order_release);
                if (g_pointerPressed.exchange(false)
                    || g_dragging.exchange(false))
                {
                    g_captureReleasedCount.fetch_add(1);
                    g_endCount.fetch_add(1);
                }
                result = 0;
                return true;
            case kNativeMascotDragCancelMessage:
                EndPointerOperation(window, true);
                result = 0;
                return true;
            case kNativeMascotDragDiagnosticMoveMessage:
                HandleDiagnosticMove(window);
                result = 0;
                return true;
            case kNativeMascotDragDiagnosticRestoreMessage:
                HandleDiagnosticRestore(window);
                result = 0;
                return true;
            default:
                return false;
        }
    }

    void NotifyNativeMascotWindowDragShutdownRequested()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        g_enabled.store(false, std::memory_order_release);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (IsWindowReady(window))
        {
            ::PostMessageW(window, kNativeMascotDragCancelMessage, 0, 0);
        }
    }

    void StopNativeMascotWindowDragOnUiThread()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window != nullptr)
        {
            EndPointerOperation(window, true);
        }
        g_enabled.store(false, std::memory_order_release);
        g_shutdownRequested.store(true, std::memory_order_release);
        g_window.store(nullptr, std::memory_order_release);
    }

    std::int32_t EnableNativeMascotWindowDrag()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (!IsWindowReady(window)
            || GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning))
        {
            return 0;
        }
        g_shutdownRequested.store(false, std::memory_order_release);
        g_enabled.store(true, std::memory_order_release);
        return 1;
    }

    std::int32_t DisableNativeMascotWindowDrag()
    {
        NotifyNativeMascotWindowDragShutdownRequested();
        return 1;
    }

    std::int32_t StartNativeMascotWindowDragDiagnostic(
        std::int32_t deltaX,
        std::int32_t deltaY)
    {
        if (deltaX == 0 || deltaY == 0)
            return 0;
        std::int32_t expected = 0;
        if (!g_diagnosticState.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    NativeMascotDragDiagnosticState::MoveRequested),
                std::memory_order_acq_rel))
        {
            Fail(NativeMascotDragFailureStage::DiagnosticAlreadyRunning);
            return 0;
        }
        g_diagnosticDeltaX.store(deltaX);
        g_diagnosticDeltaY.store(deltaY);
        g_diagnosticInitialCompletedDragGeneration.store(
            g_completedDragGeneration.load(std::memory_order_acquire),
            std::memory_order_release);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (!IsWindowReady(window)
            || ::PostMessageW(
                   window,
                   kNativeMascotDragDiagnosticMoveMessage,
                   0,
                   0)
                == FALSE)
        {
            Fail(NativeMascotDragFailureStage::DiagnosticPostFailed);
            return 0;
        }
        return 1;
    }

    std::int32_t CompleteNativeMascotWindowDragDiagnostic()
    {
        std::int32_t expected = static_cast<std::int32_t>(
            NativeMascotDragDiagnosticState::Moved);
        if (!g_diagnosticState.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    NativeMascotDragDiagnosticState::RestoreRequested),
                std::memory_order_acq_rel))
        {
            return 0;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (!IsWindowReady(window)
            || ::PostMessageW(
                   window,
                   kNativeMascotDragDiagnosticRestoreMessage,
                   0,
                   0)
                == FALSE)
        {
            Fail(NativeMascotDragFailureStage::DiagnosticPostFailed);
            return 0;
        }
        return 1;
    }

#define GET_BOOL(name, value) bool name() { return value.load(); }
#define GET_I32(name, value) std::int32_t name() { return value.load(); }
#define GET_U32(name, value) std::uint32_t name() { return value.load(); }
    GET_BOOL(IsNativeMascotWindowDragEnabled, g_enabled)
    GET_BOOL(IsNativeMascotWindowDragging, g_dragging)
    GET_BOOL(IsNativeMascotWindowCaptureOwned, g_captureOwned)
    bool IsNativeMascotWindowAvailableForDrag()
    {
        return IsWindowReady(g_window.load(std::memory_order_acquire));
    }
    GET_I32(GetNativeMascotDragDiagnosticState, g_diagnosticState)
    GET_I32(GetNativeMascotDragFailureStage, g_failureStage)
    GET_U32(GetNativeMascotDragStartCount, g_startCount)
    GET_U32(GetNativeMascotDragMoveCount, g_moveCount)
    GET_U32(GetNativeMascotDragEndCount, g_endCount)
    GET_U32(GetNativeMascotDragCaptureAcquiredCount, g_captureAcquiredCount)
    GET_U32(GetNativeMascotDragCaptureReleasedCount, g_captureReleasedCount)
    std::uint64_t GetNativeMascotCompletedDragGeneration()
    {
        return g_completedDragGeneration.load(std::memory_order_acquire);
    }
    std::uint64_t GetNativeMascotCompletedClickGeneration()
    {
        return g_completedClickGeneration.load(std::memory_order_acquire);
    }
    bool TryGetNativeMascotWindowPosition(
        std::int32_t& x,
        std::int32_t& y)
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        RECT rectangle{};
        if (!IsWindowReady(window)
            || ::GetWindowRect(window, &rectangle) == FALSE)
        {
            return false;
        }
        x = rectangle.left;
        y = rectangle.top;
        return true;
    }
    GET_I32(GetNativeMascotDragLastWindowX, g_lastWindowX)
    GET_I32(GetNativeMascotDragLastWindowY, g_lastWindowY)
    GET_BOOL(DidNativeMascotDragDiagnosticMoveSucceed, g_diagnosticMoveSucceeded)
    GET_BOOL(DidNativeMascotDragDiagnosticSizeRemainUnchanged, g_diagnosticSizeUnchanged)
    GET_BOOL(DidNativeMascotDragDiagnosticRegionRemainApplied, g_diagnosticRegionApplied)
    GET_BOOL(DidNativeMascotDragDiagnosticPresentContinue, g_diagnosticPresentContinued)
    GET_BOOL(DidNativeMascotDragDiagnosticRegionPublicationContinue, g_diagnosticPublicationContinued)
    GET_BOOL(DidNativeMascotDragDiagnosticAvoidCompositionRestart, g_diagnosticNoCompositionRestart)
    GET_BOOL(DidNativeMascotDragDiagnosticPreserveZOrder, g_diagnosticZOrderPreserved)
    GET_BOOL(DidNativeMascotDragDiagnosticAvoidActivation, g_diagnosticNoActivation)
    GET_BOOL(DidNativeMascotDragDiagnosticReleaseCapture, g_diagnosticCaptureReleased)
    GET_BOOL(DidNativeMascotDragDiagnosticRestoreInitialPosition, g_diagnosticPositionRestored)
#undef GET_BOOL
#undef GET_I32
#undef GET_U32
}
