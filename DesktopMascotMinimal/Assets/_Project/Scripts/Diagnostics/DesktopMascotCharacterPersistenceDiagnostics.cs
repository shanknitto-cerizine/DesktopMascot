using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesktopMascot.Runtime.CharacterPersistence;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotCharacterPersistenceDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotCharacterPersistenceDiagnostics]";

        internal static bool Passed { get; private set; }

        internal static bool RunFocusedTests()
        {
            var root = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M042-focused",
                Guid.NewGuid().ToString("N"));
            var results = new List<bool>();
            try
            {
                Directory.CreateDirectory(root);
                results.Add(Run(
                    "Missing record and directory initialization",
                    () => TestMissing(root)));
                results.Add(Run(
                    "Valid Japanese path save and reload",
                    () => TestSaveReload(root)));
                results.Add(Run(
                    "Strict schema and record validation",
                    TestStrictValidation));
                results.Add(Run(
                    "Invalid records are preserved for recovery",
                    () => TestInvalidRecordPreservation(root)));
                results.Add(Run(
                    "Oversized records are rejected",
                    () => TestOversizedRecord(root)));
                results.Add(Run(
                    "Atomic replacement and commit failure",
                    () => TestAtomicWrite(root)));
                results.Add(Run(
                    "Duplicate suppression and alternate path update",
                    () => TestDuplicateAndPathUpdate(root)));
                results.Add(Run(
                    "Persistence unavailable",
                    () => TestUnavailable(root)));
                results.Add(Run(
                    "Shutdown rejects new persistence writes",
                    () => TestShutdownBarrier(root)));
                results.Add(Run(
                    "Clear keeps runtime responsibility separate",
                    () => TestClear(root)));
                results.Add(Run(
                    "Production path isolation",
                    () => TestProductionIsolation(root)));
                Passed = results.All(value => value);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{Prefix} Unexpected failure type: " +
                    exception.GetType().Name);
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
                        $"{Prefix} Cleanup failure type: " +
                        exception.GetType().Name);
                }
            }
            Debug.Log($"{Prefix} Automated tests passed: {Passed}");
            return Passed;
        }

        private static bool TestMissing(string root)
        {
            var path = Path.Combine(root, "missing", "selection.json");
            var manager = Manager(path);
            var status = manager.Initialize();
            return status
                    == CharacterSelectionPersistenceInitializationStatus
                        .NoSelection
                && manager.IsAvailable
                && !manager.HasSelection
                && Directory.Exists(Path.GetDirectoryName(path))
                && manager.Cleanup();
        }

        private static bool TestSaveReload(string root)
        {
            var storage = Path.Combine(root, "reload", "selection.json");
            var selected = Path.GetFullPath(
                Path.Combine(root, "日本語 モデル", "モデル.vrm"));
            var manager = Manager(storage);
            manager.Initialize();
            var save = manager.TrySaveRuntimeSelection(selected, Hash('a'));
            var reload = Manager(storage);
            var initialized = reload.Initialize();
            return save == CharacterSelectionPersistenceWriteStatus.Saved
                && initialized
                    == CharacterSelectionPersistenceInitializationStatus
                        .Loaded
                && reload.TryGetSelection(out var record)
                && string.Equals(
                    record.AbsolutePath,
                    selected,
                    StringComparison.OrdinalIgnoreCase)
                && record.ContentSha256 == Hash('a');
        }

        private static bool TestStrictValidation()
        {
            var serializer = new CharacterSelectionSerializer();
            var validPath = @"C:\モデル\sample.vrm";
            var valid = Json(1, 1, validPath, Hash('b'));
            var cases = new[]
            {
                serializer.Deserialize(valid).IsValid,
                serializer.Deserialize("   ").Status
                    == CharacterSelectionValidationStatus.InvalidJson,
                serializer.Deserialize("not-json").Status
                    == CharacterSelectionValidationStatus.InvalidJson,
                serializer.Deserialize(
                    Json(-1, 1, validPath, Hash('b'))).Status
                    == CharacterSelectionValidationStatus.NegativeSchema,
                serializer.Deserialize(
                    Json(2, 1, validPath, Hash('b'))).Status
                    == CharacterSelectionValidationStatus.UnsupportedSchema,
                serializer.Deserialize(
                    Json(1, 2, validPath, Hash('b'))).Status
                    == CharacterSelectionValidationStatus
                        .UnsupportedSelectionVersion,
                serializer.Deserialize(
                    Json(1, 0, validPath, Hash('b'))).Status
                    == CharacterSelectionValidationStatus
                        .InvalidSelectionVersion,
                serializer.Deserialize(
                    "{\"schemaVersion\":1,\"selectionVersion\":1," +
                    "\"absolutePath\":\"C:\\\\a.vrm\"}").Status
                    == CharacterSelectionValidationStatus
                        .MissingRequiredMember,
                serializer.Deserialize(
                    Json(1, 1, "relative.vrm", Hash('b'))).Status
                    == CharacterSelectionValidationStatus.InvalidPath,
                serializer.Deserialize(
                    Json(1, 1, @"C:\folder", Hash('b'))).Status
                    == CharacterSelectionValidationStatus.InvalidPath,
                serializer.Deserialize(
                    Json(1, 1, validPath, "bad")).Status
                    == CharacterSelectionValidationStatus.InvalidSha256,
                serializer.Deserialize(
                    "{\"schemaVersion\":\"1\",\"selectionVersion\":1," +
                    "\"absolutePath\":\"C:\\\\a.vrm\"," +
                    $"\"contentSha256\":\"{Hash('b')}\"}}").Status
                    == CharacterSelectionValidationStatus.TypeMismatch,
                serializer.Deserialize(
                    valid.TrimEnd('}', '\r', '\n')
                    + ",\"schemaVersion\":1}").Status
                    == CharacterSelectionValidationStatus.DuplicateMember,
                serializer.Deserialize(
                    valid.TrimEnd('}', '\r', '\n')
                    + ",\"unknown\":1}").Status
                    == CharacterSelectionValidationStatus.UnknownMember,
                !serializer.TryCreateRecord(
                    @"C:\" +
                    new string(
                        'a',
                        CharacterSelectionSerializer
                            .MaximumPathCharacters) +
                    ".vrm",
                    Hash('b'),
                    out _)
            };
            return cases.All(value => value);
        }

        private static bool TestInvalidRecordPreservation(string root)
        {
            var path = Path.Combine(
                root,
                "invalid-preserved",
                "selection.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const string invalid =
                "{\"schemaVersion\":99,\"selectionVersion\":1," +
                "\"absolutePath\":\"C:\\\\sample.vrm\"," +
                "\"contentSha256\":\"" +
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}";
            File.WriteAllText(path, invalid, new UTF8Encoding(false));
            var manager = Manager(path);
            var status = manager.Initialize();
            return status
                    == CharacterSelectionPersistenceInitializationStatus
                        .UnsupportedVersion
                && manager.HasStoredRecord
                && !manager.HasSelection
                && string.Equals(
                    File.ReadAllText(path, Encoding.UTF8),
                    invalid,
                    StringComparison.Ordinal);
        }

        private static bool TestOversizedRecord(string root)
        {
            var path = Path.Combine(
                root,
                "oversized",
                "selection.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                stream.SetLength(
                    CharacterSelectionStore.MaximumRecordBytes + 1);
            }
            var manager = Manager(path);
            return manager.Initialize()
                    == CharacterSelectionPersistenceInitializationStatus
                        .ReadFailure
                && manager.HasStoredRecord
                && !manager.HasSelection;
        }

        private static bool TestAtomicWrite(string root)
        {
            var storage = Path.Combine(root, "atomic", "selection.json");
            var manager = Manager(storage);
            manager.Initialize();
            var first = manager.TrySaveRuntimeSelection(
                @"C:\first.vrm",
                Hash('c'));
            var second = manager.TrySaveRuntimeSelection(
                @"C:\second.vrm",
                Hash('d'));
            var failed = manager.TrySaveRuntimeSelection(
                @"C:\third.vrm",
                Hash('e'),
                (_, __) => throw new IOException(
                    "simulated commit failure"));
            var reload = Manager(storage);
            reload.Initialize();
            var temporaryCount = Directory.GetFiles(
                Path.GetDirectoryName(storage),
                "*.tmp").Length;
            return first == CharacterSelectionPersistenceWriteStatus.Saved
                && second
                    == CharacterSelectionPersistenceWriteStatus.Saved
                && failed
                    == CharacterSelectionPersistenceWriteStatus.WriteFailure
                && reload.TryGetSelection(out var record)
                && record.ContentSha256 == Hash('d')
                && temporaryCount == 0;
        }

        private static bool TestUnavailable(string root)
        {
            var path = Path.Combine(root, "unavailable", "selection.json");
            var store = new CharacterSelectionStore(
                path,
                _ => throw new UnauthorizedAccessException());
            var manager = new CharacterSelectionPersistenceManager(
                store,
                new CharacterSelectionSerializer(),
                false);
            var status = manager.Initialize();
            return status
                    == CharacterSelectionPersistenceInitializationStatus
                        .PersistenceUnavailable
                && !manager.IsAvailable
                && manager.TrySaveRuntimeSelection(
                    @"C:\sample.vrm",
                    Hash('f'))
                    == CharacterSelectionPersistenceWriteStatus
                        .PersistenceUnavailable;
        }

        private static bool TestDuplicateAndPathUpdate(string root)
        {
            var path = Path.Combine(
                root,
                "duplicate",
                "selection.json");
            var manager = Manager(path);
            manager.Initialize();
            var first = manager.TrySaveRuntimeSelection(
                @"C:\first\sample.vrm",
                Hash('a'));
            var unchanged = manager.TrySaveRuntimeSelection(
                @"C:\first\sample.vrm",
                Hash('A'));
            var alternatePath = manager.TrySaveRuntimeSelection(
                @"C:\second\sample.vrm",
                Hash('a'));
            return first == CharacterSelectionPersistenceWriteStatus.Saved
                && unchanged
                    == CharacterSelectionPersistenceWriteStatus.Unchanged
                && alternatePath
                    == CharacterSelectionPersistenceWriteStatus.Saved
                && manager.WriteCount == 2
                && manager.TryGetSelection(out var record)
                && string.Equals(
                    record.AbsolutePath,
                    @"C:\second\sample.vrm",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool TestShutdownBarrier(string root)
        {
            var path = Path.Combine(
                root,
                "shutdown",
                "selection.json");
            var manager = Manager(path);
            manager.Initialize();
            manager.BeginShutdown();
            return manager.TrySaveRuntimeSelection(
                    @"C:\sample.vrm",
                    Hash('f'))
                    == CharacterSelectionPersistenceWriteStatus
                        .ShutdownRejected
                && manager.TryClear()
                    == CharacterSelectionPersistenceWriteStatus
                        .ShutdownRejected
                && manager.Cleanup()
                && manager.Cleanup();
        }

        private static bool TestClear(string root)
        {
            var path = Path.Combine(root, "clear", "selection.json");
            var manager = Manager(path);
            manager.Initialize();
            manager.TrySaveRuntimeSelection(@"C:\sample.vrm", Hash('1'));
            var cleared = manager.TryClear();
            return cleared
                    == CharacterSelectionPersistenceWriteStatus.Cleared
                && !manager.HasSelection
                && !manager.HasStoredRecord
                && !File.Exists(path);
        }

        private static bool TestProductionIsolation(string root)
        {
            var isolated = Path.GetFullPath(
                Path.Combine(root, "isolated", "selection.json"));
            var production = Path.GetFullPath(
                CharacterSelectionPersistenceManager.ProductionPath);
            return !string.Equals(
                isolated,
                production,
                StringComparison.OrdinalIgnoreCase)
                && production.EndsWith(
                    Path.Combine(
                        "DesktopMascotMinimal",
                        "character-selection.json"),
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    Path.GetFileName(production),
                    "settings.json",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    Path.GetFileName(production),
                    "window-position.json",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static CharacterSelectionPersistenceManager Manager(
            string path) =>
            new CharacterSelectionPersistenceManager(
                new CharacterSelectionStore(path),
                new CharacterSelectionSerializer(),
                false);

        private static string Json(
            int schema,
            int selection,
            string path,
            string hash)
        {
            var escaped = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return
                $"{{\"schemaVersion\":{schema}," +
                $"\"selectionVersion\":{selection}," +
                $"\"absolutePath\":\"{escaped}\"," +
                $"\"contentSha256\":\"{hash}\"}}";
        }

        private static string Hash(char value) =>
            new string(value, 64);

        private static bool Run(string name, Func<bool> test)
        {
            var result = test();
            Debug.Log($"{Prefix} {name}: {result}");
            return result;
        }
    }
}
