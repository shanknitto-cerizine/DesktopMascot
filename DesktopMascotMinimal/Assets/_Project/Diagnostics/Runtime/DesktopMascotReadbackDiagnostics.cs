using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotReadbackDiagnostics : MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int CopyResourceEventId = 4;
        private const int ReadbackCopyEventId = 5;
        private const int FenceSignalEventId = 6;
        private const int TextureSize = 256;

        private RenderTexture sourceTexture;
        private Texture2D patternTexture;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsGraphicsInitialized();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsD3D12DeviceAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventFunc();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventAndDataFunc();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasCopyResourceRecorded();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCopyFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsReadbackBufferAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetReadbackBufferCreationResult();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetReadbackFootprintOffset();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackFootprintRowPitch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackNumRows();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetReadbackRowSizeInBytes();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetReadbackTotalBytes();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetReadbackCopyEventCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackCopyCallbackReached();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackCopyRecorded();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsReadbackFenceAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackFenceSignalAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidReadbackFenceSignalSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetReadbackFenceSignalHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetReadbackFenceSubmittedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetReadbackFenceCompletedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsReadbackFenceComplete();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_ValidateReadbackPixels();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackMapAttempted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidReadbackMapSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidReadbackVerticallyFlippedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetReadbackFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackTopLeftBGRA();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackTopRightBGRA();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackBottomLeftBGRA();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetReadbackBottomRightBGRA();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(nameof(DesktopMascotReadbackDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<DesktopMascotReadbackDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            var ready = false;
            for (var frame = 0; frame < 120; frame++)
            {
                if (!TryReadPrerequisites(out ready))
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

            if (!ready)
            {
                Debug.LogError(
                    "[DesktopMascotReadbackDiagnostics] D3D12 prerequisites did not become ready.");
                Cleanup();
                yield break;
            }

            if (!CreatePatternAndIssueSourceCopy())
            {
                Cleanup();
                yield break;
            }

            // Frame N+1 or later: event 4's callback must have completed.
            yield return null;
            for (var frame = 0;
                 frame < 120 && DMN_WasCopyResourceRecorded() == 0;
                 frame++)
            {
                yield return null;
            }
            if (DMN_WasCopyResourceRecorded() == 0)
            {
                Debug.LogError(
                    $"[DesktopMascotReadbackDiagnostics] Source copy failed; stage={DMN_GetCopyFailureStage()}.");
                Cleanup();
                yield break;
            }

            if (!IssueSimpleEvent(ReadbackCopyEventId, "Readback Copy"))
            {
                Cleanup();
                yield break;
            }

            // At least one whole frame separates event 5 and event 6.
            yield return null;
            for (var frame = 0;
                 frame < 120 && DMN_WasReadbackCopyRecorded() == 0;
                 frame++)
            {
                yield return null;
            }
            if (DMN_WasReadbackCopyRecorded() == 0
                || !IssueSimpleEvent(FenceSignalEventId, "Fence Signal"))
            {
                Cleanup();
                yield break;
            }

            var completed = false;
            for (var frame = 0; frame < 300; frame++)
            {
                if (DMN_IsReadbackFenceComplete() != 0)
                {
                    completed = true;
                    break;
                }
                yield return null;
            }

            if (completed)
            {
                DMN_ValidateReadbackPixels();
            }
            LogResults();
            Cleanup();
        }

        private static bool TryReadPrerequisites(out bool ready)
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
                Debug.LogError(
                    $"[DesktopMascotReadbackDiagnostics] Native interop failed: {exception.Message}");
                return false;
            }
        }

        private bool CreatePatternAndIssueSourceCopy()
        {
            CommandBuffer commandBuffer = null;
            try
            {
                const GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB;
                if (!SystemInfo.IsFormatSupported(
                        format,
                        GraphicsFormatUsage.Render))
                {
                    return false;
                }

                patternTexture = new Texture2D(
                    TextureSize,
                    TextureSize,
                    TextureFormat.RGBA32,
                    false,
                    true);
                var pixels = new Color32[TextureSize * TextureSize];
                for (var y = 0; y < TextureSize; y++)
                {
                    for (var x = 0; x < TextureSize; x++)
                    {
                        pixels[y * TextureSize + x] =
                            y < 32
                                ? (x < 32 ? Color.red : Color.green)
                                : (x < 32 ? Color.blue : Color.white);
                    }
                }
                patternTexture.SetPixels32(pixels);
                patternTexture.Apply(false, true);

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
                    autoGenerateMips = false
                };
                sourceTexture = new RenderTexture(descriptor);
                sourceTexture.Create();
                var sourcePointer = sourceTexture.GetNativeTexturePtr();
                var callback = DMN_GetRenderEventAndDataFunc();
                if (!sourceTexture.IsCreated()
                    || sourcePointer == IntPtr.Zero
                    || callback == IntPtr.Zero)
                {
                    return false;
                }

                commandBuffer = new CommandBuffer
                {
                    name = "Desktop Mascot Fixed Pattern and Source Copy"
                };
                commandBuffer.Blit(patternTexture, sourceTexture);
                commandBuffer.IssuePluginEventAndData(
                    callback,
                    CopyResourceEventId,
                    sourcePointer);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Debug.LogError(
                    $"[DesktopMascotReadbackDiagnostics] Native interop failed: {exception.Message}");
                return false;
            }
            finally
            {
                commandBuffer?.Release();
            }
        }

        private static bool IssueSimpleEvent(int eventId, string name)
        {
            CommandBuffer commandBuffer = null;
            try
            {
                var callback = DMN_GetRenderEventFunc();
                if (callback == IntPtr.Zero)
                {
                    return false;
                }
                commandBuffer = new CommandBuffer
                {
                    name = $"Desktop Mascot {name}"
                };
                commandBuffer.IssuePluginEvent(callback, eventId);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Debug.LogError(
                    $"[DesktopMascotReadbackDiagnostics] Native interop failed: {exception.Message}");
                return false;
            }
            finally
            {
                commandBuffer?.Release();
            }
        }

        private static void LogResults()
        {
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Readback buffer available: {DMN_IsReadbackBufferAvailable() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Creation HRESULT: 0x{unchecked((uint)DMN_GetReadbackBufferCreationResult()):X8}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Footprint offset: {DMN_GetReadbackFootprintOffset()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Footprint row pitch: {DMN_GetReadbackFootprintRowPitch()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Number of rows: {DMN_GetReadbackNumRows()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Row size in bytes: {DMN_GetReadbackRowSizeInBytes()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Total bytes: {DMN_GetReadbackTotalBytes()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Readback copy event count: {DMN_GetReadbackCopyEventCount()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Readback copy callback reached: {DMN_WasReadbackCopyCallbackReached() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] CopyTextureRegion recorded: {DMN_WasReadbackCopyRecorded() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence available: {DMN_IsReadbackFenceAvailable() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence signal attempted: {DMN_WasReadbackFenceSignalAttempted() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence signal succeeded: {DMN_DidReadbackFenceSignalSucceed() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence signal HRESULT: 0x{unchecked((uint)DMN_GetReadbackFenceSignalHRESULT()):X8}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence submitted value: {DMN_GetReadbackFenceSubmittedValue()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence completed value: {DMN_GetReadbackFenceCompletedValue()}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Fence complete: {DMN_IsReadbackFenceComplete() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Map attempted: {DMN_WasReadbackMapAttempted() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Map succeeded: {DMN_DidReadbackMapSucceed() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Validation completed: {DMN_WasReadbackValidationCompleted() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Expected orientation match: {DMN_DidReadbackExpectedOrientationMatch() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Vertically flipped orientation match: {DMN_DidReadbackVerticallyFlippedOrientationMatch() != 0}");
            Debug.Log($"[DesktopMascotReadbackDiagnostics] Failure stage: {DMN_GetReadbackFailureStage()}");
            LogPixel("Top-left", DMN_GetReadbackTopLeftBGRA());
            LogPixel("Top-right", DMN_GetReadbackTopRightBGRA());
            LogPixel("Bottom-left", DMN_GetReadbackBottomLeftBGRA());
            LogPixel("Bottom-right", DMN_GetReadbackBottomRightBGRA());
        }

        private static void LogPixel(string name, uint value)
        {
            Debug.Log(
                $"[DesktopMascotReadbackDiagnostics] {name} BGRA: 0x{value:X8} B={value & 0xFF} G={(value >> 8) & 0xFF} R={(value >> 16) & 0xFF} A={(value >> 24) & 0xFF}");
        }

        private static bool IsInteropException(Exception exception)
        {
            return exception is DllNotFoundException
                || exception is EntryPointNotFoundException
                || exception is BadImageFormatException;
        }

        private void Cleanup()
        {
            if (sourceTexture != null)
            {
                sourceTexture.Release();
                Destroy(sourceTexture);
            }
            if (patternTexture != null)
            {
                Destroy(patternTexture);
            }
            Destroy(gameObject);
        }
#endif
    }
}
