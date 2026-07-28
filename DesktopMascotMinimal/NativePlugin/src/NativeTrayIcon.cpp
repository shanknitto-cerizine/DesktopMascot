#include "DesktopMascotNative/NativeTrayIcon.h"

#include "DesktopMascotNative/NativeMascotContextMenu.h"

#include <Windows.h>
#include <shellapi.h>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <mutex>
#include <thread>

namespace
{
    using DesktopMascotNative::NativeApplicationCommandSource;
    using DesktopMascotNative::NativeMascotCommand;

    constexpr wchar_t kOwnerClassName[] =
        L"DesktopMascotMinimal.NativeTrayOwner";
    constexpr wchar_t kTooltip[] =
        L"\u3042\u306A\u305F\u3068\u3044\u3064\u3082";
    constexpr wchar_t kOpenSettingsLabel[] = L"\u8A2D\u5B9A";
    constexpr wchar_t kRequestExitLabel[] = L"\u7D42\u4E86";
    constexpr UINT kTrayCallbackMessage = WM_APP + 0x360;
    constexpr UINT kCancelPopupMessage = WM_APP + 0x361;
    constexpr UINT kSimulateRegistrationFailureMessage = WM_APP + 0x362;
    constexpr UINT kOpenSettingsCommandId = 0x5201;
    constexpr UINT kRequestExitCommandId = 0x5202;
    constexpr auto kRegistrationRetryInterval =
        std::chrono::milliseconds(200);
    constexpr std::uint32_t kMaximumRegistrationRetryCount = 75;
    constexpr GUID kTrayIconGuid{
        0x7e347aa1,
        0xcace,
        0x48b6,
        {0xa7, 0xb4, 0xb2, 0x45, 0xc1, 0x2d, 0x36, 0x51}};

    std::mutex g_threadMutex;
    std::thread g_thread;
    HANDLE g_stopEvent = nullptr;
    HANDLE g_readyEvent = nullptr;
    HANDLE g_stoppedEvent = nullptr;
    std::atomic<HWND> g_ownerWindow{nullptr};
    std::atomic<bool> g_running{false};
    std::atomic<bool> g_shutdownRequested{false};
    std::atomic<bool> g_iconRegistered{false};
    std::atomic<bool> g_popupActive{false};
    std::atomic<bool> g_tooltipConfigured{false};
    std::atomic<UINT> g_taskbarCreatedMessage{0};
    std::atomic<std::uint32_t> g_initialAddRequestCount{0};
    std::atomic<std::uint32_t> g_setVersionRequestCount{0};
    std::atomic<std::uint32_t> g_deleteRequestCount{0};
    std::atomic<std::uint32_t> g_reregisterRequestCount{0};
    std::atomic<std::uint32_t> g_ownerCreatedCount{0};
    std::atomic<std::uint32_t> g_ownerDestroyedCount{0};
    std::atomic<std::uint32_t> g_menuCreatedCount{0};
    std::atomic<std::uint32_t> g_menuDestroyedCount{0};
    std::atomic<std::int32_t> g_menuLiveOwnedCount{0};
    std::atomic<std::uint32_t> g_iconCreatedCount{0};
    std::atomic<std::uint32_t> g_iconDestroyedCount{0};
    std::atomic<std::int32_t> g_iconLiveOwnedCount{0};
    std::atomic<std::uint32_t> g_cancelledCount{0};
    std::atomic<std::uint32_t> g_openSettingsSelectionCount{0};
    std::atomic<std::uint32_t> g_requestExitSelectionCount{0};
    std::atomic<std::uint32_t> g_taskbarCreatedCount{0};
    std::atomic<std::uint32_t> g_shutdownRejectedCount{0};
    std::atomic<std::uint32_t> g_nimAddAttemptCount{0};
    std::atomic<std::uint32_t> g_nimAddSuccessCount{0};
    std::atomic<DWORD> g_nimAddLastError{ERROR_SUCCESS};
    std::atomic<std::uint32_t> g_nimSetVersionSuccessCount{0};
    std::atomic<DWORD> g_nimSetVersionLastError{ERROR_SUCCESS};
    std::atomic<std::uint32_t> g_registrationRetryCount{0};
    std::atomic<std::uint32_t> g_shutdownRetrySuppressedCount{0};
    std::atomic<bool> g_registrationFinalResult{false};
    std::atomic<bool> g_registrationRetryExhausted{false};
    std::atomic<bool> g_threadInfrastructureReady{false};

    bool g_registrationRetryPending = false;
    std::uint32_t g_registrationRetryWindowCount = 0;
    std::chrono::steady_clock::time_point g_nextRegistrationRetry{};
    std::chrono::steady_clock::time_point g_registrationRetryDeadline{};

    enum class RegistrationRequestKind
    {
        Initial,
        Retry,
        TaskbarCreated
    };

    void ResetDiagnostics()
    {
        g_initialAddRequestCount.store(0);
        g_setVersionRequestCount.store(0);
        g_deleteRequestCount.store(0);
        g_reregisterRequestCount.store(0);
        g_ownerCreatedCount.store(0);
        g_ownerDestroyedCount.store(0);
        g_menuCreatedCount.store(0);
        g_menuDestroyedCount.store(0);
        g_menuLiveOwnedCount.store(0);
        g_iconCreatedCount.store(0);
        g_iconDestroyedCount.store(0);
        g_iconLiveOwnedCount.store(0);
        g_cancelledCount.store(0);
        g_openSettingsSelectionCount.store(0);
        g_requestExitSelectionCount.store(0);
        g_taskbarCreatedCount.store(0);
        g_shutdownRejectedCount.store(0);
        g_nimAddAttemptCount.store(0);
        g_nimAddSuccessCount.store(0);
        g_nimAddLastError.store(ERROR_SUCCESS);
        g_nimSetVersionSuccessCount.store(0);
        g_nimSetVersionLastError.store(ERROR_SUCCESS);
        g_registrationRetryCount.store(0);
        g_shutdownRetrySuppressedCount.store(0);
        g_registrationFinalResult.store(false);
        g_registrationRetryExhausted.store(false);
        g_threadInfrastructureReady.store(false);
        g_tooltipConfigured.store(false);
        g_registrationRetryPending = false;
        g_registrationRetryWindowCount = 0;
        g_nextRegistrationRetry = {};
        g_registrationRetryDeadline = {};
    }

    HICON CreateOwnedApplicationIcon()
    {
        constexpr int size = 32;
        BITMAPV5HEADER header{};
        header.bV5Size = sizeof(header);
        header.bV5Width = size;
        header.bV5Height = -size;
        header.bV5Planes = 1;
        header.bV5BitCount = 32;
        header.bV5Compression = BI_BITFIELDS;
        header.bV5RedMask = 0x00ff0000;
        header.bV5GreenMask = 0x0000ff00;
        header.bV5BlueMask = 0x000000ff;
        header.bV5AlphaMask = 0xff000000;

        void* bits = nullptr;
        HDC screen = ::GetDC(nullptr);
        HBITMAP color = ::CreateDIBSection(
            screen,
            reinterpret_cast<BITMAPINFO*>(&header),
            DIB_RGB_COLORS,
            &bits,
            nullptr,
            0);
        if (screen != nullptr)
            ::ReleaseDC(nullptr, screen);
        if (color == nullptr || bits == nullptr)
            return nullptr;

        auto* pixels = static_cast<std::uint32_t*>(bits);
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                const int dx = x - 15;
                const int dy = y - 15;
                const bool body = dx * dx + dy * dy <= 13 * 13;
                const bool face = dx * dx + (dy + 2) * (dy + 2) <= 8 * 8;
                const bool accent = y >= 20 && y <= 25 && x >= 8 && x <= 23;
                pixels[y * size + x] = accent
                    ? 0xffff4fd8u
                    : face
                        ? 0xffffd26au
                        : body
                            ? 0xff54d5f7u
                            : 0x00000000u;
            }
        }

        HBITMAP mask = ::CreateBitmap(size, size, 1, 1, nullptr);
        ICONINFO info{};
        info.fIcon = TRUE;
        info.hbmColor = color;
        info.hbmMask = mask;
        HICON icon = mask != nullptr ? ::CreateIconIndirect(&info) : nullptr;
        if (mask != nullptr)
            ::DeleteObject(mask);
        ::DeleteObject(color);
        if (icon != nullptr)
        {
            g_iconCreatedCount.fetch_add(1);
            g_iconLiveOwnedCount.fetch_add(1);
        }
        return icon;
    }

    void DestroyOwnedIcon(HICON icon)
    {
        if (icon != nullptr && ::DestroyIcon(icon) != FALSE)
        {
            g_iconDestroyedCount.fetch_add(1);
            g_iconLiveOwnedCount.fetch_sub(1);
        }
    }

    NOTIFYICONDATAW BuildNotifyIconData(HWND window, HICON icon)
    {
        NOTIFYICONDATAW data{};
        data.cbSize = sizeof(data);
        data.hWnd = window;
        data.uFlags =
            NIF_GUID | NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        data.guidItem = kTrayIconGuid;
        data.uCallbackMessage = kTrayCallbackMessage;
        data.hIcon = icon;
        if (::wcscpy_s(data.szTip, kTooltip) == 0)
            g_tooltipConfigured.store(true);
        return data;
    }

    void PublishStartResult(bool succeeded)
    {
        g_registrationFinalResult.store(succeeded);
    }

    void CancelRegistrationRetry()
    {
        g_registrationRetryPending = false;
        g_registrationRetryWindowCount = 0;
    }

    void ScheduleRegistrationRetry()
    {
        if (g_shutdownRequested.load())
        {
            g_shutdownRetrySuppressedCount.fetch_add(1);
            return;
        }
        const auto now = std::chrono::steady_clock::now();
        g_registrationRetryPending = true;
        g_registrationRetryWindowCount = 0;
        g_nextRegistrationRetry = now + kRegistrationRetryInterval;
        g_registrationRetryDeadline =
            now
            + kRegistrationRetryInterval
                * kMaximumRegistrationRetryCount;
    }

    bool AddTrayIcon(
        HWND window,
        HICON icon,
        RegistrationRequestKind requestKind)
    {
        if (g_shutdownRequested.load() || window == nullptr || icon == nullptr)
        {
            g_shutdownRejectedCount.fetch_add(1);
            return false;
        }
        switch (requestKind)
        {
            case RegistrationRequestKind::Initial:
                g_initialAddRequestCount.fetch_add(1);
                break;
            case RegistrationRequestKind::Retry:
                g_registrationRetryCount.fetch_add(1);
                break;
            case RegistrationRequestKind::TaskbarCreated:
                g_reregisterRequestCount.fetch_add(1);
                break;
        }

        auto data = BuildNotifyIconData(window, icon);
        g_nimAddAttemptCount.fetch_add(1);
        ::SetLastError(ERROR_SUCCESS);
        if (::Shell_NotifyIconW(NIM_ADD, &data) == FALSE)
        {
            g_nimAddLastError.store(::GetLastError());
            g_iconRegistered.store(false);
            g_running.store(false);
            g_registrationFinalResult.store(false);
            return false;
        }
        g_nimAddSuccessCount.fetch_add(1);
        data.uVersion = NOTIFYICON_VERSION_4;
        g_setVersionRequestCount.fetch_add(1);
        ::SetLastError(ERROR_SUCCESS);
        if (::Shell_NotifyIconW(NIM_SETVERSION, &data) == FALSE)
        {
            g_nimSetVersionLastError.store(::GetLastError());
            ::Shell_NotifyIconW(NIM_DELETE, &data);
            g_iconRegistered.store(false);
            g_running.store(false);
            g_registrationFinalResult.store(false);
            return false;
        }
        g_nimSetVersionSuccessCount.fetch_add(1);
        g_iconRegistered.store(true);
        g_running.store(true);
        g_registrationFinalResult.store(true);
        return true;
    }

    void CompleteRegistrationAttempt(bool succeeded)
    {
        if (succeeded)
        {
            CancelRegistrationRetry();
            PublishStartResult(true);
            return;
        }
        ScheduleRegistrationRetry();
    }

    void ProcessRegistrationRetry(HWND window, HICON icon)
    {
        if (!g_registrationRetryPending)
            return;
        if (g_shutdownRequested.load())
        {
            g_registrationRetryPending = false;
            g_shutdownRetrySuppressedCount.fetch_add(1);
            return;
        }

        const auto now = std::chrono::steady_clock::now();
        if (g_registrationRetryWindowCount
                >= kMaximumRegistrationRetryCount
            || now >= g_registrationRetryDeadline)
        {
            g_registrationRetryPending = false;
            g_registrationRetryExhausted.store(true);
            PublishStartResult(false);
            return;
        }

        ++g_registrationRetryWindowCount;
        const bool succeeded = AddTrayIcon(
            window,
            icon,
            RegistrationRequestKind::Retry);
        if (succeeded)
        {
            CancelRegistrationRetry();
            PublishStartResult(true);
            return;
        }
        g_nextRegistrationRetry =
            std::chrono::steady_clock::now()
            + kRegistrationRetryInterval;
        if (g_registrationRetryWindowCount
            >= kMaximumRegistrationRetryCount)
        {
            g_registrationRetryPending = false;
            g_registrationRetryExhausted.store(true);
            PublishStartResult(false);
        }
    }

    DWORD GetRegistrationWaitTimeout()
    {
        if (!g_registrationRetryPending)
            return INFINITE;
        const auto now = std::chrono::steady_clock::now();
        if (now >= g_nextRegistrationRetry)
            return 0;
        const auto remaining =
            std::chrono::duration_cast<std::chrono::milliseconds>(
                g_nextRegistrationRetry - now);
        return static_cast<DWORD>(remaining.count());
    }

    void DeleteTrayIcon(HWND window, HICON icon)
    {
        if (!g_iconRegistered.exchange(false))
            return;
        auto data = BuildNotifyIconData(window, icon);
        g_deleteRequestCount.fetch_add(1);
        ::Shell_NotifyIconW(NIM_DELETE, &data);
    }

    bool ShouldReregisterAfterTaskbarCreated(
        bool shutdownRequested,
        bool ownerAvailable)
    {
        return !shutdownRequested && ownerAvailable;
    }

    bool SimulateExplorerRestartForDiagnostics()
    {
        const HWND window = g_ownerWindow.load();
        if (window == nullptr || ::IsWindow(window) == FALSE)
            return false;
        const auto icon = reinterpret_cast<HICON>(
            ::GetWindowLongPtrW(window, GWLP_USERDATA));
        auto data = BuildNotifyIconData(window, icon);
        ::Shell_NotifyIconW(NIM_DELETE, &data);
        g_iconRegistered.store(false);
        const auto handledBefore = g_taskbarCreatedCount.load();
        const auto reregisteredBefore = g_reregisterRequestCount.load();
        if (::PostMessageW(
                window,
                g_taskbarCreatedMessage.load(),
                0,
                0) == FALSE)
        {
            return false;
        }
        for (int attempt = 0; attempt < 100; ++attempt)
        {
            if (g_taskbarCreatedCount.load() == handledBefore + 1
                && g_reregisterRequestCount.load() == reregisteredBefore + 1
                && g_iconRegistered.load())
            {
                return true;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        return false;
    }

    bool SimulateRegistrationFailureForDiagnostics()
    {
        const HWND window = g_ownerWindow.load();
        if (window == nullptr || ::IsWindow(window) == FALSE)
            return false;
        const auto retryCountBefore = g_registrationRetryCount.load();
        if (::PostMessageW(
                window,
                kSimulateRegistrationFailureMessage,
                0,
                0) == FALSE)
        {
            return false;
        }
        for (int attempt = 0; attempt < 100; ++attempt)
        {
            if (g_registrationRetryCount.load() == retryCountBefore + 1
                && g_iconRegistered.load()
                && g_registrationFinalResult.load())
            {
                return true;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        return false;
    }

    void DestroyOwnedMenu(HMENU menu)
    {
        if (menu != nullptr && ::DestroyMenu(menu) != FALSE)
        {
            g_menuDestroyedCount.fetch_add(1);
            g_menuLiveOwnedCount.fetch_sub(1);
        }
    }

    bool PublishTrayCommand(NativeMascotCommand command)
    {
        if (g_shutdownRequested.load())
        {
            g_shutdownRejectedCount.fetch_add(1);
            return false;
        }
        return DesktopMascotNative::PublishNativeApplicationCommand(
            static_cast<std::int32_t>(command),
            NativeApplicationCommandSource::SystemTray);
    }

    void ShowTrayMenu(HWND window, POINT position)
    {
        HMENU menu = ::CreatePopupMenu();
        if (menu == nullptr)
            return;
        g_menuCreatedCount.fetch_add(1);
        g_menuLiveOwnedCount.fetch_add(1);
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

        const HWND previousForegroundWindow = ::GetForegroundWindow();
        ::SetForegroundWindow(window);
        g_popupActive.store(true);
        const UINT selected = ::TrackPopupMenuEx(
            menu,
            TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON,
            position.x,
            position.y,
            window,
            nullptr);
        g_popupActive.store(false);
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
            g_openSettingsSelectionCount.fetch_add(1);
            PublishTrayCommand(NativeMascotCommand::OpenSettings);
        }
        else if (selected == kRequestExitCommandId)
        {
            g_requestExitSelectionCount.fetch_add(1);
            PublishTrayCommand(NativeMascotCommand::RequestExit);
        }
        else
        {
            g_cancelledCount.fetch_add(1);
        }
    }

    LRESULT CALLBACK TrayWindowProcedure(
        HWND window,
        UINT message,
        WPARAM wParam,
        LPARAM lParam)
    {
        if (message == kCancelPopupMessage)
        {
            ::EndMenu();
            return 0;
        }
        if (message == kSimulateRegistrationFailureMessage)
        {
            const auto icon = reinterpret_cast<HICON>(
                ::GetWindowLongPtrW(window, GWLP_USERDATA));
            auto data = BuildNotifyIconData(window, icon);
            ::Shell_NotifyIconW(NIM_DELETE, &data);
            g_iconRegistered.store(false);
            g_running.store(false);
            g_registrationFinalResult.store(false);
            ScheduleRegistrationRetry();
            return 0;
        }
        if (message == g_taskbarCreatedMessage.load()
            && message != 0)
        {
            g_taskbarCreatedCount.fetch_add(1);
            if (ShouldReregisterAfterTaskbarCreated(
                    g_shutdownRequested.load(),
                    ::IsWindow(window) != FALSE))
            {
                const auto icon = reinterpret_cast<HICON>(
                    ::GetWindowLongPtrW(window, GWLP_USERDATA));
                g_iconRegistered.store(false);
                CompleteRegistrationAttempt(AddTrayIcon(
                    window,
                    icon,
                    RegistrationRequestKind::TaskbarCreated));
            }
            else
            {
                g_shutdownRejectedCount.fetch_add(1);
                g_shutdownRetrySuppressedCount.fetch_add(1);
            }
            return 0;
        }
        if (message == kTrayCallbackMessage)
        {
            const UINT notification = LOWORD(lParam);
            if (notification == WM_CONTEXTMENU)
            {
                POINT position{
                    static_cast<short>(LOWORD(wParam)),
                    static_cast<short>(HIWORD(wParam))};
                if (position.x == -1 && position.y == -1)
                    ::GetCursorPos(&position);
                ShowTrayMenu(window, position);
            }
            else if (notification == WM_LBUTTONDBLCLK)
            {
                g_openSettingsSelectionCount.fetch_add(1);
                PublishTrayCommand(NativeMascotCommand::OpenSettings);
            }
            return 0;
        }
        return ::DefWindowProcW(window, message, wParam, lParam);
    }

    void TrayThreadMain()
    {
        const HINSTANCE instance = ::GetModuleHandleW(nullptr);
        const UINT taskbarCreated =
            ::RegisterWindowMessageW(L"TaskbarCreated");
        g_taskbarCreatedMessage.store(taskbarCreated);
        WNDCLASSEXW windowClass{};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = TrayWindowProcedure;
        windowClass.hInstance = instance;
        windowClass.lpszClassName = kOwnerClassName;
        const ATOM atom = ::RegisterClassExW(&windowClass);
        const DWORD classError = atom == 0 ? ::GetLastError() : ERROR_SUCCESS;
        const bool registeredHere = atom != 0;

        HICON icon = CreateOwnedApplicationIcon();
        HWND window = nullptr;
        if (registeredHere || classError == ERROR_CLASS_ALREADY_EXISTS)
        {
            window = ::CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                kOwnerClassName,
                L"",
                WS_POPUP,
                0,
                0,
                0,
                0,
                nullptr,
                nullptr,
                instance,
                nullptr);
        }
        if (window != nullptr)
        {
            g_ownerWindow.store(window);
            g_ownerCreatedCount.fetch_add(1);
            ::SetWindowLongPtrW(
                window,
                GWLP_USERDATA,
                reinterpret_cast<LONG_PTR>(icon));
        }
        const bool infrastructureReady =
            window != nullptr
            && icon != nullptr
            && taskbarCreated != 0;
        g_threadInfrastructureReady.store(infrastructureReady);
        if (infrastructureReady)
        {
            CompleteRegistrationAttempt(AddTrayIcon(
                window,
                icon,
                RegistrationRequestKind::Initial));
        }
        else
        {
            PublishStartResult(false);
        }
        ::SetEvent(g_readyEvent);

        if (infrastructureReady)
        {
            bool stop = false;
            while (!stop)
            {
                const DWORD waitResult = ::MsgWaitForMultipleObjects(
                    1,
                    &g_stopEvent,
                    FALSE,
                    GetRegistrationWaitTimeout(),
                    QS_ALLINPUT);
                if (waitResult == WAIT_OBJECT_0)
                {
                    if (g_registrationRetryPending)
                    {
                        g_registrationRetryPending = false;
                        g_shutdownRetrySuppressedCount.fetch_add(1);
                    }
                    break;
                }
                if (waitResult == WAIT_TIMEOUT)
                {
                    ProcessRegistrationRetry(window, icon);
                    continue;
                }
                if (waitResult != WAIT_OBJECT_0 + 1)
                    break;
                MSG message{};
                while (::PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
                {
                    if (message.message == WM_QUIT)
                    {
                        stop = true;
                        break;
                    }
                    ::TranslateMessage(&message);
                    ::DispatchMessageW(&message);
                }
            }
        }

        g_shutdownRequested.store(true);
        if (g_popupActive.load())
            ::EndMenu();
        DeleteTrayIcon(window, icon);
        if (window != nullptr && ::DestroyWindow(window) != FALSE)
            g_ownerDestroyedCount.fetch_add(1);
        g_ownerWindow.store(nullptr);
        DestroyOwnedIcon(icon);
        if (registeredHere)
            ::UnregisterClassW(kOwnerClassName, instance);
        g_running.store(false);
        g_threadInfrastructureReady.store(false);
        ::SetEvent(g_stoppedEvent);
    }
}

namespace DesktopMascotNative
{
    std::int32_t StartNativeTrayIcon()
    {
        std::lock_guard lock(g_threadMutex);
        if (g_thread.joinable())
            return g_threadInfrastructureReady.load() ? 1 : 0;
        ResetDiagnostics();
        g_shutdownRequested.store(false);
        g_iconRegistered.store(false);
        g_popupActive.store(false);
        g_stopEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
        g_readyEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
        g_stoppedEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (g_stopEvent == nullptr
            || g_readyEvent == nullptr
            || g_stoppedEvent == nullptr)
        {
            if (g_stopEvent != nullptr) ::CloseHandle(g_stopEvent);
            if (g_readyEvent != nullptr) ::CloseHandle(g_readyEvent);
            if (g_stoppedEvent != nullptr) ::CloseHandle(g_stoppedEvent);
            g_stopEvent = nullptr;
            g_readyEvent = nullptr;
            g_stoppedEvent = nullptr;
            return -1;
        }
        try
        {
            g_thread = std::thread(TrayThreadMain);
        }
        catch (...)
        {
            ::CloseHandle(g_stopEvent);
            ::CloseHandle(g_readyEvent);
            ::CloseHandle(g_stoppedEvent);
            g_stopEvent = nullptr;
            g_readyEvent = nullptr;
            g_stoppedEvent = nullptr;
            return -2;
        }
        if (::WaitForSingleObject(g_readyEvent, 5000) != WAIT_OBJECT_0)
            return -3;
        return g_threadInfrastructureReady.load() ? 1 : 0;
    }

    std::int32_t StopNativeTrayIcon()
    {
        std::unique_lock lock(g_threadMutex);
        if (!g_thread.joinable())
            return 1;
        g_shutdownRequested.store(true);
        const HWND window = g_ownerWindow.load();
        if (window != nullptr)
            ::PostMessageW(window, kCancelPopupMessage, 0, 0);
        ::SetEvent(g_stopEvent);
        HANDLE stopped = g_stoppedEvent;
        lock.unlock();
        const bool stoppedSuccessfully =
            ::WaitForSingleObject(stopped, 5000) == WAIT_OBJECT_0;
        lock.lock();
        if (stoppedSuccessfully)
            g_thread.join();
        if (!g_thread.joinable())
        {
            ::CloseHandle(g_stopEvent);
            ::CloseHandle(g_readyEvent);
            ::CloseHandle(g_stoppedEvent);
            g_stopEvent = nullptr;
            g_readyEvent = nullptr;
            g_stoppedEvent = nullptr;
        }
        return stoppedSuccessfully ? 1 : 0;
    }

    bool IsNativeTrayIconRunning() { return g_running.load(); }
    bool IsNativeTrayOwnerWindowAvailable()
    {
        const HWND window = g_ownerWindow.load();
        return window != nullptr && ::IsWindow(window) != FALSE;
    }
    bool IsNativeTrayIconRegistered() { return g_iconRegistered.load(); }
    bool IsNativeTrayPopupActive() { return g_popupActive.load(); }
    bool WasNativeTrayTooltipConfigured()
    {
        return g_tooltipConfigured.load();
    }

#define DMN_TRAY_GET_U32(name, value) \
    std::uint32_t name() { return value.load(); }
    DMN_TRAY_GET_U32(
        GetNativeTrayInitialAddRequestCount,
        g_initialAddRequestCount)
    DMN_TRAY_GET_U32(
        GetNativeTraySetVersionRequestCount,
        g_setVersionRequestCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayDeleteRequestCount,
        g_deleteRequestCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayReregisterRequestCount,
        g_reregisterRequestCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayOwnerCreatedCount,
        g_ownerCreatedCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayOwnerDestroyedCount,
        g_ownerDestroyedCount)
    DMN_TRAY_GET_U32(GetNativeTrayMenuCreatedCount, g_menuCreatedCount)
    DMN_TRAY_GET_U32(GetNativeTrayMenuDestroyedCount, g_menuDestroyedCount)
    DMN_TRAY_GET_U32(GetNativeTrayIconCreatedCount, g_iconCreatedCount)
    DMN_TRAY_GET_U32(GetNativeTrayIconDestroyedCount, g_iconDestroyedCount)
    DMN_TRAY_GET_U32(GetNativeTrayCancelledCount, g_cancelledCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayOpenSettingsSelectionCount,
        g_openSettingsSelectionCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayRequestExitSelectionCount,
        g_requestExitSelectionCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayTaskbarCreatedCount,
        g_taskbarCreatedCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayShutdownRejectedCount,
        g_shutdownRejectedCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayNimAddAttemptCount,
        g_nimAddAttemptCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayNimAddSuccessCount,
        g_nimAddSuccessCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayNimAddLastError,
        g_nimAddLastError)
    DMN_TRAY_GET_U32(
        GetNativeTrayNimSetVersionSuccessCount,
        g_nimSetVersionSuccessCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayNimSetVersionLastError,
        g_nimSetVersionLastError)
    DMN_TRAY_GET_U32(
        GetNativeTrayRegistrationRetryCount,
        g_registrationRetryCount)
    DMN_TRAY_GET_U32(
        GetNativeTrayShutdownRetrySuppressedCount,
        g_shutdownRetrySuppressedCount)
#undef DMN_TRAY_GET_U32

    bool GetNativeTrayRegistrationFinalResult()
    {
        return g_registrationFinalResult.load();
    }

    bool WasNativeTrayRegistrationRetryExhausted()
    {
        return g_registrationRetryExhausted.load();
    }

    std::int32_t GetNativeTrayMenuLiveOwnedCount()
    {
        return g_menuLiveOwnedCount.load();
    }
    std::int32_t GetNativeTrayIconLiveOwnedCount()
    {
        return g_iconLiveOwnedCount.load();
    }

    std::int32_t PublishNativeTrayCommandForDiagnostics(
        std::int32_t command)
    {
        if (command == static_cast<std::int32_t>(
                NativeMascotCommand::OpenSettings))
        {
            return PublishTrayCommand(NativeMascotCommand::OpenSettings)
                ? 1
                : 0;
        }
        if (command == static_cast<std::int32_t>(
                NativeMascotCommand::RequestExit))
        {
            return PublishTrayCommand(NativeMascotCommand::RequestExit)
                ? 1
                : 0;
        }
        return 0;
    }

    std::int32_t RunNativeTrayFocusedDiagnostic()
    {
        if (!IsNativeTrayIconRunning()
            || !IsNativeTrayOwnerWindowAvailable()
            || !IsNativeTrayIconRegistered()
            || !WasNativeTrayTooltipConfigured())
        {
            return 0;
        }
        const auto menusCreatedBefore = g_menuCreatedCount.load();
        const auto menusDestroyedBefore = g_menuDestroyedCount.load();
        HMENU menu = ::CreatePopupMenu();
        if (menu == nullptr)
            return 0;
        g_menuCreatedCount.fetch_add(1);
        g_menuLiveOwnedCount.fetch_add(1);
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
        const bool taskbarRecoveryPolicy =
            ShouldReregisterAfterTaskbarCreated(true, true) == false
            && ShouldReregisterAfterTaskbarCreated(false, true)
            && ShouldReregisterAfterTaskbarCreated(false, false) == false
            && g_iconRegistered.load();
        const bool firstRecovery =
            taskbarRecoveryPolicy
            && SimulateExplorerRestartForDiagnostics();
        const bool repeatedRecovery =
            firstRecovery
            && SimulateExplorerRestartForDiagnostics();
        const bool boundedRetryRecovery =
            repeatedRecovery
            && SimulateRegistrationFailureForDiagnostics();
        return appended
            && taskbarRecoveryPolicy
            && firstRecovery
            && repeatedRecovery
            && boundedRetryRecovery
            && g_reregisterRequestCount.load() == 2
            && g_initialAddRequestCount.load() == 1
            && g_registrationRetryCount.load() == 1
            && g_nimAddAttemptCount.load() == 4
            && g_nimAddSuccessCount.load() == 4
            && g_setVersionRequestCount.load() == 4
            && g_nimSetVersionSuccessCount.load() == 4
            && !g_registrationRetryExhausted.load()
            && g_registrationFinalResult.load()
            && g_ownerCreatedCount.load() == 1
            && g_iconCreatedCount.load() == 1
            && g_iconLiveOwnedCount.load() == 1
            && g_menuCreatedCount.load() == menusCreatedBefore + 1
            && g_menuDestroyedCount.load() == menusDestroyedBefore + 1
            && g_menuLiveOwnedCount.load() == 0
            ? 1
            : 0;
    }
}
