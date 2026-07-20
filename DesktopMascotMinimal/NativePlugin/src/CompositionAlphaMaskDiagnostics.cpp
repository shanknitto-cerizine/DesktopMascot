#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"

#include <Windows.h>

#include <algorithm>
#include <atomic>
#include <cstddef>
#include <cwchar>
#include <limits>
#include <mutex>
#include <new>
#include <utility>
#include <vector>

namespace
{
    using DesktopMascotNative::CompositionAlphaMaskFailureStage;
    using DesktopMascotNative::CompositionAlphaMaskState;

    constexpr std::int32_t kWidth = 256;
    constexpr std::int32_t kHeight = 256;
    constexpr std::int32_t kStride = 256;
    constexpr std::int32_t kByteCount = kStride * kHeight;
    constexpr std::int32_t kThreshold = 128;
    constexpr std::uint32_t kTargetMaskCount = 20;
    constexpr std::int32_t kTolerance = 2;

    struct AlphaMaskSnapshot
    {
        std::int32_t width = 0;
        std::int32_t height = 0;
        std::int32_t stride = 0;
        std::int32_t byteCount = 0;
        std::int32_t threshold = 0;
        std::uint64_t generation = 0;
        std::vector<std::uint8_t> pixels;
    };

    std::mutex g_submitMutex;
    std::mutex g_publishMutex;
    AlphaMaskSnapshot g_staging;
    AlphaMaskSnapshot g_published;
    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::uint32_t> g_submittedCount{0};
    std::atomic<std::uint32_t> g_acceptedCount{0};
    std::atomic<std::uint32_t> g_rejectedCount{0};
    std::atomic<std::uint64_t> g_publishedGeneration{0};
    std::atomic<std::int32_t> g_width{0};
    std::atomic<std::int32_t> g_height{0};
    std::atomic<std::int32_t> g_stride{0};
    std::atomic<std::int32_t> g_byteCount{0};
    std::atomic<std::int32_t> g_threshold{0};
    std::atomic<bool> g_available{false};
    std::atomic<bool> g_requestPending{false};
    std::atomic<bool> g_lastSubmitSucceeded{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<std::uint32_t> g_lastWin32Error{0};
    std::atomic<std::uint32_t> g_lastSubmitThreadId{0};
    std::atomic<bool> g_validateFixedPattern{true};

    void StoreState(CompositionAlphaMaskState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    void Fail(CompositionAlphaMaskFailureStage stage)
    {
        std::int32_t expected =
            static_cast<std::int32_t>(
                CompositionAlphaMaskFailureStage::None);
        g_failureStage.compare_exchange_strong(
            expected,
            static_cast<std::int32_t>(stage),
            std::memory_order_acq_rel);
        StoreState(CompositionAlphaMaskState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Composition alpha mask diagnostics "
            L"failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool TryComputeByteCount(
        std::int32_t stride,
        std::int32_t height,
        std::int32_t& byteCount)
    {
        if (stride <= 0 || height <= 0
            || stride > std::numeric_limits<std::int32_t>::max() / height)
        {
            return false;
        }
        byteCount = stride * height;
        return true;
    }

    bool Approximately(std::int32_t actual, std::int32_t expected)
    {
        return actual >= expected - kTolerance
            && actual <= expected + kTolerance;
    }

    std::int32_t Sample(
        const AlphaMaskSnapshot& snapshot,
        std::int32_t x,
        std::int32_t y)
    {
        return snapshot.pixels[
            static_cast<std::size_t>(y)
                * static_cast<std::size_t>(snapshot.stride)
            + static_cast<std::size_t>(x)];
    }

    bool ValidatePublishedSnapshot()
    {
        StoreState(CompositionAlphaMaskState::Validating);
        std::lock_guard lock(g_publishMutex);
        if (g_published.pixels.size() != kByteCount)
        {
            Fail(CompositionAlphaMaskFailureStage::SnapshotUnavailable);
            return false;
        }
        const std::int32_t topLeft = Sample(g_published, 8, 8);
        const std::int32_t topRight = Sample(g_published, 55, 8);
        const std::int32_t bottomLeft = Sample(g_published, 8, 55);
        const std::int32_t bottomRight = Sample(g_published, 55, 55);
        const std::int32_t center = Sample(g_published, 32, 32);
        if (!Approximately(topLeft, 0)
            || !Approximately(topRight, 64)
            || !Approximately(bottomLeft, 128)
            || !Approximately(bottomRight, 255)
            || !Approximately(center, 192))
        {
            Fail(CompositionAlphaMaskFailureStage::UnexpectedAlphaValue);
            return false;
        }
        const bool flippedMatch =
            Approximately(topLeft, 128)
            && Approximately(topRight, 255)
            && Approximately(bottomLeft, 0)
            && Approximately(bottomRight, 64)
            && Approximately(center, 192);
        if (flippedMatch)
        {
            Fail(CompositionAlphaMaskFailureStage::OrientationMismatch);
            return false;
        }
        if ((topLeft >= g_published.threshold)
            || (topRight >= g_published.threshold)
            || !(bottomLeft >= g_published.threshold)
            || !(bottomRight >= g_published.threshold)
            || !(center >= g_published.threshold))
        {
            Fail(CompositionAlphaMaskFailureStage::ThresholdMismatch);
            return false;
        }
        if (g_submittedCount.load() != kTargetMaskCount)
        {
            Fail(CompositionAlphaMaskFailureStage::UnexpectedSubmittedCount);
            return false;
        }
        if (g_acceptedCount.load() != kTargetMaskCount)
        {
            Fail(CompositionAlphaMaskFailureStage::UnexpectedAcceptedCount);
            return false;
        }
        if (g_rejectedCount.load() != 0)
        {
            Fail(CompositionAlphaMaskFailureStage::UnexpectedRejectedCount);
            return false;
        }
        StoreState(CompositionAlphaMaskState::Completed);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition alpha mask validation "
            L"completed.\n");
        return true;
    }

    std::int32_t Reject(CompositionAlphaMaskFailureStage stage)
    {
        g_rejectedCount.fetch_add(1, std::memory_order_relaxed);
        g_lastSubmitSucceeded.store(false, std::memory_order_release);
        g_requestPending.store(false, std::memory_order_release);
        Fail(stage);
        return 0;
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionAlphaMaskDiagnostics()
    {
        std::scoped_lock lock(g_submitMutex, g_publishMutex);
        g_staging = {};
        g_published = {};
        g_state.store(0);
        g_failureStage.store(0);
        g_submittedCount.store(0);
        g_acceptedCount.store(0);
        g_rejectedCount.store(0);
        g_publishedGeneration.store(0);
        g_width.store(0);
        g_height.store(0);
        g_stride.store(0);
        g_byteCount.store(0);
        g_threshold.store(0);
        g_available.store(false);
        g_requestPending.store(false);
        g_lastSubmitSucceeded.store(false);
        g_shutdownRequested.store(false);
        g_lastWin32Error.store(0);
        g_lastSubmitThreadId.store(0);
        g_validateFixedPattern.store(true);
    }

    std::int32_t StartCompositionAlphaMaskDiagnostics(
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t alphaThreshold)
    {
        if (GetCompositionInitializationState()
            != static_cast<std::int32_t>(
                CompositionInitializationState::MessageLoopRunning))
        {
            Fail(CompositionAlphaMaskFailureStage::CompositionNotReady);
            return 0;
        }
        if (width != kWidth)
        {
            Fail(CompositionAlphaMaskFailureStage::InvalidWidth);
            return 0;
        }
        if (height != kHeight)
        {
            Fail(CompositionAlphaMaskFailureStage::InvalidHeight);
            return 0;
        }
        if (stride != kStride)
        {
            Fail(CompositionAlphaMaskFailureStage::InvalidStride);
            return 0;
        }
        std::int32_t byteCount = 0;
        if (!TryComputeByteCount(stride, height, byteCount)
            || byteCount != kByteCount)
        {
            Fail(CompositionAlphaMaskFailureStage::ByteCountOverflow);
            return 0;
        }
        if (alphaThreshold != kThreshold)
        {
            Fail(CompositionAlphaMaskFailureStage::ThresholdMismatch);
            return 0;
        }
        const auto state = g_state.load(std::memory_order_acquire);
        if (state != static_cast<std::int32_t>(
                         CompositionAlphaMaskState::NotStarted)
            && state != static_cast<std::int32_t>(
                            CompositionAlphaMaskState::Stopped))
        {
            return 0;
        }
        try
        {
            std::scoped_lock lock(g_submitMutex, g_publishMutex);
            g_staging = {};
            g_published = {};
            g_staging.pixels.resize(byteCount);
            g_published.pixels.resize(byteCount);
        }
        catch (const std::bad_alloc&)
        {
            Fail(CompositionAlphaMaskFailureStage::BufferAllocationFailed);
            return 0;
        }
        g_failureStage.store(0);
        g_submittedCount.store(0);
        g_acceptedCount.store(0);
        g_rejectedCount.store(0);
        g_publishedGeneration.store(0);
        g_width.store(width);
        g_height.store(height);
        g_stride.store(stride);
        g_byteCount.store(byteCount);
        g_threshold.store(alphaThreshold);
        g_available.store(false);
        g_requestPending.store(false);
        g_lastSubmitSucceeded.store(false);
        g_shutdownRequested.store(false);
        g_lastWin32Error.store(0);
        g_lastSubmitThreadId.store(0);
        g_validateFixedPattern.store(true);
        StoreState(CompositionAlphaMaskState::Ready);
        StoreState(CompositionAlphaMaskState::WaitingForFirstMask);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition alpha mask diagnostics "
            L"started.\n");
        return 1;
    }

    std::int32_t StartAnimatedCompositionAlphaMaskDiagnostics(
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t alphaThreshold)
    {
        const auto result = StartCompositionAlphaMaskDiagnostics(
            width,
            height,
            stride,
            alphaThreshold);
        if (result == 1)
        {
            g_validateFixedPattern.store(false, std::memory_order_release);
        }
        return result;
    }

    std::int32_t SubmitCompositionAlphaMask(
        const std::uint8_t* data,
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::uint64_t generation)
    {
        g_submittedCount.fetch_add(1, std::memory_order_relaxed);
        g_requestPending.store(true, std::memory_order_release);
        g_lastSubmitThreadId.store(::GetCurrentThreadId());
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            return Reject(
                CompositionAlphaMaskFailureStage::ShutdownAlreadyRequested);
        }
        const auto state = g_state.load(std::memory_order_acquire);
        if (state != static_cast<std::int32_t>(
                         CompositionAlphaMaskState::WaitingForFirstMask)
            && state != static_cast<std::int32_t>(
                            CompositionAlphaMaskState::Receiving)
            && state != static_cast<std::int32_t>(
                            CompositionAlphaMaskState::SnapshotPublished)
            && state != static_cast<std::int32_t>(
                            CompositionAlphaMaskState::Completed))
        {
            return Reject(
                CompositionAlphaMaskFailureStage::DiagnosticsNotStarted);
        }
        if (data == nullptr)
        {
            return Reject(CompositionAlphaMaskFailureStage::NullData);
        }
        if (width != g_width.load())
        {
            return Reject(CompositionAlphaMaskFailureStage::InvalidWidth);
        }
        if (height != g_height.load())
        {
            return Reject(CompositionAlphaMaskFailureStage::InvalidHeight);
        }
        if (stride != g_stride.load())
        {
            return Reject(CompositionAlphaMaskFailureStage::InvalidStride);
        }
        std::int32_t byteCount = 0;
        if (!TryComputeByteCount(stride, height, byteCount)
            || byteCount != g_byteCount.load())
        {
            return Reject(
                CompositionAlphaMaskFailureStage::ByteCountOverflow);
        }
        if (generation == 0)
        {
            return Reject(
                CompositionAlphaMaskFailureStage::InvalidGeneration);
        }
        if (generation <= g_publishedGeneration.load(std::memory_order_acquire))
        {
            return Reject(
                CompositionAlphaMaskFailureStage::StaleGeneration);
        }

        {
            std::lock_guard submitLock(g_submitMutex);
            std::copy_n(data, byteCount, g_staging.pixels.begin());
            g_staging.width = width;
            g_staging.height = height;
            g_staging.stride = stride;
            g_staging.byteCount = byteCount;
            g_staging.threshold = g_threshold.load();
            g_staging.generation = generation;
            {
                std::lock_guard publishLock(g_publishMutex);
                std::swap(g_staging, g_published);
            }
        }
        const auto accepted =
            g_acceptedCount.fetch_add(1, std::memory_order_relaxed) + 1;
        g_publishedGeneration.store(generation, std::memory_order_release);
        g_available.store(true, std::memory_order_release);
        g_lastSubmitSucceeded.store(true, std::memory_order_release);
        g_requestPending.store(false, std::memory_order_release);
        StoreState(CompositionAlphaMaskState::SnapshotPublished);
        if (accepted == 1)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] First alpha mask snapshot "
                L"published.\n");
        }
        if (accepted == kTargetMaskCount
            && g_validateFixedPattern.load(std::memory_order_acquire))
        {
            return ValidatePublishedSnapshot() ? 1 : 0;
        }
        StoreState(CompositionAlphaMaskState::Receiving);
        return 1;
    }

    void StopCompositionAlphaMaskDiagnostics()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        StoreState(CompositionAlphaMaskState::ShutdownRequested);
        {
            std::scoped_lock lock(g_submitMutex, g_publishMutex);
            g_staging.pixels.clear();
            g_published.pixels.clear();
        }
        g_available.store(false, std::memory_order_release);
        g_requestPending.store(false, std::memory_order_release);
        StoreState(CompositionAlphaMaskState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition alpha mask diagnostics "
            L"stopped.\n");
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionAlphaMaskState, g_state)
    GET_I32(GetCompositionAlphaMaskFailureStage, g_failureStage)
    GET_U32(GetCompositionAlphaMaskSubmittedCount, g_submittedCount)
    GET_U32(GetCompositionAlphaMaskAcceptedCount, g_acceptedCount)
    GET_U32(GetCompositionAlphaMaskRejectedCount, g_rejectedCount)
    GET_U64(
        GetCompositionAlphaMaskPublishedGeneration,
        g_publishedGeneration)
    GET_I32(GetCompositionAlphaMaskWidth, g_width)
    GET_I32(GetCompositionAlphaMaskHeight, g_height)
    GET_I32(GetCompositionAlphaMaskStride, g_stride)
    GET_I32(GetCompositionAlphaMaskByteCount, g_byteCount)
    GET_I32(GetCompositionAlphaMaskThreshold, g_threshold)
    GET_BOOL(IsCompositionAlphaMaskAvailable, g_available)
    GET_BOOL(IsCompositionAlphaMaskRequestPending, g_requestPending)
    GET_BOOL(
        DidCompositionAlphaMaskLastSubmitSucceed,
        g_lastSubmitSucceeded)
    GET_U32(GetCompositionAlphaMaskLastWin32Error, g_lastWin32Error)
    GET_U32(
        GetCompositionAlphaMaskLastSubmitThreadId,
        g_lastSubmitThreadId)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL

    std::int32_t GetCompositionAlphaMaskSampleAlpha(
        std::int32_t x,
        std::int32_t y)
    {
        std::lock_guard lock(g_publishMutex);
        if (!g_available.load(std::memory_order_acquire)
            || g_published.pixels.empty())
        {
            Fail(CompositionAlphaMaskFailureStage::SnapshotUnavailable);
            return -1;
        }
        if (x < 0 || x >= g_published.width
            || y < 0 || y >= g_published.height)
        {
            Fail(CompositionAlphaMaskFailureStage::SampleOutOfRange);
            return -1;
        }
        return Sample(g_published, x, y);
    }

    bool GetCompositionAlphaMaskSampleHit(
        std::int32_t x,
        std::int32_t y)
    {
        const auto alpha = GetCompositionAlphaMaskSampleAlpha(x, y);
        return alpha >= 0 && alpha >= g_threshold.load();
    }

    bool TryReadCompositionAlphaMaskPixel(
        std::int32_t x,
        std::int32_t y,
        std::uint8_t& alpha,
        std::int32_t& threshold,
        std::uint64_t& generation)
    {
        std::lock_guard lock(g_publishMutex);
        if (!g_available.load(std::memory_order_acquire)
            || g_published.pixels.empty()
            || x < 0
            || y < 0
            || x >= g_published.width
            || y >= g_published.height)
        {
            return false;
        }
        alpha = static_cast<std::uint8_t>(Sample(g_published, x, y));
        threshold = g_published.threshold;
        generation = g_published.generation;
        return true;
    }

    bool TryCopyCompositionAlphaMaskSnapshot(
        std::uint8_t* destination,
        std::size_t destinationSize,
        std::int32_t& width,
        std::int32_t& height,
        std::int32_t& stride,
        std::int32_t& threshold,
        std::uint64_t& generation)
    {
        std::lock_guard lock(g_publishMutex);
        if (!g_available.load(std::memory_order_acquire)
            || destination == nullptr
            || g_published.pixels.empty()
            || destinationSize < g_published.pixels.size())
        {
            return false;
        }
        std::copy(
            g_published.pixels.begin(),
            g_published.pixels.end(),
            destination);
        width = g_published.width;
        height = g_published.height;
        stride = g_published.stride;
        threshold = g_published.threshold;
        generation = g_published.generation;
        return true;
    }
}
