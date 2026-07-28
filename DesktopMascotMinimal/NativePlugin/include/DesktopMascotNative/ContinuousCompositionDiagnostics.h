#pragma once

#include <cstdint>

struct ID3D12Device;
struct IDXGISwapChain1;
struct IUnityGraphicsD3D12v8;

namespace DesktopMascotNative
{
    constexpr std::uint32_t kContinuousCompositionInitializeMessage =
        0x8000 + 0x42;
    constexpr std::uint32_t kContinuousCompositionPresentMessage =
        0x8000 + 0x43;
    constexpr std::uint32_t kContinuousCompositionStopMessage =
        0x8000 + 0x44;
    constexpr std::uint32_t kContinuousCompositionTargetFrameCount = 1500;

    enum class ContinuousPresentState : std::int32_t
    {
        NotStarted = 0,
        WaitingForComposition = 1,
        ReadyForFrame = 2,
        WaitingForUnityRenderEvent = 3,
        CopyCommandRecorded = 4,
        WaitingForFence = 5,
        PresentMessagePosted = 6,
        WaitingForPresent = 7,
        FramePresented = 8,
        FrameIntervalWaiting = 9,
        Completed = 10,
        ShutdownRequested = 11,
        Stopped = 12,
        Failed = 13
    };

    enum class ContinuousPresentFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        DestinationNotReady = 2,
        AlreadyRunning = 3,
        FrameRequestRejected = 4,
        RenderEventNotReceived = 5,
        InvalidBackBufferIndex = 6,
        BackBufferGetFailed = 7,
        BackBufferDescriptionChanged = 8,
        CommandAllocatorResetBeforeFence = 9,
        CommandAllocatorResetFailed = 10,
        CommandListResetFailed = 11,
        BarrierRecordingValidationFailed = 12,
        CopyRecordingFailed = 13,
        CommandListCloseFailed = 14,
        UnityExecuteCommandListFailed = 15,
        UnityFenceUnavailable = 16,
        FenceTimeout = 17,
        PresentMessagePostFailed = 18,
        PresentMessageSequenceMismatch = 19,
        PresentBeforeFenceCompletion = 20,
        PresentFailed = 21,
        DeviceRemoved = 22,
        UnexpectedSecondFrameInFlight = 23,
        UnexpectedPresentCount = 24,
        OverallTimeout = 25,
        ShutdownDuringGpuWork = 26,
        ShutdownFailed = 27
    };

    void ResetContinuousCompositionDiagnostics();
    bool SetContinuousCompositionTargetFrameCountForNextRun(
        std::uint32_t targetFrameCount);
    bool EnableContinuousCompositionRuntimeModeForNextRun();
    void SetContinuousCompositionUiObjects(
        void* window,
        IDXGISwapChain1* swapChain);
    std::int32_t StartContinuousCompositionDiagnostics();
    std::int32_t RequestContinuousCompositionFrame();
    std::int32_t RequestContinuousCompositionDiagnosticsStop();
    std::int32_t PollContinuousCompositionDiagnosticsAndGetState();
    void RecordContinuousCompositionDroppedSchedule();
    void HandleContinuousCompositionInitializeMessage();
    void HandleContinuousCompositionFrameEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device,
        void* sourceTexture);
    void PollContinuousCompositionDiagnostics();
    void HandleContinuousCompositionPresentMessage(std::uint32_t sequence);
    void HandleContinuousCompositionStopMessage();
    void NotifyContinuousCompositionShutdownRequested();
    bool ShutdownContinuousCompositionDiagnosticsOnUiThread(
        std::uint32_t timeoutMilliseconds);

    std::int32_t GetContinuousCompositionState();
    std::int32_t GetContinuousCompositionFailureStage();
    std::int32_t GetContinuousCompositionCommandSubmissionMode();
    std::uint32_t GetContinuousCompositionTargetFrameCount();
    std::uint32_t GetContinuousCompositionRequestedFrameCount();
    std::uint32_t GetContinuousCompositionRecordedFrameCount();
    std::uint32_t GetContinuousCompositionSubmittedFrameCount();
    std::uint32_t GetContinuousCompositionFenceCompletedFrameCount();
    std::uint32_t GetContinuousCompositionPresentCount();
    std::uint32_t GetContinuousCompositionLastSequence();
    std::uint32_t GetContinuousCompositionLastBackBufferIndex();
    std::uint32_t GetContinuousCompositionBackBuffer0UseCount();
    std::uint32_t GetContinuousCompositionBackBuffer1UseCount();
    std::uint32_t GetContinuousCompositionInvalidBackBufferIndexCount();
    std::uint64_t GetContinuousCompositionLastFenceSubmittedValue();
    std::uint64_t GetContinuousCompositionLastFenceCompletedValue();
    bool IsContinuousCompositionFrameInFlight();
    std::int32_t GetContinuousCompositionLastPresentResult();
    std::int32_t GetContinuousCompositionLastDeviceRemovedReason();
    std::uint64_t GetContinuousCompositionElapsedMilliseconds();
    std::uint64_t GetContinuousCompositionMinimumFrameMilliseconds();
    std::uint64_t GetContinuousCompositionMaximumFrameMilliseconds();
    std::uint64_t GetContinuousCompositionAverageFrameMicroseconds();
    std::uint32_t GetContinuousCompositionDroppedScheduleCount();
    std::uint32_t GetContinuousCompositionRejectedFrameRequestCount();
    bool DidContinuousCompositionCompleteNormally();
    bool DidContinuousCompositionTimeout();
}
