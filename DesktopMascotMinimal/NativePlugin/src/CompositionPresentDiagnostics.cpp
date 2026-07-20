#include "DesktopMascotNative/CompositionPresentDiagnostics.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/CopyResourceDiagnostics.h"
#include "DesktopMascotNative/DestinationTexture.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wrl/client.h>

#include <atomic>
#include <cwchar>
#include <mutex>

#include "IUnityGraphicsD3D12.h"

namespace
{
    using DesktopMascotNative::CompositionCommandSubmissionMode;
    using DesktopMascotNative::CompositionPresentFailureStage;
    using DesktopMascotNative::CompositionPresentState;
    using Microsoft::WRL::ComPtr;

    constexpr std::int32_t kNotAttempted = 0x7FFFFFFF;
    constexpr std::uint32_t kPresentSyncInterval = 1;
    constexpr std::uint32_t kPresentFlags = 0;
    constexpr std::uint64_t kFenceTimeoutMilliseconds = 5000;

    std::mutex g_resourceMutex;
    std::atomic<HWND> g_window{nullptr};
    IDXGISwapChain1* g_borrowedSwapChain1 = nullptr;
    ComPtr<IDXGISwapChain3> g_swapChain3;
    ComPtr<ID3D12Resource> g_backBuffer;
    ComPtr<ID3D12CommandAllocator> g_commandAllocator;
    ComPtr<ID3D12GraphicsCommandList> g_commandList;
    ID3D12Fence* g_borrowedFrameFence = nullptr;

#define DMN_BOOL(name) std::atomic<bool> name{false}
#define DMN_I32(name, value) std::atomic<std::int32_t> name{value}
    DMN_I32(g_state, 0);
    DMN_I32(g_failureStage, 0);
    DMN_I32(g_submissionMode, 0);
    DMN_BOOL(g_swapChain3QueryAttempted);
    DMN_BOOL(g_swapChain3Available);
    DMN_I32(g_swapChain3Result, kNotAttempted);
    DMN_BOOL(g_indexQueried);
    std::atomic<std::uint32_t> g_backBufferIndex{0};
    DMN_BOOL(g_indexValid);
    DMN_BOOL(g_getBufferAttempted);
    DMN_BOOL(g_backBufferAvailable);
    DMN_I32(g_getBufferResult, kNotAttempted);
    DMN_BOOL(g_backBufferDescAvailable);
    DMN_I32(g_backBufferDimension, 0);
    std::atomic<std::uint64_t> g_backBufferWidth{0};
    std::atomic<std::uint32_t> g_backBufferHeight{0};
    std::atomic<std::uint32_t> g_backBufferDepthOrArraySize{0};
    std::atomic<std::uint32_t> g_backBufferMipLevels{0};
    DMN_I32(g_backBufferFormat, 0);
    std::atomic<std::uint32_t> g_backBufferSampleCount{0};
    std::atomic<std::uint32_t> g_backBufferSampleQuality{0};
    DMN_I32(g_backBufferLayout, 0);
    std::atomic<std::uint32_t> g_backBufferFlags{0};
    DMN_BOOL(g_compatibilityChecked);
    DMN_BOOL(g_dimensionsCompatible);
    DMN_BOOL(g_samplesCompatible);
    DMN_BOOL(g_formatsCompatible);
    DMN_BOOL(g_copyEventIssued);
    DMN_I32(g_copyEventCount, 0);
    DMN_BOOL(g_copyCallbackReached);
    DMN_BOOL(g_barriersRecorded);
    DMN_BOOL(g_copyRecorded);
    DMN_BOOL(g_copySubmitted);
    DMN_BOOL(g_executeAttempted);
    DMN_BOOL(g_executeReturnedFence);
    std::atomic<std::uint64_t> g_executeFenceValue{0};
    DMN_BOOL(g_fenceCreated);
    DMN_BOOL(g_fenceAvailable);
    DMN_BOOL(g_fenceSignalAttempted);
    DMN_BOOL(g_fenceSignalSucceeded);
    DMN_I32(g_fenceSignalResult, kNotAttempted);
    std::atomic<std::uint64_t> g_fenceSubmittedValue{0};
    std::atomic<std::uint64_t> g_fenceCompletedValue{0};
    DMN_BOOL(g_fenceComplete);
    DMN_BOOL(g_fenceTimeout);
    std::atomic<std::uint64_t> g_submissionTick{0};
    DMN_BOOL(g_presentPostAttempted);
    DMN_BOOL(g_presentPostSucceeded);
    std::atomic<std::uint32_t> g_presentPostLastError{0};
    DMN_BOOL(g_presentMessageReceived);
    DMN_BOOL(g_presentAttempted);
    DMN_BOOL(g_presentSucceeded);
    DMN_I32(g_presentResult, kNotAttempted);
    std::atomic<std::uint32_t> g_presentSyncInterval{0};
    std::atomic<std::uint32_t> g_presentFlags{0};
    DMN_I32(g_presentCount, 0);
    DMN_BOOL(g_postPresentCommitAttempted);
    DMN_BOOL(g_postPresentCommitSucceeded);
    DMN_I32(g_postPresentCommitResult, kNotAttempted);
    DMN_BOOL(g_shutdownRequested);
#undef DMN_BOOL
#undef DMN_I32

    void StoreState(CompositionPresentState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    void Fail(CompositionPresentFailureStage stage)
    {
        g_failureStage.store(
            static_cast<std::int32_t>(stage),
            std::memory_order_release);
        StoreState(CompositionPresentState::Failed);
        wchar_t message[160]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Composition Present diagnostics failed: "
            L"stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    bool AreCopyFormatsCompatible(
        DXGI_FORMAT destination,
        DXGI_FORMAT source)
    {
        if (destination == source)
        {
            return true;
        }

        // Both formats are members of the
        // DXGI_FORMAT_B8G8R8A8_TYPELESS family. CopyResource performs a
        // bit-preserving copy; it does not perform an sRGB conversion.
        return
            (destination == DXGI_FORMAT_B8G8R8A8_UNORM
                && source == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)
            || (destination == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB
                && source == DXGI_FORMAT_B8G8R8A8_UNORM);
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

    bool WaitForSubmittedGpuWork(std::uint32_t timeoutMilliseconds)
    {
        if (!g_copySubmitted.load(std::memory_order_acquire)
            || g_fenceComplete.load(std::memory_order_acquire))
        {
            return true;
        }

        ID3D12Fence* fence = nullptr;
        std::uint64_t value = 0;
        {
            std::lock_guard lock(g_resourceMutex);
            fence = g_borrowedFrameFence;
            value = g_fenceSubmittedValue.load(std::memory_order_acquire);
        }
        if (fence == nullptr || value == 0)
        {
            return false;
        }

        HANDLE completionEvent = ::CreateEventW(nullptr, FALSE, FALSE, nullptr);
        if (completionEvent == nullptr)
        {
            return false;
        }
        const HRESULT result = fence->SetEventOnCompletion(value, completionEvent);
        if (FAILED(result))
        {
            ::CloseHandle(completionEvent);
            return false;
        }
        const DWORD waitResult =
            ::WaitForSingleObject(completionEvent, timeoutMilliseconds);
        ::CloseHandle(completionEvent);
        if (waitResult != WAIT_OBJECT_0)
        {
            return false;
        }

        const auto completed = fence->GetCompletedValue();
        g_fenceCompletedValue.store(completed, std::memory_order_relaxed);
        const bool complete = completed >= value;
        g_fenceComplete.store(complete, std::memory_order_release);
        return complete;
    }
}

namespace DesktopMascotNative
{
    void ResetCompositionPresentDiagnostics()
    {
        std::lock_guard lock(g_resourceMutex);
        g_window.store(nullptr, std::memory_order_relaxed);
        g_borrowedSwapChain1 = nullptr;
        g_swapChain3.Reset();
        g_backBuffer.Reset();
        g_commandAllocator.Reset();
        g_commandList.Reset();
        g_borrowedFrameFence = nullptr;
#define RESET_BOOL(name) name.store(false, std::memory_order_relaxed)
#define RESET_I32(name, value) name.store(value, std::memory_order_relaxed)
        RESET_I32(g_state, 0);
        RESET_I32(g_failureStage, 0);
        RESET_I32(g_submissionMode, 0);
        RESET_BOOL(g_swapChain3QueryAttempted);
        RESET_BOOL(g_swapChain3Available);
        RESET_I32(g_swapChain3Result, kNotAttempted);
        RESET_BOOL(g_indexQueried);
        g_backBufferIndex.store(0);
        RESET_BOOL(g_indexValid);
        RESET_BOOL(g_getBufferAttempted);
        RESET_BOOL(g_backBufferAvailable);
        RESET_I32(g_getBufferResult, kNotAttempted);
        RESET_BOOL(g_backBufferDescAvailable);
        RESET_I32(g_backBufferDimension, 0);
        g_backBufferWidth.store(0);
        g_backBufferHeight.store(0);
        g_backBufferDepthOrArraySize.store(0);
        g_backBufferMipLevels.store(0);
        RESET_I32(g_backBufferFormat, 0);
        g_backBufferSampleCount.store(0);
        g_backBufferSampleQuality.store(0);
        RESET_I32(g_backBufferLayout, 0);
        g_backBufferFlags.store(0);
        RESET_BOOL(g_compatibilityChecked);
        RESET_BOOL(g_dimensionsCompatible);
        RESET_BOOL(g_samplesCompatible);
        RESET_BOOL(g_formatsCompatible);
        RESET_BOOL(g_copyEventIssued);
        RESET_I32(g_copyEventCount, 0);
        RESET_BOOL(g_copyCallbackReached);
        RESET_BOOL(g_barriersRecorded);
        RESET_BOOL(g_copyRecorded);
        RESET_BOOL(g_copySubmitted);
        RESET_BOOL(g_executeAttempted);
        RESET_BOOL(g_executeReturnedFence);
        g_executeFenceValue.store(0);
        RESET_BOOL(g_fenceCreated);
        RESET_BOOL(g_fenceAvailable);
        RESET_BOOL(g_fenceSignalAttempted);
        RESET_BOOL(g_fenceSignalSucceeded);
        RESET_I32(g_fenceSignalResult, kNotAttempted);
        g_fenceSubmittedValue.store(0);
        g_fenceCompletedValue.store(0);
        RESET_BOOL(g_fenceComplete);
        RESET_BOOL(g_fenceTimeout);
        g_submissionTick.store(0);
        RESET_BOOL(g_presentPostAttempted);
        RESET_BOOL(g_presentPostSucceeded);
        g_presentPostLastError.store(0);
        RESET_BOOL(g_presentMessageReceived);
        RESET_BOOL(g_presentAttempted);
        RESET_BOOL(g_presentSucceeded);
        RESET_I32(g_presentResult, kNotAttempted);
        g_presentSyncInterval.store(0);
        g_presentFlags.store(0);
        RESET_I32(g_presentCount, 0);
        RESET_BOOL(g_postPresentCommitAttempted);
        RESET_BOOL(g_postPresentCommitSucceeded);
        RESET_I32(g_postPresentCommitResult, kNotAttempted);
        RESET_BOOL(g_shutdownRequested);
#undef RESET_BOOL
#undef RESET_I32
    }

    void SetCompositionPresentUiObjects(
        void* window,
        IDXGISwapChain1* swapChain)
    {
        std::lock_guard lock(g_resourceMutex);
        g_window.store(static_cast<HWND>(window), std::memory_order_release);
        g_borrowedSwapChain1 = swapChain;
    }

    std::int32_t StartCompositionPresentDiagnostics()
    {
        std::int32_t expected =
            static_cast<std::int32_t>(CompositionPresentState::NotStarted);
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    CompositionPresentState::
                        WaitingForCompositionInitialization),
                std::memory_order_acq_rel))
        {
            return 0;
        }

        if (GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning))
        {
            Fail(CompositionPresentFailureStage::CompositionNotReady);
            return -1;
        }

        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr
            || !::PostMessageW(
                window,
                kCompositionPresentInitializeMessage,
                0,
                0))
        {
            Fail(CompositionPresentFailureStage::CompositionNotReady);
            return -2;
        }
        return 1;
    }

    bool MarkCompositionCopyEventIssued()
    {
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                CompositionPresentState::WaitingForCopyEvent))
        {
            return false;
        }
        bool expected = false;
        return g_copyEventIssued.compare_exchange_strong(
            expected,
            true,
            std::memory_order_acq_rel);
    }

    void HandleCompositionPresentInitializeMessage()
    {
        std::lock_guard lock(g_resourceMutex);
        if (g_borrowedSwapChain1 == nullptr)
        {
            Fail(CompositionPresentFailureStage::CompositionNotReady);
            return;
        }

        g_swapChain3QueryAttempted.store(true, std::memory_order_relaxed);
        HRESULT result = g_borrowedSwapChain1->QueryInterface(
            IID_PPV_ARGS(g_swapChain3.ReleaseAndGetAddressOf()));
        g_swapChain3Result.store(
            static_cast<std::int32_t>(result),
            std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionPresentFailureStage::SwapChain3QueryFailed);
            return;
        }
        g_swapChain3Available.store(true, std::memory_order_release);
        StoreState(CompositionPresentState::SwapChain3Acquired);

        g_indexQueried.store(true, std::memory_order_relaxed);
        const UINT index = g_swapChain3->GetCurrentBackBufferIndex();
        g_backBufferIndex.store(index, std::memory_order_relaxed);
        const bool indexValid = index < 2;
        g_indexValid.store(indexValid, std::memory_order_relaxed);
        if (!indexValid)
        {
            Fail(CompositionPresentFailureStage::InvalidBackBufferIndex);
            return;
        }

        g_getBufferAttempted.store(true, std::memory_order_relaxed);
        result = g_swapChain3->GetBuffer(
            index,
            IID_PPV_ARGS(g_backBuffer.ReleaseAndGetAddressOf()));
        g_getBufferResult.store(
            static_cast<std::int32_t>(result),
            std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionPresentFailureStage::BackBufferGetFailed);
            return;
        }
        g_backBufferAvailable.store(true, std::memory_order_release);
        StoreState(CompositionPresentState::BackBufferAcquired);

        const auto backBufferDescription = g_backBuffer->GetDesc();
        g_backBufferDimension.store(backBufferDescription.Dimension);
        g_backBufferWidth.store(backBufferDescription.Width);
        g_backBufferHeight.store(backBufferDescription.Height);
        g_backBufferDepthOrArraySize.store(
            backBufferDescription.DepthOrArraySize);
        g_backBufferMipLevels.store(backBufferDescription.MipLevels);
        g_backBufferFormat.store(backBufferDescription.Format);
        g_backBufferSampleCount.store(backBufferDescription.SampleDesc.Count);
        g_backBufferSampleQuality.store(
            backBufferDescription.SampleDesc.Quality);
        g_backBufferLayout.store(backBufferDescription.Layout);
        g_backBufferFlags.store(backBufferDescription.Flags);
        g_backBufferDescAvailable.store(true, std::memory_order_release);

        if (backBufferDescription.Dimension
                != D3D12_RESOURCE_DIMENSION_TEXTURE2D
            || backBufferDescription.Width != 64
            || backBufferDescription.Height != 64
            || backBufferDescription.DepthOrArraySize != 1
            || backBufferDescription.MipLevels != 1
            || backBufferDescription.Format
                != DXGI_FORMAT_B8G8R8A8_UNORM
            || backBufferDescription.SampleDesc.Count != 1
            || backBufferDescription.SampleDesc.Quality != 0)
        {
            Fail(CompositionPresentFailureStage::BackBufferDescInvalid);
            return;
        }

        if (!IsDestinationTextureAvailable())
        {
            Fail(CompositionPresentFailureStage::DestinationUnavailable);
            return;
        }
        if (GetDestinationTextureDimension()
                != D3D12_RESOURCE_DIMENSION_TEXTURE2D
            || GetDestinationTextureDepthOrArraySize() != 1
            || GetDestinationTextureMipLevels() != 1)
        {
            Fail(CompositionPresentFailureStage::DestinationDescInvalid);
            return;
        }

        const bool dimensions =
            GetDestinationTextureWidth() == backBufferDescription.Width
            && GetDestinationTextureHeight() == backBufferDescription.Height
            && GetDestinationTextureDepthOrArraySize()
                == backBufferDescription.DepthOrArraySize
            && GetDestinationTextureMipLevels()
                == backBufferDescription.MipLevels;
        const bool samples =
            GetDestinationTextureSampleCount()
                == backBufferDescription.SampleDesc.Count
            && GetDestinationTextureSampleQuality()
                == backBufferDescription.SampleDesc.Quality;
        const bool formats = AreCopyFormatsCompatible(
            backBufferDescription.Format,
            static_cast<DXGI_FORMAT>(GetDestinationTextureFormat()));
        g_dimensionsCompatible.store(dimensions);
        g_samplesCompatible.store(samples);
        g_formatsCompatible.store(formats);
        g_compatibilityChecked.store(true, std::memory_order_release);
        if (!dimensions)
        {
            Fail(CompositionPresentFailureStage::IncompatibleDimensions);
            return;
        }
        if (!samples)
        {
            Fail(
                CompositionPresentFailureStage::
                    IncompatibleSampleConfiguration);
            return;
        }
        if (!formats)
        {
            Fail(CompositionPresentFailureStage::IncompatibleCopyFormats);
            return;
        }

        StoreState(CompositionPresentState::ResourcesValidated);
        StoreState(CompositionPresentState::WaitingForCopyEvent);
    }

    void HandleCompositionPresentCopyEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device)
    {
        g_copyEventCount.fetch_add(1, std::memory_order_relaxed);
        g_copyCallbackReached.store(true, std::memory_order_release);
        if (g_copyEventCount.load(std::memory_order_relaxed) != 1
            || !g_copyEventIssued.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            Fail(CompositionPresentFailureStage::UnexpectedSecondPresent);
            return;
        }
        if (d3d12 == nullptr || device == nullptr)
        {
            Fail(CompositionPresentFailureStage::CompositionNotReady);
            return;
        }

        std::lock_guard lock(g_resourceMutex);
        auto* destination = GetDestinationTextureForRenderThread();
        if (destination == nullptr || g_backBuffer == nullptr)
        {
            Fail(CompositionPresentFailureStage::DestinationUnavailable);
            return;
        }

        HRESULT result = device->CreateCommandAllocator(
            D3D12_COMMAND_LIST_TYPE_DIRECT,
            IID_PPV_ARGS(g_commandAllocator.ReleaseAndGetAddressOf()));
        if (FAILED(result))
        {
            Fail(
                CompositionPresentFailureStage::
                    CommandAllocatorCreationFailed);
            return;
        }
        result = device->CreateCommandList(
            0,
            D3D12_COMMAND_LIST_TYPE_DIRECT,
            g_commandAllocator.Get(),
            nullptr,
            IID_PPV_ARGS(g_commandList.ReleaseAndGetAddressOf()));
        if (FAILED(result))
        {
            Fail(
                CompositionPresentFailureStage::CommandListCreationFailed);
            return;
        }
        result = g_commandList->Close();
        if (FAILED(result))
        {
            Fail(CompositionPresentFailureStage::CommandListCloseFailed);
            return;
        }
        result = g_commandAllocator->Reset();
        if (FAILED(result))
        {
            Fail(
                CompositionPresentFailureStage::
                    CommandAllocatorResetFailed);
            return;
        }
        result = g_commandList->Reset(g_commandAllocator.Get(), nullptr);
        if (FAILED(result))
        {
            Fail(CompositionPresentFailureStage::CommandListResetFailed);
            return;
        }

        const auto destinationTrackedState =
            static_cast<D3D12_RESOURCE_STATES>(
                GetCopyDestinationTrackedState());
        D3D12_RESOURCE_BARRIER beforeCopy[]{
            Transition(
                destination,
                destinationTrackedState,
                D3D12_RESOURCE_STATE_COPY_SOURCE),
            Transition(
                g_backBuffer.Get(),
                D3D12_RESOURCE_STATE_PRESENT,
                D3D12_RESOURCE_STATE_COPY_DEST)};
        g_commandList->ResourceBarrier(2, beforeCopy);
        g_barriersRecorded.store(true, std::memory_order_release);

        g_commandList->CopyResource(g_backBuffer.Get(), destination);
        g_copyRecorded.store(true, std::memory_order_release);

        D3D12_RESOURCE_BARRIER afterCopy[]{
            Transition(
                g_backBuffer.Get(),
                D3D12_RESOURCE_STATE_COPY_DEST,
                D3D12_RESOURCE_STATE_PRESENT),
            Transition(
                destination,
                D3D12_RESOURCE_STATE_COPY_SOURCE,
                destinationTrackedState)};
        g_commandList->ResourceBarrier(2, afterCopy);
        result = g_commandList->Close();
        if (FAILED(result))
        {
            Fail(CompositionPresentFailureStage::CommandListCloseFailed);
            return;
        }
        StoreState(CompositionPresentState::CopyCommandsRecorded);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition back-buffer copy recorded.\n");

        g_borrowedFrameFence = d3d12->GetFrameFence();
        g_fenceAvailable.store(
            g_borrowedFrameFence != nullptr,
            std::memory_order_release);
        if (g_borrowedFrameFence == nullptr)
        {
            Fail(CompositionPresentFailureStage::FenceCreationFailed);
            return;
        }

        UnityGraphicsD3D12ResourceState resourceStates[]{
            {destination, destinationTrackedState, destinationTrackedState},
            {g_backBuffer.Get(),
             D3D12_RESOURCE_STATE_PRESENT,
             D3D12_RESOURCE_STATE_PRESENT}};
        g_submissionMode.store(
            static_cast<std::int32_t>(
                CompositionCommandSubmissionMode::UnityOfficialExecuteApi),
            std::memory_order_release);
        g_executeAttempted.store(true, std::memory_order_relaxed);
        const UINT64 fenceValue =
            d3d12->ExecuteCommandList(g_commandList.Get(), 2, resourceStates);
        g_executeFenceValue.store(fenceValue, std::memory_order_relaxed);
        g_fenceSubmittedValue.store(fenceValue, std::memory_order_relaxed);
        if (fenceValue == 0)
        {
            Fail(CompositionPresentFailureStage::CommandSubmissionFailed);
            return;
        }
        g_executeReturnedFence.store(true, std::memory_order_release);
        g_copySubmitted.store(true, std::memory_order_release);
        g_submissionTick.store(::GetTickCount64(), std::memory_order_relaxed);
        StoreState(CompositionPresentState::CopySubmitted);
        StoreState(CompositionPresentState::WaitingForFence);
    }

    void PollCompositionPresentDiagnostics()
    {
        if (!g_copySubmitted.load(std::memory_order_acquire)
            || g_fenceComplete.load(std::memory_order_acquire)
            || g_state.load(std::memory_order_acquire)
                == static_cast<std::int32_t>(
                    CompositionPresentState::Failed))
        {
            return;
        }

        ID3D12Fence* fence = nullptr;
        {
            std::lock_guard lock(g_resourceMutex);
            fence = g_borrowedFrameFence;
        }
        const auto submitted =
            g_fenceSubmittedValue.load(std::memory_order_acquire);
        if (fence == nullptr || submitted == 0)
        {
            Fail(CompositionPresentFailureStage::CommandSubmissionFailed);
            return;
        }

        const auto completed = fence->GetCompletedValue();
        g_fenceCompletedValue.store(completed, std::memory_order_relaxed);
        if (completed < submitted)
        {
            const auto elapsed =
                ::GetTickCount64()
                - g_submissionTick.load(std::memory_order_relaxed);
            if (elapsed > kFenceTimeoutMilliseconds)
            {
                g_fenceTimeout.store(true, std::memory_order_release);
                Fail(CompositionPresentFailureStage::FenceTimeout);
            }
            return;
        }

        g_fenceComplete.store(true, std::memory_order_release);
        StoreState(CompositionPresentState::FenceCompleted);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition present fence completed.\n");

        bool expected = false;
        if (!g_presentPostAttempted.compare_exchange_strong(
                expected,
                true,
                std::memory_order_acq_rel))
        {
            return;
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        const bool posted =
            window != nullptr
            && ::PostMessageW(
                window,
                kCompositionPresentOnceMessage,
                0,
                0) != FALSE;
        g_presentPostSucceeded.store(posted, std::memory_order_release);
        if (!posted)
        {
            g_presentPostLastError.store(::GetLastError());
            Fail(CompositionPresentFailureStage::PresentMessagePostFailed);
            return;
        }
        StoreState(CompositionPresentState::PresentMessagePosted);
    }

    void HandleCompositionPresentMessage()
    {
        g_presentMessageReceived.store(true, std::memory_order_release);
        StoreState(CompositionPresentState::PresentMessageReceived);
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            Fail(CompositionPresentFailureStage::ShutdownBeforePresent);
            return;
        }
        if (g_presentAttempted.exchange(true, std::memory_order_acq_rel))
        {
            Fail(CompositionPresentFailureStage::UnexpectedSecondPresent);
            return;
        }

        std::lock_guard lock(g_resourceMutex);
        if (g_swapChain3 == nullptr)
        {
            Fail(CompositionPresentFailureStage::CompositionNotReady);
            return;
        }
        g_presentSyncInterval.store(kPresentSyncInterval);
        g_presentFlags.store(kPresentFlags);
        const HRESULT result =
            g_swapChain3->Present(kPresentSyncInterval, kPresentFlags);
        g_presentResult.store(static_cast<std::int32_t>(result));
        g_presentCount.fetch_add(1, std::memory_order_relaxed);
        if (result != S_OK)
        {
            Fail(CompositionPresentFailureStage::PresentFailed);
            return;
        }
        g_presentSucceeded.store(true, std::memory_order_release);
        StoreState(CompositionPresentState::PresentSucceeded);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Composition Present succeeded.\n");
    }

    void MarkCompositionDisplayHolding()
    {
        if (g_presentSucceeded.load(std::memory_order_acquire))
        {
            StoreState(CompositionPresentState::DisplayHolding);
        }
    }

    void NotifyCompositionPresentShutdownRequested()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        const auto state = g_state.load(std::memory_order_acquire);
        if (state != static_cast<std::int32_t>(CompositionPresentState::Failed)
            && state != static_cast<std::int32_t>(
                CompositionPresentState::NotStarted))
        {
            StoreState(CompositionPresentState::ShutdownRequested);
        }
    }

    bool ShutdownCompositionPresentDiagnosticsOnUiThread(
        std::uint32_t timeoutMilliseconds)
    {
        NotifyCompositionPresentShutdownRequested();
        if (!WaitForSubmittedGpuWork(timeoutMilliseconds))
        {
            g_fenceTimeout.store(true, std::memory_order_release);
            Fail(CompositionPresentFailureStage::ShutdownFailed);
            return false;
        }

        std::lock_guard lock(g_resourceMutex);
        g_commandList.Reset();
        g_commandAllocator.Reset();
        g_backBufferAvailable.store(false, std::memory_order_release);
        g_backBuffer.Reset();
        g_swapChain3Available.store(false, std::memory_order_release);
        g_swapChain3.Reset();
        g_borrowedFrameFence = nullptr;
        g_borrowedSwapChain1 = nullptr;
        g_window.store(nullptr, std::memory_order_release);
        if (g_failureStage.load(std::memory_order_acquire)
            == static_cast<std::int32_t>(
                CompositionPresentFailureStage::None))
        {
            StoreState(CompositionPresentState::Stopped);
        }
        return true;
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionPresentState, g_state)
    GET_I32(GetCompositionPresentFailureStage, g_failureStage)
    GET_I32(GetCompositionCommandSubmissionMode, g_submissionMode)
    GET_BOOL(WasSwapChain3QueryAttempted, g_swapChain3QueryAttempted)
    GET_BOOL(IsSwapChain3Available, g_swapChain3Available)
    GET_I32(GetSwapChain3QueryResult, g_swapChain3Result)
    GET_BOOL(WasBackBufferIndexQueried, g_indexQueried)
    GET_U32(GetCurrentBackBufferIndex, g_backBufferIndex)
    GET_BOOL(IsCurrentBackBufferIndexValid, g_indexValid)
    GET_BOOL(WasBackBufferGetAttempted, g_getBufferAttempted)
    GET_BOOL(IsBackBufferAvailable, g_backBufferAvailable)
    GET_I32(GetBackBufferGetResult, g_getBufferResult)
    GET_BOOL(IsBackBufferDescriptionAvailable, g_backBufferDescAvailable)
    GET_I32(GetBackBufferDimension, g_backBufferDimension)
    GET_U64(GetBackBufferWidth, g_backBufferWidth)
    GET_U32(GetBackBufferHeight, g_backBufferHeight)
    GET_U32(GetBackBufferDepthOrArraySize, g_backBufferDepthOrArraySize)
    GET_U32(GetBackBufferMipLevels, g_backBufferMipLevels)
    GET_I32(GetBackBufferFormat, g_backBufferFormat)
    GET_U32(GetBackBufferSampleCount, g_backBufferSampleCount)
    GET_U32(GetBackBufferSampleQuality, g_backBufferSampleQuality)
    GET_I32(GetBackBufferLayout, g_backBufferLayout)
    GET_U32(GetBackBufferFlags, g_backBufferFlags)
    GET_BOOL(WasCompositionCopyCompatibilityChecked, g_compatibilityChecked)
    GET_BOOL(AreCompositionCopyDimensionsCompatible, g_dimensionsCompatible)
    GET_BOOL(AreCompositionCopySamplesCompatible, g_samplesCompatible)
    GET_BOOL(AreCompositionCopyFormatsCompatible, g_formatsCompatible)
    GET_BOOL(WasCompositionCopyEventIssued, g_copyEventIssued)
    GET_I32(GetCompositionCopyEventCount, g_copyEventCount)
    GET_BOOL(WasCompositionCopyCallbackReached, g_copyCallbackReached)
    GET_BOOL(WereCompositionCopyBarriersRecorded, g_barriersRecorded)
    GET_BOOL(WasCompositionBackBufferCopyRecorded, g_copyRecorded)
    GET_BOOL(WasCompositionCopySubmitted, g_copySubmitted)
    GET_BOOL(WasCompositionExecuteCommandListAttempted, g_executeAttempted)
    GET_BOOL(
        DidCompositionExecuteCommandListReturnFenceValue,
        g_executeReturnedFence)
    GET_U64(
        GetCompositionExecuteCommandListFenceValue,
        g_executeFenceValue)
    GET_BOOL(WasCompositionPresentFenceCreated, g_fenceCreated)
    GET_BOOL(IsCompositionPresentFenceAvailable, g_fenceAvailable)
    GET_BOOL(
        WasCompositionPresentFenceSignalAttempted,
        g_fenceSignalAttempted)
    GET_BOOL(
        DidCompositionPresentFenceSignalSucceed,
        g_fenceSignalSucceeded)
    GET_I32(GetCompositionPresentFenceSignalResult, g_fenceSignalResult)
    GET_U64(
        GetCompositionPresentFenceSubmittedValue,
        g_fenceSubmittedValue)
    GET_U64(
        GetCompositionPresentFenceCompletedValue,
        g_fenceCompletedValue)
    GET_BOOL(IsCompositionPresentFenceComplete, g_fenceComplete)
    GET_BOOL(DidCompositionPresentFenceTimeout, g_fenceTimeout)
    GET_BOOL(
        WasCompositionPresentMessagePostAttempted,
        g_presentPostAttempted)
    GET_BOOL(
        DidCompositionPresentMessagePostSucceed,
        g_presentPostSucceeded)
    GET_U32(
        GetCompositionPresentMessagePostLastError,
        g_presentPostLastError)
    GET_BOOL(
        WasCompositionPresentMessageReceived,
        g_presentMessageReceived)
    GET_BOOL(WasCompositionPresentAttempted, g_presentAttempted)
    GET_BOOL(DidCompositionPresentSucceed, g_presentSucceeded)
    GET_I32(GetCompositionPresentResult, g_presentResult)
    GET_U32(GetCompositionPresentSyncInterval, g_presentSyncInterval)
    GET_U32(GetCompositionPresentFlags, g_presentFlags)
    GET_I32(GetCompositionPresentCount, g_presentCount)
    GET_BOOL(
        WasPostPresentCommitAttempted,
        g_postPresentCommitAttempted)
    GET_BOOL(
        DidPostPresentCommitSucceed,
        g_postPresentCommitSucceeded)
    GET_I32(GetPostPresentCommitResult, g_postPresentCommitResult)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
