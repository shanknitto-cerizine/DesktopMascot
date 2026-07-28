using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionPresentDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionStopped = 17;
        private const int CompositionCopyEventId = 7;
        private const int WaitingForCopyEvent = 5;
        private const int PresentSucceeded = 12;
        private const int PresentFailed = 16;
        private bool shutdownRequested;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionPresentDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_MarkCompositionCopyEventIssued();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_PollCompositionPresentDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_MarkCompositionDisplayHolding();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPresentState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPresentFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionCommandSubmissionMode();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasSwapChain3QueryAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsSwapChain3Available();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetSwapChain3QueryHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasBackBufferIndexQueried();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCurrentBackBufferIndex();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCurrentBackBufferIndexValid();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasBackBufferGetAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsBackBufferAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetBackBufferGetHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsBackBufferDescriptionAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetBackBufferDimension();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetBackBufferWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferDepthOrArraySize();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferMipLevels();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetBackBufferFormat();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferSampleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferSampleQuality();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetBackBufferLayout();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetBackBufferFlags();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionCopyCompatibilityChecked();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_AreCompositionCopyDimensionsCompatible();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_AreCompositionCopySamplesCompatible();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_AreCompositionCopyFormatsCompatible();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionCopyEventIssued();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionCopyEventCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionCopyCallbackReached();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WereCompositionCopyBarriersRecorded();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionBackBufferCopyRecorded();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionCopySubmitted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionExecuteCommandListAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionExecuteCommandListReturnFenceValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionExecuteCommandListFenceValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionPresentFenceCreated();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionPresentFenceAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionPresentFenceSignalAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPresentFenceSignalSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPresentFenceSignalHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPresentFenceSubmittedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPresentFenceCompletedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionPresentFenceComplete();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPresentFenceTimeout();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionPresentMessagePostAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPresentMessagePostSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPresentMessagePostLastError();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionPresentMessageReceived();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionPresentAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPresentSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPresentSyncInterval();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPresentFlags();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasPostPresentCommitAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidPostPresentCommitSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetPostPresentCommitHRESULT();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventFunc();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(nameof(DesktopMascotCompositionPresentDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionPresentDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            var prerequisitesReady = false;
            for (var frame = 0; frame < 600; frame++)
            {
                try
                {
                    prerequisitesReady =
                        DMN_GetCompositionInitializationState()
                            == CompositionMessageLoopRunning
                        && DMN_IsDestinationTextureAvailable() != 0
                        && DMN_WasReadbackValidationCompleted() != 0
                        && DMN_DidReadbackExpectedOrientationMatch() != 0;
                }
                catch (Exception exception) when (IsInteropException(exception))
                {
                    LogInteropError(exception);
                    yield break;
                }
                if (prerequisitesReady)
                {
                    break;
                }
                yield return null;
            }

            if (!prerequisitesReady)
            {
                Debug.LogError(
                    "[DesktopMascotCompositionPresentDiagnostics] Prerequisites did not become ready.");
                RequestShutdown();
                yield break;
            }

            int startResult;
            try
            {
                startResult = DMN_StartCompositionPresentDiagnostics();
                Debug.Log(
                    $"[DesktopMascotCompositionPresentDiagnostics] Start result: {startResult}");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                RequestShutdown();
                yield break;
            }
            if (startResult != 1)
            {
                RequestShutdown();
                yield break;
            }

            var state = 0;
            for (var frame = 0; frame < 300; frame++)
            {
                state = DMN_GetCompositionPresentState();
                if (state == WaitingForCopyEvent || state == PresentFailed)
                {
                    break;
                }
                yield return null;
            }

            if (state == WaitingForCopyEvent && !IssueCopyEventOnce())
            {
                Debug.LogError(
                    "[DesktopMascotCompositionPresentDiagnostics] Copy event was not issued.");
                RequestShutdown();
                yield break;
            }

            for (var frame = 0; frame < 600; frame++)
            {
                state = DMN_PollCompositionPresentDiagnostics();
                if (state == PresentSucceeded || state == PresentFailed)
                {
                    break;
                }
                yield return null;
            }

            LogResults();
            if (state == PresentSucceeded)
            {
                DMN_MarkCompositionDisplayHolding();
                yield return new WaitForSecondsRealtime(5.0f);
                Debug.Log(
                    "[DesktopMascotCompositionPresentDiagnostics] Four-color display hold completed: True");
            }
            RequestShutdown();

            for (var frame = 0; frame < 600; frame++)
            {
                if (DMN_GetCompositionInitializationState()
                    == CompositionStopped)
                {
                    break;
                }
                yield return null;
            }
            Debug.Log(
                $"[DesktopMascotCompositionPresentDiagnostics] Final Present state: {DMN_GetCompositionPresentState()}");
            Destroy(gameObject);
        }

        private static bool IssueCopyEventOnce()
        {
            CommandBuffer commandBuffer = null;
            try
            {
                var callback = DMN_GetRenderEventFunc();
                if (callback == IntPtr.Zero
                    || DMN_MarkCompositionCopyEventIssued() == 0)
                {
                    return false;
                }
                commandBuffer = new CommandBuffer
                {
                    name = "Desktop Mascot Composition Copy Once"
                };
                commandBuffer.IssuePluginEvent(
                    callback,
                    CompositionCopyEventId);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
            finally
            {
                commandBuffer?.Release();
            }
        }

        private static void LogResults()
        {
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Command submission mode: {DMN_GetCompositionCommandSubmissionMode()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present state: {DMN_GetCompositionPresentState()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Failure stage: {DMN_GetCompositionPresentFailureStage()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] SwapChain3 query attempted: {B(DMN_WasSwapChain3QueryAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] SwapChain3 available: {B(DMN_IsSwapChain3Available())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] SwapChain3 HRESULT: {H(DMN_GetSwapChain3QueryHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer index queried: {B(DMN_WasBackBufferIndexQueried())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Current back-buffer index: {DMN_GetCurrentBackBufferIndex()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer index valid: {B(DMN_IsCurrentBackBufferIndexValid())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer GetBuffer attempted: {B(DMN_WasBackBufferGetAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back buffer available: {B(DMN_IsBackBufferAvailable())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer HRESULT: {H(DMN_GetBackBufferGetHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer description available: {B(DMN_IsBackBufferDescriptionAvailable())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer dimension: {DMN_GetBackBufferDimension()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer width: {DMN_GetBackBufferWidth()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer height: {DMN_GetBackBufferHeight()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer depth/array size: {DMN_GetBackBufferDepthOrArraySize()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer mip levels: {DMN_GetBackBufferMipLevels()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer format: {DMN_GetBackBufferFormat()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer sample count: {DMN_GetBackBufferSampleCount()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer sample quality: {DMN_GetBackBufferSampleQuality()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer layout: {DMN_GetBackBufferLayout()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer flags: {DMN_GetBackBufferFlags()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy compatibility checked: {B(DMN_WasCompositionCopyCompatibilityChecked())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy dimensions compatible: {B(DMN_AreCompositionCopyDimensionsCompatible())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy samples compatible: {B(DMN_AreCompositionCopySamplesCompatible())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy formats compatible: {B(DMN_AreCompositionCopyFormatsCompatible())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy event issued: {B(DMN_WasCompositionCopyEventIssued())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy event count: {DMN_GetCompositionCopyEventCount()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy callback reached: {B(DMN_WasCompositionCopyCallbackReached())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Resource barriers recorded: {B(DMN_WereCompositionCopyBarriersRecorded())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Back-buffer copy recorded: {B(DMN_WasCompositionBackBufferCopyRecorded())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Copy submitted: {B(DMN_WasCompositionCopySubmitted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Unity ExecuteCommandList attempted: {B(DMN_WasCompositionExecuteCommandListAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Unity ExecuteCommandList returned fence value: {B(DMN_DidCompositionExecuteCommandListReturnFenceValue())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Unity ExecuteCommandList fence value: {DMN_GetCompositionExecuteCommandListFenceValue()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Plugin-owned fence created: {B(DMN_WasCompositionPresentFenceCreated())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Unity frame fence available: {B(DMN_IsCompositionPresentFenceAvailable())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Direct queue Signal attempted: {B(DMN_WasCompositionPresentFenceSignalAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Direct queue Signal succeeded: {B(DMN_DidCompositionPresentFenceSignalSucceed())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Direct queue Signal HRESULT: {H(DMN_GetCompositionPresentFenceSignalHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Fence submitted value: {DMN_GetCompositionPresentFenceSubmittedValue()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Fence completed value: {DMN_GetCompositionPresentFenceCompletedValue()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Fence complete: {B(DMN_IsCompositionPresentFenceComplete())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Fence timeout: {B(DMN_DidCompositionPresentFenceTimeout())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present message post attempted: {B(DMN_WasCompositionPresentMessagePostAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present message posted: {B(DMN_DidCompositionPresentMessagePostSucceed())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present message LastError: {DMN_GetCompositionPresentMessagePostLastError()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present message received: {B(DMN_WasCompositionPresentMessageReceived())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present attempted: {B(DMN_WasCompositionPresentAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present succeeded: {B(DMN_DidCompositionPresentSucceed())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present HRESULT: {H(DMN_GetCompositionPresentHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present SyncInterval: {DMN_GetCompositionPresentSyncInterval()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present flags: {DMN_GetCompositionPresentFlags()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Present count: {DMN_GetCompositionPresentCount()}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Post-present Commit attempted: {B(DMN_WasPostPresentCommitAttempted())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Post-present Commit succeeded: {B(DMN_DidPostPresentCommitSucceed())}");
            Debug.Log($"[DesktopMascotCompositionPresentDiagnostics] Post-present Commit HRESULT: {H(DMN_GetPostPresentCommitHRESULT())}");
        }

        private void RequestShutdown()
        {
            if (shutdownRequested)
            {
                return;
            }
            shutdownRequested = true;
            try
            {
                var accepted =
                    DMN_RequestCompositionDiagnosticsShutdown() != 0;
                Debug.Log(
                    $"[DesktopMascotCompositionDiagnostics] Shutdown requested: {accepted}");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
        }

        private void OnApplicationQuit()
        {
            RequestShutdown();
        }

        private static bool IsInteropException(Exception exception)
        {
            return exception is DllNotFoundException
                || exception is EntryPointNotFoundException
                || exception is BadImageFormatException;
        }

        private static void LogInteropError(Exception exception)
        {
            Debug.LogError(
                $"[DesktopMascotCompositionPresentDiagnostics] Native interop failed: {exception.Message}");
        }

        private static bool B(int value)
        {
            return value != 0;
        }

        private static string H(int value)
        {
            return $"0x{unchecked((uint)value):X8}";
        }
#endif
    }
}
