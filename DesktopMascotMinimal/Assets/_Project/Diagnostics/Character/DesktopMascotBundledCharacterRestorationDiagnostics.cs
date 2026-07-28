using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterPersistence;
using DesktopMascot.Runtime.CharacterSelection;
using DesktopMascot.Runtime.Settings;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class
        DesktopMascotBundledCharacterRestorationDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotBundledCharacterRestorationDiagnostics]";

        internal static IEnumerator Run(
            RuntimeCharacterSelectionController selection,
            string transientImportPath,
            Action<bool> completed)
        {
            var passed = false;
            if (selection?.Manager == null
                || selection.ActiveCharacter?.SourceType
                    != CharacterAssetSourceType.RuntimeImported
                || !selection.Persistence.TryGetSelection(
                    out var activeRecord))
            {
                Debug.LogError($"{Prefix} Configuration valid: False");
                completed?.Invoke(false);
                yield break;
            }

            var manager = selection.Manager;
            var bundled = manager.BundledCharacter;
            var runtimeRoot = selection.ActiveCharacter.Root;
            var bundledRoot = bundled?.Root;
            var bundledExpression =
                bundledRoot?.GetComponent<MascotExpressionController>();
            var initialGeneration = selection.ActiveGeneration;
            var initialRetiredCount =
                manager.RetiredOwnedRuntimeHandleCount;
            var initialRuntimeRootId =
                runtimeRoot != null ? runtimeRoot.GetInstanceID() : 0;
            var bundledRootId =
                bundledRoot != null ? bundledRoot.GetInstanceID() : 0;
            var initialBlinkStartCount =
                bundledExpression?.BlinkLoopStartCount ?? 0;
            var settingsSnapshot =
                ReadSmallFile(SettingsManager.ProductionPath);
            var positionSnapshot = ReadSmallFile(
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "DesktopMascotMinimal",
                    "window-position.json"));
            var vrmHashBefore = ComputeFileHash(transientImportPath);

            var validationGeneration = selection.ActiveGeneration;
            var validationHandleCount =
                manager.TotalOwnedRuntimeHandleCount;
            var animator = bundled?.Animator;
            var animatorWasEnabled = animator != null && animator.enabled;
            if (animator != null)
                animator.enabled = false;
            var validationFailure =
                manager.TryActivateBundledCharacter();
            if (animator != null)
                animator.enabled = animatorWasEnabled;
            var validationFailurePreservedRuntime =
                validationFailure.Status
                    == CharacterActivationStatus
                        .BundledCharacterValidationFailed
                && selection.ActiveGeneration == validationGeneration
                && selection.ActiveCharacter?.RootInstanceId
                    == initialRuntimeRootId
                && manager.TotalOwnedRuntimeHandleCount
                    == validationHandleCount
                && runtimeRoot != null
                && runtimeRoot.activeInHierarchy
                && bundledRoot != null
                && !bundledRoot.activeInHierarchy;
            Debug.Log(
                $"{Prefix} Validation failure preserved Runtime " +
                $"character: {validationFailurePreservedRuntime}");

            var isolatedDirectory = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M043",
                Guid.NewGuid().ToString("N"));
            var isolatedPath = Path.Combine(
                isolatedDirectory,
                "character-selection.json");
            var failingPersistence =
                new CharacterSelectionPersistenceManager(
                    new CharacterSelectionStore(isolatedPath),
                    new CharacterSelectionSerializer(),
                    false);
            failingPersistence.Initialize();
            var stored = failingPersistence.TrySaveRuntimeSelection(
                activeRecord.AbsolutePath,
                activeRecord.ContentSha256);
            var owner = new GameObject(
                "BundledRestorationClearFailureFocused");
            var controller =
                owner.AddComponent<RuntimeCharacterSelectionController>();
            controller.Configure(
                manager,
                Camera.main,
                null,
                new FocusedFilePicker(),
                () => new IntPtr(1),
                failingPersistence,
                false);

            bool clearFailureRequestAccepted;
            using (var lockedRecord = new FileStream(
                       isolatedPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                clearFailureRequestAccepted =
                    controller.RequestActivateBundledCharacter();
            }
            yield return null;

            var activeRootCount =
                UnityEngine.Object.FindObjectsByType<MascotCharacter>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Count(character =>
                        character != null
                        && character.gameObject.activeInHierarchy);
            var standingIdleHash =
                CharacterAnimationContract.StandingIdleStateHash;
            var animatorState =
                bundled.Animator.GetCurrentAnimatorStateInfo(0);
            var activationSucceeded =
                clearFailureRequestAccepted
                && controller.State
                    == RuntimeCharacterSelectionState.Succeeded
                && controller.LastResult
                    == RuntimeCharacterSelectionResult
                        .BundledPersistenceClearFailed
                && controller.LastActivationStatus
                    == CharacterActivationStatus.Success
                && selection.ActiveCharacter?.SourceType
                    == CharacterAssetSourceType.BundledScene
                && ReferenceEquals(
                    selection.ActiveCharacter,
                    bundled)
                && selection.ActiveCharacter.RootInstanceId
                    == bundledRootId
                && selection.ActiveGeneration
                    == initialGeneration + 1
                && manager.ActiveOwnedRuntimeHandleCount == 0
                && manager.RetiredOwnedRuntimeHandleCount
                    == initialRetiredCount + 1
                && activeRootCount == 1
                && bundledRoot.activeInHierarchy
                && !runtimeRoot.activeInHierarchy
                && bundledRoot.scene.IsValid()
                && bundledRoot.scene.isLoaded
                && bundled.Animator.isActiveAndEnabled
                && bundled.Animator.isInitialized
                && animatorState.fullPathHash == standingIdleHash
                && bundled.VrmInstance.isActiveAndEnabled
                && bundledExpression != null
                && bundledExpression.IsBlinkLoopRunning
                && bundledExpression.BlinkLoopStartCount
                    == initialBlinkStartCount + 1;
            var clearFailureWasNonFatal =
                stored == CharacterSelectionPersistenceWriteStatus.Saved
                && failingPersistence.HasStoredRecord
                && activationSucceeded;
            Debug.Log(
                $"{Prefix} Runtime to Bundled activation passed: " +
                activationSucceeded);
            Debug.Log(
                $"{Prefix} Persistence clear failure was non-fatal: " +
                clearFailureWasNonFatal);
            Debug.Log(
                $"{Prefix} Active source/generation/active/retired: " +
                $"{selection.ActiveCharacter?.SourceType}/" +
                $"{selection.ActiveGeneration}/" +
                $"{manager.ActiveOwnedRuntimeHandleCount}/" +
                manager.RetiredOwnedRuntimeHandleCount);
            Debug.Log(
                $"{Prefix} Active character root count: {activeRootCount}");
            Debug.Log(
                $"{Prefix} Bundled Scene loaded/Animator initialized/" +
                "blink running: " +
                $"{bundledRoot.scene.isLoaded}/" +
                $"{bundled.Animator.isInitialized}/" +
                bundledExpression.IsBlinkLoopRunning);

            controller.BeginShutdown();
            var failureControllerCleanup = controller.Cleanup();
            failingPersistence.Cleanup();
            UnityEngine.Object.Destroy(owner);
            yield return null;

            var generationBeforeAlreadyBundled =
                selection.ActiveGeneration;
            var retiredBeforeAlreadyBundled =
                manager.RetiredOwnedRuntimeHandleCount;
            var blinkStartsBeforeAlreadyBundled =
                bundledExpression.BlinkLoopStartCount;
            var clearSucceeded =
                selection.RequestActivateBundledCharacter();
            var alreadyBundledSucceeded =
                clearSucceeded
                && selection.LastResult
                    == RuntimeCharacterSelectionResult.AlreadyBundled
                && selection.LastActivationStatus
                    == CharacterActivationStatus
                        .BundledCharacterAlreadyActive
                && !selection.Persistence.HasStoredRecord
                && selection.ActiveGeneration
                    == generationBeforeAlreadyBundled
                && manager.RetiredOwnedRuntimeHandleCount
                    == retiredBeforeAlreadyBundled
                && bundledExpression.BlinkLoopStartCount
                    == blinkStartsBeforeAlreadyBundled;
            var secondNoOp =
                selection.RequestActivateBundledCharacter()
                && selection.LastResult
                    == RuntimeCharacterSelectionResult.AlreadyBundled
                && selection.ActiveGeneration
                    == generationBeforeAlreadyBundled
                && manager.RetiredOwnedRuntimeHandleCount
                    == retiredBeforeAlreadyBundled;
            Debug.Log(
                $"{Prefix} Already Bundled clear/no-op passed: " +
                alreadyBundledSucceeded);
            Debug.Log(
                $"{Prefix} Repeated Bundled no-op passed: {secondNoOp}");

            var busyOwner = new GameObject(
                "BundledRestorationBusyFocused");
            var busyPicker = new FocusedFilePicker();
            var busyController =
                busyOwner.AddComponent<
                    RuntimeCharacterSelectionController>();
            busyController.Configure(
                manager,
                Camera.main,
                null,
                busyPicker,
                () => new IntPtr(1),
                selection.Persistence,
                false);
            var pickerStarted = busyController.RequestFileSelection();
            var busyRejected =
                !busyController.RequestActivateBundledCharacter();
            busyController.BeginShutdown();
            var shutdownRejected =
                !busyController.RequestActivateBundledCharacter();
            busyPicker.CompleteCancelled();
            yield return null;
            var busyCleanup = busyController.Cleanup();
            UnityEngine.Object.Destroy(busyOwner);
            yield return null;
            Debug.Log(
                $"{Prefix} Busy/shutdown Bundled requests rejected: " +
                $"{(pickerStarted && busyRejected)}/{shutdownRejected}");

            var settingsUnchanged = SameBytes(
                settingsSnapshot,
                ReadSmallFile(SettingsManager.ProductionPath));
            var positionUnchanged = SameBytes(
                positionSnapshot,
                ReadSmallFile(
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        "DesktopMascotMinimal",
                        "window-position.json")));
            var vrmUnchanged = SameBytes(
                vrmHashBefore,
                ComputeFileHash(transientImportPath));
            Debug.Log(
                $"{Prefix} Settings/position/VRM unchanged: " +
                $"{settingsUnchanged}/{positionUnchanged}/{vrmUnchanged}");

            TryDeleteIsolatedDirectory(isolatedDirectory);
            passed =
                validationFailurePreservedRuntime
                && activationSucceeded
                && clearFailureWasNonFatal
                && failureControllerCleanup
                && alreadyBundledSucceeded
                && secondNoOp
                && pickerStarted
                && busyRejected
                && shutdownRejected
                && busyCleanup
                && settingsUnchanged
                && positionUnchanged
                && vrmUnchanged;
            Debug.Log(
                $"{Prefix} Bundled restoration validation passed: " +
                passed);
            completed?.Invoke(passed);
        }

        private static byte[] ReadSmallFile(string path)
        {
            try
            {
                return File.Exists(path)
                    ? File.ReadAllBytes(path)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ComputeFileHash(string path)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var sha256 = SHA256.Create();
                return sha256.ComputeHash(stream);
            }
            catch
            {
                return null;
            }
        }

        private static bool SameBytes(byte[] left, byte[] right)
        {
            if (left == null || right == null)
                return left == null && right == null;
            return left.SequenceEqual(right);
        }

        private static void TryDeleteIsolatedDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{Prefix} Temporary cleanup exception type/status: " +
                    $"{exception.GetType().Name}/CleanupFailed");
            }
        }

        private sealed class FocusedFilePicker : IVrmFilePicker
        {
            private bool resultAvailable;
            private VrmFilePickerResult result;

            public bool IsRunning { get; private set; }

            public bool TryStart(IntPtr ownerWindow)
            {
                if (IsRunning)
                    return false;
                IsRunning = true;
                return true;
            }

            public bool TryTakeResult(out VrmFilePickerResult value)
            {
                if (!resultAvailable)
                {
                    value = default;
                    return false;
                }
                value = result;
                resultAvailable = false;
                return true;
            }

            public void BeginShutdown()
            {
            }

            internal void CompleteCancelled()
            {
                IsRunning = false;
                result = new VrmFilePickerResult(
                    VrmFilePickerStatus.Cancelled,
                    string.Empty,
                    string.Empty);
                resultAvailable = true;
            }
        }
    }
}
