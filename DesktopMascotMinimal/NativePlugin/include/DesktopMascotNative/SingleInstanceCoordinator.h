#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    enum class SingleInstanceStartupResult : std::int32_t
    {
        Failure = -1,
        Primary = 1,
        SecondarySignalSent = 2,
        SecondarySignalFailed = 3,
    };

    std::int32_t InitializeSingleInstance();
    std::int32_t BeginSingleInstanceShutdown();
    std::int32_t ShutdownSingleInstance();
    bool TryConsumeSingleInstanceActivation(std::uint64_t& generation);
    std::int32_t RunSingleInstanceCoalescingFocusedDiagnostic();

    bool IsSingleInstancePrimary();
    bool IsSingleInstanceNotificationReady();
    bool IsSingleInstanceAcceptingActivations();
    bool IsSingleInstanceActivationPending();
    std::uint64_t GetSingleInstanceActivationGeneration();
    std::uint32_t GetSingleInstanceSignalReceivedCount();
    std::uint32_t GetSingleInstanceGenerationPublishCount();
    std::uint32_t GetSingleInstanceActivationConsumeCount();
    std::uint32_t GetSingleInstanceActivationCoalescedCount();
    std::uint32_t GetSingleInstanceActivationRejectedCount();
    std::uint32_t GetSingleInstanceMaximumPendingCount();
    std::uint32_t GetSingleInstanceNotificationCreatedCount();
    std::uint32_t GetSingleInstanceNotificationDestroyedCount();
    std::int32_t GetSingleInstanceFailureStage();
}
