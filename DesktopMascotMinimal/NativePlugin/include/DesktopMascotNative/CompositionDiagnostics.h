#pragma once

#include <cstdint>

struct ID3D12CommandQueue;

namespace DesktopMascotNative
{
    enum class CompositionInitializationState : std::int32_t
    {
        NotStarted = 0,
        ThreadStarting = 1,
        ComInitialized = 2,
        WindowClassRegistered = 3,
        WindowCreated = 4,
        DxgiFactoryCreated = 5,
        DCompDeviceCreated = 6,
        DCompTargetCreated = 7,
        DCompVisualCreated = 8,
        SwapChainCreated = 9,
        VisualContentSet = 10,
        RootSet = 11,
        CommitSucceeded = 12,
        WindowShown = 13,
        MessageLoopRunning = 14,
        Failed = 15,
        ShutdownRequested = 16,
        Stopped = 17
    };

    enum class CompositionFailureStage : std::int32_t
    {
        None = 0,
        ThreadCreationFailed = 1,
        ComInitializationFailed = 2,
        ModuleHandleUnavailable = 3,
        WindowClassRegistrationFailed = 4,
        WindowCreationFailed = 5,
        InvalidClientSize = 6,
        DxgiFactoryCreationFailed = 7,
        DCompDeviceCreationFailed = 8,
        DCompTargetCreationFailed = 9,
        DCompVisualCreationFailed = 10,
        SwapChainCreationFailed = 11,
        SetContentFailed = 12,
        SetRootFailed = 13,
        CommitFailed = 14,
        ShowWindowFailed = 15,
        MessageLoopFailed = 16,
        ShutdownFailed = 17,
        AlreadyStarted = 18
    };

    void ResetCompositionDiagnostics();
    std::int32_t StartCompositionDiagnostics(ID3D12CommandQueue* commandQueue);
    bool RequestCompositionDiagnosticsShutdown();
    bool StopCompositionDiagnosticsForUnload(std::uint32_t timeoutMilliseconds);

    std::int32_t GetCompositionInitializationState();
    std::int32_t GetCompositionFailureStage();
    std::uint32_t GetCompositionUiThreadId();
    bool WasCompositionComInitializationAttempted();
    bool DidCompositionComInitializationSucceed();
    std::int32_t GetCompositionComInitializationResult();
    bool WasCompositionWindowClassRegistrationAttempted();
    bool DidCompositionWindowClassRegistrationSucceed();
    std::uint32_t GetCompositionWindowClassLastError();
    bool WasCompositionWindowCreationAttempted();
    bool IsCompositionWindowAvailable();
    std::uint32_t GetCompositionWindowCreationLastError();
    std::int32_t GetCompositionWindowClientWidth();
    std::int32_t GetCompositionWindowClientHeight();
    bool WasDxgiFactoryCreationAttempted();
    bool IsDxgiFactoryAvailable();
    std::int32_t GetDxgiFactoryCreationResult();
    bool WasDCompDeviceCreationAttempted();
    bool IsDCompDeviceAvailable();
    std::int32_t GetDCompDeviceCreationResult();
    bool WasDCompTargetCreationAttempted();
    bool IsDCompTargetAvailable();
    std::int32_t GetDCompTargetCreationResult();
    bool WasDCompVisualCreationAttempted();
    bool IsDCompVisualAvailable();
    std::int32_t GetDCompVisualCreationResult();
    bool WasCompositionSwapChainCreationAttempted();
    bool IsCompositionSwapChainAvailable();
    std::int32_t GetCompositionSwapChainCreationResult();
    std::int32_t GetCompositionSwapChainWidth();
    std::int32_t GetCompositionSwapChainHeight();
    std::int32_t GetCompositionSwapChainFormat();
    std::int32_t GetCompositionSwapChainBufferCount();
    std::int32_t GetCompositionSwapChainSwapEffect();
    std::int32_t GetCompositionSwapChainAlphaMode();
    std::int32_t GetCompositionSwapChainScaling();
    bool WasDCompSetContentAttempted();
    bool DidDCompSetContentSucceed();
    std::int32_t GetDCompSetContentResult();
    bool WasDCompSetRootAttempted();
    bool DidDCompSetRootSucceed();
    std::int32_t GetDCompSetRootResult();
    bool WasDCompCommitAttempted();
    bool DidDCompCommitSucceed();
    std::int32_t GetDCompCommitResult();
    std::int32_t GetDCompCommitCount();
    bool WasCompositionWindowShown();
    bool IsCompositionMessageLoopRunning();
}
