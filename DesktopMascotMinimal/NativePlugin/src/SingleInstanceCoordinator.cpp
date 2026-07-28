#include "DesktopMascotNative/SingleInstanceCoordinator.h"

#include <Windows.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <mutex>
#include <thread>

namespace
{
    constexpr wchar_t kMutexName[] =
        L"Local\\DesktopMascotMinimal.SingleInstance.v1";
    constexpr wchar_t kActivationEventName[] =
        L"Local\\DesktopMascotMinimal.SingleInstance.OpenSettings.v1";
    constexpr wchar_t kReadyEventName[] =
        L"Local\\DesktopMascotMinimal.SingleInstance.Ready.v1";
    constexpr wchar_t kShutdownEventName[] =
        L"Local\\DesktopMascotMinimal.SingleInstance.Shutdown.v1";
    constexpr int kSecondarySignalAttemptCount = 80;
    constexpr auto kSecondarySignalRetryInterval =
        std::chrono::milliseconds(25);

    std::mutex g_lifecycleMutex;
    HANDLE g_instanceMutex = nullptr;
    HANDLE g_activationEvent = nullptr;
    HANDLE g_readyEvent = nullptr;
    HANDLE g_shutdownEvent = nullptr;
    std::atomic<bool> g_primary{false};
    std::atomic<bool> g_mutexOwned{false};
    std::atomic<bool> g_notificationReady{false};
    std::atomic<bool> g_acceptingActivations{false};
    std::atomic<bool> g_activationPending{false};
    std::atomic<std::uint64_t> g_activationGeneration{0};
    std::atomic<std::uint32_t> g_signalReceivedCount{0};
    std::atomic<std::uint32_t> g_generationPublishCount{0};
    std::atomic<std::uint32_t> g_activationConsumeCount{0};
    std::atomic<std::uint32_t> g_activationCoalescedCount{0};
    std::atomic<std::uint32_t> g_activationRejectedCount{0};
    std::atomic<std::uint32_t> g_maximumPendingCount{0};
    std::atomic<std::uint32_t> g_channelCreatedCount{0};
    std::atomic<std::uint32_t> g_channelDestroyedCount{0};
    std::atomic<std::int32_t> g_failureStage{0};

    void Fail(std::int32_t stage)
    {
        std::int32_t expected = 0;
        g_failureStage.compare_exchange_strong(expected, stage);
    }

    void ResetDiagnostics()
    {
        g_activationPending.store(false);
        g_activationGeneration.store(0);
        g_signalReceivedCount.store(0);
        g_generationPublishCount.store(0);
        g_activationConsumeCount.store(0);
        g_activationCoalescedCount.store(0);
        g_activationRejectedCount.store(0);
        g_maximumPendingCount.store(0);
        g_channelCreatedCount.store(0);
        g_channelDestroyedCount.store(0);
        g_failureStage.store(0);
    }

    bool PublishLogicalActivation()
    {
        g_signalReceivedCount.fetch_add(1);
        if (!g_acceptingActivations.load(std::memory_order_acquire))
        {
            g_activationRejectedCount.fetch_add(1);
            return false;
        }
        if (!g_activationPending.exchange(
                true,
                std::memory_order_acq_rel))
        {
            g_activationGeneration.fetch_add(
                1,
                std::memory_order_acq_rel);
            g_generationPublishCount.fetch_add(1);
            g_maximumPendingCount.store(1);
        }
        else
        {
            g_activationCoalescedCount.fetch_add(1);
        }
        return true;
    }

    void CloseHandleIfPresent(HANDLE& handle)
    {
        if (handle != nullptr)
            ::CloseHandle(handle);
        handle = nullptr;
    }

    void CloseNotificationChannel()
    {
        const bool existed =
            g_activationEvent != nullptr
            || g_readyEvent != nullptr
            || g_shutdownEvent != nullptr;
        CloseHandleIfPresent(g_activationEvent);
        CloseHandleIfPresent(g_readyEvent);
        CloseHandleIfPresent(g_shutdownEvent);
        g_notificationReady.store(false);
        if (existed)
            g_channelDestroyedCount.fetch_add(1);
    }

    bool CreateNotificationChannel()
    {
        // Auto-reset is the kernel-level pending latch. Repeated SetEvent calls
        // before a wait completes remain one logical OpenSettings request.
        g_activationEvent = ::CreateEventW(
            nullptr,
            FALSE,
            FALSE,
            kActivationEventName);
        g_readyEvent = ::CreateEventW(
            nullptr,
            TRUE,
            TRUE,
            kReadyEventName);
        g_shutdownEvent = ::CreateEventW(
            nullptr,
            TRUE,
            FALSE,
            kShutdownEventName);
        if (g_activationEvent == nullptr
            || g_readyEvent == nullptr
            || g_shutdownEvent == nullptr)
        {
            Fail(3);
            CloseNotificationChannel();
            return false;
        }
        g_channelCreatedCount.fetch_add(1);
        g_notificationReady.store(true);
        g_acceptingActivations.store(true);
        return true;
    }

    bool SignalPrimaryWithDeadline()
    {
        for (int attempt = 0;
             attempt < kSecondarySignalAttemptCount;
             ++attempt)
        {
            HANDLE ready = ::OpenEventW(
                SYNCHRONIZE,
                FALSE,
                kReadyEventName);
            HANDLE shutdown = ::OpenEventW(
                SYNCHRONIZE,
                FALSE,
                kShutdownEventName);
            HANDLE activation = ::OpenEventW(
                EVENT_MODIFY_STATE,
                FALSE,
                kActivationEventName);
            const bool readyNow =
                ready != nullptr
                && ::WaitForSingleObject(ready, 0) == WAIT_OBJECT_0;
            const bool shuttingDown =
                shutdown != nullptr
                && ::WaitForSingleObject(shutdown, 0) == WAIT_OBJECT_0;
            bool signalled = false;
            if (readyNow
                && !shuttingDown
                && activation != nullptr)
            {
                signalled = ::SetEvent(activation) != FALSE;
                if (signalled
                    && shutdown != nullptr
                    && ::WaitForSingleObject(shutdown, 0)
                        == WAIT_OBJECT_0)
                {
                    signalled = false;
                }
            }
            CloseHandleIfPresent(activation);
            CloseHandleIfPresent(shutdown);
            CloseHandleIfPresent(ready);
            if (signalled)
                return true;
            std::this_thread::sleep_for(
                kSecondarySignalRetryInterval);
        }
        Fail(7);
        return false;
    }
}

namespace DesktopMascotNative
{
    std::int32_t InitializeSingleInstance()
    {
        std::lock_guard lock(g_lifecycleMutex);
        if (g_primary.load())
            return static_cast<std::int32_t>(
                SingleInstanceStartupResult::Primary);

        ResetDiagnostics();
        HANDLE instanceMutex =
            ::CreateMutexW(nullptr, FALSE, kMutexName);
        if (instanceMutex == nullptr)
        {
            Fail(1);
            return static_cast<std::int32_t>(
                SingleInstanceStartupResult::Failure);
        }
        const DWORD waitResult =
            ::WaitForSingleObject(instanceMutex, 0);
        if (waitResult == WAIT_OBJECT_0
            || waitResult == WAIT_ABANDONED)
        {
            g_instanceMutex = instanceMutex;
            g_mutexOwned.store(true);
            g_primary.store(true);
            if (!CreateNotificationChannel())
            {
                ::ReleaseMutex(g_instanceMutex);
                CloseHandleIfPresent(g_instanceMutex);
                g_mutexOwned.store(false);
                g_primary.store(false);
                return static_cast<std::int32_t>(
                    SingleInstanceStartupResult::Failure);
            }
            return static_cast<std::int32_t>(
                SingleInstanceStartupResult::Primary);
        }
        if (waitResult != WAIT_TIMEOUT)
        {
            ::CloseHandle(instanceMutex);
            Fail(2);
            return static_cast<std::int32_t>(
                SingleInstanceStartupResult::Failure);
        }

        const bool signalled = SignalPrimaryWithDeadline();
        ::CloseHandle(instanceMutex);
        return static_cast<std::int32_t>(
            signalled
                ? SingleInstanceStartupResult::SecondarySignalSent
                : SingleInstanceStartupResult::SecondarySignalFailed);
    }

    std::int32_t BeginSingleInstanceShutdown()
    {
        if (!g_primary.load())
            return 1;
        g_acceptingActivations.store(false);
        g_activationPending.store(false);
        if (g_shutdownEvent != nullptr)
            ::SetEvent(g_shutdownEvent);
        if (g_readyEvent != nullptr)
            ::ResetEvent(g_readyEvent);
        return 1;
    }

    std::int32_t ShutdownSingleInstance()
    {
        std::lock_guard lock(g_lifecycleMutex);
        if (!g_primary.load())
            return 1;
        BeginSingleInstanceShutdown();
        CloseNotificationChannel();

        bool mutexReleased = true;
        if (g_instanceMutex != nullptr && g_mutexOwned.load())
        {
            mutexReleased =
                ::ReleaseMutex(g_instanceMutex) != FALSE;
            if (!mutexReleased)
                Fail(9);
        }
        CloseHandleIfPresent(g_instanceMutex);
        g_mutexOwned.store(false);
        g_primary.store(false);
        return mutexReleased ? 1 : 0;
    }

    bool TryConsumeSingleInstanceActivation(std::uint64_t& generation)
    {
        if (!g_primary.load()
            || !g_acceptingActivations.load()
            || g_activationEvent == nullptr
            || ::WaitForSingleObject(g_activationEvent, 0)
                != WAIT_OBJECT_0
            || !PublishLogicalActivation())
        {
            return false;
        }
        if (!g_activationPending.exchange(
                false,
                std::memory_order_acq_rel))
        {
            return false;
        }
        generation =
            g_activationGeneration.load(std::memory_order_acquire);
        g_activationConsumeCount.fetch_add(1);
        return true;
    }

    std::int32_t RunSingleInstanceCoalescingFocusedDiagnostic()
    {
        if (g_primary.load())
            return 0;
        ResetDiagnostics();
        g_primary.store(true);
        g_acceptingActivations.store(true);
        for (int index = 0; index < 10; ++index)
            PublishLogicalActivation();
        const auto firstGeneration =
            g_activationGeneration.load();
        const bool firstConsumed =
            g_activationPending.exchange(false);
        if (firstConsumed)
            g_activationConsumeCount.fetch_add(1);
        PublishLogicalActivation();
        const auto secondGeneration =
            g_activationGeneration.load();
        const bool secondConsumed =
            g_activationPending.exchange(false);
        if (secondConsumed)
            g_activationConsumeCount.fetch_add(1);
        BeginSingleInstanceShutdown();
        const bool rejected = !PublishLogicalActivation();
        const bool passed =
            firstConsumed
            && secondConsumed
            && firstGeneration == 1
            && secondGeneration == 2
            && g_signalReceivedCount.load() == 12
            && g_generationPublishCount.load() == 2
            && g_activationConsumeCount.load() == 2
            && g_activationCoalescedCount.load() == 9
            && g_activationRejectedCount.load() == 1
            && g_maximumPendingCount.load() == 1
            && rejected
            && !g_activationPending.load();
        g_acceptingActivations.store(false);
        g_primary.store(false);
        return passed ? 1 : 0;
    }

    bool IsSingleInstancePrimary() { return g_primary.load(); }
    bool IsSingleInstanceNotificationReady()
    {
        return g_notificationReady.load();
    }
    bool IsSingleInstanceAcceptingActivations()
    {
        return g_acceptingActivations.load();
    }
    bool IsSingleInstanceActivationPending()
    {
        return g_activationPending.load();
    }
    std::uint64_t GetSingleInstanceActivationGeneration()
    {
        return g_activationGeneration.load();
    }

#define DMN_SINGLE_INSTANCE_GET_U32(name, value) \
    std::uint32_t name() { return value.load(); }
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceSignalReceivedCount,
        g_signalReceivedCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceGenerationPublishCount,
        g_generationPublishCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceActivationConsumeCount,
        g_activationConsumeCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceActivationCoalescedCount,
        g_activationCoalescedCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceActivationRejectedCount,
        g_activationRejectedCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceMaximumPendingCount,
        g_maximumPendingCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceNotificationCreatedCount,
        g_channelCreatedCount)
    DMN_SINGLE_INSTANCE_GET_U32(
        GetSingleInstanceNotificationDestroyedCount,
        g_channelDestroyedCount)
#undef DMN_SINGLE_INSTANCE_GET_U32

    std::int32_t GetSingleInstanceFailureStage()
    {
        return g_failureStage.load();
    }
}
