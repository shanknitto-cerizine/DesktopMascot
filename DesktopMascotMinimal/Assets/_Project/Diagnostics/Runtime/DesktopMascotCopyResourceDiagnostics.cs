using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCopyResourceDiagnostics : MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int CopyResourceDiagnosticEventId = 4;
        private const int TextureSize = 256;

        private RenderTexture sourceTexture;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsGraphicsInitialized();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12DeviceAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsDestinationTextureAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventAndDataFunc();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopyDiagnosticEventCount();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetLastCopyDiagnosticEventId();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetLastCopyDiagnosticRenderThreadId();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyEventDataNonNull();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopySourceAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyDestinationAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_WasCopyCommandRecordingStateAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyCommandListAvailable();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidCopyValidationPass();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_WasCopyResourceStateRequestAttempted();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidCopyResourceStateRequestSucceed();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyResourceRecorded();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_WasCopyResourceStateNotificationAttempted();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidCopyResourceStateNotificationComplete();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyAlreadyRecorded();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopyFailureStage();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopySourceRequestedState();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopyDestinationTrackedState();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopyAttemptCount();

        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopySuccessCount();
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            var ready = false;
            for (var frame = 0; frame < 120; frame++)
            {
                if (!TryReadReady(out ready))
                {
                    Cleanup();
                    yield break;
                }

                if (ready)
                {
                    break;
                }

                yield return null;
            }

            if (!ready || !TryIssueCopyEvent())
            {
                Debug.LogError(
                    "[DesktopMascotCopyResourceDiagnostics] Required D3D12 resources did not become ready.");
                Cleanup();
                yield break;
            }

            // IssuePluginEventAndData is asynchronous. Keep the Unity-owned
            // source alive and strongly referenced until the callback has had
            // three additional frames to execute.
            yield return null;
            yield return null;
            yield return null;

            LogResults();
            Cleanup();
        }

        private static bool TryReadReady(out bool ready)
        {
            ready = false;

            try
            {
                ready =
                    DMN_IsGraphicsInitialized() != 0
                    && DMN_IsD3D12DeviceAvailable() != 0
                    && DMN_IsDestinationTextureAvailable() != 0;
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
        }

        private bool TryIssueCopyEvent()
        {
            CommandBuffer commandBuffer = null;

            try
            {
                const GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB;
                if (!SystemInfo.IsFormatSupported(
                        format,
                        GraphicsFormatUsage.Render))
                {
                    Debug.LogError(
                        "[DesktopMascotCopyResourceDiagnostics] B8G8R8A8_SRGB render format is unsupported.");
                    return false;
                }

                var descriptor = new RenderTextureDescriptor(
                    TextureSize,
                    TextureSize)
                {
                    graphicsFormat = format,
                    depthStencilFormat = GraphicsFormat.None,
                    msaaSamples = 1,
                    volumeDepth = 1,
                    dimension = TextureDimension.Tex2D,
                    mipCount = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = false
                };

                sourceTexture = new RenderTexture(descriptor)
                {
                    name = nameof(DesktopMascotCopyResourceDiagnostics)
                };
                sourceTexture.Create();
                if (!sourceTexture.IsCreated())
                {
                    Debug.LogError(
                        "[DesktopMascotCopyResourceDiagnostics] Source RenderTexture creation failed.");
                    return false;
                }

                // One native-pointer query only. Native code borrows this
                // Unity-owned resource during event 4 and never retains,
                // AddRefs, or Releases it.
                var sourcePointer = sourceTexture.GetNativeTexturePtr();
                if (sourcePointer == IntPtr.Zero)
                {
                    Debug.LogError(
                        "[DesktopMascotCopyResourceDiagnostics] Source native texture pointer is null.");
                    return false;
                }

                var callback = DMN_GetRenderEventAndDataFunc();
                if (callback == IntPtr.Zero)
                {
                    Debug.LogError(
                        "[DesktopMascotCopyResourceDiagnostics] Render event-and-data callback is null.");
                    return false;
                }

                commandBuffer = new CommandBuffer
                {
                    name = "Desktop Mascot One-Shot CopyResource Diagnostic"
                };
                commandBuffer.SetRenderTarget(sourceTexture);
                commandBuffer.ClearRenderTarget(false, true, Color.clear);
                commandBuffer.IssuePluginEventAndData(
                    callback,
                    CopyResourceDiagnosticEventId,
                    sourcePointer);

                // The clear and event enter the same Unity render command
                // stream in this order. Native one-shot state prevents a
                // duplicate CopyResource even if event 4 is issued again.
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
            try
            {
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Copy event count: {DMN_GetCopyDiagnosticEventCount()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Last copy event ID: {DMN_GetLastCopyDiagnosticEventId()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Last copy render thread ID: {DMN_GetLastCopyDiagnosticRenderThreadId()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Copy event data non-null: {DMN_WasCopyEventDataNonNull() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Source available: {DMN_WasCopySourceAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Destination available: {DMN_WasCopyDestinationAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Command recording state available: {DMN_WasCopyCommandRecordingStateAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Command list available: {DMN_WasCopyCommandListAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Validation passed: {DMN_DidCopyValidationPass() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Resource state request attempted: {DMN_WasCopyResourceStateRequestAttempted() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Resource state request succeeded: {DMN_DidCopyResourceStateRequestSucceed() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] CopyResource recorded: {DMN_WasCopyResourceRecorded() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Resource state notification attempted: {DMN_WasCopyResourceStateNotificationAttempted() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Resource state notification completed: {DMN_DidCopyResourceStateNotificationComplete() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Copy already recorded: {DMN_WasCopyAlreadyRecorded() != 0}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Failure stage: {DMN_GetCopyFailureStage()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Source requested state: {DMN_GetCopySourceRequestedState()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Destination tracked state: {DMN_GetCopyDestinationTrackedState()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Copy attempt count: {DMN_GetCopyAttemptCount()}");
                Debug.Log(
                    $"[DesktopMascotCopyResourceDiagnostics] Copy success count: {DMN_GetCopySuccessCount()}");
                Debug.Log(
                    "[DesktopMascotCopyResourceDiagnostics] Recorded means command-list recording only; GPU completion and destination contents are unverified.");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
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
                $"[DesktopMascotCopyResourceDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
        }

        private void Cleanup()
        {
            if (sourceTexture != null)
            {
                sourceTexture.Release();
                Destroy(sourceTexture);
                sourceTexture = null;
            }

            Destroy(gameObject);
        }
#endif
    }
}
