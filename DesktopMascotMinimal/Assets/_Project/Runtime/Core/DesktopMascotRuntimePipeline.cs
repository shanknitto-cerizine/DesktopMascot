using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using DesktopMascot.Character;
using DesktopMascot.Runtime.SingleInstance;
using DesktopMascot.Runtime.CharacterSelection;
using DesktopMascot.Runtime.CharacterPersistence;
using DesktopMascot.Runtime.Settings.UI;
using DesktopMascot.Runtime.Presentation.Speech;
using DesktopMascot.Runtime.Conversation;
using DesktopMascot.Diagnostics;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Runtime
{
    internal sealed class DesktopMascotRuntimePipeline : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotRuntime]";
        private const int ContinuousFrameEventId = 8;
        private const int CompositionReady = 14;
        private const int CompositionStopped = 17;
        private const int ContinuousReadyForFrame = 2;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int RegionCompleted = 9;
        private const int DxgiStatusOccluded = 0x087A0001;
        private const float SmokeTestSeconds = 8.0f;

        [SerializeField]
        private DesktopMascotRuntimeConfiguration configuration =
            new DesktopMascotRuntimeConfiguration();

        private readonly byte[] alphaMask = new byte[256 * 256];
        private DesktopMascotCameraSourcePipeline cameraPipeline;
        private CommandBuffer presentCommandBuffer;
        private GCHandle pinnedAlphaMask;
        private IntPtr normalizedNativeTexturePointer;
        private IntPtr renderEventCallback;
        private bool previousRunInBackground;
        private bool smokeTest;
        private bool initialized;
        private bool alphaMaskStarted;
        private bool regionStarted;
        private bool continuousStarted;
        private bool nativeDragEnabled;
        private bool readbackPending;
        private bool stopPublishing;
        private bool shutdownStarted;
        private bool nativeShutdownCompleted;
        private bool managedCleanupCompleted;
        private ISettingsPlayerPresentationSource playerPresentation;
        private NativeMascotContextMenuController contextMenuController;
        private NativeSystemTrayController systemTrayController;
        private UnityPlayerWindowVisibilityController
            playerVisibilityController;
        private SingleInstanceController singleInstanceController;
        private bool playerPresentationCleanupComplete = true;
        private CharacterAssetManager characterAssetManager;
        private bool characterAssetCleanupComplete = true;
        private RuntimeVrmCharacterSource runtimeVrmCharacterSource;
        private bool runtimeVrmSourceCleanupComplete = true;
        private bool runtimeVrmShutdownWaitExceeded;
        private RuntimeCharacterSelectionController
            characterSelectionController;
        private bool characterSelectionCleanupComplete = true;
        private CharacterSelectionPersistenceManager
            characterSelectionPersistence;
        private SpeechPresentationController speechPresentationController;
        private bool speechPresentationCleanupComplete = true;
        private DesktopMascotSpeechMascotScaleDiagnostics
            speechCameraIsolationDiagnostics;
        private bool speechCameraIsolationCleanupComplete = true;
        private bool characterPersistenceCleanupComplete = true;
        private MascotClickConversationController mascotClickConversationController;

        internal int NativeTransferWidth =>
            cameraPipeline?.NormalizedTransferTexture?.width ?? 0;
        internal int NativeTransferHeight =>
            cameraPipeline?.NormalizedTransferTexture?.height ?? 0;
        internal bool IsInitialized => initialized;
        internal bool ShutdownStarted => shutdownStarted;
        internal ulong RegionPublicationCount => maskGeneration;
        internal uint PresentCount
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return continuousStarted
                    ? DMN_GetContinuousCompositionPresentCount()
                    : 0;
#else
                return 0;
#endif
            }
        }
        private bool quitAfterShutdown;
        private bool cameraTargetTextureRestored;
        private bool runInBackgroundRestored;
        private ulong maskGeneration;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private ulong lastLoggedCharacterTransferGeneration =
            ulong.MaxValue;
#endif
        private int fatalFailureStage;
        private int readbackErrors;
        private float nextReadbackAt;
        private float runtimeStartedAt;
        private double nextPresentAt;
        private Stopwatch presentClock;
        private WindowPositionPersistence positionPersistence;

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
        private static extern IntPtr DMN_GetRenderEventAndDataFunc();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_EnableContinuousCompositionRuntimeModeForNextRun();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_StartContinuousCompositionDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_RequestContinuousCompositionFrame();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_PollContinuousCompositionDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                int width, int height, int stride, int threshold);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_SubmitCompositionAlphaMask(
            IntPtr data, int width, int height, int stride, ulong generation);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_StartRuntimeAlphaRegion(
            int width, int height, int threshold,
            int publishIntervalMilliseconds);
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
        private static extern int
            DMN_GetProductionSizedAnimatedSilhouetteState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_GetProductionSizedAnimatedSilhouetteFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetAnimatedWindowRegionFinalGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetAnimatedWindowRegionGdiObjectDelta();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_FinalizeCompositionDiagnosticsThread();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_EnableNativeMascotWindowDrag();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_DisableNativeMascotWindowDrag();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeMascotWindowDragging();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeMascotWindowCaptureOwned();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotMenuLiveOwnedCount();
#endif

        internal void Configure(
            bool smokeTestEnabled,
            WindowPositionPersistence persistence,
            ISettingsPlayerPresentationSource presentation = null)
        {
            smokeTest = smokeTestEnabled;
            positionPersistence =
                persistence ?? WindowPositionPersistence.CreateDisabled();
            playerPresentation = presentation;
            playerPresentationCleanupComplete =
                playerPresentation == null;
        }

        internal void RequestOrderlyQuit(string reason)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            quitAfterShutdown = true;
            characterSelectionPersistence?.BeginShutdown();
            runtimeVrmCharacterSource?.BeginShutdown();
            characterSelectionController?.BeginShutdown();
            characterAssetManager?.BeginShutdown();
            singleInstanceController?.BeginShutdown();
            playerVisibilityController?.BeginShutdown();
            speechPresentationController?.BeginShutdown();
            mascotClickConversationController?.BeginShutdown();
            if (!shutdownStarted)
                StartCoroutine(Shutdown(reason, true));
#endif
        }

        internal void AttachCharacterAssetManager(
            CharacterAssetManager manager)
        {
            characterAssetManager = manager;
            characterAssetCleanupComplete =
                characterAssetManager == null;
        }

        internal void AttachRuntimeVrmCharacterSource(
            RuntimeVrmCharacterSource source)
        {
            runtimeVrmCharacterSource = source;
            runtimeVrmSourceCleanupComplete =
                runtimeVrmCharacterSource == null;
        }

        internal void AttachCharacterSelectionController(
            RuntimeCharacterSelectionController controller)
        {
            characterSelectionController = controller;
            characterSelectionCleanupComplete =
                characterSelectionController == null;
        }

        internal void AttachCharacterSelectionPersistenceManager(
            CharacterSelectionPersistenceManager manager)
        {
            characterSelectionPersistence = manager;
            characterPersistenceCleanupComplete =
                characterSelectionPersistence == null;
        }

        internal void AttachContextMenuController(
            NativeMascotContextMenuController controller)
        {
            contextMenuController = controller;
        }

        internal void AttachSystemTrayController(
            NativeSystemTrayController controller)
        {
            systemTrayController = controller;
        }

        internal void AttachPlayerVisibilityController(
            UnityPlayerWindowVisibilityController controller)
        {
            playerVisibilityController = controller;
        }

        internal void AttachSingleInstanceController(
            SingleInstanceController controller)
        {
            singleInstanceController = controller;
        }

        internal void AttachSpeechPresentationController(
            SpeechPresentationController controller)
        {
            speechPresentationController = controller;
            speechPresentationCleanupComplete = controller == null;
        }

        internal void AttachSpeechCameraIsolationDiagnostics(
            DesktopMascotSpeechMascotScaleDiagnostics diagnostics)
        {
            speechCameraIsolationDiagnostics = diagnostics;
            speechCameraIsolationCleanupComplete = diagnostics == null;
        }

        internal void AttachMascotClickConversationController(
            MascotClickConversationController controller)
        {
            mascotClickConversationController = controller;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            Application.wantsToQuit += WantsToQuit;
        }

        private IEnumerator Start()
        {
            var runtime = Run();
            Exception exception = null;
            while (true)
            {
                var moved = false;
                object current = null;
                try
                {
                    moved = runtime.MoveNext();
                    if (moved)
                        current = runtime.Current;
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                if (exception != null || !moved)
                    break;
                yield return current;
            }

            if (exception == null)
                yield break;
            Fail(13, $"Unhandled runtime exception: {exception}");
            yield return Shutdown("exception", smokeTest);
        }

        private IEnumerator Run()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (!configuration.IsValid)
            {
                Fail(1, "Invalid runtime configuration.");
                yield return Shutdown("invalid configuration", smokeTest);
                yield break;
            }

            for (var frame = 0; frame < 600 && !PrerequisitesReady(); ++frame)
                yield return null;
            if (!PrerequisitesReady())
            {
                Fail(2, "Native composition prerequisites did not become ready.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }

            if (!DesktopMascotCameraSourcePipeline.TryCreate(
                    Camera.main,
                    configuration.textureWidth,
                    configuration.textureHeight,
                    "Desktop Mascot Runtime Camera Source",
                    "Desktop Mascot Runtime Normalized Transfer",
                    out cameraPipeline))
            {
                Fail(3, "Camera RenderTexture pipeline creation failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            if (!cameraPipeline.Normalize(Time.frameCount))
            {
                Fail(4, "Initial Camera-source normalization failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }
            normalizedNativeTexturePointer =
                cameraPipeline.GetNormalizedNativeTexturePointer();
            renderEventCallback = DMN_GetRenderEventAndDataFunc();
            if (normalizedNativeTexturePointer == IntPtr.Zero
                || renderEventCallback == IntPtr.Zero)
            {
                Fail(5, "Native texture pointer or render callback unavailable.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }

#if DEVELOPMENT_BUILD
            Debug.Assert(
                cameraPipeline.IsNormalizedTransfer(
                    cameraPipeline.NormalizedTransferTexture),
                "Region and composition must receive the normalized transfer texture.");
            Debug.Assert(
                !DesktopMascotCameraSourcePipeline.UsesShaderSideInversion,
                "Shader-side inversion must remain disabled.");
#endif

            if (DMN_EnableContinuousCompositionRuntimeModeForNextRun() != 1)
            {
                Fail(6, "Native continuous runtime mode setup failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }
            var compositionStart =
                DMN_StartContinuousCompositionDiagnostics();
            if (compositionStart != 1)
            {
                Fail(6, "Native continuous composition start failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }
            continuousStarted = true;
            if (DMN_StartAnimatedCompositionAlphaMaskDiagnostics(
                    configuration.textureWidth,
                    configuration.textureHeight,
                    configuration.textureWidth,
                    configuration.alphaThreshold) != 1)
            {
                Fail(7, "Native alpha-mask publication start failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }
            alphaMaskStarted = true;
            nativeDragEnabled =
                DMN_EnableNativeMascotWindowDrag() == 1;
            if (!nativeDragEnabled)
            {
                Fail(14, "Native mascot window drag initialization failed.");
                yield return Shutdown("startup failure", smokeTest);
                yield break;
            }
            mascotClickConversationController?.Initialize();
            pinnedAlphaMask = GCHandle.Alloc(
                alphaMask, GCHandleType.Pinned);
            presentCommandBuffer = new CommandBuffer
            {
                name = "Desktop Mascot Runtime Present"
            };
            presentCommandBuffer.IssuePluginEventAndData(
                renderEventCallback,
                ContinuousFrameEventId,
                normalizedNativeTexturePointer);
            presentClock = Stopwatch.StartNew();
            runtimeStartedAt = Time.realtimeSinceStartup;
            nextReadbackAt = runtimeStartedAt;
            initialized = true;

            Log($"Initialization result: {initialized}");
            Log($"Width and height: {configuration.textureWidth} x {configuration.textureHeight}");
            Log($"Format: {cameraPipeline.NormalizedTransferTexture.graphicsFormat}");
            Log($"Threshold: {configuration.alphaThreshold}");
            Log($"Region update interval: {configuration.regionUpdateIntervalMilliseconds} ms");
            Log("Camera normalization active: True");
            Log($"Normalized native texture pointer available: {normalizedNativeTexturePointer != IntPtr.Zero}");
            Log($"Composition start result: {compositionStart}");
            Log($"Native mascot window drag enabled: {nativeDragEnabled}");
            Log(
                $"Window position persistence enabled: {positionPersistence.Enabled}");
            if (positionPersistence.Enabled)
            {
                Log(
                    $"Window position storage path: {positionPersistence.StoragePath}");
            }
            Log("Production runtime auto-exit timer active: False");
            Log($"Smoke-test-only auto-exit timer active: {smokeTest}");
            if (characterSelectionController != null)
            {
                var restoreRequested =
                    characterSelectionController.RequestStartupRestore();
                Log(
                    "Character startup restore requested: " +
                    restoreRequested);
            }

            while (!shutdownStarted)
            {
                yield return new WaitForEndOfFrame();
                if (shutdownStarted)
                    yield break;
                mascotClickConversationController?.Pump();
                if (!cameraPipeline.Normalize(Time.frameCount))
                {
                    Fail(8, "Camera-source normalization guard failed.");
                    yield return Shutdown("runtime failure", smokeTest);
                    yield break;
                }
                PumpPresent();
                PumpMaskReadback();
                characterAssetManager?
                    .PumpRetiredDisposalsAfterEndOfFrame();
                if (playerVisibilityController != null
                    && !playerVisibilityController.RuntimeReady
                    && (DMN_GetContinuousCompositionPresentCount() > 0
                        || DMN_GetContinuousCompositionLastPresentHRESULT()
                            == DxgiStatusOccluded)
                    && regionStarted)
                {
                    playerVisibilityController.NotifyRuntimeReady();
                }
                positionPersistence.PollCompletedDrag(
                    DMN_IsNativeMascotWindowDragging() != 0,
                    DMN_IsNativeMascotWindowCaptureOwned() != 0);
                if (regionStarted)
                    DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
                if (DMN_GetContinuousCompositionState() == ContinuousFailed
                    || DMN_GetProductionSizedAnimatedSilhouetteFailureStage()
                        != 0)
                {
                    Log($"Native continuous/region failure stages: {DMN_GetContinuousCompositionFailureStage()}/{DMN_GetProductionSizedAnimatedSilhouetteFailureStage()}");
                    Fail(9, "Native runtime path reported a failure.");
                    yield return Shutdown("runtime failure", smokeTest);
                    yield break;
                }
                if (smokeTest
                    && Time.realtimeSinceStartup - runtimeStartedAt
                        >= SmokeTestSeconds)
                {
                    LogSmokeResult();
                    yield return Shutdown("runtime smoke test complete", true);
                    yield break;
                }
            }
        }

        private void PumpPresent()
        {
            var state = DMN_PollContinuousCompositionDiagnostics();
            if (state != ContinuousReadyForFrame
                || presentClock.Elapsed.TotalSeconds < nextPresentAt)
                return;
            if (DMN_RequestContinuousCompositionFrame() != 1)
                return;
            Graphics.ExecuteCommandBuffer(presentCommandBuffer);
            nextPresentAt = Math.Max(
                nextPresentAt
                    + 1.0 / configuration.targetPresentFps,
                presentClock.Elapsed.TotalSeconds
                    + 1.0 / configuration.targetPresentFps);
        }

        private void PumpMaskReadback()
        {
            if (stopPublishing || readbackPending
                || Time.realtimeSinceStartup < nextReadbackAt)
                return;
            readbackPending = true;
            nextReadbackAt = Time.realtimeSinceStartup
                + configuration.regionUpdateIntervalMilliseconds / 1000.0f;
            AsyncGPUReadback.Request(
                cameraPipeline.NormalizedTransferTexture,
                0,
                OnMaskReadback);
        }

        private void OnMaskReadback(AsyncGPUReadbackRequest request)
        {
            readbackPending = false;
            if (shutdownStarted)
            {
                return;
            }
            if (request.hasError)
            {
                ++readbackErrors;
                Fail(10, "Runtime alpha-mask readback failed.");
                return;
            }
            var pixels = request.GetData<byte>();
            var covered = 0;
            var minimumX = configuration.textureWidth;
            var minimumY = configuration.textureHeight;
            var maximumX = -1;
            var maximumY = -1;
            for (var index = 0; index < alphaMask.Length; ++index)
            {
                var alpha = pixels[index * 4 + 3];
                alphaMask[index] = alpha;
                if (alpha >= configuration.alphaThreshold)
                {
                    ++covered;
                    var x = index % configuration.textureWidth;
                    var y = index / configuration.textureWidth;
                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
            }
            LogCharacterTransferSample(
                covered,
                minimumX,
                minimumY,
                maximumX,
                maximumY);
            if (covered == 0 || covered == alphaMask.Length)
                return;

            var nextGeneration = maskGeneration + 1;
            if (DMN_SubmitCompositionAlphaMask(
                    pinnedAlphaMask.AddrOfPinnedObject(),
                    configuration.textureWidth,
                    configuration.textureHeight,
                    configuration.textureWidth,
                    nextGeneration) != 1)
            {
                Fail(11, "Runtime alpha-mask publication failed.");
                return;
            }
            maskGeneration = nextGeneration;
            if (!regionStarted)
            {
                regionStarted = DMN_StartRuntimeAlphaRegion(
                    configuration.textureWidth,
                    configuration.textureHeight,
                    configuration.alphaThreshold,
                    configuration.regionUpdateIntervalMilliseconds) == 1;
                if (!regionStarted)
                    Fail(12, "Native runtime region start failed.");
            }
        }

        private void LogCharacterTransferSample(
            int covered,
            int minimumX,
            int minimumY,
            int maximumX,
            int maximumY)
        {
            if (characterAssetManager == null)
                return;
            var generation =
                characterAssetManager.ActiveCharacterGeneration;
            if (generation == lastLoggedCharacterTransferGeneration)
                return;
            lastLoggedCharacterTransferGeneration = generation;
            var width = maximumX >= minimumX
                ? maximumX - minimumX + 1
                : 0;
            var height = maximumY >= minimumY
                ? maximumY - minimumY + 1
                : 0;
            Log(
                "Character transfer sample source/generation/" +
                "covered/bounds: " +
                $"{characterAssetManager.ActiveCharacter?.SourceType}/" +
                $"{generation}/{covered}/" +
                $"({minimumX},{minimumY},{width},{height})");
        }

        private IEnumerator Shutdown(string reason, bool quitAfterCleanup)
        {
            quitAfterShutdown |= quitAfterCleanup;
            if (shutdownStarted)
                yield break;
            shutdownStarted = true;
            stopPublishing = true;
            characterSelectionPersistence?.BeginShutdown();
            runtimeVrmCharacterSource?.BeginShutdown();
            characterSelectionController?.BeginShutdown();
            characterAssetManager?.BeginShutdown();
            singleInstanceController?.BeginShutdown();
            playerVisibilityController?.BeginShutdown();
            systemTrayController?.BeginShutdown();
            contextMenuController?.BeginShutdown();
            speechPresentationController?.BeginShutdown();
            mascotClickConversationController?.BeginShutdown();
            Log($"Shutdown reason: {reason}");
            if (characterSelectionController != null)
            {
                while (characterSelectionController
                    .DialogShutdownWaitActive)
                {
                    yield return null;
                }
                Log(
                    "Character selection dialog shutdown wait " +
                    "milliseconds: " +
                    characterSelectionController
                        .DialogShutdownWaitMilliseconds);
                var waitStartedAt = Time.realtimeSinceStartup;
                var waitWarningLogged = false;
                while (characterSelectionController.HasPendingOperation)
                {
                    if (!waitWarningLogged
                        && characterSelectionController.ImportInProgress
                        && Time.realtimeSinceStartup - waitStartedAt
                            >= 30.0f)
                    {
                        waitWarningLogged = true;
                        runtimeVrmShutdownWaitExceeded = true;
                        Debug.LogWarning(
                            $"{Prefix} Runtime VRM selection shutdown " +
                            "wait exceeded 30000 ms; cooperative wait " +
                            "continues.");
                    }
                    yield return null;
                }
            }
            if (runtimeVrmCharacterSource != null)
            {
                var waitStartedAt = Time.realtimeSinceStartup;
                var waitWarningLogged = false;
                while (runtimeVrmCharacterSource.ImportInProgress)
                {
                    if (!waitWarningLogged
                        && Time.realtimeSinceStartup - waitStartedAt
                            >= 30.0f)
                    {
                        waitWarningLogged = true;
                        runtimeVrmShutdownWaitExceeded = true;
                        Debug.LogWarning(
                            $"{Prefix} Runtime VRM import shutdown " +
                            "wait exceeded 30000 ms; cooperative wait " +
                            "continues because UniVRM 0.131.0 cannot " +
                            "abort LoadAsync immediately.");
                    }
                    yield return null;
                }
            }
            if (nativeDragEnabled)
            {
                DMN_DisableNativeMascotWindowDrag();
                nativeDragEnabled = false;
            }
            for (var frame = 0;
                 frame < 120
                 && (DMN_IsNativeMascotWindowDragging() != 0
                     || DMN_IsNativeMascotWindowCaptureOwned() != 0);
                 ++frame)
            {
                yield return null;
            }
            if (regionStarted)
                DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
            for (var frame = 0; frame < 120 && readbackPending; ++frame)
                yield return null;

            if (regionStarted)
            {
                DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
                for (var frame = 0;
                     frame < 300
                     && DMN_GetProductionSizedAnimatedSilhouetteState()
                         != RegionCompleted;
                     ++frame)
                {
                    DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
                    yield return null;
                }
            }
            if (alphaMaskStarted)
                DMN_StopCompositionAlphaMaskDiagnostics();
            if (continuousStarted)
            {
                DMN_RequestContinuousCompositionDiagnosticsStop();
                for (var frame = 0;
                     frame < 300
                     && DMN_GetContinuousCompositionState()
                         != ContinuousStopped;
                     ++frame)
                {
                    DMN_PollContinuousCompositionDiagnostics();
                    yield return null;
                }
            }
            var noOutstandingReadback = !readbackPending;
            var pendingOwnedRegionCount = regionStarted
                ? DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()
                : 0;
            var initialGdiObjectCount = regionStarted
                ? DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount()
                : 0;
            var finalGdiObjectCount = regionStarted
                ? DMN_GetAnimatedWindowRegionFinalGdiObjectCount()
                : 0;
            var gdiObjectDelta = regionStarted
                ? DMN_GetAnimatedWindowRegionGdiObjectDelta()
                : 0;
            var initialRegionRestored = !regionStarted
                || DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()
                    != 0;
            var initialStyleRestored = !regionStarted
                || DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()
                    != 0;
            var regionCompleted = !regionStarted
                || DMN_GetProductionSizedAnimatedSilhouetteState()
                    == RegionCompleted;
            var presentResult =
                DMN_GetContinuousCompositionLastPresentHRESULT();
            var deviceRemovedResult =
                DMN_GetContinuousCompositionLastDeviceRemovedReason();
            var finalPresentCount =
                DMN_GetContinuousCompositionPresentCount();
            var finalRegionPublicationCount = maskGeneration;
            var runtimeElapsedMilliseconds =
                presentClock?.ElapsedMilliseconds ?? 0;
            var continuousFailureStage = continuousStarted
                ? DMN_GetContinuousCompositionFailureStage()
                : 0;
            var regionFailureStage = regionStarted
                ? DMN_GetProductionSizedAnimatedSilhouetteFailureStage()
                : 0;
            var draggingAfterStop =
                DMN_IsNativeMascotWindowDragging() != 0;
            var captureOwnedAfterStop =
                DMN_IsNativeMascotWindowCaptureOwned() != 0;
            var liveOwnedMenuCount =
                DMN_GetNativeMascotMenuLiveOwnedCount();
            var trayCleanupSucceeded =
                systemTrayController == null
                || systemTrayController.CleanupSucceeded;
            var trayIconRemoved =
                systemTrayController == null
                || systemTrayController.IconRemoved;
            var trayOwnerDestroyed =
                systemTrayController == null
                || systemTrayController.OwnerDestroyed;
            var trayPopupClosed =
                systemTrayController == null
                || systemTrayController.PopupClosed;
            var trayLiveOwnedMenuCount =
                systemTrayController?.LiveOwnedMenuCount ?? 0;
            var trayLiveOwnedIconCount =
                systemTrayController?.LiveOwnedIconCount ?? 0;
            positionPersistence.SaveFinalCompletedPosition(
                draggingAfterStop,
                captureOwnedAfterStop);
            DMN_RequestCompositionDiagnosticsShutdown();
            for (var frame = 0;
                 frame < 300
                 && DMN_GetCompositionInitializationState()
                     != CompositionStopped;
                 ++frame)
                yield return null;
            DMN_FinalizeCompositionDiagnosticsThread();
            nativeShutdownCompleted = true;
            CleanupManaged();
            var singleInstanceCleanupSucceeded =
                singleInstanceController == null
                || singleInstanceController.CompleteShutdown();
            var runtimeCleanupStateSucceeded =
                noOutstandingReadback
                && pendingOwnedRegionCount == 0
                && initialRegionRestored
                && initialStyleRestored
                && regionCompleted
                && continuousFailureStage == 0
                && regionFailureStage == 0
                && !draggingAfterStop
                && !captureOwnedAfterStop
                && liveOwnedMenuCount == 0
                && trayCleanupSucceeded
                && trayIconRemoved
                && trayOwnerDestroyed
                && trayPopupClosed
                && trayLiveOwnedMenuCount == 0
                && trayLiveOwnedIconCount == 0
                && cameraTargetTextureRestored
                && runInBackgroundRestored
                && playerPresentationCleanupComplete
                && characterAssetCleanupComplete
                && runtimeVrmSourceCleanupComplete
                && characterSelectionCleanupComplete
                && speechPresentationCleanupComplete
                && speechCameraIsolationCleanupComplete
                && characterPersistenceCleanupComplete
                && !runtimeVrmShutdownWaitExceeded
                && singleInstanceCleanupSucceeded
                && presentResult == 0
                && deviceRemovedResult == 0
                && readbackErrors == 0;
            Log($"No outstanding readback: {noOutstandingReadback}");
            Log($"Pending owned region count: {pendingOwnedRegionCount}");
            Log($"Initial/final GDI object count: {initialGdiObjectCount}/{finalGdiObjectCount}");
            Log($"GDI object delta: {gdiObjectDelta}");
            Log($"Initial region restored: {initialRegionRestored}");
            Log($"Initial style restored: {initialStyleRestored}");
            Log($"Camera.targetTexture restored: {cameraTargetTextureRestored}");
            Log($"runInBackground restored: {runInBackgroundRestored}");
            Log(
                "Player preview cleanup result: " +
                playerPresentationCleanupComplete);
            Log(
                "Character asset cleanup result: " +
                characterAssetCleanupComplete);
            Log(
                "Runtime VRM source cleanup result: " +
                runtimeVrmSourceCleanupComplete);
            Log(
                "Runtime character selection cleanup result: " +
                characterSelectionCleanupComplete);
            Log(
                "Speech presentation cleanup result: " +
                speechPresentationCleanupComplete);
            Log(
                "Speech Camera isolation cleanup result: " +
                speechCameraIsolationCleanupComplete);
            Log(
                "Character persistence cleanup result: " +
                characterPersistenceCleanupComplete);
            Log(
                "Runtime VRM shutdown wait exceeded: " +
                runtimeVrmShutdownWaitExceeded);
            Log(
                "Single instance cleanup result: " +
                singleInstanceCleanupSucceeded);
            Log($"Present HRESULT: {FormatHResult(presentResult)}");
            Log($"Device removed HRESULT: {FormatHResult(deviceRemovedResult)}");
            Log($"Readback errors: {readbackErrors}");
            Log($"Native continuous/region failure stages: {continuousFailureStage}/{regionFailureStage}");
            Log($"Native mascot dragging after stop: {draggingAfterStop}");
            Log($"Native mascot capture owned after stop: {captureOwnedAfterStop}");
            Log($"Native popup menu live-owned count: {liveOwnedMenuCount}");
            Log($"Tray cleanup result: {trayCleanupSucceeded}");
            Log($"Tray icon removed: {trayIconRemoved}");
            Log($"Tray owner destroyed: {trayOwnerDestroyed}");
            Log($"Tray popup closed: {trayPopupClosed}");
            Log($"Tray live-owned menu/icon counts: {trayLiveOwnedMenuCount}/{trayLiveOwnedIconCount}");
            if (systemTrayController != null)
            {
                Log(
                    "Tray delete requests: " +
                    systemTrayController.DeleteRequestCount);
                Log(
                    "Tray owner created/destroyed: " +
                    $"{systemTrayController.OwnerCreatedCount}/" +
                    systemTrayController.OwnerDestroyedCount);
            }
            Log($"Runtime elapsed milliseconds: {runtimeElapsedMilliseconds}");
            Log($"Final Present count: {finalPresentCount}");
            Log($"Final region publication count: {finalRegionPublicationCount}");
            Log($"Cleanup result: {runtimeCleanupStateSucceeded}");
            Log($"Fatal failure stage: {fatalFailureStage}");
            if (quitAfterShutdown)
            {
                var closePosted =
                    UnityPlayerWindowVisibilityController
                        .TryPostOrderlyClose();
                Log(
                    "Managed orderly UnityWndClass WM_CLOSE posted: " +
                    closePosted);
                Log("Managed orderly Application.Quit requested.");
                Application.Quit();
            }
        }

        private void LogSmokeResult()
        {
            var passed =
                initialized
                && maskGeneration > 0
                && regionStarted
                && DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount()
                    > 0
                && DMN_GetContinuousCompositionPresentCount() > 0
                && DMN_GetContinuousCompositionLastPresentHRESULT() == 0
                && DMN_GetContinuousCompositionLastDeviceRemovedReason() == 0
                && fatalFailureStage == 0;
            Log($"Smoke test Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Log($"Smoke test mask generations: {maskGeneration}");
            Log($"Smoke test region builds: {DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount()}");
            Log($"Smoke test passed: {passed}");
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState() == CompositionReady
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0;
        }

        private void Fail(int stage, string message)
        {
            if (fatalFailureStage == 0)
                fatalFailureStage = stage;
            Debug.LogError($"{Prefix} {message}");
        }

        private void CleanupManaged()
        {
            if (managedCleanupCompleted || readbackPending)
                return;
            if (characterSelectionController?.HasPendingOperation == true
                || runtimeVrmCharacterSource?.ImportInProgress == true)
            {
                Log(
                    "Managed cleanup deferred for pending runtime " +
                    "character operation.");
                return;
            }
            if (!nativeShutdownCompleted
                && characterAssetManager?.ActiveCharacter?.SourceType
                    == CharacterAssetSourceType.RuntimeImported)
            {
                Log(
                    "Runtime-owned character cleanup deferred until " +
                    "native and Camera pipeline stop.");
                return;
            }
            managedCleanupCompleted = true;
            speechPresentationCleanupComplete =
                speechPresentationController == null
                || speechPresentationController.Cleanup();
            Log(
                "Speech presentation cleanup result: " +
                speechPresentationCleanupComplete);
            speechPresentationController = null;
            Application.wantsToQuit -= WantsToQuit;
            presentCommandBuffer?.Release();
            presentCommandBuffer = null;
            if (pinnedAlphaMask.IsAllocated && !readbackPending)
                pinnedAlphaMask.Free();
            if (cameraPipeline != null)
            {
                cameraPipeline.Dispose();
                cameraTargetTextureRestored =
                    cameraPipeline.CameraTargetTextureRestored;
            }
            else
            {
                cameraTargetTextureRestored = true;
            }
            cameraPipeline = null;
            if (playerPresentation != null)
            {
                playerPresentation.Dispose();
                playerPresentationCleanupComplete =
                    playerPresentation.CleanupComplete;
                playerPresentation = null;
            }
            speechCameraIsolationCleanupComplete =
                speechCameraIsolationDiagnostics == null
                || speechCameraIsolationDiagnostics.Restore();
            Log(
                "Speech Camera isolation cleanup result: " +
                speechCameraIsolationCleanupComplete);
            speechCameraIsolationDiagnostics = null;
            if (characterSelectionController != null)
            {
                characterSelectionCleanupComplete =
                    characterSelectionController.Cleanup();
                if (characterSelectionCleanupComplete)
                    characterSelectionController = null;
            }
            if (characterAssetManager != null)
            {
                var ownedReleased =
                    characterAssetManager
                        .ReleaseActiveOwnedCharacterAfterPipelineStop();
                Log(
                    "Runtime active/retired handles after release: " +
                    $"{characterAssetManager.ActiveOwnedRuntimeHandleCount}/" +
                    characterAssetManager.RetiredOwnedRuntimeHandleCount);
                characterAssetCleanupComplete =
                    ownedReleased
                    && characterAssetManager.Cleanup();
                characterAssetManager = null;
            }
            if (runtimeVrmCharacterSource != null)
            {
                runtimeVrmSourceCleanupComplete =
                    runtimeVrmCharacterSource.Cleanup();
                if (runtimeVrmSourceCleanupComplete)
                    runtimeVrmCharacterSource = null;
            }
            if (characterSelectionPersistence != null)
            {
                characterPersistenceCleanupComplete =
                    characterSelectionPersistence.Cleanup();
                Log(
                    "Character persistence cleanup result: " +
                    characterPersistenceCleanupComplete);
                if (characterPersistenceCleanupComplete)
                    characterSelectionPersistence = null;
            }
            if (mascotClickConversationController != null)
            {
                mascotClickConversationController.Cleanup();
                mascotClickConversationController = null;
            }
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
        }

        private void RequestEmergencyShutdown(string reason)
        {
            if (nativeShutdownCompleted)
            {
                CleanupManaged();
                return;
            }
            stopPublishing = true;
            shutdownStarted = true;
            characterSelectionPersistence?.BeginShutdown();
            runtimeVrmCharacterSource?.BeginShutdown();
            characterSelectionController?.BeginShutdown();
            characterAssetManager?.BeginShutdown();
            singleInstanceController?.BeginShutdown();
            systemTrayController?.BeginShutdown();
            contextMenuController?.BeginShutdown();
            speechPresentationController?.BeginShutdown();
            mascotClickConversationController?.BeginShutdown();
            try
            {
                if (regionStarted)
                    DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
                if (alphaMaskStarted)
                    DMN_StopCompositionAlphaMaskDiagnostics();
                if (continuousStarted)
                    DMN_RequestContinuousCompositionDiagnosticsStop();
                if (nativeDragEnabled)
                {
                    DMN_DisableNativeMascotWindowDrag();
                    nativeDragEnabled = false;
                }
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception)
            {
                Debug.LogError($"{Prefix} Emergency shutdown failed: {exception}");
            }
            Log($"Shutdown reason: {reason}");
            CleanupManaged();
            singleInstanceController?.CompleteShutdown();
        }

        private void OnDisable()
        {
            if (!shutdownStarted)
                RequestEmergencyShutdown("OnDisable");
        }

        private void OnDestroy()
        {
            if (!shutdownStarted)
                RequestEmergencyShutdown("OnDestroy");
            else
                CleanupManaged();
        }

        private void OnApplicationQuit()
        {
            if (!nativeShutdownCompleted)
                RequestEmergencyShutdown("application quit");
        }

        private bool WantsToQuit()
        {
            if (nativeShutdownCompleted)
                return true;
            if (!shutdownStarted
                && playerVisibilityController != null
                && playerVisibilityController
                    .TryHandleOrdinaryPlayerClose())
            {
                Log("Ordinary Player close converted to Settings hide.");
                return false;
            }
            quitAfterShutdown = true;
            if (!shutdownStarted)
                StartCoroutine(Shutdown("application quit", true));
            return false;
        }

        private static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        private static string FormatHResult(int value)
        {
            return value == 0 ? "S_OK" : $"0x{value:X8}";
        }
#endif
    }
}
