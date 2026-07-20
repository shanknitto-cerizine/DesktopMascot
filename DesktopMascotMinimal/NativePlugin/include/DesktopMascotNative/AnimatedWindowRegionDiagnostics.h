#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kAnimatedWindowRegionApplyMessage =
        0x8000u + 0x4Bu;
    constexpr std::uint32_t kAnimatedWindowRegionRestoreMessage =
        0x8000u + 0x4Cu;

    enum class AnimatedWindowRegionState : std::int32_t
    {
        NotStarted = 0,
        WaitingForComposition = 1,
        WaitingForMask = 2,
        ApplyingInitialStyle = 3,
        WaitingForFirstRegion = 4,
        Running = 5,
        StopRequested = 6,
        RestoringInitialRegion = 7,
        RestoringInitialStyle = 8,
        Completed = 9,
        Stopped = 10,
        Failed = 11
    };

    enum class AnimatedWindowRegionFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        InvalidThreshold = 3,
        InvalidUpdateInterval = 4,
        MaskUnavailable = 5,
        SnapshotCopyFailed = 6,
        RegionCreationFailed = 7,
        RegionCombineFailed = 8,
        ApplyMessagePostFailed = 9,
        SetWindowRgnFailed = 10,
        RepresentativePointValidationFailed = 11,
        RegionPixelCountValidationFailed = 12,
        GdiObjectLeakDetected = 13,
        InitialRegionRestoreFailed = 14,
        InitialStyleRestoreFailed = 15,
        ContinuousPresentFailed = 16,
        DiagnosticsTimeout = 17,
        ShutdownFailed = 18
    };

    enum class InitialWindowRegionState : std::int32_t
    {
        Unknown = 0,
        None = 1,
        Empty = 2,
        Simple = 3,
        Complex = 4,
        QueryFailed = 5
    };

    void ResetAnimatedWindowRegionDiagnostics();
    void SetAnimatedWindowRegionUiWindow(void* window);
    void HandleAnimatedWindowRegionApplyMessage();
    void HandleAnimatedWindowRegionRestoreMessage();
    void StopAnimatedWindowRegionDiagnosticsOnUiThread();
    bool ShouldAnimatedWindowRegionReturnClient();

    std::int32_t StartAnimatedWindowRegionDiagnostics(
        std::int32_t alphaThreshold,
        std::int32_t minimumUpdateIntervalMilliseconds);
    std::int32_t PollAnimatedWindowRegionDiagnostics();
    std::int32_t CompleteAnimatedWindowRegionDiagnostics();
    std::int32_t StopAnimatedWindowRegionDiagnostics();

    std::int32_t GetAnimatedWindowRegionState();
    std::int32_t GetAnimatedWindowRegionFailureStage();
    bool IsAnimatedWindowRegionEnabled();
    std::int32_t GetAnimatedWindowRegionThreshold();
    std::int32_t GetAnimatedWindowRegionMinimumUpdateIntervalMilliseconds();
    std::uint64_t GetAnimatedWindowRegionPublishedGeneration();
    std::uint64_t GetAnimatedWindowRegionBuildGeneration();
    std::uint64_t GetAnimatedWindowRegionRequestedGeneration();
    std::uint64_t GetAnimatedWindowRegionAppliedGeneration();
    std::uint32_t GetAnimatedWindowRegionBuildCount();
    std::uint32_t GetAnimatedWindowRegionGenerationEvaluationCount();
    std::uint32_t GetAnimatedWindowRegionApplyMessagePostCount();
    std::uint32_t GetAnimatedWindowRegionApplyExecutionCount();
    std::uint32_t GetAnimatedWindowRegionDuplicateMaskSkipCount();
    std::uint32_t GetAnimatedWindowRegionSupersededGenerationCount();
    // Legacy name retained for binary compatibility. It reports the number
    // of successfully posted UI-thread apply messages.
    std::uint32_t GetAnimatedWindowRegionApplyRequestCount();
    std::uint32_t GetAnimatedWindowRegionApplySuccessCount();
    std::uint32_t GetAnimatedWindowRegionApplyFailureCount();
    std::uint32_t GetAnimatedWindowRegionSkippedGenerationCount();
    std::uint32_t GetAnimatedWindowRegionDuplicateSkipCount();
    std::uint32_t GetAnimatedWindowRegionCurrentRectangleCount();
    std::uint32_t GetAnimatedWindowRegionCurrentCoveredPixelCount();
    std::uint32_t GetAnimatedWindowRegionCurrentExcludedPixelCount();
    std::uint32_t GetAnimatedWindowRegionInitialGdiObjectCount();
    std::uint32_t GetAnimatedWindowRegionPeakGdiObjectCount();
    std::uint32_t GetAnimatedWindowRegionFinalGdiObjectCount();
    std::int32_t GetAnimatedWindowRegionGdiObjectDelta();
    bool IsAnimatedWindowRegionApplyRequestPending();
    bool DidAnimatedWindowRegionLastApplySucceed();
    std::uint32_t GetAnimatedWindowRegionLastWin32Error();
    bool DidAnimatedWindowRegionInitialRegionRestoreSucceed();
    bool DidAnimatedWindowRegionInitialStyleRestoreSucceed();
    std::int32_t GetAnimatedWindowRegionInitialRegionState();
    std::int32_t GetAnimatedWindowRegionCurrentPhase();
    bool IsAnimatedWindowRegionTopLeftInside();
    bool IsAnimatedWindowRegionTopRightInside();
    bool IsAnimatedWindowRegionBottomRightInside();
    bool IsAnimatedWindowRegionBottomLeftInside();
    bool IsAnimatedWindowRegionCenterInside();
    std::uint64_t GetAnimatedWindowRegionInitialExtendedStyle();
    std::uint64_t GetAnimatedWindowRegionDiagnosticExtendedStyle();
    std::uint64_t GetAnimatedWindowRegionCurrentExtendedStyle();

    std::int32_t GetAnimatedWindowRegionPublishedPhase();
    std::int32_t GetAnimatedWindowRegionBuiltPhase();
    std::uint32_t GetAnimatedWindowRegionPhaseTransitionCount();
    std::uint32_t GetAnimatedWindowRegionSuccessfulPhaseApplyCount();
    bool WasAnimatedWindowRegionPhaseApplied(std::int32_t phase);
    std::uint64_t GetAnimatedWindowRegionPhaseLastAppliedGeneration(
        std::int32_t phase);
    std::uint32_t GetAnimatedWindowRegionMergedRectangleCount();
    std::uint32_t GetAnimatedWindowRegionRegionDataRectangleCount();
    std::uint32_t GetAnimatedWindowRegionPhaseRawRuns(std::int32_t phase);
    std::uint32_t GetAnimatedWindowRegionPhaseMerged(std::int32_t phase);
    std::uint32_t GetAnimatedWindowRegionPhaseFinal(std::int32_t phase);
    std::uint32_t GetAnimatedWindowRegionPhaseCovered(std::int32_t phase);
    std::uint64_t GetAnimatedWindowRegionLastBuildMicroseconds();
    std::uint64_t GetAnimatedWindowRegionMinimumBuildMicroseconds();
    std::uint64_t GetAnimatedWindowRegionMaximumBuildMicroseconds();
    std::uint64_t GetAnimatedWindowRegionAverageBuildMicroseconds();
    std::uint64_t GetAnimatedWindowRegionLastSetWindowRgnMicroseconds();
    std::uint64_t GetAnimatedWindowRegionMinimumSetWindowRgnMicroseconds();
    std::uint64_t GetAnimatedWindowRegionMaximumSetWindowRgnMicroseconds();
    std::uint64_t GetAnimatedWindowRegionAverageSetWindowRgnMicroseconds();
    std::uint32_t GetAnimatedWindowRegionMaximumPendingApplyMessageCount();
    std::uint32_t GetAnimatedWindowRegionMaximumPendingRegionCount();
    std::uint32_t GetAnimatedWindowRegionHrgnCreatedCount();
    std::uint32_t GetAnimatedWindowRegionHrgnCallerDeletedCount();
    std::uint32_t GetAnimatedWindowRegionHrgnOwnershipTransferredCount();
    std::uint32_t GetAnimatedWindowRegionHrgnValidationCopyDeletedCount();
    std::int32_t GetAnimatedWindowRegionHrgnLiveOwnedCount();
    std::uint32_t GetAnimatedWindowRegionMinimumAnimationGdiObjectCount();
    std::uint32_t GetAnimatedWindowRegionLastAnimationGdiObjectCount();
    bool DidAnimatedWindowRegionCounterInvariantsSucceed();
    bool DidAnimatedWindowRegionRepresentativeValidationSucceed();
    bool DidAnimatedWindowRegionAllPhasesApply();
    bool DidAnimatedWindowRegionPhaseComplexityDiffer();
    bool DidAnimatedWindowRegionHrgnOwnershipInvariantsSucceed();
}
