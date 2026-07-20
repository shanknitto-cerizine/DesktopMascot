#include "DesktopMascotNative/ReadbackDiagnostics.h"

#include "DesktopMascotNative/DestinationTexture.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi.h>
#include <wrl/client.h>

#include <atomic>
#include <cwchar>
#include <mutex>

#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"

namespace
{
    using DesktopMascotNative::ReadbackFailureStage;
    using Microsoft::WRL::ComPtr;

    constexpr std::int32_t kNotAttempted = 0x7FFFFFFF;
    constexpr std::uint8_t kChannelTolerance = 2;

    std::mutex g_mutex;
    ComPtr<ID3D12Resource> g_readbackBuffer;
    ComPtr<ID3D12Fence> g_fence;
    D3D12_PLACED_SUBRESOURCE_FOOTPRINT g_footprint{};
    std::atomic<std::uint32_t> g_numRows{0};
    std::atomic<std::uint64_t> g_rowSize{0};
    std::atomic<std::uint64_t> g_totalBytes{0};
    std::atomic<std::int32_t> g_creationResult{kNotAttempted};
    std::atomic<std::int32_t> g_copyEventCount{0};
    std::atomic<bool> g_copyCallbackReached{false};
    std::atomic<bool> g_copyRecorded{false};
    std::atomic<bool> g_signalAttempted{false};
    std::atomic<bool> g_signalSucceeded{false};
    std::atomic<std::int32_t> g_signalResult{kNotAttempted};
    std::atomic<std::uint64_t> g_nextSignalValue{0};
    std::atomic<std::uint64_t> g_submittedSignalValue{0};
    std::atomic<bool> g_mapAttempted{false};
    std::atomic<bool> g_mapSucceeded{false};
    std::atomic<bool> g_validationCompleted{false};
    std::atomic<bool> g_expectedOrientation{false};
    std::atomic<bool> g_flippedOrientation{false};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::uint32_t> g_topLeft{0};
    std::atomic<std::uint32_t> g_topRight{0};
    std::atomic<std::uint32_t> g_bottomLeft{0};
    std::atomic<std::uint32_t> g_bottomRight{0};

    void Fail(ReadbackFailureStage stage)
    {
        g_failureStage.store(
            static_cast<std::int32_t>(stage),
            std::memory_order_release);
        wchar_t message[128]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Readback pixel validation failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    D3D12_RESOURCE_BARRIER Transition(
        ID3D12Resource* resource,
        D3D12_RESOURCE_STATES before,
        D3D12_RESOURCE_STATES after)
    {
        D3D12_RESOURCE_BARRIER barrier{};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE;
        barrier.Transition.pResource = resource;
        barrier.Transition.Subresource =
            D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        barrier.Transition.StateBefore = before;
        barrier.Transition.StateAfter = after;
        return barrier;
    }

    std::uint32_t ReadBgra(
        const std::uint8_t* bytes,
        std::uint32_t x,
        std::uint32_t y)
    {
        const auto offset =
            g_footprint.Offset
            + static_cast<std::uint64_t>(y)
                * g_footprint.Footprint.RowPitch
            + static_cast<std::uint64_t>(x) * 4;
        const auto* pixel = bytes + offset;
        return static_cast<std::uint32_t>(pixel[0])
            | (static_cast<std::uint32_t>(pixel[1]) << 8)
            | (static_cast<std::uint32_t>(pixel[2]) << 16)
            | (static_cast<std::uint32_t>(pixel[3]) << 24);
    }

    bool ChannelNear(std::uint8_t actual, std::uint8_t expected)
    {
        const auto difference =
            actual > expected ? actual - expected : expected - actual;
        return difference <= kChannelTolerance;
    }

    bool PixelNear(std::uint32_t actual, std::uint32_t expected)
    {
        for (std::uint32_t shift = 0; shift < 32; shift += 8)
        {
            if (!ChannelNear(
                    static_cast<std::uint8_t>(actual >> shift),
                    static_cast<std::uint8_t>(expected >> shift)))
            {
                return false;
            }
        }
        return true;
    }
}

namespace DesktopMascotNative
{
    void ReleaseReadbackResources()
    {
        std::lock_guard lock(g_mutex);
        g_readbackBuffer.Reset();
        g_fence.Reset();
        g_footprint = {};
    }

    void ResetReadbackDiagnostics()
    {
        ReleaseReadbackResources();
        g_numRows.store(0, std::memory_order_relaxed);
        g_rowSize.store(0, std::memory_order_relaxed);
        g_totalBytes.store(0, std::memory_order_relaxed);
        g_creationResult.store(kNotAttempted, std::memory_order_relaxed);
        g_copyEventCount.store(0, std::memory_order_relaxed);
        g_copyCallbackReached.store(false, std::memory_order_relaxed);
        g_copyRecorded.store(false, std::memory_order_relaxed);
        g_signalAttempted.store(false, std::memory_order_relaxed);
        g_signalSucceeded.store(false, std::memory_order_relaxed);
        g_signalResult.store(kNotAttempted, std::memory_order_relaxed);
        g_nextSignalValue.store(0, std::memory_order_relaxed);
        g_submittedSignalValue.store(0, std::memory_order_relaxed);
        g_mapAttempted.store(false, std::memory_order_relaxed);
        g_mapSucceeded.store(false, std::memory_order_relaxed);
        g_validationCompleted.store(false, std::memory_order_relaxed);
        g_expectedOrientation.store(false, std::memory_order_relaxed);
        g_flippedOrientation.store(false, std::memory_order_relaxed);
        g_failureStage.store(0, std::memory_order_relaxed);
        g_topLeft.store(0, std::memory_order_relaxed);
        g_topRight.store(0, std::memory_order_relaxed);
        g_bottomLeft.store(0, std::memory_order_relaxed);
        g_bottomRight.store(0, std::memory_order_relaxed);
    }

    void HandleReadbackCopyEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device)
    {
        g_copyEventCount.fetch_add(1, std::memory_order_relaxed);
        g_copyCallbackReached.store(true, std::memory_order_relaxed);
        if (g_copyRecorded.load(std::memory_order_acquire))
        {
            Fail(ReadbackFailureStage::AlreadyExecuted);
            return;
        }

        auto* destination = GetDestinationTextureForRenderThread();
        if (destination == nullptr)
        {
            Fail(ReadbackFailureStage::DestinationUnavailable);
            return;
        }
        if (device == nullptr || d3d12 == nullptr)
        {
            Fail(ReadbackFailureStage::ReadbackBufferCreationFailed);
            return;
        }

        std::lock_guard lock(g_mutex);
        const auto textureDescription = destination->GetDesc();
        UINT numRows = 0;
        UINT64 rowSize = 0;
        UINT64 totalBytes = 0;
        device->GetCopyableFootprints(
            &textureDescription,
            0,
            1,
            0,
            &g_footprint,
            &numRows,
            &rowSize,
            &totalBytes);
        g_numRows.store(numRows, std::memory_order_relaxed);
        g_rowSize.store(rowSize, std::memory_order_relaxed);
        g_totalBytes.store(totalBytes, std::memory_order_relaxed);
        if (numRows == 0 || rowSize == 0 || totalBytes == 0
            || g_footprint.Footprint.RowPitch < rowSize)
        {
            Fail(ReadbackFailureStage::InvalidFootprint);
            return;
        }

        const D3D12_HEAP_PROPERTIES heapProperties{
            D3D12_HEAP_TYPE_READBACK,
            D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
            D3D12_MEMORY_POOL_UNKNOWN,
            1,
            1};
        const D3D12_RESOURCE_DESC bufferDescription{
            D3D12_RESOURCE_DIMENSION_BUFFER,
            0,
            totalBytes,
            1,
            1,
            1,
            DXGI_FORMAT_UNKNOWN,
            {1, 0},
            D3D12_TEXTURE_LAYOUT_ROW_MAJOR,
            D3D12_RESOURCE_FLAG_NONE};

        HRESULT result = device->CreateCommittedResource(
            &heapProperties,
            D3D12_HEAP_FLAG_NONE,
            &bufferDescription,
            D3D12_RESOURCE_STATE_COPY_DEST,
            nullptr,
            IID_PPV_ARGS(g_readbackBuffer.ReleaseAndGetAddressOf()));
        g_creationResult.store(
            static_cast<std::int32_t>(result),
            std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(ReadbackFailureStage::ReadbackBufferCreationFailed);
            return;
        }

        result = device->CreateFence(
            0,
            D3D12_FENCE_FLAG_NONE,
            IID_PPV_ARGS(g_fence.ReleaseAndGetAddressOf()));
        if (FAILED(result))
        {
            Fail(ReadbackFailureStage::FenceCreationFailed);
            return;
        }

        UnityGraphicsD3D12RecordingState recordingState{};
        if (!d3d12->CommandRecordingState(&recordingState))
        {
            Fail(ReadbackFailureStage::CommandRecordingStateUnavailable);
            return;
        }
        auto* commandList = recordingState.commandList;
        if (commandList == nullptr)
        {
            Fail(ReadbackFailureStage::CommandListUnavailable);
            return;
        }

        auto toCopySource = Transition(
            destination,
            D3D12_RESOURCE_STATE_COPY_DEST,
            D3D12_RESOURCE_STATE_COPY_SOURCE);
        commandList->ResourceBarrier(1, &toCopySource);

        D3D12_TEXTURE_COPY_LOCATION readbackLocation{};
        readbackLocation.pResource = g_readbackBuffer.Get();
        readbackLocation.Type =
            D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;
        readbackLocation.PlacedFootprint = g_footprint;

        D3D12_TEXTURE_COPY_LOCATION textureLocation{};
        textureLocation.pResource = destination;
        textureLocation.Type =
            D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
        textureLocation.SubresourceIndex = 0;
        commandList->CopyTextureRegion(
            &readbackLocation,
            0,
            0,
            0,
            &textureLocation,
            nullptr);

        auto toCopyDestination = Transition(
            destination,
            D3D12_RESOURCE_STATE_COPY_SOURCE,
            D3D12_RESOURCE_STATE_COPY_DEST);
        commandList->ResourceBarrier(1, &toCopyDestination);
        g_copyRecorded.store(true, std::memory_order_release);
    }

    void HandleReadbackFenceSignalEvent(IUnityGraphicsD3D12v8* d3d12)
    {
        if (g_signalAttempted.exchange(true, std::memory_order_acq_rel))
        {
            Fail(ReadbackFailureStage::AlreadyExecuted);
            return;
        }
        if (!g_copyRecorded.load(std::memory_order_acquire) || d3d12 == nullptr)
        {
            Fail(ReadbackFailureStage::QueueUnavailable);
            return;
        }

        std::lock_guard lock(g_mutex);
        auto* queue = d3d12->GetCommandQueue();
        if (queue == nullptr || g_fence == nullptr)
        {
            Fail(ReadbackFailureStage::QueueUnavailable);
            return;
        }

        const auto value =
            g_nextSignalValue.fetch_add(1, std::memory_order_relaxed) + 1;
        const HRESULT result = queue->Signal(g_fence.Get(), value);
        g_signalResult.store(
            static_cast<std::int32_t>(result),
            std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(ReadbackFailureStage::FenceSignalFailed);
            return;
        }
        g_submittedSignalValue.store(value, std::memory_order_release);
        g_signalSucceeded.store(true, std::memory_order_release);
    }

    bool IsReadbackBufferAvailable()
    {
        std::lock_guard lock(g_mutex);
        return g_readbackBuffer != nullptr;
    }

    std::int32_t GetReadbackBufferCreationResult()
    {
        return g_creationResult.load(std::memory_order_relaxed);
    }

    std::uint64_t GetReadbackFootprintOffset()
    {
        std::lock_guard lock(g_mutex);
        return g_footprint.Offset;
    }

    std::uint32_t GetReadbackFootprintRowPitch()
    {
        std::lock_guard lock(g_mutex);
        return g_footprint.Footprint.RowPitch;
    }

    std::uint32_t GetReadbackNumRows()
    {
        return g_numRows.load(std::memory_order_relaxed);
    }

    std::uint64_t GetReadbackRowSizeInBytes()
    {
        return g_rowSize.load(std::memory_order_relaxed);
    }

    std::uint64_t GetReadbackTotalBytes()
    {
        return g_totalBytes.load(std::memory_order_relaxed);
    }

    std::int32_t GetReadbackCopyEventCount()
    {
        return g_copyEventCount.load(std::memory_order_relaxed);
    }

    bool WasReadbackCopyCallbackReached()
    {
        return g_copyCallbackReached.load(std::memory_order_relaxed);
    }

    bool WasReadbackCopyRecorded()
    {
        return g_copyRecorded.load(std::memory_order_acquire);
    }

    bool IsReadbackFenceAvailable()
    {
        std::lock_guard lock(g_mutex);
        return g_fence != nullptr;
    }

    bool WasReadbackFenceSignalAttempted()
    {
        return g_signalAttempted.load(std::memory_order_relaxed);
    }

    bool DidReadbackFenceSignalSucceed()
    {
        return g_signalSucceeded.load(std::memory_order_acquire);
    }

    std::int32_t GetReadbackFenceSignalResult()
    {
        return g_signalResult.load(std::memory_order_relaxed);
    }

    std::uint64_t GetReadbackFenceSubmittedValue()
    {
        return g_submittedSignalValue.load(std::memory_order_acquire);
    }

    std::uint64_t GetReadbackFenceCompletedValue()
    {
        std::lock_guard lock(g_mutex);
        return g_fence != nullptr ? g_fence->GetCompletedValue() : 0;
    }

    bool IsReadbackFenceComplete()
    {
        const auto submitted =
            g_submittedSignalValue.load(std::memory_order_acquire);
        return submitted != 0 && GetReadbackFenceCompletedValue() >= submitted;
    }

    std::int32_t ValidateReadbackPixels()
    {
        if (g_validationCompleted.load(std::memory_order_acquire))
        {
            return
                (g_expectedOrientation.load(std::memory_order_relaxed)
                    || g_flippedOrientation.load(std::memory_order_relaxed))
                ? 1
                : 0;
        }
        if (!IsReadbackFenceComplete())
        {
            return -1;
        }

        std::lock_guard lock(g_mutex);
        g_mapAttempted.store(true, std::memory_order_relaxed);
        void* mapped = nullptr;
        const D3D12_RANGE readRange{
            static_cast<SIZE_T>(g_footprint.Offset),
            static_cast<SIZE_T>(
                g_footprint.Offset
                + g_totalBytes.load(std::memory_order_relaxed))};
        const HRESULT result = g_readbackBuffer->Map(0, &readRange, &mapped);
        if (FAILED(result) || mapped == nullptr)
        {
            Fail(ReadbackFailureStage::MapFailed);
            return 0;
        }
        g_mapSucceeded.store(true, std::memory_order_relaxed);

        const auto* bytes = static_cast<const std::uint8_t*>(mapped);
        const auto topLeft = ReadBgra(bytes, 8, 8);
        const auto topRight = ReadBgra(bytes, 56, 8);
        const auto bottomLeft = ReadBgra(bytes, 8, 56);
        const auto bottomRight = ReadBgra(bytes, 56, 56);
        g_topLeft.store(topLeft, std::memory_order_relaxed);
        g_topRight.store(topRight, std::memory_order_relaxed);
        g_bottomLeft.store(bottomLeft, std::memory_order_relaxed);
        g_bottomRight.store(bottomRight, std::memory_order_relaxed);
        const D3D12_RANGE writtenRange{0, 0};
        g_readbackBuffer->Unmap(0, &writtenRange);

        constexpr std::uint32_t red = 0xFFFF0000;
        constexpr std::uint32_t green = 0xFF00FF00;
        constexpr std::uint32_t blue = 0xFF0000FF;
        constexpr std::uint32_t white = 0xFFFFFFFF;
        const bool expected =
            PixelNear(topLeft, red)
            && PixelNear(topRight, green)
            && PixelNear(bottomLeft, blue)
            && PixelNear(bottomRight, white);
        const bool flipped =
            PixelNear(topLeft, blue)
            && PixelNear(topRight, white)
            && PixelNear(bottomLeft, red)
            && PixelNear(bottomRight, green);
        g_expectedOrientation.store(expected, std::memory_order_relaxed);
        g_flippedOrientation.store(flipped, std::memory_order_relaxed);
        g_validationCompleted.store(true, std::memory_order_release);
        if (!expected && !flipped)
        {
            Fail(ReadbackFailureStage::PixelMismatch);
            return 0;
        }

        g_failureStage.store(0, std::memory_order_release);
        ::OutputDebugStringW(
            expected
                ? L"[DesktopMascotNative] Readback pixel validation succeeded.\n"
                : L"[DesktopMascotNative] Readback pixel validation succeeded with vertical flip.\n");
        return 1;
    }

    bool WasReadbackMapAttempted() { return g_mapAttempted.load(); }
    bool DidReadbackMapSucceed() { return g_mapSucceeded.load(); }
    bool WasReadbackValidationCompleted() { return g_validationCompleted.load(); }
    bool DidReadbackExpectedOrientationMatch() { return g_expectedOrientation.load(); }
    bool DidReadbackVerticallyFlippedOrientationMatch() { return g_flippedOrientation.load(); }
    std::int32_t GetReadbackFailureStage() { return g_failureStage.load(); }
    std::uint32_t GetReadbackTopLeftBgra() { return g_topLeft.load(); }
    std::uint32_t GetReadbackTopRightBgra() { return g_topRight.load(); }
    std::uint32_t GetReadbackBottomLeftBgra() { return g_bottomLeft.load(); }
    std::uint32_t GetReadbackBottomRightBgra() { return g_bottomRight.load(); }
}
