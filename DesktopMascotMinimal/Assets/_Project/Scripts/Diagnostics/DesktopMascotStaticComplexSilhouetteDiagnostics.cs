using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotStaticComplexSilhouetteDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Size = 64;
        private const int Threshold = 128;
        private const int TargetPresentCount = 1200;
        private const float VisualDurationSeconds = 30.0f;
        private const float OverallTimeoutSeconds = 55.0f;
        private const int CompositionRunning = 14;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int RegionVisualTestRunning = 6;
        private const int RegionCompleted = 12;
        private const int RegionFailed = 14;
        internal static bool AutoStartEnabled { get; set; } = true;

        private readonly byte[] alphaMask = new byte[Size * Size];
        private GCHandle alphaMaskHandle;
        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool readbackPending;
        private bool readbackComplete;
        private int managedFailureStage;
        private int readbackErrors;
        private float diagnosticStartedAt;
        private float visualStartedAt;
        private RenderTexture source;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_SetContinuousCompositionTargetFrameCountForNextRun(uint target);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartAnimatedCompositionAlphaMaskDiagnostics(int width, int height, int stride, int threshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_SubmitCompositionAlphaMask(IntPtr data, int width, int height, int stride, ulong generation);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartStaticComplexSilhouetteDiagnostics(int threshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_CompleteStaticComplexSilhouetteDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StopStaticComplexSilhouetteDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouetteState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouetteFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouetteThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouettePublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteAppliedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteGenerationEvaluationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteRegionBuildCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteApplyMessagePostCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteApplyExecutionCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteApplySuccessCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteApplyFailureCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteDuplicateMaskSkipCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteSupersededGenerationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteRawScanlineRunCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteMergedRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteRegionDataRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouetteRegionType();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteCoveredPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteExcludedPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteRegionDataSizeBytes();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteLastBuildMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteMinimumBuildMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteMaximumBuildMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteAverageBuildMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteInitialGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouettePreShutdownGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouettePostShutdownGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouettePreShutdownGdiDelta();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetStaticComplexSilhouettePostShutdownGdiDelta();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteCounterInvariantsSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteRepresentativeValidationSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteInitialRegionRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteInitialStyleRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteLastApplySucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetStaticComplexSilhouetteLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteBodyCenterInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteLongLeftEarInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteMirroredEarInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteBottomRightTailInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteMirroredTailInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteTransparentCornerInside();
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelectDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled =
                false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!AutoStartEnabled)
            {
                return;
            }
            var instance = new GameObject(
                nameof(DesktopMascotStaticComplexSilhouetteDiagnostics));
            DontDestroyOnLoad(instance);
            instance.AddComponent<
                DesktopMascotStaticComplexSilhouetteDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
        }

        private IEnumerator Start()
        {
            yield return null;
            diagnosticStartedAt = Time.realtimeSinceStartup;
            while (!TimedOut() && !PrerequisitesReady()) yield return null;
            if (TimedOut() || !SystemInfo.supportsAsyncGPUReadback)
            {
                managedFailureStage = TimedOut() ? 18 : 4;
                yield return ShutdownAndQuit();
                yield break;
            }

            if (DMN_SetContinuousCompositionTargetFrameCountForNextRun(
                    TargetPresentCount) != 1)
            {
                managedFailureStage = 17;
                yield return ShutdownAndQuit();
                yield break;
            }
            DesktopMascotContinuousCompositionDiagnostics.TargetFpsForNextRun =
                35;
            DesktopMascotContinuousCompositionDiagnostics.
                StaticComplexSilhouettePatternForNextRun = true;
            DesktopMascotContinuousCompositionDiagnostics.
                ExternalShutdownOwnerForNextRun = true;
            var continuous = new GameObject(
                nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuous);
            continuous.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();

            while (!TimedOut())
            {
                source = DesktopMascotContinuousCompositionDiagnostics.
                    ActiveSourceTexture;
                if (source != null && source.IsCreated()
                    && DMN_GetContinuousCompositionPresentCount() > 0) break;
                if (DMN_GetContinuousCompositionState() == ContinuousFailed)
                {
                    managedFailureStage = 17;
                    break;
                }
                yield return null;
            }
            if (source == null || managedFailureStage != 0 || TimedOut())
            {
                yield return ShutdownAndQuit();
                yield break;
            }

            alphaMaskHandle = GCHandle.Alloc(alphaMask, GCHandleType.Pinned);
            if (DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                    Size, Size, Size, Threshold) != 1)
            {
                managedFailureStage =
                    DMN_GetCompositionAlphaMaskFailureStage();
                yield return ShutdownAndQuit();
                yield break;
            }
            readbackPending = true;
            AsyncGPUReadback.Request(source, 0, OnReadback);
            while (!TimedOut() && !readbackComplete
                   && managedFailureStage == 0) yield return null;
            if (!readbackComplete || managedFailureStage != 0)
            {
                yield return ShutdownAndQuit();
                yield break;
            }

            var startResult =
                DMN_StartStaticComplexSilhouetteDiagnostics(Threshold);
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Start result: {startResult}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Threshold: {Threshold}");
            Debug.Log("[DesktopMascotStaticComplexSilhouetteDiagnostics] Initial region state: None");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Composition window position: ({DMN_GetCompositionWindowInitialX()}, {DMN_GetCompositionWindowInitialY()})");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Visual test duration seconds: {VisualDurationSeconds:0}");
            if (startResult != 1)
            {
                yield return ShutdownAndQuit();
                yield break;
            }
            while (!TimedOut()
                   && DMN_GetStaticComplexSilhouetteState()
                       != RegionVisualTestRunning
                   && DMN_GetStaticComplexSilhouetteState() != RegionFailed)
            {
                yield return null;
            }
            if (DMN_GetStaticComplexSilhouetteState()
                != RegionVisualTestRunning)
            {
                yield return ShutdownAndQuit();
                yield break;
            }
            LogAppliedState();
            visualStartedAt = Time.realtimeSinceStartup;
            while (!TimedOut()
                   && Time.realtimeSinceStartup - visualStartedAt
                       < VisualDurationSeconds)
            {
                yield return null;
            }
            if (TimedOut())
            {
                managedFailureStage = 18;
                yield return ShutdownAndQuit();
                yield break;
            }

            DMN_CompleteStaticComplexSilhouetteDiagnostics();
            DMN_StopStaticComplexSilhouetteDiagnostics();
            while (!TimedOut()
                   && DMN_GetStaticComplexSilhouetteState() != RegionCompleted
                   && DMN_GetStaticComplexSilhouetteState() != RegionFailed)
            {
                yield return null;
            }
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
                       != ContinuousStopped)
            {
                yield return null;
            }
            DMN_StopCompositionAlphaMaskDiagnostics();
            DMN_RequestCompositionDiagnosticsShutdown();
            while (!TimedOut()
                   && DMN_GetCompositionInitializationState()
                       != CompositionStopped)
            {
                yield return null;
            }
            LogFinalState();
            yield return ShutdownAndQuit();
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            readbackPending = false;
            if (request.hasError)
            {
                readbackErrors++;
                managedFailureStage = 5;
                return;
            }
            var data = request.GetData<byte>();
            var normal = OrientationMatches(data, false);
            var flipped = OrientationMatches(data, true);
            if (normal == flipped)
            {
                managedFailureStage = 9;
                return;
            }
            for (var y = 0; y < Size; y++)
            {
                var sourceY = flipped ? Size - 1 - y : y;
                for (var x = 0; x < Size; x++)
                {
                    alphaMask[y * Size + x] =
                        data[(sourceY * Size + x) * 4 + 3];
                }
            }
            if (DMN_SubmitCompositionAlphaMask(
                    alphaMaskHandle.AddrOfPinnedObject(),
                    Size, Size, Size, 1) != 1)
            {
                managedFailureStage =
                    DMN_GetCompositionAlphaMaskFailureStage();
                return;
            }
            readbackComplete = true;
        }

        private static bool OrientationMatches(
            NativeArray<byte> data,
            bool flip)
        {
            return RawHit(data, 31, 38, flip)
                && RawHit(data, 15, 8, flip)
                && !RawHit(data, 48, 8, flip)
                && RawHit(data, 57, 43, flip)
                && !RawHit(data, 57, 20, flip)
                && !RawHit(data, 2, 2, flip);
        }

        private static bool RawHit(
            NativeArray<byte> data,
            int x,
            int y,
            bool flip)
        {
            var sourceY = flip ? Size - 1 - y : y;
            return data[(sourceY * Size + x) * 4 + 3] >= Threshold;
        }

        private static void LogAppliedState()
        {
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Published generation: {DMN_GetStaticComplexSilhouettePublishedGeneration()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Applied generation: {DMN_GetStaticComplexSilhouetteAppliedGeneration()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Generation evaluation count: {DMN_GetStaticComplexSilhouetteGenerationEvaluationCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region build count: {DMN_GetStaticComplexSilhouetteRegionBuildCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region apply message post count: {DMN_GetStaticComplexSilhouetteApplyMessagePostCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region apply execution count: {DMN_GetStaticComplexSilhouetteApplyExecutionCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region apply success count: {DMN_GetStaticComplexSilhouetteApplySuccessCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region apply failure count: {DMN_GetStaticComplexSilhouetteApplyFailureCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Duplicate mask skip count: {DMN_GetStaticComplexSilhouetteDuplicateMaskSkipCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Superseded generation count: {DMN_GetStaticComplexSilhouetteSupersededGenerationCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Raw scanline run count: {DMN_GetStaticComplexSilhouetteRawScanlineRunCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Merged rectangle count: {DMN_GetStaticComplexSilhouetteMergedRectangleCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region data rectangle count: {DMN_GetStaticComplexSilhouetteRegionDataRectangleCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region type: {DMN_GetStaticComplexSilhouetteRegionType()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Covered pixel count: {DMN_GetStaticComplexSilhouetteCoveredPixelCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Excluded pixel count: {DMN_GetStaticComplexSilhouetteExcludedPixelCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Region data size bytes: {DMN_GetStaticComplexSilhouetteRegionDataSizeBytes()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Last region build time microseconds: {DMN_GetStaticComplexSilhouetteLastBuildMicroseconds()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Last SetWindowRgn time microseconds: {DMN_GetStaticComplexSilhouetteLastSetWindowRgnMicroseconds()}");
        }

        private void LogFinalState()
        {
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] State: {StateName(DMN_GetStaticComplexSilhouetteState())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Failure stage: {DMN_GetStaticComplexSilhouetteFailureStage()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Counter invariants valid: {B(DMN_DidStaticComplexSilhouetteCounterInvariantsSucceed())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Representative validation succeeded: {B(DMN_DidStaticComplexSilhouetteRepresentativeValidationSucceed())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Body center: {B(DMN_IsStaticComplexSilhouetteBodyCenterInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Long left ear: {B(DMN_IsStaticComplexSilhouetteLongLeftEarInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Mirrored right-ear counterpart: {B(DMN_IsStaticComplexSilhouetteMirroredEarInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Bottom-right tail: {B(DMN_IsStaticComplexSilhouetteBottomRightTailInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Vertically mirrored tail counterpart: {B(DMN_IsStaticComplexSilhouetteMirroredTailInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Transparent corner: {B(DMN_IsStaticComplexSilhouetteTransparentCornerInside())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Initial region restored: {B(DMN_DidStaticComplexSilhouetteInitialRegionRestoreSucceed())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Initial extended style restored: {B(DMN_DidStaticComplexSilhouetteInitialStyleRestoreSucceed())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Initial GDI object count: {DMN_GetStaticComplexSilhouetteInitialGdiObjectCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Pre-shutdown GDI object count: {DMN_GetStaticComplexSilhouettePreShutdownGdiObjectCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Pre-shutdown GDI delta: {DMN_GetStaticComplexSilhouettePreShutdownGdiDelta()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Post-shutdown GDI object count: {DMN_GetStaticComplexSilhouettePostShutdownGdiObjectCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Post-shutdown GDI delta: {DMN_GetStaticComplexSilhouettePostShutdownGdiDelta()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Minimum region build time microseconds: {DMN_GetStaticComplexSilhouetteMinimumBuildMicroseconds()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Maximum region build time microseconds: {DMN_GetStaticComplexSilhouetteMaximumBuildMicroseconds()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Average region build time microseconds: {DMN_GetStaticComplexSilhouetteAverageBuildMicroseconds()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Maximum SetWindowRgn time microseconds: {DMN_GetStaticComplexSilhouetteMaximumSetWindowRgnMicroseconds()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Last apply succeeded: {B(DMN_DidStaticComplexSilhouetteLastApplySucceed())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Last Win32 error: {DMN_GetStaticComplexSilhouetteLastWin32Error()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Continuous Present successful: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private IEnumerator ShutdownAndQuit()
        {
            try
            {
                if (DMN_GetStaticComplexSilhouetteState() != RegionCompleted)
                    DMN_StopStaticComplexSilhouetteDiagnostics();
                DMN_StopCompositionAlphaMaskDiagnostics();
                DMN_RequestContinuousCompositionDiagnosticsStop();
                if (DMN_GetCompositionInitializationState()
                    != CompositionStopped)
                    DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Debug.LogError($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
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
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Managed failure stage: {managedFailureStage}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Readback errors: {readbackErrors}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] Shutdown complete: {stopped}");
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] UI thread stopped: {stopped}");
            RestoreRunInBackground();
            if (alphaMaskHandle.IsAllocated && !readbackPending)
                alphaMaskHandle.Free();
            yield return null;
            Application.Quit();
        }

        private void OnGUI()
        {
            if (visualStartedAt <= 0 || source == null) return;
            var scale = Mathf.Min(
                8.0f,
                (Screen.width - 32.0f) / Size,
                (Screen.height - 145.0f) / Size);
            var drawSize = Size * Mathf.Max(1.0f, scale);
            var rect = new Rect(
                (Screen.width - drawSize) * 0.5f,
                110,
                drawSize,
                drawSize);
            GUI.DrawTexture(rect, source, ScaleMode.StretchToFill, true);
            var remaining = Mathf.Max(
                0,
                Mathf.CeilToInt(
                    VisualDurationSeconds
                    - (Time.realtimeSinceStartup - visualStartedAt)));
            GUI.Label(
                new Rect(16, 8, Screen.width - 32, 96),
                "Static Complex Silhouette Diagnostics\n"
                + $"Threshold {Threshold}; published "
                + $"{DMN_GetStaticComplexSilhouettePublishedGeneration()}; "
                + $"applied {DMN_GetStaticComplexSilhouetteAppliedGeneration()}\n"
                + $"runs {DMN_GetStaticComplexSilhouetteRawScanlineRunCount()}; "
                + $"merged {DMN_GetStaticComplexSilhouetteMergedRectangleCount()}; "
                + $"covered {DMN_GetStaticComplexSilhouetteCoveredPixelCount()}; "
                + $"build {DMN_GetStaticComplexSilhouetteLastBuildMicroseconds()} us\n"
                + $"CLICK the actual 64x64 window at "
                + $"({DMN_GetCompositionWindowInitialX()}, "
                + $"{DMN_GetCompositionWindowInitialY()}); "
                + $"preview is not the click target; {remaining}s remaining.");
        }

        private void RestoreRunInBackground()
        {
            if (runInBackgroundRestored) return;
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
            Debug.Log($"[DesktopMascotStaticComplexSilhouetteDiagnostics] runInBackground restored: {runInBackgroundRestored}");
        }

        private bool TimedOut() =>
            Time.realtimeSinceStartup - diagnosticStartedAt
            >= OverallTimeoutSeconds;

        private static bool PrerequisitesReady() =>
            DMN_GetCompositionInitializationState() == CompositionRunning
            && DMN_IsDestinationTextureAvailable() != 0
            && DMN_WasReadbackValidationCompleted() != 0
            && DMN_DidReadbackExpectedOrientationMatch() != 0;

        private static bool B(int value) => value != 0;
        private static string H(int value) =>
            $"0x{unchecked((uint)value):X8}";
        private static string StateName(int state) => state switch
        {
            12 => "Completed",
            13 => "Stopped",
            14 => "Failed",
            _ => state.ToString()
        };
        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;
#endif
    }
}
