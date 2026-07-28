using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotTextureDiagnostics : MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int TextureDiagnosticEventId = 2;
        private const int TextureSize = 256;

        private RenderTexture diagnosticTexture;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetRenderEventAndDataFunc",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventAndDataFunc();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureDiagnosticEventCount",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureDiagnosticEventCount();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasTextureDataNonNull",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasTextureDataNonNull();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasTextureResourceDescriptionAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasTextureResourceDescriptionAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasCommandRecordingStateAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCommandRecordingStateAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_WasCommandListAvailable",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCommandListAvailable();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureDimension",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureDimension();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureAlignment",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetTextureAlignment();

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

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureLayout",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetTextureLayout();

        [DllImport(
            PluginName,
            EntryPoint = "DMN_GetTextureFlags",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetTextureFlags();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(nameof(DesktopMascotTextureDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<DesktopMascotTextureDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!TryCreateTextureAndIssueEvent())
            {
                Cleanup();
                yield break;
            }

            yield return null;
            yield return null;
            yield return null;

            LogDiagnosticResults();
            Cleanup();
        }

        private bool TryCreateTextureAndIssueEvent()
        {
            CommandBuffer commandBuffer = null;

            try
            {
                const GraphicsFormat requestedFormat =
                    GraphicsFormat.B8G8R8A8_SRGB;
                var formatSupported = SystemInfo.IsFormatSupported(
                    requestedFormat,
                    GraphicsFormatUsage.Render);

                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Requested graphics format: {requestedFormat}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Format supported: {formatSupported}");

                if (!formatSupported)
                {
                    Debug.LogError(
                        "[DesktopMascotTextureDiagnostics] Required graphics format is not supported; no fallback was selected.");
                    return false;
                }

                var descriptor = new RenderTextureDescriptor(
                    TextureSize,
                    TextureSize)
                {
                    graphicsFormat = requestedFormat,
                    depthStencilFormat = GraphicsFormat.None,
                    msaaSamples = 1,
                    volumeDepth = 1,
                    dimension = TextureDimension.Tex2D,
                    mipCount = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = false
                };

                diagnosticTexture = new RenderTexture(descriptor)
                {
                    name = nameof(DesktopMascotTextureDiagnostics)
                };
                diagnosticTexture.Create();

                var renderTextureCreated = diagnosticTexture.IsCreated();
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Actual graphics format: {diagnosticTexture.graphicsFormat}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] RenderTexture created: {renderTextureCreated}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] RenderTexture sRGB: {diagnosticTexture.sRGB}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] RenderTexture width: {diagnosticTexture.width}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] RenderTexture height: {diagnosticTexture.height}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] RenderTexture antiAliasing: {diagnosticTexture.antiAliasing}");

                if (!renderTextureCreated)
                {
                    return false;
                }

                // This is the only GetNativeTexturePtr call for this texture.
                var nativeTexturePointer =
                    diagnosticTexture.GetNativeTexturePtr();
                var nativeTexturePointerNonZero =
                    nativeTexturePointer != IntPtr.Zero;
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Native texture pointer non-zero: {nativeTexturePointerNonZero}");

                if (!nativeTexturePointerNonZero)
                {
                    return false;
                }

                var renderEventFunction = DMN_GetRenderEventAndDataFunc();
                if (renderEventFunction == IntPtr.Zero)
                {
                    Debug.LogError(
                        "[DesktopMascotTextureDiagnostics] Render event-and-data function pointer is null.");
                    return false;
                }

                commandBuffer = new CommandBuffer
                {
                    name = "Desktop Mascot Texture Diagnostic"
                };
                commandBuffer.IssuePluginEventAndData(
                    renderEventFunction,
                    TextureDiagnosticEventId,
                    nativeTexturePointer);

                // The command buffer is submitted once to Unity's render command
                // stream. The RenderTexture remains strongly referenced until the
                // callback results are read three frames later.
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return true;
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] DLL was not found: {exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] Entry point was not found: {exception.Message}");
            }
            catch (BadImageFormatException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] DLL architecture or format is invalid: {exception.Message}");
            }
            finally
            {
                commandBuffer?.Release();
            }

            return false;
        }

        private static void LogDiagnosticResults()
        {
            try
            {
                var eventCount = DMN_GetTextureDiagnosticEventCount();
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Texture diagnostic event count: {eventCount}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Texture data non-null: {DMN_WasTextureDataNonNull() != 0}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Resource description available: {DMN_WasTextureResourceDescriptionAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Command recording state available: {DMN_WasCommandRecordingStateAvailable() != 0}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Command list available: {DMN_WasCommandListAvailable() != 0}");

                var format = DMN_GetTextureFormat();
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Dimension: {DMN_GetTextureDimension()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Alignment: {DMN_GetTextureAlignment()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Width: {DMN_GetTextureWidth()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Height: {DMN_GetTextureHeight()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Depth or array size: {DMN_GetTextureDepthOrArraySize()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Mip levels: {DMN_GetTextureMipLevels()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] DXGI format: {format} ({GetDxgiFormatName(format)})");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Sample count: {DMN_GetTextureSampleCount()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Sample quality: {DMN_GetTextureSampleQuality()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Layout: {DMN_GetTextureLayout()}");
                Debug.Log(
                    $"[DesktopMascotTextureDiagnostics] Flags: {DMN_GetTextureFlags()}");
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] DLL was not found: {exception.Message}");
            }
            catch (EntryPointNotFoundException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] Entry point was not found: {exception.Message}");
            }
            catch (BadImageFormatException exception)
            {
                Debug.LogError(
                    $"[DesktopMascotTextureDiagnostics] DLL architecture or format is invalid: {exception.Message}");
            }
        }

        private static string GetDxgiFormatName(int format)
        {
            switch (format)
            {
                case 0:
                    return "DXGI_FORMAT_UNKNOWN";
                case 28:
                    return "DXGI_FORMAT_R8G8B8A8_UNORM";
                case 29:
                    return "DXGI_FORMAT_R8G8B8A8_UNORM_SRGB";
                case 87:
                    return "DXGI_FORMAT_B8G8R8A8_UNORM";
                case 91:
                    return "DXGI_FORMAT_B8G8R8A8_UNORM_SRGB";
                default:
                    return $"UNKNOWN({format})";
            }
        }

        private void Cleanup()
        {
            if (diagnosticTexture != null)
            {
                diagnosticTexture.Release();
                Destroy(diagnosticTexture);
                diagnosticTexture = null;
            }

            Destroy(gameObject);
        }
#endif
    }
}
