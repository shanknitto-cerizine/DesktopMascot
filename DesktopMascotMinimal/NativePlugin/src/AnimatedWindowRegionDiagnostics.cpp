#include "DesktopMascotNative/AnimatedWindowRegionDiagnostics.h"

#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <array>
#include <atomic>
#include <algorithm>
#include <cstddef>
#include <cwchar>
#include <map>
#include <vector>

namespace
{
    using DesktopMascotNative::AnimatedWindowRegionFailureStage;
    using DesktopMascotNative::AnimatedWindowRegionState;
    using DesktopMascotNative::InitialWindowRegionState;

    constexpr std::int32_t kWidth = 256;
    constexpr std::int32_t kHeight = 256;
    constexpr std::int32_t kStride = 256;
    constexpr std::int32_t kThreshold = 128;
    constexpr std::size_t kByteCount = kWidth * kHeight;
    constexpr UINT kFrameChangedFlags =
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
        | SWP_FRAMECHANGED;

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_threshold{0};
    std::atomic<std::int32_t> g_intervalMilliseconds{0};
    std::atomic<std::uint64_t> g_publishedGeneration{0};
    std::atomic<std::uint64_t> g_buildGeneration{0};
    std::atomic<std::uint64_t> g_requestedGeneration{0};
    std::atomic<std::uint64_t> g_appliedGeneration{0};
    std::atomic<std::uint32_t> g_buildCount{0};
    std::atomic<std::uint32_t> g_requestCount{0};
    std::atomic<std::uint32_t> g_evaluationCount{0};
    std::atomic<std::uint32_t> g_executionCount{0};
    std::atomic<std::uint32_t> g_successCount{0};
    std::atomic<std::uint32_t> g_failureCount{0};
    std::atomic<std::uint32_t> g_skippedCount{0};
    std::atomic<std::uint32_t> g_duplicateCount{0};
    std::atomic<std::uint32_t> g_rectangleCount{0};
    std::atomic<std::uint32_t> g_mergedRectangleCount{0};
    std::atomic<std::uint32_t> g_regionDataRectangleCount{0};
    std::atomic<std::uint32_t> g_coveredPixelCount{0};
    std::atomic<std::uint32_t> g_excludedPixelCount{0};
    std::atomic<std::uint32_t> g_initialGdiCount{0};
    std::atomic<std::uint32_t> g_peakGdiCount{0};
    std::atomic<std::uint32_t> g_finalGdiCount{0};
    std::atomic<std::int32_t> g_gdiDelta{0};
    std::atomic<std::uint32_t> g_lastWin32Error{0};
    std::atomic<std::int32_t> g_initialRegionState{0};
    std::atomic<std::int32_t> g_phase{-1};
    std::atomic<std::int32_t> g_publishedPhase{-1};
    std::atomic<std::int32_t> g_builtPhase{-1};
    std::atomic<std::uint32_t> g_phaseTransitionCount{0};
    std::atomic<std::uint32_t> g_phaseApplyCount{0};
    std::array<std::atomic<bool>, 4> g_phaseApplied{};
    std::array<std::atomic<std::uint64_t>, 4> g_phaseLastGeneration{};
    std::array<std::atomic<std::uint32_t>, 4> g_phaseRawRuns{};
    std::array<std::atomic<std::uint32_t>, 4> g_phaseMerged{};
    std::array<std::atomic<std::uint32_t>, 4> g_phaseFinal{};
    std::array<std::atomic<std::uint32_t>, 4> g_phaseCovered{};
    std::array<std::atomic<std::uint64_t>, 4> g_phaseHash{};
    std::atomic<std::uint64_t> g_lastBuildUs{0};
    std::atomic<std::uint64_t> g_minBuildUs{UINT64_MAX};
    std::atomic<std::uint64_t> g_maxBuildUs{0};
    std::atomic<std::uint64_t> g_totalBuildUs{0};
    std::atomic<std::uint64_t> g_lastSetUs{0};
    std::atomic<std::uint64_t> g_minSetUs{UINT64_MAX};
    std::atomic<std::uint64_t> g_maxSetUs{0};
    std::atomic<std::uint64_t> g_totalSetUs{0};
    std::atomic<std::uint32_t> g_maxPendingMessages{0};
    std::atomic<std::uint32_t> g_maxPendingRegions{0};
    std::atomic<std::uint32_t> g_hrgnCreated{0};
    std::atomic<std::uint32_t> g_hrgnDeleted{0};
    std::atomic<std::uint32_t> g_hrgnTransferred{0};
    std::atomic<std::uint32_t> g_hrgnValidationDeleted{0};
    std::atomic<std::int32_t> g_hrgnLive{0};
    std::atomic<std::uint32_t> g_minAnimationGdi{UINT32_MAX};
    std::atomic<std::uint32_t> g_lastAnimationGdi{0};
    std::atomic<std::uint64_t> g_lastAppliedHash{0};
    std::atomic<std::uint64_t> g_lastUpdateTick{0};
    std::atomic<std::uint64_t> g_initialStyle{0};
    std::atomic<std::uint64_t> g_diagnosticStyle{0};
    std::atomic<std::uint64_t> g_currentStyle{0};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_lastApplySucceeded{false};
    std::atomic<bool> g_regionChanged{false};
    std::atomic<bool> g_styleChanged{false};
    std::atomic<bool> g_regionRestored{false};
    std::atomic<bool> g_styleRestored{false};
    std::atomic<bool> g_completeRequested{false};
    std::atomic<bool> g_topLeft{false};
    std::atomic<bool> g_topRight{false};
    std::atomic<bool> g_bottomRight{false};
    std::atomic<bool> g_bottomLeft{false};
    std::atomic<bool> g_center{false};
    std::atomic<bool> g_realMascotMode{false};
    std::atomic<bool> g_realMascotAnimatedMode{false};
    std::atomic<bool> g_runtimeMode{false};
    std::atomic<bool> g_stopRequested{false};
    std::atomic<bool> g_shutdownAuditLogged{false};
    std::atomic<std::int32_t> g_externalPhase{-1};
    std::atomic<std::uint64_t> g_externalPhaseGeneration{0};

    // Composition UI thread only.
    HRGN g_savedInitialRegion = nullptr;

    void StoreState(AnimatedWindowRegionState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    std::uint32_t CurrentGdiCount()
    {
        return ::GetGuiResources(::GetCurrentProcess(), GR_GDIOBJECTS);
    }

    HRGN CreateTrackedRegion(int l, int t, int r, int b)
    {
        HRGN value = ::CreateRectRgn(l, t, r, b);
        if (value != nullptr)
        {
            g_hrgnCreated.fetch_add(1);
            g_hrgnLive.fetch_add(1);
        }
        return value;
    }

    void DeleteTrackedRegion(HRGN value, bool validation = false)
    {
        if (value != nullptr && ::DeleteObject(value) != FALSE)
        {
            g_hrgnDeleted.fetch_add(1);
            if (validation) g_hrgnValidationDeleted.fetch_add(1);
            g_hrgnLive.fetch_sub(1);
        }
    }

    void TransferTrackedRegion()
    {
        g_hrgnTransferred.fetch_add(1);
        g_hrgnLive.fetch_sub(1);
    }

    std::uint64_t Qpc()
    {
        LARGE_INTEGER value{};
        ::QueryPerformanceCounter(&value);
        return static_cast<std::uint64_t>(value.QuadPart);
    }

    std::uint64_t Microseconds(std::uint64_t start, std::uint64_t end)
    {
        LARGE_INTEGER frequency{};
        ::QueryPerformanceFrequency(&frequency);
        return frequency.QuadPart > 0
            ? (end - start) * 1000000ull
                / static_cast<std::uint64_t>(frequency.QuadPart)
            : 0;
    }

    template<class T>
    void UpdateMinimum(std::atomic<T>& target, T value)
    {
        auto current = target.load();
        while (value < current
               && !target.compare_exchange_weak(current, value)) {}
    }

    template<class T>
    void UpdateMaximum(std::atomic<T>& target, T value)
    {
        auto current = target.load();
        while (value > current
               && !target.compare_exchange_weak(current, value)) {}
    }

    void ObserveGdiCount()
    {
        const auto count = CurrentGdiCount();
        auto peak = g_peakGdiCount.load(std::memory_order_relaxed);
        while (count > peak
               && !g_peakGdiCount.compare_exchange_weak(peak, count))
        {
        }
    }

    const wchar_t* FailureStageName(
        AnimatedWindowRegionFailureStage stage)
    {
        switch (stage)
        {
            case AnimatedWindowRegionFailureStage::GdiObjectLeakDetected:
                return L"GdiObjectLeakDetected";
            default:
                return L"Other";
        }
    }

    void LogShutdownAuditOnce(
        AnimatedWindowRegionFailureStage candidate,
        const wchar_t* operation,
        bool treatedAsFailure,
        DWORD error)
    {
        if (g_shutdownAuditLogged.exchange(true))
            return;
        const HWND window = g_window.load(std::memory_order_acquire);
        const bool windowAvailable =
            window != nullptr && ::IsWindow(window) != FALSE;
        const bool messageLoopRunning =
            DesktopMascotNative::IsCompositionMessageLoopRunning();
        const auto compositionState =
            DesktopMascotNative::GetCompositionInitializationState();
        const bool compositionStopping =
            compositionState
                >= static_cast<std::int32_t>(
                    DesktopMascotNative::
                        CompositionInitializationState::ShutdownRequested);
        wchar_t message[640]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Region shutdown audit: "
            L"candidate=%ls, operation=%ls, treatedAsFailure=%d, "
            L"shutdownStarted=%d, hwndAvailable=%d, "
            L"messageLoopRunning=%d, compositionStopping=%d, "
            L"generation=%llu, pendingOwnedRegions=%d, "
            L"lastErrorOrHresult=0x%08lX, threadId=%lu, "
            L"gdiInitial=%u, gdiFinal=%u, gdiDelta=%d.\n",
            FailureStageName(candidate),
            operation,
            treatedAsFailure ? 1 : 0,
            g_stopRequested.load() ? 1 : 0,
            windowAvailable ? 1 : 0,
            messageLoopRunning ? 1 : 0,
            compositionStopping ? 1 : 0,
            static_cast<unsigned long long>(
                g_appliedGeneration.load()),
            g_hrgnLive.load(),
            static_cast<unsigned long>(error),
            static_cast<unsigned long>(::GetCurrentThreadId()),
            g_initialGdiCount.load(),
            g_finalGdiCount.load(),
            g_gdiDelta.load());
        ::OutputDebugStringW(message);
    }

    void Fail(
        AnimatedWindowRegionFailureStage stage,
        DWORD error = ERROR_SUCCESS)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage));
        g_lastWin32Error.store(error);
        g_failureCount.fetch_add(1);
        g_pending.store(false);
        g_lastApplySucceeded.store(false);
        g_enabled.store(false);
        StoreState(AnimatedWindowRegionState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Animated window region diagnostics "
            L"failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool GetStyle(HWND window, LONG_PTR& style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        style = ::GetWindowLongPtrW(window, GWL_EXSTYLE);
        error = ::GetLastError();
        return style != 0 || error == ERROR_SUCCESS;
    }

    bool SetStyle(HWND window, LONG_PTR style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        const auto old = ::SetWindowLongPtrW(window, GWL_EXSTYLE, style);
        error = ::GetLastError();
        return !(old == 0 && error != ERROR_SUCCESS)
            && ::SetWindowPos(window, nullptr, 0, 0, 0, 0,
                              kFrameChangedFlags) != FALSE;
    }

    bool CaptureInitialRegion(HWND window, DWORD& error)
    {
        HRGN region = CreateTrackedRegion(0, 0, 0, 0);
        if (region == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        ::SetLastError(ERROR_SUCCESS);
        const int type = ::GetWindowRgn(window, region);
        error = ::GetLastError();
        if (type == ERROR)
        {
            DeleteTrackedRegion(region);
            if (error == ERROR_SUCCESS)
            {
                g_initialRegionState.store(
                    static_cast<int>(InitialWindowRegionState::None));
                return true;
            }
            g_initialRegionState.store(
                static_cast<int>(InitialWindowRegionState::QueryFailed));
            return false;
        }
        if (type == NULLREGION)
        {
            DeleteTrackedRegion(region);
            g_initialRegionState.store(
                static_cast<int>(InitialWindowRegionState::Empty));
            return true;
        }
        g_savedInitialRegion = region;
        g_initialRegionState.store(
            type == SIMPLEREGION
                ? static_cast<int>(InitialWindowRegionState::Simple)
                : static_cast<int>(InitialWindowRegionState::Complex));
        return true;
    }

    HRGN DuplicateRegion(HRGN source)
    {
        HRGN result = CreateTrackedRegion(0, 0, 0, 0);
        if (result == nullptr
            || ::CombineRgn(result, source, nullptr, RGN_COPY) == ERROR)
        {
            if (result != nullptr)
            {
                DeleteTrackedRegion(result);
            }
            return nullptr;
        }
        return result;
    }

    std::uint64_t HashMask(
        const std::array<std::uint8_t, kByteCount>& pixels)
    {
        std::uint64_t hash = 14695981039346656037ull;
        for (const auto value : pixels)
        {
            // Duplicate suppression is intentionally based on the binary
            // hit-test mask, not insignificant anti-aliased alpha changes.
            hash ^= value >= kThreshold ? 1u : 0u;
            hash *= 1099511628211ull;
        }
        return hash;
    }

    HRGN BuildRegion(
        const std::array<std::uint8_t, kByteCount>& pixels,
        std::uint32_t& rawRuns,
        std::uint32_t& mergedRectangles,
        std::uint32_t& finalRectangles,
        std::uint32_t& covered,
        DWORD& error)
    {
        struct Rectangle { int left, top, right, bottom; };
        std::map<std::pair<int, int>, Rectangle> active;
        std::vector<Rectangle> complete;
        for (int y = 0; y < kHeight; ++y)
        {
            std::map<std::pair<int, int>, Rectangle> next;
            int x = 0;
            while (x < kWidth)
            {
                while (x < kWidth
                       && pixels[y * kStride + x] < kThreshold) ++x;
                if (x == kWidth) break;
                const int left = x;
                while (x < kWidth
                       && pixels[y * kStride + x] >= kThreshold) ++x;
                const auto key = std::pair{left, x};
                auto previous = active.find(key);
                if (previous != active.end())
                {
                    auto rectangle = previous->second;
                    rectangle.bottom = y + 1;
                    next.emplace(key, rectangle);
                    active.erase(previous);
                }
                else next.emplace(key, Rectangle{left, y, x, y + 1});
                ++rawRuns;
                covered += static_cast<std::uint32_t>(x - left);
            }
            for (const auto& [key, rectangle] : active)
                complete.push_back(rectangle);
            active = std::move(next);
        }
        for (const auto& [key, rectangle] : active)
            complete.push_back(rectangle);
        mergedRectangles = static_cast<std::uint32_t>(complete.size());

        HRGN combined = CreateTrackedRegion(0, 0, 0, 0);
        if (combined == nullptr)
        {
            error = ::GetLastError();
            return nullptr;
        }
        for (const auto& rectangle : complete)
        {
            HRGN part = CreateTrackedRegion(
                rectangle.left, rectangle.top,
                rectangle.right, rectangle.bottom);
            if (part == nullptr)
            {
                error = ::GetLastError();
                DeleteTrackedRegion(combined);
                return nullptr;
            }
            const int result = ::CombineRgn(combined, combined, part, RGN_OR);
            DeleteTrackedRegion(part);
            if (result == ERROR)
            {
                error = ::GetLastError();
                DeleteTrackedRegion(combined);
                return nullptr;
            }
        }
        const DWORD bytes = ::GetRegionData(combined, 0, nullptr);
        if (bytes == 0) { error = ::GetLastError(); DeleteTrackedRegion(combined); return nullptr; }
        std::vector<std::byte> storage(bytes);
        auto* data = reinterpret_cast<RGNDATA*>(storage.data());
        if (::GetRegionData(combined, bytes, data) == 0)
        { error = ::GetLastError(); DeleteTrackedRegion(combined); return nullptr; }
        finalRectangles = data->rdh.nCount;
        return combined;
    }

    int ValidateAndStoreSamples(HRGN region)
    {
        const bool common = ::PtInRegion(region, 124, 152) != FALSE
            && ::PtInRegion(region, 60, 32) != FALSE
            && ::PtInRegion(region, 248, 8) == FALSE;
        const bool neutralArm = ::PtInRegion(region, 60, 160) != FALSE;
        const bool raisedArm = ::PtInRegion(region, 52, 104) != FALSE;
        const bool lowTail = ::PtInRegion(region, 228, 172) != FALSE;
        const bool raisedTail = ::PtInRegion(region, 228, 116) != FALSE;
        const bool normalUpper = ::PtInRegion(region, 124, 120) != FALSE;
        const bool outerLegs = ::PtInRegion(region, 68, 224) != FALSE
            && ::PtInRegion(region, 176, 224) != FALSE;
        const bool tl = common, tr = raisedArm, br = lowTail, bl = neutralArm;
        g_topLeft.store(tl);
        g_topRight.store(tr);
        g_bottomRight.store(br);
        g_bottomLeft.store(bl);
        g_center.store(common);
        if (!common) return -1;
        if (neutralArm && !raisedArm && lowTail && !raisedTail
            && normalUpper) return 0;
        if (!neutralArm && raisedArm && lowTail && !raisedTail) return 1;
        if (neutralArm && !raisedArm && !lowTail && raisedTail) return 2;
        if (!normalUpper && outerLegs) return 3;
        return -1;
    }

    void ReleaseSavedRegion()
    {
        if (g_savedInitialRegion != nullptr)
        {
            DeleteTrackedRegion(g_savedInitialRegion);
            g_savedInitialRegion = nullptr;
        }
    }

    bool RestoreRegion(HWND window, DWORD& error)
    {
        const auto state = static_cast<InitialWindowRegionState>(
            g_initialRegionState.load());
        if (state == InitialWindowRegionState::None
            || state == InitialWindowRegionState::Empty)
        {
            return ::SetWindowRgn(window, nullptr, TRUE) != 0;
        }
        HRGN copy = DuplicateRegion(g_savedInitialRegion);
        if (copy == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        if (::SetWindowRgn(window, copy, TRUE) == 0)
        {
            error = ::GetLastError();
            DeleteTrackedRegion(copy);
            return false;
        }
        TransferTrackedRegion();
        return true;
    }
}

namespace DesktopMascotNative
{
    void ResetAnimatedWindowRegionDiagnostics()
    {
        ReleaseSavedRegion();
#define RESET_I32(value) value.store(0)
#define RESET_U32(value) value.store(0)
#define RESET_U64(value) value.store(0)
#define RESET_BOOL(value) value.store(false)
        g_window.store(nullptr); RESET_I32(g_state); RESET_I32(g_failureStage);
        RESET_I32(g_threshold); RESET_I32(g_intervalMilliseconds);
        RESET_U64(g_publishedGeneration); RESET_U64(g_buildGeneration);
        RESET_U64(g_requestedGeneration); RESET_U64(g_appliedGeneration);
        RESET_U32(g_buildCount); RESET_U32(g_requestCount);
        RESET_U32(g_evaluationCount); RESET_U32(g_executionCount);
        RESET_U32(g_successCount); RESET_U32(g_failureCount);
        RESET_U32(g_skippedCount); RESET_U32(g_duplicateCount);
        RESET_U32(g_rectangleCount); RESET_U32(g_mergedRectangleCount);
        RESET_U32(g_regionDataRectangleCount); RESET_U32(g_coveredPixelCount);
        RESET_U32(g_excludedPixelCount); RESET_U32(g_initialGdiCount);
        RESET_U32(g_peakGdiCount); RESET_U32(g_finalGdiCount);
        RESET_I32(g_gdiDelta); RESET_U32(g_lastWin32Error);
        RESET_I32(g_initialRegionState); g_phase.store(-1);
        g_publishedPhase.store(-1); g_builtPhase.store(-1);
        RESET_U32(g_phaseTransitionCount); RESET_U32(g_phaseApplyCount);
        for (int phase = 0; phase < 4; ++phase)
        {
            g_phaseApplied[phase].store(false);
            g_phaseLastGeneration[phase].store(0);
            g_phaseRawRuns[phase].store(0);
            g_phaseMerged[phase].store(0);
            g_phaseFinal[phase].store(0);
            g_phaseCovered[phase].store(0);
            g_phaseHash[phase].store(0);
        }
        RESET_U64(g_lastBuildUs); g_minBuildUs.store(UINT64_MAX);
        RESET_U64(g_maxBuildUs); RESET_U64(g_totalBuildUs);
        RESET_U64(g_lastSetUs); g_minSetUs.store(UINT64_MAX);
        RESET_U64(g_maxSetUs); RESET_U64(g_totalSetUs);
        RESET_U32(g_maxPendingMessages); RESET_U32(g_maxPendingRegions);
        RESET_U32(g_hrgnCreated); RESET_U32(g_hrgnDeleted);
        RESET_U32(g_hrgnTransferred); RESET_U32(g_hrgnValidationDeleted);
        g_hrgnLive.store(0); g_minAnimationGdi.store(UINT32_MAX);
        RESET_U32(g_lastAnimationGdi);
        RESET_U64(g_lastAppliedHash); RESET_U64(g_lastUpdateTick);
        RESET_U64(g_initialStyle); RESET_U64(g_diagnosticStyle);
        RESET_U64(g_currentStyle); RESET_BOOL(g_enabled);
        RESET_BOOL(g_pending); RESET_BOOL(g_lastApplySucceeded);
        RESET_BOOL(g_regionChanged); RESET_BOOL(g_styleChanged);
        RESET_BOOL(g_regionRestored); RESET_BOOL(g_styleRestored);
        RESET_BOOL(g_completeRequested); RESET_BOOL(g_topLeft);
        RESET_BOOL(g_topRight); RESET_BOOL(g_bottomRight);
        RESET_BOOL(g_bottomLeft); RESET_BOOL(g_center);
        RESET_BOOL(g_realMascotMode);
        RESET_BOOL(g_realMascotAnimatedMode);
        RESET_BOOL(g_runtimeMode);
        RESET_BOOL(g_stopRequested);
        RESET_BOOL(g_shutdownAuditLogged);
        g_externalPhase.store(-1);
        RESET_U64(g_externalPhaseGeneration);
#undef RESET_I32
#undef RESET_U32
#undef RESET_U64
#undef RESET_BOOL
    }

    void SetAnimatedWindowRegionUiWindow(void* window)
    {
        g_window.store(static_cast<HWND>(window));
    }

    void NotifyAnimatedWindowRegionShutdownRequested()
    {
        g_stopRequested.store(true, std::memory_order_release);
        g_enabled.store(false, std::memory_order_release);
        const auto state = g_state.load(std::memory_order_acquire);
        if (state != static_cast<std::int32_t>(
                AnimatedWindowRegionState::Completed)
            && state != static_cast<std::int32_t>(
                AnimatedWindowRegionState::Stopped)
            && state != static_cast<std::int32_t>(
                AnimatedWindowRegionState::Failed))
        {
            StoreState(AnimatedWindowRegionState::StopRequested);
        }
    }

    std::int32_t StartAnimatedWindowRegionDiagnostics(
        std::int32_t threshold,
        std::int32_t intervalMilliseconds)
    {
        if (threshold != kThreshold)
        {
            Fail(AnimatedWindowRegionFailureStage::InvalidThreshold);
            return 0;
        }
        if (intervalMilliseconds < 250)
        {
            Fail(AnimatedWindowRegionFailureStage::InvalidUpdateInterval);
            return 0;
        }
        if (GetCompositionInitializationState()
            != static_cast<int>(
                CompositionInitializationState::MessageLoopRunning))
        {
            Fail(AnimatedWindowRegionFailureStage::CompositionNotReady);
            return 0;
        }
        if (!IsCompositionAlphaMaskAvailable())
        {
            StoreState(AnimatedWindowRegionState::WaitingForMask);
            return 0;
        }
        if (g_window.load() == nullptr)
        {
            Fail(AnimatedWindowRegionFailureStage::WindowUnavailable);
            return 0;
        }
        g_threshold.store(threshold);
        g_intervalMilliseconds.store(intervalMilliseconds);
        g_enabled.store(true);
        g_pending.store(true);
        g_maxPendingMessages.store(1);
        StoreState(AnimatedWindowRegionState::WaitingForFirstRegion);
        if (::PostMessageW(
                g_window.load(), kAnimatedWindowRegionApplyMessage, 0, 0)
            == FALSE)
        {
            Fail(
                AnimatedWindowRegionFailureStage::ApplyMessagePostFailed,
                ::GetLastError());
            return 0;
        }
        g_requestCount.fetch_add(1);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Animated window region diagnostics "
            L"started.\n");
        return 1;
    }

    std::int32_t StartRealMascotStaticAlphaDiagnostics(
        std::int32_t threshold)
    {
        g_realMascotMode.store(true);
        return StartAnimatedWindowRegionDiagnostics(threshold, 250);
    }

    std::int32_t StartRealMascotAnimatedAlphaDiagnostics(
        std::int32_t threshold,
        std::int32_t intervalMilliseconds)
    {
        g_realMascotMode.store(true);
        g_realMascotAnimatedMode.store(true);
        return StartAnimatedWindowRegionDiagnostics(
            threshold, intervalMilliseconds);
    }

    std::int32_t StartRuntimeAlphaRegion(
        std::int32_t threshold,
        std::int32_t intervalMilliseconds)
    {
        g_realMascotMode.store(true);
        g_runtimeMode.store(true);
        return StartAnimatedWindowRegionDiagnostics(
            threshold, intervalMilliseconds);
    }

    std::int32_t SetRealMascotAnimatedPhaseForGeneration(
        std::int32_t phase,
        std::uint64_t generation)
    {
        // The first phase is registered immediately before the first mask is
        // published, which necessarily precedes starting the region consumer.
        if (phase < 0 || phase >= 4 || generation == 0)
            return 0;
        g_externalPhase.store(phase, std::memory_order_relaxed);
        g_externalPhaseGeneration.store(
            generation, std::memory_order_release);
        return 1;
    }

    std::int32_t PollAnimatedWindowRegionDiagnostics()
    {
        const auto state = g_state.load();
        if (g_stopRequested.load(std::memory_order_acquire)
            || state != static_cast<int>(AnimatedWindowRegionState::Running)
            || g_pending.load())
        {
            return state;
        }
        const auto published = GetCompositionAlphaMaskPublishedGeneration();
        g_publishedGeneration.store(published);
        if (published <= g_appliedGeneration.load())
        {
            return state;
        }
        const auto now = ::GetTickCount64();
        if (now - g_lastUpdateTick.load()
            < static_cast<std::uint64_t>(g_intervalMilliseconds.load()))
        {
            return state;
        }
        g_requestedGeneration.store(published);
        g_pending.store(true);
        UpdateMaximum(g_maxPendingMessages, 1u);
        if (::PostMessageW(
                g_window.load(), kAnimatedWindowRegionApplyMessage, 0, 0)
            == FALSE)
        {
            Fail(
                AnimatedWindowRegionFailureStage::ApplyMessagePostFailed,
                ::GetLastError());
        }
        else
        {
            g_requestCount.fetch_add(1);
        }
        return g_state.load();
    }

    void HandleAnimatedWindowRegionApplyMessage()
    {
        if (g_stopRequested.load(std::memory_order_acquire))
        {
            g_pending.store(false, std::memory_order_release);
            g_lastApplySucceeded.store(false);
            return;
        }
        g_executionCount.fetch_add(1);
        const HWND window = g_window.load();
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(AnimatedWindowRegionFailureStage::WindowUnavailable);
            return;
        }
        DWORD error = ERROR_SUCCESS;
        if (!g_styleChanged.load())
        {
            StoreState(AnimatedWindowRegionState::ApplyingInitialStyle);
            LONG_PTR initial = 0;
            if (!GetStyle(window, initial, error)
                || (initial & WS_EX_TRANSPARENT) != 0)
            {
                Fail(
                    AnimatedWindowRegionFailureStage::
                        InitialStyleRestoreFailed,
                    error);
                return;
            }
            g_initialStyle.store(static_cast<std::uint64_t>(initial));
            g_currentStyle.store(static_cast<std::uint64_t>(initial));
            if (!CaptureInitialRegion(window, error))
            {
                Fail(
                    AnimatedWindowRegionFailureStage::
                        InitialRegionRestoreFailed,
                    error);
                return;
            }
            const auto diagnostic = initial | WS_EX_LAYERED;
            if (!SetStyle(window, diagnostic, error))
            {
                Fail(
                    AnimatedWindowRegionFailureStage::
                        InitialStyleRestoreFailed,
                    error);
                return;
            }
            g_diagnosticStyle.store(
                static_cast<std::uint64_t>(diagnostic));
            g_currentStyle.store(
                static_cast<std::uint64_t>(diagnostic));
            g_styleChanged.store(true);
            const auto initialGdi = CurrentGdiCount();
            g_initialGdiCount.store(initialGdi);
            g_peakGdiCount.store(initialGdi);
        }

        std::array<std::uint8_t, kByteCount> pixels{};
        std::int32_t width = 0, height = 0, stride = 0, threshold = 0;
        std::uint64_t generation = 0;
        if (!TryCopyCompositionAlphaMaskSnapshot(
                pixels.data(), pixels.size(), width, height, stride,
                threshold, generation)
            || width != kWidth || height != kHeight || stride != kStride
            || threshold != kThreshold)
        {
            Fail(AnimatedWindowRegionFailureStage::SnapshotCopyFailed);
            return;
        }
        g_publishedGeneration.store(
            GetCompositionAlphaMaskPublishedGeneration());
        g_buildGeneration.store(generation);
        g_evaluationCount.fetch_add(1);
        if (g_requestedGeneration.load() == 0)
        {
            g_requestedGeneration.store(generation);
        }
        const auto previous = g_appliedGeneration.load();
        if (generation > previous + 1)
        {
            g_skippedCount.fetch_add(
                static_cast<std::uint32_t>(generation - previous - 1));
        }

        const auto hash = HashMask(pixels);
        if (g_successCount.load() != 0
            && hash == g_lastAppliedHash.load())
        {
            g_duplicateCount.fetch_add(1);
            g_appliedGeneration.store(generation);
            g_pending.store(false);
            g_lastApplySucceeded.store(true);
            g_lastUpdateTick.store(::GetTickCount64());
            if (g_stopRequested.load(std::memory_order_acquire))
            {
                g_enabled.store(false, std::memory_order_release);
                StoreState(AnimatedWindowRegionState::StopRequested);
            }
            return;
        }

        std::uint32_t rectangles = 0;
        std::uint32_t merged = 0;
        std::uint32_t finalRectangles = 0;
        std::uint32_t covered = 0;
        const auto buildStart = Qpc();
        HRGN region = BuildRegion(
            pixels, rectangles, merged, finalRectangles, covered, error);
        const auto buildUs = Microseconds(buildStart, Qpc());
        g_lastBuildUs.store(buildUs);
        UpdateMinimum(g_minBuildUs, buildUs);
        UpdateMaximum(g_maxBuildUs, buildUs);
        g_totalBuildUs.fetch_add(buildUs);
        g_buildCount.fetch_add(1);
        ObserveGdiCount();
        if (region == nullptr)
        {
            Fail(
                error == ERROR_SUCCESS
                    ? AnimatedWindowRegionFailureStage::RegionCombineFailed
                    : AnimatedWindowRegionFailureStage::RegionCreationFailed,
                error);
            return;
        }
        if (covered == 0
            || (!g_realMascotMode.load() && covered >= kByteCount)
            || rectangles <= 1 || merged <= 1 || merged > rectangles
            || finalRectangles <= 1
            || (!g_runtimeMode.load() && buildUs > 10000))
        {
            DeleteTrackedRegion(region);
            Fail(
                AnimatedWindowRegionFailureStage::
                    RegionPixelCountValidationFailed);
            return;
        }
        const int phase = g_realMascotAnimatedMode.load()
            ? (g_externalPhaseGeneration.load(std::memory_order_acquire)
                    == generation
                ? g_externalPhase.load(std::memory_order_relaxed)
                : -1)
            : (g_realMascotMode.load()
                ? 0
                : ValidateAndStoreSamples(region));
        if (phase < 0)
        {
            DeleteTrackedRegion(region);
            Fail(
                AnimatedWindowRegionFailureStage::
                    RepresentativePointValidationFailed);
            return;
        }
        const auto setStart = Qpc();
        const auto setResult = ::SetWindowRgn(window, region, TRUE);
        const auto setUs = Microseconds(setStart, Qpc());
        g_lastSetUs.store(setUs);
        UpdateMinimum(g_minSetUs, setUs);
        UpdateMaximum(g_maxSetUs, setUs);
        g_totalSetUs.fetch_add(setUs);
        if (setResult == 0)
        {
            error = ::GetLastError();
            DeleteTrackedRegion(region);
            Fail(
                AnimatedWindowRegionFailureStage::SetWindowRgnFailed,
                error);
            return;
        }
        // Windows owns region after successful SetWindowRgn.
        TransferTrackedRegion();
        if (!g_runtimeMode.load() && setUs > 5000)
        {
            Fail(AnimatedWindowRegionFailureStage::SetWindowRgnFailed);
            return;
        }
        HRGN appliedCopy = CreateTrackedRegion(0, 0, 0, 0);
        if (appliedCopy == nullptr
            || ::GetWindowRgn(window, appliedCopy) == ERROR
            || (!g_realMascotMode.load()
                && ValidateAndStoreSamples(appliedCopy) != phase))
        {
            if (appliedCopy != nullptr)
                DeleteTrackedRegion(appliedCopy, true);
            Fail(
                AnimatedWindowRegionFailureStage::
                    RepresentativePointValidationFailed,
                ::GetLastError());
            return;
        }
        DeleteTrackedRegion(appliedCopy, true);
        g_regionChanged.store(true);
        g_rectangleCount.store(rectangles);
        g_mergedRectangleCount.store(merged);
        g_regionDataRectangleCount.store(finalRectangles);
        g_coveredPixelCount.store(covered);
        g_excludedPixelCount.store(
            static_cast<std::uint32_t>(kByteCount) - covered);
        g_appliedGeneration.store(generation);
        g_lastAppliedHash.store(hash);
        g_successCount.fetch_add(1);
        g_pending.store(false);
        g_lastApplySucceeded.store(true);
        g_lastWin32Error.store(0);
        g_lastUpdateTick.store(::GetTickCount64());
        const bool stopping =
            g_stopRequested.load(std::memory_order_acquire);
        g_enabled.store(!stopping);
        g_builtPhase.store(phase);
        g_publishedPhase.store(phase);
        g_phaseApplied[phase].store(true);
        g_phaseLastGeneration[phase].store(generation);
        g_phaseRawRuns[phase].store(rectangles);
        g_phaseMerged[phase].store(merged);
        g_phaseFinal[phase].store(finalRectangles);
        g_phaseCovered[phase].store(covered);
        g_phaseHash[phase].store(hash);
        g_phaseApplyCount.fetch_add(1);
        const auto animationGdi = CurrentGdiCount();
        UpdateMinimum(g_minAnimationGdi, animationGdi);
        g_lastAnimationGdi.store(animationGdi);
        const int previousPhase = g_phase.exchange(phase);
        if (previousPhase < 0)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Animated window region first "
                L"region applied.\n");
        }
        if (previousPhase != phase)
        {
            if (previousPhase >= 0) g_phaseTransitionCount.fetch_add(1);
            wchar_t message[128]{};
            swprintf_s(
                message,
                L"[DesktopMascotNative] Animated window region phase "
                L"changed: %d.\n",
                phase);
            ::OutputDebugStringW(message);
        }
        StoreState(
            stopping
                ? AnimatedWindowRegionState::StopRequested
                : AnimatedWindowRegionState::Running);
        ObserveGdiCount();
    }

    std::int32_t CompleteAnimatedWindowRegionDiagnostics()
    {
        if (g_state.load()
            != static_cast<int>(AnimatedWindowRegionState::Running))
        {
            return 0;
        }
        g_completeRequested.store(true);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Animated window region diagnostics "
            L"completed.\n");
        return 1;
    }

    std::int32_t StopAnimatedWindowRegionDiagnostics()
    {
        if (g_runtimeMode.load())
            g_completeRequested.store(true);
        NotifyAnimatedWindowRegionShutdownRequested();
        if (g_pending.exchange(true))
        {
            return 0;
        }
        if (!g_regionChanged.load() && !g_styleChanged.load())
        {
            g_pending.store(false);
            g_regionRestored.store(true);
            g_styleRestored.store(true);
            StoreState(
                g_completeRequested.load()
                    ? AnimatedWindowRegionState::Completed
                    : AnimatedWindowRegionState::Stopped);
            return 1;
        }
        if (::PostMessageW(
                g_window.load(), kAnimatedWindowRegionRestoreMessage, 0, 0)
            == FALSE)
        {
            Fail(
                AnimatedWindowRegionFailureStage::ApplyMessagePostFailed,
                ::GetLastError());
            return 0;
        }
        return 1;
    }

    void HandleAnimatedWindowRegionRestoreMessage()
    {
        const HWND window = g_window.load();
        DWORD error = ERROR_SUCCESS;
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(AnimatedWindowRegionFailureStage::WindowUnavailable);
            return;
        }
        StoreState(AnimatedWindowRegionState::RestoringInitialRegion);
        if (g_regionChanged.load() && !RestoreRegion(window, error))
        {
            Fail(
                AnimatedWindowRegionFailureStage::
                    InitialRegionRestoreFailed,
                error);
            return;
        }
        g_regionChanged.store(false);
        g_regionRestored.store(true);
        ReleaseSavedRegion();

        StoreState(AnimatedWindowRegionState::RestoringInitialStyle);
        const auto initial =
            static_cast<LONG_PTR>(g_initialStyle.load());
        if (g_styleChanged.load() && !SetStyle(window, initial, error))
        {
            Fail(
                AnimatedWindowRegionFailureStage::
                    InitialStyleRestoreFailed,
                error);
            return;
        }
        g_currentStyle.store(static_cast<std::uint64_t>(initial));
        g_styleChanged.store(false);
        g_styleRestored.store(true);
        g_pending.store(false);
        g_lastApplySucceeded.store(true);
        const auto finalGdi = CurrentGdiCount();
        g_finalGdiCount.store(finalGdi);
        const auto delta = static_cast<std::int32_t>(finalGdi)
            - static_cast<std::int32_t>(g_initialGdiCount.load());
        g_gdiDelta.store(delta);
        if (delta > 2)
        {
            const bool trackedRuntimeOwnershipIsClean =
                g_runtimeMode.load()
                && g_stopRequested.load()
                && g_hrgnLive.load() == 0
                && g_regionRestored.load()
                && g_styleRestored.load();
            LogShutdownAuditOnce(
                AnimatedWindowRegionFailureStage::GdiObjectLeakDetected,
                L"validate restored region GDI ownership",
                !trackedRuntimeOwnershipIsClean,
                ERROR_SUCCESS);
            if (!trackedRuntimeOwnershipIsClean)
            {
                Fail(
                    AnimatedWindowRegionFailureStage::
                        GdiObjectLeakDetected);
                return;
            }
        }
        StoreState(
            g_completeRequested.load()
                ? AnimatedWindowRegionState::Completed
                : AnimatedWindowRegionState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Animated window region diagnostics "
            L"stopped.\n");
    }

    void StopAnimatedWindowRegionDiagnosticsOnUiThread()
    {
        NotifyAnimatedWindowRegionShutdownRequested();
        if (g_regionChanged.load() || g_styleChanged.load())
        {
            HandleAnimatedWindowRegionRestoreMessage();
        }
        ReleaseSavedRegion();
        g_enabled.store(false);
        g_pending.store(false);
        g_window.store(nullptr);
    }

    bool ShouldAnimatedWindowRegionReturnClient()
    {
        return g_enabled.load();
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetAnimatedWindowRegionState, g_state)
    GET_I32(GetAnimatedWindowRegionFailureStage, g_failureStage)
    GET_BOOL(IsAnimatedWindowRegionEnabled, g_enabled)
    GET_I32(GetAnimatedWindowRegionThreshold, g_threshold)
    GET_I32(
        GetAnimatedWindowRegionMinimumUpdateIntervalMilliseconds,
        g_intervalMilliseconds)
    GET_U64(GetAnimatedWindowRegionPublishedGeneration, g_publishedGeneration)
    GET_U64(GetAnimatedWindowRegionBuildGeneration, g_buildGeneration)
    GET_U64(GetAnimatedWindowRegionRequestedGeneration, g_requestedGeneration)
    GET_U64(GetAnimatedWindowRegionAppliedGeneration, g_appliedGeneration)
    GET_U32(GetAnimatedWindowRegionBuildCount, g_buildCount)
    GET_U32(
        GetAnimatedWindowRegionGenerationEvaluationCount,
        g_evaluationCount)
    GET_U32(
        GetAnimatedWindowRegionApplyMessagePostCount,
        g_requestCount)
    GET_U32(
        GetAnimatedWindowRegionApplyExecutionCount,
        g_executionCount)
    GET_U32(
        GetAnimatedWindowRegionDuplicateMaskSkipCount,
        g_duplicateCount)
    GET_U32(
        GetAnimatedWindowRegionSupersededGenerationCount,
        g_skippedCount)
    GET_U32(GetAnimatedWindowRegionApplyRequestCount, g_requestCount)
    GET_U32(GetAnimatedWindowRegionApplySuccessCount, g_successCount)
    GET_U32(GetAnimatedWindowRegionApplyFailureCount, g_failureCount)
    GET_U32(GetAnimatedWindowRegionSkippedGenerationCount, g_skippedCount)
    GET_U32(GetAnimatedWindowRegionDuplicateSkipCount, g_duplicateCount)
    GET_U32(GetAnimatedWindowRegionCurrentRectangleCount, g_rectangleCount)
    GET_U32(GetAnimatedWindowRegionCurrentCoveredPixelCount, g_coveredPixelCount)
    GET_U32(GetAnimatedWindowRegionCurrentExcludedPixelCount, g_excludedPixelCount)
    GET_U32(GetAnimatedWindowRegionInitialGdiObjectCount, g_initialGdiCount)
    GET_U32(GetAnimatedWindowRegionPeakGdiObjectCount, g_peakGdiCount)
    GET_U32(GetAnimatedWindowRegionFinalGdiObjectCount, g_finalGdiCount)
    GET_I32(GetAnimatedWindowRegionGdiObjectDelta, g_gdiDelta)
    GET_BOOL(IsAnimatedWindowRegionApplyRequestPending, g_pending)
    GET_BOOL(DidAnimatedWindowRegionLastApplySucceed, g_lastApplySucceeded)
    GET_U32(GetAnimatedWindowRegionLastWin32Error, g_lastWin32Error)
    GET_BOOL(DidAnimatedWindowRegionInitialRegionRestoreSucceed, g_regionRestored)
    GET_BOOL(DidAnimatedWindowRegionInitialStyleRestoreSucceed, g_styleRestored)
    GET_I32(GetAnimatedWindowRegionInitialRegionState, g_initialRegionState)
    GET_I32(GetAnimatedWindowRegionCurrentPhase, g_phase)
    GET_BOOL(IsAnimatedWindowRegionTopLeftInside, g_topLeft)
    GET_BOOL(IsAnimatedWindowRegionTopRightInside, g_topRight)
    GET_BOOL(IsAnimatedWindowRegionBottomRightInside, g_bottomRight)
    GET_BOOL(IsAnimatedWindowRegionBottomLeftInside, g_bottomLeft)
    GET_BOOL(IsAnimatedWindowRegionCenterInside, g_center)
    GET_U64(GetAnimatedWindowRegionInitialExtendedStyle, g_initialStyle)
    GET_U64(GetAnimatedWindowRegionDiagnosticExtendedStyle, g_diagnosticStyle)
    GET_U64(GetAnimatedWindowRegionCurrentExtendedStyle, g_currentStyle)
    GET_I32(GetAnimatedWindowRegionPublishedPhase, g_publishedPhase)
    GET_I32(GetAnimatedWindowRegionBuiltPhase, g_builtPhase)
    GET_U32(GetAnimatedWindowRegionPhaseTransitionCount, g_phaseTransitionCount)
    GET_U32(GetAnimatedWindowRegionSuccessfulPhaseApplyCount, g_phaseApplyCount)
    GET_U32(GetAnimatedWindowRegionMergedRectangleCount, g_mergedRectangleCount)
    GET_U32(GetAnimatedWindowRegionRegionDataRectangleCount, g_regionDataRectangleCount)
    GET_U64(GetAnimatedWindowRegionLastBuildMicroseconds, g_lastBuildUs)
    GET_U64(GetAnimatedWindowRegionMaximumBuildMicroseconds, g_maxBuildUs)
    GET_U64(GetAnimatedWindowRegionLastSetWindowRgnMicroseconds, g_lastSetUs)
    GET_U64(GetAnimatedWindowRegionMaximumSetWindowRgnMicroseconds, g_maxSetUs)
    GET_U32(GetAnimatedWindowRegionMaximumPendingApplyMessageCount, g_maxPendingMessages)
    GET_U32(GetAnimatedWindowRegionMaximumPendingRegionCount, g_maxPendingRegions)
    GET_U32(GetAnimatedWindowRegionHrgnCreatedCount, g_hrgnCreated)
    GET_U32(GetAnimatedWindowRegionHrgnCallerDeletedCount, g_hrgnDeleted)
    GET_U32(GetAnimatedWindowRegionHrgnOwnershipTransferredCount, g_hrgnTransferred)
    GET_U32(GetAnimatedWindowRegionHrgnValidationCopyDeletedCount, g_hrgnValidationDeleted)
    GET_I32(GetAnimatedWindowRegionHrgnLiveOwnedCount, g_hrgnLive)
    GET_U32(GetAnimatedWindowRegionLastAnimationGdiObjectCount, g_lastAnimationGdi)
    bool WasAnimatedWindowRegionPhaseApplied(std::int32_t phase)
    { return phase >= 0 && phase < 4 && g_phaseApplied[phase].load(); }
    std::uint64_t GetAnimatedWindowRegionPhaseLastAppliedGeneration(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseLastGeneration[phase].load() : 0; }
    std::uint32_t GetAnimatedWindowRegionPhaseRawRuns(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseRawRuns[phase].load() : 0; }
    std::uint32_t GetAnimatedWindowRegionPhaseMerged(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseMerged[phase].load() : 0; }
    std::uint32_t GetAnimatedWindowRegionPhaseFinal(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseFinal[phase].load() : 0; }
    std::uint32_t GetAnimatedWindowRegionPhaseCovered(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseCovered[phase].load() : 0; }
    std::uint64_t GetAnimatedWindowRegionPhaseHash(std::int32_t phase)
    { return phase >= 0 && phase < 4 ? g_phaseHash[phase].load() : 0; }
    std::uint64_t GetAnimatedWindowRegionMinimumBuildMicroseconds()
    { const auto v=g_minBuildUs.load(); return v==UINT64_MAX?0:v; }
    std::uint64_t GetAnimatedWindowRegionAverageBuildMicroseconds()
    { const auto n=g_buildCount.load(); return n?g_totalBuildUs.load()/n:0; }
    std::uint64_t GetAnimatedWindowRegionMinimumSetWindowRgnMicroseconds()
    { const auto v=g_minSetUs.load(); return v==UINT64_MAX?0:v; }
    std::uint64_t GetAnimatedWindowRegionAverageSetWindowRgnMicroseconds()
    { const auto n=g_successCount.load(); return n?g_totalSetUs.load()/n:0; }
    std::uint32_t GetAnimatedWindowRegionMinimumAnimationGdiObjectCount()
    { const auto v=g_minAnimationGdi.load(); return v==UINT32_MAX?0:v; }
    bool DidAnimatedWindowRegionCounterInvariantsSucceed()
    {
        return g_evaluationCount.load()
                == g_buildCount.load() + g_duplicateCount.load()
            && g_executionCount.load()
                == g_successCount.load() + g_duplicateCount.load()
                    + g_failureCount.load()
            && !g_pending.load();
    }
    bool DidAnimatedWindowRegionRepresentativeValidationSucceed()
    {
        return g_failureStage.load() == 0 && g_successCount.load() != 0;
    }
    bool DidAnimatedWindowRegionAllPhasesApply()
    {
        return std::all_of(
            g_phaseApplied.begin(), g_phaseApplied.end(),
            [](const auto& applied) { return applied.load(); });
    }
    bool DidAnimatedWindowRegionPhaseComplexityDiffer()
    {
        for (std::size_t phase = 1; phase < g_phaseRawRuns.size(); ++phase)
        {
            if (g_phaseRawRuns[phase].load() != g_phaseRawRuns[0].load()
                || g_phaseMerged[phase].load() != g_phaseMerged[0].load()
                || g_phaseCovered[phase].load() != g_phaseCovered[0].load())
                return true;
        }
        return false;
    }
    bool DidAnimatedWindowRegionHrgnOwnershipInvariantsSucceed()
    {
        return g_hrgnCreated.load()
            == g_hrgnDeleted.load() + g_hrgnTransferred.load()
                + static_cast<std::uint32_t>(
                    std::max(g_hrgnLive.load(), 0));
    }
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
