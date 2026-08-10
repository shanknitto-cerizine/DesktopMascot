#include "DesktopMascotNative/Plugin.h"
#include "DesktopMascotNative/AnimatedWindowRegionDiagnostics.h"
#include "DesktopMascotNative/CopyResourceDiagnostics.h"
#include "DesktopMascotNative/CompositionAlphaMaskDiagnostics.h"
#include "DesktopMascotNative/CompositionDiagnostics.h"
#include "DesktopMascotNative/CompositionClickThroughDiagnostics.h"
#include "DesktopMascotNative/CompositionPresentDiagnostics.h"
#include "DesktopMascotNative/CompositionPixelHitTestDiagnostics.h"
#include "DesktopMascotNative/CompositionWindowRegionDiagnostics.h"
#include "DesktopMascotNative/CompositionWindowPositionDiagnostics.h"
#include "DesktopMascotNative/ContinuousCompositionDiagnostics.h"
#include "DesktopMascotNative/DestinationTexture.h"
#include "DesktopMascotNative/NativeMascotWindowDrag.h"
#include "DesktopMascotNative/NativeMascotContextMenu.h"
#include "DesktopMascotNative/NativeTrayIcon.h"
#include "DesktopMascotNative/ReadbackDiagnostics.h"
#include "DesktopMascotNative/SingleInstanceCoordinator.h"
#include "DesktopMascotNative/StaticComplexSilhouetteDiagnostics.h"
#include "DesktopMascotNative/SpeechPresentation.h"

#include <Windows.h>
#include <d3d12.h>
#include <dxgi.h>

#include <atomic>

#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"

namespace
{
    constexpr int kD3D12InterfaceVersion = 8;

    IUnityInterfaces* g_unityInterfaces = nullptr;
    std::atomic<IUnityGraphics*> g_unityGraphics{nullptr};
    std::atomic<IUnityGraphicsD3D12v8*> g_unityGraphicsD3D12{nullptr};
    std::atomic<ID3D12Device*> g_unityD3D12Device{nullptr};
    std::atomic<ID3D12CommandQueue*> g_unityD3D12CommandQueue{nullptr};
    std::atomic<bool> g_graphicsInitialized{false};
    std::atomic<int> g_rendererType{-1};
    std::atomic<int> g_deviceEventCount{0};
    std::atomic<int> g_d3d12InterfaceVersion{0};
    std::atomic<unsigned int> g_d3d12DeviceNodeCount{0};
    std::atomic<int> g_d3d12CommandQueueType{-1};
    std::atomic<unsigned int> g_d3d12CommandQueueNodeMask{0};
    std::atomic<int> g_renderEventCount{0};
    std::atomic<int> g_lastRenderEventId{0};
    std::atomic<unsigned long> g_lastRenderThreadId{0};
    std::atomic<bool> g_graphicsReadyDuringLastRenderEvent{false};
    std::atomic<bool> g_d3d12ReadyDuringLastRenderEvent{false};
    std::atomic<int> g_textureDiagnosticEventCount{0};
    std::atomic<int> g_lastTextureDiagnosticEventId{0};
    std::atomic<bool> g_textureDataNonNull{false};
    std::atomic<bool> g_textureResourceDescriptionAvailable{false};
    std::atomic<bool> g_commandRecordingStateAvailable{false};
    std::atomic<bool> g_commandListAvailable{false};
    std::atomic<int> g_textureDimension{0};
    std::atomic<unsigned long long> g_textureAlignment{0};
    std::atomic<unsigned long long> g_textureWidth{0};
    std::atomic<unsigned int> g_textureHeight{0};
    std::atomic<unsigned int> g_textureDepthOrArraySize{0};
    std::atomic<unsigned int> g_textureMipLevels{0};
    std::atomic<int> g_textureFormat{0};
    std::atomic<unsigned int> g_textureSampleCount{0};
    std::atomic<unsigned int> g_textureSampleQuality{0};
    std::atomic<int> g_textureLayout{0};
    std::atomic<unsigned int> g_textureFlags{0};
    std::atomic<int> g_productionRequestedFps{0};
    std::atomic<int> g_productionEffectiveFps{0};
    std::atomic<int> g_productionTargetPresentCount{0};

    void ResetRenderEventDiagnostics()
    {
        g_renderEventCount.store(0, std::memory_order_relaxed);
        g_lastRenderEventId.store(0, std::memory_order_relaxed);
        g_lastRenderThreadId.store(0, std::memory_order_relaxed);
        g_graphicsReadyDuringLastRenderEvent.store(
            false,
            std::memory_order_relaxed);
        g_d3d12ReadyDuringLastRenderEvent.store(
            false,
            std::memory_order_relaxed);
    }

    void ResetTextureDiagnostics()
    {
        g_textureDiagnosticEventCount.store(0, std::memory_order_relaxed);
        g_lastTextureDiagnosticEventId.store(0, std::memory_order_relaxed);
        g_textureDataNonNull.store(false, std::memory_order_relaxed);
        g_textureResourceDescriptionAvailable.store(
            false,
            std::memory_order_relaxed);
        g_commandRecordingStateAvailable.store(false, std::memory_order_relaxed);
        g_commandListAvailable.store(false, std::memory_order_relaxed);
        g_textureDimension.store(0, std::memory_order_relaxed);
        g_textureAlignment.store(0, std::memory_order_relaxed);
        g_textureWidth.store(0, std::memory_order_relaxed);
        g_textureHeight.store(0, std::memory_order_relaxed);
        g_textureDepthOrArraySize.store(0, std::memory_order_relaxed);
        g_textureMipLevels.store(0, std::memory_order_relaxed);
        g_textureFormat.store(0, std::memory_order_relaxed);
        g_textureSampleCount.store(0, std::memory_order_relaxed);
        g_textureSampleQuality.store(0, std::memory_order_relaxed);
        g_textureLayout.store(0, std::memory_order_relaxed);
        g_textureFlags.store(0, std::memory_order_relaxed);
    }

    void ConfigureTextureDiagnosticEvent(IUnityGraphicsD3D12v8* d3d12)
    {
        const UnityD3D12PluginEventConfig config{
            kUnityD3D12GraphicsQueueAccess_DontCare,
            0,
            false};
        d3d12->ConfigureEvent(DMN_RENDER_EVENT_TEXTURE_DIAGNOSTIC, &config);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Texture diagnostic event configured.\n");

        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_DESTINATION_TEXTURE_CREATE,
            &config);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Destination texture event configured.\n");

        const UnityD3D12PluginEventConfig copyConfig{
            kUnityD3D12GraphicsQueueAccess_DontCare,
            kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState,
            false};
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_COPY_RESOURCE_DIAGNOSTIC,
            &copyConfig);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] CopyResource diagnostic event configured.\n");

        const UnityD3D12PluginEventConfig readbackCopyConfig{
            kUnityD3D12GraphicsQueueAccess_DontCare,
            kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState,
            false};
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_READBACK_COPY,
            &readbackCopyConfig);

        const UnityD3D12PluginEventConfig fenceSignalConfig{
            kUnityD3D12GraphicsQueueAccess_Allow,
            kUnityD3D12EventConfigFlag_FlushCommandBuffers
                | kUnityD3D12EventConfigFlag_SyncWorkerThreads,
            false};
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_READBACK_FENCE_SIGNAL,
            &fenceSignalConfig);

        const UnityD3D12PluginEventConfig compositionPresentConfig{
            kUnityD3D12GraphicsQueueAccess_DontCare,
            0,
            false};
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_COMPOSITION_COPY,
            &compositionPresentConfig);
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_CONTINUOUS_COMPOSITION_FRAME,
            &copyConfig);
        d3d12->ConfigureEvent(
            DMN_RENDER_EVENT_SPEECH_PRESENTATION_FRAME,
            &copyConfig);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Readback and composition Present events "
            L"configured.\n");
    }

    void ClearD3D12State()
    {
        if (!DesktopMascotNative::StopCompositionDiagnosticsForUnload(5000))
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Fatal: composition UI thread did not stop before Unity D3D12 shutdown.\n");
            ::RaiseFailFastException(nullptr, nullptr, 0);
        }
        DesktopMascotNative::ReleaseReadbackResources();
        DesktopMascotNative::ReleaseDestinationTexture();
        g_unityD3D12CommandQueue.store(nullptr, std::memory_order_release);
        g_unityD3D12Device.store(nullptr, std::memory_order_release);
        g_unityGraphicsD3D12.store(nullptr, std::memory_order_release);
        g_d3d12InterfaceVersion.store(0, std::memory_order_relaxed);
        g_d3d12DeviceNodeCount.store(0, std::memory_order_relaxed);
        g_d3d12CommandQueueType.store(-1, std::memory_order_relaxed);
        g_d3d12CommandQueueNodeMask.store(0, std::memory_order_relaxed);
    }

    void AcquireUnityD3D12Device()
    {
        ClearD3D12State();

        if (g_unityInterfaces == nullptr)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] D3D12 interface acquisition failed: "
                L"IUnityInterfaces is unavailable.\n");
            return;
        }

        auto* d3d12 = g_unityInterfaces->Get<IUnityGraphicsD3D12v8>();
        if (d3d12 == nullptr)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] D3D12 interface acquisition failed.\n");
            return;
        }

        g_unityGraphicsD3D12.store(d3d12, std::memory_order_release);
        g_d3d12InterfaceVersion.store(
            kD3D12InterfaceVersion,
            std::memory_order_relaxed);
        ConfigureTextureDiagnosticEvent(d3d12);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] D3D12 interface acquired.\n");

        auto* device = d3d12->GetDevice();
        if (device == nullptr)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Unity ID3D12Device acquisition failed.\n");
            return;
        }

        g_unityD3D12Device.store(device, std::memory_order_release);
        g_d3d12DeviceNodeCount.store(
            device->GetNodeCount(),
            std::memory_order_relaxed);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Unity ID3D12Device acquired.\n");

        auto* commandQueue = d3d12->GetCommandQueue();
        if (commandQueue == nullptr)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Unity ID3D12CommandQueue acquisition failed.\n");
            return;
        }

        const auto commandQueueDescription = commandQueue->GetDesc();
        g_unityD3D12CommandQueue.store(commandQueue, std::memory_order_release);
        g_d3d12CommandQueueType.store(
            static_cast<int>(commandQueueDescription.Type),
            std::memory_order_relaxed);
        g_d3d12CommandQueueNodeMask.store(
            commandQueueDescription.NodeMask,
            std::memory_order_relaxed);
        ::OutputDebugStringW(
            L"[DesktopMascotNative] Unity ID3D12CommandQueue acquired.\n");
    }

    void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
    {
        g_deviceEventCount.fetch_add(1, std::memory_order_relaxed);

        switch (eventType)
        {
            case kUnityGfxDeviceEventInitialize:
            {
                auto* graphics = g_unityGraphics.load(std::memory_order_acquire);
                const auto renderer =
                    graphics != nullptr ? static_cast<int>(graphics->GetRenderer()) : -1;
                g_rendererType.store(renderer, std::memory_order_relaxed);
                g_graphicsInitialized.store(true, std::memory_order_release);

                if (renderer == static_cast<int>(kUnityGfxRendererD3D12))
                {
                    AcquireUnityD3D12Device();
                }
                else
                {
                    ClearD3D12State();
                    ::OutputDebugStringW(
                        L"[DesktopMascotNative] D3D12 renderer is not active.\n");
                }

                ::OutputDebugStringW(
                    L"[DesktopMascotNative] Graphics device event: Initialize\n");
                break;
            }

            case kUnityGfxDeviceEventShutdown:
                ClearD3D12State();
                g_graphicsInitialized.store(false, std::memory_order_release);
                g_rendererType.store(-1, std::memory_order_relaxed);
                ::OutputDebugStringW(
                    L"[DesktopMascotNative] Graphics device event: Shutdown\n");
                break;

            case kUnityGfxDeviceEventBeforeReset:
                DesktopMascotNative::ReleaseDestinationTexture();
                g_graphicsInitialized.store(false, std::memory_order_release);
                ::OutputDebugStringW(
                    L"[DesktopMascotNative] Graphics device event: BeforeReset\n");
                break;

            case kUnityGfxDeviceEventAfterReset:
            {
                auto* graphics = g_unityGraphics.load(std::memory_order_acquire);
                const auto renderer =
                    graphics != nullptr ? static_cast<int>(graphics->GetRenderer()) : -1;
                g_rendererType.store(renderer, std::memory_order_relaxed);
                g_graphicsInitialized.store(true, std::memory_order_release);
                ::OutputDebugStringW(
                    L"[DesktopMascotNative] Graphics device event: AfterReset\n");
                break;
            }
        }
    }

    void UNITY_INTERFACE_API OnRenderEvent(int eventId)
    {
        if (eventId == DMN_RENDER_EVENT_COMPOSITION_COPY)
        {
            auto* d3d12 =
                g_unityGraphicsD3D12.load(std::memory_order_acquire);
            auto* device =
                g_unityD3D12Device.load(std::memory_order_acquire);
            DesktopMascotNative::HandleCompositionPresentCopyEvent(
                d3d12,
                device);
            return;
        }

        if (eventId == DMN_RENDER_EVENT_READBACK_COPY)
        {
            auto* d3d12 =
                g_unityGraphicsD3D12.load(std::memory_order_acquire);
            auto* device =
                g_unityD3D12Device.load(std::memory_order_acquire);
            DesktopMascotNative::HandleReadbackCopyEvent(d3d12, device);
            return;
        }

        if (eventId == DMN_RENDER_EVENT_READBACK_FENCE_SIGNAL)
        {
            auto* d3d12 =
                g_unityGraphicsD3D12.load(std::memory_order_acquire);
            DesktopMascotNative::HandleReadbackFenceSignalEvent(d3d12);
            return;
        }

        if (eventId == DMN_RENDER_EVENT_DESTINATION_TEXTURE_CREATE)
        {
            auto* device =
                g_unityD3D12Device.load(std::memory_order_acquire);
            DesktopMascotNative::CreateDestinationTexture(device);
            return;
        }

        if (eventId != DMN_RENDER_EVENT_DIAGNOSTIC)
        {
            return;
        }

        const bool graphicsReady =
            g_graphicsInitialized.load(std::memory_order_acquire);
        const bool d3d12Ready =
            g_unityD3D12Device.load(std::memory_order_acquire) != nullptr
            && g_unityD3D12CommandQueue.load(std::memory_order_acquire) != nullptr;

        g_lastRenderEventId.store(eventId, std::memory_order_relaxed);
        g_lastRenderThreadId.store(
            ::GetCurrentThreadId(),
            std::memory_order_relaxed);
        g_graphicsReadyDuringLastRenderEvent.store(
            graphicsReady,
            std::memory_order_relaxed);
        g_d3d12ReadyDuringLastRenderEvent.store(
            d3d12Ready,
            std::memory_order_relaxed);
        g_renderEventCount.fetch_add(1, std::memory_order_release);

        ::OutputDebugStringW(
            L"[DesktopMascotNative] Render event received: 1\n");
    }

    void UNITY_INTERFACE_API OnRenderEventAndData(int eventId, void* data)
    {
        if (eventId == DMN_RENDER_EVENT_SPEECH_PRESENTATION_FRAME)
        {
            DesktopMascotNative::HandleSpeechPresentationFrameEvent(
                g_unityGraphicsD3D12.load(std::memory_order_acquire),
                g_unityD3D12Device.load(std::memory_order_acquire),
                data);
            return;
        }
        if (eventId == DMN_RENDER_EVENT_CONTINUOUS_COMPOSITION_FRAME)
        {
            auto* d3d12 =
                g_unityGraphicsD3D12.load(std::memory_order_acquire);
            auto* device =
                g_unityD3D12Device.load(std::memory_order_acquire);
            DesktopMascotNative::HandleContinuousCompositionFrameEvent(
                d3d12,
                device,
                data);
            return;
        }

        if (eventId == DMN_RENDER_EVENT_COPY_RESOURCE_DIAGNOSTIC)
        {
            auto* d3d12 =
                g_unityGraphicsD3D12.load(std::memory_order_acquire);
            DesktopMascotNative::HandleCopyResourceDiagnosticEvent(
                d3d12,
                eventId,
                data);
            return;
        }

        if (eventId != DMN_RENDER_EVENT_TEXTURE_DIAGNOSTIC)
        {
            return;
        }

        const bool dataNonNull = data != nullptr;
        const bool graphicsInitialized =
            g_graphicsInitialized.load(std::memory_order_acquire);
        const bool isD3D12 =
            g_rendererType.load(std::memory_order_relaxed)
            == static_cast<int>(kUnityGfxRendererD3D12);
        auto* d3d12 = g_unityGraphicsD3D12.load(std::memory_order_acquire);

        bool resourceDescriptionAvailable = false;
        bool commandRecordingStateAvailable = false;
        bool commandListAvailable = false;
        D3D12_RESOURCE_DESC description{};

        if (dataNonNull && graphicsInitialized && isD3D12 && d3d12 != nullptr)
        {
            auto* resource = static_cast<ID3D12Resource*>(data);
            description = resource->GetDesc();
            resourceDescriptionAvailable = true;

            UnityGraphicsD3D12RecordingState recordingState{};
            commandRecordingStateAvailable =
                d3d12->CommandRecordingState(&recordingState);
            commandListAvailable =
                commandRecordingStateAvailable
                && recordingState.commandList != nullptr;
        }

        g_lastTextureDiagnosticEventId.store(eventId, std::memory_order_relaxed);
        g_textureDataNonNull.store(dataNonNull, std::memory_order_relaxed);
        g_textureResourceDescriptionAvailable.store(
            resourceDescriptionAvailable,
            std::memory_order_relaxed);
        g_commandRecordingStateAvailable.store(
            commandRecordingStateAvailable,
            std::memory_order_relaxed);
        g_commandListAvailable.store(
            commandListAvailable,
            std::memory_order_relaxed);
        g_textureDimension.store(
            static_cast<int>(description.Dimension),
            std::memory_order_relaxed);
        g_textureAlignment.store(
            description.Alignment,
            std::memory_order_relaxed);
        g_textureWidth.store(description.Width, std::memory_order_relaxed);
        g_textureHeight.store(description.Height, std::memory_order_relaxed);
        g_textureDepthOrArraySize.store(
            description.DepthOrArraySize,
            std::memory_order_relaxed);
        g_textureMipLevels.store(
            description.MipLevels,
            std::memory_order_relaxed);
        g_textureFormat.store(
            static_cast<int>(description.Format),
            std::memory_order_relaxed);
        g_textureSampleCount.store(
            description.SampleDesc.Count,
            std::memory_order_relaxed);
        g_textureSampleQuality.store(
            description.SampleDesc.Quality,
            std::memory_order_relaxed);
        g_textureLayout.store(
            static_cast<int>(description.Layout),
            std::memory_order_relaxed);
        g_textureFlags.store(
            static_cast<unsigned int>(description.Flags),
            std::memory_order_relaxed);

        // Publish the event last. Managed code reads this with acquire semantics
        // before reading the individual fixed-size diagnostic values.
        g_textureDiagnosticEventCount.fetch_add(1, std::memory_order_release);

        if (resourceDescriptionAvailable)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Texture diagnostic event received: "
                L"resource description captured.\n");
        }
        else
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] Texture diagnostic event received: "
                L"resource description unavailable.\n");
        }
    }
}

extern "C"
{
    void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
    {
        ::OutputDebugStringW(L"[DesktopMascotNative] UnityPluginLoad called.\n");

        g_unityInterfaces = unityInterfaces;
        g_graphicsInitialized.store(false, std::memory_order_relaxed);
        g_rendererType.store(-1, std::memory_order_relaxed);
        g_deviceEventCount.store(0, std::memory_order_relaxed);
        ClearD3D12State();
        ResetRenderEventDiagnostics();
        ResetTextureDiagnostics();
        DesktopMascotNative::ResetDestinationTextureDiagnostics();
        DesktopMascotNative::ResetCopyResourceDiagnostics();
        DesktopMascotNative::ResetReadbackDiagnostics();
        DesktopMascotNative::ResetCompositionDiagnostics();
        DesktopMascotNative::ResetCompositionPresentDiagnostics();
        DesktopMascotNative::ResetCompositionWindowPositionDiagnostics();
        DesktopMascotNative::ResetCompositionClickThroughDiagnostics();
        DesktopMascotNative::ResetCompositionAlphaMaskDiagnostics();
        DesktopMascotNative::ResetCompositionPixelHitTestDiagnostics();
        DesktopMascotNative::ResetCompositionWindowRegionDiagnostics();
        DesktopMascotNative::ResetAnimatedWindowRegionDiagnostics();
        DesktopMascotNative::ResetStaticComplexSilhouetteDiagnostics();
        DesktopMascotNative::ResetContinuousCompositionDiagnostics();
        DesktopMascotNative::ResetSpeechPresentation();

        auto* graphics =
            unityInterfaces != nullptr ? unityInterfaces->Get<IUnityGraphics>() : nullptr;
        g_unityGraphics.store(graphics, std::memory_order_release);

        if (graphics == nullptr)
        {
            ::OutputDebugStringW(
                L"[DesktopMascotNative] IUnityGraphics is unavailable.\n");
            return;
        }

        graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
        OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    }

    void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
    {
        DesktopMascotNative::StopNativeTrayIcon();
        auto* graphics = g_unityGraphics.load(std::memory_order_acquire);
        if (graphics != nullptr)
        {
            graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
        }

        g_unityGraphics.store(nullptr, std::memory_order_release);
        ClearD3D12State();
        g_graphicsInitialized.store(false, std::memory_order_relaxed);
        g_rendererType.store(-1, std::memory_order_relaxed);
        ResetRenderEventDiagnostics();
        ResetTextureDiagnostics();
        DesktopMascotNative::ResetCopyResourceDiagnostics();
        DesktopMascotNative::ResetReadbackDiagnostics();
        DesktopMascotNative::ResetCompositionPresentDiagnostics();
        DesktopMascotNative::ResetCompositionWindowPositionDiagnostics();
        DesktopMascotNative::ResetCompositionClickThroughDiagnostics();
        DesktopMascotNative::ResetCompositionAlphaMaskDiagnostics();
        DesktopMascotNative::ResetCompositionPixelHitTestDiagnostics();
        DesktopMascotNative::ResetCompositionWindowRegionDiagnostics();
        DesktopMascotNative::ResetAnimatedWindowRegionDiagnostics();
        DesktopMascotNative::ResetStaticComplexSilhouetteDiagnostics();
        DesktopMascotNative::ResetContinuousCompositionDiagnostics();
        DesktopMascotNative::ResetSpeechPresentation();
        g_unityInterfaces = nullptr;

        ::OutputDebugStringW(L"[DesktopMascotNative] UnityPluginUnload called.\n");
    }

    int UNITY_INTERFACE_EXPORT DMN_GetPluginApiVersion()
    {
        return 1;
    }

    int UNITY_INTERFACE_EXPORT DMN_IsGraphicsInitialized()
    {
        return g_graphicsInitialized.load(std::memory_order_acquire) ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetRendererType()
    {
        return g_rendererType.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDeviceEventCount()
    {
        return g_deviceEventCount.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_IsD3D12InterfaceAvailable()
    {
        return g_unityGraphicsD3D12.load(std::memory_order_acquire) != nullptr ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_IsD3D12DeviceAvailable()
    {
        return g_unityD3D12Device.load(std::memory_order_acquire) != nullptr ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetD3D12InterfaceVersion()
    {
        return g_d3d12InterfaceVersion.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetD3D12DeviceNodeCount()
    {
        return static_cast<int>(
            g_d3d12DeviceNodeCount.load(std::memory_order_relaxed));
    }

    int UNITY_INTERFACE_EXPORT DMN_IsD3D12CommandQueueAvailable()
    {
        return g_unityD3D12CommandQueue.load(std::memory_order_acquire) != nullptr
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetD3D12CommandQueueType()
    {
        return g_d3d12CommandQueueType.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetD3D12CommandQueueNodeMask()
    {
        return static_cast<int>(
            g_d3d12CommandQueueNodeMask.load(std::memory_order_relaxed));
    }

    UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        DMN_GetRenderEventFunc()
    {
        return OnRenderEvent;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetRenderEventCount()
    {
        return g_renderEventCount.load(std::memory_order_acquire);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetLastRenderEventId()
    {
        return g_lastRenderEventId.load(std::memory_order_relaxed);
    }

    unsigned long UNITY_INTERFACE_EXPORT DMN_GetLastRenderThreadId()
    {
        return g_lastRenderThreadId.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_WasGraphicsReadyDuringLastRenderEvent()
    {
        return g_graphicsReadyDuringLastRenderEvent.load(
            std::memory_order_relaxed)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasD3D12ReadyDuringLastRenderEvent()
    {
        return g_d3d12ReadyDuringLastRenderEvent.load(
            std::memory_order_relaxed)
            ? 1
            : 0;
    }

    UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
        DMN_GetRenderEventAndDataFunc()
    {
        return OnRenderEventAndData;
    }

    int UNITY_INTERFACE_EXPORT DMN_InitializeSpeechPresentation()
    { return DesktopMascotNative::RequestSpeechPresentationInitialize() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_ShowSpeechPresentation(
        unsigned int generation, int anchorX, int anchorY)
    { return DesktopMascotNative::RequestSpeechPresentationShow(generation, anchorX, anchorY) ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_HideSpeechPresentation(unsigned int generation)
    { return DesktopMascotNative::RequestSpeechPresentationHide(generation) ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_UpdateSpeechPresentationAnchor(int anchorX, int anchorY)
    { return DesktopMascotNative::UpdateSpeechPresentationAnchor(anchorX, anchorY) ? 1 : 0; }
    void UNITY_INTERFACE_EXPORT DMN_PollSpeechPresentation()
    { DesktopMascotNative::PollSpeechPresentation(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_ConsumeSpeechClickGeneration()
    { return DesktopMascotNative::ConsumeSpeechClickGeneration(); }
    int UNITY_INTERFACE_EXPORT DMN_BeginSpeechPresentationShutdown()
    { return DesktopMascotNative::BeginSpeechPresentationShutdown() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_IsSpeechPresentationReady()
    { return DesktopMascotNative::IsSpeechPresentationReady() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_IsSpeechPresentationVisible()
    { return DesktopMascotNative::IsSpeechPresentationVisible() ? 1 : 0; }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechPresentationGeneration()
    { return DesktopMascotNative::GetSpeechPresentationGeneration(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechPresentCount()
    { return DesktopMascotNative::GetSpeechPresentCount(); }
    int UNITY_INTERFACE_EXPORT DMN_GetSpeechPresentHRESULT()
    { return DesktopMascotNative::GetSpeechPresentResult(); }
    int UNITY_INTERFACE_EXPORT DMN_GetSpeechDeviceRemovedHRESULT()
    { return DesktopMascotNative::GetSpeechDeviceRemovedReason(); }
    int UNITY_INTERFACE_EXPORT DMN_GetSpeechFailureStage()
    { return DesktopMascotNative::GetSpeechFailureStage(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechWindowCreatedCount()
    { return DesktopMascotNative::GetSpeechWindowCreatedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechWindowDestroyedCount()
    { return DesktopMascotNative::GetSpeechWindowDestroyedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechRegionCreatedCount()
    { return DesktopMascotNative::GetSpeechRegionCreatedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechRegionTransferredCount()
    { return DesktopMascotNative::GetSpeechRegionTransferredCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechRegionCallerDeletedCount()
    { return DesktopMascotNative::GetSpeechRegionCallerDeletedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechFollowUpdateCount()
    { return DesktopMascotNative::GetSpeechFollowUpdateCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechEdgeFlipCount()
    { return DesktopMascotNative::GetSpeechEdgeFlipCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechClampCount()
    { return DesktopMascotNative::GetSpeechClampCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechDpi()
    { return DesktopMascotNative::GetSpeechDpi(); }
    int UNITY_INTERFACE_EXPORT DMN_DidSpeechOwnerMatchMascot()
    { return DesktopMascotNative::DidSpeechOwnerMatchMascot() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT DMN_DidSpeechCleanupSucceed()
    { return DesktopMascotNative::DidSpeechCleanupSucceed() ? 1 : 0; }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechContextAvailabilityMask()
    { return DesktopMascotNative::GetSpeechContextAvailabilityMask(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechInitializeRequestCount()
    { return DesktopMascotNative::GetSpeechInitializeRequestCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechInitializePostSuccessCount()
    { return DesktopMascotNative::GetSpeechInitializePostSuccessCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechInitializeHandleCount()
    { return DesktopMascotNative::GetSpeechInitializeHandleCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechRegionLiveOwnedCount()
    { return DesktopMascotNative::GetSpeechRegionLiveOwnedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechCompositionTargetCreatedCount()
    { return DesktopMascotNative::GetSpeechCompositionTargetCreatedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechVisualCreatedCount()
    { return DesktopMascotNative::GetSpeechVisualCreatedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechSwapChainCreatedCount()
    { return DesktopMascotNative::GetSpeechSwapChainCreatedCount(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechMaximumFollowErrorPixels()
    { return DesktopMascotNative::GetSpeechMaximumFollowErrorPixels(); }
    unsigned int UNITY_INTERFACE_EXPORT DMN_GetSpeechLiveResourceCount()
    { return DesktopMascotNative::GetSpeechLiveResourceCount(); }

    int UNITY_INTERFACE_EXPORT DMN_GetTextureDiagnosticEventCount()
    {
        return g_textureDiagnosticEventCount.load(std::memory_order_acquire);
    }

    int UNITY_INTERFACE_EXPORT DMN_WasTextureDataNonNull()
    {
        return g_textureDataNonNull.load(std::memory_order_relaxed) ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasTextureResourceDescriptionAvailable()
    {
        return g_textureResourceDescriptionAvailable.load(
            std::memory_order_relaxed)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCommandRecordingStateAvailable()
    {
        return g_commandRecordingStateAvailable.load(
            std::memory_order_relaxed)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCommandListAvailable()
    {
        return g_commandListAvailable.load(std::memory_order_relaxed) ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetTextureDimension()
    {
        return g_textureDimension.load(std::memory_order_relaxed);
    }

    unsigned long long UNITY_INTERFACE_EXPORT DMN_GetTextureAlignment()
    {
        return g_textureAlignment.load(std::memory_order_relaxed);
    }

    unsigned long long UNITY_INTERFACE_EXPORT DMN_GetTextureWidth()
    {
        return g_textureWidth.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureHeight()
    {
        return g_textureHeight.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureDepthOrArraySize()
    {
        return g_textureDepthOrArraySize.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureMipLevels()
    {
        return g_textureMipLevels.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetTextureFormat()
    {
        return g_textureFormat.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureSampleCount()
    {
        return g_textureSampleCount.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureSampleQuality()
    {
        return g_textureSampleQuality.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetTextureLayout()
    {
        return g_textureLayout.load(std::memory_order_relaxed);
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetTextureFlags()
    {
        return g_textureFlags.load(std::memory_order_relaxed);
    }

    int UNITY_INTERFACE_EXPORT DMN_IsDestinationTextureAvailable()
    {
        return DesktopMascotNative::IsDestinationTextureAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureCreationResult()
    {
        return DesktopMascotNative::GetDestinationTextureCreationResult();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureDimension()
    {
        return DesktopMascotNative::GetDestinationTextureDimension();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetDestinationTextureWidth()
    {
        return DesktopMascotNative::GetDestinationTextureWidth();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureHeight()
    {
        return DesktopMascotNative::GetDestinationTextureHeight();
    }

    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetDestinationTextureDepthOrArraySize()
    {
        return
            DesktopMascotNative::GetDestinationTextureDepthOrArraySize();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureMipLevels()
    {
        return DesktopMascotNative::GetDestinationTextureMipLevels();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureFormat()
    {
        return DesktopMascotNative::GetDestinationTextureFormat();
    }

    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetDestinationTextureSampleCount()
    {
        return DesktopMascotNative::GetDestinationTextureSampleCount();
    }

    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetDestinationTextureSampleQuality()
    {
        return DesktopMascotNative::GetDestinationTextureSampleQuality();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureLayout()
    {
        return DesktopMascotNative::GetDestinationTextureLayout();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureFlags()
    {
        return DesktopMascotNative::GetDestinationTextureFlags();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetDestinationTextureInitialState()
    {
        return DesktopMascotNative::GetDestinationTextureInitialState();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopyDiagnosticEventCount()
    {
        return DesktopMascotNative::GetCopyDiagnosticEventCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetLastCopyDiagnosticEventId()
    {
        return DesktopMascotNative::GetLastCopyDiagnosticEventId();
    }

    unsigned long UNITY_INTERFACE_EXPORT
        DMN_GetLastCopyDiagnosticRenderThreadId()
    {
        return DesktopMascotNative::GetLastCopyDiagnosticRenderThreadId();
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopyEventDataNonNull()
    {
        return DesktopMascotNative::WasCopyEventDataNonNull() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopySourceAvailable()
    {
        return DesktopMascotNative::WasCopySourceAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopyDestinationAvailable()
    {
        return DesktopMascotNative::WasCopyDestinationAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_WasCopyCommandRecordingStateAvailable()
    {
        return
            DesktopMascotNative::WasCopyCommandRecordingStateAvailable()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopyCommandListAvailable()
    {
        return DesktopMascotNative::WasCopyCommandListAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_DidCopyValidationPass()
    {
        return DesktopMascotNative::DidCopyValidationPass() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_WasCopyResourceStateRequestAttempted()
    {
        return
            DesktopMascotNative::WasCopyResourceStateRequestAttempted()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_DidCopyResourceStateRequestSucceed()
    {
        return
            DesktopMascotNative::DidCopyResourceStateRequestSucceed()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopyResourceRecorded()
    {
        return DesktopMascotNative::WasCopyResourceRecorded() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_WasCopyResourceStateNotificationAttempted()
    {
        return
            DesktopMascotNative::
                WasCopyResourceStateNotificationAttempted()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_DidCopyResourceStateNotificationComplete()
    {
        return
            DesktopMascotNative::
                DidCopyResourceStateNotificationComplete()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasCopyAlreadyRecorded()
    {
        return DesktopMascotNative::WasCopyAlreadyRecorded() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopyFailureStage()
    {
        return DesktopMascotNative::GetCopyFailureStage();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopySourceRequestedState()
    {
        return DesktopMascotNative::GetCopySourceRequestedState();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopyDestinationTrackedState()
    {
        return DesktopMascotNative::GetCopyDestinationTrackedState();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopyAttemptCount()
    {
        return DesktopMascotNative::GetCopyAttemptCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCopySuccessCount()
    {
        return DesktopMascotNative::GetCopySuccessCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_IsReadbackBufferAvailable()
    {
        return DesktopMascotNative::IsReadbackBufferAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetReadbackBufferCreationResult()
    {
        return DesktopMascotNative::GetReadbackBufferCreationResult();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetReadbackFootprintOffset()
    {
        return DesktopMascotNative::GetReadbackFootprintOffset();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackFootprintRowPitch()
    {
        return DesktopMascotNative::GetReadbackFootprintRowPitch();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackNumRows()
    {
        return DesktopMascotNative::GetReadbackNumRows();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetReadbackRowSizeInBytes()
    {
        return DesktopMascotNative::GetReadbackRowSizeInBytes();
    }

    unsigned long long UNITY_INTERFACE_EXPORT DMN_GetReadbackTotalBytes()
    {
        return DesktopMascotNative::GetReadbackTotalBytes();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetReadbackCopyEventCount()
    {
        return DesktopMascotNative::GetReadbackCopyEventCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_WasReadbackCopyCallbackReached()
    {
        return DesktopMascotNative::WasReadbackCopyCallbackReached() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasReadbackCopyRecorded()
    {
        return DesktopMascotNative::WasReadbackCopyRecorded() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_IsReadbackFenceAvailable()
    {
        return DesktopMascotNative::IsReadbackFenceAvailable() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasReadbackFenceSignalAttempted()
    {
        return DesktopMascotNative::WasReadbackFenceSignalAttempted() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_DidReadbackFenceSignalSucceed()
    {
        return DesktopMascotNative::DidReadbackFenceSignalSucceed() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetReadbackFenceSignalHRESULT()
    {
        return DesktopMascotNative::GetReadbackFenceSignalResult();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetReadbackFenceSubmittedValue()
    {
        return DesktopMascotNative::GetReadbackFenceSubmittedValue();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetReadbackFenceCompletedValue()
    {
        return DesktopMascotNative::GetReadbackFenceCompletedValue();
    }

    int UNITY_INTERFACE_EXPORT DMN_IsReadbackFenceComplete()
    {
        return DesktopMascotNative::IsReadbackFenceComplete() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_ValidateReadbackPixels()
    {
        return DesktopMascotNative::ValidateReadbackPixels();
    }

    int UNITY_INTERFACE_EXPORT DMN_WasReadbackMapAttempted()
    {
        return DesktopMascotNative::WasReadbackMapAttempted() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_DidReadbackMapSucceed()
    {
        return DesktopMascotNative::DidReadbackMapSucceed() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_WasReadbackValidationCompleted()
    {
        return DesktopMascotNative::WasReadbackValidationCompleted() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_DidReadbackExpectedOrientationMatch()
    {
        return
            DesktopMascotNative::DidReadbackExpectedOrientationMatch()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_DidReadbackVerticallyFlippedOrientationMatch()
    {
        return
            DesktopMascotNative::
                DidReadbackVerticallyFlippedOrientationMatch()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetReadbackFailureStage()
    {
        return DesktopMascotNative::GetReadbackFailureStage();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackTopLeftBGRA()
    {
        return DesktopMascotNative::GetReadbackTopLeftBgra();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackTopRightBGRA()
    {
        return DesktopMascotNative::GetReadbackTopRightBgra();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackBottomLeftBGRA()
    {
        return DesktopMascotNative::GetReadbackBottomLeftBgra();
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetReadbackBottomRightBGRA()
    {
        return DesktopMascotNative::GetReadbackBottomRightBgra();
    }

    int UNITY_INTERFACE_EXPORT DMN_StartCompositionDiagnostics()
    {
        auto* commandQueue =
            g_unityD3D12CommandQueue.load(std::memory_order_acquire);
        return DesktopMascotNative::StartCompositionDiagnostics(commandQueue);
    }

    int UNITY_INTERFACE_EXPORT DMN_RequestCompositionDiagnosticsShutdown()
    {
        return DesktopMascotNative::RequestCompositionDiagnosticsShutdown()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_FinalizeCompositionDiagnosticsThread()
    {
        return DesktopMascotNative::StopCompositionDiagnosticsForUnload(5000)
            ? 1
            : 0;
    }

    unsigned int UNITY_INTERFACE_EXPORT DMN_GetCurrentProcessGdiObjectCount()
    {
        return ::GetGuiResources(::GetCurrentProcess(), GR_GDIOBJECTS);
    }

#define DMN_EXPORT_COMPOSITION_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_COMPOSITION_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_COMPOSITION_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionInitializationState,
        GetCompositionInitializationState)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionFailureStage,
        GetCompositionFailureStage)
    DMN_EXPORT_COMPOSITION_UINT(
        DMN_GetCompositionUiThreadId,
        GetCompositionUiThreadId)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasCompositionComInitializationAttempted,
        WasCompositionComInitializationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_DidCompositionComInitializationSucceed,
        DidCompositionComInitializationSucceed)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionComInitializationHRESULT,
        GetCompositionComInitializationResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasCompositionWindowClassRegistrationAttempted,
        WasCompositionWindowClassRegistrationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_DidCompositionWindowClassRegistrationSucceed,
        DidCompositionWindowClassRegistrationSucceed)
    DMN_EXPORT_COMPOSITION_UINT(
        DMN_GetCompositionWindowClassLastError,
        GetCompositionWindowClassLastError)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasCompositionWindowCreationAttempted,
        WasCompositionWindowCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsCompositionWindowAvailable,
        IsCompositionWindowAvailable)
    DMN_EXPORT_COMPOSITION_UINT(
        DMN_GetCompositionWindowCreationLastError,
        GetCompositionWindowCreationLastError)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionWindowClientWidth,
        GetCompositionWindowClientWidth)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionWindowClientHeight,
        GetCompositionWindowClientHeight)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDxgiFactoryCreationAttempted,
        WasDxgiFactoryCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsDxgiFactoryAvailable,
        IsDxgiFactoryAvailable)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDxgiFactoryCreationHRESULT,
        GetDxgiFactoryCreationResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompDeviceCreationAttempted,
        WasDCompDeviceCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsDCompDeviceAvailable,
        IsDCompDeviceAvailable)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompDeviceCreationHRESULT,
        GetDCompDeviceCreationResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompTargetCreationAttempted,
        WasDCompTargetCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsDCompTargetAvailable,
        IsDCompTargetAvailable)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompTargetCreationHRESULT,
        GetDCompTargetCreationResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompVisualCreationAttempted,
        WasDCompVisualCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsDCompVisualAvailable,
        IsDCompVisualAvailable)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompVisualCreationHRESULT,
        GetDCompVisualCreationResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasCompositionSwapChainCreationAttempted,
        WasCompositionSwapChainCreationAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsCompositionSwapChainAvailable,
        IsCompositionSwapChainAvailable)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainCreationHRESULT,
        GetCompositionSwapChainCreationResult)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainWidth,
        GetCompositionSwapChainWidth)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainHeight,
        GetCompositionSwapChainHeight)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainFormat,
        GetCompositionSwapChainFormat)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainBufferCount,
        GetCompositionSwapChainBufferCount)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainSwapEffect,
        GetCompositionSwapChainSwapEffect)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainAlphaMode,
        GetCompositionSwapChainAlphaMode)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetCompositionSwapChainScaling,
        GetCompositionSwapChainScaling)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompSetContentAttempted,
        WasDCompSetContentAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_DidDCompSetContentSucceed,
        DidDCompSetContentSucceed)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompSetContentHRESULT,
        GetDCompSetContentResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompSetRootAttempted,
        WasDCompSetRootAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_DidDCompSetRootSucceed,
        DidDCompSetRootSucceed)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompSetRootHRESULT,
        GetDCompSetRootResult)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasDCompCommitAttempted,
        WasDCompCommitAttempted)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_DidDCompCommitSucceed,
        DidDCompCommitSucceed)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompCommitHRESULT,
        GetDCompCommitResult)
    DMN_EXPORT_COMPOSITION_INT(
        DMN_GetDCompCommitCount,
        GetDCompCommitCount)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_WasCompositionWindowShown,
        WasCompositionWindowShown)
    DMN_EXPORT_COMPOSITION_BOOL(
        DMN_IsCompositionMessageLoopRunning,
        IsCompositionMessageLoopRunning)
#undef DMN_EXPORT_COMPOSITION_INT
#undef DMN_EXPORT_COMPOSITION_BOOL
#undef DMN_EXPORT_COMPOSITION_UINT

    int UNITY_INTERFACE_EXPORT DMN_StartCompositionPresentDiagnostics()
    {
        return
            DesktopMascotNative::StartCompositionPresentDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT DMN_MarkCompositionCopyEventIssued()
    {
        return DesktopMascotNative::MarkCompositionCopyEventIssued() ? 1 : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_PollCompositionPresentDiagnostics()
    {
        DesktopMascotNative::PollCompositionPresentDiagnostics();
        return DesktopMascotNative::GetCompositionPresentState();
    }

    int UNITY_INTERFACE_EXPORT DMN_MarkCompositionDisplayHolding()
    {
        DesktopMascotNative::MarkCompositionDisplayHolding();
        return DesktopMascotNative::GetCompositionPresentState();
    }

#define DMN_EXPORT_PRESENT_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_PRESENT_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_PRESENT_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_PRESENT_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionPresentState,
        GetCompositionPresentState)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionPresentFailureStage,
        GetCompositionPresentFailureStage)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionCommandSubmissionMode,
        GetCompositionCommandSubmissionMode)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasSwapChain3QueryAttempted,
        WasSwapChain3QueryAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsSwapChain3Available,
        IsSwapChain3Available)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetSwapChain3QueryHRESULT,
        GetSwapChain3QueryResult)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasBackBufferIndexQueried,
        WasBackBufferIndexQueried)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetCurrentBackBufferIndex,
        GetCurrentBackBufferIndex)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsCurrentBackBufferIndexValid,
        IsCurrentBackBufferIndexValid)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasBackBufferGetAttempted,
        WasBackBufferGetAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsBackBufferAvailable,
        IsBackBufferAvailable)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetBackBufferGetHRESULT,
        GetBackBufferGetResult)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsBackBufferDescriptionAvailable,
        IsBackBufferDescriptionAvailable)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetBackBufferDimension,
        GetBackBufferDimension)
    DMN_EXPORT_PRESENT_U64(
        DMN_GetBackBufferWidth,
        GetBackBufferWidth)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferHeight,
        GetBackBufferHeight)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferDepthOrArraySize,
        GetBackBufferDepthOrArraySize)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferMipLevels,
        GetBackBufferMipLevels)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetBackBufferFormat,
        GetBackBufferFormat)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferSampleCount,
        GetBackBufferSampleCount)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferSampleQuality,
        GetBackBufferSampleQuality)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetBackBufferLayout,
        GetBackBufferLayout)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetBackBufferFlags,
        GetBackBufferFlags)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionCopyCompatibilityChecked,
        WasCompositionCopyCompatibilityChecked)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_AreCompositionCopyDimensionsCompatible,
        AreCompositionCopyDimensionsCompatible)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_AreCompositionCopySamplesCompatible,
        AreCompositionCopySamplesCompatible)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_AreCompositionCopyFormatsCompatible,
        AreCompositionCopyFormatsCompatible)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionCopyEventIssued,
        WasCompositionCopyEventIssued)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionCopyEventCount,
        GetCompositionCopyEventCount)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionCopyCallbackReached,
        WasCompositionCopyCallbackReached)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WereCompositionCopyBarriersRecorded,
        WereCompositionCopyBarriersRecorded)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionBackBufferCopyRecorded,
        WasCompositionBackBufferCopyRecorded)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionCopySubmitted,
        WasCompositionCopySubmitted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionExecuteCommandListAttempted,
        WasCompositionExecuteCommandListAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidCompositionExecuteCommandListReturnFenceValue,
        DidCompositionExecuteCommandListReturnFenceValue)
    DMN_EXPORT_PRESENT_U64(
        DMN_GetCompositionExecuteCommandListFenceValue,
        GetCompositionExecuteCommandListFenceValue)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionPresentFenceCreated,
        WasCompositionPresentFenceCreated)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsCompositionPresentFenceAvailable,
        IsCompositionPresentFenceAvailable)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionPresentFenceSignalAttempted,
        WasCompositionPresentFenceSignalAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidCompositionPresentFenceSignalSucceed,
        DidCompositionPresentFenceSignalSucceed)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionPresentFenceSignalHRESULT,
        GetCompositionPresentFenceSignalResult)
    DMN_EXPORT_PRESENT_U64(
        DMN_GetCompositionPresentFenceSubmittedValue,
        GetCompositionPresentFenceSubmittedValue)
    DMN_EXPORT_PRESENT_U64(
        DMN_GetCompositionPresentFenceCompletedValue,
        GetCompositionPresentFenceCompletedValue)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_IsCompositionPresentFenceComplete,
        IsCompositionPresentFenceComplete)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidCompositionPresentFenceTimeout,
        DidCompositionPresentFenceTimeout)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionPresentMessagePostAttempted,
        WasCompositionPresentMessagePostAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidCompositionPresentMessagePostSucceed,
        DidCompositionPresentMessagePostSucceed)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetCompositionPresentMessagePostLastError,
        GetCompositionPresentMessagePostLastError)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionPresentMessageReceived,
        WasCompositionPresentMessageReceived)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasCompositionPresentAttempted,
        WasCompositionPresentAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidCompositionPresentSucceed,
        DidCompositionPresentSucceed)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionPresentHRESULT,
        GetCompositionPresentResult)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetCompositionPresentSyncInterval,
        GetCompositionPresentSyncInterval)
    DMN_EXPORT_PRESENT_UINT(
        DMN_GetCompositionPresentFlags,
        GetCompositionPresentFlags)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetCompositionPresentCount,
        GetCompositionPresentCount)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_WasPostPresentCommitAttempted,
        WasPostPresentCommitAttempted)
    DMN_EXPORT_PRESENT_BOOL(
        DMN_DidPostPresentCommitSucceed,
        DidPostPresentCommitSucceed)
    DMN_EXPORT_PRESENT_INT(
        DMN_GetPostPresentCommitHRESULT,
        GetPostPresentCommitResult)
#undef DMN_EXPORT_PRESENT_INT
#undef DMN_EXPORT_PRESENT_BOOL
#undef DMN_EXPORT_PRESENT_UINT
#undef DMN_EXPORT_PRESENT_U64

    int UNITY_INTERFACE_EXPORT DMN_StartContinuousCompositionDiagnostics()
    {
        return DesktopMascotNative::
            StartContinuousCompositionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_SetContinuousCompositionTargetFrameCountForNextRun(
            unsigned int targetFrameCount)
    {
        return DesktopMascotNative::
            SetContinuousCompositionTargetFrameCountForNextRun(
                targetFrameCount)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_EnableContinuousCompositionRuntimeModeForNextRun()
    {
        return DesktopMascotNative::
            EnableContinuousCompositionRuntimeModeForNextRun()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_RequestContinuousCompositionFrame()
    {
        return DesktopMascotNative::RequestContinuousCompositionFrame();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_RequestContinuousCompositionDiagnosticsStop()
    {
        return DesktopMascotNative::
            RequestContinuousCompositionDiagnosticsStop();
    }

    int UNITY_INTERFACE_EXPORT DMN_PollContinuousCompositionDiagnostics()
    {
        return DesktopMascotNative::
            PollContinuousCompositionDiagnosticsAndGetState();
    }

    void UNITY_INTERFACE_EXPORT
        DMN_RecordContinuousCompositionDroppedSchedule()
    {
        DesktopMascotNative::RecordContinuousCompositionDroppedSchedule();
    }

#define DMN_EXPORT_CONTINUOUS_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_CONTINUOUS_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_CONTINUOUS_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_CONTINUOUS_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_CONTINUOUS_INT(
        DMN_GetContinuousCompositionState,
        GetContinuousCompositionState)
    DMN_EXPORT_CONTINUOUS_INT(
        DMN_GetContinuousCompositionFailureStage,
        GetContinuousCompositionFailureStage)
    DMN_EXPORT_CONTINUOUS_INT(
        DMN_GetContinuousCompositionCommandSubmissionMode,
        GetContinuousCompositionCommandSubmissionMode)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionTargetFrameCount,
        GetContinuousCompositionTargetFrameCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionRequestedFrameCount,
        GetContinuousCompositionRequestedFrameCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionRecordedFrameCount,
        GetContinuousCompositionRecordedFrameCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionSubmittedFrameCount,
        GetContinuousCompositionSubmittedFrameCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionFenceCompletedFrameCount,
        GetContinuousCompositionFenceCompletedFrameCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionPresentCount,
        GetContinuousCompositionPresentCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionLastSequence,
        GetContinuousCompositionLastSequence)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionLastBackBufferIndex,
        GetContinuousCompositionLastBackBufferIndex)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionBackBuffer0UseCount,
        GetContinuousCompositionBackBuffer0UseCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionBackBuffer1UseCount,
        GetContinuousCompositionBackBuffer1UseCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionInvalidBackBufferIndexCount,
        GetContinuousCompositionInvalidBackBufferIndexCount)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionLastFenceSubmittedValue,
        GetContinuousCompositionLastFenceSubmittedValue)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionLastFenceCompletedValue,
        GetContinuousCompositionLastFenceCompletedValue)
    DMN_EXPORT_CONTINUOUS_BOOL(
        DMN_IsContinuousCompositionFrameInFlight,
        IsContinuousCompositionFrameInFlight)
    DMN_EXPORT_CONTINUOUS_INT(
        DMN_GetContinuousCompositionLastPresentHRESULT,
        GetContinuousCompositionLastPresentResult)
    DMN_EXPORT_CONTINUOUS_INT(
        DMN_GetContinuousCompositionLastDeviceRemovedReason,
        GetContinuousCompositionLastDeviceRemovedReason)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionElapsedMilliseconds,
        GetContinuousCompositionElapsedMilliseconds)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionMinimumFrameMilliseconds,
        GetContinuousCompositionMinimumFrameMilliseconds)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionMaximumFrameMilliseconds,
        GetContinuousCompositionMaximumFrameMilliseconds)
    DMN_EXPORT_CONTINUOUS_U64(
        DMN_GetContinuousCompositionAverageFrameMicroseconds,
        GetContinuousCompositionAverageFrameMicroseconds)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionDroppedScheduleCount,
        GetContinuousCompositionDroppedScheduleCount)
    DMN_EXPORT_CONTINUOUS_UINT(
        DMN_GetContinuousCompositionRejectedFrameRequestCount,
        GetContinuousCompositionRejectedFrameRequestCount)
    DMN_EXPORT_CONTINUOUS_BOOL(
        DMN_DidContinuousCompositionCompleteNormally,
        DidContinuousCompositionCompleteNormally)
    DMN_EXPORT_CONTINUOUS_BOOL(
        DMN_DidContinuousCompositionTimeout,
        DidContinuousCompositionTimeout)
#undef DMN_EXPORT_CONTINUOUS_INT
#undef DMN_EXPORT_CONTINUOUS_BOOL
#undef DMN_EXPORT_CONTINUOUS_UINT
#undef DMN_EXPORT_CONTINUOUS_U64

    int UNITY_INTERFACE_EXPORT
        DMN_StartCompositionWindowPositionDiagnostics()
    {
        return DesktopMascotNative::
            StartCompositionWindowPositionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT DMN_RequestCompositionWindowPosition(
        int x,
        int y)
    {
        return DesktopMascotNative::RequestCompositionWindowPosition(x, y);
    }

#define DMN_EXPORT_POSITION_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_POSITION_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_POSITION_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowPositionState,
        GetCompositionWindowPositionState)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowPositionFailureStage,
        GetCompositionWindowPositionFailureStage)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowRequestedX,
        GetCompositionWindowRequestedX)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowRequestedY,
        GetCompositionWindowRequestedY)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowActualX,
        GetCompositionWindowActualX)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowActualY,
        GetCompositionWindowActualY)
    DMN_EXPORT_POSITION_UINT(
        DMN_GetCompositionWindowMoveRequestCount,
        GetCompositionWindowMoveRequestCount)
    DMN_EXPORT_POSITION_UINT(
        DMN_GetCompositionWindowMoveAppliedCount,
        GetCompositionWindowMoveAppliedCount)
    DMN_EXPORT_POSITION_UINT(
        DMN_GetCompositionWindowMoveRejectedCount,
        GetCompositionWindowMoveRejectedCount)
    DMN_EXPORT_POSITION_UINT(
        DMN_GetCompositionWindowLastSetWindowPosLastError,
        GetCompositionWindowLastSetWindowPosLastError)
    DMN_EXPORT_POSITION_BOOL(
        DMN_IsCompositionWindowMovePending,
        IsCompositionWindowMovePending)
    DMN_EXPORT_POSITION_BOOL(
        DMN_DidCompositionWindowLastMoveSucceed,
        DidCompositionWindowLastMoveSucceed)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWorkAreaLeft,
        GetCompositionWorkAreaLeft)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWorkAreaTop,
        GetCompositionWorkAreaTop)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWorkAreaRight,
        GetCompositionWorkAreaRight)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWorkAreaBottom,
        GetCompositionWorkAreaBottom)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowInitialX,
        GetCompositionWindowInitialX)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowInitialY,
        GetCompositionWindowInitialY)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowInitialWidth,
        GetCompositionWindowInitialWidth)
    DMN_EXPORT_POSITION_INT(
        DMN_GetCompositionWindowInitialHeight,
        GetCompositionWindowInitialHeight)
#undef DMN_EXPORT_POSITION_INT
#undef DMN_EXPORT_POSITION_BOOL
#undef DMN_EXPORT_POSITION_UINT

    int UNITY_INTERFACE_EXPORT
        DMN_StartCompositionClickThroughDiagnostics()
    {
        return DesktopMascotNative::
            StartCompositionClickThroughDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_RequestCompositionClickThroughEnabled(int enabled)
    {
        return DesktopMascotNative::
            RequestCompositionClickThroughEnabled(enabled);
    }

#define DMN_EXPORT_CLICK_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_CLICK_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_CLICK_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_CLICK_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_CLICK_INT(
        DMN_GetCompositionClickThroughState,
        GetCompositionClickThroughState)
    DMN_EXPORT_CLICK_INT(
        DMN_GetCompositionClickThroughFailureStage,
        GetCompositionClickThroughFailureStage)
    DMN_EXPORT_CLICK_BOOL(
        DMN_IsCompositionClickThroughEnabled,
        IsCompositionClickThroughEnabled)
    DMN_EXPORT_CLICK_BOOL(
        DMN_IsCompositionClickThroughRequestPending,
        IsCompositionClickThroughRequestPending)
    DMN_EXPORT_CLICK_BOOL(
        DMN_DidCompositionClickThroughLastRequestSucceed,
        DidCompositionClickThroughLastRequestSucceed)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughEnableRequestCount,
        GetCompositionClickThroughEnableRequestCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughDisableRequestCount,
        GetCompositionClickThroughDisableRequestCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughAppliedCount,
        GetCompositionClickThroughAppliedCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughRejectedCount,
        GetCompositionClickThroughRejectedCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughLastWin32Error,
        GetCompositionClickThroughLastWin32Error)
    DMN_EXPORT_CLICK_U64(
        DMN_GetCompositionWindowInitialStyle,
        GetCompositionWindowInitialStyle)
    DMN_EXPORT_CLICK_U64(
        DMN_GetCompositionWindowCurrentStyle,
        GetCompositionWindowCurrentStyle)
    DMN_EXPORT_CLICK_U64(
        DMN_GetCompositionWindowInitialExtendedStyle,
        GetCompositionWindowInitialExtendedStyle)
    DMN_EXPORT_CLICK_U64(
        DMN_GetCompositionWindowCurrentExtendedStyle,
        GetCompositionWindowCurrentExtendedStyle)
    DMN_EXPORT_CLICK_U64(
        DMN_GetCompositionWindowClassStyle,
        GetCompositionWindowClassStyle)
    DMN_EXPORT_CLICK_BOOL(
        DMN_IsCompositionInitialExtendedStyleRestored,
        IsCompositionInitialExtendedStyleRestored)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughHitTestCount,
        GetCompositionClickThroughHitTestCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughTransparentHitTestCount,
        GetCompositionClickThroughTransparentHitTestCount)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughLastRequestThreadId,
        GetCompositionClickThroughLastRequestThreadId)
    DMN_EXPORT_CLICK_UINT(
        DMN_GetCompositionClickThroughLastAppliedThreadId,
        GetCompositionClickThroughLastAppliedThreadId)
#undef DMN_EXPORT_CLICK_INT
#undef DMN_EXPORT_CLICK_BOOL
#undef DMN_EXPORT_CLICK_UINT
#undef DMN_EXPORT_CLICK_U64

    int UNITY_INTERFACE_EXPORT DMN_StartCompositionAlphaMaskDiagnostics(
        int width,
        int height,
        int stride,
        int alphaThreshold)
    {
        return DesktopMascotNative::StartCompositionAlphaMaskDiagnostics(
            width,
            height,
            stride,
            alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
            int width,
            int height,
            int stride,
            int alphaThreshold)
    {
        return DesktopMascotNative::
            StartAnimatedCompositionAlphaMaskDiagnostics(
                width, height, stride, alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT DMN_SubmitCompositionAlphaMask(
        const unsigned char* data,
        int width,
        int height,
        int stride,
        unsigned long long generation)
    {
        return DesktopMascotNative::SubmitCompositionAlphaMask(
            data,
            width,
            height,
            stride,
            generation);
    }

    void UNITY_INTERFACE_EXPORT DMN_StopCompositionAlphaMaskDiagnostics()
    {
        DesktopMascotNative::StopCompositionAlphaMaskDiagnostics();
    }

#define DMN_EXPORT_ALPHA_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_ALPHA_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_ALPHA_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_ALPHA_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskState,
        GetCompositionAlphaMaskState)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskFailureStage,
        GetCompositionAlphaMaskFailureStage)
    DMN_EXPORT_ALPHA_UINT(
        DMN_GetCompositionAlphaMaskSubmittedCount,
        GetCompositionAlphaMaskSubmittedCount)
    DMN_EXPORT_ALPHA_UINT(
        DMN_GetCompositionAlphaMaskAcceptedCount,
        GetCompositionAlphaMaskAcceptedCount)
    DMN_EXPORT_ALPHA_UINT(
        DMN_GetCompositionAlphaMaskRejectedCount,
        GetCompositionAlphaMaskRejectedCount)
    DMN_EXPORT_ALPHA_U64(
        DMN_GetCompositionAlphaMaskPublishedGeneration,
        GetCompositionAlphaMaskPublishedGeneration)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskWidth,
        GetCompositionAlphaMaskWidth)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskHeight,
        GetCompositionAlphaMaskHeight)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskStride,
        GetCompositionAlphaMaskStride)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskByteCount,
        GetCompositionAlphaMaskByteCount)
    DMN_EXPORT_ALPHA_INT(
        DMN_GetCompositionAlphaMaskThreshold,
        GetCompositionAlphaMaskThreshold)
    DMN_EXPORT_ALPHA_BOOL(
        DMN_IsCompositionAlphaMaskAvailable,
        IsCompositionAlphaMaskAvailable)
    DMN_EXPORT_ALPHA_BOOL(
        DMN_IsCompositionAlphaMaskRequestPending,
        IsCompositionAlphaMaskRequestPending)
    DMN_EXPORT_ALPHA_BOOL(
        DMN_DidCompositionAlphaMaskLastSubmitSucceed,
        DidCompositionAlphaMaskLastSubmitSucceed)
    DMN_EXPORT_ALPHA_UINT(
        DMN_GetCompositionAlphaMaskLastWin32Error,
        GetCompositionAlphaMaskLastWin32Error)
    DMN_EXPORT_ALPHA_UINT(
        DMN_GetCompositionAlphaMaskLastSubmitThreadId,
        GetCompositionAlphaMaskLastSubmitThreadId)
#undef DMN_EXPORT_ALPHA_INT
#undef DMN_EXPORT_ALPHA_BOOL
#undef DMN_EXPORT_ALPHA_UINT
#undef DMN_EXPORT_ALPHA_U64

    int UNITY_INTERFACE_EXPORT DMN_GetCompositionAlphaMaskSampleAlpha(
        int x,
        int y)
    {
        return DesktopMascotNative::GetCompositionAlphaMaskSampleAlpha(x, y);
    }

    int UNITY_INTERFACE_EXPORT DMN_GetCompositionAlphaMaskSampleHit(
        int x,
        int y)
    {
        return DesktopMascotNative::GetCompositionAlphaMaskSampleHit(x, y)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartCompositionPixelHitTestDiagnostics(int alphaThreshold)
    {
        return DesktopMascotNative::
            StartCompositionPixelHitTestDiagnostics(alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_CompleteCompositionPixelHitTestDiagnostics()
    {
        return DesktopMascotNative::
            CompleteCompositionPixelHitTestDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StopCompositionPixelHitTestDiagnostics()
    {
        return DesktopMascotNative::
            StopCompositionPixelHitTestDiagnostics();
    }

#define DMN_EXPORT_PIXEL_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_PIXEL_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_PIXEL_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_PIXEL_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestState,
        GetCompositionPixelHitTestState)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestFailureStage,
        GetCompositionPixelHitTestFailureStage)
    DMN_EXPORT_PIXEL_BOOL(
        DMN_IsCompositionPixelHitTestEnabled,
        IsCompositionPixelHitTestEnabled)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestThreshold,
        GetCompositionPixelHitTestThreshold)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestTotalCount,
        GetCompositionPixelHitTestTotalCount)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestTransparentCount,
        GetCompositionPixelHitTestTransparentCount)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestOpaqueCount,
        GetCompositionPixelHitTestOpaqueCount)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestOutsideClientCount,
        GetCompositionPixelHitTestOutsideClientCount)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestMaskUnavailableCount,
        GetCompositionPixelHitTestMaskUnavailableCount)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestScreenToClientFailureCount,
        GetCompositionPixelHitTestScreenToClientFailureCount)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestLastX,
        GetCompositionPixelHitTestLastX)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestLastY,
        GetCompositionPixelHitTestLastY)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestLastAlpha,
        GetCompositionPixelHitTestLastAlpha)
    DMN_EXPORT_PIXEL_INT(
        DMN_GetCompositionPixelHitTestLastResult,
        GetCompositionPixelHitTestLastResult)
    DMN_EXPORT_PIXEL_U64(
        DMN_GetCompositionPixelHitTestPublishedGeneration,
        GetCompositionPixelHitTestPublishedGeneration)
    DMN_EXPORT_PIXEL_UINT(
        DMN_GetCompositionPixelHitTestLastWin32Error,
        GetCompositionPixelHitTestLastWin32Error)
    DMN_EXPORT_PIXEL_BOOL(
        DMN_DidCompositionPixelHitTestStyleRestoreSucceed,
        DidCompositionPixelHitTestStyleRestoreSucceed)
    DMN_EXPORT_PIXEL_BOOL(
        DMN_IsCompositionPixelHitTestRequestPending,
        IsCompositionPixelHitTestRequestPending)
    DMN_EXPORT_PIXEL_BOOL(
        DMN_DidCompositionPixelHitTestLastRequestSucceed,
        DidCompositionPixelHitTestLastRequestSucceed)
    DMN_EXPORT_PIXEL_U64(
        DMN_GetCompositionPixelHitTestInitialExtendedStyle,
        GetCompositionPixelHitTestInitialExtendedStyle)
    DMN_EXPORT_PIXEL_U64(
        DMN_GetCompositionPixelHitTestDiagnosticExtendedStyle,
        GetCompositionPixelHitTestDiagnosticExtendedStyle)
    DMN_EXPORT_PIXEL_U64(
        DMN_GetCompositionPixelHitTestCurrentExtendedStyle,
        GetCompositionPixelHitTestCurrentExtendedStyle)
    DMN_EXPORT_PIXEL_U64(
        DMN_GetCompositionPixelHitTestWindowClassStyle,
        GetCompositionPixelHitTestWindowClassStyle)
#undef DMN_EXPORT_PIXEL_INT
#undef DMN_EXPORT_PIXEL_BOOL
#undef DMN_EXPORT_PIXEL_UINT
#undef DMN_EXPORT_PIXEL_U64

    int UNITY_INTERFACE_EXPORT
        DMN_StartCompositionWindowRegionDiagnostics(int alphaThreshold)
    {
        return DesktopMascotNative::
            StartCompositionWindowRegionDiagnostics(alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_CompleteCompositionWindowRegionDiagnostics()
    {
        return DesktopMascotNative::
            CompleteCompositionWindowRegionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StopCompositionWindowRegionDiagnostics()
    {
        return DesktopMascotNative::
            StopCompositionWindowRegionDiagnostics();
    }

#define DMN_EXPORT_REGION_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_REGION_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_REGION_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_REGION_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_REGION_INT(
        DMN_GetCompositionWindowRegionState,
        GetCompositionWindowRegionState)
    DMN_EXPORT_REGION_INT(
        DMN_GetCompositionWindowRegionFailureStage,
        GetCompositionWindowRegionFailureStage)
    DMN_EXPORT_REGION_INT(
        DMN_GetCompositionWindowRegionThreshold,
        GetCompositionWindowRegionThreshold)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionApplied,
        IsCompositionWindowRegionApplied)
    DMN_EXPORT_REGION_INT(
        DMN_GetCompositionWindowRegionType,
        GetCompositionWindowRegionType)
    DMN_EXPORT_REGION_UINT(
        DMN_GetCompositionWindowRegionRectangleCount,
        GetCompositionWindowRegionRectangleCount)
    DMN_EXPORT_REGION_UINT(
        DMN_GetCompositionWindowRegionCoveredPixelCount,
        GetCompositionWindowRegionCoveredPixelCount)
    DMN_EXPORT_REGION_UINT(
        DMN_GetCompositionWindowRegionExcludedPixelCount,
        GetCompositionWindowRegionExcludedPixelCount)
    DMN_EXPORT_REGION_INT(
        DMN_GetCompositionWindowInitialRegionType,
        GetCompositionWindowInitialRegionType)
    DMN_EXPORT_REGION_BOOL(
        DMN_DidCompositionWindowInitialRegionRestoreSucceed,
        DidCompositionWindowInitialRegionRestoreSucceed)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionApplyRequestPending,
        IsCompositionWindowRegionApplyRequestPending)
    DMN_EXPORT_REGION_BOOL(
        DMN_DidCompositionWindowRegionLastApplySucceed,
        DidCompositionWindowRegionLastApplySucceed)
    DMN_EXPORT_REGION_UINT(
        DMN_GetCompositionWindowRegionLastWin32Error,
        GetCompositionWindowRegionLastWin32Error)
    DMN_EXPORT_REGION_U64(
        DMN_GetCompositionWindowRegionPublishedGeneration,
        GetCompositionWindowRegionPublishedGeneration)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionTopLeftInside,
        IsCompositionWindowRegionTopLeftInside)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionTopRightInside,
        IsCompositionWindowRegionTopRightInside)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionBottomLeftInside,
        IsCompositionWindowRegionBottomLeftInside)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionBottomRightInside,
        IsCompositionWindowRegionBottomRightInside)
    DMN_EXPORT_REGION_BOOL(
        DMN_IsCompositionWindowRegionCenterInside,
        IsCompositionWindowRegionCenterInside)
    DMN_EXPORT_REGION_U64(
        DMN_GetCompositionWindowRegionInitialExtendedStyle,
        GetCompositionWindowRegionInitialExtendedStyle)
    DMN_EXPORT_REGION_U64(
        DMN_GetCompositionWindowRegionDiagnosticExtendedStyle,
        GetCompositionWindowRegionDiagnosticExtendedStyle)
    DMN_EXPORT_REGION_U64(
        DMN_GetCompositionWindowRegionCurrentExtendedStyle,
        GetCompositionWindowRegionCurrentExtendedStyle)
    DMN_EXPORT_REGION_BOOL(
        DMN_DidCompositionWindowRegionStyleRestoreSucceed,
        DidCompositionWindowRegionStyleRestoreSucceed)
#undef DMN_EXPORT_REGION_INT
#undef DMN_EXPORT_REGION_BOOL
#undef DMN_EXPORT_REGION_UINT
#undef DMN_EXPORT_REGION_U64

    int UNITY_INTERFACE_EXPORT
        DMN_StartAnimatedWindowRegionDiagnostics(
            int alphaThreshold,
            int minimumUpdateIntervalMilliseconds)
    {
        return DesktopMascotNative::StartAnimatedWindowRegionDiagnostics(
            alphaThreshold,
            minimumUpdateIntervalMilliseconds);
    }

    int UNITY_INTERFACE_EXPORT DMN_PollAnimatedWindowRegionDiagnostics()
    {
        return DesktopMascotNative::PollAnimatedWindowRegionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_CompleteAnimatedWindowRegionDiagnostics()
    {
        return DesktopMascotNative::
            CompleteAnimatedWindowRegionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT DMN_StopAnimatedWindowRegionDiagnostics()
    {
        return DesktopMascotNative::StopAnimatedWindowRegionDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartProductionSizedAnimatedSilhouetteDiagnostics(
            int width, int height, int alphaThreshold, int targetFps,
            int targetPresentCount, int publishIntervalMilliseconds,
            int phaseDurationMilliseconds)
    {
        if (width != 256 || height != 256 || alphaThreshold != 128
            || targetFps != 30 || targetPresentCount != 1200
            || publishIntervalMilliseconds != 250
            || phaseDurationMilliseconds != 2000) return 0;
        return DesktopMascotNative::StartAnimatedWindowRegionDiagnostics(
            alphaThreshold, publishIntervalMilliseconds);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartRealMascotStaticAlphaDiagnostics(
            int width, int height, int alphaThreshold,
            int targetFps, int targetPresentCount)
    {
        if (width != 256 || height != 256 || alphaThreshold != 128
            || targetFps != 30 || targetPresentCount != 600)
            return 0;
        return DesktopMascotNative::StartRealMascotStaticAlphaDiagnostics(
            alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartRealMascotAnimatedAlphaDiagnostics(
            int width, int height, int alphaThreshold,
            int targetFps, int targetPresentCount,
            int publishIntervalMilliseconds)
    {
        if (width != 256 || height != 256 || alphaThreshold != 128
            || targetFps != 30 || targetPresentCount != 1200
            || publishIntervalMilliseconds != 250)
            return 0;
        return DesktopMascotNative::
            StartRealMascotAnimatedAlphaDiagnostics(
                alphaThreshold, publishIntervalMilliseconds);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_SetRealMascotAnimatedPhaseForGeneration(
            int phase, unsigned long long generation)
    {
        return DesktopMascotNative::
            SetRealMascotAnimatedPhaseForGeneration(phase, generation);
    }

    int UNITY_INTERFACE_EXPORT DMN_StartRuntimeAlphaRegion(
        int width,
        int height,
        int alphaThreshold,
        int publishIntervalMilliseconds)
    {
        if (width != 256 || height != 256
            || alphaThreshold != 128
            || publishIntervalMilliseconds < 250)
        {
            return 0;
        }
        return DesktopMascotNative::StartRuntimeAlphaRegion(
            alphaThreshold, publishIntervalMilliseconds);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_PollProductionSizedAnimatedSilhouetteDiagnostics()
    { return DesktopMascotNative::PollAnimatedWindowRegionDiagnostics(); }
    int UNITY_INTERFACE_EXPORT
        DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics()
    { return DesktopMascotNative::CompleteAnimatedWindowRegionDiagnostics(); }
    int UNITY_INTERFACE_EXPORT
        DMN_StopProductionSizedAnimatedSilhouetteDiagnostics()
    { return DesktopMascotNative::StopAnimatedWindowRegionDiagnostics(); }

#define DMN_EXPORT_ANIMATED_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_ANIMATED_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_ANIMATED_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_ANIMATED_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionState,
        GetAnimatedWindowRegionState)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionFailureStage,
        GetAnimatedWindowRegionFailureStage)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionEnabled,
        IsAnimatedWindowRegionEnabled)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionThreshold,
        GetAnimatedWindowRegionThreshold)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionMinimumUpdateIntervalMilliseconds,
        GetAnimatedWindowRegionMinimumUpdateIntervalMilliseconds)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionPublishedGeneration,
        GetAnimatedWindowRegionPublishedGeneration)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionBuildGeneration,
        GetAnimatedWindowRegionBuildGeneration)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionRequestedGeneration,
        GetAnimatedWindowRegionRequestedGeneration)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionAppliedGeneration,
        GetAnimatedWindowRegionAppliedGeneration)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionBuildCount,
        GetAnimatedWindowRegionBuildCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionGenerationEvaluationCount,
        GetAnimatedWindowRegionGenerationEvaluationCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionApplyMessagePostCount,
        GetAnimatedWindowRegionApplyMessagePostCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionApplyExecutionCount,
        GetAnimatedWindowRegionApplyExecutionCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionDuplicateMaskSkipCount,
        GetAnimatedWindowRegionDuplicateMaskSkipCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionSupersededGenerationCount,
        GetAnimatedWindowRegionSupersededGenerationCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionApplyRequestCount,
        GetAnimatedWindowRegionApplyRequestCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionApplySuccessCount,
        GetAnimatedWindowRegionApplySuccessCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionApplyFailureCount,
        GetAnimatedWindowRegionApplyFailureCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionSkippedGenerationCount,
        GetAnimatedWindowRegionSkippedGenerationCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionDuplicateSkipCount,
        GetAnimatedWindowRegionDuplicateSkipCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionCurrentRectangleCount,
        GetAnimatedWindowRegionCurrentRectangleCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionCurrentCoveredPixelCount,
        GetAnimatedWindowRegionCurrentCoveredPixelCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionCurrentExcludedPixelCount,
        GetAnimatedWindowRegionCurrentExcludedPixelCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionInitialGdiObjectCount,
        GetAnimatedWindowRegionInitialGdiObjectCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionPeakGdiObjectCount,
        GetAnimatedWindowRegionPeakGdiObjectCount)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionFinalGdiObjectCount,
        GetAnimatedWindowRegionFinalGdiObjectCount)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionGdiObjectDelta,
        GetAnimatedWindowRegionGdiObjectDelta)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionApplyRequestPending,
        IsAnimatedWindowRegionApplyRequestPending)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_DidAnimatedWindowRegionLastApplySucceed,
        DidAnimatedWindowRegionLastApplySucceed)
    DMN_EXPORT_ANIMATED_UINT(
        DMN_GetAnimatedWindowRegionLastWin32Error,
        GetAnimatedWindowRegionLastWin32Error)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_DidAnimatedWindowRegionInitialRegionRestoreSucceed,
        DidAnimatedWindowRegionInitialRegionRestoreSucceed)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_DidAnimatedWindowRegionInitialStyleRestoreSucceed,
        DidAnimatedWindowRegionInitialStyleRestoreSucceed)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionInitialRegionState,
        GetAnimatedWindowRegionInitialRegionState)
    DMN_EXPORT_ANIMATED_INT(
        DMN_GetAnimatedWindowRegionCurrentPhase,
        GetAnimatedWindowRegionCurrentPhase)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionTopLeftInside,
        IsAnimatedWindowRegionTopLeftInside)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionTopRightInside,
        IsAnimatedWindowRegionTopRightInside)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionBottomRightInside,
        IsAnimatedWindowRegionBottomRightInside)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionBottomLeftInside,
        IsAnimatedWindowRegionBottomLeftInside)
    DMN_EXPORT_ANIMATED_BOOL(
        DMN_IsAnimatedWindowRegionCenterInside,
        IsAnimatedWindowRegionCenterInside)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionInitialExtendedStyle,
        GetAnimatedWindowRegionInitialExtendedStyle)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionDiagnosticExtendedStyle,
        GetAnimatedWindowRegionDiagnosticExtendedStyle)
    DMN_EXPORT_ANIMATED_U64(
        DMN_GetAnimatedWindowRegionCurrentExtendedStyle,
        GetAnimatedWindowRegionCurrentExtendedStyle)

#define DMN_PROD_ANIM_INT(suffix, nativeName) \
    DMN_EXPORT_ANIMATED_INT(DMN_GetProductionSizedAnimatedSilhouette##suffix, nativeName)
#define DMN_PROD_ANIM_UINT(suffix, nativeName) \
    DMN_EXPORT_ANIMATED_UINT(DMN_GetProductionSizedAnimatedSilhouette##suffix, nativeName)
#define DMN_PROD_ANIM_U64(suffix, nativeName) \
    DMN_EXPORT_ANIMATED_U64(DMN_GetProductionSizedAnimatedSilhouette##suffix, nativeName)
    DMN_PROD_ANIM_INT(State, GetAnimatedWindowRegionState)
    DMN_PROD_ANIM_INT(FailureStage, GetAnimatedWindowRegionFailureStage)
    DMN_PROD_ANIM_INT(CurrentPhase, GetAnimatedWindowRegionCurrentPhase)
    DMN_PROD_ANIM_INT(PublishedPhase, GetAnimatedWindowRegionPublishedPhase)
    DMN_PROD_ANIM_INT(BuiltPhase, GetAnimatedWindowRegionBuiltPhase)
    DMN_PROD_ANIM_INT(AppliedPhase, GetAnimatedWindowRegionCurrentPhase)
    DMN_PROD_ANIM_U64(PublishedGeneration, GetAnimatedWindowRegionPublishedGeneration)
    DMN_PROD_ANIM_U64(AppliedGeneration, GetAnimatedWindowRegionAppliedGeneration)
    DMN_PROD_ANIM_UINT(GenerationEvaluationCount, GetAnimatedWindowRegionGenerationEvaluationCount)
    DMN_PROD_ANIM_UINT(RegionBuildCount, GetAnimatedWindowRegionBuildCount)
    DMN_PROD_ANIM_UINT(ApplyMessagePostCount, GetAnimatedWindowRegionApplyMessagePostCount)
    DMN_PROD_ANIM_UINT(ApplyExecutionCount, GetAnimatedWindowRegionApplyExecutionCount)
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount()
    {
        return DesktopMascotNative::GetAnimatedWindowRegionApplySuccessCount()
            + DesktopMascotNative::GetAnimatedWindowRegionDuplicateMaskSkipCount();
    }
    DMN_PROD_ANIM_UINT(ApplyFailureCount, GetAnimatedWindowRegionApplyFailureCount)
    DMN_PROD_ANIM_UINT(DuplicateMaskSkipCount, GetAnimatedWindowRegionDuplicateMaskSkipCount)
    DMN_PROD_ANIM_UINT(SupersededGenerationCount, GetAnimatedWindowRegionSupersededGenerationCount)
    DMN_PROD_ANIM_UINT(MaximumPendingApplyMessageCount, GetAnimatedWindowRegionMaximumPendingApplyMessageCount)
    DMN_PROD_ANIM_UINT(MaximumPendingRegionCount, GetAnimatedWindowRegionMaximumPendingRegionCount)
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePendingHrgnReplacementCount()
    { return 0; }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount()
    { return 0; }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount()
    { return 0; }
    DMN_PROD_ANIM_UINT(PhaseTransitionCount, GetAnimatedWindowRegionPhaseTransitionCount)
    DMN_PROD_ANIM_UINT(SuccessfulPhaseApplyCount, GetAnimatedWindowRegionSuccessfulPhaseApplyCount)
    DMN_PROD_ANIM_UINT(RawScanlineRunCount, GetAnimatedWindowRegionCurrentRectangleCount)
    DMN_PROD_ANIM_UINT(MergedRectangleCount, GetAnimatedWindowRegionMergedRectangleCount)
    DMN_PROD_ANIM_UINT(RegionDataRectangleCount, GetAnimatedWindowRegionRegionDataRectangleCount)
    DMN_PROD_ANIM_UINT(CoveredPixelCount, GetAnimatedWindowRegionCurrentCoveredPixelCount)
    DMN_PROD_ANIM_UINT(ExcludedPixelCount, GetAnimatedWindowRegionCurrentExcludedPixelCount)
    DMN_PROD_ANIM_U64(LastBuildMicroseconds, GetAnimatedWindowRegionLastBuildMicroseconds)
    DMN_PROD_ANIM_U64(MinimumBuildMicroseconds, GetAnimatedWindowRegionMinimumBuildMicroseconds)
    DMN_PROD_ANIM_U64(MaximumBuildMicroseconds, GetAnimatedWindowRegionMaximumBuildMicroseconds)
    DMN_PROD_ANIM_U64(AverageBuildMicroseconds, GetAnimatedWindowRegionAverageBuildMicroseconds)
    DMN_PROD_ANIM_U64(LastSetWindowRgnMicroseconds, GetAnimatedWindowRegionLastSetWindowRgnMicroseconds)
    DMN_PROD_ANIM_U64(MinimumSetWindowRgnMicroseconds, GetAnimatedWindowRegionMinimumSetWindowRgnMicroseconds)
    DMN_PROD_ANIM_U64(MaximumSetWindowRgnMicroseconds, GetAnimatedWindowRegionMaximumSetWindowRgnMicroseconds)
    DMN_PROD_ANIM_U64(AverageSetWindowRgnMicroseconds, GetAnimatedWindowRegionAverageSetWindowRgnMicroseconds)
    DMN_PROD_ANIM_UINT(HrgnCreatedCount, GetAnimatedWindowRegionHrgnCreatedCount)
    DMN_PROD_ANIM_UINT(HrgnCallerDeletedCount, GetAnimatedWindowRegionHrgnCallerDeletedCount)
    DMN_PROD_ANIM_UINT(HrgnOwnershipTransferredCount, GetAnimatedWindowRegionHrgnOwnershipTransferredCount)
    DMN_PROD_ANIM_UINT(HrgnValidationCopyDeletedCount, GetAnimatedWindowRegionHrgnValidationCopyDeletedCount)
    DMN_PROD_ANIM_INT(HrgnLiveOwnedCount, GetAnimatedWindowRegionHrgnLiveOwnedCount)
    DMN_PROD_ANIM_UINT(InitialGdiObjectCount, GetAnimatedWindowRegionInitialGdiObjectCount)
    DMN_PROD_ANIM_UINT(MinimumAnimationGdiObjectCount, GetAnimatedWindowRegionMinimumAnimationGdiObjectCount)
    DMN_PROD_ANIM_UINT(MaximumAnimationGdiObjectCount, GetAnimatedWindowRegionPeakGdiObjectCount)
    DMN_PROD_ANIM_UINT(LastAnimationGdiObjectCount, GetAnimatedWindowRegionLastAnimationGdiObjectCount)
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouettePhaseApply(int phase)
    { return DesktopMascotNative::WasAnimatedWindowRegionPhaseApplied(phase) ? 1 : 0; }
    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseLastAppliedGeneration(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseLastAppliedGeneration(phase); }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseRawRuns(phase); }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseMerged(phase); }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseFinal(phase); }
    unsigned int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseCovered(phase); }
    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedAnimatedSilhouettePhaseHash(int phase)
    { return DesktopMascotNative::GetAnimatedWindowRegionPhaseHash(phase); }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()
    { return DesktopMascotNative::DidAnimatedWindowRegionInitialRegionRestoreSucceed() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()
    { return DesktopMascotNative::DidAnimatedWindowRegionInitialStyleRestoreSucceed() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed()
    { return DesktopMascotNative::DidAnimatedWindowRegionCounterInvariantsSucceed() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteRepresentativeValidationSucceed()
    { return DesktopMascotNative::DidAnimatedWindowRegionRepresentativeValidationSucceed() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteAllPhasesApply()
    { return DesktopMascotNative::DidAnimatedWindowRegionAllPhasesApply() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouettePhaseComplexityDiffer()
    { return DesktopMascotNative::DidAnimatedWindowRegionPhaseComplexityDiffer() ? 1 : 0; }
    int UNITY_INTERFACE_EXPORT
        DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()
    { return DesktopMascotNative::DidAnimatedWindowRegionHrgnOwnershipInvariantsSucceed() ? 1 : 0; }
#undef DMN_PROD_ANIM_INT
#undef DMN_PROD_ANIM_UINT
#undef DMN_PROD_ANIM_U64
#undef DMN_EXPORT_ANIMATED_INT
#undef DMN_EXPORT_ANIMATED_BOOL
#undef DMN_EXPORT_ANIMATED_UINT
#undef DMN_EXPORT_ANIMATED_U64

    int UNITY_INTERFACE_EXPORT
        DMN_StartStaticComplexSilhouetteDiagnostics(int alphaThreshold)
    {
        return DesktopMascotNative::
            StartStaticComplexSilhouetteDiagnostics(alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_CompleteStaticComplexSilhouetteDiagnostics()
    {
        return DesktopMascotNative::
            CompleteStaticComplexSilhouetteDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StopStaticComplexSilhouetteDiagnostics()
    {
        return DesktopMascotNative::
            StopStaticComplexSilhouetteDiagnostics();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_StartProductionSizedStaticSilhouetteDiagnostics(
            int width,
            int height,
            int alphaThreshold,
            int targetFps,
            int targetPresentCount)
    {
        if (width != 256 || height != 256 || alphaThreshold != 128
            || targetFps != 30 || targetPresentCount != 1200)
        {
            return 0;
        }
        g_productionRequestedFps.store(targetFps);
        g_productionEffectiveFps.store(targetFps);
        g_productionTargetPresentCount.store(targetPresentCount);
        return DesktopMascotNative::
            StartStaticComplexSilhouetteDiagnostics(alphaThreshold);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedStaticSilhouetteWidth() { return 256; }
    int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedStaticSilhouetteHeight() { return 256; }
    int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedStaticSilhouetteRequestedTargetFps()
    {
        return g_productionRequestedFps.load();
    }
    int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedStaticSilhouetteEffectiveTargetFps()
    {
        return g_productionEffectiveFps.load();
    }
    int UNITY_INTERFACE_EXPORT
        DMN_GetProductionSizedStaticSilhouetteTargetPresentCount()
    {
        return g_productionTargetPresentCount.load();
    }

#define DMN_EXPORT_STATIC_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_STATIC_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName() ? 1 : 0; \
    }
#define DMN_EXPORT_STATIC_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
#define DMN_EXPORT_STATIC_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { \
        return DesktopMascotNative::nativeName(); \
    }
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouetteState, GetStaticComplexSilhouetteState)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouetteFailureStage, GetStaticComplexSilhouetteFailureStage)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouetteThreshold, GetStaticComplexSilhouetteThreshold)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouettePublishedGeneration, GetStaticComplexSilhouettePublishedGeneration)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteAppliedGeneration, GetStaticComplexSilhouetteAppliedGeneration)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteGenerationEvaluationCount, GetStaticComplexSilhouetteGenerationEvaluationCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteRegionBuildCount, GetStaticComplexSilhouetteRegionBuildCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteApplyMessagePostCount, GetStaticComplexSilhouetteApplyMessagePostCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteApplyExecutionCount, GetStaticComplexSilhouetteApplyExecutionCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteApplySuccessCount, GetStaticComplexSilhouetteApplySuccessCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteApplyFailureCount, GetStaticComplexSilhouetteApplyFailureCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteDuplicateMaskSkipCount, GetStaticComplexSilhouetteDuplicateMaskSkipCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteSupersededGenerationCount, GetStaticComplexSilhouetteSupersededGenerationCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteRawScanlineRunCount, GetStaticComplexSilhouetteRawScanlineRunCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteMergedRectangleCount, GetStaticComplexSilhouetteMergedRectangleCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteRegionDataRectangleCount, GetStaticComplexSilhouetteRegionDataRectangleCount)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouetteRegionType, GetStaticComplexSilhouetteRegionType)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteCoveredPixelCount, GetStaticComplexSilhouetteCoveredPixelCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteExcludedPixelCount, GetStaticComplexSilhouetteExcludedPixelCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteRegionDataSizeBytes, GetStaticComplexSilhouetteRegionDataSizeBytes)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteLastBuildMicroseconds, GetStaticComplexSilhouetteLastBuildMicroseconds)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteMinimumBuildMicroseconds, GetStaticComplexSilhouetteMinimumBuildMicroseconds)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteMaximumBuildMicroseconds, GetStaticComplexSilhouetteMaximumBuildMicroseconds)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteAverageBuildMicroseconds, GetStaticComplexSilhouetteAverageBuildMicroseconds)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds, GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds)
    DMN_EXPORT_STATIC_U64(DMN_GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds, GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteInitialGdiObjectCount, GetStaticComplexSilhouetteInitialGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouettePreShutdownGdiObjectCount, GetStaticComplexSilhouettePreShutdownGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouettePostShutdownGdiObjectCount, GetStaticComplexSilhouettePostShutdownGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterRegionBuildGdiObjectCount, GetStaticComplexSilhouetteAfterRegionBuildGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterRegionApplyGdiObjectCount, GetStaticComplexSilhouetteAfterRegionApplyGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterRegionRestoreGdiObjectCount, GetStaticComplexSilhouetteAfterRegionRestoreGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterWindowDestroyGdiObjectCount, GetStaticComplexSilhouetteAfterWindowDestroyGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterUiThreadJoinGdiObjectCount, GetStaticComplexSilhouetteAfterUiThreadJoinGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteAfterNativeCleanupGdiObjectCount, GetStaticComplexSilhouetteAfterNativeCleanupGdiObjectCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteHrgnCreatedCount, GetStaticComplexSilhouetteHrgnCreatedCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteHrgnCallerDeletedCount, GetStaticComplexSilhouetteHrgnCallerDeletedCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteHrgnOwnershipTransferredCount, GetStaticComplexSilhouetteHrgnOwnershipTransferredCount)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteHrgnRestoreCreatedCount, GetStaticComplexSilhouetteHrgnRestoreCreatedCount)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouetteHrgnLiveOwnedCount, GetStaticComplexSilhouetteHrgnLiveOwnedCount)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouettePreShutdownGdiDelta, GetStaticComplexSilhouettePreShutdownGdiDelta)
    DMN_EXPORT_STATIC_INT(DMN_GetStaticComplexSilhouettePostShutdownGdiDelta, GetStaticComplexSilhouettePostShutdownGdiDelta)
    DMN_EXPORT_STATIC_BOOL(DMN_DidStaticComplexSilhouetteCounterInvariantsSucceed, DidStaticComplexSilhouetteCounterInvariantsSucceed)
    DMN_EXPORT_STATIC_BOOL(DMN_DidStaticComplexSilhouetteRepresentativeValidationSucceed, DidStaticComplexSilhouetteRepresentativeValidationSucceed)
    DMN_EXPORT_STATIC_BOOL(DMN_DidStaticComplexSilhouetteInitialRegionRestoreSucceed, DidStaticComplexSilhouetteInitialRegionRestoreSucceed)
    DMN_EXPORT_STATIC_BOOL(DMN_DidStaticComplexSilhouetteInitialStyleRestoreSucceed, DidStaticComplexSilhouetteInitialStyleRestoreSucceed)
    DMN_EXPORT_STATIC_BOOL(DMN_DidStaticComplexSilhouetteLastApplySucceed, DidStaticComplexSilhouetteLastApplySucceed)
    DMN_EXPORT_STATIC_UINT(DMN_GetStaticComplexSilhouetteLastWin32Error, GetStaticComplexSilhouetteLastWin32Error)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteBodyCenterInside, IsStaticComplexSilhouetteBodyCenterInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteLongLeftEarInside, IsStaticComplexSilhouetteLongLeftEarInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteMirroredEarInside, IsStaticComplexSilhouetteMirroredEarInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteBottomRightTailInside, IsStaticComplexSilhouetteBottomRightTailInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteMirroredTailInside, IsStaticComplexSilhouetteMirroredTailInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteTransparentCornerInside, IsStaticComplexSilhouetteTransparentCornerInside)
    DMN_EXPORT_STATIC_BOOL(DMN_IsStaticComplexSilhouetteTransparentBottomLeftCornerInside, IsStaticComplexSilhouetteTransparentBottomLeftCornerInside)

#define DMN_PRODUCTION_ALIAS_INT(suffix, nativeName) \
    DMN_EXPORT_STATIC_INT( \
        DMN_GetProductionSizedStaticSilhouette##suffix, nativeName)
#define DMN_PRODUCTION_ALIAS_UINT(suffix, nativeName) \
    DMN_EXPORT_STATIC_UINT( \
        DMN_GetProductionSizedStaticSilhouette##suffix, nativeName)
#define DMN_PRODUCTION_ALIAS_U64(suffix, nativeName) \
    DMN_EXPORT_STATIC_U64( \
        DMN_GetProductionSizedStaticSilhouette##suffix, nativeName)
#define DMN_PRODUCTION_ALIAS_BOOL(suffix, nativeName) \
    DMN_EXPORT_STATIC_BOOL( \
        DMN_DidProductionSizedStaticSilhouette##suffix, nativeName)
    DMN_PRODUCTION_ALIAS_INT(State, GetStaticComplexSilhouetteState)
    DMN_PRODUCTION_ALIAS_INT(FailureStage, GetStaticComplexSilhouetteFailureStage)
    DMN_PRODUCTION_ALIAS_INT(Threshold, GetStaticComplexSilhouetteThreshold)
    DMN_PRODUCTION_ALIAS_U64(PublishedGeneration, GetStaticComplexSilhouettePublishedGeneration)
    DMN_PRODUCTION_ALIAS_U64(AppliedGeneration, GetStaticComplexSilhouetteAppliedGeneration)
    DMN_PRODUCTION_ALIAS_UINT(GenerationEvaluationCount, GetStaticComplexSilhouetteGenerationEvaluationCount)
    DMN_PRODUCTION_ALIAS_UINT(RegionBuildCount, GetStaticComplexSilhouetteRegionBuildCount)
    DMN_PRODUCTION_ALIAS_UINT(ApplyMessagePostCount, GetStaticComplexSilhouetteApplyMessagePostCount)
    DMN_PRODUCTION_ALIAS_UINT(ApplyExecutionCount, GetStaticComplexSilhouetteApplyExecutionCount)
    DMN_PRODUCTION_ALIAS_UINT(ApplySuccessCount, GetStaticComplexSilhouetteApplySuccessCount)
    DMN_PRODUCTION_ALIAS_UINT(ApplyFailureCount, GetStaticComplexSilhouetteApplyFailureCount)
    DMN_PRODUCTION_ALIAS_UINT(RawScanlineRunCount, GetStaticComplexSilhouetteRawScanlineRunCount)
    DMN_PRODUCTION_ALIAS_UINT(MergedRectangleCount, GetStaticComplexSilhouetteMergedRectangleCount)
    DMN_PRODUCTION_ALIAS_UINT(RegionDataRectangleCount, GetStaticComplexSilhouetteRegionDataRectangleCount)
    DMN_PRODUCTION_ALIAS_UINT(RegionDataSizeBytes, GetStaticComplexSilhouetteRegionDataSizeBytes)
    DMN_PRODUCTION_ALIAS_UINT(CoveredPixelCount, GetStaticComplexSilhouetteCoveredPixelCount)
    DMN_PRODUCTION_ALIAS_UINT(ExcludedPixelCount, GetStaticComplexSilhouetteExcludedPixelCount)
    DMN_PRODUCTION_ALIAS_U64(TotalBuildMicroseconds, GetStaticComplexSilhouetteLastBuildMicroseconds)
    DMN_PRODUCTION_ALIAS_U64(SetWindowRgnMicroseconds, GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds)
    DMN_PRODUCTION_ALIAS_UINT(InitialGdiObjectCount, GetStaticComplexSilhouetteInitialGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterRegionBuildGdiObjectCount, GetStaticComplexSilhouetteAfterRegionBuildGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterRegionApplyGdiObjectCount, GetStaticComplexSilhouetteAfterRegionApplyGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterRegionRestoreGdiObjectCount, GetStaticComplexSilhouetteAfterRegionRestoreGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterWindowDestroyGdiObjectCount, GetStaticComplexSilhouetteAfterWindowDestroyGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterUiThreadJoinGdiObjectCount, GetStaticComplexSilhouetteAfterUiThreadJoinGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(AfterNativeCleanupGdiObjectCount, GetStaticComplexSilhouetteAfterNativeCleanupGdiObjectCount)
    DMN_PRODUCTION_ALIAS_UINT(HrgnCreatedCount, GetStaticComplexSilhouetteHrgnCreatedCount)
    DMN_PRODUCTION_ALIAS_UINT(HrgnCallerDeletedCount, GetStaticComplexSilhouetteHrgnCallerDeletedCount)
    DMN_PRODUCTION_ALIAS_UINT(HrgnOwnershipTransferredCount, GetStaticComplexSilhouetteHrgnOwnershipTransferredCount)
    DMN_PRODUCTION_ALIAS_UINT(HrgnRestoreCreatedCount, GetStaticComplexSilhouetteHrgnRestoreCreatedCount)
    DMN_PRODUCTION_ALIAS_INT(HrgnLiveOwnedCount, GetStaticComplexSilhouetteHrgnLiveOwnedCount)
    DMN_PRODUCTION_ALIAS_BOOL(CounterInvariantsSucceed, DidStaticComplexSilhouetteCounterInvariantsSucceed)
    DMN_PRODUCTION_ALIAS_BOOL(RepresentativeValidationSucceed, DidStaticComplexSilhouetteRepresentativeValidationSucceed)
#undef DMN_PRODUCTION_ALIAS_INT
#undef DMN_PRODUCTION_ALIAS_UINT
#undef DMN_PRODUCTION_ALIAS_U64
#undef DMN_PRODUCTION_ALIAS_BOOL
#undef DMN_EXPORT_STATIC_INT
#undef DMN_EXPORT_STATIC_BOOL
#undef DMN_EXPORT_STATIC_UINT
#undef DMN_EXPORT_STATIC_U64

    int UNITY_INTERFACE_EXPORT DMN_EnableNativeMascotWindowDrag()
    {
        return DesktopMascotNative::EnableNativeMascotWindowDrag();
    }

    int UNITY_INTERFACE_EXPORT DMN_SetCompositionInitialPosition(
        int x,
        int y)
    {
        return DesktopMascotNative::ConfigureCompositionInitialPosition(x, y)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_DisableNativeMascotWindowDrag()
    {
        return DesktopMascotNative::DisableNativeMascotWindowDrag();
    }

    int UNITY_INTERFACE_EXPORT DMN_StartNativeMascotWindowDragDiagnostic(
        int deltaX,
        int deltaY)
    {
        return DesktopMascotNative::
            StartNativeMascotWindowDragDiagnostic(deltaX, deltaY);
    }

    int UNITY_INTERFACE_EXPORT
        DMN_CompleteNativeMascotWindowDragDiagnostic()
    {
        return DesktopMascotNative::
            CompleteNativeMascotWindowDragDiagnostic();
    }

#define DMN_EXPORT_DRAG_INT(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
#define DMN_EXPORT_DRAG_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
#define DMN_EXPORT_DRAG_U64(exportName, nativeName) \
    unsigned long long UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
#define DMN_EXPORT_DRAG_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName() ? 1 : 0; }
    DMN_EXPORT_DRAG_BOOL(
        DMN_IsNativeMascotWindowDragEnabled,
        IsNativeMascotWindowDragEnabled)
    DMN_EXPORT_DRAG_BOOL(
        DMN_IsNativeMascotWindowDragging,
        IsNativeMascotWindowDragging)
    DMN_EXPORT_DRAG_BOOL(
        DMN_IsNativeMascotWindowCaptureOwned,
        IsNativeMascotWindowCaptureOwned)
    DMN_EXPORT_DRAG_BOOL(
        DMN_IsNativeMascotWindowAvailableForDrag,
        IsNativeMascotWindowAvailableForDrag)
    DMN_EXPORT_DRAG_INT(
        DMN_GetNativeMascotDragDiagnosticState,
        GetNativeMascotDragDiagnosticState)
    DMN_EXPORT_DRAG_INT(
        DMN_GetNativeMascotDragFailureStage,
        GetNativeMascotDragFailureStage)
    DMN_EXPORT_DRAG_UINT(
        DMN_GetNativeMascotDragStartCount,
        GetNativeMascotDragStartCount)
    DMN_EXPORT_DRAG_UINT(
        DMN_GetNativeMascotDragMoveCount,
        GetNativeMascotDragMoveCount)
    DMN_EXPORT_DRAG_UINT(
        DMN_GetNativeMascotDragEndCount,
        GetNativeMascotDragEndCount)
    DMN_EXPORT_DRAG_UINT(
        DMN_GetNativeMascotDragCaptureAcquiredCount,
        GetNativeMascotDragCaptureAcquiredCount)
    DMN_EXPORT_DRAG_UINT(
        DMN_GetNativeMascotDragCaptureReleasedCount,
        GetNativeMascotDragCaptureReleasedCount)
    DMN_EXPORT_DRAG_U64(
        DMN_GetNativeMascotCompletedDragGeneration,
        GetNativeMascotCompletedDragGeneration)
    int UNITY_INTERFACE_EXPORT DMN_TryGetNativeMascotWindowPosition(
        int* x,
        int* y)
    {
        if (x == nullptr || y == nullptr)
        {
            return 0;
        }
        std::int32_t nativeX = 0;
        std::int32_t nativeY = 0;
        if (!DesktopMascotNative::TryGetNativeMascotWindowPosition(
                nativeX,
                nativeY))
        {
            return 0;
        }
        *x = nativeX;
        *y = nativeY;
        return 1;
    }
    DMN_EXPORT_DRAG_INT(
        DMN_GetNativeMascotDragLastWindowX,
        GetNativeMascotDragLastWindowX)
    DMN_EXPORT_DRAG_INT(
        DMN_GetNativeMascotDragLastWindowY,
        GetNativeMascotDragLastWindowY)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticMoveSucceed,
        DidNativeMascotDragDiagnosticMoveSucceed)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticSizeRemainUnchanged,
        DidNativeMascotDragDiagnosticSizeRemainUnchanged)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticRegionRemainApplied,
        DidNativeMascotDragDiagnosticRegionRemainApplied)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticPresentContinue,
        DidNativeMascotDragDiagnosticPresentContinue)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticRegionPublicationContinue,
        DidNativeMascotDragDiagnosticRegionPublicationContinue)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticAvoidCompositionRestart,
        DidNativeMascotDragDiagnosticAvoidCompositionRestart)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticPreserveZOrder,
        DidNativeMascotDragDiagnosticPreserveZOrder)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticAvoidActivation,
        DidNativeMascotDragDiagnosticAvoidActivation)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticReleaseCapture,
        DidNativeMascotDragDiagnosticReleaseCapture)
    DMN_EXPORT_DRAG_BOOL(
        DMN_DidNativeMascotDragDiagnosticRestoreInitialPosition,
        DidNativeMascotDragDiagnosticRestoreInitialPosition)
#undef DMN_EXPORT_DRAG_INT
#undef DMN_EXPORT_DRAG_UINT
#undef DMN_EXPORT_DRAG_U64
#undef DMN_EXPORT_DRAG_BOOL

    int UNITY_INTERFACE_EXPORT DMN_EnableNativeMascotContextMenu()
    {
        return DesktopMascotNative::EnableNativeMascotContextMenu();
    }

    int UNITY_INTERFACE_EXPORT DMN_DisableNativeMascotContextMenu()
    {
        return DesktopMascotNative::DisableNativeMascotContextMenu();
    }

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetNativeMascotCommandGeneration()
    {
        return DesktopMascotNative::GetNativeMascotCommandGeneration();
    }

    int UNITY_INTERFACE_EXPORT DMN_TryConsumeNativeMascotCommand(
        unsigned long long* generation,
        int* command)
    {
        if (generation == nullptr || command == nullptr)
            return 0;
        std::uint64_t nativeGeneration = 0;
        std::int32_t nativeCommand = 0;
        if (!DesktopMascotNative::TryConsumeNativeMascotCommand(
                nativeGeneration,
                nativeCommand))
        {
            return 0;
        }
        *generation = nativeGeneration;
        *command = nativeCommand;
        return 1;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_PublishNativeMascotCommandForDiagnostics(int command)
    {
        return DesktopMascotNative::
            PublishNativeMascotCommandForDiagnostics(command)
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_RunNativeMascotMenuResourceDiagnostic()
    {
        return DesktopMascotNative::RunNativeMascotMenuResourceDiagnostic()
            ? 1
            : 0;
    }

#define DMN_EXPORT_MENU_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotCommandPublishCount,
        GetNativeMascotCommandPublishCount)
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotCommandConsumeCount,
        GetNativeMascotCommandConsumeCount)
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotCommandRejectedCount,
        GetNativeMascotCommandRejectedCount)
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotMenuCreatedCount,
        GetNativeMascotMenuCreatedCount)
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotMenuDestroyedCount,
        GetNativeMascotMenuDestroyedCount)
    DMN_EXPORT_MENU_UINT(
        DMN_GetNativeMascotMenuCancelledCount,
        GetNativeMascotMenuCancelledCount)
#undef DMN_EXPORT_MENU_UINT

    int UNITY_INTERFACE_EXPORT DMN_GetNativeMascotMenuLiveOwnedCount()
    {
        return DesktopMascotNative::GetNativeMascotMenuLiveOwnedCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetNativeApplicationCommandLastSource()
    {
        return DesktopMascotNative::GetNativeApplicationCommandLastSource();
    }

    int UNITY_INTERFACE_EXPORT DMN_StartNativeTrayIcon()
    {
        return DesktopMascotNative::StartNativeTrayIcon();
    }

    int UNITY_INTERFACE_EXPORT DMN_StopNativeTrayIcon()
    {
        return DesktopMascotNative::StopNativeTrayIcon();
    }

#define DMN_EXPORT_TRAY_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName() ? 1 : 0; }
    DMN_EXPORT_TRAY_BOOL(
        DMN_IsNativeTrayIconRunning,
        IsNativeTrayIconRunning)
    DMN_EXPORT_TRAY_BOOL(
        DMN_IsNativeTrayOwnerWindowAvailable,
        IsNativeTrayOwnerWindowAvailable)
    DMN_EXPORT_TRAY_BOOL(
        DMN_IsNativeTrayIconRegistered,
        IsNativeTrayIconRegistered)
    DMN_EXPORT_TRAY_BOOL(
        DMN_IsNativeTrayPopupActive,
        IsNativeTrayPopupActive)
    DMN_EXPORT_TRAY_BOOL(
        DMN_WasNativeTrayTooltipConfigured,
        WasNativeTrayTooltipConfigured)
#undef DMN_EXPORT_TRAY_BOOL

#define DMN_EXPORT_TRAY_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayInitialAddRequestCount,
        GetNativeTrayInitialAddRequestCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTraySetVersionRequestCount,
        GetNativeTraySetVersionRequestCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayDeleteRequestCount,
        GetNativeTrayDeleteRequestCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayReregisterRequestCount,
        GetNativeTrayReregisterRequestCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayOwnerCreatedCount,
        GetNativeTrayOwnerCreatedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayOwnerDestroyedCount,
        GetNativeTrayOwnerDestroyedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayMenuCreatedCount,
        GetNativeTrayMenuCreatedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayMenuDestroyedCount,
        GetNativeTrayMenuDestroyedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayIconCreatedCount,
        GetNativeTrayIconCreatedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayIconDestroyedCount,
        GetNativeTrayIconDestroyedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayCancelledCount,
        GetNativeTrayCancelledCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayOpenSettingsSelectionCount,
        GetNativeTrayOpenSettingsSelectionCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayRequestExitSelectionCount,
        GetNativeTrayRequestExitSelectionCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayTaskbarCreatedCount,
        GetNativeTrayTaskbarCreatedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayShutdownRejectedCount,
        GetNativeTrayShutdownRejectedCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayNimAddAttemptCount,
        GetNativeTrayNimAddAttemptCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayNimAddSuccessCount,
        GetNativeTrayNimAddSuccessCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayNimAddLastError,
        GetNativeTrayNimAddLastError)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayNimSetVersionSuccessCount,
        GetNativeTrayNimSetVersionSuccessCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayNimSetVersionLastError,
        GetNativeTrayNimSetVersionLastError)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayRegistrationRetryCount,
        GetNativeTrayRegistrationRetryCount)
    DMN_EXPORT_TRAY_UINT(
        DMN_GetNativeTrayShutdownRetrySuppressedCount,
        GetNativeTrayShutdownRetrySuppressedCount)
#undef DMN_EXPORT_TRAY_UINT

    int UNITY_INTERFACE_EXPORT
        DMN_GetNativeTrayRegistrationFinalResult()
    {
        return DesktopMascotNative::
            GetNativeTrayRegistrationFinalResult()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_WasNativeTrayRegistrationRetryExhausted()
    {
        return DesktopMascotNative::
            WasNativeTrayRegistrationRetryExhausted()
            ? 1
            : 0;
    }

    int UNITY_INTERFACE_EXPORT DMN_GetNativeTrayMenuLiveOwnedCount()
    {
        return DesktopMascotNative::GetNativeTrayMenuLiveOwnedCount();
    }

    int UNITY_INTERFACE_EXPORT DMN_GetNativeTrayIconLiveOwnedCount()
    {
        return DesktopMascotNative::GetNativeTrayIconLiveOwnedCount();
    }

    int UNITY_INTERFACE_EXPORT
        DMN_PublishNativeTrayCommandForDiagnostics(int command)
    {
        return DesktopMascotNative::
            PublishNativeTrayCommandForDiagnostics(command);
    }

    int UNITY_INTERFACE_EXPORT DMN_RunNativeTrayFocusedDiagnostic()
    {
        return DesktopMascotNative::RunNativeTrayFocusedDiagnostic();
    }

    int UNITY_INTERFACE_EXPORT DMN_InitializeSingleInstance()
    {
        return DesktopMascotNative::InitializeSingleInstance();
    }

    int UNITY_INTERFACE_EXPORT DMN_BeginSingleInstanceShutdown()
    {
        return DesktopMascotNative::BeginSingleInstanceShutdown();
    }

    int UNITY_INTERFACE_EXPORT DMN_ShutdownSingleInstance()
    {
        return DesktopMascotNative::ShutdownSingleInstance();
    }

    int UNITY_INTERFACE_EXPORT DMN_TryConsumeSingleInstanceActivation(
        unsigned long long* generation)
    {
        if (generation == nullptr)
            return 0;
        std::uint64_t nativeGeneration = 0;
        if (!DesktopMascotNative::TryConsumeSingleInstanceActivation(
                nativeGeneration))
        {
            return 0;
        }
        *generation = nativeGeneration;
        return 1;
    }

    int UNITY_INTERFACE_EXPORT
        DMN_RunSingleInstanceCoalescingFocusedDiagnostic()
    {
        return DesktopMascotNative::
            RunSingleInstanceCoalescingFocusedDiagnostic();
    }

#define DMN_EXPORT_SINGLE_BOOL(exportName, nativeName) \
    int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName() ? 1 : 0; }
    DMN_EXPORT_SINGLE_BOOL(
        DMN_IsSingleInstancePrimary,
        IsSingleInstancePrimary)
    DMN_EXPORT_SINGLE_BOOL(
        DMN_IsSingleInstanceNotificationReady,
        IsSingleInstanceNotificationReady)
    DMN_EXPORT_SINGLE_BOOL(
        DMN_IsSingleInstanceAcceptingActivations,
        IsSingleInstanceAcceptingActivations)
    DMN_EXPORT_SINGLE_BOOL(
        DMN_IsSingleInstanceActivationPending,
        IsSingleInstanceActivationPending)
#undef DMN_EXPORT_SINGLE_BOOL

    unsigned long long UNITY_INTERFACE_EXPORT
        DMN_GetSingleInstanceActivationGeneration()
    {
        return DesktopMascotNative::
            GetSingleInstanceActivationGeneration();
    }

#define DMN_EXPORT_SINGLE_UINT(exportName, nativeName) \
    unsigned int UNITY_INTERFACE_EXPORT exportName() \
    { return DesktopMascotNative::nativeName(); }
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceSignalReceivedCount,
        GetSingleInstanceSignalReceivedCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceGenerationPublishCount,
        GetSingleInstanceGenerationPublishCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceActivationConsumeCount,
        GetSingleInstanceActivationConsumeCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceActivationCoalescedCount,
        GetSingleInstanceActivationCoalescedCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceActivationRejectedCount,
        GetSingleInstanceActivationRejectedCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceMaximumPendingCount,
        GetSingleInstanceMaximumPendingCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceNotificationCreatedCount,
        GetSingleInstanceNotificationCreatedCount)
    DMN_EXPORT_SINGLE_UINT(
        DMN_GetSingleInstanceNotificationDestroyedCount,
        GetSingleInstanceNotificationDestroyedCount)
#undef DMN_EXPORT_SINGLE_UINT

    int UNITY_INTERFACE_EXPORT DMN_GetSingleInstanceFailureStage()
    {
        return DesktopMascotNative::GetSingleInstanceFailureStage();
    }
}
