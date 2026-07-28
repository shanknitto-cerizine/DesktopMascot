#pragma once

#include <cstdint>

namespace DesktopMascotNative
{
    constexpr std::uint32_t kNativeMascotContextMenuCancelMessage =
        0x8000u + 0x315u;

    enum class NativeMascotCommand : std::int32_t
    {
        None = 0,
        OpenSettings = 1,
        RequestExit = 2
    };

    enum class NativeApplicationCommandSource : std::int32_t
    {
        None = 0,
        MascotContextMenu = 1,
        SystemTray = 2,
        Diagnostic = 3
    };

    bool PublishNativeApplicationCommand(
        std::int32_t command,
        NativeApplicationCommandSource source);
    void ResetNativeMascotContextMenu();
    void SetNativeMascotContextMenuUiWindow(void* window);
    bool HandleNativeMascotContextMenuMessage(
        void* window,
        std::uint32_t message,
        std::intptr_t lParam,
        std::intptr_t& result);
    void NotifyNativeMascotContextMenuShutdownRequested();
    void StopNativeMascotContextMenuOnUiThread();

    std::int32_t EnableNativeMascotContextMenu();
    std::int32_t DisableNativeMascotContextMenu();
    std::uint64_t GetNativeMascotCommandGeneration();
    bool TryConsumeNativeMascotCommand(
        std::uint64_t& generation,
        std::int32_t& command);

    bool PublishNativeMascotCommandForDiagnostics(std::int32_t command);
    bool RunNativeMascotMenuResourceDiagnostic();
    std::uint32_t GetNativeMascotCommandPublishCount();
    std::uint32_t GetNativeMascotCommandConsumeCount();
    std::uint32_t GetNativeMascotCommandRejectedCount();
    std::int32_t GetNativeApplicationCommandLastSource();
    std::uint32_t GetNativeMascotMenuCreatedCount();
    std::uint32_t GetNativeMascotMenuDestroyedCount();
    std::int32_t GetNativeMascotMenuLiveOwnedCount();
    std::uint32_t GetNativeMascotMenuCancelledCount();
}
