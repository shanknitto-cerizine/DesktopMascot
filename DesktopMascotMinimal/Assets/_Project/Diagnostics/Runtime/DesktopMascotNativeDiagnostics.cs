using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotNativeDiagnostics
    {
        private const string PluginName = "DesktopMascotNative";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetPluginApiVersion",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetPluginApiVersion();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsGraphicsInitialized",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsGraphicsInitialized();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetRendererType",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetRendererType();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDeviceEventCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDeviceEventCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsD3D12InterfaceAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12InterfaceAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsD3D12DeviceAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12DeviceAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetD3D12InterfaceVersion",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetD3D12InterfaceVersion();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetD3D12DeviceNodeCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetD3D12DeviceNodeCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsD3D12CommandQueueAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12CommandQueueAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetD3D12CommandQueueType",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetD3D12CommandQueueType();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetD3D12CommandQueueNodeMask",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetD3D12CommandQueueNodeMask();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void VerifyPluginLoad()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var apiVersion = DMN_GetPluginApiVersion();
                Debug.Log($"[DesktopMascotNativeDiagnostics] Native plugin API version: {apiVersion}");

                var graphicsInitialized = DMN_IsGraphicsInitialized() != 0;
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] Graphics initialized: {graphicsInitialized}");

                var rendererType = DMN_GetRendererType();
                Debug.Log($"[DesktopMascotNativeDiagnostics] Renderer type: {rendererType}");

                var deviceEventCount = DMN_GetDeviceEventCount();
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] Device event count: {deviceEventCount}");

                var d3D12InterfaceAvailable = DMN_IsD3D12InterfaceAvailable() != 0;
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 interface available: {d3D12InterfaceAvailable}");

                var d3D12DeviceAvailable = DMN_IsD3D12DeviceAvailable() != 0;
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 device available: {d3D12DeviceAvailable}");

                var d3D12InterfaceVersion = DMN_GetD3D12InterfaceVersion();
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 interface version: {d3D12InterfaceVersion}");

                var d3D12DeviceNodeCount = DMN_GetD3D12DeviceNodeCount();
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 device node count: {d3D12DeviceNodeCount}");

                var d3D12CommandQueueAvailable =
                    DMN_IsD3D12CommandQueueAvailable() != 0;
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 command queue available: {d3D12CommandQueueAvailable}");

                var d3D12CommandQueueType = DMN_GetD3D12CommandQueueType();
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 command queue type: {d3D12CommandQueueType}");

                var d3D12CommandQueueNodeMask =
                    DMN_GetD3D12CommandQueueNodeMask();
                Debug.Log(
                    $"[DesktopMascotNativeDiagnostics] D3D12 command queue node mask: {d3D12CommandQueueNodeMask}");
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError($"[DesktopMascotNative] DLL was not found: {exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError($"[DesktopMascotNative] Entry point was not found: {exception.Message}");
            }
            catch (BadImageFormatException exception)
            {
                Debug.LogError($"[DesktopMascotNative] DLL architecture or format is invalid: {exception.Message}");
            }
#endif
        }
    }
}
