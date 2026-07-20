#pragma once

#include <cstdint>

struct ID3D12Device;
struct ID3D12CommandQueue;
struct IUnityGraphicsD3D12v8;

namespace DesktopMascotNative
{
    enum class ReadbackFailureStage : std::int32_t
    {
        None = 0,
        DestinationUnavailable = 1,
        ReadbackBufferCreationFailed = 2,
        CopyableFootprintFailed = 3,
        CommandRecordingStateUnavailable = 4,
        CommandListUnavailable = 5,
        DestinationStateTransitionFailed = 6,
        CopyTextureRegionNotRecorded = 7,
        FenceCreationFailed = 8,
        QueueUnavailable = 9,
        FenceSignalFailed = 10,
        FenceNotCompleted = 11,
        MapFailed = 12,
        PixelMismatch = 13,
        InvalidFootprint = 14,
        AlreadyExecuted = 15
    };

    void ResetReadbackDiagnostics();
    void ReleaseReadbackResources();
    void HandleReadbackCopyEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device);
    void HandleReadbackFenceSignalEvent(
        IUnityGraphicsD3D12v8* d3d12);

    bool IsReadbackBufferAvailable();
    std::int32_t GetReadbackBufferCreationResult();
    std::uint64_t GetReadbackFootprintOffset();
    std::uint32_t GetReadbackFootprintRowPitch();
    std::uint32_t GetReadbackNumRows();
    std::uint64_t GetReadbackRowSizeInBytes();
    std::uint64_t GetReadbackTotalBytes();
    std::int32_t GetReadbackCopyEventCount();
    bool WasReadbackCopyCallbackReached();
    bool WasReadbackCopyRecorded();
    bool IsReadbackFenceAvailable();
    bool WasReadbackFenceSignalAttempted();
    bool DidReadbackFenceSignalSucceed();
    std::int32_t GetReadbackFenceSignalResult();
    std::uint64_t GetReadbackFenceSubmittedValue();
    std::uint64_t GetReadbackFenceCompletedValue();
    bool IsReadbackFenceComplete();
    std::int32_t ValidateReadbackPixels();
    bool WasReadbackMapAttempted();
    bool DidReadbackMapSucceed();
    bool WasReadbackValidationCompleted();
    bool DidReadbackExpectedOrientationMatch();
    bool DidReadbackVerticallyFlippedOrientationMatch();
    std::int32_t GetReadbackFailureStage();
    std::uint32_t GetReadbackTopLeftBgra();
    std::uint32_t GetReadbackTopRightBgra();
    std::uint32_t GetReadbackBottomLeftBgra();
    std::uint32_t GetReadbackBottomRightBgra();
}
