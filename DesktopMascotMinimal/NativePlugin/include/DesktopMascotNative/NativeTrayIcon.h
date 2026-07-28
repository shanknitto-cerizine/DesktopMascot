#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    std::int32_t StartNativeTrayIcon();
    std::int32_t StopNativeTrayIcon();
    bool IsNativeTrayIconRunning();
    bool IsNativeTrayOwnerWindowAvailable();
    bool IsNativeTrayIconRegistered();
    bool IsNativeTrayPopupActive();
    bool WasNativeTrayTooltipConfigured();
    std::uint32_t GetNativeTrayInitialAddRequestCount();
    std::uint32_t GetNativeTraySetVersionRequestCount();
    std::uint32_t GetNativeTrayDeleteRequestCount();
    std::uint32_t GetNativeTrayReregisterRequestCount();
    std::uint32_t GetNativeTrayOwnerCreatedCount();
    std::uint32_t GetNativeTrayOwnerDestroyedCount();
    std::uint32_t GetNativeTrayMenuCreatedCount();
    std::uint32_t GetNativeTrayMenuDestroyedCount();
    std::int32_t GetNativeTrayMenuLiveOwnedCount();
    std::uint32_t GetNativeTrayIconCreatedCount();
    std::uint32_t GetNativeTrayIconDestroyedCount();
    std::int32_t GetNativeTrayIconLiveOwnedCount();
    std::uint32_t GetNativeTrayCancelledCount();
    std::uint32_t GetNativeTrayOpenSettingsSelectionCount();
    std::uint32_t GetNativeTrayRequestExitSelectionCount();
    std::uint32_t GetNativeTrayTaskbarCreatedCount();
    std::uint32_t GetNativeTrayShutdownRejectedCount();
    std::int32_t PublishNativeTrayCommandForDiagnostics(
        std::int32_t command);
    std::int32_t RunNativeTrayFocusedDiagnostic();
}
