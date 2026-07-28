using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Runtime.CharacterPersistence
{
    internal enum CharacterSelectionPersistenceInitializationStatus
    {
        Loaded = 0,
        NoSelection = 1,
        InvalidRecord = 2,
        UnsupportedVersion = 3,
        ReadFailure = 4,
        PersistenceUnavailable = 5
    }

    internal enum CharacterSelectionPersistenceWriteStatus
    {
        Saved = 0,
        Unchanged = 1,
        Cleared = 2,
        InvalidRecord = 3,
        WriteFailure = 4,
        PersistenceUnavailable = 5,
        ShutdownRejected = 6
    }

    internal sealed class CharacterSelectionPersistenceManager
    {
        private const string Prefix =
            "[DesktopMascotCharacterPersistence]";

        private readonly CharacterSelectionStore store;
        private readonly CharacterSelectionSerializer serializer;
        private readonly bool loggingEnabled;
        private CharacterSelectionRecord current;
        private bool initialized;
        private bool available;
        private bool hasSelection;
        private bool hasStoredRecord;
        private bool shutdownStarted;
        private bool cleanupCompleted;

        internal CharacterSelectionPersistenceManager(
            CharacterSelectionStore selectionStore,
            CharacterSelectionSerializer selectionSerializer,
            bool enableLogging)
        {
            store = selectionStore ??
                throw new ArgumentNullException(nameof(selectionStore));
            serializer = selectionSerializer ??
                throw new ArgumentNullException(nameof(selectionSerializer));
            loggingEnabled = enableLogging;
        }

        internal static string ProductionPath
        {
            get
            {
                var root = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(
                    root,
                    "DesktopMascotMinimal",
                    "character-selection.json");
            }
        }

        internal bool IsInitialized => initialized;
        internal bool IsAvailable => available;
        internal bool HasSelection => hasSelection;
        internal bool HasStoredRecord => hasStoredRecord;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool CleanupCompleted => cleanupCompleted;
        internal int WriteCount { get; private set; }
        internal int ClearCount { get; private set; }
        internal CharacterSelectionPersistenceInitializationStatus
            InitializationStatus { get; private set; }
        internal CharacterSelectionValidationStatus ValidationStatus
        {
            get;
            private set;
        }

        internal static CharacterSelectionPersistenceManager
            CreateProduction() =>
            new CharacterSelectionPersistenceManager(
                new CharacterSelectionStore(ProductionPath),
                new CharacterSelectionSerializer(),
                true);

        internal static CharacterSelectionPersistenceManager
            CreateIsolated(string label)
        {
            var path = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M042",
                Process.GetCurrentProcess().Id.ToString(),
                string.IsNullOrWhiteSpace(label)
                    ? "isolated"
                    : label,
                "character-selection.json");
            return new CharacterSelectionPersistenceManager(
                new CharacterSelectionStore(path),
                new CharacterSelectionSerializer(),
                true);
        }

        internal CharacterSelectionPersistenceInitializationStatus
            Initialize()
        {
            if (initialized)
                return InitializationStatus;
            initialized = true;
            available = store.Initialize()
                == CharacterSelectionStoreStatus.Ready;
            if (!available)
            {
                InitializationStatus =
                    CharacterSelectionPersistenceInitializationStatus
                        .PersistenceUnavailable;
                LogWarning(
                    "Initialization status: PersistenceUnavailable");
                return InitializationStatus;
            }

            var readStatus = store.Read(
                out var json,
                out var failureType);
            hasStoredRecord =
                readStatus != CharacterSelectionStoreReadStatus.Missing;
            if (readStatus == CharacterSelectionStoreReadStatus.Missing)
            {
                InitializationStatus =
                    CharacterSelectionPersistenceInitializationStatus
                        .NoSelection;
                Log("Initialization status: NoSelection");
                return InitializationStatus;
            }
            if (readStatus != CharacterSelectionStoreReadStatus.Loaded)
            {
                InitializationStatus =
                    CharacterSelectionPersistenceInitializationStatus
                        .ReadFailure;
                LogWarning(
                    "Initialization status/type: ReadFailure/" +
                    (string.IsNullOrEmpty(failureType)
                        ? readStatus.ToString()
                        : failureType));
                return InitializationStatus;
            }

            var validation = serializer.Deserialize(json);
            ValidationStatus = validation.Status;
            if (!validation.IsValid)
            {
                InitializationStatus =
                    validation.Status
                        == CharacterSelectionValidationStatus
                            .UnsupportedSchema
                        || validation.Status
                            == CharacterSelectionValidationStatus
                                .UnsupportedSelectionVersion
                        ? CharacterSelectionPersistenceInitializationStatus
                            .UnsupportedVersion
                        : CharacterSelectionPersistenceInitializationStatus
                            .InvalidRecord;
                LogWarning(
                    "Initialization validation status: " +
                    validation.Status);
                return InitializationStatus;
            }

            current = validation.Record;
            hasSelection = true;
            InitializationStatus =
                CharacterSelectionPersistenceInitializationStatus.Loaded;
            Log(
                "Initialization status/path present: Loaded/True");
            return InitializationStatus;
        }

        internal bool TryGetSelection(
            out CharacterSelectionRecord record)
        {
            record = current;
            return initialized && hasSelection;
        }

        internal CharacterSelectionPersistenceWriteStatus
            TrySaveRuntimeSelection(
                string absolutePath,
                string contentSha256,
                Action<string, string> commitOverride = null)
        {
            if (shutdownStarted)
            {
                return CharacterSelectionPersistenceWriteStatus
                    .ShutdownRejected;
            }
            if (!initialized || !available)
            {
                return CharacterSelectionPersistenceWriteStatus
                    .PersistenceUnavailable;
            }
            if (!serializer.TryCreateRecord(
                    absolutePath,
                    contentSha256,
                    out var candidate)
                || !serializer.TrySerialize(candidate, out var json))
            {
                return CharacterSelectionPersistenceWriteStatus
                    .InvalidRecord;
            }
            if (hasSelection && current.Equals(candidate))
            {
                return CharacterSelectionPersistenceWriteStatus.Unchanged;
            }
            if (!store.TryWriteAtomically(
                    json,
                    out var failureType,
                    commitOverride))
            {
                LogWarning(
                    "Save failure type/status: " +
                    $"{failureType}/WriteFailure");
                return CharacterSelectionPersistenceWriteStatus.WriteFailure;
            }
            current = candidate;
            hasSelection = true;
            hasStoredRecord = true;
            WriteCount++;
            Log("Save result/path present: Saved/True");
            return CharacterSelectionPersistenceWriteStatus.Saved;
        }

        internal CharacterSelectionPersistenceWriteStatus TryClear()
        {
            if (shutdownStarted)
            {
                return CharacterSelectionPersistenceWriteStatus
                    .ShutdownRejected;
            }
            if (!initialized || !available)
            {
                return CharacterSelectionPersistenceWriteStatus
                    .PersistenceUnavailable;
            }
            if (!hasStoredRecord)
                return CharacterSelectionPersistenceWriteStatus.Unchanged;
            if (!store.TryClear(out var failureType))
            {
                LogWarning(
                    "Clear failure type/status: " +
                    $"{failureType}/WriteFailure");
                return CharacterSelectionPersistenceWriteStatus.WriteFailure;
            }
            current = default;
            hasSelection = false;
            hasStoredRecord = false;
            ClearCount++;
            Log("Clear result: Cleared");
            return CharacterSelectionPersistenceWriteStatus.Cleared;
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
            Log("Shutdown barrier closed: True");
        }

        internal bool Cleanup()
        {
            if (cleanupCompleted)
                return true;
            BeginShutdown();
            current = default;
            hasSelection = false;
            cleanupCompleted = true;
            Log("Cleanup result: True");
            return true;
        }

        private void Log(string message)
        {
            if (loggingEnabled)
                Debug.Log($"{Prefix} {message}");
        }

        private void LogWarning(string message)
        {
            if (loggingEnabled)
                Debug.LogWarning($"{Prefix} {message}");
        }
    }
}
