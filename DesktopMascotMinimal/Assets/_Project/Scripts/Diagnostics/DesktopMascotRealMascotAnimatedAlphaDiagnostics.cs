using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DesktopMascot.Character;
using DesktopMascot.Interaction;
using DesktopMascot.Runtime;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotRealMascotAnimatedAlphaDiagnostics :
        MonoBehaviour
    {
        internal static bool AutoStartEnabled { get; set; }
        private const string Dll = "DesktopMascotNative";
        private const string Prefix =
            "[DesktopMascotRealMascotAnimatedAlphaDiagnostics]";
        private const int Size = 256;
        private const int Threshold = 128;
        private const int TargetPresents = 1200;
        private const float ActiveSeconds = 32.0f;
        private const float PublishSeconds = 0.25f;
        private const float TimeoutSeconds = 58.0f;

        private readonly byte[] mask = new byte[Size * Size];
        private readonly uint[] finalGdi = new uint[5];
        private readonly bool[] phaseObserved = new bool[4];
        private readonly ulong[] phaseGeneration = new ulong[4];
        private readonly uint[] phaseCovered = new uint[4];
        private readonly RectInt[] phaseBounds = new RectInt[4];
        private readonly ulong[] phaseHash = new ulong[4];
        private readonly HashSet<ulong> distinctHashes = new HashSet<ulong>();
        private readonly List<(Behaviour behaviour, bool enabled)> disabled =
            new List<(Behaviour, bool)>();

        private Camera mascotCamera;
        private Animator mascotAnimator;
        private DesktopMascotCameraSourcePipeline cameraSourcePipeline;
        private RenderTexture cameraTexture;
        private RenderTexture transferTexture;
        private float originalAnimatorSpeed;
        private bool previousRunInBackground;
        private bool nativeTextureAvailable;
        private bool readbackPending;
        private bool stopPublishing;
        private bool animationProgressed;
        private bool regionStarted;
        private int requestPhase;
        private int readbackErrors;
        private int invalidOrEmptyMasks;
        private int managedFailureStage;
        private ulong generation;
        private float startedAt;
        private float visualStartedAt;
        private float nextReadbackAt;
        private float initialNormalizedTime;
        private GCHandle pinnedMask;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionInitializationState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidReadbackVerticallyFlippedOrientationMatch();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
            int width, int height, int stride, int threshold);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_SubmitCompositionAlphaMask(
            IntPtr data, int width, int height, int stride, ulong maskGeneration);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_SetContinuousCompositionTargetFrameCountForNextRun(uint count);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_StartRealMascotAnimatedAlphaDiagnostics(
            int width, int height, int threshold, int fps,
            int presentCount, int publishMilliseconds);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_SetRealMascotAnimatedPhaseForGeneration(
            int phase, ulong maskGeneration);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();

        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetProductionSizedAnimatedSilhouetteState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetProductionSizedAnimatedSilhouetteFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouettePublishedGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteGenerationEvaluationCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyMessagePostCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyExecutionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteDuplicateMaskSkipCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededGenerationCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouettePendingHrgnReplacementCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMinimumBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMinimumSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong DMN_GetContinuousCompositionElapsedMilliseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_FinalizeCompositionDiagnosticsThread();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetCurrentProcessGdiObjectCount();

        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidProductionSizedAnimatedSilhouettePhaseApply(int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetProductionSizedAnimatedSilhouettePhaseLastAppliedGeneration(
                int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(
                int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(
                int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(
                int phase);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetProductionSizedAnimatedSilhouettePhaseHash(int phase);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelectDiagnostic()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!AutoStartEnabled)
                return;
            DesktopMascotRealMascotStaticAlphaDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics
                .AutoStartEnabled = false;
            DesktopMascotProductionSizedStaticSilhouetteDiagnostics
                .AutoStartEnabled = false;
            DesktopMascotStaticComplexSilhouetteDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled =
                false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostic()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!AutoStartEnabled)
                return;
            var owner =
                new GameObject(nameof(DesktopMascotRealMascotAnimatedAlphaDiagnostics));
            DontDestroyOnLoad(owner);
            owner.AddComponent<DesktopMascotRealMascotAnimatedAlphaDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            startedAt = Time.realtimeSinceStartup;
            Log($"Previous runInBackground: {previousRunInBackground}");
            Log("Diagnostic runInBackground: True");

            while (!TimedOut() && !PrerequisitesReady())
                yield return null;
            if (!CreateTargets() || !PrepareMascotAnimation())
            {
                managedFailureStage = 1;
                yield return Shutdown();
                yield break;
            }

            Log("Width and height: 256 x 256");
            Log($"Graphics format: {transferTexture.graphicsFormat}");
            Log("Threshold: 128");
            Log("Camera RenderTexture availability: True");
            Log("Transfer RenderTexture availability: True");
            Log("Camera normalization active: True");
            Log("Camera source normalized with explicit Blit Y transform: True");
            Log("All region generation uses normalized transfer texture: True");
            Log("DirectComposition presentation uses normalized transfer texture: True");
            Log("Camera normalization is a localized source-boundary correction; downstream orientation is unchanged.");
            Log("Requested FPS: 30");
            Log("Effective FPS: 30");
            Log("Target Present count: 1200");
            Log("Publish interval: 250 ms");
            Log("Active animation duration: 32 seconds");
            Log($"Preview uses validated UV transform: {DesktopMascotValidatedPreview.UsesValidatedUvTransform}");

            DesktopMascotContinuousCompositionDiagnostics.TargetFpsForNextRun =
                30;
            DesktopMascotContinuousCompositionDiagnostics
                .ExternalSourceTextureForNextRun = transferTexture;
            DesktopMascotContinuousCompositionDiagnostics
                .ExternalShutdownOwnerForNextRun = true;
            DMN_SetContinuousCompositionTargetFrameCountForNextRun(
                TargetPresents);
            var continuous =
                new GameObject(nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuous);
            continuous.AddComponent<DesktopMascotContinuousCompositionDiagnostics>();

            pinnedMask = GCHandle.Alloc(mask, GCHandleType.Pinned);
            if (DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                    Size, Size, Size, Threshold) != 1)
            {
                managedFailureStage = 2;
                yield return Shutdown();
                yield break;
            }

            visualStartedAt = Time.realtimeSinceStartup;
            nextReadbackAt = visualStartedAt;
            while (!TimedOut()
                   && Time.realtimeSinceStartup - visualStartedAt
                       < ActiveSeconds
                   && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()
                       == 0)
            {
                yield return new WaitForEndOfFrame();
                NormalizeCameraSource();
                ObserveAnimationProgress();
                ScheduleReadback();
                if (regionStarted)
                    DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
            }

            stopPublishing = true;
            while (!TimedOut() && readbackPending)
                yield return null;
            if (!regionStarted)
                managedFailureStage = 3;

            while (!TimedOut() && regionStarted
                   && (DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()
                           != generation
                       || DMN_GetProductionSizedAnimatedSilhouetteGenerationEvaluationCount()
                           != generation)
                   && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()
                       == 0)
            {
                DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
                yield return null;
            }

            if (regionStarted)
            {
                DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
                DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
                while (!TimedOut()
                       && DMN_GetProductionSizedAnimatedSilhouetteState() != 9
                       && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()
                           == 0)
                    yield return null;
            }
            DMN_StopCompositionAlphaMaskDiagnostics();

            while (!TimedOut()
                   && DMN_GetContinuousCompositionState() != 10
                   && DMN_GetContinuousCompositionState() != 13)
            {
                yield return new WaitForEndOfFrame();
                NormalizeCameraSource();
            }
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while (!TimedOut()
                   && DMN_GetContinuousCompositionState() != 12)
                yield return null;
            DMN_RequestCompositionDiagnosticsShutdown();
            while (!TimedOut()
                   && DMN_GetCompositionInitializationState() != 17)
                yield return null;
            DMN_FinalizeCompositionDiagnosticsThread();

            for (var index = 0; index < finalGdi.Length; ++index)
            {
                finalGdi[index] = DMN_GetCurrentProcessGdiObjectCount();
                if (index + 1 < finalGdi.Length)
                    yield return new WaitForSecondsRealtime(0.1f);
            }
            LogSummary();
            yield return Shutdown();
        }

        private bool CreateTargets()
        {
            mascotCamera = Camera.main;
            if (!DesktopMascotCameraSourcePipeline.TryCreate(
                    mascotCamera,
                    Size,
                    Size,
                    "Real Mascot Animated Camera Source Diagnostic",
                    "Real Mascot Animated Normalized Transfer Diagnostic",
                    out cameraSourcePipeline))
                return false;
            cameraTexture = cameraSourcePipeline.CameraSourceTexture;
            transferTexture =
                cameraSourcePipeline.NormalizedTransferTexture;
            return true;
        }

        private bool PrepareMascotAnimation()
        {
            mascotAnimator = FindFirstObjectByType<Animator>();
            if (mascotAnimator == null
                || mascotAnimator.runtimeAnimatorController == null)
                return false;
            originalAnimatorSpeed = mascotAnimator.speed;
            mascotAnimator.speed = 1.0f;
            var idle = Animator.StringToHash("Base Layer.Standing Idle");
            if (mascotAnimator.HasState(0, idle))
                mascotAnimator.Play(idle, 0, 0.0f);
            initialNormalizedTime =
                mascotAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;

            Disable(FindFirstObjectByType<MascotExpressionController>());
            Disable(FindFirstObjectByType<MascotInteractionController>());
            Disable(FindFirstObjectByType<MascotDragController>());
            Log($"Actual mascot available: True");
            Log($"Animator available: True");
            Log($"Animator controller: {mascotAnimator.runtimeAnimatorController.name}");
            return true;
        }

        private void Disable(Behaviour behaviour)
        {
            if (behaviour == null)
                return;
            disabled.Add((behaviour, behaviour.enabled));
            behaviour.enabled = false;
        }

        private void NormalizeCameraSource()
        {
            cameraSourcePipeline.Normalize(Time.frameCount);
            nativeTextureAvailable =
                cameraSourcePipeline.GetNormalizedNativeTexturePointer()
                    != IntPtr.Zero;
        }

        private void ObserveAnimationProgress()
        {
            var info = mascotAnimator.GetCurrentAnimatorStateInfo(0);
            if (Mathf.Abs(info.normalizedTime - initialNormalizedTime) > 0.1f)
                animationProgressed = true;
        }

        private void ScheduleReadback()
        {
            if (stopPublishing || readbackPending
                || Time.realtimeSinceStartup < nextReadbackAt)
                return;
            var normalized =
                mascotAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            requestPhase = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Repeat(normalized, 1.0f) * 4.0f),
                0, 3);
            readbackPending = true;
            nextReadbackAt = Time.realtimeSinceStartup + PublishSeconds;
            AsyncGPUReadback.Request(transferTexture, 0, OnReadback);
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            readbackPending = false;
            if (request.hasError)
            {
                ++readbackErrors;
                return;
            }
            var pixels = request.GetData<byte>();
            var covered = 0u;
            var minX = Size;
            var minY = Size;
            var maxX = -1;
            var maxY = -1;
            var hash = 14695981039346656037UL;
            for (var y = 0; y < Size; ++y)
            for (var x = 0; x < Size; ++x)
            {
                var alpha = pixels[(y * Size + x) * 4 + 3];
                mask[y * Size + x] = alpha;
                var inside = alpha >= Threshold;
                hash ^= inside ? 1UL : 0UL;
                hash *= 1099511628211UL;
                if (!inside)
                    continue;
                ++covered;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            if (covered == 0 || covered == Size * Size)
            {
                ++invalidOrEmptyMasks;
                return;
            }

            var nextGeneration = generation + 1;
            if (DMN_SetRealMascotAnimatedPhaseForGeneration(
                    requestPhase, nextGeneration) != 1
                || DMN_SubmitCompositionAlphaMask(
                    pinnedMask.AddrOfPinnedObject(),
                    Size, Size, Size, nextGeneration) != 1)
            {
                ++invalidOrEmptyMasks;
                return;
            }
            generation = nextGeneration;
            phaseObserved[requestPhase] = true;
            phaseGeneration[requestPhase] = generation;
            phaseCovered[requestPhase] = covered;
            phaseBounds[requestPhase] =
                new RectInt(
                    minX, minY, maxX - minX + 1, maxY - minY + 1);
            phaseHash[requestPhase] = hash;
            distinctHashes.Add(hash);

            if (!regionStarted)
            {
                var result = DMN_StartRealMascotAnimatedAlphaDiagnostics(
                    Size, Size, Threshold, 30, TargetPresents, 250);
                regionStarted = result == 1;
                Log($"Start result: {result}");
                Log($"Transfer native texture pointer availability: {nativeTextureAvailable}");
            }
        }

        private void LogSummary()
        {
            var sorted = (uint[])finalGdi.Clone();
            Array.Sort(sorted);
            var initialGdi =
                DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
            var stabilizedDelta = (long)sorted[2] - initialGdi;
            var gdiStable = sorted[4] - sorted[0] <= 1;
            var elapsed = DMN_GetContinuousCompositionElapsedMilliseconds();
            var presentCount = DMN_GetContinuousCompositionPresentCount();
            var fps = elapsed == 0 ? 0.0 : presentCount * 1000.0 / elapsed;
            var allObserved = Array.TrueForAll(phaseObserved, value => value);
            var allApplied = true;
            for (var phase = 0; phase < 4; ++phase)
            {
                var applied =
                    DMN_DidProductionSizedAnimatedSilhouettePhaseApply(phase)
                    != 0;
                allApplied &= applied;
                var bounds = phaseBounds[phase];
                Log(
                    $"Phase {phase}: observed={phaseObserved[phase]}, applied={applied}, " +
                    $"managed generation={phaseGeneration[phase]}, last applied generation=" +
                    $"{DMN_GetProductionSizedAnimatedSilhouettePhaseLastAppliedGeneration(phase)}, " +
                    $"covered={phaseCovered[phase]}, bbox=({bounds.x},{bounds.y}," +
                    $"{bounds.width},{bounds.height}), raw runs=" +
                    $"{DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(phase)}, " +
                    $"merged rectangles={DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(phase)}, " +
                    $"final complexity={DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(phase)}, " +
                    $"managed mask hash=0x{phaseHash[phase]:X16}, native mask hash=0x" +
                    $"{DMN_GetProductionSizedAnimatedSilhouettePhaseHash(phase):X16}");
            }

            var evaluations =
                DMN_GetProductionSizedAnimatedSilhouetteGenerationEvaluationCount();
            var builds =
                DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
            var duplicates =
                DMN_GetProductionSizedAnimatedSilhouetteDuplicateMaskSkipCount();
            var automated =
                regionStarted
                && DMN_GetProductionSizedAnimatedSilhouetteState() == 9
                && DMN_GetProductionSizedAnimatedSilhouetteFailureStage() == 0
                && mascotAnimator != null && animationProgressed
                && cameraTexture != null && cameraTexture.IsCreated()
                && transferTexture != null && transferTexture.IsCreated()
                && nativeTextureAvailable
                && generation > 0 && distinctHashes.Count >= 2
                && allObserved && allApplied
                && evaluations == generation
                && DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()
                    == generation
                && builds > 0 && builds + duplicates == evaluations
                && DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount()
                    == 0
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount()
                    <= 1
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount()
                    == 0
                && DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds()
                    <= 5000
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds()
                    <= 15000
                && DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds()
                    <= 5000
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds()
                    <= 15000
                && DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed()
                    != 0
                && DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()
                    != 0
                && DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()
                    == 0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()
                    != 0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()
                    != 0
                && presentCount == TargetPresents
                && DMN_GetContinuousCompositionLastPresentHRESULT() == 0
                && DMN_GetContinuousCompositionLastDeviceRemovedReason() == 0
                && DMN_DidContinuousCompositionCompleteNormally() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0
                && readbackErrors == 0 && invalidOrEmptyMasks == 0
                && gdiStable && stabilizedDelta <= 2;
            if (!automated && managedFailureStage == 0)
                managedFailureStage = 30;

            Log($"State: {DMN_GetProductionSizedAnimatedSilhouetteState()} (Completed=9)");
            Log($"Failure stage: {DMN_GetProductionSizedAnimatedSilhouetteFailureStage()}");
            Log($"Animation is playing: {animationProgressed}");
            Log($"Published generation count: {generation}");
            Log($"Applied generation: {DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()}");
            Log($"Generation evaluation count: {evaluations}");
            Log($"Valid mask evaluation count: {generation}");
            Log($"Empty or invalid mask count: {invalidOrEmptyMasks}");
            Log($"Distinct binary mask hash count: {distinctHashes.Count}");
            Log($"Region build count: {builds}");
            Log($"Duplicate mask skip count: {duplicates}");
            Log($"Superseded generation count: {DMN_GetProductionSizedAnimatedSilhouetteSupersededGenerationCount()}");
            Log($"Apply message post/execution/success/failure counts: {DMN_GetProductionSizedAnimatedSilhouetteApplyMessagePostCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplyExecutionCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount()}");
            Log($"Maximum pending apply message count: {DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount()}");
            Log($"Maximum pending owned-region count: {DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount()}");
            Log($"Pending replacement/superseded built/deleted counts: {DMN_GetProductionSizedAnimatedSilhouettePendingHrgnReplacementCount()}/{DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount()}/{DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount()}");
            Log($"All phases observed: {allObserved}");
            Log($"All phases applied: {allApplied}");
            Log($"Region build min/max/average microseconds: {DMN_GetProductionSizedAnimatedSilhouetteMinimumBuildMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds()}");
            Log($"SetWindowRgn min/max/average microseconds: {DMN_GetProductionSizedAnimatedSilhouetteMinimumSetWindowRgnMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds()}");
            Log($"HRGN created/caller-deleted/transferred/validation/live: {DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()}");
            Log($"HRGN ownership invariant: {DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()!=0}");
            Log($"GDI initial/min/max/last: {initialGdi}/{DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount()}");
            Log($"Final GDI samples: [{string.Join(", ", finalGdi)}]; stabilized delta: {stabilizedDelta}; stable: {gdiStable}");
            Log($"Present count: {presentCount}");
            Log($"Elapsed time: {elapsed} ms");
            Log($"Measured FPS: {fps:F2}");
            Log($"Present HRESULT: 0x{DMN_GetContinuousCompositionLastPresentHRESULT():X8}");
            Log($"Device removed HRESULT: 0x{DMN_GetContinuousCompositionLastDeviceRemovedReason():X8}");
            Log($"Initial region restored: {DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()!=0}");
            Log($"Initial style restored: {DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()!=0}");
            Log($"Shared GPU/readback expected orientation match: {DMN_DidReadbackExpectedOrientationMatch()!=0}");
            Log($"Shared GPU/readback vertically flipped orientation match: {DMN_DidReadbackVerticallyFlippedOrientationMatch()!=0}");
            Log($"Automated diagnostics passed: {automated}");
            Log("Visual verification pending: True");
        }

        private IEnumerator Shutdown()
        {
            stopPublishing = true;
            while (readbackPending && !TimedOut())
                yield return null;
            if (pinnedMask.IsAllocated)
                pinnedMask.Free();
            foreach (var item in disabled)
                if (item.behaviour != null)
                    item.behaviour.enabled = item.enabled;
            if (mascotAnimator != null)
                mascotAnimator.speed = originalAnimatorSpeed;
            cameraSourcePipeline?.Dispose();
            cameraSourcePipeline = null;
            transferTexture = null;
            cameraTexture = null;
            Application.runInBackground = previousRunInBackground;
            Log($"Managed failure stage: {managedFailureStage}");
            Log($"Readback errors: {readbackErrors}");
            Log($"runInBackground restored: {Application.runInBackground == previousRunInBackground}");
            yield return null;
            Application.Quit();
        }

        private void OnGUI()
        {
            if (transferTexture == null)
                return;
            var previous = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1.0f);
            GUI.DrawTexture(
                new Rect(0, 0, Screen.width, Screen.height),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = previous;
            var scale = Mathf.Min(1.5f, (Screen.height - 100.0f) / Size);
            DesktopMascotValidatedPreview.DrawValidatedTopLeftPreview(
                new Rect(
                    (Screen.width - Size * scale) / 2.0f,
                    80.0f, Size * scale, Size * scale),
                transferTexture);
            GUI.Label(
                new Rect(10, 5, Screen.width - 20, 70),
                $"Real Mascot Animated Alpha Diagnostics\n" +
                $"Present {DMN_GetContinuousCompositionPresentCount()}/{TargetPresents}\n" +
                $"Published masks {generation}");
        }

        private bool TimedOut()
        {
            return Time.realtimeSinceStartup - startedAt >= TimeoutSeconds;
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState() == 14
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0;
        }

        private static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }
#endif
    }
}
