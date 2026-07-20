#include "DesktopMascotNative/CopyResourceDiagnostics.h"

#include "DesktopMascotNative/DestinationTexture.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi.h>

#include <atomic>
#include <cwchar>

#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"

namespace
{
    using DesktopMascotNative::CopyDiagnosticFailureStage;

    constexpr auto kSourceRequiredState = D3D12_RESOURCE_STATE_COPY_SOURCE;
    constexpr auto kDestinationTrackedState =
        D3D12_RESOURCE_STATE_COPY_DEST;

    std::atomic<bool> g_attemptClaimed{false};
    std::atomic<std::int32_t> g_eventCount{0};
    std::atomic<std::int32_t> g_lastEventId{0};
    std::atomic<std::uint32_t> g_lastRenderThreadId{0};
    std::atomic<bool> g_eventDataNonNull{false};
    std::atomic<bool> g_sourceAvailable{false};
    std::atomic<bool> g_destinationAvailable{false};
    std::atomic<bool> g_commandRecordingStateAvailable{false};
    std::atomic<bool> g_commandListAvailable{false};
    std::atomic<bool> g_validationPassed{false};
    std::atomic<bool> g_stateRequestAttempted{false};
    std::atomic<bool> g_stateRequestCompleted{false};
    std::atomic<bool> g_copyRecorded{false};
    std::atomic<bool> g_stateNotificationAttempted{false};
    std::atomic<bool> g_stateNotificationCompleted{false};
    std::atomic<bool> g_alreadyRecorded{false};
    std::atomic<std::int32_t> g_failureStage{
        static_cast<std::int32_t>(CopyDiagnosticFailureStage::None)};
    std::atomic<std::int32_t> g_attemptCount{0};
    std::atomic<std::int32_t> g_successCount{0};

    bool AreCopyCompatible(
        const D3D12_RESOURCE_DESC& source,
        const D3D12_RESOURCE_DESC& destination)
    {
        return source.Dimension == destination.Dimension
            && source.Width == destination.Width
            && source.Height == destination.Height
            && source.DepthOrArraySize == destination.DepthOrArraySize
            && source.MipLevels == destination.MipLevels
            && source.Format == destination.Format
            && source.SampleDesc.Count == destination.SampleDesc.Count
            && source.SampleDesc.Quality == destination.SampleDesc.Quality;
    }

    void StoreFailure(CopyDiagnosticFailureStage stage)
    {
        g_failureStage.store(
            static_cast<std::int32_t>(stage),
            std::memory_order_release);

        wchar_t message[128]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] CopyResource recording failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }
}

namespace DesktopMascotNative
{
    void ResetCopyResourceDiagnostics()
    {
        g_attemptClaimed.store(false, std::memory_order_relaxed);
        g_eventCount.store(0, std::memory_order_relaxed);
        g_lastEventId.store(0, std::memory_order_relaxed);
        g_lastRenderThreadId.store(0, std::memory_order_relaxed);
        g_eventDataNonNull.store(false, std::memory_order_relaxed);
        g_sourceAvailable.store(false, std::memory_order_relaxed);
        g_destinationAvailable.store(false, std::memory_order_relaxed);
        g_commandRecordingStateAvailable.store(
            false,
            std::memory_order_relaxed);
        g_commandListAvailable.store(false, std::memory_order_relaxed);
        g_validationPassed.store(false, std::memory_order_relaxed);
        g_stateRequestAttempted.store(false, std::memory_order_relaxed);
        g_stateRequestCompleted.store(false, std::memory_order_relaxed);
        g_copyRecorded.store(false, std::memory_order_relaxed);
        g_stateNotificationAttempted.store(false, std::memory_order_relaxed);
        g_stateNotificationCompleted.store(false, std::memory_order_relaxed);
        g_alreadyRecorded.store(false, std::memory_order_relaxed);
        g_failureStage.store(
            static_cast<std::int32_t>(CopyDiagnosticFailureStage::None),
            std::memory_order_relaxed);
        g_attemptCount.store(0, std::memory_order_relaxed);
        g_successCount.store(0, std::memory_order_relaxed);
    }

    void HandleCopyResourceDiagnosticEvent(
        IUnityGraphicsD3D12v8* d3d12,
        std::int32_t eventId,
        void* eventData)
    {
        g_eventCount.fetch_add(1, std::memory_order_relaxed);
        g_lastEventId.store(eventId, std::memory_order_relaxed);
        g_lastRenderThreadId.store(
            ::GetCurrentThreadId(),
            std::memory_order_relaxed);
        g_eventDataNonNull.store(
            eventData != nullptr,
            std::memory_order_relaxed);

        bool expected = false;
        if (!g_attemptClaimed.compare_exchange_strong(
                expected,
                true,
                std::memory_order_acq_rel))
        {
            g_alreadyRecorded.store(
                g_copyRecorded.load(std::memory_order_acquire),
                std::memory_order_relaxed);
            return;
        }

        g_attemptCount.fetch_add(1, std::memory_order_relaxed);

        if (eventData == nullptr)
        {
            StoreFailure(CopyDiagnosticFailureStage::EventDataNull);
            return;
        }

        auto* source = static_cast<ID3D12Resource*>(eventData);
        g_sourceAvailable.store(true, std::memory_order_relaxed);

        auto* destination = GetDestinationTextureForRenderThread();
        g_destinationAvailable.store(
            destination != nullptr,
            std::memory_order_relaxed);
        if (destination == nullptr)
        {
            StoreFailure(CopyDiagnosticFailureStage::DestinationUnavailable);
            return;
        }

        if (source == destination
            || !AreCopyCompatible(source->GetDesc(), destination->GetDesc()))
        {
            StoreFailure(CopyDiagnosticFailureStage::ResourceMismatch);
            return;
        }
        g_validationPassed.store(true, std::memory_order_relaxed);

        if (d3d12 == nullptr)
        {
            StoreFailure(
                CopyDiagnosticFailureStage::CommandRecordingStateUnavailable);
            return;
        }

        UnityGraphicsD3D12RecordingState recordingState{};
        const bool recordingStateAvailable =
            d3d12->CommandRecordingState(&recordingState);
        g_commandRecordingStateAvailable.store(
            recordingStateAvailable,
            std::memory_order_relaxed);
        if (!recordingStateAvailable)
        {
            StoreFailure(
                CopyDiagnosticFailureStage::CommandRecordingStateUnavailable);
            return;
        }

        auto* commandList = recordingState.commandList;
        g_commandListAvailable.store(
            commandList != nullptr,
            std::memory_order_relaxed);
        if (commandList == nullptr)
        {
            StoreFailure(CopyDiagnosticFailureStage::CommandListUnavailable);
            return;
        }

        // v8 returns void. "completed" means the API call returned; it is not
        // an HRESULT or proof of GPU execution.
        g_stateRequestAttempted.store(true, std::memory_order_relaxed);
        d3d12->RequestResourceState(source, kSourceRequiredState);
        g_stateRequestCompleted.store(true, std::memory_order_relaxed);

        // This records exactly one command. It does not prove GPU completion
        // or validate the destination contents.
        commandList->CopyResource(destination, source);
        g_copyRecorded.store(true, std::memory_order_release);

        g_stateNotificationAttempted.store(true, std::memory_order_relaxed);
        d3d12->NotifyResourceState(source, kSourceRequiredState, false);
        g_stateNotificationCompleted.store(true, std::memory_order_relaxed);

        g_successCount.fetch_add(1, std::memory_order_relaxed);
        g_failureStage.store(
            static_cast<std::int32_t>(CopyDiagnosticFailureStage::None),
            std::memory_order_release);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] CopyResource recorded successfully.\n");
    }

    std::int32_t GetCopyDiagnosticEventCount()
    {
        return g_eventCount.load(std::memory_order_acquire);
    }

    std::int32_t GetLastCopyDiagnosticEventId()
    {
        return g_lastEventId.load(std::memory_order_relaxed);
    }

    std::uint32_t GetLastCopyDiagnosticRenderThreadId()
    {
        return g_lastRenderThreadId.load(std::memory_order_relaxed);
    }

    bool WasCopyEventDataNonNull()
    {
        return g_eventDataNonNull.load(std::memory_order_relaxed);
    }

    bool WasCopySourceAvailable()
    {
        return g_sourceAvailable.load(std::memory_order_relaxed);
    }

    bool WasCopyDestinationAvailable()
    {
        return g_destinationAvailable.load(std::memory_order_relaxed);
    }

    bool WasCopyCommandRecordingStateAvailable()
    {
        return g_commandRecordingStateAvailable.load(std::memory_order_relaxed);
    }

    bool WasCopyCommandListAvailable()
    {
        return g_commandListAvailable.load(std::memory_order_relaxed);
    }

    bool DidCopyValidationPass()
    {
        return g_validationPassed.load(std::memory_order_relaxed);
    }

    bool WasCopyResourceStateRequestAttempted()
    {
        return g_stateRequestAttempted.load(std::memory_order_relaxed);
    }

    bool DidCopyResourceStateRequestSucceed()
    {
        return g_stateRequestCompleted.load(std::memory_order_relaxed);
    }

    bool WasCopyResourceRecorded()
    {
        return g_copyRecorded.load(std::memory_order_acquire);
    }

    bool WasCopyResourceStateNotificationAttempted()
    {
        return g_stateNotificationAttempted.load(std::memory_order_relaxed);
    }

    bool DidCopyResourceStateNotificationComplete()
    {
        return g_stateNotificationCompleted.load(std::memory_order_relaxed);
    }

    bool WasCopyAlreadyRecorded()
    {
        return g_alreadyRecorded.load(std::memory_order_relaxed);
    }

    std::int32_t GetCopyFailureStage()
    {
        return g_failureStage.load(std::memory_order_acquire);
    }

    std::int32_t GetCopySourceRequestedState()
    {
        return static_cast<std::int32_t>(kSourceRequiredState);
    }

    std::int32_t GetCopyDestinationTrackedState()
    {
        return static_cast<std::int32_t>(kDestinationTrackedState);
    }

    std::int32_t GetCopyAttemptCount()
    {
        return g_attemptCount.load(std::memory_order_relaxed);
    }

    std::int32_t GetCopySuccessCount()
    {
        return g_successCount.load(std::memory_order_relaxed);
    }
}
