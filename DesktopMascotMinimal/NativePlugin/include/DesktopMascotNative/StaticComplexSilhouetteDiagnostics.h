#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kStaticComplexSilhouetteApplyMessage =
        0x8000u + 0x4Du;
    constexpr std::uint32_t kStaticComplexSilhouetteRestoreMessage =
        0x8000u + 0x4Eu;

    enum class StaticComplexSilhouetteState : std::int32_t
    {
        NotStarted = 0,
        WaitingForComposition = 1,
        WaitingForMask = 2,
        BuildingRegion = 3,
        PostingRegionApply = 4,
        WaitingForRegionApply = 5,
        VisualTestRunning = 6,
        StopRequested = 7,
        RestoringInitialRegion = 8,
        RestoringInitialStyle = 9,
        WaitingForContinuousPresent = 10,
        ShuttingDown = 11,
        Completed = 12,
        Stopped = 13,
        Failed = 14
    };

    enum class StaticComplexSilhouetteFailureStage : std::int32_t
    {
        None = 0,
        CompositionNotReady = 1,
        WindowUnavailable = 2,
        InvalidThreshold = 3,
        MaskUnavailable = 4,
        SnapshotCopyFailed = 5,
        RegionCreationFailed = 6,
        RegionCombineFailed = 7,
        RegionDataQueryFailed = 8,
        RepresentativePointValidationFailed = 9,
        RegionPixelCountValidationFailed = 10,
        ApplyMessagePostFailed = 11,
        SetWindowRgnFailed = 12,
        CounterInvariantFailed = 13,
        InitialRegionRestoreFailed = 14,
        InitialStyleRestoreFailed = 15,
        GdiObjectLeakDetected = 16,
        ContinuousPresentFailed = 17,
        DiagnosticsTimeout = 18,
        ShutdownFailed = 19,
        PerformanceThresholdExceeded = 20
    };

    void ResetStaticComplexSilhouetteDiagnostics();
    void SetStaticComplexSilhouetteUiWindow(void* window);
    void HandleStaticComplexSilhouetteApplyMessage();
    void HandleStaticComplexSilhouetteRestoreMessage();
    void StopStaticComplexSilhouetteDiagnosticsOnUiThread();
    void RecordStaticComplexSilhouettePostShutdownGdiCount();
    void RecordStaticComplexSilhouetteAfterWindowDestroyGdiCount();
    void RecordStaticComplexSilhouetteAfterUiThreadJoinGdiCount();
    void RecordStaticComplexSilhouetteAfterNativeCleanupGdiCount();
    bool ShouldStaticComplexSilhouetteReturnClient();

    std::int32_t StartStaticComplexSilhouetteDiagnostics(
        std::int32_t alphaThreshold);
    std::int32_t CompleteStaticComplexSilhouetteDiagnostics();
    std::int32_t StopStaticComplexSilhouetteDiagnostics();

    std::int32_t GetStaticComplexSilhouetteState();
    std::int32_t GetStaticComplexSilhouetteFailureStage();
    std::int32_t GetStaticComplexSilhouetteThreshold();
    std::uint64_t GetStaticComplexSilhouettePublishedGeneration();
    std::uint64_t GetStaticComplexSilhouetteAppliedGeneration();
    std::uint32_t GetStaticComplexSilhouetteGenerationEvaluationCount();
    std::uint32_t GetStaticComplexSilhouetteRegionBuildCount();
    std::uint32_t GetStaticComplexSilhouetteApplyMessagePostCount();
    std::uint32_t GetStaticComplexSilhouetteApplyExecutionCount();
    std::uint32_t GetStaticComplexSilhouetteApplySuccessCount();
    std::uint32_t GetStaticComplexSilhouetteApplyFailureCount();
    std::uint32_t GetStaticComplexSilhouetteDuplicateMaskSkipCount();
    std::uint32_t GetStaticComplexSilhouetteSupersededGenerationCount();
    std::uint32_t GetStaticComplexSilhouetteRawScanlineRunCount();
    std::uint32_t GetStaticComplexSilhouetteMergedRectangleCount();
    std::uint32_t GetStaticComplexSilhouetteRegionDataRectangleCount();
    std::int32_t GetStaticComplexSilhouetteRegionType();
    std::uint32_t GetStaticComplexSilhouetteCoveredPixelCount();
    std::uint32_t GetStaticComplexSilhouetteExcludedPixelCount();
    std::uint32_t GetStaticComplexSilhouetteRegionDataSizeBytes();
    std::uint64_t GetStaticComplexSilhouetteLastBuildMicroseconds();
    std::uint64_t GetStaticComplexSilhouetteMinimumBuildMicroseconds();
    std::uint64_t GetStaticComplexSilhouetteMaximumBuildMicroseconds();
    std::uint64_t GetStaticComplexSilhouetteAverageBuildMicroseconds();
    std::uint64_t GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds();
    std::uint64_t GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds();
    std::uint32_t GetStaticComplexSilhouetteInitialGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouettePreShutdownGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouettePostShutdownGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterRegionBuildGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterRegionApplyGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterRegionRestoreGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterWindowDestroyGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterUiThreadJoinGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteAfterNativeCleanupGdiObjectCount();
    std::uint32_t GetStaticComplexSilhouetteHrgnCreatedCount();
    std::uint32_t GetStaticComplexSilhouetteHrgnCallerDeletedCount();
    std::uint32_t GetStaticComplexSilhouetteHrgnOwnershipTransferredCount();
    std::uint32_t GetStaticComplexSilhouetteHrgnRestoreCreatedCount();
    std::int32_t GetStaticComplexSilhouetteHrgnLiveOwnedCount();
    std::int32_t GetStaticComplexSilhouettePreShutdownGdiDelta();
    std::int32_t GetStaticComplexSilhouettePostShutdownGdiDelta();
    bool DidStaticComplexSilhouetteCounterInvariantsSucceed();
    bool DidStaticComplexSilhouetteRepresentativeValidationSucceed();
    bool DidStaticComplexSilhouetteInitialRegionRestoreSucceed();
    bool DidStaticComplexSilhouetteInitialStyleRestoreSucceed();
    bool DidStaticComplexSilhouetteLastApplySucceed();
    std::uint32_t GetStaticComplexSilhouetteLastWin32Error();
    bool IsStaticComplexSilhouetteBodyCenterInside();
    bool IsStaticComplexSilhouetteLongLeftEarInside();
    bool IsStaticComplexSilhouetteMirroredEarInside();
    bool IsStaticComplexSilhouetteBottomRightTailInside();
    bool IsStaticComplexSilhouetteMirroredTailInside();
    bool IsStaticComplexSilhouetteTransparentCornerInside();
    bool IsStaticComplexSilhouetteTransparentBottomLeftCornerInside();
}
