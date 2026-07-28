using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionInitializationDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int MessageLoopRunning = 14;
        private const int Failed = 15;
        private const int Stopped = 17;
        private bool shutdownRequested;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsD3D12DeviceAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsD3D12CommandQueueAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionUiThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionComInitializationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionComInitializationSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionComInitializationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionWindowClassRegistrationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionWindowClassRegistrationSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowClassLastError();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionWindowCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionWindowAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowCreationLastError();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowClientWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowClientHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDxgiFactoryCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDxgiFactoryAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDxgiFactoryCreationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompDeviceCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDCompDeviceAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompDeviceCreationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompTargetCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDCompTargetAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompTargetCreationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompVisualCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDCompVisualAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompVisualCreationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionSwapChainCreationAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionSwapChainAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainCreationHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainFormat();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainBufferCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainSwapEffect();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainAlphaMode();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionSwapChainScaling();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompSetContentAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidDCompSetContentSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompSetContentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompSetRootAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidDCompSetRootSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompSetRootHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasDCompCommitAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidDCompCommitSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompCommitHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetDCompCommitCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasCompositionWindowShown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionMessageLoopRunning();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionInitializationDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionInitializationDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            var ready = false;
            for (var frame = 0; frame < 180; frame++)
            {
                if (!TryReadReady(out ready))
                {
                    yield break;
                }
                if (ready)
                {
                    break;
                }
                yield return null;
            }
            if (!ready)
            {
                Debug.LogError(
                    "[DesktopMascotCompositionDiagnostics] D3D12 prerequisites did not become ready.");
                yield break;
            }

            if (!TryStart(out var startResult))
            {
                yield break;
            }
            if (startResult != 1)
            {
                yield break;
            }

            var state = 0;
            for (var frame = 0; frame < 300; frame++)
            {
                state = DMN_GetCompositionInitializationState();
                if (state == MessageLoopRunning || state == Failed
                    || state == Stopped)
                {
                    break;
                }
                yield return null;
            }
            LogInitializationResults();

            // CompositionPresentDiagnostics owns the normal diagnostic
            // shutdown after its one-shot Present and five-second hold.
            for (var frame = 0; frame < 1800; frame++)
            {
                state = DMN_GetCompositionInitializationState();
                if (state == Stopped || state == Failed)
                {
                    break;
                }
                yield return null;
            }
            Debug.Log(
                $"[DesktopMascotCompositionDiagnostics] Final state: {StateName(state)}");
            Debug.Log(
                $"[DesktopMascotCompositionDiagnostics] Message loop running: {DMN_IsCompositionMessageLoopRunning() != 0}");
            Destroy(gameObject);
        }

        private static bool TryReadReady(out bool ready)
        {
            ready = false;
            try
            {
                ready =
                    DMN_IsD3D12DeviceAvailable() != 0
                    && DMN_IsD3D12CommandQueueAvailable() != 0;
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
        }

        private static bool TryStart(out int result)
        {
            result = 0;
            try
            {
                result = DMN_StartCompositionDiagnostics();
                Debug.Log(
                    $"[DesktopMascotCompositionDiagnostics] Start result: {result}");
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
        }

        private void OnApplicationQuit()
        {
            RequestShutdown();
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

        private static void LogInitializationResults()
        {
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Initialization state: {DMN_GetCompositionInitializationState()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Failure stage: {DMN_GetCompositionFailureStage()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] UI thread ID: {DMN_GetCompositionUiThreadId()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] COM initialization attempted: {B(DMN_WasCompositionComInitializationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] COM initialization succeeded: {B(DMN_DidCompositionComInitializationSucceed())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] COM HRESULT: {H(DMN_GetCompositionComInitializationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window class registration attempted: {B(DMN_WasCompositionWindowClassRegistrationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window class registration succeeded: {B(DMN_DidCompositionWindowClassRegistrationSucceed())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window class LastError: {DMN_GetCompositionWindowClassLastError()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window creation attempted: {B(DMN_WasCompositionWindowCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window available: {B(DMN_IsCompositionWindowAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window creation LastError: {DMN_GetCompositionWindowCreationLastError()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Client width: {DMN_GetCompositionWindowClientWidth()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Client height: {DMN_GetCompositionWindowClientHeight()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DXGI factory creation attempted: {B(DMN_WasDxgiFactoryCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DXGI factory available: {B(DMN_IsDxgiFactoryAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DXGI factory HRESULT: {H(DMN_GetDxgiFactoryCreationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp device creation attempted: {B(DMN_WasDCompDeviceCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp device available: {B(DMN_IsDCompDeviceAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp device HRESULT: {H(DMN_GetDCompDeviceCreationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp target creation attempted: {B(DMN_WasDCompTargetCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp target available: {B(DMN_IsDCompTargetAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp target HRESULT: {H(DMN_GetDCompTargetCreationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp visual creation attempted: {B(DMN_WasDCompVisualCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp visual available: {B(DMN_IsDCompVisualAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] DComp visual HRESULT: {H(DMN_GetDCompVisualCreationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Composition swap chain creation attempted: {B(DMN_WasCompositionSwapChainCreationAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Composition swap chain available: {B(DMN_IsCompositionSwapChainAvailable())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Swap-chain HRESULT: {H(DMN_GetCompositionSwapChainCreationHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Width: {DMN_GetCompositionSwapChainWidth()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Height: {DMN_GetCompositionSwapChainHeight()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Format: {DMN_GetCompositionSwapChainFormat()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Buffer count: {DMN_GetCompositionSwapChainBufferCount()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Swap effect: {DMN_GetCompositionSwapChainSwapEffect()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Alpha mode: {DMN_GetCompositionSwapChainAlphaMode()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Scaling: {DMN_GetCompositionSwapChainScaling()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetContent attempted: {B(DMN_WasDCompSetContentAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetContent succeeded: {B(DMN_DidDCompSetContentSucceed())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetContent HRESULT: {H(DMN_GetDCompSetContentHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetRoot attempted: {B(DMN_WasDCompSetRootAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetRoot succeeded: {B(DMN_DidDCompSetRootSucceed())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] SetRoot HRESULT: {H(DMN_GetDCompSetRootHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Commit attempted: {B(DMN_WasDCompCommitAttempted())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Commit succeeded: {B(DMN_DidDCompCommitSucceed())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Commit HRESULT: {H(DMN_GetDCompCommitHRESULT())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Commit count: {DMN_GetDCompCommitCount()}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Window shown: {B(DMN_WasCompositionWindowShown())}");
            Debug.Log($"[DesktopMascotCompositionDiagnostics] Message loop running: {B(DMN_IsCompositionMessageLoopRunning())}");
        }

        private static bool B(int value) => value != 0;
        private static string H(int value) =>
            $"0x{unchecked((uint)value):X8}";
        private static string StateName(int state) =>
            state == Stopped ? "Stopped"
            : state == Failed ? "Failed"
            : state.ToString();
        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;
        private static void LogInteropError(Exception exception) =>
            Debug.LogError(
                $"[DesktopMascotCompositionDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
#endif
    }
}
