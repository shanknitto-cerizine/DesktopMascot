using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopMascot.Runtime
{
    internal sealed class WindowPositionPersistence
    {
        private const string Prefix =
            "[DesktopMascotWindowPosition]";
        private static readonly WindowPosition DefaultPosition =
            new WindowPosition(100, 100);

        private readonly WindowPositionStore store;
        private readonly bool enabled;
        private WindowPosition lastSavedPosition;
        private bool hasLastSavedPosition;
        private ulong observedCompletedDragGeneration;
        private int writeCount;

        internal WindowPositionPersistence(
            WindowPositionStore store,
            bool enabled)
        {
            this.store = store;
            this.enabled = enabled;
        }

        internal bool Enabled => enabled;
        internal string StoragePath => store?.Path ?? string.Empty;
        internal int WriteCount => writeCount;

        internal static WindowPositionPersistence CreateProduction()
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(
                root,
                "DesktopMascotMinimal",
                "window-position.json");
            return new WindowPositionPersistence(
                new WindowPositionStore(path),
                true);
        }

        internal static WindowPositionPersistence CreateIsolated()
        {
            var path = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M031",
                Process.GetCurrentProcess().Id.ToString(),
                "window-position.json");
            return new WindowPositionPersistence(
                new WindowPositionStore(path),
                true);
        }

        internal static WindowPositionPersistence CreateDisabled() =>
            new WindowPositionPersistence(null, false);

        internal WindowPosition PrepareInitialPosition(
            int windowWidth,
            int windowHeight)
        {
            if (!enabled)
            {
                Log("Persistence disabled for selected runtime mode.");
                return Resolve(
                    false,
                    default,
                    windowWidth,
                    windowHeight).Position;
            }

            var load = store.Load();
            LogLoadDecision(load);
            var resolution = Resolve(
                load.HasPosition,
                load.Position,
                windowWidth,
                windowHeight);
            LogResolution(resolution);
            if (load.HasPosition)
            {
                hasLastSavedPosition = true;
                lastSavedPosition = load.Position;
            }

            if (load.HasPosition
                && resolution.Kind
                    == PositionResolutionKind.SavedPositionClamped)
            {
                SaveResolvedPosition(
                    resolution.Position,
                    "recovered position");
            }
            return resolution.Position;
        }

        internal void PollCompletedDrag(
            bool dragging,
            bool captureOwned)
        {
            if (!enabled || dragging || captureOwned)
                return;
            var generation =
                NativeMascotWindowPositionBridge
                    .GetCompletedDragGeneration();
            if (generation <= observedCompletedDragGeneration)
                return;
            if (!NativeMascotWindowPositionBridge.TryGetCurrentPosition(
                    out var position))
            {
                observedCompletedDragGeneration = generation;
                Debug.LogWarning(
                    $"{Prefix} Completed drag position was unavailable.");
                return;
            }
            ProcessCompletedDrag(generation, position, false, false);
        }

        internal void SaveFinalCompletedPosition(
            bool dragging,
            bool captureOwned)
        {
            PollCompletedDrag(dragging, captureOwned);
            Log(
                $"Final save check complete. Writes: {writeCount}");
        }

        internal bool ProcessCompletedDrag(
            ulong generation,
            WindowPosition position,
            bool dragging,
            bool captureOwned)
        {
            if (!enabled || dragging || captureOwned
                || generation <= observedCompletedDragGeneration)
            {
                return false;
            }
            observedCompletedDragGeneration = generation;
            if (hasLastSavedPosition
                && lastSavedPosition.Equals(position))
            {
                Log(
                    $"Completed drag generation {generation} did not change position; write skipped.");
                return true;
            }
            return SaveResolvedPosition(
                position,
                $"completed drag generation {generation}");
        }

        private PositionResolution Resolve(
            bool hasSavedPosition,
            WindowPosition savedPosition,
            int windowWidth,
            int windowHeight)
        {
            return WindowsScreenBoundsService.Resolve(
                hasSavedPosition,
                savedPosition,
                DefaultPosition,
                windowWidth,
                windowHeight,
                WindowsScreenBoundsService.GetCurrentWorkAreas());
        }

        private bool SaveResolvedPosition(
            WindowPosition position,
            string reason)
        {
            if (!store.TrySave(position, out var error))
            {
                Debug.LogWarning(
                    $"{Prefix} Write failed ({reason}): {error}");
                return false;
            }
            lastSavedPosition = position;
            hasLastSavedPosition = true;
            ++writeCount;
            Log($"Write succeeded ({reason}): {position}");
            return true;
        }

        private static void LogLoadDecision(
            WindowPositionLoadResult load)
        {
            switch (load.Status)
            {
                case WindowPositionLoadStatus.Loaded:
                    Log($"Saved position loaded: {load.Position}");
                    break;
                case WindowPositionLoadStatus.Missing:
                    Log("No saved position; using the production default.");
                    break;
                case WindowPositionLoadStatus.UnsupportedSchema:
                    Log(
                        $"Unsupported schema ({load.Detail}); saved data was not interpreted.");
                    break;
                case WindowPositionLoadStatus.ParseFailure:
                    Log("Saved position parse failure; using safe recovery.");
                    break;
                case WindowPositionLoadStatus.NumericValidationFailure:
                    Log(
                        "Saved position numeric validation failure; using safe recovery.");
                    break;
                default:
                    Log(
                        $"Saved position read failure ({load.Detail}); using safe recovery.");
                    break;
            }
        }

        private static void LogResolution(PositionResolution resolution)
        {
            switch (resolution.Kind)
            {
                case PositionResolutionKind.SavedPositionAccepted:
                    Log($"Saved position accepted: {resolution.Position}");
                    break;
                case PositionResolutionKind.SavedPositionClamped:
                    Log($"Saved position clamped: {resolution.Position}");
                    break;
                case PositionResolutionKind.RecoveredToPrimaryOrDefault:
                    Log(
                        $"Position recovered to primary/default: {resolution.Position}");
                    break;
                default:
                    Log($"Default position selected: {resolution.Position}");
                    break;
            }
        }

        private static void Log(string message) =>
            Debug.Log($"{Prefix} {message}");
    }
}
