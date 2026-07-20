using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotRenderEventDiagnostics : MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int DiagnosticRenderEventId = 1;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetRenderEventFunc",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventFunc();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetRenderEventCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetRenderEventCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetLastRenderEventId",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetLastRenderEventId();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetLastRenderThreadId",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetLastRenderThreadId();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasGraphicsReadyDuringLastRenderEvent",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasGraphicsReadyDuringLastRenderEvent();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasD3D12ReadyDuringLastRenderEvent",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasD3D12ReadyDuringLastRenderEvent();

        [DllImport(
            "kernel32",
            EntryPoint = "GetCurrentThreadId",
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi)]
        private static extern uint GetCurrentThreadId();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(nameof(DesktopMascotRenderEventDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<DesktopMascotRenderEventDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!TryIssueDiagnosticRenderEvent())
            {
                Destroy(gameObject);
                yield break;
            }

            yield return null;
            yield return null;

            LogDiagnosticResults();
            Destroy(gameObject);
        }

        private static bool TryIssueDiagnosticRenderEvent()
        {
            try
            {
                var mainThreadId = GetCurrentThreadId();
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] Main thread ID: {mainThreadId}");

                var renderEventFunc = DMN_GetRenderEventFunc();
                if (renderEventFunc == IntPtr.Zero)
                {
                    Debug.LogError(
                        "[DesktopMascotRenderEventDiagnostics] Render event function pointer is null.");
                    return false;
                }

                GL.IssuePluginEvent(renderEventFunc, DiagnosticRenderEventId);
                return true;
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] DLL was not found: {exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] Entry point was not found: {exception.Message}");
            }
            catch (BadImageFormatException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] DLL architecture or format is invalid: {exception.Message}");
            }

            return false;
        }

        private static void LogDiagnosticResults()
        {
            try
            {
                var renderEventCount = DMN_GetRenderEventCount();
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] Render event count: {renderEventCount}");

                var lastRenderEventId = DMN_GetLastRenderEventId();
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] Last render event ID: {lastRenderEventId}");

                var lastRenderThreadId = DMN_GetLastRenderThreadId();
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] Last render thread ID: {lastRenderThreadId}");

                var graphicsReady =
                    DMN_WasGraphicsReadyDuringLastRenderEvent() != 0;
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] Graphics ready during render event: {graphicsReady}");

                var d3D12Ready =
                    DMN_WasD3D12ReadyDuringLastRenderEvent() != 0;
                Debug.Log(
                    $"[DesktopMascotRenderEventDiagnostics] D3D12 ready during render event: {d3D12Ready}");
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] DLL was not found: {exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] Entry point was not found: {exception.Message}");
            }
            catch (BadImageFormatException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotRenderEventDiagnostics] DLL architecture or format is invalid: {exception.Message}");
            }
        }
#endif
    }
}
