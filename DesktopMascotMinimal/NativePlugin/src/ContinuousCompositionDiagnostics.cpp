#include "DesktopMascotNative/ContinuousCompositionDiagnostics.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/CopyResourceDiagnostics.h"
#include "DesktopMascotNative/DestinationTexture.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wrl/client.h>

#include <atomic>
#include <cwchar>
#include <limits>
#include <mutex>

#include "IUnityGraphicsD3D12.h"

namespace
{
    using DesktopMascotNative::ContinuousPresentFailureStage;
    using DesktopMascotNative::ContinuousPresentState;
    using Microsoft::WRL::ComPtr;

    constexpr std::uint64_t kFenceTimeoutMilliseconds = 2000;
    constexpr std::uint64_t kOverallTimeoutMilliseconds = 50000;
    constexpr UINT kPresentSyncInterval = 1;
    constexpr UINT kPresentFlags = 0;
    constexpr UINT kBufferCount = 2;
    constexpr UINT64 kTextureWidth = 256;
    constexpr UINT kTextureHeight = 256;
    constexpr DXGI_FORMAT kSourceFormat =
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    constexpr DXGI_FORMAT kDestinationFormat =
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
    constexpr DXGI_FORMAT kBackBufferFormat =
        DXGI_FORMAT_B8G8R8A8_UNORM;

    std::mutex g_resourceMutex;
    std::atomic<HWND> g_window{nullptr};
    IDXGISwapChain1* g_borrowedSwapChain1 = nullptr;
    ComPtr<IDXGISwapChain3> g_swapChain3;
    ComPtr<ID3D12Resource> g_backBuffer;
    ComPtr<ID3D12CommandAllocator> g_commandAllocator;
    ComPtr<ID3D12GraphicsCommandList> g_commandList;
    std::atomic<ID3D12Fence*> g_borrowedFrameFence{nullptr};
    std::atomic<ID3D12Device*> g_borrowedDevice{nullptr};

    std::atomic<std::int32_t> g_state{0};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::uint32_t> g_requestedFrames{0};
    std::atomic<std::uint32_t> g_recordedFrames{0};
    std::atomic<std::uint32_t> g_submittedFrames{0};
    std::atomic<std::uint32_t> g_fenceCompletedFrames{0};
    std::atomic<std::uint32_t> g_presentCount{0};
    std::atomic<std::uint32_t> g_targetFrameCount{
        DesktopMascotNative::kContinuousCompositionTargetFrameCount};
    std::atomic<std::uint32_t> g_lastSequence{0};
    std::atomic<std::uint32_t> g_expectedPresentSequence{0};
    std::atomic<std::uint32_t> g_lastBackBufferIndex{0};
    std::atomic<std::uint32_t> g_backBuffer0UseCount{0};
    std::atomic<std::uint32_t> g_backBuffer1UseCount{0};
    std::atomic<std::uint32_t> g_invalidBackBufferIndexCount{0};
    std::atomic<std::uint64_t> g_lastFenceSubmittedValue{0};
    std::atomic<std::uint64_t> g_lastFenceCompletedValue{0};
    std::atomic<bool> g_frameInFlight{false};
    std::atomic<std::int32_t> g_lastPresentResult{
        static_cast<std::int32_t>(S_OK)};
    std::atomic<std::int32_t> g_lastDeviceRemovedReason{
        static_cast<std::int32_t>(S_OK)};
    std::atomic<bool> g_presentOcclusionReported{false};
    std::atomic<std::uint64_t> g_startTick{0};
    std::atomic<std::uint64_t> g_frameRequestTick{0};
    std::atomic<std::uint64_t> g_fenceSubmissionTick{0};
    std::atomic<std::uint64_t> g_elapsedMilliseconds{0};
    std::atomic<std::uint64_t> g_previousPresentQpc{0};
    std::atomic<std::uint64_t> g_minimumFrameMicroseconds{
        std::numeric_limits<std::uint64_t>::max()};
    std::atomic<std::uint64_t> g_maximumFrameMicroseconds{0};
    std::atomic<std::uint64_t> g_totalFrameMicroseconds{0};
    std::atomic<std::uint32_t> g_frameIntervalSampleCount{0};
    std::atomic<std::uint32_t> g_droppedScheduleCount{0};
    std::atomic<std::uint32_t> g_rejectedFrameRequestCount{0};
    std::atomic<bool> g_completedNormally{false};
    std::atomic<bool> g_overallTimeout{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<bool> g_runtimeMode{false};

    void StoreState(ContinuousPresentState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    std::uint64_t QueryPerformanceCounterValue()
    {
        LARGE_INTEGER value{};
        ::QueryPerformanceCounter(&value);
        return static_cast<std::uint64_t>(value.QuadPart);
    }

    std::uint64_t QpcDeltaMicroseconds(
        std::uint64_t earlier,
        std::uint64_t later)
    {
        LARGE_INTEGER frequency{};
        ::QueryPerformanceFrequency(&frequency);
        if (earlier == 0 || later < earlier || frequency.QuadPart <= 0)
        {
            return 0;
        }
        return ((later - earlier) * 1000000ULL)
            / static_cast<std::uint64_t>(frequency.QuadPart);
    }

    void UpdateMinimum(std::atomic<std::uint64_t>& target, std::uint64_t value)
    {
        auto current = target.load(std::memory_order_relaxed);
        while (value < current
               && !target.compare_exchange_weak(
                   current,
                   value,
                   std::memory_order_relaxed))
        {
        }
    }

    void UpdateMaximum(std::atomic<std::uint64_t>& target, std::uint64_t value)
    {
        auto current = target.load(std::memory_order_relaxed);
        while (value > current
               && !target.compare_exchange_weak(
                   current,
                   value,
                   std::memory_order_relaxed))
        {
        }
    }

    void Fail(ContinuousPresentFailureStage stage)
    {
        std::int32_t expected =
            static_cast<std::int32_t>(ContinuousPresentFailureStage::None);
        if (!g_failureStage.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(stage),
                std::memory_order_acq_rel))
        {
            return;
        }
        StoreState(ContinuousPresentState::Failed);
        wchar_t message[192]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] Continuous composition diagnostics "
            L"failed: stage=%d, sequence=%u.\n",
            static_cast<int>(stage),
            g_lastSequence.load(std::memory_order_relaxed));
        ::OutputDebugStringW(message);
    }

    bool IsTexture2D64(
        const D3D12_RESOURCE_DESC& description,
        DXGI_FORMAT format)
    {
        return description.Dimension == D3D12_RESOURCE_DIMENSION_TEXTURE2D
            && description.Width == kTextureWidth
            && description.Height == kTextureHeight
            && description.DepthOrArraySize == 1
            && description.MipLevels == 1
            && description.Format == format
            && description.SampleDesc.Count == 1
            && description.SampleDesc.Quality == 0;
    }

    D3D12_RESOURCE_BARRIER Transition(
        ID3D12Resource* resource,
        D3D12_RESOURCE_STATES before,
        D3D12_RESOURCE_STATES after)
    {
        D3D12_RESOURCE_BARRIER barrier{};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition.pResource = resource;
        barrier.Transition.Subresource =
            D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        barrier.Transition.StateBefore = before;
        barrier.Transition.StateAfter = after;
        return barrier;
    }

    bool IsTerminalState(std::int32_t state)
    {
        return state == static_cast<std::int32_t>(
                            ContinuousPresentState::Completed)
            || state == static_cast<std::int32_t>(
                            ContinuousPresentState::Stopped)
            || state == static_cast<std::int32_t>(
                            ContinuousPresentState::Failed);
    }

    bool WaitForSubmittedGpuWork(std::uint32_t timeoutMilliseconds)
    {
        if (!g_frameInFlight.load(std::memory_order_acquire))
        {
            return true;
        }
        auto* fence = g_borrowedFrameFence.load(std::memory_order_acquire);
        const auto value =
            g_lastFenceSubmittedValue.load(std::memory_order_acquire);
        if (fence == nullptr || value == 0)
        {
            return false;
        }
        if (fence->GetCompletedValue() >= value)
        {
            return true;
        }
        HANDLE event = ::CreateEventW(nullptr, FALSE, FALSE, nullptr);
        if (event == nullptr)
        {
            return false;
        }
        const HRESULT result = fence->SetEventOnCompletion(value, event);
        if (FAILED(result))
        {
            ::CloseHandle(event);
            return false;
        }
        const DWORD waitResult = ::WaitForSingleObject(event, timeoutMilliseconds);
        ::CloseHandle(event);
        return waitResult == WAIT_OBJECT_0
            && fence->GetCompletedValue() >= value;
    }
}

namespace DesktopMascotNative
{
    void ResetContinuousCompositionDiagnostics()
    {
        std::lock_guard lock(g_resourceMutex);
        g_window.store(nullptr);
        g_borrowedSwapChain1 = nullptr;
        g_swapChain3.Reset();
        g_backBuffer.Reset();
        g_commandList.Reset();
        g_commandAllocator.Reset();
        g_borrowedFrameFence.store(nullptr);
        g_borrowedDevice.store(nullptr);
        g_state.store(0);
        g_failureStage.store(0);
        g_requestedFrames.store(0);
        g_recordedFrames.store(0);
        g_submittedFrames.store(0);
        g_fenceCompletedFrames.store(0);
        g_presentCount.store(0);
        g_targetFrameCount.store(kContinuousCompositionTargetFrameCount);
        g_lastSequence.store(0);
        g_expectedPresentSequence.store(0);
        g_lastBackBufferIndex.store(0);
        g_backBuffer0UseCount.store(0);
        g_backBuffer1UseCount.store(0);
        g_invalidBackBufferIndexCount.store(0);
        g_lastFenceSubmittedValue.store(0);
        g_lastFenceCompletedValue.store(0);
        g_frameInFlight.store(false);
        g_lastPresentResult.store(static_cast<std::int32_t>(S_OK));
        g_lastDeviceRemovedReason.store(static_cast<std::int32_t>(S_OK));
        g_presentOcclusionReported.store(false);
        g_startTick.store(0);
        g_frameRequestTick.store(0);
        g_fenceSubmissionTick.store(0);
        g_elapsedMilliseconds.store(0);
        g_previousPresentQpc.store(0);
        g_minimumFrameMicroseconds.store(
            std::numeric_limits<std::uint64_t>::max());
        g_maximumFrameMicroseconds.store(0);
        g_totalFrameMicroseconds.store(0);
        g_frameIntervalSampleCount.store(0);
        g_droppedScheduleCount.store(0);
        g_rejectedFrameRequestCount.store(0);
        g_completedNormally.store(false);
        g_overallTimeout.store(false);
        g_shutdownRequested.store(false);
        g_runtimeMode.store(false);
    }

    void SetContinuousCompositionUiObjects(
        void* window,
        IDXGISwapChain1* swapChain)
    {
        std::lock_guard lock(g_resourceMutex);
        g_window.store(static_cast<HWND>(window), std::memory_order_release);
        g_borrowedSwapChain1 = swapChain;
    }

    bool SetContinuousCompositionTargetFrameCountForNextRun(
        std::uint32_t targetFrameCount)
    {
        if (targetFrameCount == 0
            || g_state.load(std::memory_order_acquire)
                != static_cast<std::int32_t>(
                    ContinuousPresentState::NotStarted))
        {
            return false;
        }
        g_targetFrameCount.store(targetFrameCount, std::memory_order_release);
        return true;
    }

    bool EnableContinuousCompositionRuntimeModeForNextRun()
    {
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                ContinuousPresentState::NotStarted))
        {
            return false;
        }
        g_runtimeMode.store(true, std::memory_order_release);
        return true;
    }

    std::int32_t StartContinuousCompositionDiagnostics()
    {
        std::int32_t expected =
            static_cast<std::int32_t>(ContinuousPresentState::NotStarted);
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    ContinuousPresentState::WaitingForComposition),
                std::memory_order_acq_rel))
        {
            g_failureStage.store(
                static_cast<std::int32_t>(
                    ContinuousPresentFailureStage::AlreadyRunning));
            return 0;
        }
        if (GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning)
            || !IsDestinationTextureAvailable())
        {
            Fail(
                !IsDestinationTextureAvailable()
                    ? ContinuousPresentFailureStage::DestinationNotReady
                    : ContinuousPresentFailureStage::CompositionNotReady);
            return -1;
        }

        g_startTick.store(::GetTickCount64(), std::memory_order_relaxed);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr
            || ::PostMessageW(
                   window,
                   kContinuousCompositionInitializeMessage,
                   0,
                   0)
                == FALSE)
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return -2;
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Continuous composition diagnostics "
            L"started.\n");
        return 1;
    }

    void HandleContinuousCompositionInitializeMessage()
    {
        std::lock_guard lock(g_resourceMutex);
        if (g_borrowedSwapChain1 == nullptr)
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return;
        }
        const HRESULT result = g_borrowedSwapChain1->QueryInterface(
            IID_PPV_ARGS(g_swapChain3.ReleaseAndGetAddressOf()));
        if (FAILED(result))
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return;
        }
        StoreState(ContinuousPresentState::ReadyForFrame);
    }

    std::int32_t RequestContinuousCompositionFrame()
    {
        if (g_shutdownRequested.load(std::memory_order_acquire)
            || (!g_runtimeMode.load(std::memory_order_acquire)
                && g_presentCount.load(std::memory_order_acquire)
                    >= g_targetFrameCount.load(std::memory_order_acquire)))
        {
            g_rejectedFrameRequestCount.fetch_add(1);
            return 0;
        }
        std::int32_t expected =
            static_cast<std::int32_t>(ContinuousPresentState::ReadyForFrame);
        if (!g_state.compare_exchange_strong(
                expected,
                static_cast<std::int32_t>(
                    ContinuousPresentState::WaitingForUnityRenderEvent),
                std::memory_order_acq_rel))
        {
            g_rejectedFrameRequestCount.fetch_add(1);
            return 0;
        }
        if (g_frameInFlight.load(std::memory_order_acquire))
        {
            Fail(ContinuousPresentFailureStage::UnexpectedSecondFrameInFlight);
            return -1;
        }
        const auto sequence =
            g_requestedFrames.fetch_add(1, std::memory_order_relaxed) + 1;
        g_lastSequence.store(sequence, std::memory_order_release);
        g_frameRequestTick.store(::GetTickCount64(), std::memory_order_relaxed);
        return 1;
    }

    void HandleContinuousCompositionFrameEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device,
        void* sourceTexture)
    {
        if (g_state.load(std::memory_order_acquire)
                != static_cast<std::int32_t>(
                    ContinuousPresentState::WaitingForUnityRenderEvent)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            return;
        }
        if (g_frameInFlight.load(std::memory_order_acquire))
        {
            Fail(ContinuousPresentFailureStage::UnexpectedSecondFrameInFlight);
            return;
        }
        if (d3d12 == nullptr || device == nullptr)
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return;
        }
        auto* source = static_cast<ID3D12Resource*>(sourceTexture);
        auto* destination = GetDestinationTextureForRenderThread();
        if (source == nullptr || destination == nullptr)
        {
            Fail(ContinuousPresentFailureStage::DestinationNotReady);
            return;
        }

        std::lock_guard lock(g_resourceMutex);
        if (g_swapChain3 == nullptr)
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return;
        }
        const UINT index = g_swapChain3->GetCurrentBackBufferIndex();
        g_lastBackBufferIndex.store(index, std::memory_order_relaxed);
        if (index >= kBufferCount)
        {
            g_invalidBackBufferIndexCount.fetch_add(1);
            Fail(ContinuousPresentFailureStage::InvalidBackBufferIndex);
            return;
        }
        HRESULT result = g_swapChain3->GetBuffer(
            index,
            IID_PPV_ARGS(g_backBuffer.ReleaseAndGetAddressOf()));
        if (FAILED(result))
        {
            Fail(ContinuousPresentFailureStage::BackBufferGetFailed);
            return;
        }
        if (!IsTexture2D64(source->GetDesc(), kSourceFormat)
            || !IsTexture2D64(destination->GetDesc(), kDestinationFormat)
            || !IsTexture2D64(g_backBuffer->GetDesc(), kBackBufferFormat))
        {
            Fail(ContinuousPresentFailureStage::BackBufferDescriptionChanged);
            return;
        }

        if (g_commandAllocator == nullptr)
        {
            result = device->CreateCommandAllocator(
                D3D12_COMMAND_LIST_TYPE_DIRECT,
                IID_PPV_ARGS(g_commandAllocator.ReleaseAndGetAddressOf()));
            if (SUCCEEDED(result))
            {
                result = device->CreateCommandList(
                    0,
                    D3D12_COMMAND_LIST_TYPE_DIRECT,
                    g_commandAllocator.Get(),
                    nullptr,
                    IID_PPV_ARGS(g_commandList.ReleaseAndGetAddressOf()));
            }
            if (FAILED(result))
            {
                Fail(ContinuousPresentFailureStage::CommandListResetFailed);
                return;
            }
        }
        else
        {
            auto* previousFence =
                g_borrowedFrameFence.load(std::memory_order_acquire);
            const auto previousValue =
                g_lastFenceSubmittedValue.load(std::memory_order_acquire);
            if (previousFence == nullptr
                || previousFence->GetCompletedValue() < previousValue)
            {
                Fail(
                    ContinuousPresentFailureStage::
                        CommandAllocatorResetBeforeFence);
                return;
            }
            result = g_commandAllocator->Reset();
            if (FAILED(result))
            {
                Fail(ContinuousPresentFailureStage::CommandAllocatorResetFailed);
                return;
            }
            result = g_commandList->Reset(g_commandAllocator.Get(), nullptr);
            if (FAILED(result))
            {
                Fail(ContinuousPresentFailureStage::CommandListResetFailed);
                return;
            }
        }

        const auto destinationTrackedState =
            static_cast<D3D12_RESOURCE_STATES>(
                GetCopyDestinationTrackedState());
        if (destinationTrackedState != D3D12_RESOURCE_STATE_COPY_DEST)
        {
            const D3D12_RESOURCE_BARRIER sourceToDestination[] = {
                Transition(
                    destination,
                    destinationTrackedState,
                    D3D12_RESOURCE_STATE_COPY_DEST)};
            g_commandList->ResourceBarrier(1, sourceToDestination);
        }
        g_commandList->CopyResource(destination, source);

        const D3D12_RESOURCE_BARRIER destinationToBackBuffer[] = {
            Transition(
                destination,
                D3D12_RESOURCE_STATE_COPY_DEST,
                D3D12_RESOURCE_STATE_COPY_SOURCE),
            Transition(
                g_backBuffer.Get(),
                D3D12_RESOURCE_STATE_PRESENT,
                D3D12_RESOURCE_STATE_COPY_DEST)};
        g_commandList->ResourceBarrier(2, destinationToBackBuffer);
        g_commandList->CopyResource(g_backBuffer.Get(), destination);

        const D3D12_RESOURCE_BARRIER restoreStates[] = {
            Transition(
                g_backBuffer.Get(),
                D3D12_RESOURCE_STATE_COPY_DEST,
                D3D12_RESOURCE_STATE_PRESENT),
            Transition(
                destination,
                D3D12_RESOURCE_STATE_COPY_SOURCE,
                destinationTrackedState)};
        g_commandList->ResourceBarrier(2, restoreStates);
        result = g_commandList->Close();
        if (FAILED(result))
        {
            Fail(ContinuousPresentFailureStage::CommandListCloseFailed);
            return;
        }
        g_recordedFrames.fetch_add(1);
        StoreState(ContinuousPresentState::CopyCommandRecorded);

        auto* frameFence = d3d12->GetFrameFence();
        if (frameFence == nullptr)
        {
            Fail(ContinuousPresentFailureStage::UnityFenceUnavailable);
            return;
        }
        UnityGraphicsD3D12ResourceState resourceStates[] = {
            {source,
             D3D12_RESOURCE_STATE_COPY_SOURCE,
             D3D12_RESOURCE_STATE_COPY_SOURCE},
            {destination, destinationTrackedState, destinationTrackedState},
            {g_backBuffer.Get(),
             D3D12_RESOURCE_STATE_PRESENT,
             D3D12_RESOURCE_STATE_PRESENT}};
        g_frameInFlight.store(true, std::memory_order_release);
        const UINT64 fenceValue =
            d3d12->ExecuteCommandList(g_commandList.Get(), 3, resourceStates);
        if (fenceValue == 0)
        {
            g_frameInFlight.store(false, std::memory_order_release);
            Fail(ContinuousPresentFailureStage::UnityExecuteCommandListFailed);
            return;
        }
        g_borrowedFrameFence.store(frameFence, std::memory_order_release);
        g_borrowedDevice.store(device, std::memory_order_release);
        g_lastFenceSubmittedValue.store(fenceValue, std::memory_order_release);
        g_submittedFrames.fetch_add(1);
        g_fenceSubmissionTick.store(::GetTickCount64());
        StoreState(ContinuousPresentState::WaitingForFence);
    }

    void PollContinuousCompositionDiagnostics()
    {
        const auto state = g_state.load(std::memory_order_acquire);
        if (state == static_cast<std::int32_t>(
                         ContinuousPresentState::NotStarted)
            || IsTerminalState(state)
            || state == static_cast<std::int32_t>(
                            ContinuousPresentState::ShutdownRequested))
        {
            return;
        }
        const auto now = ::GetTickCount64();
        const auto start = g_startTick.load(std::memory_order_relaxed);
        if (start != 0)
        {
            g_elapsedMilliseconds.store(now - start);
            if (!g_runtimeMode.load(std::memory_order_acquire)
                && now - start > kOverallTimeoutMilliseconds)
            {
                g_overallTimeout.store(true);
                Fail(ContinuousPresentFailureStage::OverallTimeout);
                return;
            }
        }
        if (state == static_cast<std::int32_t>(
                         ContinuousPresentState::WaitingForUnityRenderEvent)
            && now - g_frameRequestTick.load() > kFenceTimeoutMilliseconds)
        {
            Fail(ContinuousPresentFailureStage::RenderEventNotReceived);
            return;
        }
        if (state != static_cast<std::int32_t>(
                         ContinuousPresentState::WaitingForFence))
        {
            return;
        }
        auto* fence = g_borrowedFrameFence.load(std::memory_order_acquire);
        const auto submitted =
            g_lastFenceSubmittedValue.load(std::memory_order_acquire);
        if (fence == nullptr)
        {
            Fail(ContinuousPresentFailureStage::UnityFenceUnavailable);
            return;
        }
        const auto completed = fence->GetCompletedValue();
        g_lastFenceCompletedValue.store(completed, std::memory_order_relaxed);
        if (completed < submitted)
        {
            if (now - g_fenceSubmissionTick.load()
                > kFenceTimeoutMilliseconds)
            {
                Fail(ContinuousPresentFailureStage::FenceTimeout);
            }
            return;
        }
        g_fenceCompletedFrames.fetch_add(1);
        const auto sequence = g_lastSequence.load(std::memory_order_acquire);
        g_expectedPresentSequence.store(sequence, std::memory_order_release);
        StoreState(ContinuousPresentState::PresentMessagePosted);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr
            || ::PostMessageW(
                   window,
                   kContinuousCompositionPresentMessage,
                   static_cast<WPARAM>(sequence),
                   0)
                == FALSE)
        {
            Fail(ContinuousPresentFailureStage::PresentMessagePostFailed);
            return;
        }
        StoreState(ContinuousPresentState::WaitingForPresent);
    }

    std::int32_t PollContinuousCompositionDiagnosticsAndGetState()
    {
        PollContinuousCompositionDiagnostics();
        return GetContinuousCompositionState();
    }

    void RecordContinuousCompositionDroppedSchedule()
    {
        g_droppedScheduleCount.fetch_add(1, std::memory_order_relaxed);
    }

    void HandleContinuousCompositionPresentMessage(std::uint32_t sequence)
    {
        if (g_shutdownRequested.load(std::memory_order_acquire))
        {
            return;
        }
        if (sequence == 0
            || sequence
                != g_expectedPresentSequence.load(std::memory_order_acquire))
        {
            Fail(
                ContinuousPresentFailureStage::
                    PresentMessageSequenceMismatch);
            return;
        }
        auto* fence = g_borrowedFrameFence.load(std::memory_order_acquire);
        const auto submitted =
            g_lastFenceSubmittedValue.load(std::memory_order_acquire);
        if (fence == nullptr || fence->GetCompletedValue() < submitted)
        {
            Fail(ContinuousPresentFailureStage::PresentBeforeFenceCompletion);
            return;
        }

        std::lock_guard lock(g_resourceMutex);
        if (g_swapChain3 == nullptr || g_backBuffer == nullptr)
        {
            Fail(ContinuousPresentFailureStage::CompositionNotReady);
            return;
        }
        const HRESULT result =
            g_swapChain3->Present(kPresentSyncInterval, kPresentFlags);
        if (result == DXGI_STATUS_OCCLUDED)
        {
            if (!g_presentOcclusionReported.exchange(true))
            {
                ::OutputDebugStringW(
                    L"[DesktopMascotNative] Continuous composition Present "
                    L"temporarily occluded; retrying.\n");
            }
        }
        else
        {
            g_lastPresentResult.store(static_cast<std::int32_t>(result));
            if (result != S_OK)
            {
                if (result == DXGI_ERROR_DEVICE_REMOVED
                    || result == DXGI_ERROR_DEVICE_RESET)
                {
                    auto* device =
                        g_borrowedDevice.load(std::memory_order_acquire);
                    if (device != nullptr)
                    {
                        g_lastDeviceRemovedReason.store(
                            static_cast<std::int32_t>(
                                device->GetDeviceRemovedReason()));
                    }
                    Fail(ContinuousPresentFailureStage::DeviceRemoved);
                }
                else
                {
                    Fail(ContinuousPresentFailureStage::PresentFailed);
                }
                return;
            }
        }

        const auto index = g_lastBackBufferIndex.load();
        if (index == 0)
        {
            g_backBuffer0UseCount.fetch_add(1);
        }
        else if (index == 1)
        {
            g_backBuffer1UseCount.fetch_add(1);
        }
        else
        {
            g_invalidBackBufferIndexCount.fetch_add(1);
        }
        const auto count = g_presentCount.fetch_add(1) + 1;
        const auto target =
            g_targetFrameCount.load(std::memory_order_acquire);
        if (!g_runtimeMode.load(std::memory_order_acquire)
            && count > target)
        {
            Fail(ContinuousPresentFailureStage::UnexpectedPresentCount);
            return;
        }

        const auto nowQpc = QueryPerformanceCounterValue();
        const auto previous = g_previousPresentQpc.exchange(nowQpc);
        if (previous != 0)
        {
            const auto interval = QpcDeltaMicroseconds(previous, nowQpc);
            UpdateMinimum(g_minimumFrameMicroseconds, interval);
            UpdateMaximum(g_maximumFrameMicroseconds, interval);
            g_totalFrameMicroseconds.fetch_add(interval);
            g_frameIntervalSampleCount.fetch_add(1);
        }
        g_backBuffer.Reset();
        g_frameInFlight.store(false, std::memory_order_release);
        StoreState(ContinuousPresentState::FramePresented);

        if (count == 1)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Continuous composition first "
                L"Present succeeded.\n");
        }
        if (!g_runtimeMode.load(std::memory_order_acquire)
            && count == target)
        {
            const auto start = g_startTick.load();
            g_elapsedMilliseconds.store(::GetTickCount64() - start);
            g_completedNormally.store(true, std::memory_order_release);
            StoreState(ContinuousPresentState::Completed);
            wchar_t message[128]{};
            swprintf_s(
                message,
                L"[DesktopMascotNative] Continuous composition diagnostics "
                L"completed: presents=%u.\n",
                target);
            ::OutputDebugStringW(message);
            return;
        }
        StoreState(ContinuousPresentState::FrameIntervalWaiting);
        StoreState(ContinuousPresentState::ReadyForFrame);
    }

    std::int32_t RequestContinuousCompositionDiagnosticsStop()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        const auto state = g_state.load(std::memory_order_acquire);
        if (state != static_cast<std::int32_t>(
                         ContinuousPresentState::NotStarted)
            && state
                != static_cast<std::int32_t>(
                    ContinuousPresentState::Stopped))
        {
            StoreState(ContinuousPresentState::ShutdownRequested);
        }
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr
            || ::PostMessageW(
                   window,
                   kContinuousCompositionStopMessage,
                   0,
                   0)
                == FALSE)
        {
            return 0;
        }
        return 1;
    }

    void NotifyContinuousCompositionShutdownRequested()
    {
        g_shutdownRequested.store(true, std::memory_order_release);
        const auto state = g_state.load(std::memory_order_acquire);
        if (!IsTerminalState(state)
            && state
                != static_cast<std::int32_t>(
                    ContinuousPresentState::NotStarted))
        {
            StoreState(ContinuousPresentState::ShutdownRequested);
        }
    }

    bool ShutdownContinuousCompositionDiagnosticsOnUiThread(
        std::uint32_t timeoutMilliseconds)
    {
        NotifyContinuousCompositionShutdownRequested();
        if (!WaitForSubmittedGpuWork(timeoutMilliseconds))
        {
            Fail(ContinuousPresentFailureStage::ShutdownFailed);
            return false;
        }
        std::lock_guard lock(g_resourceMutex);
        g_frameInFlight.store(false);
        g_backBuffer.Reset();
        g_commandList.Reset();
        g_commandAllocator.Reset();
        g_swapChain3.Reset();
        g_borrowedFrameFence.store(nullptr);
        g_borrowedDevice.store(nullptr);
        g_borrowedSwapChain1 = nullptr;
        g_window.store(nullptr);
        StoreState(ContinuousPresentState::Stopped);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Continuous composition diagnostics "
            L"stopped.\n");
        return true;
    }

    void HandleContinuousCompositionStopMessage()
    {
        ShutdownContinuousCompositionDiagnosticsOnUiThread(
            static_cast<std::uint32_t>(kFenceTimeoutMilliseconds));
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_U64(fn, value) std::uint64_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetContinuousCompositionState, g_state)
    GET_I32(GetContinuousCompositionFailureStage, g_failureStage)
    std::int32_t GetContinuousCompositionCommandSubmissionMode()
    {
        // Same stable diagnostic value as CompositionCommandSubmissionMode::
        // UnityOfficialExecuteApi. This path never performs raw queue
        // submission.
        return 1;
    }
    std::uint32_t GetContinuousCompositionTargetFrameCount()
    {
        return g_targetFrameCount.load(std::memory_order_acquire);
    }
    GET_U32(GetContinuousCompositionRequestedFrameCount, g_requestedFrames)
    GET_U32(GetContinuousCompositionRecordedFrameCount, g_recordedFrames)
    GET_U32(GetContinuousCompositionSubmittedFrameCount, g_submittedFrames)
    GET_U32(
        GetContinuousCompositionFenceCompletedFrameCount,
        g_fenceCompletedFrames)
    GET_U32(GetContinuousCompositionPresentCount, g_presentCount)
    GET_U32(GetContinuousCompositionLastSequence, g_lastSequence)
    GET_U32(
        GetContinuousCompositionLastBackBufferIndex,
        g_lastBackBufferIndex)
    GET_U32(
        GetContinuousCompositionBackBuffer0UseCount,
        g_backBuffer0UseCount)
    GET_U32(
        GetContinuousCompositionBackBuffer1UseCount,
        g_backBuffer1UseCount)
    GET_U32(
        GetContinuousCompositionInvalidBackBufferIndexCount,
        g_invalidBackBufferIndexCount)
    GET_U64(
        GetContinuousCompositionLastFenceSubmittedValue,
        g_lastFenceSubmittedValue)
    GET_U64(
        GetContinuousCompositionLastFenceCompletedValue,
        g_lastFenceCompletedValue)
    GET_BOOL(IsContinuousCompositionFrameInFlight, g_frameInFlight)
    GET_I32(GetContinuousCompositionLastPresentResult, g_lastPresentResult)
    GET_I32(
        GetContinuousCompositionLastDeviceRemovedReason,
        g_lastDeviceRemovedReason)
    GET_U64(
        GetContinuousCompositionElapsedMilliseconds,
        g_elapsedMilliseconds)
    std::uint64_t GetContinuousCompositionMinimumFrameMilliseconds()
    {
        const auto value = g_minimumFrameMicroseconds.load();
        return value == std::numeric_limits<std::uint64_t>::max()
            ? 0
            : value / 1000;
    }
    std::uint64_t GetContinuousCompositionMaximumFrameMilliseconds()
    {
        return g_maximumFrameMicroseconds.load() / 1000;
    }
    std::uint64_t GetContinuousCompositionAverageFrameMicroseconds()
    {
        const auto count = g_frameIntervalSampleCount.load();
        return count == 0 ? 0 : g_totalFrameMicroseconds.load() / count;
    }
    GET_U32(
        GetContinuousCompositionDroppedScheduleCount,
        g_droppedScheduleCount)
    GET_U32(
        GetContinuousCompositionRejectedFrameRequestCount,
        g_rejectedFrameRequestCount)
    GET_BOOL(
        DidContinuousCompositionCompleteNormally,
        g_completedNormally)
    GET_BOOL(DidContinuousCompositionTimeout, g_overallTimeout)
#undef GET_I32
#undef GET_U32
#undef GET_U64
#undef GET_BOOL
}
