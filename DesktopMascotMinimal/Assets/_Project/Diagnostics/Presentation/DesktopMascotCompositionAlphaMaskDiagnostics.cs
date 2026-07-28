using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionAlphaMaskDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Width = 64;
        private const int Height = 64;
        private const int Stride = 64;
        private const int ByteCount = Stride * Height;
        private const int Threshold = 128;
        private const int TargetReadbacks = 20;
        private const float RequestIntervalSeconds = 0.25f;
        private const float RequestTimeoutSeconds = 2.0f;
        private const float OverallTimeoutSeconds = 20.0f;
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionFailed = 15;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int AlphaCompleted = 6;

        private readonly byte[] alphaMask = new byte[ByteCount];
        private Action<AsyncGPUReadbackRequest> readbackCallback;
        private GCHandle alphaMaskHandle;
        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool shutdownRequested;
        private bool requestPending;
        private bool orientationKnown;
        private bool flipRows;
        private float requestStartedAt;
        private int requestedReadbacks;
        private int completedReadbacks;
        private int readbackErrors;
        private int managedFailureStage;
        private ulong generation;
        private uint readbackCallbackThreadId;
        private bool externalLifecycleOwner;
        internal static bool ExternalLifecycleOwnerForNextRun { get; set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionUiThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionRequestedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionRecordedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionSubmittedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionFenceCompletedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionBackBuffer0UseCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionBackBuffer1UseCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionInvalidBackBufferIndexCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionElapsedMilliseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionMinimumFrameMilliseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionMaximumFrameMilliseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionAverageFrameMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionDroppedScheduleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionRejectedFrameRequestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidContinuousCompositionTimeout();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionAlphaMaskDiagnostics(int width, int height, int stride, int alphaThreshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_SubmitCompositionAlphaMask(IntPtr data, int width, int height, int stride, ulong generation);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionAlphaMaskSubmittedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionAlphaMaskAcceptedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionAlphaMaskRejectedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionAlphaMaskPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskStride();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskByteCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskSampleAlpha(int x, int y);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskSampleHit(int x, int y);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionAlphaMaskAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionAlphaMaskRequestPending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionAlphaMaskLastSubmitSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionAlphaMaskLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionAlphaMaskLastSubmitThreadId();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionAlphaMaskDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionAlphaMaskDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            externalLifecycleOwner = ExternalLifecycleOwnerForNextRun;
            ExternalLifecycleOwnerForNextRun = false;
            readbackCallback = OnReadbackCompleted;
            if (!externalLifecycleOwner)
            {
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                Debug.Log(
                    $"[DesktopMascotAlphaMaskDiagnostics] Previous runInBackground: {previousRunInBackground}");
                Debug.Log(
                    $"[DesktopMascotAlphaMaskDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            var startedAt = Time.realtimeSinceStartup;
            while (!TimedOut(startedAt) && !PrerequisitesReady())
            {
                if (DMN_GetCompositionInitializationState()
                    == CompositionFailed)
                {
                    yield return FinishFailed();
                    yield break;
                }
                yield return null;
            }
            if (TimedOut(startedAt))
            {
                managedFailureStage = 1;
                yield return FinishFailed();
                yield break;
            }

            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] AsyncGPUReadback supported: {SystemInfo.supportsAsyncGPUReadback}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Readback source format: {GraphicsFormat.B8G8R8A8_SRGB}");
            Debug.Log(
                "[DesktopMascotAlphaMaskDiagnostics] Readback destination format: Native BGRA8 bytes (no format conversion; alpha byte offset 3)");
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                managedFailureStage = 21;
                yield return FinishFailed();
                yield break;
            }

            DesktopMascotContinuousCompositionDiagnostics.
                TargetFpsForNextRun = 30;
            DesktopMascotContinuousCompositionDiagnostics.
                PatternBorderMode = 0;
            DesktopMascotContinuousCompositionDiagnostics.
                AlphaMaskPatternForNextRun = true;
            DesktopMascotContinuousCompositionDiagnostics.
                ExternalShutdownOwnerForNextRun = true;
            var continuousObject = new GameObject(
                nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuousObject);
            continuousObject.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();

            RenderTexture source = null;
            while (!TimedOut(startedAt))
            {
                source = DesktopMascotContinuousCompositionDiagnostics.
                    ActiveSourceTexture;
                if (source != null
                    && source.IsCreated()
                    && DMN_GetContinuousCompositionPresentCount() > 0)
                {
                    break;
                }
                if (DMN_GetContinuousCompositionState() == ContinuousFailed)
                {
                    managedFailureStage = 24;
                    yield return FinishFailed();
                    yield break;
                }
                yield return null;
            }
            if (source == null || TimedOut(startedAt))
            {
                managedFailureStage = 24;
                yield return FinishFailed();
                yield break;
            }

            int startResult;
            var interopFailed = false;
            try
            {
                startResult = DMN_StartCompositionAlphaMaskDiagnostics(
                    Width,
                    Height,
                    Stride,
                    Threshold);
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                startResult = 0;
                interopFailed = true;
            }
            if (interopFailed)
            {
                yield return FinishFailed();
                yield break;
            }
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Start result: {startResult}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Width: {DMN_GetCompositionAlphaMaskWidth()}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Height: {DMN_GetCompositionAlphaMaskHeight()}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Stride: {DMN_GetCompositionAlphaMaskStride()}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Byte count: {DMN_GetCompositionAlphaMaskByteCount()}");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Threshold: {DMN_GetCompositionAlphaMaskThreshold()}");
            Debug.Log(
                "[DesktopMascotAlphaMaskDiagnostics] Readback interval milliseconds: 250");
            Debug.Log(
                "[DesktopMascotAlphaMaskDiagnostics] Maximum outstanding requests: 1");
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Unity main thread ID: {GetCurrentThreadId()}");
            if (startResult != 1)
            {
                yield return FinishFailed();
                yield break;
            }

            alphaMaskHandle = GCHandle.Alloc(alphaMask, GCHandleType.Pinned);
            var nextRequestAt = Time.realtimeSinceStartup;
            while (completedReadbacks < TargetReadbacks
                   && managedFailureStage == 0
                   && !TimedOut(startedAt))
            {
                if (requestPending
                    && Time.realtimeSinceStartup - requestStartedAt
                        > RequestTimeoutSeconds)
                {
                    managedFailureStage = 23;
                    break;
                }
                if (!requestPending
                    && requestedReadbacks < TargetReadbacks
                    && Time.realtimeSinceStartup >= nextRequestAt)
                {
                    requestPending = true;
                    requestStartedAt = Time.realtimeSinceStartup;
                    requestedReadbacks++;
                    AsyncGPUReadback.Request(source, 0, readbackCallback);
                    nextRequestAt =
                        Time.realtimeSinceStartup + RequestIntervalSeconds;
                }
                yield return null;
            }
            if (TimedOut(startedAt) && managedFailureStage == 0)
            {
                managedFailureStage = 23;
            }
            while (requestPending && !TimedOut(startedAt))
            {
                yield return null;
            }
            if (managedFailureStage != 0)
            {
                yield return FinishFailed();
                yield break;
            }

            while (!TimedOut(startedAt)
                   && DMN_GetCompositionAlphaMaskState() != AlphaCompleted)
            {
                if (DMN_GetCompositionAlphaMaskFailureStage() != 0)
                {
                    break;
                }
                yield return null;
            }

            LogMaskResults();

            if (externalLifecycleOwner)
            {
                shutdownRequested = true;
                ReleasePinnedBuffer();
                Destroy(gameObject);
                yield break;
            }

            while (!TimedOut(startedAt))
            {
                var continuousState = DMN_GetContinuousCompositionState();
                if (continuousState == ContinuousCompleted
                    || continuousState == ContinuousStopped
                    || continuousState == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            LogContinuousResults();

            shutdownRequested = true;
            DMN_StopCompositionAlphaMaskDiagnostics();
            DMN_RequestContinuousCompositionDiagnosticsStop();
            DMN_RequestCompositionDiagnosticsShutdown();
            while (!TimedOut(startedAt)
                   && DMN_GetCompositionInitializationState()
                       != CompositionStopped)
            {
                yield return null;
            }
            var shutdownComplete =
                DMN_GetCompositionInitializationState()
                    == CompositionStopped;
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] Shutdown complete: {shutdownComplete}");
            RestoreRunInBackground();
            ReleasePinnedBuffer();
            Destroy(gameObject);
        }

        private void OnReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            readbackCallbackThreadId = GetCurrentThreadId();
            requestPending = false;
            if (shutdownRequested)
            {
                return;
            }
            if (request.hasError)
            {
                readbackErrors++;
                managedFailureStage = 22;
                return;
            }
            NativeArray<byte> data = request.GetData<byte>();
            if (data.Length != Width * Height * 4)
            {
                readbackErrors++;
                managedFailureStage = 22;
                return;
            }
            if (!orientationKnown)
            {
                var expected = RawSamplesMatch(data, false);
                var flipped = RawSamplesMatch(data, true);
                if (expected == flipped)
                {
                    managedFailureStage = 16;
                    return;
                }
                flipRows = flipped;
                orientationKnown = true;
            }
            for (var y = 0; y < Height; y++)
            {
                var sourceY = flipRows ? Height - 1 - y : y;
                var sourceRow = sourceY * Width * 4;
                var destinationRow = y * Stride;
                for (var x = 0; x < Width; x++)
                {
                    alphaMask[destinationRow + x] =
                        data[sourceRow + x * 4 + 3];
                }
            }
            generation++;
            try
            {
                var result = DMN_SubmitCompositionAlphaMask(
                    alphaMaskHandle.AddrOfPinnedObject(),
                    Width,
                    Height,
                    Stride,
                    generation);
                if (result != 1)
                {
                    managedFailureStage =
                        DMN_GetCompositionAlphaMaskFailureStage();
                    return;
                }
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                managedFailureStage = 12;
                return;
            }
            completedReadbacks++;
            if (completedReadbacks % 5 == 0)
            {
                Debug.Log(
                    $"[DesktopMascotAlphaMaskDiagnostics] Completed mask progress: {completedReadbacks}/{TargetReadbacks}");
            }
        }

        private static bool RawSamplesMatch(
            NativeArray<byte> data,
            bool verticallyFlipped)
        {
            var topY = verticallyFlipped ? 55 : 8;
            var bottomY = verticallyFlipped ? 8 : 55;
            return Near(RawAlpha(data, 8, topY), 0)
                && Near(RawAlpha(data, 55, topY), 64)
                && Near(RawAlpha(data, 8, bottomY), 128)
                && Near(RawAlpha(data, 55, bottomY), 255)
                && Near(RawAlpha(data, 32, 32), 192);
        }

        private static byte RawAlpha(
            NativeArray<byte> data,
            int x,
            int y)
        {
            return data[(y * Width + x) * 4 + 3];
        }

        private static bool Near(int actual, int expected)
        {
            return Math.Abs(actual - expected) <= 2;
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState()
                       == CompositionMessageLoopRunning
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0;
        }

        private static bool TimedOut(float startedAt)
        {
            return Time.realtimeSinceStartup - startedAt
                >= OverallTimeoutSeconds;
        }

        private void LogMaskResults()
        {
            var topLeft = DMN_GetCompositionAlphaMaskSampleAlpha(8, 8);
            var topRight = DMN_GetCompositionAlphaMaskSampleAlpha(55, 8);
            var bottomLeft =
                DMN_GetCompositionAlphaMaskSampleAlpha(8, 55);
            var bottomRight =
                DMN_GetCompositionAlphaMaskSampleAlpha(55, 55);
            var center = DMN_GetCompositionAlphaMaskSampleAlpha(32, 32);
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Top-left alpha: {topLeft}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Top-right alpha: {topRight}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Bottom-left alpha: {bottomLeft}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Bottom-right alpha: {bottomRight}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Center alpha: {center}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Top-left hit: {B(DMN_GetCompositionAlphaMaskSampleHit(8, 8))}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Top-right hit: {B(DMN_GetCompositionAlphaMaskSampleHit(55, 8))}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Bottom-left hit: {B(DMN_GetCompositionAlphaMaskSampleHit(8, 55))}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Bottom-right hit: {B(DMN_GetCompositionAlphaMaskSampleHit(55, 55))}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Center hit: {B(DMN_GetCompositionAlphaMaskSampleHit(32, 32))}");
            var expectedOrientation = Near(topLeft, 0)
                && Near(topRight, 64)
                && Near(bottomLeft, 128)
                && Near(bottomRight, 255)
                && Near(center, 192);
            var flippedOrientation = Near(topLeft, 128)
                && Near(topRight, 255)
                && Near(bottomLeft, 0)
                && Near(bottomRight, 64)
                && Near(center, 192);
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Expected orientation match: {expectedOrientation}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Vertically flipped orientation match: {flippedOrientation}");
            Debug.Log("[DesktopMascotAlphaMaskDiagnostics] Published origin: TopLeft");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] State: {AlphaStateName(DMN_GetCompositionAlphaMaskState())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Failure stage: {DMN_GetCompositionAlphaMaskFailureStage()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Requested readbacks: {requestedReadbacks}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Completed readbacks: {completedReadbacks}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Readback errors: {readbackErrors}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Native submitted masks: {DMN_GetCompositionAlphaMaskSubmittedCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Native accepted masks: {DMN_GetCompositionAlphaMaskAcceptedCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Native rejected masks: {DMN_GetCompositionAlphaMaskRejectedCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Published generation: {DMN_GetCompositionAlphaMaskPublishedGeneration()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Mask available: {B(DMN_IsCompositionAlphaMaskAvailable())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Last submit succeeded: {B(DMN_DidCompositionAlphaMaskLastSubmitSucceed())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Request pending: {B(DMN_IsCompositionAlphaMaskRequestPending())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Last Win32 error: {DMN_GetCompositionAlphaMaskLastWin32Error()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Readback callback thread ID: {readbackCallbackThreadId}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Native submit thread ID: {DMN_GetCompositionAlphaMaskLastSubmitThreadId()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Composition UI thread ID: {DMN_GetCompositionUiThreadId()}");
        }

        private static void LogContinuousResults()
        {
            var elapsed =
                DMN_GetContinuousCompositionElapsedMilliseconds();
            var averageUs =
                DMN_GetContinuousCompositionAverageFrameMicroseconds();
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Continuous state: {DMN_GetContinuousCompositionState()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Continuous failure stage: {DMN_GetContinuousCompositionFailureStage()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Requested frames: {DMN_GetContinuousCompositionRequestedFrameCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Recorded frames: {DMN_GetContinuousCompositionRecordedFrameCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Submitted frames: {DMN_GetContinuousCompositionSubmittedFrameCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Fence-completed frames: {DMN_GetContinuousCompositionFenceCompletedFrameCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Back buffer 0 use count: {DMN_GetContinuousCompositionBackBuffer0UseCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Back buffer 1 use count: {DMN_GetContinuousCompositionBackBuffer1UseCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Invalid back buffer count: {DMN_GetContinuousCompositionInvalidBackBufferIndexCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Average frame milliseconds: {averageUs / 1000.0:F3}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Minimum frame milliseconds: {DMN_GetContinuousCompositionMinimumFrameMilliseconds()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Maximum frame milliseconds: {DMN_GetContinuousCompositionMaximumFrameMilliseconds()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Elapsed milliseconds: {elapsed}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Dropped schedules: {DMN_GetContinuousCompositionDroppedScheduleCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Rejected frame requests: {DMN_GetContinuousCompositionRejectedFrameRequestCount()}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Overall timeout: {B(DMN_DidContinuousCompositionTimeout())}");
            Debug.Log($"[DesktopMascotAlphaMaskDiagnostics] Completed normally: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private IEnumerator FinishFailed()
        {
            Debug.LogError(
                $"[DesktopMascotAlphaMaskDiagnostics] Diagnostic failed: managed stage={managedFailureStage}, native stage={SafeNativeFailureStage()}.");
            shutdownRequested = true;
            while (requestPending)
            {
                yield return null;
            }
            try
            {
                DMN_StopCompositionAlphaMaskDiagnostics();
                DMN_RequestContinuousCompositionDiagnosticsStop();
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
            RestoreRunInBackground();
            ReleasePinnedBuffer();
            Destroy(gameObject);
        }

        private static int SafeNativeFailureStage()
        {
            try
            {
                return DMN_GetCompositionAlphaMaskFailureStage();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return -1;
            }
        }

        private void OnApplicationQuit()
        {
            if (externalLifecycleOwner)
            {
                return;
            }
            shutdownRequested = true;
            try
            {
                DMN_StopCompositionAlphaMaskDiagnostics();
                DMN_RequestContinuousCompositionDiagnosticsStop();
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
            RestoreRunInBackground();
            if (!requestPending)
            {
                ReleasePinnedBuffer();
            }
        }

        private void RestoreRunInBackground()
        {
            if (externalLifecycleOwner || runInBackgroundRestored)
            {
                return;
            }
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
            Debug.Log(
                $"[DesktopMascotAlphaMaskDiagnostics] runInBackground restored: {runInBackgroundRestored}");
        }

        private void ReleasePinnedBuffer()
        {
            if (alphaMaskHandle.IsAllocated)
            {
                alphaMaskHandle.Free();
            }
        }

        private static string AlphaStateName(int state)
        {
            return state switch
            {
                0 => "NotStarted",
                1 => "Ready",
                2 => "WaitingForFirstMask",
                3 => "Receiving",
                4 => "SnapshotPublished",
                5 => "Validating",
                6 => "Completed",
                7 => "ShutdownRequested",
                8 => "Stopped",
                9 => "Failed",
                _ => $"Unknown({state})"
            };
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
                $"[DesktopMascotAlphaMaskDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
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
