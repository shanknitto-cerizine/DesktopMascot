#pragma once

#include "IUnityGraphics.h"

enum DesktopMascotNativeRenderEventId
{
    DMN_RENDER_EVENT_DIAGNOSTIC = 1,
    DMN_RENDER_EVENT_TEXTURE_DIAGNOSTIC = 2,
    DMN_RENDER_EVENT_DESTINATION_TEXTURE_CREATE = 3,
    DMN_RENDER_EVENT_COPY_RESOURCE_DIAGNOSTIC = 4,
    DMN_RENDER_EVENT_READBACK_COPY = 5,
    DMN_RENDER_EVENT_READBACK_FENCE_SIGNAL = 6,
    DMN_RENDER_EVENT_COMPOSITION_COPY = 7,
    DMN_RENDER_EVENT_CONTINUOUS_COMPOSITION_FRAME = 8
};

extern "C"
{
    UNITY_INTERFACE_EXPORT int DMN_GetPluginApiVersion();
    UNITY_INTERFACE_EXPORT int DMN_IsGraphicsInitialized();
    UNITY_INTERFACE_EXPORT int DMN_GetRendererType();
    UNITY_INTERFACE_EXPORT int DMN_GetDeviceEventCount();
    UNITY_INTERFACE_EXPORT int DMN_IsD3D12InterfaceAvailable();
    UNITY_INTERFACE_EXPORT int DMN_IsD3D12DeviceAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetD3D12InterfaceVersion();
    UNITY_INTERFACE_EXPORT int DMN_GetD3D12DeviceNodeCount();
    UNITY_INTERFACE_EXPORT int DMN_IsD3D12CommandQueueAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetD3D12CommandQueueType();
    UNITY_INTERFACE_EXPORT int DMN_GetD3D12CommandQueueNodeMask();
    UNITY_INTERFACE_EXPORT UnityRenderingEvent UNITY_INTERFACE_API
        DMN_GetRenderEventFunc();
    UNITY_INTERFACE_EXPORT int DMN_GetRenderEventCount();
    UNITY_INTERFACE_EXPORT int DMN_GetLastRenderEventId();
    UNITY_INTERFACE_EXPORT unsigned long DMN_GetLastRenderThreadId();
    UNITY_INTERFACE_EXPORT int DMN_WasGraphicsReadyDuringLastRenderEvent();
    UNITY_INTERFACE_EXPORT int DMN_WasD3D12ReadyDuringLastRenderEvent();
    UNITY_INTERFACE_EXPORT UnityRenderingEventAndData UNITY_INTERFACE_API
        DMN_GetRenderEventAndDataFunc();
    UNITY_INTERFACE_EXPORT int DMN_GetTextureDiagnosticEventCount();
    UNITY_INTERFACE_EXPORT int DMN_WasTextureDataNonNull();
    UNITY_INTERFACE_EXPORT int DMN_WasTextureResourceDescriptionAvailable();
    UNITY_INTERFACE_EXPORT int DMN_WasCommandRecordingStateAvailable();
    UNITY_INTERFACE_EXPORT int DMN_WasCommandListAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetTextureDimension();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetTextureAlignment();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetTextureWidth();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureHeight();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureDepthOrArraySize();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureMipLevels();
    UNITY_INTERFACE_EXPORT int DMN_GetTextureFormat();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureSampleCount();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureSampleQuality();
    UNITY_INTERFACE_EXPORT int DMN_GetTextureLayout();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetTextureFlags();
    UNITY_INTERFACE_EXPORT int DMN_IsDestinationTextureAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetDestinationTextureCreationResult();
    UNITY_INTERFACE_EXPORT int DMN_GetDestinationTextureDimension();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetDestinationTextureWidth();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetDestinationTextureHeight();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetDestinationTextureDepthOrArraySize();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetDestinationTextureMipLevels();
    UNITY_INTERFACE_EXPORT int DMN_GetDestinationTextureFormat();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetDestinationTextureSampleCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetDestinationTextureSampleQuality();
    UNITY_INTERFACE_EXPORT int DMN_GetDestinationTextureLayout();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetDestinationTextureFlags();
    UNITY_INTERFACE_EXPORT int DMN_GetDestinationTextureInitialState();
    UNITY_INTERFACE_EXPORT int DMN_GetCopyDiagnosticEventCount();
    UNITY_INTERFACE_EXPORT int DMN_GetLastCopyDiagnosticEventId();
    UNITY_INTERFACE_EXPORT unsigned long
        DMN_GetLastCopyDiagnosticRenderThreadId();
    UNITY_INTERFACE_EXPORT int DMN_WasCopyEventDataNonNull();
    UNITY_INTERFACE_EXPORT int DMN_WasCopySourceAvailable();
    UNITY_INTERFACE_EXPORT int DMN_WasCopyDestinationAvailable();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCopyCommandRecordingStateAvailable();
    UNITY_INTERFACE_EXPORT int DMN_WasCopyCommandListAvailable();
    UNITY_INTERFACE_EXPORT int DMN_DidCopyValidationPass();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCopyResourceStateRequestAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidCopyResourceStateRequestSucceed();
    UNITY_INTERFACE_EXPORT int DMN_WasCopyResourceRecorded();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCopyResourceStateNotificationAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCopyResourceStateNotificationComplete();
    UNITY_INTERFACE_EXPORT int DMN_WasCopyAlreadyRecorded();
    UNITY_INTERFACE_EXPORT int DMN_GetCopyFailureStage();
    UNITY_INTERFACE_EXPORT int DMN_GetCopySourceRequestedState();
    UNITY_INTERFACE_EXPORT int DMN_GetCopyDestinationTrackedState();
    UNITY_INTERFACE_EXPORT int DMN_GetCopyAttemptCount();
    UNITY_INTERFACE_EXPORT int DMN_GetCopySuccessCount();
    UNITY_INTERFACE_EXPORT int DMN_IsReadbackBufferAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetReadbackBufferCreationResult();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetReadbackFootprintOffset();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackFootprintRowPitch();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackNumRows();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetReadbackRowSizeInBytes();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetReadbackTotalBytes();
    UNITY_INTERFACE_EXPORT int DMN_GetReadbackCopyEventCount();
    UNITY_INTERFACE_EXPORT int DMN_WasReadbackCopyCallbackReached();
    UNITY_INTERFACE_EXPORT int DMN_WasReadbackCopyRecorded();
    UNITY_INTERFACE_EXPORT int DMN_IsReadbackFenceAvailable();
    UNITY_INTERFACE_EXPORT int DMN_WasReadbackFenceSignalAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidReadbackFenceSignalSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetReadbackFenceSignalHRESULT();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetReadbackFenceSubmittedValue();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetReadbackFenceCompletedValue();
    UNITY_INTERFACE_EXPORT int DMN_IsReadbackFenceComplete();
    UNITY_INTERFACE_EXPORT int DMN_ValidateReadbackPixels();
    UNITY_INTERFACE_EXPORT int DMN_WasReadbackMapAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidReadbackMapSucceed();
    UNITY_INTERFACE_EXPORT int DMN_WasReadbackValidationCompleted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidReadbackExpectedOrientationMatch();
    UNITY_INTERFACE_EXPORT int
        DMN_DidReadbackVerticallyFlippedOrientationMatch();
    UNITY_INTERFACE_EXPORT int DMN_GetReadbackFailureStage();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackTopLeftBGRA();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackTopRightBGRA();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackBottomLeftBGRA();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetReadbackBottomRightBGRA();
    UNITY_INTERFACE_EXPORT int DMN_StartCompositionDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_RequestCompositionDiagnosticsShutdown();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionInitializationState();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionFailureStage();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetCompositionUiThreadId();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionComInitializationAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionComInitializationSucceed();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionComInitializationHRESULT();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionWindowClassRegistrationAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionWindowClassRegistrationSucceed();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowClassLastError();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionWindowCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionWindowAvailable();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowCreationLastError();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowClientWidth();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowClientHeight();
    UNITY_INTERFACE_EXPORT int DMN_WasDxgiFactoryCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsDxgiFactoryAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetDxgiFactoryCreationHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompDeviceCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsDCompDeviceAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompDeviceCreationHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompTargetCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsDCompTargetAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompTargetCreationHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompVisualCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsDCompVisualAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompVisualCreationHRESULT();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionSwapChainCreationAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionSwapChainAvailable();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionSwapChainCreationHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainWidth();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainHeight();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainFormat();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainBufferCount();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainSwapEffect();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainAlphaMode();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionSwapChainScaling();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompSetContentAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidDCompSetContentSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompSetContentHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompSetRootAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidDCompSetRootSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompSetRootHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasDCompCommitAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidDCompCommitSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompCommitHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_GetDCompCommitCount();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionWindowShown();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionMessageLoopRunning();
    UNITY_INTERFACE_EXPORT int DMN_StartCompositionPresentDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_MarkCompositionCopyEventIssued();
    UNITY_INTERFACE_EXPORT int DMN_PollCompositionPresentDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_MarkCompositionDisplayHolding();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPresentState();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPresentFailureStage();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionCommandSubmissionMode();
    UNITY_INTERFACE_EXPORT int DMN_WasSwapChain3QueryAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsSwapChain3Available();
    UNITY_INTERFACE_EXPORT int DMN_GetSwapChain3QueryHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_WasBackBufferIndexQueried();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetCurrentBackBufferIndex();
    UNITY_INTERFACE_EXPORT int DMN_IsCurrentBackBufferIndexValid();
    UNITY_INTERFACE_EXPORT int DMN_WasBackBufferGetAttempted();
    UNITY_INTERFACE_EXPORT int DMN_IsBackBufferAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetBackBufferGetHRESULT();
    UNITY_INTERFACE_EXPORT int DMN_IsBackBufferDescriptionAvailable();
    UNITY_INTERFACE_EXPORT int DMN_GetBackBufferDimension();
    UNITY_INTERFACE_EXPORT unsigned long long DMN_GetBackBufferWidth();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetBackBufferHeight();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetBackBufferDepthOrArraySize();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetBackBufferMipLevels();
    UNITY_INTERFACE_EXPORT int DMN_GetBackBufferFormat();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetBackBufferSampleCount();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetBackBufferSampleQuality();
    UNITY_INTERFACE_EXPORT int DMN_GetBackBufferLayout();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetBackBufferFlags();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionCopyCompatibilityChecked();
    UNITY_INTERFACE_EXPORT int
        DMN_AreCompositionCopyDimensionsCompatible();
    UNITY_INTERFACE_EXPORT int DMN_AreCompositionCopySamplesCompatible();
    UNITY_INTERFACE_EXPORT int DMN_AreCompositionCopyFormatsCompatible();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionCopyEventIssued();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionCopyEventCount();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionCopyCallbackReached();
    UNITY_INTERFACE_EXPORT int DMN_WereCompositionCopyBarriersRecorded();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionBackBufferCopyRecorded();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionCopySubmitted();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionExecuteCommandListAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionExecuteCommandListReturnFenceValue();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionExecuteCommandListFenceValue();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionPresentFenceCreated();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionPresentFenceAvailable();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionPresentFenceSignalAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionPresentFenceSignalSucceed();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionPresentFenceSignalHRESULT();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPresentFenceSubmittedValue();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPresentFenceCompletedValue();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionPresentFenceComplete();
    UNITY_INTERFACE_EXPORT int DMN_DidCompositionPresentFenceTimeout();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionPresentMessagePostAttempted();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionPresentMessagePostSucceed();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPresentMessagePostLastError();
    UNITY_INTERFACE_EXPORT int
        DMN_WasCompositionPresentMessageReceived();
    UNITY_INTERFACE_EXPORT int DMN_WasCompositionPresentAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidCompositionPresentSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPresentHRESULT();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPresentSyncInterval();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetCompositionPresentFlags();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPresentCount();
    UNITY_INTERFACE_EXPORT int DMN_WasPostPresentCommitAttempted();
    UNITY_INTERFACE_EXPORT int DMN_DidPostPresentCommitSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetPostPresentCommitHRESULT();
    UNITY_INTERFACE_EXPORT int
        DMN_StartContinuousCompositionDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_RequestContinuousCompositionFrame();
    UNITY_INTERFACE_EXPORT int
        DMN_RequestContinuousCompositionDiagnosticsStop();
    UNITY_INTERFACE_EXPORT int
        DMN_PollContinuousCompositionDiagnostics();
    UNITY_INTERFACE_EXPORT void
        DMN_RecordContinuousCompositionDroppedSchedule();
    UNITY_INTERFACE_EXPORT int DMN_GetContinuousCompositionState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetContinuousCompositionFailureStage();
    UNITY_INTERFACE_EXPORT int
        DMN_GetContinuousCompositionCommandSubmissionMode();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionTargetFrameCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionRequestedFrameCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionRecordedFrameCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionSubmittedFrameCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionFenceCompletedFrameCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionPresentCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionLastSequence();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionLastBackBufferIndex();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionBackBuffer0UseCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionBackBuffer1UseCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionInvalidBackBufferIndexCount();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionLastFenceSubmittedValue();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionLastFenceCompletedValue();
    UNITY_INTERFACE_EXPORT int
        DMN_IsContinuousCompositionFrameInFlight();
    UNITY_INTERFACE_EXPORT int
        DMN_GetContinuousCompositionLastPresentHRESULT();
    UNITY_INTERFACE_EXPORT int
        DMN_GetContinuousCompositionLastDeviceRemovedReason();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionElapsedMilliseconds();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionMinimumFrameMilliseconds();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionMaximumFrameMilliseconds();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetContinuousCompositionAverageFrameMicroseconds();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionDroppedScheduleCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetContinuousCompositionRejectedFrameRequestCount();
    UNITY_INTERFACE_EXPORT int
        DMN_DidContinuousCompositionCompleteNormally();
    UNITY_INTERFACE_EXPORT int DMN_DidContinuousCompositionTimeout();
    UNITY_INTERFACE_EXPORT int
        DMN_StartCompositionWindowPositionDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_RequestCompositionWindowPosition(
        int x,
        int y);
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowPositionState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionWindowPositionFailureStage();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowRequestedX();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowRequestedY();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowActualX();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowActualY();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowMoveRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowMoveAppliedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowMoveRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowLastSetWindowPosLastError();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionWindowMovePending();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionWindowLastMoveSucceed();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWorkAreaLeft();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWorkAreaTop();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWorkAreaRight();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWorkAreaBottom();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowInitialX();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowInitialY();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowInitialWidth();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowInitialHeight();
    UNITY_INTERFACE_EXPORT int
        DMN_StartCompositionClickThroughDiagnostics();
    UNITY_INTERFACE_EXPORT int
        DMN_RequestCompositionClickThroughEnabled(int enabled);
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionClickThroughState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionClickThroughFailureStage();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionClickThroughEnabled();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionClickThroughRequestPending();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionClickThroughLastRequestSucceed();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughEnableRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughDisableRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughAppliedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughLastWin32Error();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowInitialStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowCurrentStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowInitialExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowCurrentExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowClassStyle();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionInitialExtendedStyleRestored();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughHitTestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughTransparentHitTestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughLastRequestThreadId();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionClickThroughLastAppliedThreadId();
    UNITY_INTERFACE_EXPORT int DMN_StartCompositionAlphaMaskDiagnostics(
        int width,
        int height,
        int stride,
        int alphaThreshold);
    UNITY_INTERFACE_EXPORT int DMN_SubmitCompositionAlphaMask(
        const unsigned char* data,
        int width,
        int height,
        int stride,
        unsigned long long generation);
    UNITY_INTERFACE_EXPORT void DMN_StopCompositionAlphaMaskDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionAlphaMaskFailureStage();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionAlphaMaskSubmittedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionAlphaMaskAcceptedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionAlphaMaskRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionAlphaMaskPublishedGeneration();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskWidth();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskHeight();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskStride();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskByteCount();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskThreshold();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskSampleAlpha(
        int x,
        int y);
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionAlphaMaskSampleHit(
        int x,
        int y);
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionAlphaMaskAvailable();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionAlphaMaskRequestPending();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionAlphaMaskLastSubmitSucceed();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionAlphaMaskLastWin32Error();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionAlphaMaskLastSubmitThreadId();
    UNITY_INTERFACE_EXPORT int
        DMN_StartCompositionPixelHitTestDiagnostics(int alphaThreshold);
    UNITY_INTERFACE_EXPORT int
        DMN_CompleteCompositionPixelHitTestDiagnostics();
    UNITY_INTERFACE_EXPORT int
        DMN_StopCompositionPixelHitTestDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPixelHitTestState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionPixelHitTestFailureStage();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionPixelHitTestEnabled();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionPixelHitTestThreshold();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestTotalCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestTransparentCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestOpaqueCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestOutsideClientCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestMaskUnavailableCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestScreenToClientFailureCount();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPixelHitTestLastX();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPixelHitTestLastY();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPixelHitTestLastAlpha();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionPixelHitTestLastResult();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPixelHitTestPublishedGeneration();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionPixelHitTestLastWin32Error();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionPixelHitTestStyleRestoreSucceed();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionPixelHitTestRequestPending();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionPixelHitTestLastRequestSucceed();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPixelHitTestInitialExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPixelHitTestDiagnosticExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPixelHitTestCurrentExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionPixelHitTestWindowClassStyle();
    UNITY_INTERFACE_EXPORT int
        DMN_StartCompositionWindowRegionDiagnostics(int alphaThreshold);
    UNITY_INTERFACE_EXPORT int
        DMN_CompleteCompositionWindowRegionDiagnostics();
    UNITY_INTERFACE_EXPORT int
        DMN_StopCompositionWindowRegionDiagnostics();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowRegionState();
    UNITY_INTERFACE_EXPORT int
        DMN_GetCompositionWindowRegionFailureStage();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowRegionThreshold();
    UNITY_INTERFACE_EXPORT int DMN_IsCompositionWindowRegionApplied();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowRegionType();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowRegionRectangleCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowRegionCoveredPixelCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowRegionExcludedPixelCount();
    UNITY_INTERFACE_EXPORT int DMN_GetCompositionWindowInitialRegionType();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionWindowInitialRegionRestoreSucceed();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionApplyRequestPending();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionWindowRegionLastApplySucceed();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetCompositionWindowRegionLastWin32Error();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowRegionPublishedGeneration();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionTopLeftInside();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionTopRightInside();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionBottomLeftInside();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionBottomRightInside();
    UNITY_INTERFACE_EXPORT int
        DMN_IsCompositionWindowRegionCenterInside();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowRegionInitialExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowRegionDiagnosticExtendedStyle();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetCompositionWindowRegionCurrentExtendedStyle();
    UNITY_INTERFACE_EXPORT int
        DMN_DidCompositionWindowRegionStyleRestoreSucceed();
    UNITY_INTERFACE_EXPORT int DMN_SetCompositionInitialPosition(
        int x,
        int y);
    UNITY_INTERFACE_EXPORT int DMN_EnableNativeMascotWindowDrag();
    UNITY_INTERFACE_EXPORT int DMN_DisableNativeMascotWindowDrag();
    UNITY_INTERFACE_EXPORT int DMN_StartNativeMascotWindowDragDiagnostic(
        int deltaX,
        int deltaY);
    UNITY_INTERFACE_EXPORT int
        DMN_CompleteNativeMascotWindowDragDiagnostic();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeMascotWindowDragEnabled();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeMascotWindowDragging();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeMascotWindowCaptureOwned();
    UNITY_INTERFACE_EXPORT int
        DMN_IsNativeMascotWindowAvailableForDrag();
    UNITY_INTERFACE_EXPORT int DMN_GetNativeMascotDragDiagnosticState();
    UNITY_INTERFACE_EXPORT int DMN_GetNativeMascotDragFailureStage();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetNativeMascotDragStartCount();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetNativeMascotDragMoveCount();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetNativeMascotDragEndCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotDragCaptureAcquiredCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotDragCaptureReleasedCount();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetNativeMascotCompletedDragGeneration();
    UNITY_INTERFACE_EXPORT int DMN_TryGetNativeMascotWindowPosition(
        int* x,
        int* y);
    UNITY_INTERFACE_EXPORT int DMN_GetNativeMascotDragLastWindowX();
    UNITY_INTERFACE_EXPORT int DMN_GetNativeMascotDragLastWindowY();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticMoveSucceed();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticSizeRemainUnchanged();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticRegionRemainApplied();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticPresentContinue();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticRegionPublicationContinue();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticAvoidCompositionRestart();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticPreserveZOrder();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticAvoidActivation();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticReleaseCapture();
    UNITY_INTERFACE_EXPORT int
        DMN_DidNativeMascotDragDiagnosticRestoreInitialPosition();
    UNITY_INTERFACE_EXPORT int DMN_EnableNativeMascotContextMenu();
    UNITY_INTERFACE_EXPORT int DMN_DisableNativeMascotContextMenu();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetNativeMascotCommandGeneration();
    UNITY_INTERFACE_EXPORT int DMN_TryConsumeNativeMascotCommand(
        unsigned long long* generation,
        int* command);
    UNITY_INTERFACE_EXPORT int
        DMN_PublishNativeMascotCommandForDiagnostics(int command);
    UNITY_INTERFACE_EXPORT int
        DMN_RunNativeMascotMenuResourceDiagnostic();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotCommandPublishCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotCommandConsumeCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotCommandRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotMenuCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotMenuDestroyedCount();
    UNITY_INTERFACE_EXPORT int
        DMN_GetNativeMascotMenuLiveOwnedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeMascotMenuCancelledCount();
    UNITY_INTERFACE_EXPORT int
        DMN_GetNativeApplicationCommandLastSource();
    UNITY_INTERFACE_EXPORT int DMN_StartNativeTrayIcon();
    UNITY_INTERFACE_EXPORT int DMN_StopNativeTrayIcon();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeTrayIconRunning();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeTrayOwnerWindowAvailable();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeTrayIconRegistered();
    UNITY_INTERFACE_EXPORT int DMN_IsNativeTrayPopupActive();
    UNITY_INTERFACE_EXPORT int DMN_WasNativeTrayTooltipConfigured();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayInitialAddRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTraySetVersionRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayDeleteRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayReregisterRequestCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayOwnerCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayOwnerDestroyedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayMenuCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayMenuDestroyedCount();
    UNITY_INTERFACE_EXPORT int DMN_GetNativeTrayMenuLiveOwnedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayIconCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayIconDestroyedCount();
    UNITY_INTERFACE_EXPORT int DMN_GetNativeTrayIconLiveOwnedCount();
    UNITY_INTERFACE_EXPORT unsigned int DMN_GetNativeTrayCancelledCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayOpenSettingsSelectionCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayRequestExitSelectionCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayTaskbarCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayShutdownRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayNimAddAttemptCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayNimAddSuccessCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayNimAddLastError();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayNimSetVersionSuccessCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayNimSetVersionLastError();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayRegistrationRetryCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetNativeTrayShutdownRetrySuppressedCount();
    UNITY_INTERFACE_EXPORT int
        DMN_GetNativeTrayRegistrationFinalResult();
    UNITY_INTERFACE_EXPORT int
        DMN_WasNativeTrayRegistrationRetryExhausted();
    UNITY_INTERFACE_EXPORT int
        DMN_PublishNativeTrayCommandForDiagnostics(int command);
    UNITY_INTERFACE_EXPORT int DMN_RunNativeTrayFocusedDiagnostic();
    UNITY_INTERFACE_EXPORT int DMN_InitializeSingleInstance();
    UNITY_INTERFACE_EXPORT int DMN_BeginSingleInstanceShutdown();
    UNITY_INTERFACE_EXPORT int DMN_ShutdownSingleInstance();
    UNITY_INTERFACE_EXPORT int DMN_TryConsumeSingleInstanceActivation(
        unsigned long long* generation);
    UNITY_INTERFACE_EXPORT int
        DMN_RunSingleInstanceCoalescingFocusedDiagnostic();
    UNITY_INTERFACE_EXPORT int DMN_IsSingleInstancePrimary();
    UNITY_INTERFACE_EXPORT int
        DMN_IsSingleInstanceNotificationReady();
    UNITY_INTERFACE_EXPORT int
        DMN_IsSingleInstanceAcceptingActivations();
    UNITY_INTERFACE_EXPORT int DMN_IsSingleInstanceActivationPending();
    UNITY_INTERFACE_EXPORT unsigned long long
        DMN_GetSingleInstanceActivationGeneration();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceSignalReceivedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceGenerationPublishCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceActivationConsumeCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceActivationCoalescedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceActivationRejectedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceMaximumPendingCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceNotificationCreatedCount();
    UNITY_INTERFACE_EXPORT unsigned int
        DMN_GetSingleInstanceNotificationDestroyedCount();
    UNITY_INTERFACE_EXPORT int DMN_GetSingleInstanceFailureStage();
}
