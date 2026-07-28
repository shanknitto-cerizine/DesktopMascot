#include "DesktopMascotNative/NativeMascotContextMenu.h"

#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/NativeMascotWindowDrag.h"

#include <Windows.h>
#include <windowsx.h>

#include <atomic>
#include <mutex>

namespace
{
    using DesktopMascotNative::NativeMascotCommand;

    constexpr UINT kOpenSettingsCommandId = 0x5101;
    constexpr UINT kRequestExitCommandId = 0x5102;
    constexpr wchar_t kOpenSettingsLabel[] = L"\u8A2D\u5B9A";
    constexpr wchar_t kRequestExitLabel[] = L"\u7D42\u4E86";

    std::atomic<HWND> g_window{nullptr};
    std::atomic<bool> g_enabled{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<std::int32_t> g_pendingCommand{
        static_cast<std::int32_t>(NativeMascotCommand::None)};
    std::atomic<std::uint64_t> g_commandGeneration{0};
    std::atomic<std::uint32_t> g_publishCount{0};
    std::atomic<std::uint32_t> g_consumeCount{0};
    std::atomic<std::uint32_t> g_rejectedCount{0};
    std::atomic<std::uint32_t> g_menuCreatedCount{0};
    std::atomic<std::uint32_t> g_menuDestroyedCount{0};
    std::atomic<std::int32_t> g_menuLiveOwnedCount{0};
    std::atomic<std::uint32_t> g_menuCancelledCount{0};
    std::mutex g_commandMutex;

    bool IsValidCommand(std::int32_t value)
    {
        return value == static_cast<std::int32_t>(
                    NativeMascotCommand::OpenSettings)
            || value == static_cast<std::int32_t>(
                    NativeMascotCommand::RequestExit);
    }

    std::atomic<std::int32_t> g_lastCommandSource{
        static_cast<std::int32_t>(
            DesktopMascotNative::NativeApplicationCommandSource::None)};

    bool PublishCommand(
        std::int32_t command,
        DesktopMascotNative::NativeApplicationCommandSource source)
    {
        const std::lock_guard<std::mutex> lock(g_commandMutex);
        if (!IsValidCommand(command)
            || !g_enabled.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            g_rejectedCount.fetch_add(1, std::memory_order_relaxed);
            return false;
        }

        if (g_pendingCommand.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(NativeMascotCommand::None))
        {
            g_rejectedCount.fetch_add(1, std::memory_order_relaxed);
            return false;
        }
        g_commandGeneration.fetch_add(1, std::memory_order_acq_rel);
        g_lastCommandSource.store(
            static_cast<std::int32_t>(source),
            std::memory_order_release);
        g_pendingCommand.store(command, std::memory_order_release);
        g_publishCount.fetch_add(1, std::memory_order_relaxed);
        return true;
    }

    POINT ResolveMenuPosition(HWND window, LPARAM lParam)
    {
        if (lParam != -1)
        {
            return POINT{
                GET_X_LPARAM(lParam),
                GET_Y_LPARAM(lParam)};
        }

        RECT rectangle{};
        if (::GetWindowRect(window, &rectangle) != FALSE)
        {
            return POINT{
                rectangle.left + (rectangle.right - rectangle.left) / 2,
                rectangle.top + (rectangle.bottom - rectangle.top) / 2};
        }
        return POINT{};
    }

    void DestroyOwnedMenu(HMENU menu)
    {
        if (menu == nullptr)
            return;
        if (::DestroyMenu(menu) != FALSE)
        {
            g_menuDestroyedCount.fetch_add(1, std::memory_order_relaxed);
            g_menuLiveOwnedCount.fetch_sub(1, std::memory_order_relaxed);
        }
    }

    void ShowContextMenu(HWND window, LPARAM lParam)
    {
        HMENU menu = ::CreatePopupMenu();
        if (menu == nullptr)
            return;
        g_menuCreatedCount.fetch_add(1, std::memory_order_relaxed);
        g_menuLiveOwnedCount.fetch_add(1, std::memory_order_relaxed);

        if (::AppendMenuW(
                menu,
                MF_STRING,
                kOpenSettingsCommandId,
                kOpenSettingsLabel) == FALSE
            || ::AppendMenuW(
                menu,
                MF_STRING,
                kRequestExitCommandId,
                kRequestExitLabel) == FALSE)
        {
            DestroyOwnedMenu(menu);
            return;
        }

        const POINT position = ResolveMenuPosition(window, lParam);
        const HWND previousForegroundWindow = ::GetForegroundWindow();
        const bool foregroundReady =
            previousForegroundWindow == window
            || ::SetForegroundWindow(window) != FALSE;
        if (!foregroundReady)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Context menu owner could not become "
                L"the temporary foreground window.\n");
        }
        const UINT selected = ::TrackPopupMenuEx(
            menu,
            TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON,
            position.x,
            position.y,
            window,
            nullptr);
        ::PostMessageW(window, WM_NULL, 0, 0);
        DestroyOwnedMenu(menu);
        if (previousForegroundWindow != nullptr
            && previousForegroundWindow != window
            && ::IsWindow(previousForegroundWindow) != FALSE)
        {
            ::SetForegroundWindow(previousForegroundWindow);
        }

        if (selected == kOpenSettingsCommandId)
        {
            DesktopMascotNative::PublishNativeApplicationCommand(
                static_cast<std::int32_t>(
                    NativeMascotCommand::OpenSettings),
                DesktopMascotNative::
                    NativeApplicationCommandSource::MascotContextMenu);
        }
        else if (selected == kRequestExitCommandId)
        {
            DesktopMascotNative::PublishNativeApplicationCommand(
                static_cast<std::int32_t>(
                    NativeMascotCommand::RequestExit),
                DesktopMascotNative::
                    NativeApplicationCommandSource::MascotContextMenu);
        }
        else
        {
            g_menuCancelledCount.fetch_add(1, std::memory_order_relaxed);
        }
    }
}

namespace DesktopMascotNative
{
    bool PublishNativeApplicationCommand(
        std::int32_t command,
        NativeApplicationCommandSource source)
    {
        return PublishCommand(command, source);
    }

    void ResetNativeMascotContextMenu()
    {
        const std::lock_guard<std::mutex> lock(g_commandMutex);
        g_window.store(nullptr, std::memory_order_release);
        g_enabled.store(false, std::memory_order_release);
        g_shutdownRequested.store(false, std::memory_order_release);
        g_pendingCommand.store(
            static_cast<std::int32_t>(NativeMascotCommand::None),
            std::memory_order_release);
        g_commandGeneration.store(0, std::memory_order_release);
        g_publishCount.store(0, std::memory_order_release);
        g_consumeCount.store(0, std::memory_order_release);
        g_rejectedCount.store(0, std::memory_order_release);
        g_lastCommandSource.store(
            static_cast<std::int32_t>(
                NativeApplicationCommandSource::None),
            std::memory_order_release);
        g_menuCreatedCount.store(0, std::memory_order_release);
        g_menuDestroyedCount.store(0, std::memory_order_release);
        g_menuLiveOwnedCount.store(0, std::memory_order_release);
        g_menuCancelledCount.store(0, std::memory_order_release);
    }

    void SetNativeMascotContextMenuUiWindow(void* window)
    {
        g_window.store(static_cast<HWND>(window), std::memory_order_release);
    }

    bool HandleNativeMascotContextMenuMessage(
        void* windowValue,
        std::uint32_t message,
        std::intptr_t lParam,
        std::intptr_t& result)
    {
        if (message == kNativeMascotContextMenuCancelMessage)
        {
            ::EndMenu();
            result = 0;
            return true;
        }
        if (message != WM_CONTEXTMENU
            || !g_enabled.load(std::memory_order_acquire)
            || g_shutdownRequested.load(std::memory_order_acquire))
        {
            return false;
        }
        const HWND window = static_cast<HWND>(windowValue);
        if (window == nullptr
            || window != g_window.load(std::memory_order_acquire)
            || ::IsWindow(window) == FALSE)
        {
            return false;
        }
        if (IsNativeMascotWindowDragging()
            || IsNativeMascotWindowCaptureOwned())
        {
            result = 0;
            return true;
        }
        ShowContextMenu(window, static_cast<LPARAM>(lParam));
        result = 0;
        return true;
    }

    void NotifyNativeMascotContextMenuShutdownRequested()
    {
        const std::lock_guard<std::mutex> lock(g_commandMutex);
        g_shutdownRequested.store(true, std::memory_order_release);
        g_enabled.store(false, std::memory_order_release);
        g_pendingCommand.store(
            static_cast<std::int32_t>(NativeMascotCommand::None),
            std::memory_order_release);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window != nullptr && ::IsWindow(window) != FALSE)
        {
            ::PostMessageW(
                window,
                kNativeMascotContextMenuCancelMessage,
                0,
                0);
        }
    }

    void StopNativeMascotContextMenuOnUiThread()
    {
        NotifyNativeMascotContextMenuShutdownRequested();
        g_window.store(nullptr, std::memory_order_release);
    }

    std::int32_t EnableNativeMascotContextMenu()
    {
        const HWND window = g_window.load(std::memory_order_acquire);
        if (window == nullptr
            || ::IsWindow(window) == FALSE
            || GetCompositionInitializationState()
                != static_cast<std::int32_t>(
                    CompositionInitializationState::MessageLoopRunning))
        {
            return 0;
        }
        {
            const std::lock_guard<std::mutex> lock(g_commandMutex);
            g_pendingCommand.store(
                static_cast<std::int32_t>(NativeMascotCommand::None),
                std::memory_order_release);
            g_shutdownRequested.store(false, std::memory_order_release);
            g_enabled.store(true, std::memory_order_release);
        }
        return 1;
    }

    std::int32_t DisableNativeMascotContextMenu()
    {
        NotifyNativeMascotContextMenuShutdownRequested();
        return 1;
    }

    std::uint64_t GetNativeMascotCommandGeneration()
    {
        return g_commandGeneration.load(std::memory_order_acquire);
    }

    bool TryConsumeNativeMascotCommand(
        std::uint64_t& generation,
        std::int32_t& command)
    {
        const std::lock_guard<std::mutex> lock(g_commandMutex);
        command = g_pendingCommand.load(std::memory_order_acquire);
        if (!IsValidCommand(command))
        {
            command = static_cast<std::int32_t>(NativeMascotCommand::None);
            generation = g_commandGeneration.load(std::memory_order_acquire);
            return false;
        }
        generation = g_commandGeneration.load(std::memory_order_acquire);
        g_pendingCommand.store(
            static_cast<std::int32_t>(NativeMascotCommand::None),
            std::memory_order_release);
        g_consumeCount.fetch_add(1, std::memory_order_relaxed);
        return true;
    }

    bool PublishNativeMascotCommandForDiagnostics(std::int32_t command)
    {
        return PublishNativeApplicationCommand(
            command,
            NativeApplicationCommandSource::Diagnostic);
    }

    bool RunNativeMascotMenuResourceDiagnostic()
    {
        const auto createdBefore =
            g_menuCreatedCount.load(std::memory_order_acquire);
        const auto destroyedBefore =
            g_menuDestroyedCount.load(std::memory_order_acquire);
        HMENU menu = ::CreatePopupMenu();
        if (menu == nullptr)
            return false;
        g_menuCreatedCount.fetch_add(1, std::memory_order_relaxed);
        g_menuLiveOwnedCount.fetch_add(1, std::memory_order_relaxed);
        const bool appended =
            ::AppendMenuW(
                menu,
                MF_STRING,
                kOpenSettingsCommandId,
                kOpenSettingsLabel) != FALSE
            && ::AppendMenuW(
                menu,
                MF_STRING,
                kRequestExitCommandId,
                kRequestExitLabel) != FALSE;
        DestroyOwnedMenu(menu);
        return appended
            && g_menuCreatedCount.load(std::memory_order_acquire)
                == createdBefore + 1
            && g_menuDestroyedCount.load(std::memory_order_acquire)
                == destroyedBefore + 1
            && g_menuLiveOwnedCount.load(std::memory_order_acquire) == 0;
    }

    std::uint32_t GetNativeMascotCommandPublishCount()
    {
        return g_publishCount.load(std::memory_order_acquire);
    }

    std::uint32_t GetNativeMascotCommandConsumeCount()
    {
        return g_consumeCount.load(std::memory_order_acquire);
    }

    std::uint32_t GetNativeMascotCommandRejectedCount()
    {
        return g_rejectedCount.load(std::memory_order_acquire);
    }

    std::int32_t GetNativeApplicationCommandLastSource()
    {
        return g_lastCommandSource.load(std::memory_order_acquire);
    }

    std::uint32_t GetNativeMascotMenuCreatedCount()
    {
        return g_menuCreatedCount.load(std::memory_order_acquire);
    }

    std::uint32_t GetNativeMascotMenuDestroyedCount()
    {
        return g_menuDestroyedCount.load(std::memory_order_acquire);
    }

    std::int32_t GetNativeMascotMenuLiveOwnedCount()
    {
        return g_menuLiveOwnedCount.load(std::memory_order_acquire);
    }

    std::uint32_t GetNativeMascotMenuCancelledCount()
    {
        return g_menuCancelledCount.load(std::memory_order_acquire);
    }
}
