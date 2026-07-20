#pragma once

#include <cstdint>

struct ID3D12Resource;
struct IUnityGraphicsD3D12v8;

namespace DesktopMascotNative
{
    // Stable values exposed through DMN_GetCopyFailureStage().
    enum class CopyDiagnosticFailureStage : std::int32_t
    {
        None = 0,
        EventDataNull = 1,
        SourceUnavailable = 2,
        DestinationUnavailable = 3,
        SourceDescriptionUnavailable = 4,
        DestinationDescriptionUnavailable = 5,
        ResourceMismatch = 6,
        CommandRecordingStateUnavailable = 7,
        CommandListUnavailable = 8,
        SourceStateRequestFailed = 9,
        CopyNotRecorded = 10,
        SourceStateNotifyFailed = 11,
        DestinationStateTrackingFailed = 12,
        AlreadyRecorded = 13
    };

    void ResetCopyResourceDiagnostics();
    void HandleCopyResourceDiagnosticEvent(
        IUnityGraphicsD3D12v8* d3d12,
        std::int32_t eventId,
        void* eventData);

    std::int32_t GetCopyDiagnosticEventCount();
    std::int32_t GetLastCopyDiagnosticEventId();
    std::uint32_t GetLastCopyDiagnosticRenderThreadId();
    bool WasCopyEventDataNonNull();
    bool WasCopySourceAvailable();
    bool WasCopyDestinationAvailable();
    bool WasCopyCommandRecordingStateAvailable();
    bool WasCopyCommandListAvailable();
    bool DidCopyValidationPass();
    bool WasCopyResourceStateRequestAttempted();
    bool DidCopyResourceStateRequestSucceed();
    bool WasCopyResourceRecorded();
    bool WasCopyResourceStateNotificationAttempted();
    bool DidCopyResourceStateNotificationComplete();
    bool WasCopyAlreadyRecorded();
    std::int32_t GetCopyFailureStage();
    std::int32_t GetCopySourceRequestedState();
    std::int32_t GetCopyDestinationTrackedState();
    std::int32_t GetCopyAttemptCount();
    std::int32_t GetCopySuccessCount();
}
