using System.Collections;
using System;
using System.IO;
using System.Linq;
using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterPersistence;
using DesktopMascot.Runtime.CharacterSelection;
using UnityEngine;
using UnityEngine.Profiling;

namespace DesktopMascot.Diagnostics
{
    internal sealed class
        DesktopMascotRuntimeCharacterSelectionDiagnostics : MonoBehaviour
    {
        private const string Prefix =
            "[DesktopMascotRuntimeCharacterSelectionDiagnostics]";

        private RuntimeCharacterSelectionController selection;
        private DesktopMascotRuntimeVrmImportDiagnostics initialImport;
        private string transientImportPath;
        private bool configured;
        private bool restoreMismatchPassed;
        private bool persistenceUnavailablePassed;
        private bool persistenceFailurePassed;
        private bool clearSelectionPassed;
        private bool bundledRestorationPassed;
        private long managedMemoryBefore;
        private long managedMemoryPeak;

        internal void Configure(
            RuntimeCharacterSelectionController selectionController,
            DesktopMascotRuntimeVrmImportDiagnostics
                initialImportDiagnostics,
            string importPath)
        {
            selection = selectionController;
            initialImport = initialImportDiagnostics;
            transientImportPath = importPath;
            configured =
                selection != null
                && initialImport != null
                && importPath != null;
        }

        private IEnumerator Start()
        {
            managedMemoryBefore =
                Profiler.GetTotalAllocatedMemoryLong();
            managedMemoryPeak = managedMemoryBefore;
            RunFocusedTests();
            if (!configured)
            {
                Debug.LogError($"{Prefix} Configuration valid: False");
                yield break;
            }

            yield return RunPickerLifecycleFocusedTest();
            yield return null;
            while (!initialImport.ValidationCompleted)
                yield return null;
            if (!initialImport.ValidationPassed)
            {
                Debug.LogError(
                    $"{Prefix} Initial M-040 import passed: False");
                yield break;
            }

            yield return RunStartupRestoreFailureFocusedTests();

            var sameGeneration = selection.ActiveGeneration;
            var sameRoot = selection.ActiveCharacter?.RootInstanceId ?? 0;
            var sameWriteCount =
                selection.Persistence?.WriteCount ?? 0;
            var sameRequestAccepted =
                selection.RequestManualImportForDiagnostics(
                    transientImportPath);
            while (selection.IsBusy)
                yield return null;
            var sameContentPassed =
                sameRequestAccepted
                && selection.LastResult
                    == RuntimeCharacterSelectionResult.AlreadyActive
                && selection.ActiveGeneration == sameGeneration
                && selection.ActiveCharacter?.RootInstanceId == sameRoot
                && (selection.Persistence?.WriteCount ?? 0)
                    == sameWriteCount;
            Debug.Log(
                $"{Prefix} Same SHA skipped/generation/root unchanged: " +
                sameContentPassed);
            Debug.Log(
                $"{Prefix} Same SHA persistence write count: " +
                (selection.Persistence?.WriteCount ?? 0));

            var successfulSwitches = 1;
            for (var index = 0; index < 2; ++index)
            {
                var source = new RuntimeVrmCharacterSource();
                var task = source.StartLoadAsync(transientImportPath);
                while (!task.IsCompleted)
                    yield return null;
                var import = task.Result;
                if (!import.Succeeded)
                {
                    source.Cleanup();
                    break;
                }
                var preparation =
                    RuntimeImportedCharacterPreparer.Prepare(
                        source,
                        import,
                        selection.ActiveCharacter,
                        Camera.main);
                if (!preparation.Succeeded)
                {
                    source.Cleanup();
                    break;
                }
                var activation =
                    selection.Manager.TryActivateImportedCharacter(
                        preparation.Prepared);
                source.Cleanup();
                if (!activation.Succeeded)
                    break;
                successfulSwitches++;
                yield return WaitForRetiredDisposals();
            }

            var activeRootCount =
                FindObjectsByType<MascotCharacter>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Count(character =>
                        character != null
                        && character.gameObject.activeInHierarchy);
            var switchPassed =
                successfulSwitches >= 3
                && selection.ActiveGeneration == 4
                && activeRootCount == 1
                && selection.Manager.ActiveOwnedRuntimeHandleCount == 1
                && selection.RetiredOwnedHandleCount == 0
                && selection.Manager.RetiredDisposeFailureCount == 0;
            Debug.Log(
                $"{Prefix} Consecutive successful switches: " +
                successfulSwitches);
            Debug.Log(
                $"{Prefix} Active root count: {activeRootCount}");
            Debug.Log(
                $"{Prefix} Active/retired/total owned handles: " +
                $"{selection.Manager.ActiveOwnedRuntimeHandleCount}/" +
                $"{selection.RetiredOwnedHandleCount}/" +
                selection.Manager.TotalOwnedRuntimeHandleCount);
            Debug.Log(
                $"{Prefix} Three-switch validation passed: " +
                switchPassed);
            yield return
                DesktopMascotBundledCharacterRestorationDiagnostics.Run(
                    selection,
                    transientImportPath,
                    passed => bundledRestorationPassed = passed);
            yield return WaitForRetiredDisposals();

            var runtimeDActivated = false;
            var runtimeDSource = new RuntimeVrmCharacterSource();
            var runtimeDTask =
                runtimeDSource.StartLoadAsync(transientImportPath);
            while (!runtimeDTask.IsCompleted)
            {
                ObserveManagedMemory();
                yield return null;
            }
            var runtimeDImport = runtimeDTask.Result;
            if (runtimeDImport.Succeeded)
            {
                var runtimeDPreparation =
                    RuntimeImportedCharacterPreparer.Prepare(
                        runtimeDSource,
                        runtimeDImport,
                        selection.ActiveCharacter,
                        Camera.main);
                if (runtimeDPreparation.Succeeded)
                {
                    runtimeDActivated =
                        selection.Manager.TryActivateImportedCharacter(
                            runtimeDPreparation.Prepared).Succeeded;
                }
            }
            runtimeDSource.Cleanup();
            yield return WaitForRetiredDisposals();

            var finalActiveRootCount =
                FindObjectsByType<MascotCharacter>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Count(character =>
                        character != null
                        && character.gameObject.activeInHierarchy);
            var disposalValidationPassed =
                switchPassed
                && bundledRestorationPassed
                && runtimeDActivated
                && finalActiveRootCount == 1
                && selection.ActiveCharacter?.SourceType
                    == CharacterAssetSourceType.RuntimeImported
                && selection.Manager.ActiveOwnedRuntimeHandleCount == 1
                && selection.Manager.RetiredOwnedRuntimeHandleCount == 0
                && selection.Manager.RetiredDisposeFailureCount == 0
                && selection.Manager.RetiredReleaseConfirmedCount >= 3;
            var managedMemoryAfter =
                Profiler.GetTotalAllocatedMemoryLong();
            ObserveManagedMemory();
            Debug.Log(
                $"{Prefix} Retired GraphicsFence supported: " +
                selection.Manager.RetiredGraphicsFenceSupported);
            Debug.Log(
                $"{Prefix} Retired fence cohorts/passed/timeouts/" +
                "unsupported fallback: " +
                $"{selection.Manager.RetiredFenceCohortCount}/" +
                $"{selection.Manager.RetiredFencePassedCount}/" +
                $"{selection.Manager.RetiredFenceTimeoutCount}/" +
                selection.Manager.RetiredUnsupportedFallbackCount);
            Debug.Log(
                $"{Prefix} Dispose requested/release confirmed/failure: " +
                $"{selection.Manager.RetiredDisposeRequestedCount}/" +
                $"{selection.Manager.RetiredReleaseConfirmedCount}/" +
                selection.Manager.RetiredDisposeFailureCount);
            Debug.Log(
                $"{Prefix} Retired queue current/maximum: " +
                $"{selection.Manager.RetiredOwnedRuntimeHandleCount}/" +
                selection.Manager.MaximumRetiredQueueCount);
            Debug.Log(
                $"{Prefix} Managed Memory Before: {managedMemoryBefore}");
            Debug.Log(
                $"{Prefix} Managed Memory Peak: {managedMemoryPeak}");
            Debug.Log(
                $"{Prefix} Managed Memory After: {managedMemoryAfter}");
            Debug.Log(
                $"{Prefix} Managed Memory Delta: " +
                (managedMemoryAfter - managedMemoryBefore));
            Debug.Log(
                $"{Prefix} Render-safe disposal validation passed: " +
                disposalValidationPassed);
            Debug.Log(
                $"{Prefix} Runtime selection validation passed: " +
                (sameContentPassed
                    && switchPassed
                    && restoreMismatchPassed
                    && persistenceUnavailablePassed
                    && persistenceFailurePassed
                    && clearSelectionPassed
                    && bundledRestorationPassed
                    && disposalValidationPassed));

            var generationBeforeFailure = selection.ActiveGeneration;
            var rootBeforeFailure =
                selection.ActiveCharacter?.RootInstanceId ?? 0;
            var handlesBeforeFailure =
                selection.Manager.TotalOwnedRuntimeHandleCount;
            var missingPath = Path.Combine(
                Path.GetTempPath(),
                $"desktop-mascot-m041-missing-{Guid.NewGuid():N}.vrm");
            var failureAccepted =
                selection.RequestDevelopmentImport(missingPath);
            while (selection.IsBusy)
                yield return null;
            var failurePreservedActive =
                failureAccepted
                && selection.State
                    == RuntimeCharacterSelectionState.Failed
                && selection.ActiveGeneration == generationBeforeFailure
                && selection.ActiveCharacter?.RootInstanceId
                    == rootBeforeFailure
                && selection.Manager.TotalOwnedRuntimeHandleCount
                    == handlesBeforeFailure;
            Debug.Log(
                $"{Prefix} Failure preserved active root/generation/" +
                $"handles: {failurePreservedActive}");
        }

        private IEnumerator WaitForRetiredDisposals()
        {
            var startedFrame = Time.frameCount;
            var startedAt = Time.realtimeSinceStartupAsDouble;
            while (selection.Manager.RetiredOwnedRuntimeHandleCount != 0
                && Time.frameCount - startedFrame
                    < RuntimeRetiredCharacterDisposalQueue
                        .FenceTimeoutFrames + 30
                && Time.realtimeSinceStartupAsDouble - startedAt
                    < RuntimeRetiredCharacterDisposalQueue
                        .FenceTimeoutSeconds + 2.0)
            {
                ObserveManagedMemory();
                yield return null;
            }
            ObserveManagedMemory();
        }

        private void ObserveManagedMemory()
        {
            managedMemoryPeak = Math.Max(
                managedMemoryPeak,
                Profiler.GetTotalAllocatedMemoryLong());
        }

        private IEnumerator RunStartupRestoreFailureFocusedTests()
        {
            var generation = selection.ActiveGeneration;
            var root = selection.ActiveCharacter?.RootInstanceId ?? 0;
            var persistence =
                CharacterSelectionPersistenceManager.CreateIsolated(
                    "sha-mismatch-" + Guid.NewGuid().ToString("N"));
            persistence.Initialize();
            var stored = persistence.TrySaveRuntimeSelection(
                transientImportPath,
                new string('0', 64));
            var owner = new GameObject(
                "RuntimeCharacterRestoreMismatchFocused");
            var fake = new FocusedFilePicker();
            var controller =
                owner.AddComponent<RuntimeCharacterSelectionController>();
            controller.Configure(
                selection.Manager,
                Camera.main,
                null,
                fake,
                () => new IntPtr(1),
                persistence,
                true);
            var restoreAccepted = controller.RequestStartupRestore();
            var manualRejectedDuringRestore =
                !controller.RequestFileSelection();
            while (controller.IsBusy)
                yield return null;
            var mismatchPassed =
                stored == CharacterSelectionPersistenceWriteStatus.Saved
                && restoreAccepted
                && manualRejectedDuringRestore
                && controller.State
                    == RuntimeCharacterSelectionState.FailedFallback
                && controller.LastResult
                    == RuntimeCharacterSelectionResult.ShaMismatch
                && controller.ImportInvocationCount == 0
                && selection.ActiveGeneration == generation
                && selection.ActiveCharacter?.RootInstanceId == root
                && persistence.HasStoredRecord;
            restoreMismatchPassed = mismatchPassed;
            controller.BeginShutdown();
            var cleanup = controller.Cleanup()
                && persistence.Cleanup();
            Destroy(owner);
            yield return null;
            Debug.Log(
                $"{Prefix} SHA mismatch skipped import and preserved " +
                $"active character: {mismatchPassed}");
            Debug.Log(
                $"{Prefix} Manual selection rejected during restore: " +
                manualRejectedDuringRestore);
            Debug.Log(
                $"{Prefix} SHA mismatch record preserved: " +
                persistence.HasStoredRecord);
            Debug.Log(
                $"{Prefix} Restore mismatch cleanup passed: {cleanup}");

            var unavailableStore = new CharacterSelectionStore(
                Path.Combine(
                    Application.temporaryCachePath,
                    "DesktopMascotMinimal",
                    "M042-unavailable",
                    "character-selection.json"),
                _ => throw new UnauthorizedAccessException());
            var unavailable =
                new CharacterSelectionPersistenceManager(
                    unavailableStore,
                    new CharacterSelectionSerializer(),
                    false);
            unavailable.Initialize();
            var unavailableOwner = new GameObject(
                "RuntimeCharacterPersistenceUnavailableFocused");
            var unavailableController =
                unavailableOwner.AddComponent<
                    RuntimeCharacterSelectionController>();
            unavailableController.Configure(
                selection.Manager,
                Camera.main,
                null,
                new FocusedFilePicker(),
                () => new IntPtr(1),
                unavailable,
                true);
            var unavailablePassed =
                unavailableController.State
                    == RuntimeCharacterSelectionState.FailedFallback
                && unavailableController.LastResult
                    == RuntimeCharacterSelectionResult
                        .PersistenceUnavailable
                && selection.ActiveGeneration == generation
                && selection.ActiveCharacter?.RootInstanceId == root;
            persistenceUnavailablePassed = unavailablePassed;
            unavailableController.BeginShutdown();
            unavailableController.Cleanup();
            unavailable.Cleanup();
            Destroy(unavailableOwner);
            yield return null;
            Debug.Log(
                $"{Prefix} Persistence unavailable keeps runtime usable: " +
                unavailablePassed);

            if (!selection.Persistence.TryGetSelection(
                    out var activeRecord))
            {
                Debug.Log(
                    $"{Prefix} Persistence failure remains nonfatal: False");
                Debug.Log(
                    $"{Prefix} Clear affects next startup only: False");
                yield break;
            }

            var failingPersistence =
                CharacterSelectionPersistenceManager.CreateIsolated(
                    "commit-failure-" + Guid.NewGuid().ToString("N"));
            failingPersistence.Initialize();
            var failedWrite =
                failingPersistence.TrySaveRuntimeSelection(
                    activeRecord.AbsolutePath,
                    activeRecord.ContentSha256,
                    (_, __) => throw new IOException(
                        "simulated commit failure"));
            persistenceFailurePassed =
                failedWrite
                    == CharacterSelectionPersistenceWriteStatus.WriteFailure
                && selection.ActiveGeneration == generation
                && selection.ActiveCharacter?.RootInstanceId == root;
            failingPersistence.Cleanup();
            Debug.Log(
                $"{Prefix} Persistence failure remains nonfatal: " +
                persistenceFailurePassed);

            var clearPersistence =
                CharacterSelectionPersistenceManager.CreateIsolated(
                    "clear-next-start-" + Guid.NewGuid().ToString("N"));
            clearPersistence.Initialize();
            clearPersistence.TrySaveRuntimeSelection(
                activeRecord.AbsolutePath,
                activeRecord.ContentSha256);
            var clearOwner = new GameObject(
                "RuntimeCharacterClearNextStartupFocused");
            var clearController =
                clearOwner.AddComponent<
                    RuntimeCharacterSelectionController>();
            clearController.Configure(
                selection.Manager,
                Camera.main,
                null,
                new FocusedFilePicker(),
                () => new IntPtr(1),
                clearPersistence,
                false);
            var clearAccepted =
                clearController.RequestUseBundledOnNextStartup();
            clearSelectionPassed =
                clearAccepted
                && !clearPersistence.HasStoredRecord
                && selection.ActiveGeneration == generation
                && selection.ActiveCharacter?.RootInstanceId == root;
            clearController.BeginShutdown();
            clearController.Cleanup();
            clearPersistence.Cleanup();
            Destroy(clearOwner);
            yield return null;
            Debug.Log(
                $"{Prefix} Clear affects next startup only: " +
                clearSelectionPassed);
        }

        private static void RunFocusedTests()
        {
            var passed =
                RuntimeCharacterSelectionMessages.ForImport(
                    RuntimeVrmImportStatus.ImportFailed)
                    == "このVRMには対応していません"
                && RuntimeCharacterSelectionMessages.ForPicker(
                    VrmFilePickerStatus.Cancelled)
                    == "ファイルの読み込みをキャンセルしました"
                && RuntimeCharacterSelectionMessages.ForActivation(
                    CharacterActivationStatus.ShutdownStarted)
                    == "終了処理中のため変更できません"
                && WindowsVrmFilePicker.VrmFilterPattern == "*.vrm";
            Debug.Log(
                $"{Prefix} Focused status/message tests passed: {passed}");
        }

        private IEnumerator RunPickerLifecycleFocusedTest()
        {
            var owner = new GameObject(
                "RuntimeCharacterSelectionFocusedPicker");
            var fake = new FocusedFilePicker();
            var controller =
                owner.AddComponent<RuntimeCharacterSelectionController>();
            controller.Configure(
                selection.Manager,
                Camera.main,
                null,
                fake,
                () => new IntPtr(1));
            var firstAccepted = controller.RequestFileSelection();
            var duplicateRejected = !controller.RequestFileSelection();
            controller.BeginShutdown();
            var waitActive =
                controller.DialogShutdownWaitActive
                && controller.State
                    == RuntimeCharacterSelectionState.Shutdown;
            fake.CompleteCancelled();
            yield return null;
            var resultDiscarded =
                controller.State
                    == RuntimeCharacterSelectionState.Shutdown
                && controller.LastResult
                    != RuntimeCharacterSelectionResult.Cancelled;
            var cleanup = controller.Cleanup();
            Destroy(owner);
            yield return null;
            var productionControllerCount =
                FindObjectsByType<RuntimeCharacterSelectionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length;
            var passed =
                firstAccepted
                && duplicateRejected
                && waitActive
                && resultDiscarded
                && cleanup
                && fake.OwnerWindow == new IntPtr(1)
                && productionControllerCount == 1;
            Debug.Log(
                $"{Prefix} Picker cancel/duplicate/shutdown discard " +
                $"passed: {passed}");
            Debug.Log(
                $"{Prefix} Production selection controller count: " +
                productionControllerCount);
        }

        private sealed class FocusedFilePicker : IVrmFilePicker
        {
            private bool resultAvailable;
            private VrmFilePickerResult result;

            public bool IsRunning { get; private set; }
            internal IntPtr OwnerWindow { get; private set; }

            public bool TryStart(IntPtr ownerWindow)
            {
                if (IsRunning)
                    return false;
                OwnerWindow = ownerWindow;
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
