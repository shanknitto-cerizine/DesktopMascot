#include "DesktopMascotNative/SpeechPresentation.h"

#include <Windows.h>
#include <d3d12.h>
#include <dcomp.h>
#include <dxgi1_4.h>
#include <wrl/client.h>

#include <atomic>
#include <algorithm>
#include <mutex>

#include "IUnityGraphicsD3D12.h"

namespace
{
    using Microsoft::WRL::ComPtr;

    constexpr wchar_t kClassName[] = L"DesktopMascotSpeechPresentationWindow";
    constexpr DWORD kStyle = WS_POPUP;
    constexpr DWORD kExtendedStyle =
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP;
    constexpr UINT kBufferCount = 2;
    constexpr std::uint64_t kFenceTimeoutMilliseconds = 2000;
    constexpr int kVerticalAnchorBias = 32;

    std::mutex g_mutex;
    std::atomic<HWND> g_owner{nullptr};
    std::atomic<HWND> g_window{nullptr};
    IDXGIFactory2* g_factory = nullptr;
    IDCompositionDesktopDevice* g_compositionDevice = nullptr;
    ID3D12CommandQueue* g_commandQueue = nullptr;
    ComPtr<IDCompositionTarget> g_target;
    ComPtr<IDCompositionVisual2> g_visual;
    ComPtr<IDXGISwapChain1> g_swapChain;
    ComPtr<IDXGISwapChain3> g_swapChain3;
    ComPtr<ID3D12Resource> g_backBuffer;
    ComPtr<ID3D12CommandAllocator> g_allocator;
    ComPtr<ID3D12GraphicsCommandList> g_commandList;
    std::atomic<ID3D12Fence*> g_frameFence{nullptr};
    std::atomic<std::uint64_t> g_fenceValue{0};
    std::atomic<std::uint64_t> g_fenceStartTick{0};
    std::atomic<bool> g_frameInFlight{false};
    std::atomic<bool> g_ready{false};
    std::atomic<bool> g_visible{false};
    std::atomic<bool> g_shutdown{false};
    std::atomic<bool> g_cleanupSucceeded{false};
    std::atomic<std::uint32_t> g_generation{0};
    std::atomic<std::uint32_t> g_clickGeneration{0};
    std::atomic<std::int32_t> g_anchorX{128};
    std::atomic<std::int32_t> g_anchorY{128};
    std::atomic<std::int32_t> g_failureStage{0};
    std::atomic<std::int32_t> g_presentResult{S_OK};
    std::atomic<std::int32_t> g_deviceRemovedResult{S_OK};
    std::atomic<std::uint32_t> g_presentCount{0};
    std::atomic<std::uint32_t> g_createdCount{0};
    std::atomic<std::uint32_t> g_destroyedCount{0};
    std::atomic<std::uint32_t> g_regionCreatedCount{0};
    std::atomic<std::uint32_t> g_regionTransferredCount{0};
    std::atomic<std::uint32_t> g_regionCallerDeletedCount{0};
    std::atomic<std::uint32_t> g_targetCreatedCount{0};
    std::atomic<std::uint32_t> g_visualCreatedCount{0};
    std::atomic<std::uint32_t> g_swapChainCreatedCount{0};
    std::atomic<std::uint32_t> g_followCount{0};
    std::atomic<std::uint32_t> g_maximumFollowErrorPixels{0};
    std::atomic<std::uint32_t> g_flipCount{0};
    std::atomic<std::uint32_t> g_clampCount{0};
    std::atomic<std::uint32_t> g_dpi{96};
    std::atomic<bool> g_ownerMatches{false};
    std::atomic<std::uint32_t> g_initializeRequestCount{0};
    std::atomic<std::uint32_t> g_initializePostSuccessCount{0};
    std::atomic<std::uint32_t> g_initializeHandleCount{0};

    void Fail(std::int32_t stage)
    {
        std::int32_t expected = 0;
        const bool firstFailure =
            g_failureStage.compare_exchange_strong(expected, stage);
        if (firstFailure && stage >= 9)
        {
            const HWND owner = g_owner.load(std::memory_order_acquire);
            if (owner != nullptr)
                ::PostMessageW(
                    owner,
                    DesktopMascotNative::kSpeechHideMessage,
                    g_generation.load(),
                    0);
        }
        wchar_t text[128]{};
        swprintf_s(text, L"[DesktopMascotNative] Speech failure stage: %d.\n", stage);
        ::OutputDebugStringW(text);
    }

    D3D12_RESOURCE_BARRIER Transition(
        ID3D12Resource* resource,
        D3D12_RESOURCE_STATES before,
        D3D12_RESOURCE_STATES after)
    {
        D3D12_RESOURCE_BARRIER barrier{};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition.pResource = resource;
        barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        barrier.Transition.StateBefore = before;
        barrier.Transition.StateAfter = after;
        return barrier;
    }

    void PositionWindow()
    {
        const HWND owner = g_owner.load(std::memory_order_acquire);
        const HWND window = g_window.load(std::memory_order_acquire);
        if (owner == nullptr || window == nullptr)
            return;
        RECT mascot{};
        if (!::GetWindowRect(owner, &mascot))
            return;
        const UINT dpi = ::GetDpiForWindow(owner);
        g_dpi.store(dpi == 0 ? 96 : dpi);
        const int mascotWidth = mascot.right - mascot.left;
        const int mascotHeight = mascot.bottom - mascot.top;
        RECT silhouette{0, 0, mascotWidth, mascotHeight};
        RECT appliedRegion{};
        const int regionType = ::GetWindowRgnBox(owner, &appliedRegion);
        if (regionType != ERROR && regionType != NULLREGION
            && appliedRegion.right > appliedRegion.left
            && appliedRegion.bottom > appliedRegion.top)
        {
            silhouette = appliedRegion;
        }
        const int silhouetteLeft = mascot.left + silhouette.left;
        const int silhouetteRight = mascot.left + silhouette.right;
        // Align the fixed-size card center to the current applied mascot silhouette
        // center. The mascot region remains a read-only positioning input;
        // this does not alter its HRGN or ownership. Work-area clamping below
        // applies the smallest required edge correction without side-flipping.
        const int silhouetteCenter = silhouetteLeft
            + (silhouetteRight - silhouetteLeft) / 2;
        int x = silhouetteCenter
            - static_cast<int>(DesktopMascotNative::kSpeechWidth) / 2;
        // The resolved point is within the upper/lower-leg blend. Bias the
        // card's top edge upward by one eighth of mascot space so the visual
        // edge starts at the waist/thigh area rather than below the feet.
        int y = mascot.top
            + ((g_anchorY.load() - kVerticalAnchorBias) * mascotHeight / 256);
        HMONITOR monitor = ::MonitorFromRect(&mascot, MONITOR_DEFAULTTONEAREST);
        MONITORINFO info{sizeof(info)};
        if (::GetMonitorInfoW(monitor, &info))
        {
            const int originalX = x;
            const int originalY = y;
            const int maxX = info.rcWork.right - static_cast<int>(DesktopMascotNative::kSpeechWidth);
            const int maxY = info.rcWork.bottom - static_cast<int>(DesktopMascotNative::kSpeechHeight);
            const int workLeft = static_cast<int>(info.rcWork.left);
            const int workTop = static_cast<int>(info.rcWork.top);
            x = maxX >= workLeft
                ? std::clamp(x, workLeft, maxX)
                : workLeft;
            y = maxY >= workTop
                ? std::clamp(y, workTop, maxY)
                : workTop;
            if (x != originalX || y != originalY)
                g_clampCount.fetch_add(1);
        }
        RECT current{};
        if (::GetWindowRect(window, &current)
            && abs(current.left - x) < 2
            && abs(current.top - y) < 2)
            return;
        if (::SetWindowPos(
                window,
                nullptr,
                x,
                y,
                DesktopMascotNative::kSpeechWidth,
                DesktopMascotNative::kSpeechHeight,
                SWP_NOACTIVATE | SWP_NOZORDER))
        {
            g_followCount.fetch_add(1);
            RECT applied{};
            if (::GetWindowRect(window, &applied))
            {
                const auto error = static_cast<std::uint32_t>(std::max(
                    abs(applied.left - x),
                    abs(applied.top - y)));
                auto maximum = g_maximumFollowErrorPixels.load();
                while (error > maximum
                    && !g_maximumFollowErrorPixels.compare_exchange_weak(
                        maximum, error)) {}
            }
        }
    }

    LRESULT CALLBACK SpeechWindowProcedure(
        HWND window,
        UINT message,
        WPARAM wParam,
        LPARAM lParam)
    {
        switch (message)
        {
            case WM_MOUSEACTIVATE:
                return MA_NOACTIVATE;
            case WM_NCHITTEST:
                return HTCLIENT;
            case WM_LBUTTONUP:
                if (g_visible.load(std::memory_order_acquire))
                    g_clickGeneration.store(g_generation.load());
                return 0;
            case WM_DESTROY:
                g_destroyedCount.fetch_add(1);
                return 0;
            default:
                return ::DefWindowProcW(window, message, wParam, lParam);
        }
    }

    bool EnsureWindowAndComposition()
    {
        if (g_ready.load(std::memory_order_acquire))
            return true;
        const HWND owner = g_owner.load(std::memory_order_acquire);
        if (owner == nullptr || g_factory == nullptr
            || g_compositionDevice == nullptr || g_commandQueue == nullptr)
        {
            Fail(1);
            return false;
        }
        WNDCLASSEXW existing{};
        existing.cbSize = sizeof(existing);
        if (!::GetClassInfoExW(nullptr, kClassName, &existing))
        {
            WNDCLASSEXW windowClass{};
            windowClass.cbSize = sizeof(windowClass);
            windowClass.lpfnWndProc = SpeechWindowProcedure;
            windowClass.hInstance = reinterpret_cast<HINSTANCE>(
                ::GetWindowLongPtrW(owner, GWLP_HINSTANCE));
            windowClass.hCursor = ::LoadCursorW(
                nullptr,
                MAKEINTRESOURCEW(32512));
            windowClass.lpszClassName = kClassName;
            if (::RegisterClassExW(&windowClass) == 0
                && ::GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            {
                Fail(2);
                return false;
            }
        }
        HWND window = ::CreateWindowExW(
            kExtendedStyle,
            kClassName,
            L"",
            kStyle,
            0,
            0,
            DesktopMascotNative::kSpeechWidth,
            DesktopMascotNative::kSpeechHeight,
            owner,
            nullptr,
            reinterpret_cast<HINSTANCE>(::GetWindowLongPtrW(owner, GWLP_HINSTANCE)),
            nullptr);
        if (window == nullptr)
        {
            Fail(3);
            return false;
        }
        g_window.store(window, std::memory_order_release);
        g_createdCount.fetch_add(1);
        g_ownerMatches.store(::GetWindow(window, GW_OWNER) == owner);

        DXGI_SWAP_CHAIN_DESC1 description{};
        description.Width = DesktopMascotNative::kSpeechWidth;
        description.Height = DesktopMascotNative::kSpeechHeight;
        description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        description.SampleDesc.Count = 1;
        description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.BufferCount = kBufferCount;
        description.Scaling = DXGI_SCALING_STRETCH;
        description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        description.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
        HRESULT result = g_factory->CreateSwapChainForComposition(
            g_commandQueue,
            &description,
            nullptr,
            g_swapChain.ReleaseAndGetAddressOf());
        if (FAILED(result)) { Fail(4); return false; }
        g_swapChainCreatedCount.fetch_add(1);
        result = g_swapChain.As(&g_swapChain3);
        if (FAILED(result)) { Fail(5); return false; }
        result = g_compositionDevice->CreateTargetForHwnd(
            window,
            TRUE,
            g_target.ReleaseAndGetAddressOf());
        if (FAILED(result)) { Fail(6); return false; }
        g_targetCreatedCount.fetch_add(1);
        result = g_compositionDevice->CreateVisual(
            g_visual.ReleaseAndGetAddressOf());
        if (FAILED(result)) { Fail(7); return false; }
        g_visualCreatedCount.fetch_add(1);
        result = g_visual->SetContent(g_swapChain.Get());
        if (SUCCEEDED(result))
        {
            // Unity's Camera target RenderTexture is vertically inverted when
            // its D3D12 resource is copied byte-for-byte to a composition
            // swap chain. Correct only the Speech visual at the platform
            // presentation boundary. This does not add a Camera-source Blit
            // or alter the validated mascot normalization path.
            const D2D_MATRIX_3X2_F speechOrientation{
                1.0f, 0.0f,
                0.0f, -1.0f,
                0.0f, static_cast<float>(DesktopMascotNative::kSpeechHeight)};
            result = g_visual->SetTransform(speechOrientation);
        }
        if (SUCCEEDED(result)) result = g_target->SetRoot(g_visual.Get());
        if (SUCCEEDED(result)) result = g_compositionDevice->Commit();
        if (FAILED(result)) { Fail(8); return false; }

        HRGN region = ::CreateRoundRectRgn(
            0,
            0,
            DesktopMascotNative::kSpeechWidth + 1,
            DesktopMascotNative::kSpeechHeight + 1,
            16,
            16);
        if (region != nullptr)
        {
            g_regionCreatedCount.fetch_add(1);
            if (::SetWindowRgn(window, region, FALSE))
                g_regionTransferredCount.fetch_add(1);
            else
            {
                ::DeleteObject(region);
                g_regionCallerDeletedCount.fetch_add(1);
            }
        }
        PositionWindow();
        g_ready.store(true, std::memory_order_release);
        ::OutputDebugStringW(L"[DesktopMascotNative] Speech presentation initialized.\n");
        return true;
    }
}

namespace DesktopMascotNative
{
    static_assert(kSpeechInitializeMessage > 0x8000u + 0x362u);

    void ResetSpeechPresentation()
    {
        std::lock_guard lock(g_mutex);
        g_owner.store(nullptr); g_window.store(nullptr);
        g_factory = nullptr; g_compositionDevice = nullptr; g_commandQueue = nullptr;
        g_target.Reset(); g_visual.Reset(); g_swapChain.Reset(); g_swapChain3.Reset();
        g_backBuffer.Reset(); g_allocator.Reset(); g_commandList.Reset();
        g_frameFence.store(nullptr); g_fenceValue.store(0); g_frameInFlight.store(false);
        g_ready.store(false); g_visible.store(false); g_shutdown.store(false);
        g_cleanupSucceeded.store(false); g_generation.store(0); g_clickGeneration.store(0);
        g_failureStage.store(0); g_presentResult.store(S_OK);
        g_deviceRemovedResult.store(S_OK); g_presentCount.store(0);
        g_createdCount.store(0); g_destroyedCount.store(0);
        g_regionCreatedCount.store(0); g_regionTransferredCount.store(0);
        g_regionCallerDeletedCount.store(0); g_followCount.store(0);
        g_targetCreatedCount.store(0); g_visualCreatedCount.store(0);
        g_swapChainCreatedCount.store(0); g_maximumFollowErrorPixels.store(0);
        g_flipCount.store(0); g_clampCount.store(0); g_dpi.store(96);
        g_ownerMatches.store(false);
        g_initializeRequestCount.store(0);
        g_initializePostSuccessCount.store(0);
        g_initializeHandleCount.store(0);
    }

    void SetSpeechPresentationUiContext(
        void* mascotWindow,
        IDXGIFactory2* factory,
        IDCompositionDesktopDevice* compositionDevice,
        ID3D12CommandQueue* commandQueue)
    {
        std::lock_guard lock(g_mutex);
        g_owner.store(static_cast<HWND>(mascotWindow));
        g_factory = factory;
        g_compositionDevice = compositionDevice;
        g_commandQueue = commandQueue;
    }

    bool HandleSpeechPresentationOwnerMessage(
        std::uint32_t message,
        std::uintptr_t wParam,
        std::intptr_t,
        std::intptr_t& result)
    {
        if (message == kSpeechInitializeMessage)
        {
            g_initializeHandleCount.fetch_add(1);
            result = EnsureWindowAndComposition() ? 1 : 0;
            return true;
        }
        if (message == kSpeechShowMessage)
        {
            if (EnsureWindowAndComposition() && !g_shutdown.load())
            {
                g_generation.store(static_cast<std::uint32_t>(wParam));
                PositionWindow();
                ::ShowWindow(g_window.load(), SW_SHOWNOACTIVATE);
                g_visible.store(true);
            }
            result = 0;
            return true;
        }
        if (message == kSpeechHideMessage)
        {
            if (static_cast<std::uint32_t>(wParam) == g_generation.load())
            {
                ::ShowWindow(g_window.load(), SW_HIDE);
                g_visible.store(false);
            }
            result = 0;
            return true;
        }
        if (message == kSpeechPresentMessage)
        {
            if (static_cast<std::uint32_t>(wParam) == g_generation.load()
                && g_swapChain != nullptr)
            {
                const HRESULT present = g_swapChain->Present(1, 0);
                g_presentResult.store(present);
                if (SUCCEEDED(present)) g_presentCount.fetch_add(1);
                else if (g_commandQueue != nullptr)
                {
                    ComPtr<ID3D12Device> device;
                    if (SUCCEEDED(g_commandQueue->GetDevice(IID_PPV_ARGS(&device))))
                        g_deviceRemovedResult.store(device->GetDeviceRemovedReason());
                    Fail(13);
                }
            }
            g_frameInFlight.store(false);
            result = 0;
            return true;
        }
        if (message == WM_WINDOWPOSCHANGED)
        {
            PositionWindow();
            return false;
        }
        return false;
    }

    void HandleSpeechPresentationFrameEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device,
        void* sourceTexture)
    {
        if (!g_ready.load() || !g_visible.load() || g_shutdown.load()
            || g_failureStage.load() != 0
            || g_frameInFlight.exchange(true))
            return;
        std::lock_guard lock(g_mutex);
        auto* source = static_cast<ID3D12Resource*>(sourceTexture);
        if (source == nullptr || d3d12 == nullptr || device == nullptr
            || g_swapChain3 == nullptr) { g_frameInFlight.store(false); Fail(9); return; }
        const auto sourceDesc = source->GetDesc();
        if (sourceDesc.Width != kSpeechWidth || sourceDesc.Height != kSpeechHeight
            || sourceDesc.Format != DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)
        { g_frameInFlight.store(false); Fail(10); return; }
        HRESULT hr = g_swapChain3->GetBuffer(
            g_swapChain3->GetCurrentBackBufferIndex(),
            IID_PPV_ARGS(g_backBuffer.ReleaseAndGetAddressOf()));
        if (FAILED(hr)) { g_frameInFlight.store(false); Fail(11); return; }
        if (g_allocator == nullptr)
        {
            hr = device->CreateCommandAllocator(
                D3D12_COMMAND_LIST_TYPE_DIRECT,
                IID_PPV_ARGS(g_allocator.ReleaseAndGetAddressOf()));
            if (SUCCEEDED(hr)) hr = device->CreateCommandList(
                0, D3D12_COMMAND_LIST_TYPE_DIRECT, g_allocator.Get(), nullptr,
                IID_PPV_ARGS(g_commandList.ReleaseAndGetAddressOf()));
        }
        else
        {
            auto* fence = g_frameFence.load();
            if (fence == nullptr || fence->GetCompletedValue() < g_fenceValue.load())
            { g_frameInFlight.store(false); return; }
            hr = g_allocator->Reset();
            if (SUCCEEDED(hr)) hr = g_commandList->Reset(g_allocator.Get(), nullptr);
        }
        if (FAILED(hr)) { g_frameInFlight.store(false); Fail(12); return; }
        const auto toCopy = Transition(g_backBuffer.Get(), D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_COPY_DEST);
        g_commandList->ResourceBarrier(1, &toCopy);
        g_commandList->CopyResource(g_backBuffer.Get(), source);
        const auto toPresent = Transition(g_backBuffer.Get(), D3D12_RESOURCE_STATE_COPY_DEST, D3D12_RESOURCE_STATE_PRESENT);
        g_commandList->ResourceBarrier(1, &toPresent);
        hr = g_commandList->Close();
        if (FAILED(hr)) { g_frameInFlight.store(false); Fail(12); return; }
        auto* fence = d3d12->GetFrameFence();
        UnityGraphicsD3D12ResourceState states[] = {
            {source, D3D12_RESOURCE_STATE_COPY_SOURCE, D3D12_RESOURCE_STATE_COPY_SOURCE},
            {g_backBuffer.Get(), D3D12_RESOURCE_STATE_PRESENT, D3D12_RESOURCE_STATE_PRESENT}};
        const UINT64 value = d3d12->ExecuteCommandList(g_commandList.Get(), 2, states);
        if (value == 0 || fence == nullptr) { g_frameInFlight.store(false); Fail(12); return; }
        g_frameFence.store(fence); g_fenceValue.store(value);
        g_fenceStartTick.store(::GetTickCount64());
    }

    bool RequestSpeechPresentationInitialize()
    {
        g_initializeRequestCount.fetch_add(1);
        HWND owner = g_owner.load();
        const bool posted = owner != nullptr
            && ::PostMessageW(owner, kSpeechInitializeMessage, 0, 0);
        if (posted) g_initializePostSuccessCount.fetch_add(1);
        return posted;
    }
    bool RequestSpeechPresentationShow(std::uint32_t generation, std::int32_t anchorX, std::int32_t anchorY)
    {
        if (g_shutdown.load()) return false;
        g_anchorX.store(anchorX); g_anchorY.store(anchorY);
        HWND owner = g_owner.load();
        return owner != nullptr && ::PostMessageW(owner, kSpeechShowMessage, generation, 0);
    }
    bool RequestSpeechPresentationHide(std::uint32_t generation)
    {
        HWND owner = g_owner.load();
        return owner != nullptr && ::PostMessageW(owner, kSpeechHideMessage, generation, 0);
    }
    bool UpdateSpeechPresentationAnchor(std::int32_t anchorX, std::int32_t anchorY)
    {
        g_anchorX.store(anchorX); g_anchorY.store(anchorY);
        HWND owner = g_owner.load();
        return owner != nullptr && ::PostMessageW(owner, WM_WINDOWPOSCHANGED, 0, 0);
    }
    void PollSpeechPresentation()
    {
        if (!g_frameInFlight.load()) return;
        auto* fence = g_frameFence.load();
        if (fence != nullptr && fence->GetCompletedValue() >= g_fenceValue.load())
        {
            HWND owner = g_owner.load();
            if (owner != nullptr) ::PostMessageW(owner, kSpeechPresentMessage, g_generation.load(), 0);
        }
        else if (::GetTickCount64() - g_fenceStartTick.load() > kFenceTimeoutMilliseconds)
        { g_frameInFlight.store(false); Fail(14); }
    }
    std::uint32_t ConsumeSpeechClickGeneration() { return g_clickGeneration.exchange(0); }
    bool BeginSpeechPresentationShutdown() { g_shutdown.store(true); return true; }
    bool ShutdownSpeechPresentationOnUiThread(std::uint32_t timeoutMilliseconds)
    {
        const auto start = ::GetTickCount64();
        while (g_frameInFlight.load() && ::GetTickCount64() - start < timeoutMilliseconds)
        {
            auto* fence = g_frameFence.load();
            if (fence != nullptr && fence->GetCompletedValue() >= g_fenceValue.load())
                g_frameInFlight.store(false);
            else ::Sleep(1);
        }
        if (g_frameInFlight.load()) { Fail(15); return false; }
        std::lock_guard lock(g_mutex);
        HWND window = g_window.exchange(nullptr);
        if (window != nullptr && ::IsWindow(window))
        {
            ::SetWindowRgn(window, nullptr, FALSE);
            ::DestroyWindow(window);
        }
        g_visible.store(false); g_ready.store(false);
        g_backBuffer.Reset(); g_commandList.Reset(); g_allocator.Reset();
        g_swapChain3.Reset(); g_swapChain.Reset(); g_visual.Reset(); g_target.Reset();
        g_frameFence.store(nullptr); g_owner.store(nullptr);
        g_factory = nullptr; g_compositionDevice = nullptr; g_commandQueue = nullptr;
        g_cleanupSucceeded.store(true);
        return true;
    }

    bool IsSpeechPresentationReady() { return g_ready.load(); }
    bool IsSpeechPresentationVisible() { return g_visible.load(); }
    std::uint32_t GetSpeechPresentationGeneration() { return g_generation.load(); }
    std::uint32_t GetSpeechPresentCount() { return g_presentCount.load(); }
    std::int32_t GetSpeechPresentResult() { return g_presentResult.load(); }
    std::int32_t GetSpeechDeviceRemovedReason() { return g_deviceRemovedResult.load(); }
    std::int32_t GetSpeechFailureStage() { return g_failureStage.load(); }
    std::uint32_t GetSpeechWindowCreatedCount() { return g_createdCount.load(); }
    std::uint32_t GetSpeechWindowDestroyedCount() { return g_destroyedCount.load(); }
    std::uint32_t GetSpeechRegionCreatedCount() { return g_regionCreatedCount.load(); }
    std::uint32_t GetSpeechRegionTransferredCount() { return g_regionTransferredCount.load(); }
    std::uint32_t GetSpeechRegionCallerDeletedCount() { return g_regionCallerDeletedCount.load(); }
    std::uint32_t GetSpeechRegionLiveOwnedCount()
    {
        return g_regionCreatedCount.load()
            - g_regionTransferredCount.load()
            - g_regionCallerDeletedCount.load();
    }
    std::uint32_t GetSpeechCompositionTargetCreatedCount() { return g_targetCreatedCount.load(); }
    std::uint32_t GetSpeechVisualCreatedCount() { return g_visualCreatedCount.load(); }
    std::uint32_t GetSpeechSwapChainCreatedCount() { return g_swapChainCreatedCount.load(); }
    std::uint32_t GetSpeechFollowUpdateCount() { return g_followCount.load(); }
    std::uint32_t GetSpeechMaximumFollowErrorPixels() { return g_maximumFollowErrorPixels.load(); }
    std::uint32_t GetSpeechEdgeFlipCount() { return g_flipCount.load(); }
    std::uint32_t GetSpeechClampCount() { return g_clampCount.load(); }
    std::uint32_t GetSpeechDpi() { return g_dpi.load(); }
    bool DidSpeechOwnerMatchMascot() { return g_ownerMatches.load(); }
    bool DidSpeechCleanupSucceed() { return g_cleanupSucceeded.load(); }
    std::uint32_t GetSpeechContextAvailabilityMask()
    {
        std::uint32_t mask = 0;
        if (g_owner.load() != nullptr) mask |= 1;
        if (g_factory != nullptr) mask |= 2;
        if (g_compositionDevice != nullptr) mask |= 4;
        if (g_commandQueue != nullptr) mask |= 8;
        return mask;
    }
    std::uint32_t GetSpeechInitializeRequestCount() { return g_initializeRequestCount.load(); }
    std::uint32_t GetSpeechInitializePostSuccessCount() { return g_initializePostSuccessCount.load(); }
    std::uint32_t GetSpeechInitializeHandleCount() { return g_initializeHandleCount.load(); }
    std::uint32_t GetSpeechLiveResourceCount()
    {
        return (g_window.load() != nullptr ? 1u : 0u)
            + (g_target != nullptr ? 1u : 0u)
            + (g_visual != nullptr ? 1u : 0u)
            + (g_swapChain != nullptr ? 1u : 0u)
            + (g_allocator != nullptr ? 1u : 0u)
            + (g_commandList != nullptr ? 1u : 0u);
    }
}
