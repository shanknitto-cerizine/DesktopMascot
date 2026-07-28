using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DesktopMascot.Runtime;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class
        DesktopMascotWindowPositionPersistenceDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotWindowPositionDiagnostics]";

        internal static bool Passed { get; private set; }

        internal static bool RunFocusedTests()
        {
            var root = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M031-tests",
                Guid.NewGuid().ToString("N"));
            var results = new List<bool>();
            try
            {
                Directory.CreateDirectory(root);
                results.Add(Run("No saved file", () =>
                    TestNoSavedFile(root)));
                results.Add(Run("Valid saved position", () =>
                    TestValidSavedPosition(root)));
                results.Add(Run("Negative monitor coordinates", () =>
                    TestNegativeCoordinates()));
                results.Add(Run("Far off-screen recovery", () =>
                    TestFarOffScreen()));
                results.Add(Run("Valid edge placement accepted", () =>
                    TestValidEdgePlacement()));
                results.Add(Run("Removed monitor recovery", () =>
                    TestRemovedMonitorRecovery()));
                results.Add(Run("Oversized window recovery", () =>
                    TestOversizedWindowRecovery()));
                results.Add(Run("Corrupt data", () =>
                    TestCorruptData(root)));
                results.Add(Run("Unsupported schema", () =>
                    TestUnsupportedSchema(root)));
                results.Add(Run("Atomic write failure", () =>
                    TestAtomicWriteFailure(root)));
                results.Add(Run("Drag completion write count", () =>
                    TestDragCompletion(root)));
                results.Add(Run("Restart simulation", () =>
                    TestRestartSimulation(root)));
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
                        $"{Prefix} Temporary cleanup failed: {exception.Message}");
                }
            }
            Debug.Log($"{Prefix} Automated tests passed: {Passed}");
            return Passed;
        }

        private static bool TestNoSavedFile(string root)
        {
            var store = Store(root, "missing.json");
            var load = store.Load();
            var resolution = Resolve(false, default);
            return load.Status == WindowPositionLoadStatus.Missing
                && resolution.Position.Equals(new WindowPosition(100, 100));
        }

        private static bool TestValidSavedPosition(string root)
        {
            var store = Store(root, "valid.json");
            var expected = new WindowPosition(240, 180);
            return store.TrySave(expected, out _)
                && store.Load().Position.Equals(expected)
                && Resolve(true, expected).Kind
                    == PositionResolutionKind.SavedPositionAccepted;
        }

        private static bool TestNegativeCoordinates()
        {
            var areas = new[]
            {
                new MonitorWorkArea(-1920, 0, 0, 1040, false),
                new MonitorWorkArea(0, 0, 1920, 1040, true)
            };
            var expected = new WindowPosition(-1400, 200);
            var result = WindowsScreenBoundsService.Resolve(
                true,
                expected,
                new WindowPosition(100, 100),
                256,
                256,
                areas);
            return result.Position.Equals(expected)
                && result.Kind
                    == PositionResolutionKind.SavedPositionAccepted;
        }

        private static bool TestFarOffScreen()
        {
            var result = Resolve(
                true,
                new WindowPosition(900000, 900000));
            return result.Kind
                    == PositionResolutionKind.SavedPositionClamped
                && result.Position.Equals(
                    new WindowPosition(1664, 784))
                && IsEntireWindowInside(
                    result.Position,
                    256,
                    256,
                    PrimaryWorkArea()[0]);
        }

        private static bool TestValidEdgePlacement()
        {
            var expected = new WindowPosition(-208, 100);
            var result = Resolve(true, expected);
            return result.Position.Equals(expected)
                && result.Kind
                    == PositionResolutionKind.SavedPositionAccepted;
        }

        private static bool TestRemovedMonitorRecovery()
        {
            var areas = new[]
            {
                new MonitorWorkArea(-1920, 0, 0, 1040, false),
                new MonitorWorkArea(0, 0, 1920, 1040, true)
            };
            var result = WindowsScreenBoundsService.Resolve(
                true,
                new WindowPosition(3000, 300),
                new WindowPosition(100, 100),
                256,
                256,
                areas);
            return result.Kind
                    == PositionResolutionKind.SavedPositionClamped
                && result.Position.Equals(
                    new WindowPosition(1664, 300))
                && IsEntireWindowInside(
                    result.Position,
                    256,
                    256,
                    areas[1]);
        }

        private static bool TestOversizedWindowRecovery()
        {
            var area = new MonitorWorkArea(
                -100, 20, 100, 140, true);
            var result = WindowsScreenBoundsService.Resolve(
                true,
                new WindowPosition(
                    WindowPositionStore.MaximumAbsoluteCoordinate,
                    WindowPositionStore.MaximumAbsoluteCoordinate),
                new WindowPosition(100, 100),
                512,
                256,
                new[] { area });
            return result.Kind
                    == PositionResolutionKind.SavedPositionClamped
                && result.Position.Equals(
                    new WindowPosition(area.Left, area.Top))
                && WindowsScreenBoundsService.IsMinimumVisible(
                    result.Position,
                    512,
                    256,
                    new[] { area });
        }

        private static bool TestCorruptData(string root)
        {
            var store = Store(root, "corrupt.json");
            File.WriteAllText(
                store.Path,
                "{ definitely-not-json",
                Encoding.UTF8);
            return store.Load().Status
                == WindowPositionLoadStatus.ParseFailure;
        }

        private static bool TestUnsupportedSchema(string root)
        {
            var store = Store(root, "future.json");
            File.WriteAllText(
                store.Path,
                "{\"schemaVersion\":99,\"x\":100,\"y\":100}",
                Encoding.UTF8);
            return store.Load().Status
                == WindowPositionLoadStatus.UnsupportedSchema;
        }

        private static bool TestAtomicWriteFailure(string root)
        {
            var store = Store(root, "atomic.json");
            var original = new WindowPosition(10, 20);
            if (!store.TrySave(original, out _))
                return false;
            var saved = store.TrySave(
                new WindowPosition(30, 40),
                out _,
                (temporary, final) =>
                    throw new IOException("simulated commit failure"));
            return !saved
                && store.Load().Position.Equals(original);
        }

        private static bool TestDragCompletion(string root)
        {
            var path = Path.Combine(root, "drag", "position.json");
            var persistence = CreateTestPersistence(path);
            var position = new WindowPosition(300, 220);
            var cancelled = persistence.ProcessCompletedDrag(
                0, position, false, false);
            var completed = persistence.ProcessCompletedDrag(
                1, position, false, false);
            var duplicateGeneration = persistence.ProcessCompletedDrag(
                1, new WindowPosition(301, 221), false, false);
            var activeDrag = persistence.ProcessCompletedDrag(
                2, new WindowPosition(302, 222), true, true);
            return !cancelled
                && completed
                && !duplicateGeneration
                && !activeDrag
                && persistence.WriteCount == 1;
        }

        private static bool TestRestartSimulation(string root)
        {
            var store = Store(root, "restart.json");
            var expected = new WindowPosition(420, 310);
            return store.TrySave(expected, out _)
                && store.Load().HasPosition
                && Resolve(true, store.Load().Position)
                    .Position.Equals(expected);
        }

        private static WindowPositionPersistence
            CreateTestPersistence(string path)
        {
            return new WindowPositionPersistence(
                new WindowPositionStore(path),
                true);
        }

        private static WindowPositionStore Store(
            string root,
            string name)
        {
            var path = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return new WindowPositionStore(path);
        }

        private static PositionResolution Resolve(
            bool hasSaved,
            WindowPosition position) =>
            WindowsScreenBoundsService.Resolve(
                hasSaved,
                position,
                new WindowPosition(100, 100),
                256,
                256,
                PrimaryWorkArea());

        private static IReadOnlyList<MonitorWorkArea>
            PrimaryWorkArea() =>
            new[]
            {
                new MonitorWorkArea(0, 0, 1920, 1040, true)
            };

        private static bool IsEntireWindowInside(
            WindowPosition position,
            int width,
            int height,
            MonitorWorkArea area) =>
            position.X >= area.Left
            && position.Y >= area.Top
            && (long)position.X + width <= area.Right
            && (long)position.Y + height <= area.Bottom;

        private static bool Run(string name, Func<bool> test)
        {
            var passed = test();
            Debug.Log($"{Prefix} {name}: {passed}");
            return passed;
        }
    }
}
