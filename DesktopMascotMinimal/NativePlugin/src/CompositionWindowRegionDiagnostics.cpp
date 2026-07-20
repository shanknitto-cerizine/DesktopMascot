#include "DesktopMascotNative/CompositionWindowRegionDiagnostics.h"

#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <array>
#include <atomic>
#include <cwchar>

namespace
{
    using DesktopMascotNative::CompositionWindowRegionFailureStage;
    using DesktopMascotNative::CompositionWindowRegionState;

    constexpr std::int32_t kWidth = 64;
    constexpr std::int32_t kHeight = 64;
    constexpr std::int32_t kStride = 64;
    constexpr std::int32_t kThreshold = 128;
    constexpr std::size_t kByteCount =
        static_cast<std::size_t>(kStride * kHeight);
    constexpr UINT kFrameChangedFlags =
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
        | SWP_FRAMECHANGED;
    constexpr UINT kRedrawFlags =
        RDW_INVALIDATE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_threshold{0};
    std::atomic<std::int32_t> g_regionType{ERROR};
    std::atomic<std::int32_t> g_initialRegionType{ERROR};
    std::atomic<std::uint32_t> g_rectangleCount{0};
    std::atomic<std::uint32_t> g_coveredPixelCount{0};
    std::atomic<std::uint32_t> g_excludedPixelCount{0};
    std::atomic<std::uint32_t> g_lastWin32Error{0};
    std::atomic<std::uint64_t> g_publishedGeneration{0};
    std::atomic<std::uint64_t> g_initialExtendedStyle{0};
    std::atomic<std::uint64_t> g_diagnosticExtendedStyle{0};
    std::atomic<std::uint64_t> g_currentExtendedStyle{0};
    std::atomic<bool> g_regionApplied{false};
    std::atomic<bool> g_regionChanged{false};
    std::atomic<bool> g_regionActive{false};
    std::atomic<bool> g_initialRegionRestored{false};
    std::atomic<bool> g_styleChanged{false};
    std::atomic<bool> g_styleRestored{false};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_lastApplySucceeded{false};
    std::atomic<bool> g_topLeftInside{false};
    std::atomic<bool> g_topRightInside{false};
    std::atomic<bool> g_bottomLeftInside{false};
    std::atomic<bool> g_bottomRightInside{false};
    std::atomic<bool> g_centerInside{false};

    // Accessed only by the Composition UI thread.
    HRGN g_savedInitialRegion = nullptr;

    void StoreState(CompositionWindowRegionState state)
    {
        g_state.store(
            static_cast<std::int32_t>(state),
            std::memory_order_release);
    }

    void Fail(
        CompositionWindowRegionFailureStage stage,
        DWORD error = ERROR_SUCCESS)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        g_lastWin32Error.store(error, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastApplySucceeded.store(false, std::memory_order_release);
        StoreState(CompositionWindowRegionState::Failed);
        wchar_t message[176]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Window region diagnostics failed: "
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

    HRGN DuplicateRegion(HRGN source)
    {
        HRGN duplicate = ::CreateRectRgn(0, 0, 0, 0);
        if (duplicate == nullptr)
        {
            return nullptr;
        }
        if (::CombineRgn(duplicate, source, nullptr, RGN_COPY) == ERROR)
        {
            ::DeleteObject(duplicate);
            return nullptr;
        }
        return duplicate;
    }

    bool RedrawRegionWindow(HWND window, DWORD& error)
    {
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
        if (::RedrawWindow(window, nullptr, nullptr, kRedrawFlags) == FALSE)
        {
            error = ::GetLastError();
            return false;
        }
        return true;
    }

    bool CaptureInitialRegion(HWND window, DWORD& error)
    {
        HRGN captured = ::CreateRectRgn(0, 0, 0, 0);
        if (captured == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        ::SetLastError(ERROR_SUCCESS);
        const int type = ::GetWindowRgn(window, captured);
        error = ::GetLastError();
        g_initialRegionType.store(type);
        if (type == ERROR)
        {
            ::DeleteObject(captured);
            g_savedInitialRegion = nullptr;
            return error == ERROR_SUCCESS;
        }
        if (type == NULLREGION)
        {
            ::DeleteObject(captured);
            g_savedInitialRegion = nullptr;
            return true;
        }
        g_savedInitialRegion = captured;
        return true;
    }

    HRGN BuildRegion(
        const std::array<std::uint8_t, kByteCount>& pixels,
        std::uint32_t& rectangleCount,
        std::uint32_t& coveredPixelCount,
        DWORD& error)
    {
        HRGN combined = ::CreateRectRgn(0, 0, 0, 0);
        if (combined == nullptr)
        {
            error = ::GetLastError();
            return nullptr;
        }
        for (std::int32_t y = 0; y < kHeight; ++y)
        {
            std::int32_t x = 0;
            while (x < kWidth)
            {
                while (x < kWidth
                       && pixels[static_cast<std::size_t>(
                              y * kStride + x)] < kThreshold)
                {
                    ++x;
                }
                if (x >= kWidth)
                {
                    break;
                }
                const std::int32_t runStart = x;
                while (x < kWidth
                       && pixels[static_cast<std::size_t>(
                              y * kStride + x)] >= kThreshold)
                {
                    ++x;
                }
                HRGN run = ::CreateRectRgn(runStart, y, x, y + 1);
                if (run == nullptr)
                {
                    error = ::GetLastError();
                    ::DeleteObject(combined);
                    return nullptr;
                }
                const int combineResult =
                    ::CombineRgn(combined, combined, run, RGN_OR);
                ::DeleteObject(run);
                if (combineResult == ERROR)
                {
                    error = ::GetLastError();
                    ::DeleteObject(combined);
                    return nullptr;
                }
                ++rectangleCount;
                coveredPixelCount +=
                    static_cast<std::uint32_t>(x - runStart);
            }
        }
        return combined;
    }

    bool ValidateRepresentativePoints(HRGN region)
    {
        const bool topLeft = ::PtInRegion(region, 8, 8) != FALSE;
        const bool topRight = ::PtInRegion(region, 55, 8) != FALSE;
        const bool bottomLeft = ::PtInRegion(region, 8, 55) != FALSE;
        const bool bottomRight = ::PtInRegion(region, 55, 55) != FALSE;
        const bool center = ::PtInRegion(region, 32, 32) != FALSE;
        g_topLeftInside.store(topLeft);
        g_topRightInside.store(topRight);
        g_bottomLeftInside.store(bottomLeft);
        g_bottomRightInside.store(bottomRight);
        g_centerInside.store(center);
        return !topLeft && !topRight && bottomLeft && bottomRight && center;
    }

    bool VerifyAppliedRegion(HWND window, DWORD& error)
    {
        HRGN actual = ::CreateRectRgn(0, 0, 0, 0);
        if (actual == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        ::SetLastError(ERROR_SUCCESS);
        const int type = ::GetWindowRgn(window, actual);
        error = ::GetLastError();
        g_regionType.store(type);
        const bool valid =
            type != ERROR
            && type != NULLREGION
            && ValidateRepresentativePoints(actual);
        ::DeleteObject(actual);
        return valid;
    }

    bool RestoreInitialRegion(HWND window, DWORD& error)
    {
        const int initialType = g_initialRegionType.load();
        if (initialType == ERROR || initialType == NULLREGION)
        {
            if (::SetWindowRgn(window, nullptr, TRUE) == 0)
            {
                error = ::GetLastError();
                return false;
            }
            HRGN verification = ::CreateRectRgn(0, 0, 0, 0);
            if (verification == nullptr)
            {
                error = ::GetLastError();
                return false;
            }
            const int restoredType =
                ::GetWindowRgn(window, verification);
            ::DeleteObject(verification);
            return restoredType == initialType;
        }
        if (g_savedInitialRegion == nullptr)
        {
            return false;
        }
        HRGN restored = DuplicateRegion(g_savedInitialRegion);
        if (restored == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        if (::SetWindowRgn(window, restored, TRUE) == 0)
        {
            error = ::GetLastError();
            ::DeleteObject(restored);
            return false;
        }
        // Windows owns restored after SetWindowRgn succeeds.
        HRGN verification = ::CreateRectRgn(0, 0, 0, 0);
        if (verification == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        const int restoredType = ::GetWindowRgn(window, verification);
        const bool equal =
            restoredType == initialType
            && ::EqualRgn(verification, g_savedInitialRegion) != FALSE;
        ::DeleteObject(verification);
        return equal;
    }

    void ReleaseSavedInitialRegion()
    {
        if (g_savedInitialRegion != nullptr)
        {
            ::DeleteObject(g_savedInitialRegion);
            g_savedInitialRegion = nullptr;
        }
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionWindowRegionDiagnostics()
    {
        g_savedInitialRegion = nullptr;
        g_window.store(nullptr);
        g_state.store(0);
        g_failureStage.store(0);
        g_threshold.store(0);
        g_regionType.store(ERROR);
        g_initialRegionType.store(ERROR);
        g_rectangleCount.store(0);
        g_coveredPixelCount.store(0);
        g_excludedPixelCount.store(0);
        g_lastWin32Error.store(0);
        g_publishedGeneration.store(0);
        g_initialExtendedStyle.store(0);
        g_diagnosticExtendedStyle.store(0);
        g_currentExtendedStyle.store(0);
        g_regionApplied.store(false);
        g_regionChanged.store(false);
        g_regionActive.store(false);
        g_initialRegionRestored.store(false);
        g_styleChanged.store(false);
        g_styleRestored.store(false);
        g_pending.store(false);
        g_lastApplySucceeded.store(false);
        g_topLeftInside.store(false);
        g_topRightInside.store(false);
        g_bottomLeftInside.store(false);
        g_bottomRightInside.store(false);
        g_centerInside.store(false);
    }

    void SetCompositionWindowRegionUiWindow(void* window)
    {
        g_window.store(static_cast<HWND>(window), std::memory_order_release);
    }

    std::int32_t StartCompositionWindowRegionDiagnostics(
        std::int32_t alphaThreshold)
    {
        std::int32_t expected = 0;
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    CompositionWindowRegionState::WaitingForComposition)))
        {
            return 0;
        }
        if (GetCompositionInitializationState()
            != static_cast<std::int32_t>(
                CompositionInitializationState::MessageLoopRunning))
        {
            Fail(CompositionWindowRegionFailureStage::CompositionNotReady);
            return 0;
        }
        if (alphaThreshold != kThreshold)
        {
            Fail(CompositionWindowRegionFailureStage::InvalidThreshold);
            return 0;
        }
        if (!IsCompositionAlphaMaskAvailable())
        {
            StoreState(CompositionWindowRegionState::WaitingForMask);
            Fail(CompositionWindowRegionFailureStage::AlphaMaskUnavailable);
            return 0;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr)
        {
            Fail(CompositionWindowRegionFailureStage::WindowUnavailable);
            return 0;
        }
        g_threshold.store(alphaThreshold);
        g_pending.store(true, std::memory_order_release);
        g_lastApplySucceeded.store(false);
        StoreState(CompositionWindowRegionState::ApplyRequested);
        if (::PostMessageW(
                window,
                kCompositionWindowRegionApplyMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                CompositionWindowRegionFailureStage::SetWindowRegionFailed,
                ::GetLastError());
            return 0;
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Window region diagnostics started.\n");
        return 1;
    }

    void HandleCompositionWindowRegionApplyMessage()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(CompositionWindowRegionFailureStage::WindowUnavailable);
            return;
        }
        DWORD error = ERROR_SUCCESS;
        LONG_PTR initialStyle = 0;
        if (!GetExtendedStyle(window, initialStyle, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    GetInitialExtendedStyleFailed,
                error);
            return;
        }
        g_initialExtendedStyle.store(
            static_cast<std::uint64_t>(initialStyle));
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(initialStyle));
        if ((initialStyle & WS_EX_TRANSPARENT) != 0)
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    ExistingTransparentStyleUnsupported);
            return;
        }
        if (!CaptureInitialRegion(window, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::GetInitialRegionFailed,
                error);
            return;
        }

        std::array<std::uint8_t, kByteCount> pixels{};
        std::int32_t width = 0;
        std::int32_t height = 0;
        std::int32_t stride = 0;
        std::int32_t threshold = 0;
        std::uint64_t generation = 0;
        if (!TryCopyCompositionAlphaMaskSnapshot(
                pixels.data(),
                pixels.size(),
                width,
                height,
                stride,
                threshold,
                generation)
            || width != kWidth
            || height != kHeight
            || stride != kStride
            || threshold != kThreshold)
        {
            Fail(
                CompositionWindowRegionFailureStage::SnapshotCopyFailed);
            return;
        }

        std::uint32_t rectangleCount = 0;
        std::uint32_t coveredPixelCount = 0;
        HRGN region = BuildRegion(
            pixels,
            rectangleCount,
            coveredPixelCount,
            error);
        if (region == nullptr)
        {
            Fail(
                error == ERROR_SUCCESS
                    ? CompositionWindowRegionFailureStage::CombineRegionFailed
                    : CompositionWindowRegionFailureStage::CreateRegionFailed,
                error);
            return;
        }
        if (!ValidateRepresentativePoints(region))
        {
            ::DeleteObject(region);
            Fail(
                CompositionWindowRegionFailureStage::
                    RepresentativeValidationFailed);
            return;
        }

        const LONG_PTR diagnosticStyle = initialStyle | WS_EX_LAYERED;
        g_diagnosticExtendedStyle.store(
            static_cast<std::uint64_t>(diagnosticStyle));
        if (!ApplyExtendedStyle(window, diagnosticStyle, error))
        {
            ::DeleteObject(region);
            Fail(
                CompositionWindowRegionFailureStage::ApplyLayeredStyleFailed,
                error);
            return;
        }
        g_styleChanged.store(true, std::memory_order_release);
        LONG_PTR appliedStyle = 0;
        if (!GetExtendedStyle(window, appliedStyle, error)
            || appliedStyle != diagnosticStyle
            || (appliedStyle & WS_EX_TRANSPARENT) != 0)
        {
            ::DeleteObject(region);
            Fail(
                CompositionWindowRegionFailureStage::ApplyLayeredStyleFailed,
                error);
            return;
        }
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(appliedStyle));

        if (::SetWindowRgn(window, region, TRUE) == 0)
        {
            error = ::GetLastError();
            ::DeleteObject(region);
            Fail(
                CompositionWindowRegionFailureStage::SetWindowRegionFailed,
                error);
            return;
        }
        // Windows owns region after SetWindowRgn succeeds.
        g_regionChanged.store(true, std::memory_order_release);
        g_regionApplied.store(true, std::memory_order_release);
        if (!VerifyAppliedRegion(window, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::VerifyWindowRegionFailed,
                error);
            return;
        }
        if (!RedrawRegionWindow(window, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::RedrawFailed,
                error);
            return;
        }

        g_rectangleCount.store(rectangleCount);
        g_coveredPixelCount.store(coveredPixelCount);
        g_excludedPixelCount.store(
            static_cast<std::uint32_t>(kByteCount) - coveredPixelCount);
        g_publishedGeneration.store(generation);
        g_regionActive.store(true, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastApplySucceeded.store(true, std::memory_order_release);
        g_lastWin32Error.store(0);
        StoreState(CompositionWindowRegionState::VisualTestRunning);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Window region ready for visual "
            L"verification.\n");
    }

    bool ShouldCompositionWindowRegionReturnClient()
    {
        return g_regionActive.load(std::memory_order_acquire);
    }

    std::int32_t CompleteCompositionWindowRegionDiagnostics()
    {
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                CompositionWindowRegionState::VisualTestRunning))
        {
            return 0;
        }
        StoreState(CompositionWindowRegionState::Completed);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Window region diagnostics completed.\n");
        return 1;
    }

    std::int32_t StopCompositionWindowRegionDiagnostics()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr || g_pending.exchange(true))
        {
            return 0;
        }
        if (!g_regionChanged.load(std::memory_order_acquire)
            && !g_styleChanged.load(std::memory_order_acquire))
        {
            g_pending.store(false);
            g_lastApplySucceeded.store(true);
            StoreState(CompositionWindowRegionState::Stopped);
            return 1;
        }
        g_regionActive.store(false, std::memory_order_release);
        StoreState(CompositionWindowRegionState::RestoreRequested);
        if (::PostMessageW(
                window,
                kCompositionWindowRegionRestoreMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    RestoreInitialRegionFailed,
                ::GetLastError());
            return 0;
        }
        return 1;
    }

    void HandleCompositionWindowRegionRestoreMessage()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        DWORD error = ERROR_SUCCESS;
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(CompositionWindowRegionFailureStage::WindowUnavailable);
            return;
        }
        if (g_regionChanged.load(std::memory_order_acquire)
            && !RestoreInitialRegion(window, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    RestoreInitialRegionFailed,
                error);
            return;
        }
        g_regionChanged.store(false, std::memory_order_release);
        g_initialRegionRestored.store(true, std::memory_order_release);
        ReleaseSavedInitialRegion();

        const LONG_PTR initialStyle = static_cast<LONG_PTR>(
            g_initialExtendedStyle.load(std::memory_order_acquire));
        if (g_styleChanged.load(std::memory_order_acquire)
            && !ApplyExtendedStyle(window, initialStyle, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    RestoreInitialStyleFailed,
                error);
            return;
        }
        LONG_PTR restoredStyle = 0;
        if (!GetExtendedStyle(window, restoredStyle, error)
            || restoredStyle != initialStyle)
        {
            Fail(
                CompositionWindowRegionFailureStage::
                    RestoreInitialStyleFailed,
                error);
            return;
        }
        if (!RedrawRegionWindow(window, error))
        {
            Fail(
                CompositionWindowRegionFailureStage::RedrawFailed,
                error);
            return;
        }
        g_currentExtendedStyle.store(
            static_cast<std::uint64_t>(restoredStyle));
        g_styleChanged.store(false, std::memory_order_release);
        g_styleRestored.store(true, std::memory_order_release);
        g_regionActive.store(false, std::memory_order_release);
        g_pending.store(false, std::memory_order_release);
        g_lastApplySucceeded.store(true, std::memory_order_release);
        g_lastWin32Error.store(0);
        StoreState(CompositionWindowRegionState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Window region diagnostics stopped.\n");
    }

    void StopCompositionWindowRegionDiagnosticsOnUiThread()
    {
        if (g_regionChanged.load(std::memory_order_acquire)
            || g_styleChanged.load(std::memory_order_acquire))
        {
            HandleCompositionWindowRegionRestoreMessage();
        }
        ReleaseSavedInitialRegion();
        g_regionActive.store(false);
        g_pending.store(false);
        g_window.store(nullptr);
        if (g_state.load() != static_cast<std::int32_t>(
                                  CompositionWindowRegionState::Failed))
        {
            StoreState(CompositionWindowRegionState::Stopped);
        }
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionWindowRegionState, g_state)
    GET_I32(GetCompositionWindowRegionFailureStage, g_failureStage)
    GET_I32(GetCompositionWindowRegionThreshold, g_threshold)
    GET_BOOL(IsCompositionWindowRegionApplied, g_regionApplied)
    GET_I32(GetCompositionWindowRegionType, g_regionType)
    GET_U32(GetCompositionWindowRegionRectangleCount, g_rectangleCount)
    GET_U32(
        GetCompositionWindowRegionCoveredPixelCount,
        g_coveredPixelCount)
    GET_U32(
        GetCompositionWindowRegionExcludedPixelCount,
        g_excludedPixelCount)
    GET_I32(GetCompositionWindowInitialRegionType, g_initialRegionType)
    GET_BOOL(
        DidCompositionWindowInitialRegionRestoreSucceed,
        g_initialRegionRestored)
    GET_BOOL(IsCompositionWindowRegionApplyRequestPending, g_pending)
    GET_BOOL(
        DidCompositionWindowRegionLastApplySucceed,
        g_lastApplySucceeded)
    GET_U32(GetCompositionWindowRegionLastWin32Error, g_lastWin32Error)
    GET_U64(
        GetCompositionWindowRegionPublishedGeneration,
        g_publishedGeneration)
    GET_BOOL(IsCompositionWindowRegionTopLeftInside, g_topLeftInside)
    GET_BOOL(IsCompositionWindowRegionTopRightInside, g_topRightInside)
    GET_BOOL(IsCompositionWindowRegionBottomLeftInside, g_bottomLeftInside)
    GET_BOOL(IsCompositionWindowRegionBottomRightInside, g_bottomRightInside)
    GET_BOOL(IsCompositionWindowRegionCenterInside, g_centerInside)
    GET_U64(
        GetCompositionWindowRegionInitialExtendedStyle,
        g_initialExtendedStyle)
    GET_U64(
        GetCompositionWindowRegionDiagnosticExtendedStyle,
        g_diagnosticExtendedStyle)
    GET_U64(
        GetCompositionWindowRegionCurrentExtendedStyle,
        g_currentExtendedStyle)
    GET_BOOL(
        DidCompositionWindowRegionStyleRestoreSucceed,
        g_styleRestored)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
