using System;
using DesktopMascot.Character;
using DesktopMascot.Diagnostics;
using DesktopMascot.Runtime.SingleInstance;
using DesktopMascot.Runtime.CharacterSelection;
using DesktopMascot.Runtime.CharacterPersistence;
using DesktopMascot.Runtime.Settings;
using DesktopMascot.Runtime.Settings.UI;
using DesktopMascot.Runtime.Presentation.Speech;
using DesktopMascot.Runtime.Conversation;
using UnityEngine;

namespace DesktopMascot.Runtime
{
    internal static class DesktopMascotRuntimeBootstrap
    {
        private const string ModeEnvironmentVariable =
            "DESKTOP_MASCOT_MODE";
        private const string ModeArgumentPrefix =
            "--desktop-mascot-mode=";
        private const string RuntimeVrmImportArgumentPrefix =
            "--runtime-vrm-import=";
        private const string DevelopmentLaunchArgument =
            "--desktop-mascot-development-launch";
        private const string SpeechManualCheckEnvironmentVariable =
            "DESKTOP_MASCOT_SPEECH_MANUAL_CHECK";
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static string selectedMode = "runtime";
        private static WindowPositionPersistence positionPersistence;
        private static SettingsManager settingsManager;
        private static CharacterSelectionPersistenceManager
            characterPersistence;
        private static GameObject runtimeOwner;
#endif

        internal static bool IsNormalRuntimeRequested()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return ReadMode() == "runtime";
#else
            return false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelectMode()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            selectedMode = ReadMode();
            if (selectedMode == "runtime"
                && SingleInstanceStartupGate
                    .ShouldSkipNormalRuntimeStartup())
            {
                SingleInstanceStartupGate
                    .RequestSecondaryOrderlyExit();
                return;
            }
            Debug.Log(
                "[DesktopMascotProductIdentity] Application.companyName: " +
                Application.companyName);
            Debug.Log(
                "[DesktopMascotProductIdentity] Application.productName: " +
                Application.productName);
            Debug.Log($"[DesktopMascotRuntime] Selected mode: {selectedMode}");
            if (selectedMode == "drag-diagnostic")
            {
                DesktopMascotSettingsDiagnostics.RunFocusedTests();
                DesktopMascotSingleInstanceDiagnostics.RunFocusedTests();
                DesktopMascotCharacterPersistenceDiagnostics
                    .RunFocusedTests();
                NativeMascotClickCompletionDiagnostics.RunFocusedTests();
            }
            settingsManager = selectedMode == "runtime"
                ? SettingsManager.CreateProduction()
                : SettingsManager.CreateIsolated(selectedMode);
            var settingsResult = settingsManager.Initialize();
            Debug.Log(
                "[DesktopMascotSettings] Storage path: " +
                settingsManager.StoragePath);
            Debug.Log(
                "[DesktopMascotSettings] Initialization status: " +
                settingsResult.Status);
            Debug.Log(
                "[DesktopMascotSettings] Current schema/settings/first-run: " +
                $"{settingsManager.Current.SchemaVersion}/" +
                $"{settingsManager.Current.SettingsVersion}/" +
                settingsManager.Current.FirstRunCompleted);
            if (selectedMode == "runtime"
                || selectedMode == "message-window-diagnostic")
            {
                if (selectedMode == "message-window-diagnostic")
                {
                    characterPersistence =
                        CharacterSelectionPersistenceManager.CreateIsolated(
                            "message-window-diagnostic");
                }
                else
                {
                    var developmentImportRequested =
                        IsDevelopmentLaunchRequested()
                        && ReadRuntimeVrmImportPath() != null;
                    characterPersistence = developmentImportRequested
                        ? CharacterSelectionPersistenceManager.CreateIsolated(
                            "development-import")
                        : CharacterSelectionPersistenceManager
                            .CreateProduction();
                }
                var persistenceStatus =
                    characterPersistence.Initialize();
                Debug.Log(
                    "[DesktopMascotCharacterPersistence] Startup status/" +
                    "available/record present: " +
                    $"{persistenceStatus}/" +
                    $"{characterPersistence.IsAvailable}/" +
                    characterPersistence.HasStoredRecord);
            }
            if (selectedMode == "drag-diagnostic")
            {
                DesktopMascotSettingsUiDiagnostics.RunFocusedTests();
                DesktopMascotPlayerVisibilityDiagnostics.RunFocusedTests();
            }
            positionPersistence = selectedMode == "runtime"
                ? WindowPositionPersistence.CreateProduction()
                : selectedMode == "drag-diagnostic"
                    ? WindowPositionPersistence.CreateIsolated()
                    : WindowPositionPersistence.CreateDisabled();
            if (selectedMode == "drag-diagnostic")
            {
                DesktopMascotWindowPositionPersistenceDiagnostics
                    .RunFocusedTests();
            }
            var initialPosition =
                positionPersistence.PrepareInitialPosition(256, 256);
            if (!NativeMascotWindowPositionBridge.TrySetInitialPosition(
                    initialPosition,
                    out var initialPositionError))
            {
                Debug.LogError(
                    "[DesktopMascotWindowPosition] Native initial position " +
                    $"configuration failed: {initialPositionError}");
            }
            else
            {
                Debug.Log(
                    "[DesktopMascotWindowPosition] Native initial position " +
                    $"configured: {initialPosition}");
            }
            DisableRegionDiagnostics();
            if (selectedMode == "real-static-diagnostic")
                DesktopMascotRealMascotStaticAlphaDiagnostics.AutoStartEnabled =
                    true;
            else if (selectedMode == "real-animated-diagnostic")
                DesktopMascotRealMascotAnimatedAlphaDiagnostics
                    .AutoStartEnabled = true;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRuntime()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (selectedMode == "runtime"
                && SingleInstanceStartupGate
                    .ShouldSkipNormalRuntimeStartup())
            {
                return;
            }
            if (selectedMode != "runtime"
                && selectedMode != "runtime-smoke"
                && selectedMode != "drag-diagnostic"
                && selectedMode != "message-window-diagnostic")
                return;
            if (runtimeOwner != null)
                return;

            if (selectedMode == "drag-diagnostic")
            {
                DesktopMascotCharacterAssetDiagnostics
                    .RunFocusedTests();
            }

            CharacterAssetManager characterAssetManager = null;
            if (selectedMode == "runtime"
                || selectedMode == "message-window-diagnostic")
            {
                characterAssetManager = selectedMode == "runtime"
                    ? CharacterAssetManager.GetOrCreateProduction()
                    : CharacterAssetManager.CreateForFocusedDiagnostics();
                var characterResult =
                    characterAssetManager != null
                        ? characterAssetManager
                            .InitializeBundledDefault()
                        : new CharacterAssetInitializationResult(
                            CharacterAssetInitializationStatus
                                .ShutdownStarted,
                            null);
                Debug.Log(
                    "[DesktopMascotCharacterAsset] Startup result: " +
                    characterResult.Status);
                if (!ShouldCreateRuntimeSurfaces(characterResult))
                {
                    Debug.LogError(
                        "[DesktopMascotCharacterAsset] Fatal startup " +
                        "failure; runtime surfaces were not created.");
                    characterAssetManager?.BeginShutdown();
                    characterAssetManager?.Cleanup();
                    characterPersistence?.BeginShutdown();
                    characterPersistence?.Cleanup();
                    SingleInstanceStartupGate
                        .RequestPrimaryStartupAbortOrderlyExit(
                            "character asset initialization failed");
                    return;
                }
                Debug.Log(
                    "[DesktopMascotCharacterAsset] Diagnostic/production " +
                    "manager count: " +
                    $"{(selectedMode == "message-window-diagnostic" ? 1 : 0)}/" +
                    CharacterAssetManager.ProductionInstanceCount);
            }

            var owner = new GameObject(nameof(DesktopMascotRuntimePipeline));
            UnityEngine.Object.DontDestroyOnLoad(owner);
            runtimeOwner = owner;
            DesktopMascotSpeechMascotScaleDiagnostics scaleDiagnostics = null;
            if (selectedMode == "message-window-diagnostic")
            {
                scaleDiagnostics = owner.AddComponent<
                    DesktopMascotSpeechMascotScaleDiagnostics>();
                if (!scaleDiagnostics.Configure(
                        Camera.main,
                        characterAssetManager))
                {
                    Debug.LogError(
                        "[DesktopMascotSpeechDiagnostics] Diagnostic " +
                        "mascot framing adjustment failed.");
                }
            }
            var runtime = owner.AddComponent<DesktopMascotRuntimePipeline>();
            runtime.AttachSpeechCameraIsolationDiagnostics(scaleDiagnostics);
            runtime.AttachCharacterAssetManager(characterAssetManager);
            runtime.AttachCharacterSelectionPersistenceManager(
                characterPersistence);
            var runtimeVrmPath =
                selectedMode == "runtime"
                && IsDevelopmentLaunchRequested()
                    ? ReadRuntimeVrmImportPath()
                    : null;
            DesktopMascotPlayerPreviewPresentation playerPresentation = null;
            if (selectedMode == "runtime"
                || selectedMode == "drag-diagnostic"
                || selectedMode == "message-window-diagnostic")
            {
                var previewOwner = new GameObject(
                    nameof(DesktopMascotPlayerPreviewPresentation));
                UnityEngine.Object.DontDestroyOnLoad(previewOwner);
                var candidate = previewOwner.AddComponent<
                    DesktopMascotPlayerPreviewPresentation>();
                if (candidate.Initialize(Camera.main))
                {
                    playerPresentation = candidate;
                }
                else
                {
                    UnityEngine.Object.Destroy(previewOwner);
                    Debug.LogError(
                        "[DesktopMascotSettingsUI] Full-quality Player " +
                        "preview initialization failed.");
                }
            }
            runtime.Configure(
                selectedMode == "runtime-smoke",
                positionPersistence,
                playerPresentation);
            if (selectedMode == "runtime")
            {
                runtime.AttachMascotClickConversationController(
                    new MascotClickConversationController());
            }
            if (selectedMode == "message-window-diagnostic")
            {
                var speech = owner.AddComponent<SpeechPresentationController>();
                if (!speech.Initialize(characterAssetManager, Camera.main))
                {
                    Debug.LogError(
                        "[DesktopMascotSpeechDiagnostics] Initialization failed.");
                }
                else
                {
                    runtime.AttachSpeechPresentationController(speech);
                    var speechDiagnostics = owner.AddComponent<
                        DesktopMascotSpeechPresentationDiagnostics>();
                    speechDiagnostics.Configure(
                        speech,
                        runtime,
                        scaleDiagnostics,
                        playerPresentation,
                        Environment.GetEnvironmentVariable(
                            SpeechManualCheckEnvironmentVariable));
                }
            }
            SettingsWindowController settingsWindow = null;
            UnityPlayerWindowVisibilityController playerVisibility = null;
            if (playerPresentation != null)
            {
                settingsWindow = owner.AddComponent<SettingsWindowController>();
                playerVisibility = owner.AddComponent<
                    UnityPlayerWindowVisibilityController>();
                playerVisibility.Initialize(
                    settingsWindow,
                    selectedMode == "runtime",
                    presentationSource: playerPresentation);
                RuntimeCharacterSelectionController characterSelection = null;
                if (selectedMode == "runtime"
                    || selectedMode == "message-window-diagnostic")
                {
                    characterSelection = owner.AddComponent<
                        RuntimeCharacterSelectionController>();
                    characterSelection.Configure(
                        characterAssetManager,
                        Camera.main,
                        playerVisibility,
                        persistenceManager: characterPersistence,
                        enableStartupRestore:
                            selectedMode == "runtime"
                            && runtimeVrmPath == null);
                    runtime.AttachCharacterSelectionController(
                        characterSelection);
                }
                settingsWindow.Initialize(
                    settingsManager,
                    playerPresentation,
                    characterSelection);
                if (selectedMode == "runtime")
                {
                    runtime.AttachPlayerVisibilityController(
                        playerVisibility);
                    var singleInstance = owner.AddComponent<
                        SingleInstanceController>();
                    singleInstance.Configure(playerVisibility);
                    runtime.AttachSingleInstanceController(
                        singleInstance);
                    if (runtimeVrmPath != null)
                    {
                        var importDiagnostics = owner.AddComponent<
                            DesktopMascotRuntimeVrmImportDiagnostics>();
                        importDiagnostics.Configure(
                            characterSelection,
                            runtimeVrmPath);
                        var selectionDiagnostics = owner.AddComponent<
                            DesktopMascotRuntimeCharacterSelectionDiagnostics>();
                        selectionDiagnostics.Configure(
                            characterSelection,
                            importDiagnostics,
                            runtimeVrmPath);
                    }
                }
                if (selectedMode == "drag-diagnostic")
                {
                    var presentationDiagnostics = owner.AddComponent<
                        DesktopMascotSettingsUiPresentationDiagnostics>();
                    presentationDiagnostics.Configure(
                        settingsWindow,
                        playerPresentation,
                        runtime,
                        playerVisibility);
                }
            }
            if (settingsWindow != null)
            {
                var contextMenu = owner.AddComponent<
                    NativeMascotContextMenuController>();
                contextMenu.Configure(
                    settingsWindow,
                    runtime,
                    playerVisibility);
                runtime.AttachContextMenuController(contextMenu);
                var systemTray = owner.AddComponent<
                    NativeSystemTrayController>();
                systemTray.Configure();
                runtime.AttachSystemTrayController(systemTray);
                if (selectedMode == "drag-diagnostic")
                {
                    var contextDiagnostics = owner.AddComponent<
                        DesktopMascotNativeContextMenuDiagnostics>();
                    contextDiagnostics.Configure(
                        contextMenu,
                        settingsWindow,
                        positionPersistence,
                        settingsManager);
                    var trayDiagnostics = owner.AddComponent<
                        DesktopMascotSystemTrayDiagnostics>();
                    trayDiagnostics.Configure(
                        systemTray,
                        contextMenu,
                        settingsWindow,
                        positionPersistence,
                        settingsManager);
                }
            }
            if (selectedMode == "drag-diagnostic")
            {
                var diagnostics = new GameObject(
                    nameof(DesktopMascotNativeDragDiagnostics));
                UnityEngine.Object.DontDestroyOnLoad(diagnostics);
                diagnostics.AddComponent<
                    DesktopMascotNativeDragDiagnostics>();
            }
#endif
        }

        internal static bool ShouldCreateRuntimeSurfaces(
            CharacterAssetInitializationResult characterResult)
        {
            return characterResult.Succeeded;
        }

        private static void DisableRegionDiagnostics()
        {
            DesktopMascotRealMascotStaticAlphaDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotRealMascotAnimatedAlphaDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics
                .AutoStartEnabled = false;
            DesktopMascotProductionSizedStaticSilhouetteDiagnostics
                .AutoStartEnabled = false;
            DesktopMascotStaticComplexSilhouetteDiagnostics.AutoStartEnabled =
                false;
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled =
                false;
        }

        private static string ReadMode()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith(
                    ModeArgumentPrefix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeMode(
                        argument.Substring(ModeArgumentPrefix.Length));
                }
            }
            return NormalizeMode(
                Environment.GetEnvironmentVariable(
                    ModeEnvironmentVariable));
        }

        private static string NormalizeMode(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "runtime-smoke":
                case "real-static-diagnostic":
                case "real-animated-diagnostic":
                case "drag-diagnostic":
                case "message-window-diagnostic":
                    return value.Trim().ToLowerInvariant();
                default:
                    return "runtime";
            }
        }

        private static string ReadRuntimeVrmImportPath()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                var normalizedArgument = argument?.Trim();
                if (normalizedArgument != null
                    && normalizedArgument.Length >= 2
                    && normalizedArgument[0] == '"'
                    && normalizedArgument[
                        normalizedArgument.Length - 1] == '"')
                {
                    normalizedArgument = normalizedArgument.Substring(
                        1,
                        normalizedArgument.Length - 2);
                }
                if (normalizedArgument != null
                    && normalizedArgument.StartsWith(
                    RuntimeVrmImportArgumentPrefix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedArgument.Substring(
                        RuntimeVrmImportArgumentPrefix.Length);
                }
            }
            return null;
        }

        private static bool IsDevelopmentLaunchRequested()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(
                    argument?.Trim().Trim('"'),
                    DevelopmentLaunchArgument,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
