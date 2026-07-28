#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/AnimatedWindowRegionDiagnostics.h"
#include "DesktopMascotNative/CompositionClickThroughDiagnostics.h"
#include "DesktopMascotNative/CompositionPixelHitTestDiagnostics.h"
#include "DesktopMascotNative/CompositionWindowRegionDiagnostics.h"
#include "DesktopMascotNative/CompositionPresentDiagnostics.h"
#include "DesktopMascotNative/CompositionWindowPositionDiagnostics.h"
#include "DesktopMascotNative/ContinuousCompositionDiagnostics.h"
#include "DesktopMascotNative/NativeMascotContextMenu.h"
#include "DesktopMascotNative/NativeMascotWindowDrag.h"
#include "DesktopMascotNative/StaticComplexSilhouetteDiagnostics.h"

#include <Windows.h>
#include <d3d12.h>
#include <dcomp.h>
#include <dxgi1_2.h>
#include <objbase.h>
#include <wrl/client.h>

#include <atomic>
#include <mutex>
#include <thread>

namespace
{
    using DesktopMascotNative::CompositionFailureStage;
    using DesktopMascotNative::CompositionInitializationState;
    using Microsoft::WRL::ComPtr;

    constexpr wchar_t kWindowClassName[] =
        L"DesktopMascotDirectCompositionDiagnosticWindow";
    constexpr int kWindowX = 100;
    constexpr int kWindowY = 100;
    constexpr int kClientWidth = 256;
    constexpr int kClientHeight = 256;
    constexpr DWORD kWindowStyle = WS_POPUP;
    constexpr DWORD kExtendedWindowStyle =
        WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
    constexpr std::int32_t kNotAttempted = 0x7FFFFFFF;

    std::mutex g_threadMutex;
    std::thread g_uiThread;
    HANDLE g_shutdownEvent = nullptr;
    HANDLE g_stoppedEvent = nullptr;
    ID3D12CommandQueue* g_borrowedCommandQueue = nullptr;
    std::atomic<std::int32_t> g_initialWindowX{kWindowX};
    std::atomic<std::int32_t> g_initialWindowY{kWindowY};

#define DMN_ATOMIC_BOOL(name) std::atomic<bool> name{false}
#define DMN_ATOMIC_I32(name, value) std::atomic<std::int32_t> name{value}
    DMN_ATOMIC_I32(g_state, 0);
    DMN_ATOMIC_I32(g_failureStage, 0);
    std::atomic<std::uint32_t> g_uiThreadId{0};
    DMN_ATOMIC_BOOL(g_comAttempted);
    DMN_ATOMIC_BOOL(g_comSucceeded);
    DMN_ATOMIC_I32(g_comResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_classAttempted);
    DMN_ATOMIC_BOOL(g_classSucceeded);
    std::atomic<std::uint32_t> g_classLastError{0};
    DMN_ATOMIC_BOOL(g_windowAttempted);
    DMN_ATOMIC_BOOL(g_windowAvailable);
    std::atomic<std::uint32_t> g_windowLastError{0};
    DMN_ATOMIC_I32(g_clientWidth, 0);
    DMN_ATOMIC_I32(g_clientHeight, 0);
    DMN_ATOMIC_BOOL(g_factoryAttempted);
    DMN_ATOMIC_BOOL(g_factoryAvailable);
    DMN_ATOMIC_I32(g_factoryResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_deviceAttempted);
    DMN_ATOMIC_BOOL(g_deviceAvailable);
    DMN_ATOMIC_I32(g_deviceResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_targetAttempted);
    DMN_ATOMIC_BOOL(g_targetAvailable);
    DMN_ATOMIC_I32(g_targetResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_visualAttempted);
    DMN_ATOMIC_BOOL(g_visualAvailable);
    DMN_ATOMIC_I32(g_visualResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_swapChainAttempted);
    DMN_ATOMIC_BOOL(g_swapChainAvailable);
    DMN_ATOMIC_I32(g_swapChainResult, kNotAttempted);
    DMN_ATOMIC_I32(g_swapChainWidth, 0);
    DMN_ATOMIC_I32(g_swapChainHeight, 0);
    DMN_ATOMIC_I32(g_swapChainFormat, 0);
    DMN_ATOMIC_I32(g_swapChainBufferCount, 0);
    DMN_ATOMIC_I32(g_swapChainSwapEffect, 0);
    DMN_ATOMIC_I32(g_swapChainAlphaMode, 0);
    DMN_ATOMIC_I32(g_swapChainScaling, 0);
    DMN_ATOMIC_BOOL(g_setContentAttempted);
    DMN_ATOMIC_BOOL(g_setContentSucceeded);
    DMN_ATOMIC_I32(g_setContentResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_setRootAttempted);
    DMN_ATOMIC_BOOL(g_setRootSucceeded);
    DMN_ATOMIC_I32(g_setRootResult, kNotAttempted);
    DMN_ATOMIC_BOOL(g_commitAttempted);
    DMN_ATOMIC_BOOL(g_commitSucceeded);
    DMN_ATOMIC_I32(g_commitResult, kNotAttempted);
    DMN_ATOMIC_I32(g_commitCount, 0);
    DMN_ATOMIC_BOOL(g_windowShown);
    DMN_ATOMIC_BOOL(g_messageLoopRunning);
#undef DMN_ATOMIC_BOOL
#undef DMN_ATOMIC_I32

    void StoreState(CompositionInitializationState state)
    {
        g_state.store(static_cast<std::int32_t>(state), std::memory_order_release);
    }

    void Fail(CompositionFailureStage stage)
    {
        DesktopMascotNative::
            NotifyNativeMascotWindowDragShutdownRequested();
        DesktopMascotNative::
            NotifyNativeMascotContextMenuShutdownRequested();
        g_failureStage.store(
            static_cast<std::int32_t>(stage),
            std::memory_order_release);
        StoreState(CompositionInitializationState::Failed);
        wchar_t message[128]{};
        swprintf_s(
            message,
            L"[DesktopMascotNative] DirectComposition initialization failed: stage=%d.\n",
            static_cast<int>(stage));
        ::OutputDebugStringW(message);
    }

    LRESULT CALLBACK WindowProcedure(
        HWND window,
        UINT message,
        WPARAM wParam,
        LPARAM lParam)
    {
        std::intptr_t dragResult = 0;
        if (DesktopMascotNative::HandleNativeMascotWindowDragMessage(
                window,
                message,
                static_cast<std::uintptr_t>(wParam),
                static_cast<std::intptr_t>(lParam),
                dragResult))
        {
            return static_cast<LRESULT>(dragResult);
        }
        std::intptr_t menuResult = 0;
        if (DesktopMascotNative::HandleNativeMascotContextMenuMessage(
                window,
                message,
                static_cast<std::intptr_t>(lParam),
                menuResult))
        {
            return static_cast<LRESULT>(menuResult);
        }
        switch (message)
        {
            case WM_NCHITTEST:
            {
                if (DesktopMascotNative::
                        ShouldAnimatedWindowRegionReturnClient())
                {
                    return HTCLIENT;
                }
                if (DesktopMascotNative::
                        ShouldStaticComplexSilhouetteReturnClient())
                {
                    return HTCLIENT;
                }
                if (DesktopMascotNative::
                        ShouldCompositionWindowRegionReturnClient())
                {
                    return HTCLIENT;
                }
                std::intptr_t hitTestResult = 0;
                if (DesktopMascotNative::
                        TryHandleCompositionPixelHitTest(
                            window,
                            lParam,
                            hitTestResult))
                {
                    return static_cast<LRESULT>(hitTestResult);
                }
                if (DesktopMascotNative::
                        ShouldCompositionHitTestBeTransparent())
                {
                    return HTTRANSPARENT;
                }
                break;
            }
            case DesktopMascotNative::kCompositionPresentInitializeMessage:
                DesktopMascotNative::
                    HandleCompositionPresentInitializeMessage();
                return 0;
            case DesktopMascotNative::kCompositionPresentOnceMessage:
                DesktopMascotNative::HandleCompositionPresentMessage();
                return 0;
            case DesktopMascotNative::kContinuousCompositionInitializeMessage:
                DesktopMascotNative::
                    HandleContinuousCompositionInitializeMessage();
                return 0;
            case DesktopMascotNative::kContinuousCompositionPresentMessage:
                DesktopMascotNative::HandleContinuousCompositionPresentMessage(
                    static_cast<std::uint32_t>(wParam));
                return 0;
            case DesktopMascotNative::kContinuousCompositionStopMessage:
                DesktopMascotNative::
                    HandleContinuousCompositionStopMessage();
                return 0;
            case DesktopMascotNative::kCompositionWindowPositionMessage:
                DesktopMascotNative::
                    HandleCompositionWindowPositionMessage(
                        static_cast<std::uint32_t>(wParam));
                return 0;
            case DesktopMascotNative::kCompositionClickThroughMessage:
                DesktopMascotNative::
                    HandleCompositionClickThroughMessage(
                        static_cast<std::uint32_t>(wParam));
                return 0;
            case DesktopMascotNative::kCompositionPixelHitTestApplyMessage:
                DesktopMascotNative::
                    HandleCompositionPixelHitTestApplyMessage();
                return 0;
            case DesktopMascotNative::kCompositionPixelHitTestRestoreMessage:
                DesktopMascotNative::
                    HandleCompositionPixelHitTestRestoreMessage();
                return 0;
            case DesktopMascotNative::kCompositionWindowRegionApplyMessage:
                DesktopMascotNative::
                    HandleCompositionWindowRegionApplyMessage();
                return 0;
            case DesktopMascotNative::kCompositionWindowRegionRestoreMessage:
                DesktopMascotNative::
                    HandleCompositionWindowRegionRestoreMessage();
                return 0;
            case DesktopMascotNative::kAnimatedWindowRegionApplyMessage:
                DesktopMascotNative::
                    HandleAnimatedWindowRegionApplyMessage();
                return 0;
            case DesktopMascotNative::kAnimatedWindowRegionRestoreMessage:
                DesktopMascotNative::
                    HandleAnimatedWindowRegionRestoreMessage();
                return 0;
            case DesktopMascotNative::kStaticComplexSilhouetteApplyMessage:
                DesktopMascotNative::
                    HandleStaticComplexSilhouetteApplyMessage();
                return 0;
            case DesktopMascotNative::kStaticComplexSilhouetteRestoreMessage:
                DesktopMascotNative::
                    HandleStaticComplexSilhouetteRestoreMessage();
                return 0;
            case WM_CLOSE:
                ::DestroyWindow(window);
                return 0;
            case WM_DESTROY:
                DesktopMascotNative::
                    StopNativeMascotWindowDragOnUiThread();
                DesktopMascotNative::
                    StopNativeMascotContextMenuOnUiThread();
                ::PostQuitMessage(0);
                return 0;
            case WM_NCDESTROY:
                g_windowAvailable.store(false, std::memory_order_release);
                break;
        }
        return ::DefWindowProcW(window, message, wParam, lParam);
    }

    bool RegisterWindowClass(HINSTANCE instance, bool& registeredHere)
    {
        g_classAttempted.store(true, std::memory_order_relaxed);
        WNDCLASSEXW windowClass{};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = WindowProcedure;
        windowClass.hInstance = instance;
        windowClass.hCursor =
            ::LoadCursorW(nullptr, MAKEINTRESOURCEW(32512));
        windowClass.hbrBackground = nullptr;
        windowClass.lpszClassName = kWindowClassName;

        if (::RegisterClassExW(&windowClass) != 0)
        {
            registeredHere = true;
            g_classSucceeded.store(true, std::memory_order_release);
            return true;
        }

        const DWORD error = ::GetLastError();
        g_classLastError.store(error, std::memory_order_relaxed);
        if (error != ERROR_CLASS_ALREADY_EXISTS)
        {
            return false;
        }

        WNDCLASSEXW existing{};
        existing.cbSize = sizeof(existing);
        if (!::GetClassInfoExW(instance, kWindowClassName, &existing)
            || existing.lpfnWndProc != WindowProcedure
            || existing.hInstance != instance)
        {
            return false;
        }
        g_classSucceeded.store(true, std::memory_order_release);
        return true;
    }

    void UiThreadMain()
    {
        g_uiThreadId.store(::GetCurrentThreadId(), std::memory_order_release);
        bool comInitialized = false;
        bool registeredHere = false;
        HWND window = nullptr;
        HINSTANCE instance = nullptr;
        ComPtr<IDXGIFactory2> factory;
        ComPtr<IDCompositionDesktopDevice> device;
        ComPtr<IDCompositionTarget> target;
        ComPtr<IDCompositionVisual2> visual;
        ComPtr<IDXGISwapChain1> swapChain;

        const auto runInitializationAndMessageLoop = [&]()
        {
        g_comAttempted.store(true, std::memory_order_relaxed);
        HRESULT result = ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        g_comResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::ComInitializationFailed);
            return;
        }
        comInitialized = true;
        g_comSucceeded.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::ComInitialized);

        if (!::GetModuleHandleExW(
                GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS
                    | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                reinterpret_cast<LPCWSTR>(&WindowProcedure),
                &instance))
        {
            Fail(CompositionFailureStage::ModuleHandleUnavailable);
            return;
        }
        if (!RegisterWindowClass(instance, registeredHere))
        {
            Fail(CompositionFailureStage::WindowClassRegistrationFailed);
            return;
        }
        StoreState(CompositionInitializationState::WindowClassRegistered);

        RECT windowRectangle{0, 0, kClientWidth, kClientHeight};
        if (!::AdjustWindowRectEx(
                &windowRectangle,
                kWindowStyle,
                FALSE,
                kExtendedWindowStyle))
        {
            g_windowLastError.store(::GetLastError(), std::memory_order_relaxed);
            Fail(CompositionFailureStage::WindowCreationFailed);
            return;
        }
        g_windowAttempted.store(true, std::memory_order_relaxed);
        window = ::CreateWindowExW(
            kExtendedWindowStyle,
            kWindowClassName,
            L"",
            kWindowStyle,
            g_initialWindowX.load(std::memory_order_acquire),
            g_initialWindowY.load(std::memory_order_acquire),
            windowRectangle.right - windowRectangle.left,
            windowRectangle.bottom - windowRectangle.top,
            nullptr,
            nullptr,
            instance,
            nullptr);
        if (window == nullptr)
        {
            g_windowLastError.store(::GetLastError(), std::memory_order_relaxed);
            Fail(CompositionFailureStage::WindowCreationFailed);
            return;
        }
        g_windowAvailable.store(true, std::memory_order_release);
        RECT clientRectangle{};
        if (!::GetClientRect(window, &clientRectangle))
        {
            g_windowLastError.store(::GetLastError(), std::memory_order_relaxed);
            Fail(CompositionFailureStage::InvalidClientSize);
            return;
        }
        g_clientWidth.store(clientRectangle.right, std::memory_order_relaxed);
        g_clientHeight.store(clientRectangle.bottom, std::memory_order_relaxed);
        if (clientRectangle.right != kClientWidth
            || clientRectangle.bottom != kClientHeight)
        {
            Fail(CompositionFailureStage::InvalidClientSize);
            return;
        }
        StoreState(CompositionInitializationState::WindowCreated);

        g_factoryAttempted.store(true, std::memory_order_relaxed);
        result = ::CreateDXGIFactory2(
            0,
            IID_PPV_ARGS(factory.ReleaseAndGetAddressOf()));
        g_factoryResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::DxgiFactoryCreationFailed);
            return;
        }
        g_factoryAvailable.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::DxgiFactoryCreated);

        g_deviceAttempted.store(true, std::memory_order_relaxed);
        result = ::DCompositionCreateDevice3(
            nullptr,
            __uuidof(IDCompositionDesktopDevice),
            reinterpret_cast<void**>(device.ReleaseAndGetAddressOf()));
        g_deviceResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::DCompDeviceCreationFailed);
            return;
        }
        g_deviceAvailable.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::DCompDeviceCreated);

        g_targetAttempted.store(true, std::memory_order_relaxed);
        result = device->CreateTargetForHwnd(
            window,
            TRUE,
            target.ReleaseAndGetAddressOf());
        g_targetResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::DCompTargetCreationFailed);
            return;
        }
        g_targetAvailable.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::DCompTargetCreated);

        g_visualAttempted.store(true, std::memory_order_relaxed);
        result = device->CreateVisual(visual.ReleaseAndGetAddressOf());
        g_visualResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::DCompVisualCreationFailed);
            return;
        }
        g_visualAvailable.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::DCompVisualCreated);

        const DXGI_SWAP_CHAIN_DESC1 description{
            256,
            256,
            DXGI_FORMAT_B8G8R8A8_UNORM,
            FALSE,
            {1, 0},
            DXGI_USAGE_RENDER_TARGET_OUTPUT,
            2,
            DXGI_SCALING_STRETCH,
            DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
            DXGI_ALPHA_MODE_PREMULTIPLIED,
            0};
        g_swapChainWidth.store(description.Width, std::memory_order_relaxed);
        g_swapChainHeight.store(description.Height, std::memory_order_relaxed);
        g_swapChainFormat.store(description.Format, std::memory_order_relaxed);
        g_swapChainBufferCount.store(description.BufferCount, std::memory_order_relaxed);
        g_swapChainSwapEffect.store(description.SwapEffect, std::memory_order_relaxed);
        g_swapChainAlphaMode.store(description.AlphaMode, std::memory_order_relaxed);
        g_swapChainScaling.store(description.Scaling, std::memory_order_relaxed);
        g_swapChainAttempted.store(true, std::memory_order_relaxed);
        result = factory->CreateSwapChainForComposition(
            g_borrowedCommandQueue,
            &description,
            nullptr,
            swapChain.ReleaseAndGetAddressOf());
        g_swapChainResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::SwapChainCreationFailed);
            return;
        }
        g_swapChainAvailable.store(true, std::memory_order_release);
        DesktopMascotNative::SetCompositionPresentUiObjects(
            window,
            swapChain.Get());
        DesktopMascotNative::SetContinuousCompositionUiObjects(
            window,
            swapChain.Get());
        DesktopMascotNative::SetCompositionWindowPositionUiWindow(window);
        DesktopMascotNative::SetCompositionClickThroughUiWindow(window);
        DesktopMascotNative::SetCompositionPixelHitTestUiWindow(window);
        DesktopMascotNative::SetCompositionWindowRegionUiWindow(window);
        DesktopMascotNative::SetAnimatedWindowRegionUiWindow(window);
        DesktopMascotNative::SetStaticComplexSilhouetteUiWindow(window);
        DesktopMascotNative::SetNativeMascotWindowDragUiWindow(window);
        DesktopMascotNative::SetNativeMascotContextMenuUiWindow(window);
        StoreState(CompositionInitializationState::SwapChainCreated);

        g_setContentAttempted.store(true, std::memory_order_relaxed);
        result = visual->SetContent(swapChain.Get());
        g_setContentResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::SetContentFailed);
            return;
        }
        g_setContentSucceeded.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::VisualContentSet);

        g_setRootAttempted.store(true, std::memory_order_relaxed);
        result = target->SetRoot(visual.Get());
        g_setRootResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::SetRootFailed);
            return;
        }
        g_setRootSucceeded.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::RootSet);

        g_commitAttempted.store(true, std::memory_order_relaxed);
        result = device->Commit();
        g_commitResult.store(static_cast<std::int32_t>(result), std::memory_order_relaxed);
        g_commitCount.fetch_add(1, std::memory_order_relaxed);
        if (FAILED(result))
        {
            Fail(CompositionFailureStage::CommitFailed);
            return;
        }
        g_commitSucceeded.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::CommitSucceeded);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] DirectComposition initialization succeeded.\n");

        ::ShowWindow(window, SW_SHOWNOACTIVATE);
        if (!::IsWindowVisible(window))
        {
            Fail(CompositionFailureStage::ShowWindowFailed);
            return;
        }
        g_windowShown.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::WindowShown);

        g_messageLoopRunning.store(true, std::memory_order_release);
        StoreState(CompositionInitializationState::MessageLoopRunning);
        bool quit = false;
        while (!quit)
        {
            const DWORD waitResult = ::MsgWaitForMultipleObjects(
                1,
                &g_shutdownEvent,
                FALSE,
                INFINITE,
                QS_ALLINPUT);
            if (waitResult == WAIT_OBJECT_0)
            {
                break;
            }
            if (waitResult != WAIT_OBJECT_0 + 1)
            {
                Fail(CompositionFailureStage::MessageLoopFailed);
                break;
            }
            MSG message{};
            while (::PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
            {
                if (message.message == WM_QUIT)
                {
                    quit = true;
                    break;
                }
                ::TranslateMessage(&message);
                ::DispatchMessageW(&message);
            }
        }

        };
        runInitializationAndMessageLoop();

        g_messageLoopRunning.store(false, std::memory_order_release);
        if (!DesktopMascotNative::
                ShutdownContinuousCompositionDiagnosticsOnUiThread(2000))
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Fatal: continuous composition GPU "
                L"work did not complete before UI teardown.\n");
            ::RaiseFailFastException(nullptr, nullptr, 0);
        }
        if (!DesktopMascotNative::
                ShutdownCompositionPresentDiagnosticsOnUiThread(4000))
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Fatal: composition Present GPU work "
                L"did not complete before UI teardown.\n");
            ::RaiseFailFastException(nullptr, nullptr, 0);
        }
        DesktopMascotNative::
            StopCompositionWindowPositionDiagnosticsOnUiThread();
        DesktopMascotNative::
            StopCompositionClickThroughDiagnosticsOnUiThread();
        DesktopMascotNative::
            StopCompositionPixelHitTestDiagnosticsOnUiThread();
        DesktopMascotNative::
            StopCompositionWindowRegionDiagnosticsOnUiThread();
        DesktopMascotNative::
            StopAnimatedWindowRegionDiagnosticsOnUiThread();
        DesktopMascotNative::
            StopStaticComplexSilhouetteDiagnosticsOnUiThread();
        DesktopMascotNative::StopNativeMascotWindowDragOnUiThread();
        DesktopMascotNative::StopNativeMascotContextMenuOnUiThread();
        if (window != nullptr && ::IsWindow(window))
        {
            ::DestroyWindow(window);
        }
        DesktopMascotNative::
            RecordStaticComplexSilhouetteAfterWindowDestroyGdiCount();
        if (target != nullptr)
        {
            target->SetRoot(nullptr);
            if (device != nullptr)
            {
                device->Commit();
            }
        }
        g_swapChainAvailable.store(false, std::memory_order_release);
        swapChain.Reset();
        g_visualAvailable.store(false, std::memory_order_release);
        visual.Reset();
        g_targetAvailable.store(false, std::memory_order_release);
        target.Reset();
        g_deviceAvailable.store(false, std::memory_order_release);
        device.Reset();
        g_factoryAvailable.store(false, std::memory_order_release);
        factory.Reset();
        if (registeredHere)
        {
            ::UnregisterClassW(kWindowClassName, instance);
        }
        if (comInitialized)
        {
            ::CoUninitialize();
        }
        DesktopMascotNative::
            RecordStaticComplexSilhouettePostShutdownGdiCount();
        g_borrowedCommandQueue = nullptr;
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                CompositionInitializationState::Failed))
        {
            StoreState(CompositionInitializationState::Stopped);
        }
        ::OutputDebugStringW(
            L"[DesktopMascotNative] DirectComposition diagnostics stopped.\n");
        ::SetEvent(g_stoppedEvent);
    }
}

namespace DesktopMascotNative
{
    bool ConfigureCompositionInitialPosition(
        std::int32_t x,
        std::int32_t y)
    {
        std::lock_guard lock(g_threadMutex);
        if (g_uiThread.joinable())
        {
            return false;
        }
        g_initialWindowX.store(x, std::memory_order_release);
        g_initialWindowY.store(y, std::memory_order_release);
        return true;
    }

    void ResetCompositionDiagnostics()
    {
        g_state.store(0);
        g_failureStage.store(0);
        g_uiThreadId.store(0);
#define RESET_BOOL(name) name.store(false)
#define RESET_I32(name) name.store(kNotAttempted)
        RESET_BOOL(g_comAttempted); RESET_BOOL(g_comSucceeded); RESET_I32(g_comResult);
        RESET_BOOL(g_classAttempted); RESET_BOOL(g_classSucceeded); g_classLastError.store(0);
        RESET_BOOL(g_windowAttempted); RESET_BOOL(g_windowAvailable); g_windowLastError.store(0);
        g_clientWidth.store(0); g_clientHeight.store(0);
        RESET_BOOL(g_factoryAttempted); RESET_BOOL(g_factoryAvailable); RESET_I32(g_factoryResult);
        RESET_BOOL(g_deviceAttempted); RESET_BOOL(g_deviceAvailable); RESET_I32(g_deviceResult);
        RESET_BOOL(g_targetAttempted); RESET_BOOL(g_targetAvailable); RESET_I32(g_targetResult);
        RESET_BOOL(g_visualAttempted); RESET_BOOL(g_visualAvailable); RESET_I32(g_visualResult);
        RESET_BOOL(g_swapChainAttempted); RESET_BOOL(g_swapChainAvailable); RESET_I32(g_swapChainResult);
        g_swapChainWidth.store(0); g_swapChainHeight.store(0); g_swapChainFormat.store(0);
        g_swapChainBufferCount.store(0); g_swapChainSwapEffect.store(0);
        g_swapChainAlphaMode.store(0); g_swapChainScaling.store(0);
        RESET_BOOL(g_setContentAttempted); RESET_BOOL(g_setContentSucceeded); RESET_I32(g_setContentResult);
        RESET_BOOL(g_setRootAttempted); RESET_BOOL(g_setRootSucceeded); RESET_I32(g_setRootResult);
        RESET_BOOL(g_commitAttempted); RESET_BOOL(g_commitSucceeded); RESET_I32(g_commitResult);
        g_commitCount.store(0); RESET_BOOL(g_windowShown); RESET_BOOL(g_messageLoopRunning);
#undef RESET_BOOL
#undef RESET_I32
    }

    std::int32_t StartCompositionDiagnostics(ID3D12CommandQueue* commandQueue)
    {
        std::lock_guard lock(g_threadMutex);
        if (g_uiThread.joinable())
        {
            return 0;
        }
        if (commandQueue == nullptr)
        {
            return -1;
        }
        ResetCompositionDiagnostics();
        ResetCompositionWindowPositionDiagnostics();
        ResetCompositionClickThroughDiagnostics();
        ResetCompositionPixelHitTestDiagnostics();
        ResetCompositionWindowRegionDiagnostics();
        ResetAnimatedWindowRegionDiagnostics();
        ResetStaticComplexSilhouetteDiagnostics();
        ResetNativeMascotWindowDrag();
        ResetNativeMascotContextMenu();
        g_shutdownEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
        g_stoppedEvent = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (g_shutdownEvent == nullptr || g_stoppedEvent == nullptr)
        {
            if (g_shutdownEvent != nullptr) ::CloseHandle(g_shutdownEvent);
            if (g_stoppedEvent != nullptr) ::CloseHandle(g_stoppedEvent);
            g_shutdownEvent = nullptr;
            g_stoppedEvent = nullptr;
            Fail(CompositionFailureStage::ThreadCreationFailed);
            return -2;
        }
        g_borrowedCommandQueue = commandQueue;
        StoreState(CompositionInitializationState::ThreadStarting);
        try
        {
            g_uiThread = std::thread(UiThreadMain);
        }
        catch (...)
        {
            ::CloseHandle(g_shutdownEvent);
            ::CloseHandle(g_stoppedEvent);
            g_shutdownEvent = nullptr;
            g_stoppedEvent = nullptr;
            g_borrowedCommandQueue = nullptr;
            Fail(CompositionFailureStage::ThreadCreationFailed);
            return -3;
        }
        return 1;
    }

    bool RequestCompositionDiagnosticsShutdown()
    {
        std::lock_guard lock(g_threadMutex);
        if (!g_uiThread.joinable() || g_shutdownEvent == nullptr)
        {
            return false;
        }
        if (::WaitForSingleObject(g_stoppedEvent, 0) == WAIT_OBJECT_0)
        {
            return true;
        }
        NotifyCompositionPresentShutdownRequested();
        NotifyContinuousCompositionShutdownRequested();
        NotifyCompositionWindowPositionShutdownRequested();
        NotifyCompositionClickThroughShutdownRequested();
        NotifyAnimatedWindowRegionShutdownRequested();
        NotifyNativeMascotWindowDragShutdownRequested();
        NotifyNativeMascotContextMenuShutdownRequested();
        if (g_state.load(std::memory_order_acquire)
            != static_cast<std::int32_t>(
                CompositionInitializationState::Stopped))
        {
            StoreState(CompositionInitializationState::ShutdownRequested);
        }
        return ::SetEvent(g_shutdownEvent) != FALSE;
    }

    bool StopCompositionDiagnosticsForUnload(std::uint32_t timeoutMilliseconds)
    {
        std::unique_lock lock(g_threadMutex);
        if (!g_uiThread.joinable())
        {
            return true;
        }
        StoreState(CompositionInitializationState::ShutdownRequested);
        ::SetEvent(g_shutdownEvent);
        HANDLE stoppedEvent = g_stoppedEvent;
        lock.unlock();
        const DWORD waitResult =
            ::WaitForSingleObject(stoppedEvent, timeoutMilliseconds);
        lock.lock();
        if (waitResult != WAIT_OBJECT_0)
        {
            g_failureStage.store(
                static_cast<std::int32_t>(
                    CompositionFailureStage::ShutdownFailed));
            return false;
        }
        g_uiThread.join();
        RecordStaticComplexSilhouetteAfterUiThreadJoinGdiCount();
        ::CloseHandle(g_shutdownEvent);
        ::CloseHandle(g_stoppedEvent);
        g_shutdownEvent = nullptr;
        g_stoppedEvent = nullptr;
        RecordStaticComplexSilhouetteAfterNativeCleanupGdiCount();
        if (g_failureStage.load(std::memory_order_acquire) == 0)
        {
            StoreState(CompositionInitializationState::Stopped);
        }
        return true;
    }

#define GET_I32(fn, value) std::int32_t fn() { return value.load(); }
#define GET_U32(fn, value) std::uint32_t fn() { return value.load(); }
#define GET_BOOL(fn, value) bool fn() { return value.load(); }
    GET_I32(GetCompositionInitializationState, g_state)
    GET_I32(GetCompositionFailureStage, g_failureStage)
    GET_U32(GetCompositionUiThreadId, g_uiThreadId)
    GET_BOOL(WasCompositionComInitializationAttempted, g_comAttempted)
    GET_BOOL(DidCompositionComInitializationSucceed, g_comSucceeded)
    GET_I32(GetCompositionComInitializationResult, g_comResult)
    GET_BOOL(WasCompositionWindowClassRegistrationAttempted, g_classAttempted)
    GET_BOOL(DidCompositionWindowClassRegistrationSucceed, g_classSucceeded)
    GET_U32(GetCompositionWindowClassLastError, g_classLastError)
    GET_BOOL(WasCompositionWindowCreationAttempted, g_windowAttempted)
    GET_BOOL(IsCompositionWindowAvailable, g_windowAvailable)
    GET_U32(GetCompositionWindowCreationLastError, g_windowLastError)
    GET_I32(GetCompositionWindowClientWidth, g_clientWidth)
    GET_I32(GetCompositionWindowClientHeight, g_clientHeight)
    GET_BOOL(WasDxgiFactoryCreationAttempted, g_factoryAttempted)
    GET_BOOL(IsDxgiFactoryAvailable, g_factoryAvailable)
    GET_I32(GetDxgiFactoryCreationResult, g_factoryResult)
    GET_BOOL(WasDCompDeviceCreationAttempted, g_deviceAttempted)
    GET_BOOL(IsDCompDeviceAvailable, g_deviceAvailable)
    GET_I32(GetDCompDeviceCreationResult, g_deviceResult)
    GET_BOOL(WasDCompTargetCreationAttempted, g_targetAttempted)
    GET_BOOL(IsDCompTargetAvailable, g_targetAvailable)
    GET_I32(GetDCompTargetCreationResult, g_targetResult)
    GET_BOOL(WasDCompVisualCreationAttempted, g_visualAttempted)
    GET_BOOL(IsDCompVisualAvailable, g_visualAvailable)
    GET_I32(GetDCompVisualCreationResult, g_visualResult)
    GET_BOOL(WasCompositionSwapChainCreationAttempted, g_swapChainAttempted)
    GET_BOOL(IsCompositionSwapChainAvailable, g_swapChainAvailable)
    GET_I32(GetCompositionSwapChainCreationResult, g_swapChainResult)
    GET_I32(GetCompositionSwapChainWidth, g_swapChainWidth)
    GET_I32(GetCompositionSwapChainHeight, g_swapChainHeight)
    GET_I32(GetCompositionSwapChainFormat, g_swapChainFormat)
    GET_I32(GetCompositionSwapChainBufferCount, g_swapChainBufferCount)
    GET_I32(GetCompositionSwapChainSwapEffect, g_swapChainSwapEffect)
    GET_I32(GetCompositionSwapChainAlphaMode, g_swapChainAlphaMode)
    GET_I32(GetCompositionSwapChainScaling, g_swapChainScaling)
    GET_BOOL(WasDCompSetContentAttempted, g_setContentAttempted)
    GET_BOOL(DidDCompSetContentSucceed, g_setContentSucceeded)
    GET_I32(GetDCompSetContentResult, g_setContentResult)
    GET_BOOL(WasDCompSetRootAttempted, g_setRootAttempted)
    GET_BOOL(DidDCompSetRootSucceed, g_setRootSucceeded)
    GET_I32(GetDCompSetRootResult, g_setRootResult)
    GET_BOOL(WasDCompCommitAttempted, g_commitAttempted)
    GET_BOOL(DidDCompCommitSucceed, g_commitSucceeded)
    GET_I32(GetDCompCommitResult, g_commitResult)
    GET_I32(GetDCompCommitCount, g_commitCount)
    GET_BOOL(WasCompositionWindowShown, g_windowShown)
    GET_BOOL(IsCompositionMessageLoopRunning, g_messageLoopRunning)
#undef GET_I32
#undef GET_U32
#undef GET_BOOL
}
