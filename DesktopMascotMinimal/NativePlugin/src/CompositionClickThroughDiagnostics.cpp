#include "DesktopMascotNative/CompositionClickThroughDiagnostics.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <atomic>
#include <cwchar>

namespace
{
    using DesktopMascotNative::CompositionClickThroughFailureStage;
    using DesktopMascotNative::CompositionClickThroughState;

    constexpr UINT kFrameChangedFlags =
        SWP_NOMOVE
        | SWP_NOSIZE
        | SWP_NOZORDER
        | SWP_NOACTIVATE
        | SWP_FRAMECHANGED;
    constexpr std::uint32_t kExpectedRequestCount = 2;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_requestedEnabled{0};
    std::atomic<std::uint32_t> g_sequence{0};
    std::atomic<std::uint32_t> g_pendingSequence{0};
    std::atomic<std::uint32_t> g_enableRequestCount{0};
    std::atomic<std::uint32_t> g_disableRequestCount{0};
    std::atomic<std::uint32_t> g_appliedCount{0};
    std::atomic<std::uint32_t> g_rejectedCount{0};
    std::atomic<std::uint32_t> g_lastWin32Error{0};
    std::atomic<std::uint64_t> g_initialStyle{0};
    std::atomic<std::uint64_t> g_currentStyle{0};
    std::atomic<std::uint64_t> g_initialExtendedStyle{0};
    std::atomic<std::uint64_t> g_currentExtendedStyle{0};
    std::atomic<std::uint64_t> g_windowClassStyle{0};
    std::atomic<std::uint32_t> g_hitTestCount{0};
    std::atomic<std::uint32_t> g_transparentHitTestCount{0};
    std::atomic<std::uint32_t> g_lastRequestThreadId{0};
    std::atomic<std::uint32_t> g_lastAppliedThreadId{0};
    std::atomic<std::int32_t> g_initialX{0};
    std::atomic<std::int32_t> g_initialY{0};
    std::atomic<std::int32_t> g_initialWidth{0};
    std::atomic<std::int32_t> g_initialHeight{0};
    std::atomic<bool> g_styleAvailable{false};
    std::atomic<bool> g_initialRectAvailable{false};
    std::atomic<bool> g_classStyleAvailable{false};
    std::atomic<bool> g_initialLayeredEnabled{false};
    std::atomic<bool> g_initialTransparentEnabled{false};
    std::atomic<bool> g_initialExtendedStyleRestored{false};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_lastRequestSucceeded{false};
    std::atomic<bool> g_shutdownRequested{false};

    void StoreState(CompositionClickThroughState state)
    {
        g_state.store(
            static_cast<std::int32_t>(state),
            std::memory_order_release);
    }

    void Fail(CompositionClickThroughFailureStage stage, DWORD error = 0)
    {
        std::int32_t expected =
            static_cast<std::int32_t>(
                CompositionClickThroughFailureStage::None);
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        g_lastWin32Error.store(error, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastRequestSucceeded.store(false, std::memory_order_release);
        StoreState(CompositionClickThroughState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Composition click-through "
            L"diagnostics failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool GetWindowStyle(
        HWND window,
        int index,
        LONG_PTR& value,
        DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        value = ::GetWindowLongPtrW(window, index);
        error = ::GetLastError();
        return value != 0 || error == ERROR_SUCCESS;
    }

    bool GetWindowClassStyle(
        HWND window,
        ULONG_PTR& value,
        DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        value = ::GetClassLongPtrW(window, GCL_STYLE);
        error = ::GetLastError();
        return value != 0 || error == ERROR_SUCCESS;
    }

    bool IsRequestableState(std::int32_t state)
    {
        return state
                == static_cast<std::int32_t>(
                    CompositionClickThroughState::Ready)
            || state
                == static_cast<std::int32_t>(
                    CompositionClickThroughState::Enabled);
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionClickThroughDiagnostics()
    {
        g_window.store(nullptr);
        g_state.store(0);
        g_failureStage.store(0);
        g_requestedEnabled.store(0);
        g_sequence.store(0);
        g_pendingSequence.store(0);
        g_enableRequestCount.store(0);
        g_disableRequestCount.store(0);
        g_appliedCount.store(0);
        g_rejectedCount.store(0);
        g_lastWin32Error.store(0);
        g_initialStyle.store(0);
        g_currentStyle.store(0);
        g_initialExtendedStyle.store(0);
        g_currentExtendedStyle.store(0);
        g_windowClassStyle.store(0);
        g_hitTestCount.store(0);
        g_transparentHitTestCount.store(0);
        g_lastRequestThreadId.store(0);
        g_lastAppliedThreadId.store(0);
        g_initialX.store(0);
        g_initialY.store(0);
        g_initialWidth.store(0);
        g_initialHeight.store(0);
        g_styleAvailable.store(false);
        g_initialRectAvailable.store(false);
        g_classStyleAvailable.store(false);
        g_initialLayeredEnabled.store(false);
        g_initialTransparentEnabled.store(false);
        g_initialExtendedStyleRestored.store(false);
        g_enabled.store(false);
        g_pending.store(false);
        g_lastRequestSucceeded.store(false);
        g_shutdownRequested.store(false);
    }

    void SetCompositionClickThroughUiWindow(void* windowValue)
    {
        const HWND window = static_cast<HWND>(windowValue);
        g_window.store(window, std::memory_order_release);
        if (window == nullptr)
        {
            return;
        }

        LONG_PTR style = 0;
        LONG_PTR extendedStyle = 0;
        DWORD error = ERROR_SUCCESS;
        if (!GetWindowStyle(window, GWL_STYLE, style, error)
            || !GetWindowStyle(
                window,
                GWL_EXSTYLE,
                extendedStyle,
                error))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetInitialExtendedStyleFailed,
                error);
            return;
        }

        g_initialStyle.store(static_cast<std::uint64_t>(style));
        g_currentStyle.store(static_cast<std::uint64_t>(style));
        g_initialExtendedStyle.store(
            static_cast<std::uint64_t>(extendedStyle));
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(extendedStyle));
        g_initialLayeredEnabled.store(
            (extendedStyle & WS_EX_LAYERED) != 0);
        g_initialTransparentEnabled.store(
            (extendedStyle & WS_EX_TRANSPARENT) != 0);
        g_enabled.store(false, std::memory_order_release);
        g_initialExtendedStyleRestored.store(
            true,
            std::memory_order_release);

        ULONG_PTR classStyle = 0;
        if (!GetWindowClassStyle(window, classStyle, error))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetInitialExtendedStyleFailed,
                error);
            return;
        }
        g_windowClassStyle.store(
            static_cast<std::uint64_t>(classStyle));
        g_classStyleAvailable.store(true, std::memory_order_release);

        RECT rectangle{};
        if (::GetWindowRect(window, &rectangle) == FALSE)
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetInitialExtendedStyleFailed,
                ::GetLastError());
            return;
        }
        g_initialX.store(rectangle.left);
        g_initialY.store(rectangle.top);
        g_initialWidth.store(rectangle.right - rectangle.left);
        g_initialHeight.store(rectangle.bottom - rectangle.top);
        g_initialRectAvailable.store(true, std::memory_order_release);
        g_styleAvailable.store(true, std::memory_order_release);
    }

    std::int32_t StartCompositionClickThroughDiagnostics()
    {
        std::int32_t expected =
            static_cast<std::int32_t>(
                CompositionClickThroughState::NotStarted);
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    CompositionClickThroughState::Ready),
                std::memory_order_acq_rel))
        {
            return 0;
        }
        if (GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning)
            || !g_styleAvailable.load(std::memory_order_acquire))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    CompositionNotReady);
            return -1;
        }
        if (!g_classStyleAvailable.load(std::memory_order_acquire))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetInitialExtendedStyleFailed);
            return -2;
        }
        const auto classStyle = g_windowClassStyle.load();
        if ((classStyle & (CS_OWNDC | CS_CLASSDC)) != 0)
        {
            Fail(
                CompositionClickThroughFailureStage::
                    LayeredWindowClassStyleUnsupported);
            return -3;
        }
        if (g_window.load(std::memory_order_acquire) == nullptr)
        {
            Fail(
                CompositionClickThroughFailureStage::WindowUnavailable);
            return -4;
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition click-through "
            L"diagnostics started.\n");
        return 1;
    }

    std::int32_t RequestCompositionClickThroughEnabled(
        std::int32_t enabled)
    {
        const std::int32_t normalized = enabled == 0 ? 0 : 1;
        g_lastRequestThreadId.store(
            ::GetCurrentThreadId(),
            std::memory_order_release);

        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionClickThroughFailureStage::
                    ShutdownAlreadyRequested);
            return 0;
        }
        if (g_pending.load(std::memory_order_acquire))
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionClickThroughFailureStage::
                    RequestAlreadyPending);
            return 0;
        }
        if (!IsRequestableState(g_state.load(std::memory_order_acquire)))
        {
            g_rejectedCount.fetch_add(1);
            return 0;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr)
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionClickThroughFailureStage::WindowUnavailable);
            return 0;
        }

        const auto sequence =
            g_sequence.fetch_add(1, std::memory_order_relaxed) + 1;
        if (sequence > kExpectedRequestCount)
        {
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionClickThroughFailureStage::
                    UnexpectedRequestCount);
            return 0;
        }
        g_requestedEnabled.store(normalized);
        g_pendingSequence.store(sequence, std::memory_order_release);
        g_pending.store(true, std::memory_order_release);
        g_lastRequestSucceeded.store(false);
        if (normalized != 0)
        {
            g_enableRequestCount.fetch_add(1);
            StoreState(CompositionClickThroughState::EnableRequested);
        }
        else
        {
            g_disableRequestCount.fetch_add(1);
            StoreState(CompositionClickThroughState::DisableRequested);
        }

        if (::PostMessageW(
                window,
                kCompositionClickThroughMessage,
                static_cast<WPARAM>(sequence),
                0)
            == FALSE)
        {
            const DWORD error = ::GetLastError();
            g_pending.store(false, std::memory_order_release);
            g_rejectedCount.fetch_add(1);
            Fail(
                CompositionClickThroughFailureStage::PostMessageFailed,
                error);
            return 0;
        }
        StoreState(
            normalized != 0
                ? CompositionClickThroughState::EnableMessagePosted
                : CompositionClickThroughState::DisableMessagePosted);
        return 1;
    }

    void HandleCompositionClickThroughMessage(std::uint32_t sequence)
    {
        const bool enable = g_requestedEnabled.load() != 0;
        StoreState(
            enable
                ? CompositionClickThroughState::EnableMessageReceived
                : CompositionClickThroughState::DisableMessageReceived);
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    ShutdownAlreadyRequested);
            return;
        }
        if (sequence == 0
            || sequence
                != g_pendingSequence.load(std::memory_order_acquire))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    MessageSequenceMismatch);
            return;
        }

        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(
                CompositionClickThroughFailureStage::WindowUnavailable,
                ::GetLastError());
            return;
        }

        LONG_PTR extendedStyle = 0;
        DWORD error = ERROR_SUCCESS;
        if (!GetWindowStyle(
                window,
                GWL_EXSTYLE,
                extendedStyle,
                error))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetAppliedExtendedStyleFailed,
                error);
            return;
        }
        const LONG_PTR initialExtendedStyle =
            static_cast<LONG_PTR>(
                g_initialExtendedStyle.load(
                    std::memory_order_acquire));
        const LONG_PTR expectedCurrentStyle =
            enable
                ? initialExtendedStyle
                : initialExtendedStyle
                    | WS_EX_LAYERED
                    | WS_EX_TRANSPARENT;
        if (extendedStyle != expectedCurrentStyle)
        {
            Fail(
                enable
                    ? CompositionClickThroughFailureStage::
                        LayeredTransparentStyleVerificationFailed
                    : CompositionClickThroughFailureStage::
                        InitialExtendedStyleRestoreFailed);
            return;
        }
        const LONG_PTR requestedStyle =
            enable
                ? initialExtendedStyle
                    | WS_EX_LAYERED
                    | WS_EX_TRANSPARENT
                : initialExtendedStyle;

        ::SetLastError(ERROR_SUCCESS);
        const LONG_PTR previousStyle =
            ::SetWindowLongPtrW(
                window,
                GWL_EXSTYLE,
                requestedStyle);
        error = ::GetLastError();
        if (previousStyle == 0 && error != ERROR_SUCCESS)
        {
            Fail(
                enable
                    ? CompositionClickThroughFailureStage::
                        LayeredStyleApplyFailed
                    : CompositionClickThroughFailureStage::
                        InitialExtendedStyleRestoreFailed,
                error);
            return;
        }

        if (::SetWindowPos(
                window,
                nullptr,
                0,
                0,
                0,
                0,
                kFrameChangedFlags)
            == FALSE)
        {
            Fail(
                CompositionClickThroughFailureStage::
                    SetWindowPosFrameChangedFailed,
                ::GetLastError());
            return;
        }

        RECT rectangle{};
        if (::GetWindowRect(window, &rectangle) == FALSE)
        {
            Fail(
                CompositionClickThroughFailureStage::
                    ExtendedStyleVerificationFailed,
                ::GetLastError());
            return;
        }
        if (!g_initialRectAvailable.load(std::memory_order_acquire)
            || rectangle.left != g_initialX.load()
            || rectangle.top != g_initialY.load()
            || rectangle.right - rectangle.left != g_initialWidth.load()
            || rectangle.bottom - rectangle.top != g_initialHeight.load())
        {
            Fail(
                CompositionClickThroughFailureStage::
                    ExtendedStyleVerificationFailed);
            return;
        }

        LONG_PTR appliedStyle = 0;
        if (!GetWindowStyle(
                window,
                GWL_EXSTYLE,
                appliedStyle,
                error))
        {
            Fail(
                CompositionClickThroughFailureStage::
                    GetAppliedExtendedStyleFailed,
                error);
            return;
        }
        const bool hasLayeredTransparent =
            (appliedStyle & WS_EX_LAYERED) != 0
            && (appliedStyle & WS_EX_TRANSPARENT) != 0;
        if (appliedStyle != requestedStyle
            || (enable && !hasLayeredTransparent))
        {
            Fail(
                enable
                    ? CompositionClickThroughFailureStage::
                        LayeredTransparentStyleVerificationFailed
                    : CompositionClickThroughFailureStage::
                        InitialExtendedStyleRestoreFailed);
            return;
        }

        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(appliedStyle),
            std::memory_order_release);
        LONG_PTR currentStyle = 0;
        if (GetWindowStyle(window, GWL_STYLE, currentStyle, error))
        {
            g_currentStyle.store(
                static_cast<std::uint64_t>(currentStyle),
                std::memory_order_release);
        }
        g_lastWin32Error.store(ERROR_SUCCESS);
        g_lastAppliedThreadId.store(
            ::GetCurrentThreadId(),
            std::memory_order_release);
        g_enabled.store(enable, std::memory_order_release);
        g_initialExtendedStyleRestored.store(
            !enable && appliedStyle == initialExtendedStyle,
            std::memory_order_release);
        g_lastRequestSucceeded.store(true, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        const auto applied =
            g_appliedCount.fetch_add(1, std::memory_order_relaxed) + 1;
        if (applied > kExpectedRequestCount)
        {
            Fail(
                CompositionClickThroughFailureStage::
                    UnexpectedAppliedCount);
            return;
        }

        if (enable)
        {
            StoreState(CompositionClickThroughState::Enabled);
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Composition click-through "
                L"enabled.\n");
        }
        else if (applied == kExpectedRequestCount
            && g_enableRequestCount.load() == 1
            && g_disableRequestCount.load() == 1)
        {
            StoreState(CompositionClickThroughState::Completed);
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Composition click-through "
                L"disabled.\n");
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Composition click-through "
                L"diagnostics completed.\n");
        }
        else
        {
            StoreState(CompositionClickThroughState::Disabled);
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Composition click-through "
                L"disabled.\n");
        }
    }

    bool ShouldCompositionHitTestBeTransparent()
    {
        g_hitTestCount.fetch_add(1, std::memory_order_relaxed);
        if (!g_enabled.load(std::memory_order_acquire))
        {
            return false;
        }
        g_transparentHitTestCount.fetch_add(1, std::memory_order_relaxed);
        return true;
    }

    void NotifyCompositionClickThroughShutdownRequested()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        const auto currentState = g_state.load(std::memory_order_acquire);
        if (currentState
                != static_cast<std::int32_t>(
                    CompositionClickThroughState::NotStarted)
            && currentState
                != static_cast<std::int32_t>(
                    CompositionClickThroughState::Stopped)
            && currentState
                != static_cast<std::int32_t>(
                    CompositionClickThroughState::Failed))
        {
            StoreState(
                CompositionClickThroughState::ShutdownRequested);
        }
    }

    void StopCompositionClickThroughDiagnosticsOnUiThread()
    {
        NotifyCompositionClickThroughShutdownRequested();
        g_pending.store(false, std::memory_order_release);
        g_window.store(nullptr, std::memory_order_release);
        StoreState(CompositionClickThroughState::Stopped);
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionClickThroughState, g_state)
    GET_I32(GetCompositionClickThroughFailureStage, g_failureStage)
    GET_BOOL(IsCompositionClickThroughEnabled, g_enabled)
    GET_BOOL(IsCompositionClickThroughRequestPending, g_pending)
    GET_BOOL(
        DidCompositionClickThroughLastRequestSucceed,
        g_lastRequestSucceeded)
    GET_U32(
        GetCompositionClickThroughEnableRequestCount,
        g_enableRequestCount)
    GET_U32(
        GetCompositionClickThroughDisableRequestCount,
        g_disableRequestCount)
    GET_U32(GetCompositionClickThroughAppliedCount, g_appliedCount)
    GET_U32(GetCompositionClickThroughRejectedCount, g_rejectedCount)
    GET_U32(GetCompositionClickThroughLastWin32Error, g_lastWin32Error)
    GET_U64(GetCompositionWindowInitialStyle, g_initialStyle)
    GET_U64(GetCompositionWindowCurrentStyle, g_currentStyle)
    GET_U64(
        GetCompositionWindowInitialExtendedStyle,
        g_initialExtendedStyle)
    GET_U64(
        GetCompositionWindowCurrentExtendedStyle,
        g_currentExtendedStyle)
    GET_U64(GetCompositionWindowClassStyle, g_windowClassStyle)
    GET_BOOL(
        IsCompositionInitialExtendedStyleRestored,
        g_initialExtendedStyleRestored)
    GET_U32(GetCompositionClickThroughHitTestCount, g_hitTestCount)
    GET_U32(
        GetCompositionClickThroughTransparentHitTestCount,
        g_transparentHitTestCount)
    GET_U32(
        GetCompositionClickThroughLastRequestThreadId,
        g_lastRequestThreadId)
    GET_U32(
        GetCompositionClickThroughLastAppliedThreadId,
        g_lastAppliedThreadId)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
