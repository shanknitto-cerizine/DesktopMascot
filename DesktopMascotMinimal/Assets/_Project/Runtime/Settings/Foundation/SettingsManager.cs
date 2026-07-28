using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Runtime.Settings
{
    internal enum SettingsInitializationStatus
    {
        Loaded,
        DefaultsCreated,
        DefaultsRecovered,
        DefaultsInMemoryAfterWriteFailure
    }

    internal readonly struct SettingsInitializationResult
    {
        internal SettingsInitializationResult(
            SettingsInitializationStatus status,
            SettingsValidationStatus validationStatus,
            bool writeSucceeded)
        {
            Status = status;
            ValidationStatus = validationStatus;
            WriteSucceeded = writeSucceeded;
        }

        internal SettingsInitializationStatus Status { get; }
        internal SettingsValidationStatus ValidationStatus { get; }
        internal bool WriteSucceeded { get; }
    }

    internal sealed class SettingsManager
    {
        private const string Prefix = "[DesktopMascotSettings]";

        private readonly SettingsStore store;
        private readonly SettingsSerializer serializer;
        private readonly bool loggingEnabled;
        private SettingsValues current;
        private bool initialized;

        internal SettingsManager(
            SettingsStore store,
            SettingsSerializer serializer,
            bool loggingEnabled)
        {
            this.store = store ??
                throw new ArgumentNullException(nameof(store));
            this.serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));
            this.loggingEnabled = loggingEnabled;
        }

        internal string StoragePath => store.Path;
        internal SettingsValues Current => current;
        internal bool IsInitialized => initialized;

        internal static string ProductionPath
        {
            get
            {
                var root = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(
                    root,
                    "DesktopMascotMinimal",
                    "settings.json");
            }
        }

        internal static SettingsManager CreateProduction() =>
            new SettingsManager(
                new SettingsStore(ProductionPath),
                new SettingsSerializer(),
                true);

        internal static SettingsManager CreateIsolated(string mode)
        {
            var path = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M033",
                Process.GetCurrentProcess().Id.ToString(),
                string.IsNullOrWhiteSpace(mode) ? "diagnostic" : mode,
                "settings.json");
            return new SettingsManager(
                new SettingsStore(path),
                new SettingsSerializer(),
                true);
        }

        internal SettingsInitializationResult Initialize()
        {
            try
            {
                var readStatus = store.Read(
                    out var json,
                    out var readError);
                if (readStatus == SettingsStoreReadStatus.Loaded)
                {
                    var validation = serializer.Deserialize(json);
                    if (validation.IsValid)
                    {
                        current = validation.Values;
                        initialized = true;
                        Log(
                            $"Loaded schema={current.SchemaVersion}, " +
                            $"settings={current.SettingsVersion}.");
                        return new SettingsInitializationResult(
                            SettingsInitializationStatus.Loaded,
                            SettingsValidationStatus.Valid,
                            true);
                    }
                    LogWarning(
                        $"Validation failed ({validation.Status}); " +
                        "recovering defaults.");
                    return RecoverDefaults(
                        SettingsInitializationStatus.DefaultsRecovered,
                        validation.Status);
                }
                if (readStatus == SettingsStoreReadStatus.ReadFailure)
                {
                    LogWarning(
                        $"Read failed ({readError}); recovering defaults.");
                    return RecoverDefaults(
                        SettingsInitializationStatus.DefaultsRecovered,
                        SettingsValidationStatus.InvalidJson);
                }
                return RecoverDefaults(
                    SettingsInitializationStatus.DefaultsCreated,
                    SettingsValidationStatus.Valid);
            }
            catch (Exception exception)
            {
                LogWarning(
                    "Unexpected initialization failure; using defaults: " +
                    exception.GetType().Name);
                current = SettingsDefaults.Create();
                initialized = true;
                return new SettingsInitializationResult(
                    SettingsInitializationStatus
                        .DefaultsInMemoryAfterWriteFailure,
                    SettingsValidationStatus.InvalidJson,
                    false);
            }
        }

        internal bool Save()
        {
            return TrySave(current);
        }

        internal bool TrySave(SettingsValues values)
        {
            if (!initialized
                || !serializer.TrySerialize(values, out var json))
            {
                return false;
            }
            if (store.TryWriteAtomically(json, out var error))
            {
                current = values;
                return true;
            }
            LogWarning($"Save failed: {error}");
            return false;
        }

        private SettingsInitializationResult RecoverDefaults(
            SettingsInitializationStatus successStatus,
            SettingsValidationStatus validationStatus)
        {
            current = SettingsDefaults.Create();
            initialized = true;
            var writeError = "serialization failed";
            if (!serializer.TrySerialize(current, out var json)
                || !store.TryWriteAtomically(json, out writeError))
            {
                LogWarning(
                    $"Defaults remain in memory; write failed: {writeError}");
                return new SettingsInitializationResult(
                    SettingsInitializationStatus
                        .DefaultsInMemoryAfterWriteFailure,
                    validationStatus,
                    false);
            }

            var reloadStatus = store.Read(
                out var reloadedJson,
                out var reloadError);
            var reload = reloadStatus == SettingsStoreReadStatus.Loaded
                ? serializer.Deserialize(reloadedJson)
                : new SettingsValidationResult(
                    SettingsValidationStatus.InvalidJson,
                    default,
                    reloadError);
            if (!reload.IsValid)
            {
                LogWarning(
                    "Defaults were written but could not be reloaded; " +
                    "continuing with in-memory defaults.");
                return new SettingsInitializationResult(
                    SettingsInitializationStatus
                        .DefaultsInMemoryAfterWriteFailure,
                    validationStatus,
                    false);
            }
            current = reload.Values;
            Log(
                successStatus
                    == SettingsInitializationStatus.DefaultsCreated
                    ? "Default settings created."
                    : "Invalid settings replaced with defaults.");
            return new SettingsInitializationResult(
                successStatus,
                validationStatus,
                true);
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
