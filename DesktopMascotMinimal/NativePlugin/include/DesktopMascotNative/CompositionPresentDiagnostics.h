#pragma once

#include <cstdint>

struct ID3D12Device;
struct IDXGISwapChain1;
struct IUnityGraphicsD3D12v8;

namespace DesktopMascotNative
{
    constexpr std::uint32_t kCompositionPresentInitializeMessage =
        0x8000u + 0x40u;
    constexpr std::uint32_t kCompositionPresentOnceMessage =
        0x8000u + 0x41u;

    enum class CompositionPresentState : std::int32_t
    {
        NotStarted = 0,
        WaitingForCompositionInitialization = 1,
        SwapChain3Acquired = 2,
        BackBufferAcquired = 3,
        ResourcesValidated = 4,
        WaitingForCopyEvent = 5,
        CopyCommandsRecorded = 6,
        CopySubmitted = 7,
        WaitingForFence = 8,
        FenceCompleted = 9,
        PresentMessagePosted = 10,
        PresentMessageReceived = 11,
        PresentSucceeded = 12,
        DisplayHolding = 13,
        ShutdownRequested = 14,
        Stopped = 15,
        Failed = 16
    };

    enum class CompositionPresentFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        SwapChain3QueryFailed = 2,
        InvalidBackBufferIndex = 3,
        BackBufferGetFailed = 4,
        BackBufferDescInvalid = 5,
        DestinationUnavailable = 6,
        DestinationDescInvalid = 7,
        IncompatibleDimensions = 8,
        IncompatibleSampleConfiguration = 9,
        IncompatibleCopyFormats = 10,
        CommandAllocatorCreationFailed = 11,
        CommandListCreationFailed = 12,
        CommandAllocatorResetFailed = 13,
        CommandListResetFailed = 14,
        CommandRecordingStateUnavailable = 15,
        UnityCommandListUnavailable = 16,
        DestinationBarrierFailed = 17,
        BackBufferBarrierFailed = 18,
        CopyRecordingFailed = 19,
        CommandListCloseFailed = 20,
        CommandSubmissionFailed = 21,
        FenceCreationFailed = 22,
        FenceSignalFailed = 23,
        FenceTimeout = 24,
        PresentMessagePostFailed = 25,
        PresentMessageNotReceived = 26,
        PresentFailed = 27,
        UnexpectedSecondPresent = 28,
        ShutdownBeforePresent = 29,
        ShutdownFailed = 30
    };

    enum class CompositionCommandSubmissionMode : std::int32_t
    {
        None = 0,
        UnityOfficialExecuteApi = 1,
        UnityActiveCommandList = 2,
        RawQueueDirectSubmission = 3
    };

    void ResetCompositionPresentDiagnostics();
    void SetCompositionPresentUiObjects(
        void* window,
        IDXGISwapChain1* swapChain);
    std::int32_t StartCompositionPresentDiagnostics();
    bool MarkCompositionCopyEventIssued();
    void HandleCompositionPresentInitializeMessage();
    void HandleCompositionPresentCopyEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device);
    void PollCompositionPresentDiagnostics();
    void HandleCompositionPresentMessage();
    void MarkCompositionDisplayHolding();
    void NotifyCompositionPresentShutdownRequested();
    bool ShutdownCompositionPresentDiagnosticsOnUiThread(
        std::uint32_t timeoutMilliseconds);

    std::int32_t GetCompositionPresentState();
    std::int32_t GetCompositionPresentFailureStage();
    std::int32_t GetCompositionCommandSubmissionMode();
    bool WasSwapChain3QueryAttempted();
    bool IsSwapChain3Available();
    std::int32_t GetSwapChain3QueryResult();
    bool WasBackBufferIndexQueried();
    std::uint32_t GetCurrentBackBufferIndex();
    bool IsCurrentBackBufferIndexValid();
    bool WasBackBufferGetAttempted();
    bool IsBackBufferAvailable();
    std::int32_t GetBackBufferGetResult();
    bool IsBackBufferDescriptionAvailable();
    std::int32_t GetBackBufferDimension();
    std::uint64_t GetBackBufferWidth();
    std::uint32_t GetBackBufferHeight();
    std::uint32_t GetBackBufferDepthOrArraySize();
    std::uint32_t GetBackBufferMipLevels();
    std::int32_t GetBackBufferFormat();
    std::uint32_t GetBackBufferSampleCount();
    std::uint32_t GetBackBufferSampleQuality();
    std::int32_t GetBackBufferLayout();
    std::uint32_t GetBackBufferFlags();
    bool WasCompositionCopyCompatibilityChecked();
    bool AreCompositionCopyDimensionsCompatible();
    bool AreCompositionCopySamplesCompatible();
    bool AreCompositionCopyFormatsCompatible();
    bool WasCompositionCopyEventIssued();
    std::int32_t GetCompositionCopyEventCount();
    bool WasCompositionCopyCallbackReached();
    bool WereCompositionCopyBarriersRecorded();
    bool WasCompositionBackBufferCopyRecorded();
    bool WasCompositionCopySubmitted();
    bool WasCompositionExecuteCommandListAttempted();
    bool DidCompositionExecuteCommandListReturnFenceValue();
    std::uint64_t GetCompositionExecuteCommandListFenceValue();
    bool WasCompositionPresentFenceCreated();
    bool IsCompositionPresentFenceAvailable();
    bool WasCompositionPresentFenceSignalAttempted();
    bool DidCompositionPresentFenceSignalSucceed();
    std::int32_t GetCompositionPresentFenceSignalResult();
    std::uint64_t GetCompositionPresentFenceSubmittedValue();
    std::uint64_t GetCompositionPresentFenceCompletedValue();
    bool IsCompositionPresentFenceComplete();
    bool DidCompositionPresentFenceTimeout();
    bool WasCompositionPresentMessagePostAttempted();
    bool DidCompositionPresentMessagePostSucceed();
    std::uint32_t GetCompositionPresentMessagePostLastError();
    bool WasCompositionPresentMessageReceived();
    bool WasCompositionPresentAttempted();
    bool DidCompositionPresentSucceed();
    std::int32_t GetCompositionPresentResult();
    std::uint32_t GetCompositionPresentSyncInterval();
    std::uint32_t GetCompositionPresentFlags();
    std::int32_t GetCompositionPresentCount();
    bool WasPostPresentCommitAttempted();
    bool DidPostPresentCommitSucceed();
    std::int32_t GetPostPresentCommitResult();
}
