#pragma once

#include <cstddef>
#include <cstdint>

namespace DesktopMascotNative
{
    enum class CompositionAlphaMaskState : std::int32_t
    {
        NotStarted = 0,
        Ready = 1,
        WaitingForFirstMask = 2,
        Receiving = 3,
        SnapshotPublished = 4,
        Validating = 5,
        Completed = 6,
        ShutdownRequested = 7,
        Stopped = 8,
        Failed = 9
    };

    enum class CompositionAlphaMaskFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        InvalidWidth = 2,
        InvalidHeight = 3,
        InvalidStride = 4,
        ByteCountOverflow = 5,
        NullData = 6,
        DiagnosticsNotStarted = 7,
        ShutdownAlreadyRequested = 8,
        InvalidGeneration = 9,
        StaleGeneration = 10,
        BufferAllocationFailed = 11,
        SnapshotPublishFailed = 12,
        SnapshotUnavailable = 13,
        SampleOutOfRange = 14,
        UnexpectedAlphaValue = 15,
        OrientationMismatch = 16,
        ThresholdMismatch = 17,
        UnexpectedSubmittedCount = 18,
        UnexpectedAcceptedCount = 19,
        UnexpectedRejectedCount = 20,
        ReadbackUnsupported = 21,
        ReadbackRequestError = 22,
        ReadbackTimeout = 23,
        ContinuousPresentFailed = 24,
        ShutdownFailed = 25
    };

    void ResetCompositionAlphaMaskDiagnostics();
    std::int32_t StartCompositionAlphaMaskDiagnostics(
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t alphaThreshold);
    std::int32_t StartAnimatedCompositionAlphaMaskDiagnostics(
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::int32_t alphaThreshold);
    std::int32_t SubmitCompositionAlphaMask(
        const std::uint8_t* data,
        std::int32_t width,
        std::int32_t height,
        std::int32_t stride,
        std::uint64_t generation);
    void StopCompositionAlphaMaskDiagnostics();

    std::int32_t GetCompositionAlphaMaskState();
    std::int32_t GetCompositionAlphaMaskFailureStage();
    std::uint32_t GetCompositionAlphaMaskSubmittedCount();
    std::uint32_t GetCompositionAlphaMaskAcceptedCount();
    std::uint32_t GetCompositionAlphaMaskRejectedCount();
    std::uint64_t GetCompositionAlphaMaskPublishedGeneration();
    std::int32_t GetCompositionAlphaMaskWidth();
    std::int32_t GetCompositionAlphaMaskHeight();
    std::int32_t GetCompositionAlphaMaskStride();
    std::int32_t GetCompositionAlphaMaskByteCount();
    std::int32_t GetCompositionAlphaMaskThreshold();
    std::int32_t GetCompositionAlphaMaskSampleAlpha(
        std::int32_t x,
        std::int32_t y);
    bool GetCompositionAlphaMaskSampleHit(
        std::int32_t x,
        std::int32_t y);
    bool IsCompositionAlphaMaskAvailable();
    bool IsCompositionAlphaMaskRequestPending();
    bool DidCompositionAlphaMaskLastSubmitSucceed();
    std::uint32_t GetCompositionAlphaMaskLastWin32Error();
    std::uint32_t GetCompositionAlphaMaskLastSubmitThreadId();
    bool TryReadCompositionAlphaMaskPixel(
        std::int32_t x,
        std::int32_t y,
        std::uint8_t& alpha,
        std::int32_t& threshold,
        std::uint64_t& generation);
    bool TryCopyCompositionAlphaMaskSnapshot(
        std::uint8_t* destination,
        std::size_t destinationSize,
        std::int32_t& width,
        std::int32_t& height,
        std::int32_t& stride,
        std::int32_t& threshold,
        std::uint64_t& generation);
}
