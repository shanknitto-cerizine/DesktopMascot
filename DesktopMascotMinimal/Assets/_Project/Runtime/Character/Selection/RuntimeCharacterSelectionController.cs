using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterPersistence;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Runtime.CharacterSelection
{
    internal enum RuntimeCharacterSelectionState
    {
        Idle = 0,
        SelectingFile = 1,
        Importing = 2,
        Activating = 3,
        Succeeded = 4,
        Failed = 5,
        Shutdown = 6,
        RestorePending = 7,
        Restoring = 8,
        FailedFallback = 9
    }

    internal enum RuntimeCharacterSelectionResult
    {
        None = 0,
        Success = 1,
        Cancelled = 2,
        PickerFailed = 3,
        ImportFailed = 4,
        PreparationFailed = 5,
        ActivationRejected = 6,
        AlreadyImporting = 7,
        AlreadyActive = 8,
        ShutdownRejected = 9,
        CleanupFailed = 10,
        NoSavedSelection = 11,
        RestoreFailed = 12,
        ShaMismatch = 13,
        PersistenceFailed = 14,
        PersistenceUnavailable = 15,
        PersistenceCleared = 16,
        BundledActivated = 17,
        AlreadyBundled = 18,
        BundledActivationFailed = 19,
        BundledPersistenceClearFailed = 20
    }

    internal enum RuntimeCharacterSelectionOperation
    {
        Manual = 0,
        StartupRestore = 1,
        DevelopmentDiagnostic = 2
    }

    internal sealed class RuntimeCharacterSelectionController :
        MonoBehaviour
    {
        private const string Prefix =
            "[DesktopMascotRuntimeCharacterSelection]";

        private CharacterAssetManager manager;
        private Camera targetCamera;
        private UnityPlayerWindowVisibilityController visibility;
        private IVrmFilePicker picker;
        private Func<IntPtr> ownerWindowProvider;
        private RuntimeVrmCharacterSource activeSource;
        private CharacterSelectionPersistenceManager persistence;
        private bool startupRestoreEnabled;
        private bool startupRestoreRequested;
        private bool initialized;
        private bool operationExecuting;
        private bool shutdownStarted;
        private bool cleanupCompleted;
        private Stopwatch dialogShutdownWait;

        internal RuntimeCharacterSelectionState State { get; private set; }
        internal RuntimeCharacterSelectionResult LastResult
        {
            get;
            private set;
        }
        internal RuntimeVrmImportStatus LastImportStatus
        {
            get;
            private set;
        }
        internal RuntimeCharacterPreparationStatus LastPreparationStatus
        {
            get;
            private set;
        }
        internal CharacterActivationStatus LastActivationStatus
        {
            get;
            private set;
        }
        internal string StatusMessage { get; private set; } = string.Empty;
        internal string LastSelectedFileName { get; private set; } =
            string.Empty;
        internal bool IsBusy =>
            State == RuntimeCharacterSelectionState.SelectingFile
            || State == RuntimeCharacterSelectionState.RestorePending
            || State == RuntimeCharacterSelectionState.Restoring
            || State == RuntimeCharacterSelectionState.Importing
            || State == RuntimeCharacterSelectionState.Activating;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool HasPendingOperation =>
            picker?.IsRunning == true
            || operationExecuting
            || activeSource?.ImportInProgress == true;
        internal bool ImportInProgress =>
            activeSource?.ImportInProgress == true;
        internal bool DialogShutdownWaitActive =>
            shutdownStarted && picker?.IsRunning == true;
        internal long DialogShutdownWaitMilliseconds =>
            dialogShutdownWait?.ElapsedMilliseconds ?? 0;
        internal bool CleanupCompleted => cleanupCompleted;
        internal CharacterAssetManager Manager => manager;
        internal CharacterDescriptor ActiveCharacter =>
            manager?.ActiveCharacter;
        internal ulong ActiveGeneration =>
            manager?.ActiveCharacterGeneration ?? 0;
        internal int RetiredOwnedHandleCount =>
            manager?.RetiredOwnedRuntimeHandleCount ?? 0;
        internal bool HasActiveCharacterOperation =>
            IsBusy
            || operationExecuting
            || picker?.IsRunning == true
            || activeSource?.ImportInProgress == true;
        internal bool CanActivateBundledCharacter =>
            initialized
            && manager?.BundledCharacter != null
            && !HasActiveCharacterOperation
            && !shutdownStarted
            && persistence?.ShutdownStarted != true;
        internal bool CanClearPersistedSelection =>
            persistence != null
            && persistence.HasStoredRecord
            && !HasActiveCharacterOperation
            && !shutdownStarted
            && !persistence.ShutdownStarted;
        internal bool StartupRestoreRequested => startupRestoreRequested;
        internal int ImportInvocationCount { get; private set; }
        internal CharacterSelectionPersistenceManager Persistence =>
            persistence;

        internal void Configure(
            CharacterAssetManager characterManager,
            Camera camera,
            UnityPlayerWindowVisibilityController visibilityController,
            IVrmFilePicker filePicker = null,
            Func<IntPtr> dialogOwnerProvider = null,
            CharacterSelectionPersistenceManager persistenceManager = null,
            bool enableStartupRestore = true)
        {
            manager = characterManager;
            targetCamera = camera;
            visibility = visibilityController;
            picker = filePicker ?? new WindowsVrmFilePicker();
            ownerWindowProvider = dialogOwnerProvider;
            persistence = persistenceManager;
            startupRestoreEnabled =
                enableStartupRestore && persistence?.HasSelection == true;
            initialized =
                manager != null
                && manager.IsInitialized
                && targetCamera != null
                && picker != null;
            InitializePersistenceState();
            Debug.Log($"{Prefix} Initialized: {initialized}");
        }

        private void InitializePersistenceState()
        {
            State = RuntimeCharacterSelectionState.Idle;
            LastResult = RuntimeCharacterSelectionResult.None;
            StatusMessage = string.Empty;
            if (persistence == null)
                return;
            if (!persistence.IsAvailable)
            {
                State = RuntimeCharacterSelectionState.FailedFallback;
                LastResult =
                    RuntimeCharacterSelectionResult.PersistenceUnavailable;
                StatusMessage =
                    RuntimeCharacterSelectionMessages
                        .ForPersistenceUnavailable();
                return;
            }
            if (startupRestoreEnabled)
            {
                State = RuntimeCharacterSelectionState.RestorePending;
                StatusMessage =
                    RuntimeCharacterSelectionMessages.ForRestorePending();
                return;
            }
            switch (persistence.InitializationStatus)
            {
                case CharacterSelectionPersistenceInitializationStatus
                    .InvalidRecord:
                case CharacterSelectionPersistenceInitializationStatus
                    .UnsupportedVersion:
                case CharacterSelectionPersistenceInitializationStatus
                    .ReadFailure:
                    State =
                        RuntimeCharacterSelectionState.FailedFallback;
                    LastResult =
                        RuntimeCharacterSelectionResult.RestoreFailed;
                    StatusMessage =
                        RuntimeCharacterSelectionMessages.ForRecordFailure(
                            persistence.InitializationStatus);
                    break;
            }
        }

        internal bool RequestFileSelection()
        {
            if (!initialized)
                return FailRequest(
                    RuntimeCharacterSelectionResult.PickerFailed,
                    "ファイル選択を開始できませんでした");
            if (shutdownStarted)
                return FailRequest(
                    RuntimeCharacterSelectionResult.ShutdownRejected,
                    "終了処理中のため変更できません");
            if (IsBusy || operationExecuting || picker.IsRunning)
                return FailRequest(
                    RuntimeCharacterSelectionResult.AlreadyImporting,
                    "モデルの読み込み中です");
            var ownerWindow = ownerWindowProvider?.Invoke()
                ?? IntPtr.Zero;
            if (ownerWindow == IntPtr.Zero
                && (visibility == null
                    || !visibility.TryGetFileDialogOwner(
                        out ownerWindow)))
            {
                return FailRequest(
                    RuntimeCharacterSelectionResult.PickerFailed,
                    RuntimeCharacterSelectionMessages.ForPicker(
                        VrmFilePickerStatus.OwnerUnavailable));
            }

            State = RuntimeCharacterSelectionState.SelectingFile;
            LastResult = RuntimeCharacterSelectionResult.None;
            StatusMessage = "VRM 1.0ファイルを選択してください";
            if (picker.TryStart(ownerWindow))
            {
                Debug.Log($"{Prefix} File picker started: True");
                return true;
            }

            State = RuntimeCharacterSelectionState.Failed;
            LastResult = RuntimeCharacterSelectionResult.PickerFailed;
            StatusMessage = RuntimeCharacterSelectionMessages.ForPicker(
                VrmFilePickerStatus.AlreadyRunning);
            return false;
        }

        internal bool RequestDevelopmentImport(string absolutePath)
        {
            if (!initialized || shutdownStarted)
                return false;
            if (IsBusy || operationExecuting)
                return false;
            LastSelectedFileName = SafeFileName(absolutePath);
            StartImport(
                absolutePath,
                RuntimeCharacterSelectionOperation.DevelopmentDiagnostic,
                string.Empty);
            return true;
        }

        internal bool RequestManualImportForDiagnostics(
            string absolutePath)
        {
            if (!initialized || shutdownStarted)
                return false;
            if (IsBusy || operationExecuting)
                return false;
            LastSelectedFileName = SafeFileName(absolutePath);
            StartImport(
                absolutePath,
                RuntimeCharacterSelectionOperation.Manual,
                string.Empty);
            return true;
        }

        internal bool RequestStartupRestore()
        {
            return RequestStartupRestoreCore(false);
        }

        internal bool RequestStartupRestoreForDiagnostics()
        {
            return RequestStartupRestoreCore(true);
        }

        private bool RequestStartupRestoreCore(bool allowDisabled)
        {
            if (!initialized
                || shutdownStarted
                || startupRestoreRequested
                || (!startupRestoreEnabled && !allowDisabled)
                || persistence == null
                || !persistence.TryGetSelection(out var record))
            {
                return false;
            }
            if (operationExecuting || picker.IsRunning)
                return false;
            startupRestoreRequested = true;
            LastSelectedFileName = SafeFileName(record.AbsolutePath);
            StartImport(
                record.AbsolutePath,
                RuntimeCharacterSelectionOperation.StartupRestore,
                record.ContentSha256);
            return true;
        }

        internal bool RequestUseBundledOnNextStartup()
        {
            if (persistence == null
                || HasActiveCharacterOperation
                || shutdownStarted
                || persistence.ShutdownStarted)
                return false;
            operationExecuting = true;
            try
            {
                var clear = persistence.TryClear();
                if (clear
                        != CharacterSelectionPersistenceWriteStatus.Cleared
                    && clear
                        != CharacterSelectionPersistenceWriteStatus
                            .Unchanged)
                {
                    LastResult =
                        clear == CharacterSelectionPersistenceWriteStatus
                            .PersistenceUnavailable
                        ? RuntimeCharacterSelectionResult
                            .PersistenceUnavailable
                        : RuntimeCharacterSelectionResult.PersistenceFailed;
                    StatusMessage =
                        RuntimeCharacterSelectionMessages
                            .ForPersistenceWriteFailure();
                    return false;
                }
                LastResult =
                    RuntimeCharacterSelectionResult.PersistenceCleared;
                StatusMessage =
                    RuntimeCharacterSelectionMessages
                        .ForPersistenceCleared();
                return true;
            }
            finally
            {
                operationExecuting = false;
            }
        }

        internal bool RequestActivateBundledCharacter()
        {
            if (!initialized || manager == null)
            {
                return FailRequest(
                    RuntimeCharacterSelectionResult
                        .BundledActivationFailed,
                    RuntimeCharacterSelectionMessages
                        .ForBundledActivationFailure(
                            CharacterActivationStatus.NotInitialized));
            }
            if (shutdownStarted
                || persistence?.ShutdownStarted == true)
            {
                return FailRequest(
                    RuntimeCharacterSelectionResult.ShutdownRejected,
                    RuntimeCharacterSelectionMessages
                        .ForBundledActivationFailure(
                            CharacterActivationStatus.ShutdownStarted));
            }
            if (HasActiveCharacterOperation)
            {
                return FailRequest(
                    RuntimeCharacterSelectionResult.AlreadyImporting,
                    RuntimeCharacterSelectionMessages
                        .ForCharacterOperationBusy());
            }

            operationExecuting = true;
            State = RuntimeCharacterSelectionState.Activating;
            LastResult = RuntimeCharacterSelectionResult.None;
            StatusMessage =
                RuntimeCharacterSelectionMessages.ForBundledActivating();
            try
            {
                var activation =
                    manager.TryActivateBundledCharacter();
                LastActivationStatus = activation.Status;
                var alreadyBundled =
                    activation.Status
                    == CharacterActivationStatus
                        .BundledCharacterAlreadyActive;
                if (!activation.Succeeded && !alreadyBundled)
                {
                    State = activation.Status
                            == CharacterActivationStatus.ShutdownStarted
                        ? RuntimeCharacterSelectionState.Shutdown
                        : RuntimeCharacterSelectionState.Failed;
                    LastResult = activation.Status
                            == CharacterActivationStatus.ShutdownStarted
                        ? RuntimeCharacterSelectionResult.ShutdownRejected
                        : RuntimeCharacterSelectionResult
                            .BundledActivationFailed;
                    StatusMessage =
                        RuntimeCharacterSelectionMessages
                            .ForBundledActivationFailure(
                                activation.Status);
                    return false;
                }

                var clear = persistence != null
                    ? persistence.TryClear()
                    : CharacterSelectionPersistenceWriteStatus
                        .PersistenceUnavailable;
                State = RuntimeCharacterSelectionState.Succeeded;
                LastSelectedFileName = string.Empty;
                if (clear
                        == CharacterSelectionPersistenceWriteStatus.Cleared
                    || clear
                        == CharacterSelectionPersistenceWriteStatus
                            .Unchanged)
                {
                    LastResult = alreadyBundled
                        ? RuntimeCharacterSelectionResult.AlreadyBundled
                        : RuntimeCharacterSelectionResult.BundledActivated;
                    StatusMessage = alreadyBundled
                        ? RuntimeCharacterSelectionMessages
                            .ForBundledAlreadyActive()
                        : RuntimeCharacterSelectionMessages
                            .ForBundledActivated();
                    Debug.Log(
                        $"{Prefix} Bundled selection success/status/" +
                        "generation/active/retired handles: " +
                        $"{activation.Status}/" +
                        $"{manager.ActiveCharacterGeneration}/" +
                        $"{manager.ActiveOwnedRuntimeHandleCount}/" +
                        manager.RetiredOwnedRuntimeHandleCount);
                    return true;
                }

                LastResult = RuntimeCharacterSelectionResult
                    .BundledPersistenceClearFailed;
                StatusMessage =
                    RuntimeCharacterSelectionMessages
                        .ForBundledPersistenceClearFailure();
                Debug.LogWarning(
                    $"{Prefix} Bundled activation persisted clear: " +
                    $"False; status: {clear}");
                return true;
            }
            finally
            {
                operationExecuting = false;
            }
        }

        private void Update()
        {
            if (picker == null
                || !picker.TryTakeResult(out var pickerResult))
            {
                return;
            }

            if (shutdownStarted)
            {
                Debug.Log(
                    $"{Prefix} Picker result discarded after shutdown: True");
                State = RuntimeCharacterSelectionState.Shutdown;
                return;
            }

            if (!pickerResult.Succeeded)
            {
                State = pickerResult.Status
                    == VrmFilePickerStatus.Cancelled
                    ? RuntimeCharacterSelectionState.Idle
                    : RuntimeCharacterSelectionState.Failed;
                LastResult = pickerResult.Status
                    == VrmFilePickerStatus.Cancelled
                    ? RuntimeCharacterSelectionResult.Cancelled
                    : RuntimeCharacterSelectionResult.PickerFailed;
                StatusMessage =
                    RuntimeCharacterSelectionMessages.ForPicker(
                        pickerResult.Status);
                Debug.Log(
                    $"{Prefix} File picker completed with status: " +
                    pickerResult.Status);
                return;
            }

            LastSelectedFileName =
                SafeFileName(pickerResult.SelectedPath);
            StartImport(
                pickerResult.SelectedPath,
                RuntimeCharacterSelectionOperation.Manual,
                string.Empty);
        }

        private async void StartImport(
            string transientAbsolutePath,
            RuntimeCharacterSelectionOperation operation,
            string expectedSha256)
        {
            operationExecuting = true;
            State = operation
                    == RuntimeCharacterSelectionOperation.StartupRestore
                ? RuntimeCharacterSelectionState.Restoring
                : RuntimeCharacterSelectionState.Importing;
            LastResult = RuntimeCharacterSelectionResult.None;
            StatusMessage = operation
                    == RuntimeCharacterSelectionOperation.StartupRestore
                ? RuntimeCharacterSelectionMessages.ForRestoreValidating()
                : "モデルを読み込んでいます…";
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var source = new RuntimeVrmCharacterSource();
            activeSource = source;
            try
            {
                var preflight =
                    await source.StartPreflightAsync(
                        transientAbsolutePath);
                if (shutdownStarted)
                {
                    source.BeginShutdown();
                    source.Cleanup();
                    State = RuntimeCharacterSelectionState.Shutdown;
                    return;
                }
                if (!preflight.Succeeded)
                {
                    FailImport(preflight.Status, operation);
                    source.Cleanup();
                    return;
                }
                if (operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    && !string.Equals(
                        preflight.ContentSha256,
                        expectedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    State =
                        RuntimeCharacterSelectionState.FailedFallback;
                    LastResult =
                        RuntimeCharacterSelectionResult.ShaMismatch;
                    StatusMessage =
                        RuntimeCharacterSelectionMessages.ForShaMismatch();
                    source.Cleanup();
                    Debug.Log(
                        $"{Prefix} Startup restore SHA mismatch: True");
                    return;
                }
                if (manager.ActiveCharacter != null
                    && string.Equals(
                        manager.ActiveCharacter.InternalId,
                        preflight.StableSourceId,
                        StringComparison.Ordinal))
                {
                    LastResult =
                        RuntimeCharacterSelectionResult.AlreadyActive;
                    State = RuntimeCharacterSelectionState.Succeeded;
                    StatusMessage = PersistAlreadyActiveSelection(
                        operation,
                        preflight);
                    source.Cleanup();
                    Debug.Log(
                        $"{Prefix} Same content skipped before import: True");
                    return;
                }

                State = RuntimeCharacterSelectionState.Importing;
                StatusMessage = operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    ? RuntimeCharacterSelectionMessages.ForRestoreImporting()
                    : "モデルを読み込んでいます…";
                ImportInvocationCount++;
                var import = await source.StartLoadAsync(preflight);
                LastImportStatus = import.Status;
                if (shutdownStarted)
                {
                    source.BeginShutdown();
                    source.Cleanup();
                    State = RuntimeCharacterSelectionState.Shutdown;
                    return;
                }
                if (!import.Succeeded)
                {
                    FailImport(import.Status, operation);
                    source.Cleanup();
                    return;
                }
                if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
                {
                    FailImport(
                        RuntimeVrmImportStatus.ImportFailed,
                        operation);
                    source.Cleanup();
                    return;
                }

                var preparation =
                    RuntimeImportedCharacterPreparer.Prepare(
                        source,
                        import,
                        manager.ActiveCharacter,
                        targetCamera);
                LastPreparationStatus = preparation.Status;
                if (!preparation.Succeeded)
                {
                    State = operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionState.FailedFallback
                        : RuntimeCharacterSelectionState.Failed;
                    LastResult =
                        operation
                                == RuntimeCharacterSelectionOperation
                                    .StartupRestore
                            ? RuntimeCharacterSelectionResult.RestoreFailed
                            : RuntimeCharacterSelectionResult
                                .PreparationFailed;
                    StatusMessage = operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionMessages
                            .ForRestorePreparationFailure()
                        : RuntimeCharacterSelectionMessages.ForPreparation(
                            preparation.Status);
                    source.Cleanup();
                    return;
                }
                if (shutdownStarted)
                {
                    preparation.Prepared.Rollback();
                    State = RuntimeCharacterSelectionState.Shutdown;
                    return;
                }

                State = RuntimeCharacterSelectionState.Activating;
                StatusMessage = operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    ? RuntimeCharacterSelectionMessages.ForRestoreActivating()
                    : "モデルを切り替えています…";
                var activation =
                    manager.TryActivateImportedCharacter(
                        preparation.Prepared);
                LastActivationStatus = activation.Status;
                if (!activation.Succeeded)
                {
                    State = operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionState.FailedFallback
                        : RuntimeCharacterSelectionState.Failed;
                    LastResult =
                        operation
                                == RuntimeCharacterSelectionOperation
                                    .StartupRestore
                            ? RuntimeCharacterSelectionResult.RestoreFailed
                            : RuntimeCharacterSelectionResult
                                .ActivationRejected;
                    StatusMessage = operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionMessages
                            .ForRestoreActivationFailure()
                        : RuntimeCharacterSelectionMessages.ForActivation(
                            activation.Status);
                    source.Cleanup();
                    return;
                }

                source.Cleanup();
                State = RuntimeCharacterSelectionState.Succeeded;
                ApplyPersistenceAfterActivation(
                    operation,
                    preflight);
                Debug.Log(
                    $"{Prefix} Selection success source/generation/" +
                    "active/retired handles: " +
                    $"{manager.ActiveCharacter.SourceType}/" +
                    $"{manager.ActiveCharacterGeneration}/" +
                    $"{manager.ActiveOwnedRuntimeHandleCount}/" +
                    manager.RetiredOwnedRuntimeHandleCount);
            }
            catch (Exception exception)
            {
                source.BeginShutdown();
                source.Cleanup();
                State = shutdownStarted
                    ? RuntimeCharacterSelectionState.Shutdown
                    : operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionState.FailedFallback
                        : RuntimeCharacterSelectionState.Failed;
                LastResult = shutdownStarted
                    ? RuntimeCharacterSelectionResult.ShutdownRejected
                    : operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionResult.RestoreFailed
                        : RuntimeCharacterSelectionResult.ImportFailed;
                StatusMessage = shutdownStarted
                    ? "終了処理中のため変更できません"
                    : operation
                            == RuntimeCharacterSelectionOperation
                                .StartupRestore
                        ? RuntimeCharacterSelectionMessages
                            .ForRestoreFailure(
                                RuntimeVrmImportStatus.ImportFailed)
                        : "ファイルを読み込めませんでした";
                Debug.LogError(
                    $"{Prefix} Operation exception type/status: " +
                    $"{exception.GetType().Name}/{LastResult}");
            }
            finally
            {
                if (ReferenceEquals(activeSource, source))
                    activeSource = null;
                operationExecuting = false;
            }
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
            State = RuntimeCharacterSelectionState.Shutdown;
            picker?.BeginShutdown();
            activeSource?.BeginShutdown();
            if (picker?.IsRunning == true)
            {
                dialogShutdownWait = Stopwatch.StartNew();
                Debug.Log(
                    $"{Prefix} Dialog shutdown wait active: True");
            }
            Debug.Log($"{Prefix} Shutdown barrier closed: True");
        }

        internal bool Cleanup()
        {
            if (cleanupCompleted)
                return true;
            BeginShutdown();
            if (HasPendingOperation)
                return false;
            var sourceClean = activeSource == null
                || activeSource.Cleanup();
            activeSource = null;
            cleanupCompleted = sourceClean;
            if (!sourceClean)
            {
                LastResult =
                    RuntimeCharacterSelectionResult.CleanupFailed;
            }
            Debug.Log(
                $"{Prefix} Dialog shutdown wait milliseconds: " +
                DialogShutdownWaitMilliseconds);
            Debug.Log(
                $"{Prefix} Cleanup completed: {cleanupCompleted}");
            return cleanupCompleted;
        }

        private void FailImport(
            RuntimeVrmImportStatus status,
            RuntimeCharacterSelectionOperation operation)
        {
            LastImportStatus = status;
            State = shutdownStarted
                ? RuntimeCharacterSelectionState.Shutdown
                : operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    ? RuntimeCharacterSelectionState.FailedFallback
                    : RuntimeCharacterSelectionState.Failed;
            LastResult = shutdownStarted
                ? RuntimeCharacterSelectionResult.ShutdownRejected
                : operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    ? RuntimeCharacterSelectionResult.RestoreFailed
                    : RuntimeCharacterSelectionResult.ImportFailed;
            StatusMessage = operation
                    == RuntimeCharacterSelectionOperation.StartupRestore
                ? RuntimeCharacterSelectionMessages.ForRestoreFailure(status)
                : RuntimeCharacterSelectionMessages.ForImport(status);
            Debug.Log(
                $"{Prefix} Import operation failed with status: {status}");
        }

        private string PersistAlreadyActiveSelection(
            RuntimeCharacterSelectionOperation operation,
            RuntimeVrmPreflightResult preflight)
        {
            if (operation != RuntimeCharacterSelectionOperation.Manual
                || persistence == null)
            {
                return operation
                        == RuntimeCharacterSelectionOperation.StartupRestore
                    ? RuntimeCharacterSelectionMessages.ForRestoreSucceeded()
                    : "現在使用中のモデルです";
            }
            var save = persistence.TrySaveRuntimeSelection(
                preflight.FullPath,
                preflight.ContentSha256);
            if (save == CharacterSelectionPersistenceWriteStatus.Saved)
                return "現在使用中のモデルです。次回起動先を更新しました";
            if (save == CharacterSelectionPersistenceWriteStatus.Unchanged)
                return "現在使用中のモデルです";
            LastResult = RuntimeCharacterSelectionResult.PersistenceFailed;
            return RuntimeCharacterSelectionMessages
                .ForPersistenceWriteFailure();
        }

        private void ApplyPersistenceAfterActivation(
            RuntimeCharacterSelectionOperation operation,
            RuntimeVrmPreflightResult preflight)
        {
            LastResult = RuntimeCharacterSelectionResult.Success;
            if (operation
                == RuntimeCharacterSelectionOperation.StartupRestore)
            {
                StatusMessage =
                    RuntimeCharacterSelectionMessages.ForRestoreSucceeded();
                return;
            }
            if (operation
                    == RuntimeCharacterSelectionOperation
                        .DevelopmentDiagnostic
                || persistence == null)
            {
                StatusMessage = "モデルを変更しました";
                return;
            }
            var save = persistence.TrySaveRuntimeSelection(
                preflight.FullPath,
                preflight.ContentSha256);
            if (save == CharacterSelectionPersistenceWriteStatus.Saved
                || save == CharacterSelectionPersistenceWriteStatus
                    .Unchanged)
            {
                StatusMessage = "モデルを変更しました";
                return;
            }
            LastResult = RuntimeCharacterSelectionResult.PersistenceFailed;
            StatusMessage =
                RuntimeCharacterSelectionMessages
                    .ForPersistenceWriteFailure();
            Debug.LogWarning(
                $"{Prefix} Runtime activation persisted: False; " +
                $"status: {save}");
        }

        private bool FailRequest(
            RuntimeCharacterSelectionResult result,
            string message)
        {
            LastResult = result;
            StatusMessage = message;
            if (result == RuntimeCharacterSelectionResult.ShutdownRejected)
                State = RuntimeCharacterSelectionState.Shutdown;
            return false;
        }

        private static string SafeFileName(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path)
                    ? string.Empty
                    : Path.GetFileName(path);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
