using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotDestinationTextureDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int DestinationTextureCreateEventId = 3;
        private const int CreationNotAttempted = 0x7FFFFFFF;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsGraphicsInitialized",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsGraphicsInitialized();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsD3D12DeviceAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12DeviceAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetRenderEventFunc",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventFunc();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_IsDestinationTextureAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsDestinationTextureAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureCreationResult",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDestinationTextureCreationResult();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureDimension",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDestinationTextureDimension();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureWidth",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetDestinationTextureWidth();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureHeight",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureHeight();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureDepthOrArraySize",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureDepthOrArraySize();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureMipLevels",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureMipLevels();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureFormat",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDestinationTextureFormat();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureSampleCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureSampleCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureSampleQuality",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureSampleQuality();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureLayout",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDestinationTextureLayout();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureFlags",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetDestinationTextureFlags();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetDestinationTextureInitialState",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetDestinationTextureInitialState();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureDiagnosticEventCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureDiagnosticEventCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureDimension",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureDimension();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureWidth",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetTextureWidth();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureHeight",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureHeight();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureDepthOrArraySize",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureDepthOrArraySize();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureMipLevels",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureMipLevels();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureFormat",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureFormat();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureSampleCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureSampleCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureSampleQuality",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureSampleQuality();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(nameof(DesktopMascotDestinationTextureDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotDestinationTextureDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            var graphicsReady = false;
            for (var frame = 0; frame < 120; frame++)
            {
                if (!TryReadGraphicsReady(out graphicsReady))
                {
                    Destroy(gameObject);
                    yield break;
                }

                if (graphicsReady)
                {
                    break;
                }

                yield return null;
            }

            if (!graphicsReady || !TryIssueCreateEvent())
            {
                Debug.LogError(
                    "[DesktopMascotDestinationTextureDiagnostics] Graphics or D3D12 device did not become ready.");
                Destroy(gameObject);
                yield break;
            }

            yield return null;
            yield return null;
            yield return null;

            // The source diagnostic is independent and runs once at startup.
            // Wait briefly for both read-only result sets before comparing them.
            for (var frame = 0; frame < 60; frame++)
            {
                if (DMN_IsDestinationTextureAvailable() != 0
                    && DMN_GetTextureDiagnosticEventCount() > 0)
                {
                    break;
                }

                yield return null;
            }

            LogResults();
            Destroy(gameObject);
        }

        private static bool TryReadGraphicsReady(out bool ready)
        {
            ready = false;

            try
            {
                ready =
                    DMN_IsGraphicsInitialized() != 0
                    && DMN_IsD3D12DeviceAvailable() != 0;
                return true;
            }
            catch (DllNotFoundException exception)
            {
                LogInteropError("DLL was not found", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                LogInteropError("Entry point was not found", exception);
            }
            catch (BadImageFormatException exception)
            {
                LogInteropError("DLL architecture or format is invalid", exception);
            }

            return false;
        }

        private static bool TryIssueCreateEvent()
        {
            try
            {
                var renderEventFunction = DMN_GetRenderEventFunc();
                if (renderEventFunction == IntPtr.Zero)
                {
                    Debug.LogError(
                        "[DesktopMascotDestinationTextureDiagnostics] Render event function pointer is null.");
                    return false;
                }

                GL.IssuePluginEvent(
                    renderEventFunction,
                    DestinationTextureCreateEventId);
                return true;
            }
            catch (DllNotFoundException exception)
            {
                LogInteropError("DLL was not found", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                LogInteropError("Entry point was not found", exception);
            }
            catch (BadImageFormatException exception)
            {
                LogInteropError("DLL architecture or format is invalid", exception);
            }

            return false;
        }

        private static void LogResults()
        {
            try
            {
                var available = DMN_IsDestinationTextureAvailable() != 0;
                var creationResult =
                    DMN_GetDestinationTextureCreationResult();
                var destinationDimension =
                    DMN_GetDestinationTextureDimension();
                var destinationWidth = DMN_GetDestinationTextureWidth();
                var destinationHeight = DMN_GetDestinationTextureHeight();
                var destinationDepth =
                    DMN_GetDestinationTextureDepthOrArraySize();
                var destinationMipLevels =
                    DMN_GetDestinationTextureMipLevels();
                var destinationFormat = DMN_GetDestinationTextureFormat();
                var destinationSampleCount =
                    DMN_GetDestinationTextureSampleCount();
                var destinationSampleQuality =
                    DMN_GetDestinationTextureSampleQuality();

                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Destination texture available: {available}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Creation HRESULT: {FormatCreationResult(creationResult)}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Dimension: {destinationDimension}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Width: {destinationWidth}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Height: {destinationHeight}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Depth or array size: {destinationDepth}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Mip levels: {destinationMipLevels}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] DXGI format: {destinationFormat} ({GetDxgiFormatName(destinationFormat)})");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Sample count: {destinationSampleCount}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Sample quality: {destinationSampleQuality}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Layout: {DMN_GetDestinationTextureLayout()}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Flags: {DMN_GetDestinationTextureFlags()}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Initial state: {DMN_GetDestinationTextureInitialState()}");

                var dimensionsCompatible =
                    DMN_GetTextureDimension() == destinationDimension
                    && DMN_GetTextureWidth() == destinationWidth
                    && DMN_GetTextureHeight() == destinationHeight
                    && DMN_GetTextureDepthOrArraySize() == destinationDepth
                    && DMN_GetTextureMipLevels() == destinationMipLevels;
                var sampleConfigurationCompatible =
                    DMN_GetTextureSampleCount() == destinationSampleCount
                    && DMN_GetTextureSampleQuality()
                    == destinationSampleQuality;
                var formatCompatible =
                    DMN_GetTextureFormat() == destinationFormat;

                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Copy dimensions compatible: {dimensionsCompatible}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Copy sample configuration compatible: {sampleConfigurationCompatible}");
                Debug.Log(
                    $"[DesktopMascotDestinationTextureDiagnostics] Copy format compatible: {formatCompatible}");
            }
            catch (DllNotFoundException exception)
            {
                LogInteropError("DLL was not found", exception);
            }
            catch (EntryPointNotFoundException exception)
            {
                LogInteropError("Entry point was not found", exception);
            }
            catch (BadImageFormatException exception)
            {
                LogInteropError("DLL architecture or format is invalid", exception);
            }
        }

        private static string FormatCreationResult(int result)
        {
            return result == CreationNotAttempted
                ? "NOT_ATTEMPTED (0x7FFFFFFF)"
                : $"0x{unchecked((uint)result):X8}";
        }

        private static string GetDxgiFormatName(int format)
        {
            switch (format)
            {
                case 91:
                    return "DXGI_FORMAT_B8G8R8A8_UNORM_SRGB";
                default:
                    return $"UNKNOWN({format})";
            }
        }

        private static void LogInteropError(
            string description,
            Exception exception)
        {
            Debug.LogError(
                $"[DesktopMascotDestinationTextureDiagnostics] {description}: {exception.Message}");
        }
#endif
    }
}
