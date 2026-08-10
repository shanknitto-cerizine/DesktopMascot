#pragma once

#include <cstdint>

struct ID3D12CommandQueue;
struct ID3D12Device;
struct IDXGIFactory2;
struct IDCompositionDesktopDevice;
struct IUnityGraphicsD3D12v8;

namespace DesktopMascotNative
{
    // Keep this range separate from the mascot window's drag, region,
    // context-menu, and tray messages. These messages are dispatched by the
    // shared mascot UI-thread window procedure.
    constexpr std::uint32_t kSpeechInitializeMessage = 0x8000u + 0x370u;
    constexpr std::uint32_t kSpeechShowMessage = 0x8000u + 0x371u;
    constexpr std::uint32_t kSpeechHideMessage = 0x8000u + 0x372u;
    constexpr std::uint32_t kSpeechPresentMessage = 0x8000u + 0x373u;
    constexpr std::uint32_t kSpeechShutdownMessage = 0x8000u + 0x374u;
    constexpr int kSpeechRenderEvent = 9;
    constexpr std::uint32_t kSpeechWidth = 170;
    constexpr std::uint32_t kSpeechHeight = 64;

    void ResetSpeechPresentation();
    void SetSpeechPresentationUiContext(
        void* mascotWindow,
        IDXGIFactory2* factory,
        IDCompositionDesktopDevice* compositionDevice,
        ID3D12CommandQueue* commandQueue);
    bool HandleSpeechPresentationOwnerMessage(
        std::uint32_t message,
        std::uintptr_t wParam,
        std::intptr_t lParam,
        std::intptr_t& result);
    void HandleSpeechPresentationFrameEvent(
        IUnityGraphicsD3D12v8* d3d12,
        ID3D12Device* device,
        void* sourceTexture);
    bool RequestSpeechPresentationInitialize();
    bool RequestSpeechPresentationShow(
        std::uint32_t generation,
        std::int32_t anchorX,
        std::int32_t anchorY);
    bool RequestSpeechPresentationHide(std::uint32_t generation);
    bool UpdateSpeechPresentationAnchor(
        std::int32_t anchorX,
        std::int32_t anchorY);
    void PollSpeechPresentation();
    std::uint32_t ConsumeSpeechClickGeneration();
    bool BeginSpeechPresentationShutdown();
    bool ShutdownSpeechPresentationOnUiThread(
        std::uint32_t timeoutMilliseconds);

    bool IsSpeechPresentationReady();
    bool IsSpeechPresentationVisible();
    std::uint32_t GetSpeechPresentationGeneration();
    std::uint32_t GetSpeechPresentCount();
    std::int32_t GetSpeechPresentResult();
    std::int32_t GetSpeechDeviceRemovedReason();
    std::int32_t GetSpeechFailureStage();
    std::uint32_t GetSpeechWindowCreatedCount();
    std::uint32_t GetSpeechWindowDestroyedCount();
    std::uint32_t GetSpeechRegionCreatedCount();
    std::uint32_t GetSpeechRegionTransferredCount();
    std::uint32_t GetSpeechRegionCallerDeletedCount();
    std::uint32_t GetSpeechRegionLiveOwnedCount();
    std::uint32_t GetSpeechCompositionTargetCreatedCount();
    std::uint32_t GetSpeechVisualCreatedCount();
    std::uint32_t GetSpeechSwapChainCreatedCount();
    std::uint32_t GetSpeechFollowUpdateCount();
    std::uint32_t GetSpeechMaximumFollowErrorPixels();
    std::uint32_t GetSpeechEdgeFlipCount();
    std::uint32_t GetSpeechClampCount();
    std::uint32_t GetSpeechDpi();
    bool DidSpeechOwnerMatchMascot();
    bool DidSpeechCleanupSucceed();
    std::uint32_t GetSpeechContextAvailabilityMask();
    std::uint32_t GetSpeechInitializeRequestCount();
    std::uint32_t GetSpeechInitializePostSuccessCount();
    std::uint32_t GetSpeechInitializeHandleCount();
    std::uint32_t GetSpeechLiveResourceCount();
}
