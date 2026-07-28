using System;
using System.Collections.Generic;
using System.IO;
using DesktopMascot.Runtime.Settings;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;
using RuntimeSettingsStore =
    DesktopMascot.Runtime.Settings.SettingsStore;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotPlayerVisibilityDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotPlayerVisibilityDiagnostics]";

        internal static bool Passed { get; private set; }

        internal static bool RunFocusedTests()
        {
            var root = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M037-tests",
                Guid.NewGuid().ToString("N"));
            GameObject owner = null;
            var results = new List<bool>();
            try
            {
                Directory.CreateDirectory(root);
                var settingsPath = Path.Combine(root, "settings.json");
                var manager = new SettingsManager(
                    new RuntimeSettingsStore(settingsPath),
                    new SettingsSerializer(),
                    false);
                manager.Initialize();
                var initialSettingsStamp = Stamp(settingsPath);

                owner = new GameObject("M037VisibilityDiagnostics");
                var settings =
                    owner.AddComponent<SettingsWindowController>();
                settings.Initialize(manager);
                var backend = new FakeVisibilityBackend();
                var visibility = owner.AddComponent<
                    UnityPlayerWindowVisibilityController>();
                visibility.Initialize(settings, true, backend);

                results.Add(Record(
                    "Exact UnityWndClass lookup succeeds",
                    UnityPlayerWindowVisibilityController
                        .TryFindCurrentPlayerWindow(out var playerWindow)
                    && playerWindow != IntPtr.Zero));

                visibility.NotifyRuntimeReady();
                visibility.NotifyRuntimeReady();
                results.Add(Record(
                    "One idempotent startup hide",
                    visibility.RuntimeReady
                    && !visibility.RequestedVisible
                    && visibility.StartupHideRequestCount == 1
                    && visibility.LogicalHideRequestCount == 1
                    && visibility.HideExecutionCount == 1
                    && backend.HideCount == 1
                    && backend.IsRenderingSurfaceVisible()));

                var firstOpen = visibility.TryOpenSettings();
                var secondOpen = visibility.TryOpenSettings();
                results.Add(Record(
                    "Open shows and reactivates existing Settings",
                    firstOpen
                    && secondOpen
                    && settings.IsOpen
                    && visibility.LogicalShowRequestCount == 2
                    && visibility.ShowExecutionCount == 2
                    && backend.ShowCount == 2
                    && visibility.RedundantShowRequestCount >= 1));

                settings.Binding.FirstRunCompleted = true;
                var dirtyBeforeSecondaryActivation =
                    settings.Binding.IsDirty;
                var showCountBeforeSecondaryActivation = backend.ShowCount;
                var secondaryActivation =
                    visibility.TryActivateSettingsFromSecondary();
                results.Add(Record(
                    "Secondary activation preserves open dirty Settings",
                    secondaryActivation
                    && settings.IsOpen
                    && dirtyBeforeSecondaryActivation
                    && settings.Binding.IsDirty
                    && backend.ShowCount
                        == showCountBeforeSecondaryActivation + 1));

                settings.Cancel();
                var cancelHides =
                    !settings.IsOpen
                    && visibility.SettingsCloseHidePending
                    && visibility
                        .CompletePendingSettingsCloseHideForDiagnostics()
                    && !visibility.RequestedVisible
                    && backend.HideCount == 2;
                visibility.TryOpenSettings();
                settings.Close();
                var closeHides =
                    !settings.IsOpen
                    && visibility.SettingsCloseHidePending
                    && visibility
                        .CompletePendingSettingsCloseHideForDiagnostics()
                    && backend.HideCount == 3;
                results.Add(Record(
                    "Cancel and Close hide",
                    cancelHides
                    && closeHides));

                visibility.TryOpenSettings();
                settings.Close();
                var pendingBeforeImmediateReopen =
                    visibility.SettingsCloseHidePending;
                settings.Open();
                var staleHideCancelled =
                    !visibility
                        .CompletePendingSettingsCloseHideForDiagnostics();
                results.Add(Record(
                    "Immediate reopen cancels pending Settings hide",
                    pendingBeforeImmediateReopen
                    && staleHideCancelled
                    && settings.IsOpen
                    && !visibility.SettingsCloseHidePending
                    && backend.HideCount == 3));
                settings.Close();
                visibility
                    .CompletePendingSettingsCloseHideForDiagnostics();

                backend.Minimized = true;
                visibility.TryOpenSettings();
                results.Add(Record(
                    "Minimized Player restores",
                    backend.RestoreCount == 1
                    && !backend.Minimized
                    && settings.IsOpen));

                var hideCountBeforeNonClosingCommands = backend.HideCount;
                settings.Binding.FirstRunCompleted = true;
                var applySucceeded = settings.Apply();
                settings.RestoreDefaults();
                var stampAfterApply = Stamp(settingsPath);
                results.Add(Record(
                    "Apply and Restore Defaults remain visible",
                    applySucceeded
                    && settings.IsOpen
                    && backend.HideCount
                        == hideCountBeforeNonClosingCommands));

                settings.Cancel();
                visibility
                    .CompletePendingSettingsCloseHideForDiagnostics();
                var closeWithoutApplyStamp = Stamp(settingsPath);
                results.Add(Record(
                    "Visibility writes no persistence",
                    stampAfterApply != initialSettingsStamp
                    && closeWithoutApplyStamp == stampAfterApply
                    && !settings.IsOpen));

                var trayDispatcher = new NativeMascotCommandDispatcher(
                    settings,
                    visibility.TryOpenSettings,
                    _ => { });
                var trayOpen = trayDispatcher.Dispatch(
                    NativeMascotCommandDispatcher.OpenSettingsCommand,
                    NativeMascotCommandDispatcher.SystemTraySource);
                settings.Cancel();
                visibility
                    .CompletePendingSettingsCloseHideForDiagnostics();
                var mascotOpen = trayDispatcher.Dispatch(
                    NativeMascotCommandDispatcher.OpenSettingsCommand,
                    NativeMascotCommandDispatcher.MascotContextMenuSource);
                results.Add(Record(
                    "Hidden tray and mascot commands reopen Settings",
                    trayOpen
                    && mascotOpen
                    && settings.IsOpen
                    && trayDispatcher.OpenSettingsDispatchCount == 2));

                settings.Cancel();
                visibility
                    .CompletePendingSettingsCloseHideForDiagnostics();
                visibility.BeginShutdown();
                var rejectedBefore =
                    visibility.RejectedShowRequestCount;
                var showRejected = !visibility.TryOpenSettings();
                results.Add(Record(
                    "Shutdown rejects show",
                    showRejected
                    && visibility.ShutdownStarted
                    && visibility.RejectedShowRequestCount
                        == rejectedBefore + 1
                    && !settings.IsOpen));

                results.Add(Record(
                    "Ordinary hide posts no WM_CLOSE",
                    backend.OrderlyClosePostCount == 0));

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
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
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

        private static string Stamp(string path)
        {
            if (!File.Exists(path))
                return "missing";
            var info = new FileInfo(path);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }

        private static bool Record(string name, bool result)
        {
            Debug.Log($"{Prefix} {name}: {result}");
            return result;
        }

        private sealed class FakeVisibilityBackend :
            IUnityPlayerWindowVisibilityBackend
        {
            internal bool Visible { get; private set; } = true;
            internal bool Minimized { get; set; }
            internal int ShowCount { get; private set; }
            internal int HideCount { get; private set; }
            internal int RestoreCount { get; private set; }
            internal int OrderlyClosePostCount { get; private set; }

            public bool TryFind(out IntPtr window)
            {
                window = new IntPtr(1);
                return true;
            }

            public bool IsVisible() => Visible;

            public bool IsRenderingSurfaceVisible() => true;

            public bool IsMinimized() => Minimized;

            public bool TryShowInteractive()
            {
                ShowCount++;
                if (Minimized)
                {
                    RestoreCount++;
                    Minimized = false;
                }
                Visible = true;
                return true;
            }

            public bool TryHide()
            {
                HideCount++;
                Visible = false;
                return true;
            }
        }
    }
}
