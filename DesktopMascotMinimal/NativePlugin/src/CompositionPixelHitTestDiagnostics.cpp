#include "DesktopMascotNative/CompositionPixelHitTestDiagnostics.h"

#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>
#include <windowsx.h>

#include <atomic>
#include <cwchar>

namespace
{
    using DesktopMascotNative::CompositionPixelHitTestFailureStage;
    using DesktopMascotNative::CompositionPixelHitTestState;
    using DesktopMascotNative::PixelHitTestResult;

    constexpr LONG_PTR kUnsupportedClassStyles = CS_OWNDC | CS_CLASSDC;
    constexpr UINT kFrameChangedFlags =
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
        | SWP_FRAMECHANGED;
    constexpr std::int32_t kRequiredThreshold = 128;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_threshold{0};
    std::atomic<std::uint32_t> g_totalCount{0};
    std::atomic<std::uint32_t> g_transparentCount{0};
    std::atomic<std::uint32_t> g_opaqueCount{0};
    std::atomic<std::uint32_t> g_outsideClientCount{0};
    std::atomic<std::uint32_t> g_maskUnavailableCount{0};
    std::atomic<std::uint32_t> g_screenToClientFailureCount{0};
    std::atomic<std::int32_t> g_lastX{-1};
    std::atomic<std::int32_t> g_lastY{-1};
    std::atomic<std::int32_t> g_lastAlpha{-1};
    std::atomic<std::int32_t> g_lastResult{0};
    std::atomic<std::uint64_t> g_publishedGeneration{0};
    std::atomic<std::uint32_t> g_lastWin32Error{0};
    std::atomic<std::uint64_t> g_initialExtendedStyle{0};
    std::atomic<std::uint64_t> g_diagnosticExtendedStyle{0};
    std::atomic<std::uint64_t> g_currentExtendedStyle{0};
    std::atomic<std::uint64_t> g_windowClassStyle{0};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_styleChanged{false};
    std::atomic<bool> g_restoreSucceeded{false};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_lastRequestSucceeded{false};

    void StoreState(CompositionPixelHitTestState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    void Fail(
        CompositionPixelHitTestFailureStage stage,
        DWORD error = ERROR_SUCCESS)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        g_lastWin32Error.store(error, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastRequestSucceeded.store(false, std::memory_order_release);
        StoreState(CompositionPixelHitTestState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Pixel hit-test diagnostics failed: "
            L"stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool GetExtendedStyle(HWND window, LONG_PTR& style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        style = ::GetWindowLongPtrW(window, GWL_EXSTYLE);
        error = ::GetLastError();
        return style != 0 || error == ERROR_SUCCESS;
    }

    bool GetClassStyle(HWND window, ULONG_PTR& style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        style = ::GetClassLongPtrW(window, GCL_STYLE);
        error = ::GetLastError();
        return style != 0 || error == ERROR_SUCCESS;
    }

    bool ApplyExtendedStyle(HWND window, LONG_PTR style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        const LONG_PTR previous =
            ::SetWindowLongPtrW(window, GWL_EXSTYLE, style);
        error = ::GetLastError();
        if (previous == 0 && error != ERROR_SUCCESS)
        {
            return false;
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
            error = ::GetLastError();
            return false;
        }
        return true;
    }

    void RecordResult(
        std::int32_t x,
        std::int32_t y,
        std::int32_t alpha,
        PixelHitTestResult result,
        std::uint64_t generation)
    {
        g_lastX.store(x, std::memory_order_relaxed);
        g_lastY.store(y, std::memory_order_relaxed);
        g_lastAlpha.store(alpha, std::memory_order_relaxed);
        g_lastResult.store(static_cast<std::int32_t>(result));
        g_publishedGeneration.store(generation, std::memory_order_relaxed);
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionPixelHitTestDiagnostics()
    {
        g_window.store(nullptr);
        g_state.store(0);
        g_failureStage.store(0);
        g_threshold.store(0);
        g_totalCount.store(0);
        g_transparentCount.store(0);
        g_opaqueCount.store(0);
        g_outsideClientCount.store(0);
        g_maskUnavailableCount.store(0);
        g_screenToClientFailureCount.store(0);
        g_lastX.store(-1);
        g_lastY.store(-1);
        g_lastAlpha.store(-1);
        g_lastResult.store(0);
        g_publishedGeneration.store(0);
        g_lastWin32Error.store(0);
        g_initialExtendedStyle.store(0);
        g_diagnosticExtendedStyle.store(0);
        g_currentExtendedStyle.store(0);
        g_windowClassStyle.store(0);
        g_enabled.store(false);
        g_styleChanged.store(false);
        g_restoreSucceeded.store(false);
        g_pending.store(false);
        g_lastRequestSucceeded.store(false);
    }

    void SetCompositionPixelHitTestUiWindow(void* window)
    {
        g_window.store(static_cast<HWND>(window), std::memory_order_release);
    }

    std::int32_t StartCompositionPixelHitTestDiagnostics(
        std::int32_t alphaThreshold)
    {
        std::int32_t expected = 0;
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    CompositionPixelHitTestState::WaitingForComposition),
                std::memory_order_acq_rel))
        {
            return 0;
        }
        if (GetCompositionInitializationState()
            != static_cast<std::int32_t>(
                CompositionInitializationState::MessageLoopRunning))
        {
            Fail(CompositionPixelHitTestFailureStage::CompositionNotReady);
            return 0;
        }
        if (alphaThreshold != kRequiredThreshold)
        {
            Fail(CompositionPixelHitTestFailureStage::InvalidThreshold);
            return 0;
        }
        if (!IsCompositionAlphaMaskAvailable())
        {
            StoreState(CompositionPixelHitTestState::WaitingForMask);
            Fail(CompositionPixelHitTestFailureStage::AlphaMaskUnavailable);
            return 0;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr)
        {
            Fail(CompositionPixelHitTestFailureStage::WindowUnavailable);
            return 0;
        }
        g_threshold.store(alphaThreshold);
        g_pending.store(true, std::memory_order_release);
        g_lastRequestSucceeded.store(false);
        StoreState(CompositionPixelHitTestState::ApplyingWindowStyle);
        if (::PostMessageW(
                window,
                kCompositionPixelHitTestApplyMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                CompositionPixelHitTestFailureStage::ApplyLayeredStyleFailed,
                ::GetLastError());
            return 0;
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Pixel hit-test diagnostics started.\n");
        return 1;
    }

    void HandleCompositionPixelHitTestApplyMessage()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(CompositionPixelHitTestFailureStage::WindowUnavailable);
            return;
        }
        DWORD error = ERROR_SUCCESS;
        LONG_PTR initialStyle = 0;
        if (!GetExtendedStyle(window, initialStyle, error))
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    GetInitialExtendedStyleFailed,
                error);
            return;
        }
        ULONG_PTR classStyle = 0;
        if (!GetClassStyle(window, classStyle, error))
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    GetInitialExtendedStyleFailed,
                error);
            return;
        }
        g_initialExtendedStyle.store(
            static_cast<std::uint64_t>(initialStyle));
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(initialStyle));
        g_windowClassStyle.store(static_cast<std::uint64_t>(classStyle));
        if ((initialStyle & WS_EX_TRANSPARENT) != 0)
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    ExistingTransparentStyleUnsupported);
            return;
        }
        if ((classStyle & kUnsupportedClassStyles) != 0)
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    LayeredWindowClassStyleUnsupported);
            return;
        }
        const LONG_PTR diagnosticStyle = initialStyle | WS_EX_LAYERED;
        g_diagnosticExtendedStyle.store(
            static_cast<std::uint64_t>(diagnosticStyle));
        if (!ApplyExtendedStyle(window, diagnosticStyle, error))
        {
            Fail(
                CompositionPixelHitTestFailureStage::ApplyLayeredStyleFailed,
                error);
            return;
        }
        g_styleChanged.store(true, std::memory_order_release);
        LONG_PTR appliedStyle = 0;
        if (!GetExtendedStyle(window, appliedStyle, error)
            || appliedStyle != diagnosticStyle
            || (appliedStyle & WS_EX_LAYERED) == 0
            || (appliedStyle & WS_EX_TRANSPARENT) != 0)
        {
            Fail(
                CompositionPixelHitTestFailureStage::VerifyLayeredStyleFailed,
                error);
            return;
        }
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(appliedStyle));
        g_enabled.store(true, std::memory_order_release);
        g_restoreSucceeded.store(false);
        g_pending.store(false, std::memory_order_release);
        g_lastRequestSucceeded.store(true, std::memory_order_release);
        g_lastWin32Error.store(0);
        StoreState(CompositionPixelHitTestState::ReadyForVisualTest);
        StoreState(CompositionPixelHitTestState::VisualTestRunning);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Pixel hit-test ready for visual "
            L"verification.\n");
    }

    bool TryHandleCompositionPixelHitTest(
        void* windowValue,
        std::intptr_t lParam,
        std::intptr_t& result)
    {
        if (!g_enabled.load(std::memory_order_acquire))
        {
            return false;
        }
        g_totalCount.fetch_add(1, std::memory_order_relaxed);
        const HWND window = static_cast<HWND>(windowValue);
        POINT point{
            GET_X_LPARAM(static_cast<LPARAM>(lParam)),
            GET_Y_LPARAM(static_cast<LPARAM>(lParam))};
        if (::ScreenToClient(window, &point) == FALSE)
        {
            const DWORD error = ::GetLastError();
            g_screenToClientFailureCount.fetch_add(1);
            RecordResult(
                -1,
                -1,
                -1,
                PixelHitTestResult::CoordinateConversionFailed,
                g_publishedGeneration.load());
            Fail(
                CompositionPixelHitTestFailureStage::ScreenToClientFailed,
                error);
            return false;
        }
        if (point.x < 0 || point.y < 0 || point.x >= 64 || point.y >= 64)
        {
            g_outsideClientCount.fetch_add(1);
            RecordResult(
                point.x,
                point.y,
                -1,
                PixelHitTestResult::OutsideClient,
                g_publishedGeneration.load());
            result = HTNOWHERE;
            return true;
        }

        std::uint8_t alpha = 0;
        std::int32_t threshold = 0;
        std::uint64_t generation = 0;
        if (!TryReadCompositionAlphaMaskPixel(
                point.x,
                point.y,
                alpha,
                threshold,
                generation))
        {
            g_maskUnavailableCount.fetch_add(1);
            RecordResult(
                point.x,
                point.y,
                -1,
                PixelHitTestResult::MaskUnavailable,
                0);
            result = HTCLIENT;
            return true;
        }
        if (threshold != g_threshold.load(std::memory_order_acquire))
        {
            Fail(CompositionPixelHitTestFailureStage::SnapshotReadFailed);
            return false;
        }
        if (alpha < threshold)
        {
            g_transparentCount.fetch_add(1);
            RecordResult(
                point.x,
                point.y,
                alpha,
                PixelHitTestResult::Transparent,
                generation);
            result = HTTRANSPARENT;
            return true;
        }
        g_opaqueCount.fetch_add(1);
        RecordResult(
            point.x,
            point.y,
            alpha,
            PixelHitTestResult::Opaque,
            generation);
        result = HTCLIENT;
        return true;
    }

    std::int32_t CompleteCompositionPixelHitTestDiagnostics()
    {
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                CompositionPixelHitTestState::VisualTestRunning))
        {
            return 0;
        }
        StoreState(CompositionPixelHitTestState::Completed);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Pixel hit-test diagnostics "
            L"completed.\n");
        return 1;
    }

    std::int32_t StopCompositionPixelHitTestDiagnostics()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || g_pending.exchange(true))
        {
            return 0;
        }
        if (!g_styleChanged.load(std::memory_order_acquire))
        {
            g_pending.store(false, std::memory_order_release);
            g_lastRequestSucceeded.store(true, std::memory_order_release);
            StoreState(CompositionPixelHitTestState::Stopped);
            return 1;
        }
        g_lastRequestSucceeded.store(false);
        StoreState(CompositionPixelHitTestState::RestoreRequested);
        if (::PostMessageW(
                window,
                kCompositionPixelHitTestRestoreMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    RestoreInitialStyleFailed,
                ::GetLastError());
            return 0;
        }
        return 1;
    }

    void HandleCompositionPixelHitTestRestoreMessage()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        const LONG_PTR initialStyle = static_cast<LONG_PTR>(
            g_initialExtendedStyle.load(std::memory_order_acquire));
        DWORD error = ERROR_SUCCESS;
        if (window == nullptr
            || !ApplyExtendedStyle(window, initialStyle, error))
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    RestoreInitialStyleFailed,
                error);
            return;
        }
        LONG_PTR restoredStyle = 0;
        if (!GetExtendedStyle(window, restoredStyle, error)
            || restoredStyle != initialStyle)
        {
            Fail(
                CompositionPixelHitTestFailureStage::
                    RestoreInitialStyleFailed,
                error);
            return;
        }
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(restoredStyle));
        g_enabled.store(false, std::memory_order_release);
        g_styleChanged.store(false, std::memory_order_release);
        g_restoreSucceeded.store(true, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastRequestSucceeded.store(true, std::memory_order_release);
        g_lastWin32Error.store(0);
        StoreState(CompositionPixelHitTestState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Pixel hit-test diagnostics stopped.\n");
    }

    void StopCompositionPixelHitTestDiagnosticsOnUiThread()
    {
        if (g_styleChanged.load(std::memory_order_acquire))
        {
            HandleCompositionPixelHitTestRestoreMessage();
        }
        g_enabled.store(false, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_window.store(nullptr, std::memory_order_release);
        if (g_state.load() != static_cast<std::int32_t>(
                                  CompositionPixelHitTestState::Failed))
        {
            StoreState(CompositionPixelHitTestState::Stopped);
        }
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionPixelHitTestState, g_state)
    GET_I32(GetCompositionPixelHitTestFailureStage, g_failureStage)
    GET_BOOL(IsCompositionPixelHitTestEnabled, g_enabled)
    GET_I32(GetCompositionPixelHitTestThreshold, g_threshold)
    GET_U32(GetCompositionPixelHitTestTotalCount, g_totalCount)
    GET_U32(
        GetCompositionPixelHitTestTransparentCount,
        g_transparentCount)
    GET_U32(GetCompositionPixelHitTestOpaqueCount, g_opaqueCount)
    GET_U32(
        GetCompositionPixelHitTestOutsideClientCount,
        g_outsideClientCount)
    GET_U32(
        GetCompositionPixelHitTestMaskUnavailableCount,
        g_maskUnavailableCount)
    GET_U32(
        GetCompositionPixelHitTestScreenToClientFailureCount,
        g_screenToClientFailureCount)
    GET_I32(GetCompositionPixelHitTestLastX, g_lastX)
    GET_I32(GetCompositionPixelHitTestLastY, g_lastY)
    GET_I32(GetCompositionPixelHitTestLastAlpha, g_lastAlpha)
    GET_I32(GetCompositionPixelHitTestLastResult, g_lastResult)
    GET_U64(
        GetCompositionPixelHitTestPublishedGeneration,
        g_publishedGeneration)
    GET_U32(GetCompositionPixelHitTestLastWin32Error, g_lastWin32Error)
    GET_BOOL(
        DidCompositionPixelHitTestStyleRestoreSucceed,
        g_restoreSucceeded)
    GET_BOOL(IsCompositionPixelHitTestRequestPending, g_pending)
    GET_BOOL(
        DidCompositionPixelHitTestLastRequestSucceed,
        g_lastRequestSucceeded)
    GET_U64(
        GetCompositionPixelHitTestInitialExtendedStyle,
        g_initialExtendedStyle)
    GET_U64(
        GetCompositionPixelHitTestDiagnosticExtendedStyle,
        g_diagnosticExtendedStyle)
    GET_U64(
        GetCompositionPixelHitTestCurrentExtendedStyle,
        g_currentExtendedStyle)
    GET_U64(
        GetCompositionPixelHitTestWindowClassStyle,
        g_windowClassStyle)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
