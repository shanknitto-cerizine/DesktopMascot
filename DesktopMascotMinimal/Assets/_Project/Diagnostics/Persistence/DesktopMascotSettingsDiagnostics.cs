using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DesktopMascot.Runtime.Settings;
using UnityEngine;
using RuntimeSettingsStore =
    DesktopMascot.Runtime.Settings.SettingsStore;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotSettingsDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotSettingsDiagnostics]";

        internal static bool Passed { get; private set; }

        internal static bool RunFocusedTests()
        {
            var root = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M033-tests",
                Guid.NewGuid().ToString("N"));
            var results = new List<bool>();
            try
            {
                Directory.CreateDirectory(root);
                results.Add(Run("Missing settings", () =>
                    TestMissing(root)));
                results.Add(Run("Existing settings", () =>
                    TestExisting(root)));
                results.Add(Run("Corrupt settings recovery", () =>
                    TestRecovery(
                        root,
                        "corrupt.json",
                        "not-json",
                        SettingsValidationStatus.InvalidJson)));
                results.Add(Run("Negative schema recovery", () =>
                    TestRecovery(
                        root,
                        "negative-schema.json",
                        Json(-1, 1, false),
                        SettingsValidationStatus.NegativeSchema)));
                results.Add(Run("Future schema recovery", () =>
                    TestRecovery(
                        root,
                        "future-schema.json",
                        Json(99, 1, false),
                        SettingsValidationStatus.UnsupportedSchema)));
                results.Add(Run("Missing member recovery", () =>
                    TestRecovery(
                        root,
                        "missing-member.json",
                        "{\"schemaVersion\":1,\"settingsVersion\":1}",
                        SettingsValidationStatus.MissingRequiredMember)));
                results.Add(Run("Type mismatch recovery", () =>
                    TestRecovery(
                        root,
                        "type-mismatch.json",
                        "{\"schemaVersion\":1,\"settingsVersion\":1," +
                        "\"firstRunCompleted\":\"false\"}",
                        SettingsValidationStatus.TypeMismatch)));
                results.Add(Run("Startup version recovery", () =>
                    TestRecovery(
                        root,
                        "future-settings-version.json",
                        Json(1, 2, false),
                        SettingsValidationStatus
                            .UnsupportedSettingsVersion)));
                results.Add(Run("Partial write simulation", () =>
                    TestPartialWrite(root)));
                results.Add(Run("Atomic replacement", () =>
                    TestAtomicReplacement(root)));
                results.Add(Run("Production isolation", () =>
                    TestProductionIsolation(root)));
                Passed = results.TrueForAll(value => value);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{Prefix} Unexpected test failure: {exception}");
                Passed = false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"{Prefix} Temporary cleanup failed: " +
                        exception.Message);
                }
            }
            Debug.Log($"{Prefix} Automated tests passed: {Passed}");
            return Passed;
        }

        private static bool TestMissing(string root)
        {
            var path = Path.Combine(root, "missing.json");
            var manager = Manager(path);
            var result = manager.Initialize();
            return result.Status
                    == SettingsInitializationStatus.DefaultsCreated
                && result.WriteSucceeded
                && manager.IsInitialized
                && manager.Current.Equals(SettingsDefaults.Create())
                && Read(path).IsValid;
        }

        private static bool TestExisting(string root)
        {
            var path = Path.Combine(root, "existing.json");
            var expected = new SettingsValues(1, 1, true);
            if (!Write(path, expected))
                return false;
            var manager = Manager(path);
            var result = manager.Initialize();
            return result.Status == SettingsInitializationStatus.Loaded
                && result.WriteSucceeded
                && manager.Current.Equals(expected);
        }

        private static bool TestRecovery(
            string root,
            string name,
            string invalidJson,
            SettingsValidationStatus expectedValidation)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, invalidJson, Encoding.UTF8);
            var manager = Manager(path);
            var result = manager.Initialize();
            var recovered = Read(path);
            return result.Status
                    == SettingsInitializationStatus.DefaultsRecovered
                && result.ValidationStatus == expectedValidation
                && result.WriteSucceeded
                && manager.Current.Equals(SettingsDefaults.Create())
                && recovered.IsValid
                && recovered.Values.Equals(SettingsDefaults.Create());
        }

        private static bool TestPartialWrite(string root)
        {
            var path = Path.Combine(root, "partial-write.json");
            var original = SettingsDefaults.Create();
            var replacement = new SettingsValues(1, 1, true);
            if (!Write(path, original))
                return false;

            var serializer = new SettingsSerializer();
            if (!serializer.TrySerialize(replacement, out var json))
                return false;
            var store = new RuntimeSettingsStore(path);
            var result = store.TryWriteAtomically(
                json,
                out _,
                (temporary, final) =>
                    throw new IOException("simulated commit failure"));
            var temporaryFiles = Directory.GetFiles(
                root,
                "partial-write.json.*.tmp");
            return !result
                && temporaryFiles.Length == 0
                && Read(path).Values.Equals(original);
        }

        private static bool TestAtomicReplacement(string root)
        {
            var path = Path.Combine(root, "replace.json");
            var replacement = new SettingsValues(1, 1, true);
            return Write(path, SettingsDefaults.Create())
                && Write(path, replacement)
                && Read(path).Values.Equals(replacement);
        }

        private static bool TestProductionIsolation(string root)
        {
            var testPath = Path.GetFullPath(
                Path.Combine(root, "isolated.json"));
            var productionPath = Path.GetFullPath(
                SettingsManager.ProductionPath);
            return !string.Equals(
                testPath,
                productionPath,
                StringComparison.OrdinalIgnoreCase)
                && !testPath.StartsWith(
                    Path.GetDirectoryName(productionPath) +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static SettingsManager Manager(string path) =>
            new SettingsManager(
                new RuntimeSettingsStore(path),
                new SettingsSerializer(),
                false);

        private static bool Write(
            string path,
            SettingsValues values)
        {
            var serializer = new SettingsSerializer();
            return serializer.TrySerialize(values, out var json)
                && new RuntimeSettingsStore(path).TryWriteAtomically(
                    json,
                    out _);
        }

        private static SettingsValidationResult Read(string path)
        {
            var store = new RuntimeSettingsStore(path);
            return store.Read(out var json, out _)
                    == SettingsStoreReadStatus.Loaded
                ? new SettingsSerializer().Deserialize(json)
                : new SettingsValidationResult(
                    SettingsValidationStatus.InvalidJson,
                    default,
                    string.Empty);
        }

        private static string Json(
            int schemaVersion,
            int settingsVersion,
            bool firstRunCompleted) =>
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"schemaVersion\":{0},\"settingsVersion\":{1}," +
                "\"firstRunCompleted\":{2}}}",
                schemaVersion,
                settingsVersion,
                firstRunCompleted ? "true" : "false");

        private static bool Run(string name, Func<bool> test)
        {
            var passed = test();
            Debug.Log($"{Prefix} {name}: {passed}");
            return passed;
        }
    }
}
