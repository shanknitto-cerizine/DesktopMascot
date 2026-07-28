using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotAnimatedWindowRegionDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Size = 64;
        private const int ByteCount = Size * Size;
        private const int Threshold = 128;
        private const int UpdateIntervalMilliseconds = 250;
        private const float ReadbackIntervalSeconds = 0.25f;
        private const float PhaseDurationSeconds = 8.0f;
        private const float VisualDurationSeconds = 32.0f;
        private const float OverallTimeoutSeconds = 60.0f;
        private const int CompositionRunning = 14;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int RegionRunning = 5;
        private const int RegionCompleted = 9;
        private const int RegionFailed = 11;
        internal static bool AutoStartEnabled { get; set; } = true;

        private readonly byte[] alphaMask = new byte[ByteCount];
        private readonly bool[] observedPhases = new bool[4];
        private GCHandle alphaMaskHandle;
        private Action<AsyncGPUReadbackRequest> readbackCallback;
        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool requestPending;
        private bool shutdownRequested;
        private bool orientationKnown;
        private bool flipRows;
        private int requestPhase;
        private int managedFailureStage;
        private int readbackErrors;
        private ulong generation;
        private float nextReadbackAt;
        private float diagnosticStartedAt;
        private float visualStartedAt;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartAnimatedCompositionAlphaMaskDiagnostics(int width, int height, int stride, int threshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_SubmitCompositionAlphaMask(IntPtr data, int width, int height, int stride, ulong generation);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionAlphaMaskPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartAnimatedWindowRegionDiagnostics(int threshold, int minimumUpdateIntervalMilliseconds);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_PollAnimatedWindowRegionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_CompleteAnimatedWindowRegionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StopAnimatedWindowRegionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionMinimumUpdateIntervalMilliseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionBuildGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionRequestedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionAppliedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionBuildCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionGenerationEvaluationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionApplyMessagePostCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionApplyExecutionCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionDuplicateMaskSkipCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionSupersededGenerationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionApplyRequestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionApplySuccessCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionApplyFailureCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionSkippedGenerationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionDuplicateSkipCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionCurrentRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionCurrentCoveredPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionCurrentExcludedPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionInitialGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionPeakGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionFinalGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionGdiObjectDelta();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionApplyRequestPending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidAnimatedWindowRegionLastApplySucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetAnimatedWindowRegionLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidAnimatedWindowRegionInitialRegionRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidAnimatedWindowRegionInitialStyleRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionInitialRegionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetAnimatedWindowRegionCurrentPhase();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionTopLeftInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionTopRightInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionBottomRightInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionBottomLeftInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsAnimatedWindowRegionCenterInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionInitialExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionDiagnosticExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetAnimatedWindowRegionCurrentExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!AutoStartEnabled)
            {
                return;
            }
            var instance = new GameObject(
                nameof(DesktopMascotAnimatedWindowRegionDiagnostics));
            DontDestroyOnLoad(instance);
            instance.AddComponent<
                DesktopMascotAnimatedWindowRegionDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            readbackCallback = OnReadback;
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
        }

        private IEnumerator Start()
        {
            yield return null;
            diagnosticStartedAt = Time.realtimeSinceStartup;
            while (!TimedOut() && !PrerequisitesReady())
            {
                yield return null;
            }
            if (TimedOut() || !SystemInfo.supportsAsyncGPUReadback)
            {
                managedFailureStage = TimedOut() ? 17 : 5;
                yield return ShutdownAndQuit();
                yield break;
            }

            DesktopMascotContinuousCompositionDiagnostics.
                TargetFpsForNextRun = 35;
            DesktopMascotContinuousCompositionDiagnostics.
                AnimatedAlphaPatternForNextRun = true;
            DesktopMascotContinuousCompositionDiagnostics.
                AnimatedAlphaPhaseDurationForNextRun =
                    PhaseDurationSeconds;
            DesktopMascotContinuousCompositionDiagnostics.
                ExternalShutdownOwnerForNextRun = true;
            var continuous = new GameObject(
                nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuous);
            continuous.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();

            RenderTexture source = null;
            while (!TimedOut())
            {
                source = DesktopMascotContinuousCompositionDiagnostics.
                    ActiveSourceTexture;
                if (source != null && source.IsCreated()
                    && DMN_GetContinuousCompositionPresentCount() > 0)
                {
                    break;
                }
                if (DMN_GetContinuousCompositionState() == ContinuousFailed)
                {
                    managedFailureStage = 16;
                    yield return ShutdownAndQuit();
                    yield break;
                }
                yield return null;
            }
            if (source == null || TimedOut())
            {
                managedFailureStage = 17;
                yield return ShutdownAndQuit();
                yield break;
            }

            alphaMaskHandle = GCHandle.Alloc(alphaMask, GCHandleType.Pinned);
            var alphaStart = DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                Size, Size, Size, Threshold);
            if (alphaStart != 1)
            {
                managedFailureStage = DMN_GetCompositionAlphaMaskFailureStage();
                yield return ShutdownAndQuit();
                yield break;
            }

            nextReadbackAt = Time.realtimeSinceStartup;
            while (!TimedOut()
                   && DMN_GetCompositionAlphaMaskPublishedGeneration() == 0)
            {
                PumpReadback(source);
                yield return null;
            }
            if (TimedOut() || managedFailureStage != 0)
            {
                yield return ShutdownAndQuit();
                yield break;
            }

            var startResult = DMN_StartAnimatedWindowRegionDiagnostics(
                Threshold, UpdateIntervalMilliseconds);
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Start result: {startResult}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Threshold: {Threshold}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Minimum update interval milliseconds: {UpdateIntervalMilliseconds}");
            Debug.Log("[DesktopMascotAnimatedWindowRegionDiagnostics] Maximum update frequency: 4 Hz");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial region state: {InitialRegionName(DMN_GetAnimatedWindowRegionInitialRegionState())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Composition window position: ({DMN_GetCompositionWindowInitialX()}, {DMN_GetCompositionWindowInitialY()})");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Visual test duration seconds: {VisualDurationSeconds:0}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Phase duration seconds: {PhaseDurationSeconds:0}");
            Debug.Log("[DesktopMascotAnimatedWindowRegionDiagnostics] Continuous target FPS: 35");
            if (startResult != 1)
            {
                yield return ShutdownAndQuit();
                yield break;
            }

            while (!TimedOut()
                   && DMN_GetAnimatedWindowRegionState() != RegionRunning)
            {
                PumpReadback(source);
                DMN_PollAnimatedWindowRegionDiagnostics();
                if (DMN_GetAnimatedWindowRegionState() == RegionFailed)
                {
                    break;
                }
                yield return null;
            }
            if (DMN_GetAnimatedWindowRegionState() != RegionRunning)
            {
                yield return ShutdownAndQuit();
                yield break;
            }

            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial GDI object count: {DMN_GetAnimatedWindowRegionInitialGdiObjectCount()}");
            visualStartedAt = Time.realtimeSinceStartup;
            var lastLoggedPhase = -1;
            while (!TimedOut()
                   && Time.realtimeSinceStartup - visualStartedAt
                       < VisualDurationSeconds)
            {
                PumpReadback(source);
                DMN_PollAnimatedWindowRegionDiagnostics();
                var phase = DMN_GetAnimatedWindowRegionCurrentPhase();
                if (phase >= 0 && phase < 4 && phase != lastLoggedPhase)
                {
                    LogPhase(phase);
                    observedPhases[phase] = ValidateCurrentPhase(phase);
                    lastLoggedPhase = phase;
                }
                if (DMN_GetAnimatedWindowRegionState() == RegionFailed
                    || managedFailureStage != 0)
                {
                    break;
                }
                yield return null;
            }

            while (requestPending && !TimedOut())
            {
                yield return null;
            }
            for (var frame = 0; frame < 120 && !TimedOut(); frame++)
            {
                DMN_PollAnimatedWindowRegionDiagnostics();
                if (!B(DMN_IsAnimatedWindowRegionApplyRequestPending())
                    && DMN_GetAnimatedWindowRegionAppliedGeneration()
                        >= DMN_GetAnimatedWindowRegionPublishedGeneration())
                {
                    break;
                }
                yield return null;
            }
            if (!AllPhasesObserved())
            {
                managedFailureStage = 11;
            }
            if (managedFailureStage == 0
                && DMN_GetAnimatedWindowRegionFailureStage() == 0)
            {
                DMN_CompleteAnimatedWindowRegionDiagnostics();
            }
            DMN_StopAnimatedWindowRegionDiagnostics();
            while (!TimedOut()
                   && DMN_GetAnimatedWindowRegionState() != RegionCompleted
                   && DMN_GetAnimatedWindowRegionState() != RegionFailed)
            {
                yield return null;
            }
            LogFinalRegionState();

            DMN_StopCompositionAlphaMaskDiagnostics();
            while (!TimedOut()
                   && DMN_GetContinuousCompositionState()
                       != ContinuousCompleted
                   && DMN_GetContinuousCompositionState()
                       != ContinuousFailed)
            {
                yield return null;
            }
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while (!TimedOut()
                   && DMN_GetContinuousCompositionState()
                       != ContinuousStopped
                   && DMN_GetContinuousCompositionState()
                       != ContinuousFailed)
            {
                yield return null;
            }
            LogContinuous();
            yield return ShutdownAndQuit();
        }

        private void PumpReadback(RenderTexture source)
        {
            if (requestPending
                || Time.realtimeSinceStartup < nextReadbackAt)
            {
                return;
            }
            requestPending = true;
            requestPhase =
                DesktopMascotContinuousCompositionDiagnostics.
                    CurrentAnimatedAlphaPhase;
            nextReadbackAt =
                Time.realtimeSinceStartup + ReadbackIntervalSeconds;
            AsyncGPUReadback.Request(source, 0, readbackCallback);
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            requestPending = false;
            if (shutdownRequested || request.hasError)
            {
                if (request.hasError)
                {
                    readbackErrors++;
                    managedFailureStage = 5;
                }
                return;
            }
            NativeArray<byte> data = request.GetData<byte>();
            if (data.Length != ByteCount * 4)
            {
                managedFailureStage = 5;
                return;
            }
            if (!orientationKnown)
            {
                var normal = DetectRawPhase(data, false);
                var flipped = DetectRawPhase(data, true);
                var current =
                    DesktopMascotContinuousCompositionDiagnostics.
                        CurrentAnimatedAlphaPhase;
                if (normal == requestPhase || normal == current)
                {
                    flipRows = false;
                }
                else if (flipped == requestPhase || flipped == current)
                {
                    flipRows = true;
                }
                else
                {
                    managedFailureStage = 5;
                    return;
                }
                orientationKnown = true;
            }
            for (var y = 0; y < Size; y++)
            {
                var sourceY = flipRows ? Size - 1 - y : y;
                for (var x = 0; x < Size; x++)
                {
                    alphaMask[y * Size + x] =
                        data[(sourceY * Size + x) * 4 + 3];
                }
            }
            generation++;
            if (DMN_SubmitCompositionAlphaMask(
                    alphaMaskHandle.AddrOfPinnedObject(),
                    Size, Size, Size, generation) != 1)
            {
                managedFailureStage =
                    DMN_GetCompositionAlphaMaskFailureStage();
            }
        }

        private static int DetectRawPhase(
            NativeArray<byte> data,
            bool flip)
        {
            var tl = RawOpaque(data, 16, flip ? 47 : 16);
            var tr = RawOpaque(data, 48, flip ? 47 : 16);
            var br = RawOpaque(data, 48, flip ? 15 : 48);
            var bl = RawOpaque(data, 16, flip ? 15 : 48);
            if (tl && !tr && !br && !bl) return 0;
            if (!tl && tr && !br && !bl) return 1;
            if (!tl && !tr && br && !bl) return 2;
            if (!tl && !tr && !br && bl) return 3;
            return -1;
        }

        private static bool RawOpaque(
            NativeArray<byte> data,
            int x,
            int y)
        {
            return data[(y * Size + x) * 4 + 3] >= Threshold;
        }

        private static bool ValidateCurrentPhase(int phase)
        {
            var tl = B(DMN_IsAnimatedWindowRegionTopLeftInside());
            var tr = B(DMN_IsAnimatedWindowRegionTopRightInside());
            var br = B(DMN_IsAnimatedWindowRegionBottomRightInside());
            var bl = B(DMN_IsAnimatedWindowRegionBottomLeftInside());
            return phase switch
            {
                0 => tl && !tr && !br && !bl,
                1 => !tl && tr && !br && !bl,
                2 => !tl && !tr && br && !bl,
                3 => !tl && !tr && !br && bl,
                _ => false
            };
        }

        private static void LogPhase(int phase)
        {
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Phase: {phase}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Opaque quadrant: {QuadrantName(phase)}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Published generation: {DMN_GetAnimatedWindowRegionPublishedGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Applied generation: {DMN_GetAnimatedWindowRegionAppliedGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Rectangle count: {DMN_GetAnimatedWindowRegionCurrentRectangleCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Covered pixel count: {DMN_GetAnimatedWindowRegionCurrentCoveredPixelCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Excluded pixel count: {DMN_GetAnimatedWindowRegionCurrentExcludedPixelCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Top-left in region: {B(DMN_IsAnimatedWindowRegionTopLeftInside())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Top-right in region: {B(DMN_IsAnimatedWindowRegionTopRightInside())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Bottom-right in region: {B(DMN_IsAnimatedWindowRegionBottomRightInside())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Bottom-left in region: {B(DMN_IsAnimatedWindowRegionBottomLeftInside())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Center in region: {B(DMN_IsAnimatedWindowRegionCenterInside())}");
        }

        private static void LogFinalRegionState()
        {
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] State: {StateName(DMN_GetAnimatedWindowRegionState())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Failure stage: {DMN_GetAnimatedWindowRegionFailureStage()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Published generation: {DMN_GetAnimatedWindowRegionPublishedGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Build generation: {DMN_GetAnimatedWindowRegionBuildGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Requested generation: {DMN_GetAnimatedWindowRegionRequestedGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Applied generation: {DMN_GetAnimatedWindowRegionAppliedGeneration()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Region build count: {DMN_GetAnimatedWindowRegionBuildCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Generation evaluation count: {DMN_GetAnimatedWindowRegionGenerationEvaluationCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Region apply message post count: {DMN_GetAnimatedWindowRegionApplyMessagePostCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Region apply execution count: {DMN_GetAnimatedWindowRegionApplyExecutionCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Region apply success count: {DMN_GetAnimatedWindowRegionApplySuccessCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Region apply failure count: {DMN_GetAnimatedWindowRegionApplyFailureCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Duplicate mask skip count: {DMN_GetAnimatedWindowRegionDuplicateMaskSkipCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Superseded generation count: {DMN_GetAnimatedWindowRegionSupersededGenerationCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Last apply succeeded: {B(DMN_DidAnimatedWindowRegionLastApplySucceed())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Apply request pending: {B(DMN_IsAnimatedWindowRegionApplyRequestPending())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Last Win32 error: {DMN_GetAnimatedWindowRegionLastWin32Error()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial region restored: {B(DMN_DidAnimatedWindowRegionInitialRegionRestoreSucceed())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial extended style restored: {B(DMN_DidAnimatedWindowRegionInitialStyleRestoreSucceed())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial GDI object count: {DMN_GetAnimatedWindowRegionInitialGdiObjectCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Peak GDI object count: {DMN_GetAnimatedWindowRegionPeakGdiObjectCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Final GDI object count: {DMN_GetAnimatedWindowRegionFinalGdiObjectCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] GDI object delta: {DMN_GetAnimatedWindowRegionGdiObjectDelta()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Initial style: {H(DMN_GetAnimatedWindowRegionInitialExtendedStyle())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Diagnostic style: {H(DMN_GetAnimatedWindowRegionDiagnosticExtendedStyle())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Final style: {H(DMN_GetAnimatedWindowRegionCurrentExtendedStyle())}");
        }

        private static void LogContinuous()
        {
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Continuous Present successful: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private IEnumerator ShutdownAndQuit()
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                while (requestPending
                       && Time.realtimeSinceStartup - diagnosticStartedAt
                           < OverallTimeoutSeconds)
                {
                    yield return null;
                }
                try
                {
                    DMN_StopAnimatedWindowRegionDiagnostics();
                    DMN_StopCompositionAlphaMaskDiagnostics();
                    DMN_RequestContinuousCompositionDiagnosticsStop();
                    DMN_RequestCompositionDiagnosticsShutdown();
                }
                catch (Exception exception) when (IsInteropException(exception))
                {
                    Debug.LogError($"[DesktopMascotAnimatedWindowRegionDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
                }
            }
            var shutdownAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - shutdownAt < 10.0f
                   && DMN_GetCompositionInitializationState()
                       != CompositionStopped)
            {
                yield return null;
            }
            var stopped = DMN_GetCompositionInitializationState()
                == CompositionStopped;
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Managed failure stage: {managedFailureStage}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Readback errors: {readbackErrors}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] Shutdown complete: {stopped}");
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] UI thread stopped: {stopped}");
            RestoreRunInBackground();
            ReleasePinnedMask();
            yield return null;
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            shutdownRequested = true;
            try
            {
                DMN_StopAnimatedWindowRegionDiagnostics();
                DMN_StopCompositionAlphaMaskDiagnostics();
                DMN_RequestContinuousCompositionDiagnosticsStop();
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Debug.LogError(exception);
            }
            RestoreRunInBackground();
            if (!requestPending)
            {
                ReleasePinnedMask();
            }
        }

        private void OnGUI()
        {
            if (visualStartedAt <= 0)
            {
                return;
            }
            const float maximumScale = 8.0f;
            const float horizontalMargin = 16.0f;
            const float bottomMargin = 16.0f;
            const float guideTop = 96.0f;
            var availableWidth =
                Math.Max(1.0f, Screen.width - horizontalMargin * 2.0f);
            var availableHeight =
                Math.Max(1.0f, Screen.height - guideTop - bottomMargin);
            var scale = Mathf.Min(
                maximumScale,
                availableWidth / Size,
                availableHeight / Size);
            var size = Size * scale;
            var x = (Screen.width - size) * 0.5f;
            var y = guideTop;
            var half = size * 0.5f;
            var phase = Math.Max(0, DMN_GetAnimatedWindowRegionCurrentPhase());
            for (var quadrant = 0; quadrant < 4; quadrant++)
            {
                var rect = quadrant switch
                {
                    0 => new Rect(x, y, half, half),
                    1 => new Rect(x + half, y, half, half),
                    2 => new Rect(x + half, y + half, half, half),
                    _ => new Rect(x, y + half, half, half)
                };
                GUI.color = quadrant == phase
                    ? new Color(0.15f, 0.65f, 0.9f, 1.0f)
                    : new Color(0.08f, 0.08f, 0.08f, 1.0f);
                GUI.Box(rect, quadrant == phase
                    ? $"{QuadrantName(quadrant)}\nOPAQUE"
                    : $"{QuadrantName(quadrant)}\nTRANSPARENT");
            }
            GUI.color = Color.white;
            var remaining = Math.Max(
                0,
                Mathf.CeilToInt(
                    VisualDurationSeconds
                    - (Time.realtimeSinceStartup - visualStartedAt)));
            var patternStartedAt =
                DesktopMascotContinuousCompositionDiagnostics.
                    AnimatedAlphaPatternStartedAt;
            var untilNext = PhaseDurationSeconds
                - ((Time.realtimeSinceStartup - patternStartedAt)
                    % PhaseDurationSeconds);
            GUI.Label(
                new Rect(16, 8, Screen.width - 32, 76),
                $"CLICK TEST: actual 64x64 window at "
                + $"({DMN_GetCompositionWindowInitialX()}, "
                + $"{DMN_GetCompositionWindowInitialY()})\n"
                + $"Phase {phase}: {QuadrantName(phase)} is OPAQUE "
                + $"for {untilNext:F1}s; other quadrants are TRANSPARENT\n"
                + $"Test remaining {remaining}s; published "
                + $"{DMN_GetAnimatedWindowRegionPublishedGeneration()}; "
                + $"applied {DMN_GetAnimatedWindowRegionAppliedGeneration()}; "
                + "the large guide is not the click target.");
        }

        private bool AllPhasesObserved()
        {
            foreach (var observed in observedPhases)
            {
                if (!observed) return false;
            }
            return true;
        }

        private bool TimedOut()
        {
            return Time.realtimeSinceStartup - diagnosticStartedAt
                >= OverallTimeoutSeconds;
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState()
                       == CompositionRunning
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0;
        }

        private void RestoreRunInBackground()
        {
            if (runInBackgroundRestored) return;
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
            Debug.Log($"[DesktopMascotAnimatedWindowRegionDiagnostics] runInBackground restored: {runInBackgroundRestored}");
        }

        private void ReleasePinnedMask()
        {
            if (alphaMaskHandle.IsAllocated) alphaMaskHandle.Free();
        }

        private static bool B(int value) => value != 0;
        private static string H(int value) =>
            $"0x{unchecked((uint)value):X8}";
        private static string H(ulong value) => $"0x{value:X16}";
        private static string QuadrantName(int phase) => phase switch
        {
            0 => "TopLeft",
            1 => "TopRight",
            2 => "BottomRight",
            3 => "BottomLeft",
            _ => "Unknown"
        };
        private static string StateName(int state) => state switch
        {
            9 => "Completed",
            10 => "Stopped",
            11 => "Failed",
            _ => state.ToString()
        };
        private static string InitialRegionName(int state) => state switch
        {
            0 => "Unknown",
            1 => "None",
            2 => "Empty",
            3 => "Simple",
            4 => "Complex",
            5 => "QueryFailed",
            _ => state.ToString()
        };
        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;
#endif
    }
}
