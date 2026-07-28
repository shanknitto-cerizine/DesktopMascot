using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotContinuousCompositionDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int ContinuousFrameEventId = 8;
        private const int TextureSize = 256;
        private const int DefaultTargetFps = 30;
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionStopped = 17;
        private const int ReadyForFrame = 2;
        private const int Completed = 10;
        private const int Stopped = 12;
        private const int Failed = 13;

        private readonly Color32[] pixels =
            new Color32[TextureSize * TextureSize];
        private Texture2D patternTexture;
        private RenderTexture sourceTexture;
        private Material alphaMaskMaterial;
        private bool stopRequested;
        private bool useAlphaMaskPattern;
        private bool useAnimatedAlphaPattern;
        private bool useStaticComplexSilhouettePattern;
        private bool useProductionAnimatedSilhouettePattern;
        private bool useExternalSourceTexture;
        private bool ownsSourceTexture;
        private float animatedPatternStartedAt;
        private bool externalShutdownOwner;
        internal static int TargetFpsForNextRun { get; set; } =
            DefaultTargetFps;
        internal static int PatternBorderMode { get; set; }
        internal static bool AlphaMaskPatternForNextRun { get; set; }
        internal static bool AnimatedAlphaPatternForNextRun { get; set; }
        internal static bool StaticComplexSilhouettePatternForNextRun
            { get; set; }
        internal static bool ProductionAnimatedSilhouettePatternForNextRun
            { get; set; }
        internal static float AnimatedAlphaPhaseDurationForNextRun
            { get; set; } = 2.0f;
        internal static bool ExternalShutdownOwnerForNextRun { get; set; }
        internal static RenderTexture ExternalSourceTextureForNextRun
            { get; set; }
        internal static RenderTexture ActiveSourceTexture { get; private set; }
        internal static int CurrentAnimatedAlphaPhase { get; private set; }
        internal static float AnimatedAlphaPatternStartedAt
            { get; private set; }
        internal static float CurrentAnimatedAlphaPhaseDuration
            { get; private set; } = 2.0f;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionCommandSubmissionMode();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr DMN_GetRenderEventAndDataFunc();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartContinuousCompositionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionFrame();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_PollContinuousCompositionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void DMN_RecordContinuousCompositionDroppedSchedule();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionTargetFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionRequestedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionRecordedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionSubmittedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionFenceCompletedFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionLastSequence();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionLastBackBufferIndex();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionBackBuffer0UseCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionBackBuffer1UseCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionInvalidBackBufferIndexCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionLastFenceSubmittedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionLastFenceCompletedValue();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsContinuousCompositionFrameInFlight();
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
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject =
                new GameObject(
                    nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            var targetFps = Math.Max(1, TargetFpsForNextRun);
            TargetFpsForNextRun = DefaultTargetFps;
            useAlphaMaskPattern = AlphaMaskPatternForNextRun;
            AlphaMaskPatternForNextRun = false;
            useAnimatedAlphaPattern = AnimatedAlphaPatternForNextRun;
            AnimatedAlphaPatternForNextRun = false;
            useStaticComplexSilhouettePattern =
                StaticComplexSilhouettePatternForNextRun;
            StaticComplexSilhouettePatternForNextRun = false;
            useProductionAnimatedSilhouettePattern =
                ProductionAnimatedSilhouettePatternForNextRun;
            ProductionAnimatedSilhouettePatternForNextRun = false;
            sourceTexture = ExternalSourceTextureForNextRun;
            ExternalSourceTextureForNextRun = null;
            useExternalSourceTexture = sourceTexture != null;
            animatedPatternStartedAt = Time.realtimeSinceStartup;
            AnimatedAlphaPatternStartedAt = animatedPatternStartedAt;
            CurrentAnimatedAlphaPhaseDuration = Math.Max(
                0.25f,
                AnimatedAlphaPhaseDurationForNextRun);
            AnimatedAlphaPhaseDurationForNextRun = 2.0f;
            CurrentAnimatedAlphaPhase = 0;
            externalShutdownOwner = ExternalShutdownOwnerForNextRun;
            ExternalShutdownOwnerForNextRun = false;
            var frameIntervalSeconds = 1.0 / targetFps;
            yield return null;
            var prerequisitesReady = false;
            for (var frame = 0; frame < 600; frame++)
            {
                if (!TryGetPrerequisitesReady(out prerequisitesReady))
                {
                    Cleanup();
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
                LogError("Prerequisites did not become ready.");
                RequestStopAndShutdown();
                Cleanup();
                yield break;
            }

            if (!CreateTextures(out var callback, out var sourcePointer))
            {
                LogError("Could not create the moving diagnostic texture.");
                RequestStopAndShutdown();
                Cleanup();
                yield break;
            }

            int startResult;
            uint targetFrameCount;
            try
            {
                startResult = DMN_StartContinuousCompositionDiagnostics();
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Start result: {startResult}");
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Command submission mode: {DMN_GetContinuousCompositionCommandSubmissionMode()}");
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Target FPS: {targetFps}");
                targetFrameCount =
                    DMN_GetContinuousCompositionTargetFrameCount();
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Target Present count: {targetFrameCount}");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                RequestStopAndShutdown();
                Cleanup();
                yield break;
            }
            if (startResult != 1)
            {
                RequestStopAndShutdown();
                Cleanup();
                yield break;
            }

            var stopwatch = Stopwatch.StartNew();
            var nextScheduledTime = 0.0;
            var patternFrame = 0;
            uint lastPresentCount = 0;
            var firstLogged = false;
            var firstThirdLogged = false;
            var secondThirdLogged = false;
            var targetLogged = false;
            var state = 0;

            while (true)
            {
                state = DMN_PollContinuousCompositionDiagnostics();
                var presentCount =
                    DMN_GetContinuousCompositionPresentCount();
                LogMilestones(
                    presentCount,
                    targetFrameCount,
                    ref firstLogged,
                    ref firstThirdLogged,
                    ref secondThirdLogged,
                    ref targetLogged);
                lastPresentCount = presentCount;
                if (state == Completed || state == Failed)
                {
                    break;
                }

                var now = stopwatch.Elapsed.TotalSeconds;
                if (state == ReadyForFrame && now >= nextScheduledTime)
                {
                    if (now - nextScheduledTime >= frameIntervalSeconds)
                    {
                        DMN_RecordContinuousCompositionDroppedSchedule();
                    }
                    UpdatePattern(patternFrame++);
                    if (!IssueFrame(callback, sourcePointer))
                    {
                        break;
                    }
                    nextScheduledTime =
                        Math.Max(
                            nextScheduledTime + frameIntervalSeconds,
                            now + frameIntervalSeconds);
                }
                yield return null;
            }

            LogMilestones(
                lastPresentCount,
                targetFrameCount,
                ref firstLogged,
                ref firstThirdLogged,
                ref secondThirdLogged,
                ref targetLogged);
            LogSummary();
            if (state == Completed)
            {
                yield return new WaitForSecondsRealtime(1.0f);
            }

            RequestContinuousStop();
            for (var frame = 0; frame < 600; frame++)
            {
                state = DMN_PollContinuousCompositionDiagnostics();
                if (state == Stopped)
                {
                    break;
                }
                yield return null;
            }
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Shutdown complete: {state == Stopped}");

            if (!externalShutdownOwner)
            {
                DMN_RequestCompositionDiagnosticsShutdown();
                for (var frame = 0; frame < 600; frame++)
                {
                    if (DMN_GetCompositionInitializationState()
                        == CompositionStopped)
                    {
                        break;
                    }
                    yield return null;
                }
            }
            Cleanup();
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState()
                       == CompositionMessageLoopRunning
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0;
        }

        private static bool TryGetPrerequisitesReady(out bool ready)
        {
            ready = false;
            try
            {
                ready = PrerequisitesReady();
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
        }

        private bool CreateTextures(
            out IntPtr callback,
            out IntPtr sourcePointer)
        {
            callback = IntPtr.Zero;
            sourcePointer = IntPtr.Zero;
            if (useExternalSourceTexture)
            {
                ActiveSourceTexture = sourceTexture;
                callback = DMN_GetRenderEventAndDataFunc();
                sourcePointer = sourceTexture.GetNativeTexturePtr();
                return sourceTexture.IsCreated()
                    && sourceTexture.width == TextureSize
                    && sourceTexture.height == TextureSize
                    && sourceTexture.graphicsFormat
                        == GraphicsFormat.B8G8R8A8_SRGB
                    && callback != IntPtr.Zero
                    && sourcePointer != IntPtr.Zero;
            }
            const GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB;
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
            {
                return false;
            }
            if (useAlphaMaskPattern || useAnimatedAlphaPattern
                || useStaticComplexSilhouettePattern
                || useProductionAnimatedSilhouettePattern)
            {
                var shader = Resources.Load<Shader>(
                    useProductionAnimatedSilhouettePattern
                        ? "DesktopMascotProductionSizedAnimatedSilhouetteDiagnostic"
                        : useStaticComplexSilhouettePattern
                        ? "DesktopMascotStaticComplexSilhouetteDiagnostic"
                        : (useAnimatedAlphaPattern
                            ? "DesktopMascotAnimatedAlphaMaskDiagnostic"
                            : "DesktopMascotAlphaMaskDiagnostic"));
                if (shader == null)
                {
                    return false;
                }
                alphaMaskMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                patternTexture = new Texture2D(
                    TextureSize,
                    TextureSize,
                    TextureFormat.RGBA32,
                    false,
                    true);
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
                autoGenerateMips = false
            };
            sourceTexture = new RenderTexture(descriptor);
            sourceTexture.Create();
            ownsSourceTexture = true;
            ActiveSourceTexture = sourceTexture;
            callback = DMN_GetRenderEventAndDataFunc();
            sourcePointer = sourceTexture.GetNativeTexturePtr();
            return sourceTexture.IsCreated()
                && callback != IntPtr.Zero
                && sourcePointer != IntPtr.Zero;
        }

        private void UpdatePattern(int frameIndex)
        {
            if (useExternalSourceTexture)
            {
                return;
            }
            if (useAnimatedAlphaPattern
                || useProductionAnimatedSilhouettePattern)
            {
                CurrentAnimatedAlphaPhase =
                    Mathf.FloorToInt(
                        (Time.realtimeSinceStartup
                            - animatedPatternStartedAt)
                        / CurrentAnimatedAlphaPhaseDuration) % 4;
                alphaMaskMaterial.SetInt(
                    "_Phase",
                    CurrentAnimatedAlphaPhase);
                return;
            }
            if (useAlphaMaskPattern)
            {
                return;
            }
            if (useStaticComplexSilhouettePattern)
            {
                return;
            }
            var firstBarX = frameIndex % TextureSize;
            var secondBarX = (firstBarX + 1) % TextureSize;
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var color = y < TextureSize / 2
                        ? (x < TextureSize / 2
                            ? new Color32(255, 0, 0, 255)
                            : new Color32(0, 255, 0, 255))
                        : (x < TextureSize / 2
                            ? new Color32(0, 0, 255, 255)
                            : new Color32(255, 255, 255, 255));
                    if (x == firstBarX || x == secondBarX)
                    {
                        color = color.r == 255
                                && color.g == 255
                                && color.b == 255
                            ? new Color32(0, 0, 0, 255)
                            : new Color32(255, 255, 255, 255);
                    }
                    if ((x == 0
                            || y == 0
                            || x == TextureSize - 1
                            || y == TextureSize - 1)
                        && PatternBorderMode != 0)
                    {
                        color = PatternBorderMode == 1
                            ? new Color32(255, 255, 0, 255)
                            : new Color32(0, 128, 255, 255);
                    }
                    pixels[y * TextureSize + x] = color;
                }
            }
            patternTexture.SetPixels32(pixels);
            patternTexture.Apply(false, false);
        }

        private bool IssueFrame(IntPtr callback, IntPtr sourcePointer)
        {
            CommandBuffer commandBuffer = null;
            try
            {
                if (DMN_RequestContinuousCompositionFrame() != 1)
                {
                    return false;
                }
                commandBuffer = new CommandBuffer
                {
                    name = "Desktop Mascot Continuous Composition Frame"
                };
                if (useExternalSourceTexture)
                {
                    // The scene camera already rendered the real mascot.
                }
                else if (useAlphaMaskPattern || useAnimatedAlphaPattern
                    || useStaticComplexSilhouettePattern
                    || useProductionAnimatedSilhouettePattern)
                {
                    commandBuffer.Blit(
                        null,
                        sourceTexture,
                        alphaMaskMaterial);
                }
                else
                {
                    commandBuffer.Blit(patternTexture, sourceTexture);
                }
                commandBuffer.IssuePluginEventAndData(
                    callback,
                    ContinuousFrameEventId,
                    sourcePointer);
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

        private static void LogMilestones(
            uint count,
            uint targetCount,
            ref bool first,
            ref bool firstThird,
            ref bool secondThird,
            ref bool target)
        {
            if (count >= 1 && !first)
            {
                first = true;
                Debug.Log(
                    "[DesktopMascotContinuousCompositionDiagnostics] First Present succeeded.");
            }
            var firstThreshold = targetCount / 3;
            var secondThreshold = targetCount * 2 / 3;
            if (count >= firstThreshold && !firstThird)
            {
                firstThird = true;
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Present progress: {firstThreshold}/{targetCount}");
            }
            if (count >= secondThreshold && !secondThird)
            {
                secondThird = true;
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Present progress: {secondThreshold}/{targetCount}");
            }
            if (count >= targetCount && !target)
            {
                target = true;
                Debug.Log(
                    $"[DesktopMascotContinuousCompositionDiagnostics] Present progress: {targetCount}/{targetCount}");
            }
        }

        private static void LogSummary()
        {
            var elapsed =
                DMN_GetContinuousCompositionElapsedMilliseconds();
            var count = DMN_GetContinuousCompositionPresentCount();
            var averageUs =
                DMN_GetContinuousCompositionAverageFrameMicroseconds();
            var effectiveFps =
                elapsed == 0 ? 0.0 : count * 1000.0 / elapsed;
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] State: {StateName(DMN_GetContinuousCompositionState())}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Failure stage: {DMN_GetContinuousCompositionFailureStage()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Requested frames: {DMN_GetContinuousCompositionRequestedFrameCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Recorded frames: {DMN_GetContinuousCompositionRecordedFrameCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Submitted frames: {DMN_GetContinuousCompositionSubmittedFrameCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Fence-completed frames: {DMN_GetContinuousCompositionFenceCompletedFrameCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Present count: {count}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Last sequence: {DMN_GetContinuousCompositionLastSequence()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Last back buffer index: {DMN_GetContinuousCompositionLastBackBufferIndex()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Back buffer 0 use count: {DMN_GetContinuousCompositionBackBuffer0UseCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Back buffer 1 use count: {DMN_GetContinuousCompositionBackBuffer1UseCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Invalid back buffer count: {DMN_GetContinuousCompositionInvalidBackBufferIndexCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Last submitted fence: {DMN_GetContinuousCompositionLastFenceSubmittedValue()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Last completed fence: {DMN_GetContinuousCompositionLastFenceCompletedValue()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Frame in flight: {B(DMN_IsContinuousCompositionFrameInFlight())}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Elapsed milliseconds: {elapsed}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Average frame milliseconds: {averageUs / 1000.0:F3}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Minimum frame milliseconds: {DMN_GetContinuousCompositionMinimumFrameMilliseconds()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Maximum frame milliseconds: {DMN_GetContinuousCompositionMaximumFrameMilliseconds()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Effective Present FPS: {effectiveFps:F2}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Dropped schedules: {DMN_GetContinuousCompositionDroppedScheduleCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Rejected frame requests: {DMN_GetContinuousCompositionRejectedFrameRequestCount()}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Overall timeout: {B(DMN_DidContinuousCompositionTimeout())}");
            Debug.Log(
                $"[DesktopMascotContinuousCompositionDiagnostics] Completed normally: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private void RequestContinuousStop()
        {
            if (stopRequested)
            {
                return;
            }
            stopRequested = true;
            DMN_RequestContinuousCompositionDiagnosticsStop();
        }

        private void RequestStopAndShutdown()
        {
            try
            {
                RequestContinuousStop();
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
        }

        private void OnApplicationQuit()
        {
            RequestStopAndShutdown();
        }

        private void Cleanup()
        {
            PatternBorderMode = 0;
            CurrentAnimatedAlphaPhase = 0;
            AnimatedAlphaPatternStartedAt = 0;
            CurrentAnimatedAlphaPhaseDuration = 2.0f;
            if (ActiveSourceTexture == sourceTexture)
            {
                ActiveSourceTexture = null;
            }
            if (sourceTexture != null && ownsSourceTexture)
            {
                sourceTexture.Release();
                Destroy(sourceTexture);
            }
            sourceTexture = null;
            if (patternTexture != null)
            {
                Destroy(patternTexture);
                patternTexture = null;
            }
            if (alphaMaskMaterial != null)
            {
                Destroy(alphaMaskMaterial);
                alphaMaskMaterial = null;
            }
            Destroy(gameObject);
        }

        private static string StateName(int state)
        {
            return state switch
            {
                0 => "NotStarted",
                1 => "WaitingForComposition",
                2 => "ReadyForFrame",
                3 => "WaitingForUnityRenderEvent",
                4 => "CopyCommandRecorded",
                5 => "WaitingForFence",
                6 => "PresentMessagePosted",
                7 => "WaitingForPresent",
                8 => "FramePresented",
                9 => "FrameIntervalWaiting",
                10 => "Completed",
                11 => "ShutdownRequested",
                12 => "Stopped",
                13 => "Failed",
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
                $"[DesktopMascotContinuousCompositionDiagnostics] Native interop failed: {exception.Message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError(
                $"[DesktopMascotContinuousCompositionDiagnostics] {message}");
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
