using System;
using System.Collections;
using System.IO;
using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterSelection;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotRuntimeVrmImportDiagnostics
        : MonoBehaviour
    {
        private const string Prefix =
            "[DesktopMascotRuntimeVrmImport]";

        private CharacterAssetManager manager;
        private RuntimeCharacterSelectionController selection;
        private string importPath;
        private bool configured;

        internal RuntimeVrmImportStatus ImportStatus { get; private set; }
        internal RuntimeCharacterPreparationStatus PreparationStatus
        {
            get;
            private set;
        }
        internal CharacterActivationStatus ActivationStatus
        {
            get;
            private set;
        }
        internal bool ValidationCompleted { get; private set; }
        internal bool ValidationPassed { get; private set; }

        internal void Configure(
            RuntimeCharacterSelectionController selectionController,
            string path)
        {
            selection = selectionController;
            manager = selection?.Manager;
            importPath = path;
            configured = manager != null
                && selection != null
                && importPath != null;
        }

        private IEnumerator Start()
        {
            if (!configured)
            {
                Debug.LogError($"{Prefix} Configuration valid: False");
                ValidationCompleted = true;
                yield break;
            }

            RunFocusedTests();
            var preflightSource = new RuntimeVrmCharacterSource();
            var preflightTask =
                preflightSource.StartPreflightAsync(importPath);
            while (!preflightTask.IsCompleted)
                yield return null;
            var preflight = preflightTask.Result;
            var persistenceSave =
                preflight.Succeeded && selection.Persistence != null
                    ? selection.Persistence.TrySaveRuntimeSelection(
                        preflight.FullPath,
                        preflight.ContentSha256)
                    : DesktopMascot.Runtime.CharacterPersistence
                        .CharacterSelectionPersistenceWriteStatus
                        .InvalidRecord;
            preflightSource.Cleanup();
            var restoreAccepted =
                preflight.Succeeded
                && (persistenceSave
                        == DesktopMascot.Runtime.CharacterPersistence
                            .CharacterSelectionPersistenceWriteStatus.Saved
                    || persistenceSave
                        == DesktopMascot.Runtime.CharacterPersistence
                            .CharacterSelectionPersistenceWriteStatus
                            .Unchanged)
                && selection.RequestStartupRestoreForDiagnostics();
            if (!restoreAccepted)
            {
                Debug.LogError(
                    $"{Prefix} Startup restore request accepted: False");
                LogFinal(false);
                yield break;
            }
            while (selection.IsBusy)
                yield return null;

            ImportStatus = selection.LastImportStatus;
            PreparationStatus = selection.LastPreparationStatus;
            ActivationStatus = selection.LastActivationStatus;
            Debug.Log($"{Prefix} Import status: {ImportStatus}");
            Debug.Log(
                $"{Prefix} Preparation status: {PreparationStatus}");
            Debug.Log(
                $"{Prefix} Activation status: {ActivationStatus}");
            ValidationPassed =
                selection.LastResult
                    == RuntimeCharacterSelectionResult.Success
                && manager.ActiveCharacter != null
                && manager.ActiveCharacter.SourceType
                    == CharacterAssetSourceType.RuntimeImported
                && manager.ActiveCharacterGeneration == 2;
            LogFinal(ValidationPassed);
            if (!ValidationPassed)
            {
                Debug.LogError(
                    $"{Prefix} Runtime import diagnostic failed: True");
            }
        }

        private void LogFinal(bool passed)
        {
            var descriptor = manager?.ActiveCharacter;
            var runtimeImportedActive =
                descriptor?.SourceType == CharacterAssetSourceType.RuntimeImported;
            Debug.Log(
                $"{Prefix} Active source type: " +
                $"{descriptor?.SourceType.ToString() ?? "None"}");
            Debug.Log(
                $"{Prefix} Active generation: " +
                $"{manager?.ActiveCharacterGeneration ?? 0}");
            Debug.Log(
                $"{Prefix} Imported Animator available: " +
                $"{(runtimeImportedActive && descriptor?.Animator != null)}");
            Debug.Log(
                $"{Prefix} Imported Vrm10Instance available: " +
                $"{(runtimeImportedActive && descriptor?.VrmInstance != null)}");
            Debug.Log(
                $"{Prefix} Runtime import validation passed: {passed}");
            ValidationCompleted = true;
        }

        private static void RunFocusedTests()
        {
            var missingPath = Path.Combine(
                Path.GetTempPath(),
                $"desktop-mascot-missing-{Guid.NewGuid():N}.vrm");
            var wrongExtensionPath =
                Environment.GetCommandLineArgs()[0];
            var passed =
                RuntimeVrmCharacterSource.MaximumFileSizeBytes
                    == 268435456L
                && RuntimeVrmCharacterSource
                    .ValidatePathStatusForDiagnostics(string.Empty)
                    == RuntimeVrmImportStatus.EmptyPath
                && RuntimeVrmCharacterSource
                    .ValidatePathStatusForDiagnostics("relative.vrm")
                    == RuntimeVrmImportStatus.RelativePath
                && RuntimeVrmCharacterSource
                    .ValidatePathStatusForDiagnostics(
                        Application.dataPath)
                    == RuntimeVrmImportStatus.DirectoryPath
                && RuntimeVrmCharacterSource
                    .ValidatePathStatusForDiagnostics(missingPath)
                    == RuntimeVrmImportStatus.FileNotFound
                && RuntimeVrmCharacterSource
                    .ValidatePathStatusForDiagnostics(
                        wrongExtensionPath)
                    == RuntimeVrmImportStatus.UnsupportedExtension
                && RuntimeVrmCharacterSource
                    .ClassifyFileLengthForDiagnostics(0)
                    == RuntimeVrmImportStatus.EmptyFile
                && RuntimeVrmCharacterSource
                    .ClassifyFileLengthForDiagnostics(1)
                    == RuntimeVrmImportStatus.Success
                && RuntimeVrmCharacterSource
                    .ClassifyFileLengthForDiagnostics(268435456L)
                    == RuntimeVrmImportStatus.Success
                && RuntimeVrmCharacterSource
                    .ClassifyFileLengthForDiagnostics(268435457L)
                    == RuntimeVrmImportStatus.FileTooLarge;
            Debug.Log(
                $"{Prefix} Focused input validation passed: {passed}");
        }
    }
}
