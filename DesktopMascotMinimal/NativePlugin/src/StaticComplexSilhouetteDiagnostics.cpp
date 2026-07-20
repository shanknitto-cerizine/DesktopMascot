#include "DesktopMascotNative/StaticComplexSilhouetteDiagnostics.h"

#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cwchar>
#include <limits>
#include <map>
#include <utility>
#include <vector>

namespace
{
    using DesktopMascotNative::StaticComplexSilhouetteFailureStage;
    using DesktopMascotNative::StaticComplexSilhouetteState;

    constexpr int kWidth = 256;
    constexpr int kHeight = 256;
    constexpr int kStride = 256;
    constexpr std::size_t kByteCount = kWidth * kHeight;
    constexpr UINT kFrameChangedFlags =
        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE
        | SWP_FRAMECHANGED;

    struct Rectangle
    {
        int left;
        int top;
        int right;
        int bottom;
    };

    std::atomic<HWND> g_window{nullptr};
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_threshold{0};
    std::atomic<std::uint64_t> g_publishedGeneration{0};
    std::atomic<std::uint64_t> g_appliedGeneration{0};
    std::atomic<std::uint32_t> g_evaluationCount{0};
    std::atomic<std::uint32_t> g_buildCount{0};
    std::atomic<std::uint32_t> g_postCount{0};
    std::atomic<std::uint32_t> g_executionCount{0};
    std::atomic<std::uint32_t> g_successCount{0};
    std::atomic<std::uint32_t> g_failureCount{0};
    std::atomic<std::uint32_t> g_duplicateCount{0};
    std::atomic<std::uint32_t> g_supersededCount{0};
    std::atomic<std::uint32_t> g_rawRunCount{0};
    std::atomic<std::uint32_t> g_mergedRectangleCount{0};
    std::atomic<std::uint32_t> g_regionDataRectangleCount{0};
    std::atomic<std::int32_t> g_regionType{0};
    std::atomic<std::uint32_t> g_coveredCount{0};
    std::atomic<std::uint32_t> g_excludedCount{0};
    std::atomic<std::uint32_t> g_regionDataBytes{0};
    std::atomic<std::uint64_t> g_lastBuildUs{0};
    std::atomic<std::uint64_t> g_minBuildUs{
        std::numeric_limits<std::uint64_t>::max()};
    std::atomic<std::uint64_t> g_maxBuildUs{0};
    std::atomic<std::uint64_t> g_totalBuildUs{0};
    std::atomic<std::uint64_t> g_lastSetWindowRgnUs{0};
    std::atomic<std::uint64_t> g_maxSetWindowRgnUs{0};
    std::atomic<std::uint32_t> g_initialGdi{0};
    std::atomic<std::uint32_t> g_preShutdownGdi{0};
    std::atomic<std::uint32_t> g_postShutdownGdi{0};
    std::atomic<std::uint32_t> g_afterBuildGdi{0};
    std::atomic<std::uint32_t> g_afterApplyGdi{0};
    std::atomic<std::uint32_t> g_afterRestoreGdi{0};
    std::atomic<std::uint32_t> g_afterWindowDestroyGdi{0};
    std::atomic<std::uint32_t> g_afterUiJoinGdi{0};
    std::atomic<std::uint32_t> g_afterCleanupGdi{0};
    std::atomic<std::uint32_t> g_hrgnCreated{0};
    std::atomic<std::uint32_t> g_hrgnDeleted{0};
    std::atomic<std::uint32_t> g_hrgnTransferred{0};
    std::atomic<std::uint32_t> g_hrgnRestoreCreated{0};
    std::atomic<std::int32_t> g_hrgnLiveOwned{0};
    std::atomic<std::int32_t> g_preShutdownDelta{0};
    std::atomic<std::int32_t> g_postShutdownDelta{0};
    std::atomic<std::uint32_t> g_lastError{0};
    std::atomic<std::uint64_t> g_initialStyle{0};
    std::atomic<bool> g_pending{false};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_regionChanged{false};
    std::atomic<bool> g_styleChanged{false};
    std::atomic<bool> g_completeRequested{false};
    std::atomic<bool> g_lastApplySucceeded{false};
    std::atomic<bool> g_counterInvariants{false};
    std::atomic<bool> g_representativeValidation{false};
    std::atomic<bool> g_regionRestored{false};
    std::atomic<bool> g_styleRestored{false};
    std::atomic<bool> g_bodyCenter{false};
    std::atomic<bool> g_longEar{false};
    std::atomic<bool> g_mirroredEar{false};
    std::atomic<bool> g_tail{false};
    std::atomic<bool> g_mirroredTail{false};
    std::atomic<bool> g_corner{false};
    std::atomic<bool> g_bottomLeftCorner{false};

    // Composition UI thread only.
    HRGN g_savedInitialRegion = nullptr;
    bool g_initialRegionWasNone = true;

    void StoreState(StaticComplexSilhouetteState state)
    {
        g_state.store(static_cast<std::int32_t>(state));
    }

    std::uint64_t Qpc()
    {
        LARGE_INTEGER value{};
        ::QueryPerformanceCounter(&value);
        return static_cast<std::uint64_t>(value.QuadPart);
    }

    std::uint64_t ToMicroseconds(std::uint64_t start, std::uint64_t end)
    {
        LARGE_INTEGER frequency{};
        ::QueryPerformanceFrequency(&frequency);
        if (frequency.QuadPart <= 0 || end < start)
        {
            return 0;
        }
        return ((end - start) * 1000000ull)
            / static_cast<std::uint64_t>(frequency.QuadPart);
    }

    void UpdateMaximum(
        std::atomic<std::uint64_t>& value,
        std::uint64_t candidate)
    {
        auto current = value.load();
        while (candidate > current
               && !value.compare_exchange_weak(current, candidate))
        {
        }
    }

    std::uint32_t CurrentGdiCount()
    {
        return ::GetGuiResources(::GetCurrentProcess(), GR_GDIOBJECTS);
    }

    HRGN CreateTrackedRectRgn(int left, int top, int right, int bottom)
    {
        HRGN region = ::CreateRectRgn(left, top, right, bottom);
        if (region != nullptr)
        {
            g_hrgnCreated.fetch_add(1);
            g_hrgnLiveOwned.fetch_add(1);
        }
        return region;
    }

    void DeleteTrackedRegion(HRGN region)
    {
        if (region != nullptr && ::DeleteObject(region) != FALSE)
        {
            g_hrgnDeleted.fetch_add(1);
            g_hrgnLiveOwned.fetch_sub(1);
        }
    }

    void TransferTrackedRegion()
    {
        g_hrgnTransferred.fetch_add(1);
        g_hrgnLiveOwned.fetch_sub(1);
    }

    void Fail(
        StaticComplexSilhouetteFailureStage stage,
        DWORD error = ERROR_SUCCESS)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage));
        g_lastError.store(error);
        g_pending.store(false);
        g_enabled.store(false);
        StoreState(StaticComplexSilhouetteState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Static complex silhouette failed: "
            L"stage=%d error=%lu.\n",
            static_cast<int>(stage),
            error);
        ::OutputDebugStringW(message);
    }

    bool SetStyle(HWND window, LONG_PTR style, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        const auto previous =
            ::SetWindowLongPtrW(window, GWL_EXSTYLE, style);
        error = ::GetLastError();
        return !(previous == 0 && error != ERROR_SUCCESS)
            && ::SetWindowPos(
                   window, nullptr, 0, 0, 0, 0, kFrameChangedFlags)
                != FALSE;
    }

    bool CaptureInitialRegion(HWND window, DWORD& error)
    {
        HRGN region = CreateTrackedRectRgn(0, 0, 0, 0);
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
                g_initialRegionWasNone = true;
                return true;
            }
            return false;
        }
        g_initialRegionWasNone = false;
        g_savedInitialRegion = region;
        return true;
    }

    HRGN DuplicateRegion(HRGN source)
    {
        HRGN copy = CreateTrackedRectRgn(0, 0, 0, 0);
        if (copy == nullptr
            || ::CombineRgn(copy, source, nullptr, RGN_COPY) == ERROR)
        {
            if (copy != nullptr) DeleteTrackedRegion(copy);
            return nullptr;
        }
        g_hrgnRestoreCreated.fetch_add(1);
        return copy;
    }

    void ReleaseSavedRegion()
    {
        if (g_savedInitialRegion != nullptr)
        {
            DeleteTrackedRegion(g_savedInitialRegion);
            g_savedInitialRegion = nullptr;
        }
    }

    bool BuildMergedRegion(
        const std::array<std::uint8_t, kByteCount>& pixels,
        int threshold,
        HRGN& output,
        DWORD& error)
    {
        std::map<std::pair<int, int>, Rectangle> active;
        std::vector<Rectangle> complete;
        std::uint32_t rawRuns = 0;
        std::uint32_t covered = 0;
        for (int y = 0; y < kHeight; ++y)
        {
            std::map<std::pair<int, int>, Rectangle> next;
            int x = 0;
            while (x < kWidth)
            {
                while (x < kWidth
                       && pixels[y * kStride + x] < threshold) ++x;
                if (x == kWidth) break;
                const int start = x;
                while (x < kWidth
                       && pixels[y * kStride + x] >= threshold) ++x;
                const auto key = std::make_pair(start, x);
                const auto previous = active.find(key);
                next[key] = previous == active.end()
                    ? Rectangle{start, y, x, y + 1}
                    : Rectangle{
                        previous->second.left,
                        previous->second.top,
                        previous->second.right,
                        y + 1};
                ++rawRuns;
                covered += static_cast<std::uint32_t>(x - start);
            }
            for (const auto& [key, rectangle] : active)
            {
                if (!next.contains(key)) complete.push_back(rectangle);
            }
            active = std::move(next);
        }
        for (const auto& [key, rectangle] : active)
        {
            static_cast<void>(key);
            complete.push_back(rectangle);
        }

        HRGN combined = CreateTrackedRectRgn(0, 0, 0, 0);
        if (combined == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        for (const auto& rectangle : complete)
        {
            HRGN part = CreateTrackedRectRgn(
                rectangle.left,
                rectangle.top,
                rectangle.right,
                rectangle.bottom);
            if (part == nullptr)
            {
                error = ::GetLastError();
                DeleteTrackedRegion(combined);
                return false;
            }
            const int result =
                ::CombineRgn(combined, combined, part, RGN_OR);
            DeleteTrackedRegion(part);
            if (result == ERROR)
            {
                error = ::GetLastError();
                DeleteTrackedRegion(combined);
                return false;
            }
        }
        g_rawRunCount.store(rawRuns);
        g_mergedRectangleCount.store(
            static_cast<std::uint32_t>(complete.size()));
        g_coveredCount.store(covered);
        g_excludedCount.store(
            static_cast<std::uint32_t>(kByteCount) - covered);
        output = combined;
        return true;
    }

    bool QueryRegionData(HRGN region, DWORD& error)
    {
        ::SetLastError(ERROR_SUCCESS);
        const DWORD bytes = ::GetRegionData(region, 0, nullptr);
        if (bytes == 0)
        {
            error = ::GetLastError();
            return false;
        }
        std::vector<std::byte> storage(bytes);
        auto* data = reinterpret_cast<RGNDATA*>(storage.data());
        if (::GetRegionData(region, bytes, data) == 0)
        {
            error = ::GetLastError();
            return false;
        }
        g_regionDataBytes.store(bytes);
        g_regionDataRectangleCount.store(data->rdh.nCount);
        RECT box{};
        const int type = ::GetRgnBox(region, &box);
        if (type == ERROR)
        {
            error = ::GetLastError();
            return false;
        }
        g_regionType.store(type);
        return true;
    }

    bool ValidateRepresentativePoints(HRGN region)
    {
        const bool body = ::PtInRegion(region, 124, 152) != FALSE;
        const bool ear = ::PtInRegion(region, 60, 32) != FALSE;
        const bool earMirror = ::PtInRegion(region, 192, 32) != FALSE;
        const bool tail = ::PtInRegion(region, 228, 172) != FALSE;
        const bool tailMirror = ::PtInRegion(region, 228, 80) != FALSE;
        const bool corner = ::PtInRegion(region, 248, 8) != FALSE;
        const bool bottomLeftCorner =
            ::PtInRegion(region, 8, 248) != FALSE;
        g_bodyCenter.store(body);
        g_longEar.store(ear);
        g_mirroredEar.store(earMirror);
        g_tail.store(tail);
        g_mirroredTail.store(tailMirror);
        g_corner.store(corner);
        g_bottomLeftCorner.store(bottomLeftCorner);
        return body && ear && !earMirror && tail && !tailMirror
            && !corner && !bottomLeftCorner;
    }

    bool ValidateAppliedRegion(HWND window, DWORD& error)
    {
        HRGN applied = CreateTrackedRectRgn(0, 0, 0, 0);
        if (applied == nullptr)
        {
            error = ::GetLastError();
            return false;
        }
        const int result = ::GetWindowRgn(window, applied);
        if (result == ERROR)
        {
            error = ::GetLastError();
            DeleteTrackedRegion(applied);
            return false;
        }
        const bool valid = ValidateRepresentativePoints(applied);
        DeleteTrackedRegion(applied);
        return valid;
    }
}

namespace DesktopMascotNative
{
    void ResetStaticComplexSilhouetteDiagnostics()
    {
        ReleaseSavedRegion();
        g_window.store(nullptr); g_state.store(0); g_failureStage.store(0);
        g_threshold.store(0); g_publishedGeneration.store(0);
        g_appliedGeneration.store(0); g_evaluationCount.store(0);
        g_buildCount.store(0); g_postCount.store(0);
        g_executionCount.store(0); g_successCount.store(0);
        g_failureCount.store(0); g_duplicateCount.store(0);
        g_supersededCount.store(0); g_rawRunCount.store(0);
        g_mergedRectangleCount.store(0);
        g_regionDataRectangleCount.store(0); g_regionType.store(0);
        g_coveredCount.store(0); g_excludedCount.store(0);
        g_regionDataBytes.store(0); g_lastBuildUs.store(0);
        g_minBuildUs.store(std::numeric_limits<std::uint64_t>::max());
        g_maxBuildUs.store(0); g_totalBuildUs.store(0);
        g_lastSetWindowRgnUs.store(0); g_maxSetWindowRgnUs.store(0);
        g_initialGdi.store(0); g_preShutdownGdi.store(0);
        g_postShutdownGdi.store(0); g_preShutdownDelta.store(0);
        g_afterBuildGdi.store(0); g_afterApplyGdi.store(0);
        g_afterRestoreGdi.store(0); g_afterWindowDestroyGdi.store(0);
        g_afterUiJoinGdi.store(0); g_afterCleanupGdi.store(0);
        g_hrgnCreated.store(0); g_hrgnDeleted.store(0);
        g_hrgnTransferred.store(0); g_hrgnRestoreCreated.store(0);
        g_hrgnLiveOwned.store(0);
        g_postShutdownDelta.store(0); g_lastError.store(0);
        g_initialStyle.store(0); g_pending.store(false);
        g_enabled.store(false); g_regionChanged.store(false);
        g_styleChanged.store(false); g_completeRequested.store(false);
        g_lastApplySucceeded.store(false);
        g_counterInvariants.store(false);
        g_representativeValidation.store(false);
        g_regionRestored.store(false); g_styleRestored.store(false);
        g_bodyCenter.store(false); g_longEar.store(false);
        g_mirroredEar.store(false); g_tail.store(false);
        g_mirroredTail.store(false); g_corner.store(false);
        g_bottomLeftCorner.store(false);
        g_initialRegionWasNone = true;
    }

    void SetStaticComplexSilhouetteUiWindow(void* window)
    {
        g_window.store(static_cast<HWND>(window));
    }

    std::int32_t StartStaticComplexSilhouetteDiagnostics(
        std::int32_t threshold)
    {
        if (threshold != 128 && threshold != 32)
        {
            Fail(StaticComplexSilhouetteFailureStage::InvalidThreshold);
            return 0;
        }
        if (GetCompositionInitializationState()
            != static_cast<std::int32_t>(
                CompositionInitializationState::MessageLoopRunning))
        {
            Fail(StaticComplexSilhouetteFailureStage::CompositionNotReady);
            return 0;
        }
        if (!IsCompositionAlphaMaskAvailable())
        {
            StoreState(StaticComplexSilhouetteState::WaitingForMask);
            return 0;
        }
        const HWND window = g_window.load();
        if (window == nullptr)
        {
            Fail(StaticComplexSilhouetteFailureStage::WindowUnavailable);
            return 0;
        }
        g_threshold.store(threshold);
        g_publishedGeneration.store(
            GetCompositionAlphaMaskPublishedGeneration());
        g_pending.store(true);
        StoreState(StaticComplexSilhouetteState::PostingRegionApply);
        if (::PostMessageW(
                window,
                kStaticComplexSilhouetteApplyMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::ApplyMessagePostFailed,
                ::GetLastError());
            return 0;
        }
        g_postCount.fetch_add(1);
        StoreState(StaticComplexSilhouetteState::WaitingForRegionApply);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Static complex silhouette diagnostics "
            L"started.\n");
        return 1;
    }

    void HandleStaticComplexSilhouetteApplyMessage()
    {
        g_executionCount.fetch_add(1);
        const HWND window = g_window.load();
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(StaticComplexSilhouetteFailureStage::WindowUnavailable);
            return;
        }
        DWORD error = ERROR_SUCCESS;
        ::SetLastError(ERROR_SUCCESS);
        const LONG_PTR initial = ::GetWindowLongPtrW(window, GWL_EXSTYLE);
        error = ::GetLastError();
        if (initial == 0 && error != ERROR_SUCCESS)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    InitialStyleRestoreFailed,
                error);
            return;
        }
        g_initialStyle.store(static_cast<std::uint64_t>(initial));
        if (!CaptureInitialRegion(window, error))
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    InitialRegionRestoreFailed,
                error);
            return;
        }
        if (!SetStyle(window, initial | WS_EX_LAYERED, error))
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    InitialStyleRestoreFailed,
                error);
            return;
        }
        g_styleChanged.store(true);
        g_initialGdi.store(CurrentGdiCount());

        std::array<std::uint8_t, kByteCount> pixels{};
        std::int32_t width = 0, height = 0, stride = 0, maskThreshold = 0;
        std::uint64_t generation = 0;
        if (!TryCopyCompositionAlphaMaskSnapshot(
                pixels.data(),
                pixels.size(),
                width,
                height,
                stride,
                maskThreshold,
                generation)
            || width != kWidth || height != kHeight || stride != kStride)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::SnapshotCopyFailed);
            return;
        }
        g_evaluationCount.fetch_add(1);
        g_publishedGeneration.store(generation);
        StoreState(StaticComplexSilhouetteState::BuildingRegion);
        const auto buildStart = Qpc();
        HRGN region = nullptr;
        if (!BuildMergedRegion(
                pixels,
                g_threshold.load(),
                region,
                error))
        {
            Fail(
                StaticComplexSilhouetteFailureStage::RegionCreationFailed,
                error);
            return;
        }
        if (g_coveredCount.load() == 0
            || g_coveredCount.load() + g_excludedCount.load()
                != kByteCount)
        {
            DeleteTrackedRegion(region);
            Fail(
                StaticComplexSilhouetteFailureStage::
                    RegionPixelCountValidationFailed);
            return;
        }
        if (!QueryRegionData(region, error))
        {
            DeleteTrackedRegion(region);
            Fail(
                StaticComplexSilhouetteFailureStage::RegionDataQueryFailed,
                error);
            return;
        }
        if (!ValidateRepresentativePoints(region))
        {
            DeleteTrackedRegion(region);
            Fail(
                StaticComplexSilhouetteFailureStage::
                    RepresentativePointValidationFailed);
            return;
        }
        const auto buildUs = std::max<std::uint64_t>(
            1,
            ToMicroseconds(buildStart, Qpc()));
        g_lastBuildUs.store(buildUs);
        g_minBuildUs.store(buildUs);
        g_maxBuildUs.store(buildUs);
        g_totalBuildUs.store(buildUs);
        g_buildCount.fetch_add(1);
        g_afterBuildGdi.store(CurrentGdiCount());

        const auto applyStart = Qpc();
        const int applyResult = ::SetWindowRgn(window, region, TRUE);
        const auto applyUs = ToMicroseconds(applyStart, Qpc());
        g_lastSetWindowRgnUs.store(applyUs);
        UpdateMaximum(g_maxSetWindowRgnUs, applyUs);
        if (applyResult == 0)
        {
            error = ::GetLastError();
            DeleteTrackedRegion(region);
            g_failureCount.fetch_add(1);
            Fail(
                StaticComplexSilhouetteFailureStage::SetWindowRgnFailed,
                error);
            return;
        }
        // SetWindowRgn owns region after success.
        TransferTrackedRegion();
        g_successCount.fetch_add(1);
        g_afterApplyGdi.store(CurrentGdiCount());
        g_regionChanged.store(true);
        g_appliedGeneration.store(generation);
        g_lastApplySucceeded.store(true);
        g_pending.store(false);
        g_enabled.store(true);
        if (g_lastBuildUs.load() > 10000
            || g_lastSetWindowRgnUs.load() > 5000)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    PerformanceThresholdExceeded);
            return;
        }
        if (!ValidateAppliedRegion(window, error))
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    RepresentativePointValidationFailed,
                error);
            return;
        }
        g_representativeValidation.store(true);
        const bool invariant =
            g_successCount.load() + g_failureCount.load()
                == g_executionCount.load()
            && g_executionCount.load() == g_postCount.load()
            && g_buildCount.load() == g_postCount.load()
            && g_buildCount.load() == g_successCount.load();
        g_counterInvariants.store(invariant);
        if (!invariant)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::CounterInvariantFailed);
            return;
        }
        StoreState(StaticComplexSilhouetteState::VisualTestRunning);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Static complex silhouette region "
            L"applied.\n");
    }

    std::int32_t CompleteStaticComplexSilhouetteDiagnostics()
    {
        if (g_state.load()
            != static_cast<std::int32_t>(
                StaticComplexSilhouetteState::VisualTestRunning))
        {
            return 0;
        }
        g_completeRequested.store(true);
        return 1;
    }

    std::int32_t StopStaticComplexSilhouetteDiagnostics()
    {
        if (!g_regionChanged.load() && !g_styleChanged.load()) return 1;
        if (g_pending.exchange(true)) return 0;
        g_enabled.store(false);
        StoreState(StaticComplexSilhouetteState::StopRequested);
        if (::PostMessageW(
                g_window.load(),
                kStaticComplexSilhouetteRestoreMessage,
                0,
                0)
            == FALSE)
        {
            Fail(
                StaticComplexSilhouetteFailureStage::ApplyMessagePostFailed,
                ::GetLastError());
            return 0;
        }
        return 1;
    }

    void HandleStaticComplexSilhouetteRestoreMessage()
    {
        const HWND window = g_window.load();
        DWORD error = ERROR_SUCCESS;
        if (window == nullptr || ::IsWindow(window) == FALSE)
        {
            Fail(StaticComplexSilhouetteFailureStage::WindowUnavailable);
            return;
        }
        StoreState(StaticComplexSilhouetteState::RestoringInitialRegion);
        bool restored = false;
        if (g_initialRegionWasNone)
        {
            restored = ::SetWindowRgn(window, nullptr, TRUE) != 0;
        }
        else
        {
            HRGN copy = DuplicateRegion(g_savedInitialRegion);
            restored = copy != nullptr
                && ::SetWindowRgn(window, copy, TRUE) != 0;
            if (restored) TransferTrackedRegion();
            if (!restored && copy != nullptr) DeleteTrackedRegion(copy);
        }
        if (!restored)
        {
            error = ::GetLastError();
            Fail(
                StaticComplexSilhouetteFailureStage::
                    InitialRegionRestoreFailed,
                error);
            return;
        }
        g_regionChanged.store(false);
        g_regionRestored.store(true);
        ReleaseSavedRegion();

        StoreState(StaticComplexSilhouetteState::RestoringInitialStyle);
        if (g_styleChanged.load()
            && !SetStyle(
                window,
                static_cast<LONG_PTR>(g_initialStyle.load()),
                error))
        {
            Fail(
                StaticComplexSilhouetteFailureStage::
                    InitialStyleRestoreFailed,
                error);
            return;
        }
        g_styleChanged.store(false);
        g_styleRestored.store(true);
        g_afterRestoreGdi.store(CurrentGdiCount());
        g_pending.store(false);
        const auto count = CurrentGdiCount();
        g_preShutdownGdi.store(count);
        g_preShutdownDelta.store(
            static_cast<std::int32_t>(count)
            - static_cast<std::int32_t>(g_initialGdi.load()));
        StoreState(
            g_completeRequested.load()
                ? StaticComplexSilhouetteState::Completed
                : StaticComplexSilhouetteState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Static complex silhouette diagnostics "
            L"stopped.\n");
    }

    void StopStaticComplexSilhouetteDiagnosticsOnUiThread()
    {
        if (g_regionChanged.load() || g_styleChanged.load())
        {
            HandleStaticComplexSilhouetteRestoreMessage();
        }
        ReleaseSavedRegion();
        g_enabled.store(false);
        g_pending.store(false);
        g_window.store(nullptr);
    }

    void RecordStaticComplexSilhouettePostShutdownGdiCount()
    {
        const auto count = CurrentGdiCount();
        g_postShutdownGdi.store(count);
        g_postShutdownDelta.store(
            static_cast<std::int32_t>(count)
            - static_cast<std::int32_t>(g_initialGdi.load()));
    }

    void RecordStaticComplexSilhouetteAfterWindowDestroyGdiCount()
    {
        g_afterWindowDestroyGdi.store(CurrentGdiCount());
    }

    void RecordStaticComplexSilhouetteAfterUiThreadJoinGdiCount()
    {
        g_afterUiJoinGdi.store(CurrentGdiCount());
    }

    void RecordStaticComplexSilhouetteAfterNativeCleanupGdiCount()
    {
        g_afterCleanupGdi.store(CurrentGdiCount());
    }

    bool ShouldStaticComplexSilhouetteReturnClient()
    {
        return g_enabled.load();
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetStaticComplexSilhouetteState, g_state)
    GET_I32(GetStaticComplexSilhouetteFailureStage, g_failureStage)
    GET_I32(GetStaticComplexSilhouetteThreshold, g_threshold)
    GET_U64(
        GetStaticComplexSilhouettePublishedGeneration,
        g_publishedGeneration)
    GET_U64(GetStaticComplexSilhouetteAppliedGeneration, g_appliedGeneration)
    GET_U32(GetStaticComplexSilhouetteGenerationEvaluationCount, g_evaluationCount)
    GET_U32(GetStaticComplexSilhouetteRegionBuildCount, g_buildCount)
    GET_U32(GetStaticComplexSilhouetteApplyMessagePostCount, g_postCount)
    GET_U32(GetStaticComplexSilhouetteApplyExecutionCount, g_executionCount)
    GET_U32(GetStaticComplexSilhouetteApplySuccessCount, g_successCount)
    GET_U32(GetStaticComplexSilhouetteApplyFailureCount, g_failureCount)
    GET_U32(GetStaticComplexSilhouetteDuplicateMaskSkipCount, g_duplicateCount)
    GET_U32(GetStaticComplexSilhouetteSupersededGenerationCount, g_supersededCount)
    GET_U32(GetStaticComplexSilhouetteRawScanlineRunCount, g_rawRunCount)
    GET_U32(GetStaticComplexSilhouetteMergedRectangleCount, g_mergedRectangleCount)
    GET_U32(GetStaticComplexSilhouetteRegionDataRectangleCount, g_regionDataRectangleCount)
    GET_I32(GetStaticComplexSilhouetteRegionType, g_regionType)
    GET_U32(GetStaticComplexSilhouetteCoveredPixelCount, g_coveredCount)
    GET_U32(GetStaticComplexSilhouetteExcludedPixelCount, g_excludedCount)
    GET_U32(GetStaticComplexSilhouetteRegionDataSizeBytes, g_regionDataBytes)
    GET_U64(GetStaticComplexSilhouetteLastBuildMicroseconds, g_lastBuildUs)
    std::uint64_t GetStaticComplexSilhouetteMinimumBuildMicroseconds()
    {
        const auto value = g_minBuildUs.load();
        return value == std::numeric_limits<std::uint64_t>::max()
            ? 0
            : value;
    }
    GET_U64(GetStaticComplexSilhouetteMaximumBuildMicroseconds, g_maxBuildUs)
    std::uint64_t GetStaticComplexSilhouetteAverageBuildMicroseconds()
    {
        const auto count = g_buildCount.load();
        return count == 0 ? 0 : g_totalBuildUs.load() / count;
    }
    GET_U64(GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds, g_lastSetWindowRgnUs)
    GET_U64(GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds, g_maxSetWindowRgnUs)
    GET_U32(GetStaticComplexSilhouetteInitialGdiObjectCount, g_initialGdi)
    GET_U32(GetStaticComplexSilhouettePreShutdownGdiObjectCount, g_preShutdownGdi)
    GET_U32(GetStaticComplexSilhouettePostShutdownGdiObjectCount, g_postShutdownGdi)
    GET_U32(GetStaticComplexSilhouetteAfterRegionBuildGdiObjectCount, g_afterBuildGdi)
    GET_U32(GetStaticComplexSilhouetteAfterRegionApplyGdiObjectCount, g_afterApplyGdi)
    GET_U32(GetStaticComplexSilhouetteAfterRegionRestoreGdiObjectCount, g_afterRestoreGdi)
    GET_U32(GetStaticComplexSilhouetteAfterWindowDestroyGdiObjectCount, g_afterWindowDestroyGdi)
    GET_U32(GetStaticComplexSilhouetteAfterUiThreadJoinGdiObjectCount, g_afterUiJoinGdi)
    GET_U32(GetStaticComplexSilhouetteAfterNativeCleanupGdiObjectCount, g_afterCleanupGdi)
    GET_U32(GetStaticComplexSilhouetteHrgnCreatedCount, g_hrgnCreated)
    GET_U32(GetStaticComplexSilhouetteHrgnCallerDeletedCount, g_hrgnDeleted)
    GET_U32(GetStaticComplexSilhouetteHrgnOwnershipTransferredCount, g_hrgnTransferred)
    GET_U32(GetStaticComplexSilhouetteHrgnRestoreCreatedCount, g_hrgnRestoreCreated)
    GET_I32(GetStaticComplexSilhouetteHrgnLiveOwnedCount, g_hrgnLiveOwned)
    GET_I32(GetStaticComplexSilhouettePreShutdownGdiDelta, g_preShutdownDelta)
    GET_I32(GetStaticComplexSilhouettePostShutdownGdiDelta, g_postShutdownDelta)
    GET_BOOL(DidStaticComplexSilhouetteCounterInvariantsSucceed, g_counterInvariants)
    GET_BOOL(DidStaticComplexSilhouetteRepresentativeValidationSucceed, g_representativeValidation)
    GET_BOOL(DidStaticComplexSilhouetteInitialRegionRestoreSucceed, g_regionRestored)
    GET_BOOL(DidStaticComplexSilhouetteInitialStyleRestoreSucceed, g_styleRestored)
    GET_BOOL(DidStaticComplexSilhouetteLastApplySucceed, g_lastApplySucceeded)
    GET_U32(GetStaticComplexSilhouetteLastWin32Error, g_lastError)
    GET_BOOL(IsStaticComplexSilhouetteBodyCenterInside, g_bodyCenter)
    GET_BOOL(IsStaticComplexSilhouetteLongLeftEarInside, g_longEar)
    GET_BOOL(IsStaticComplexSilhouetteMirroredEarInside, g_mirroredEar)
    GET_BOOL(IsStaticComplexSilhouetteBottomRightTailInside, g_tail)
    GET_BOOL(IsStaticComplexSilhouetteMirroredTailInside, g_mirroredTail)
    GET_BOOL(IsStaticComplexSilhouetteTransparentCornerInside, g_corner)
    GET_BOOL(
        IsStaticComplexSilhouetteTransparentBottomLeftCornerInside,
        g_bottomLeftCorner)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
