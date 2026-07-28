using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class
        DesktopMascotProductionSizedStaticSilhouetteDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Size = 256;
        private const int Threshold = 128;
        private const int RequestedFps = 30;
        private const int TargetPresents = 1200;
        private const float VisualSeconds = 30.0f;
        private const float TimeoutSeconds = 55.0f;
        private const int CompositionRunning = 14;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int RegionRunning = 6;
        private const int RegionCompleted = 12;
        private const int RegionFailed = 14;
        internal static bool AutoStartEnabled { get; set; } = true;

        private readonly byte[] mask = new byte[Size * Size];
        private readonly uint[] finalGdiSamples = new uint[5];
        private GCHandle maskHandle;
        private RenderTexture source;
        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool readbackPending;
        private bool readbackComplete;
        private int managedFailureStage;
        private int readbackErrors;
        private float startedAt;
        private float visualStartedAt;

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
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartProductionSizedStaticSilhouetteDiagnostics(int width, int height, int threshold, int targetFps, int targetPresentCount);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteRequestedTargetFps();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteEffectiveTargetFps();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteTargetPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetProductionSizedStaticSilhouettePublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetProductionSizedStaticSilhouetteAppliedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteGenerationEvaluationCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteRegionBuildCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteApplyMessagePostCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteApplyExecutionCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteApplySuccessCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteApplyFailureCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteRawScanlineRunCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteMergedRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteRegionDataRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteRegionDataSizeBytes();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteCoveredPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteExcludedPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetProductionSizedStaticSilhouetteTotalBuildMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetProductionSizedStaticSilhouetteSetWindowRgnMicroseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteInitialGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterRegionBuildGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterRegionApplyGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterRegionRestoreGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterWindowDestroyGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterUiThreadJoinGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteAfterNativeCleanupGdiObjectCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteHrgnCreatedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteHrgnCallerDeletedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteHrgnOwnershipTransferredCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetProductionSizedStaticSilhouetteHrgnRestoreCreatedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetProductionSizedStaticSilhouetteHrgnLiveOwnedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidProductionSizedStaticSilhouetteCounterInvariantsSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidProductionSizedStaticSilhouetteRepresentativeValidationSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_CompleteStaticComplexSilhouetteDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StopStaticComplexSilhouetteDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteInitialRegionRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidStaticComplexSilhouetteInitialStyleRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteBodyCenterInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteLongLeftEarInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteMirroredEarInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteBottomRightTailInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteMirroredTailInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteTransparentCornerInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsStaticComplexSilhouetteTransparentBottomLeftCornerInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionTargetFrameCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetContinuousCompositionElapsedMilliseconds();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_FinalizeCompositionDiagnosticsThread();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCurrentProcessGdiObjectCount();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelectDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotStaticComplexSilhouetteDiagnostics.AutoStartEnabled =
                false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!AutoStartEnabled) return;
            var instance = new GameObject(
                nameof(
                    DesktopMascotProductionSizedStaticSilhouetteDiagnostics));
            DontDestroyOnLoad(instance);
            instance.AddComponent<
                DesktopMascotProductionSizedStaticSilhouetteDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log("[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Diagnostic runInBackground: True");
        }

        private IEnumerator Start()
        {
            yield return null;
            startedAt = Time.realtimeSinceStartup;
            while (!TimedOut() && !PrerequisitesReady()) yield return null;
            if (TimedOut() || !SystemInfo.supportsAsyncGPUReadback)
            {
                managedFailureStage = 25;
                yield return Shutdown();
                yield break;
            }

            DesktopMascotContinuousCompositionDiagnostics.TargetFpsForNextRun =
                RequestedFps;
            DesktopMascotContinuousCompositionDiagnostics.
                StaticComplexSilhouettePatternForNextRun = true;
            DesktopMascotContinuousCompositionDiagnostics.
                ExternalShutdownOwnerForNextRun = true;
            if (DMN_SetContinuousCompositionTargetFrameCountForNextRun(
                    TargetPresents) != 1)
            {
                managedFailureStage = 24;
                yield return Shutdown();
                yield break;
            }
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
                    managedFailureStage = 24;
                    break;
                }
                yield return null;
            }
            if (source == null || managedFailureStage != 0 || TimedOut())
            {
                yield return Shutdown();
                yield break;
            }

            maskHandle = GCHandle.Alloc(mask, GCHandleType.Pinned);
            if (DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                    Size, Size, Size, Threshold) != 1)
            {
                managedFailureStage =
                    DMN_GetCompositionAlphaMaskFailureStage();
                yield return Shutdown();
                yield break;
            }
            readbackPending = true;
            AsyncGPUReadback.Request(source, 0, OnReadback);
            while (!TimedOut() && !readbackComplete
                   && managedFailureStage == 0) yield return null;
            if (!readbackComplete || managedFailureStage != 0)
            {
                yield return Shutdown();
                yield break;
            }

            var startResult =
                DMN_StartProductionSizedStaticSilhouetteDiagnostics(
                    Size, Size, Threshold, RequestedFps, TargetPresents);
            LogStart(startResult);
            if (startResult != 1)
            {
                managedFailureStage = 1;
                yield return Shutdown();
                yield break;
            }
            while (!TimedOut()
                   && DMN_GetProductionSizedStaticSilhouetteState()
                       != RegionRunning
                   && DMN_GetProductionSizedStaticSilhouetteState()
                       != RegionFailed) yield return null;
            if (DMN_GetProductionSizedStaticSilhouetteState()
                != RegionRunning)
            {
                yield return Shutdown();
                yield break;
            }
            LogApplied();
            visualStartedAt = Time.realtimeSinceStartup;
            while (!TimedOut()
                   && Time.realtimeSinceStartup - visualStartedAt
                       < VisualSeconds) yield return null;

            DMN_CompleteStaticComplexSilhouetteDiagnostics();
            DMN_StopStaticComplexSilhouetteDiagnostics();
            while (!TimedOut()
                   && DMN_GetProductionSizedStaticSilhouetteState()
                       != RegionCompleted
                   && DMN_GetProductionSizedStaticSilhouetteState()
                       != RegionFailed) yield return null;
            while (!TimedOut()
                   && DMN_GetContinuousCompositionState()
                       != ContinuousCompleted
                   && DMN_GetContinuousCompositionState()
                       != ContinuousFailed) yield return null;
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while (!TimedOut()
                   && DMN_GetContinuousCompositionState()
                       != ContinuousStopped) yield return null;
            yield return null;
            yield return null;
            DMN_StopCompositionAlphaMaskDiagnostics();
            DMN_RequestCompositionDiagnosticsShutdown();
            while (!TimedOut()
                   && DMN_GetCompositionInitializationState()
                       != CompositionStopped) yield return null;
            if (DMN_FinalizeCompositionDiagnosticsThread() != 1)
                managedFailureStage = 21;

            for (var i = 0; i < finalGdiSamples.Length; i++)
            {
                finalGdiSamples[i] = DMN_GetCurrentProcessGdiObjectCount();
                if (i + 1 < finalGdiSamples.Length)
                    yield return new WaitForSecondsRealtime(0.1f);
            }
            LogFinal();
            yield return Shutdown();
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            readbackPending = false;
            if (request.hasError)
            {
                readbackErrors++;
                managedFailureStage = 8;
                return;
            }
            var data = request.GetData<byte>();
            var normal = OrientationMatches(data, false);
            var flipped = OrientationMatches(data, true);
            if (normal == flipped)
            {
                managedFailureStage = 12;
                return;
            }
            for (var y = 0; y < Size; y++)
            {
                var sy = flipped ? Size - 1 - y : y;
                for (var x = 0; x < Size; x++)
                    mask[y * Size + x] = data[(sy * Size + x) * 4 + 3];
            }
            if (DMN_SubmitCompositionAlphaMask(
                    maskHandle.AddrOfPinnedObject(),
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
            return Hit(data, 124, 152, flip)
                && Hit(data, 60, 32, flip)
                && !Hit(data, 192, 32, flip)
                && Hit(data, 228, 172, flip)
                && !Hit(data, 228, 80, flip)
                && !Hit(data, 248, 8, flip)
                && !Hit(data, 8, 248, flip);
        }

        private static bool Hit(
            NativeArray<byte> data,
            int x,
            int y,
            bool flip)
        {
            var sy = flip ? Size - 1 - y : y;
            return data[(sy * Size + x) * 4 + 3] >= Threshold;
        }

        private static void LogStart(int result)
        {
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Start result: {result}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Width: {DMN_GetProductionSizedStaticSilhouetteWidth()}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Height: {DMN_GetProductionSizedStaticSilhouetteHeight()}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Threshold: {DMN_GetProductionSizedStaticSilhouetteThreshold()}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Requested target FPS: {DMN_GetProductionSizedStaticSilhouetteRequestedTargetFps()}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Effective target FPS: {DMN_GetProductionSizedStaticSilhouetteEffectiveTargetFps()}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Target Present count: {DMN_GetProductionSizedStaticSilhouetteTargetPresentCount()}");
            Debug.Log("[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Expected Present duration seconds: 40");
        }

        private static void LogApplied()
        {
            const string p =
                "[DesktopMascotProductionSizedStaticSilhouetteDiagnostics]";
            Debug.Log($"{p} Published generation: {DMN_GetProductionSizedStaticSilhouettePublishedGeneration()}");
            Debug.Log($"{p} Applied generation: {DMN_GetProductionSizedStaticSilhouetteAppliedGeneration()}");
            Debug.Log($"{p} Generation evaluation count: {DMN_GetProductionSizedStaticSilhouetteGenerationEvaluationCount()}");
            Debug.Log($"{p} Region build count: {DMN_GetProductionSizedStaticSilhouetteRegionBuildCount()}");
            Debug.Log($"{p} Region apply message post count: {DMN_GetProductionSizedStaticSilhouetteApplyMessagePostCount()}");
            Debug.Log($"{p} Region apply execution count: {DMN_GetProductionSizedStaticSilhouetteApplyExecutionCount()}");
            Debug.Log($"{p} Region apply success count: {DMN_GetProductionSizedStaticSilhouetteApplySuccessCount()}");
            Debug.Log($"{p} Region apply failure count: {DMN_GetProductionSizedStaticSilhouetteApplyFailureCount()}");
            Debug.Log($"{p} Raw scanline run count: {DMN_GetProductionSizedStaticSilhouetteRawScanlineRunCount()}");
            Debug.Log($"{p} Merged input rectangle count: {DMN_GetProductionSizedStaticSilhouetteMergedRectangleCount()}");
            Debug.Log($"{p} Region data rectangle count: {DMN_GetProductionSizedStaticSilhouetteRegionDataRectangleCount()}");
            Debug.Log($"{p} Region data size bytes: {DMN_GetProductionSizedStaticSilhouetteRegionDataSizeBytes()}");
            Debug.Log($"{p} Covered pixel count: {DMN_GetProductionSizedStaticSilhouetteCoveredPixelCount()}");
            Debug.Log($"{p} Excluded pixel count: {DMN_GetProductionSizedStaticSilhouetteExcludedPixelCount()}");
            Debug.Log($"{p} Initial GDI object count: {DMN_GetProductionSizedStaticSilhouetteInitialGdiObjectCount()}");
            var raw = DMN_GetProductionSizedStaticSilhouetteRawScanlineRunCount();
            var merged = DMN_GetProductionSizedStaticSilhouetteMergedRectangleCount();
            var finalRectangles =
                DMN_GetProductionSizedStaticSilhouetteRegionDataRectangleCount();
            var covered =
                DMN_GetProductionSizedStaticSilhouetteCoveredPixelCount();
            var reduction = raw >= merged ? raw - merged : 0;
            Debug.Log($"{p} Raw runs per row: {raw / (double)Size:F3}");
            Debug.Log($"{p} Merged rectangles per row: {merged / (double)Size:F3}");
            Debug.Log($"{p} Final region rectangles per row: {finalRectangles / (double)Size:F3}");
            Debug.Log($"{p} Merge reduction count: {reduction}");
            Debug.Log($"{p} Merge reduction percentage: {(raw == 0 ? 0 : reduction * 100.0 / raw):F2}");
            Debug.Log($"{p} Covered pixel percentage: {covered * 100.0 / (Size * Size):F2}");
            Debug.Log($"{p} Total region build time microseconds: {DMN_GetProductionSizedStaticSilhouetteTotalBuildMicroseconds()}");
            Debug.Log($"{p} SetWindowRgn time microseconds: {DMN_GetProductionSizedStaticSilhouetteSetWindowRgnMicroseconds()}");
        }

        private void LogFinal()
        {
            const string p =
                "[DesktopMascotProductionSizedStaticSilhouetteDiagnostics]";
            var sorted = (uint[])finalGdiSamples.Clone();
            Array.Sort(sorted);
            var median = sorted[sorted.Length / 2];
            var stable = sorted[^1] - sorted[0] <= 1;
            var initial =
                DMN_GetProductionSizedStaticSilhouetteInitialGdiObjectCount();
            var delta = (long)median - initial;
            var elapsed = DMN_GetContinuousCompositionElapsedMilliseconds();
            var presents = DMN_GetContinuousCompositionPresentCount();
            var rate = elapsed == 0 ? 0 : presents * 1000.0 / elapsed;
            if (!stable)
                managedFailureStage = 22;
            else if (delta > 2
                     || DMN_GetProductionSizedStaticSilhouetteHrgnLiveOwnedCount()
                         != 0)
                managedFailureStage = 23;
            else if (presents != TargetPresents
                     || DMN_DidContinuousCompositionCompleteNormally() == 0
                     || rate < 25.0 || rate > 35.0)
                managedFailureStage = 24;
            else if (DMN_GetProductionSizedStaticSilhouetteRequestedTargetFps()
                     != DMN_GetProductionSizedStaticSilhouetteEffectiveTargetFps())
                managedFailureStage = 4;
            Debug.Log($"{p} State: {(DMN_GetProductionSizedStaticSilhouetteState() == RegionCompleted ? "Completed" : DMN_GetProductionSizedStaticSilhouetteState().ToString())}");
            Debug.Log($"{p} Failure stage: {DMN_GetProductionSizedStaticSilhouetteFailureStage()}");
            Debug.Log($"{p} Counter invariants valid: {B(DMN_DidProductionSizedStaticSilhouetteCounterInvariantsSucceed())}");
            Debug.Log($"{p} Representative validation succeeded: {B(DMN_DidProductionSizedStaticSilhouetteRepresentativeValidationSucceed())}");
            Debug.Log($"{p} Requested/effective FPS match: {DMN_GetProductionSizedStaticSilhouetteRequestedTargetFps() == DMN_GetProductionSizedStaticSilhouetteEffectiveTargetFps()}");
            Debug.Log($"{p} Body center: {B(DMN_IsStaticComplexSilhouetteBodyCenterInside())}");
            Debug.Log($"{p} Long left ear: {B(DMN_IsStaticComplexSilhouetteLongLeftEarInside())}");
            Debug.Log($"{p} Mirrored right-ear counterpart: {B(DMN_IsStaticComplexSilhouetteMirroredEarInside())}");
            Debug.Log($"{p} Bottom-right tail: {B(DMN_IsStaticComplexSilhouetteBottomRightTailInside())}");
            Debug.Log($"{p} Vertically mirrored tail counterpart: {B(DMN_IsStaticComplexSilhouetteMirroredTailInside())}");
            Debug.Log($"{p} Transparent top-right corner: {B(DMN_IsStaticComplexSilhouetteTransparentCornerInside())}");
            Debug.Log($"{p} Transparent bottom-left corner: {B(DMN_IsStaticComplexSilhouetteTransparentBottomLeftCornerInside())}");
            Debug.Log($"{p} Initial region restored: {B(DMN_DidStaticComplexSilhouetteInitialRegionRestoreSucceed())}");
            Debug.Log($"{p} Initial extended style restored: {B(DMN_DidStaticComplexSilhouetteInitialStyleRestoreSucceed())}");
            Debug.Log($"{p} Initial GDI object count: {initial}");
            Debug.Log($"{p} After region build GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterRegionBuildGdiObjectCount()}");
            Debug.Log($"{p} After region apply GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterRegionApplyGdiObjectCount()}");
            Debug.Log($"{p} After region restore GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterRegionRestoreGdiObjectCount()}");
            Debug.Log($"{p} After composition window destroyed GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterWindowDestroyGdiObjectCount()}");
            Debug.Log($"{p} After UI thread joined GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterUiThreadJoinGdiObjectCount()}");
            Debug.Log($"{p} After native diagnostic cleanup GDI object count: {DMN_GetProductionSizedStaticSilhouetteAfterNativeCleanupGdiObjectCount()}");
            Debug.Log($"{p} Final GDI samples: [{string.Join(", ", finalGdiSamples)}]");
            Debug.Log($"{p} Final stabilized GDI object count: {median}");
            Debug.Log($"{p} Final stabilized GDI delta: {delta}");
            Debug.Log($"{p} GDI samples stable: {stable}");
            Debug.Log($"{p} HRGN created count: {DMN_GetProductionSizedStaticSilhouetteHrgnCreatedCount()}");
            Debug.Log($"{p} HRGN caller-deleted count: {DMN_GetProductionSizedStaticSilhouetteHrgnCallerDeletedCount()}");
            Debug.Log($"{p} HRGN ownership transferred count: {DMN_GetProductionSizedStaticSilhouetteHrgnOwnershipTransferredCount()}");
            Debug.Log($"{p} HRGN restore-created count: {DMN_GetProductionSizedStaticSilhouetteHrgnRestoreCreatedCount()}");
            Debug.Log($"{p} HRGN live-owned count: {DMN_GetProductionSizedStaticSilhouetteHrgnLiveOwnedCount()}");
            Debug.Log($"{p} Present count: {presents}");
            Debug.Log($"{p} Elapsed Present duration milliseconds: {elapsed}");
            Debug.Log($"{p} Measured average Present rate: {rate:F2}");
            Debug.Log($"{p} Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log($"{p} Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log($"{p} Continuous Present successful: {B(DMN_DidContinuousCompositionCompleteNormally())}");
            Debug.Log($"{p} Automated diagnostics passed: {managedFailureStage == 0 && DMN_GetProductionSizedStaticSilhouetteFailureStage() == 0}");
            Debug.Log($"{p} Visual verification pending: True");
        }

        private IEnumerator Shutdown()
        {
            try
            {
                DMN_StopStaticComplexSilhouetteDiagnostics();
                DMN_StopCompositionAlphaMaskDiagnostics();
                DMN_RequestContinuousCompositionDiagnosticsStop();
                if (DMN_GetCompositionInitializationState()
                    != CompositionStopped)
                    DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                Debug.LogError(exception);
            }
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Managed failure stage: {managedFailureStage}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Readback errors: {readbackErrors}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] Shutdown complete: {DMN_GetCompositionInitializationState() == CompositionStopped}");
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] UI thread stopped: {DMN_GetCompositionInitializationState() == CompositionStopped}");
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
            Debug.Log($"[DesktopMascotProductionSizedStaticSilhouetteDiagnostics] runInBackground restored: {runInBackgroundRestored}");
            if (maskHandle.IsAllocated && !readbackPending) maskHandle.Free();
            yield return null;
            Application.Quit();
        }

        private void OnGUI()
        {
            if (visualStartedAt <= 0 || source == null) return;
            var scale = Mathf.Min(
                2.0f,
                (Screen.width - 24.0f) / Size,
                (Screen.height - 130.0f) / Size);
            var drawSize = Size * Mathf.Max(0.25f, scale);
            DesktopMascotValidatedPreview.DrawValidatedTopLeftPreview(
                new Rect(
                    (Screen.width - drawSize) * 0.5f,
                    110,
                    drawSize,
                    drawSize),
                source);
            GUI.Label(
                new Rect(12, 8, Screen.width - 24, 96),
                "Production-Sized Static Silhouette Diagnostics\n"
                + "Resolution 256x256; threshold 128; requested/effective FPS 30\n"
                + $"Present {DMN_GetContinuousCompositionPresentCount()}/{DMN_GetContinuousCompositionTargetFrameCount()}; "
                + $"build {DMN_GetProductionSizedStaticSilhouetteTotalBuildMicroseconds()} us; "
                + $"SetWindowRgn {DMN_GetProductionSizedStaticSilhouetteSetWindowRgnMicroseconds()} us\n"
                + $"Click actual composition window at ({DMN_GetCompositionWindowInitialX()}, {DMN_GetCompositionWindowInitialY()}); preview is not the target.");
        }

        private bool TimedOut() =>
            Time.realtimeSinceStartup - startedAt >= TimeoutSeconds;
        private static bool PrerequisitesReady() =>
            DMN_GetCompositionInitializationState() == CompositionRunning
            && DMN_IsDestinationTextureAvailable() != 0
            && DMN_WasReadbackValidationCompleted() != 0
            && DMN_DidReadbackExpectedOrientationMatch() != 0;
        private static bool B(int value) => value != 0;
        private static string H(int value) =>
            $"0x{unchecked((uint)value):X8}";
        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;
#endif
    }
}
