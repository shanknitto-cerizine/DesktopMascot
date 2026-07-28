using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime;
using DesktopMascot.Runtime.Settings;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotSystemTrayDiagnostics : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotSystemTrayDiagnostics]";

        private NativeSystemTrayController trayController;
        private NativeMascotContextMenuController commandController;
        private SettingsWindowController settingsWindow;
        private WindowPositionPersistence positionPersistence;
        private SettingsManager settingsManager;

        internal static bool Passed { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_StartNativeTrayIcon();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_StopNativeTrayIcon();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeTrayIconRunning();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeTrayOwnerWindowAvailable();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeTrayIconRegistered();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasNativeTrayTooltipConfigured();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_RunNativeTrayFocusedDiagnostic();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_PublishNativeTrayCommandForDiagnostics(int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetNativeMascotCommandGeneration();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_TryConsumeNativeMascotCommand(
            out ulong generation,
            out int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeApplicationCommandLastSource();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayInitialAddRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTraySetVersionRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayDeleteRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayReregisterRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayOwnerCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayOwnerDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayMenuCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayMenuDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeTrayMenuLiveOwnedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayIconCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayIconDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeTrayIconLiveOwnedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayTaskbarCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetNativeMascotCompletedDragGeneration();
#endif

        internal void Configure(
            NativeSystemTrayController tray,
            NativeMascotContextMenuController commands,
            SettingsWindowController window,
            WindowPositionPersistence persistence,
            SettingsManager manager)
        {
            trayController = tray;
            commandController = commands;
            settingsWindow = window;
            positionPersistence = persistence;
            settingsManager = manager;
            Passed = false;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            for (var frame = 0;
                 frame < 900
                 && (!DesktopMascotNativeContextMenuDiagnostics.Passed
                     || trayController == null
                     || !trayController.Running);
                 ++frame)
            {
                yield return null;
            }
            if (!DesktopMascotNativeContextMenuDiagnostics.Passed
                || trayController == null
                || !trayController.Running
                || commandController == null)
            {
                LogFinal(false, false, false, false, false, false);
                yield break;
            }

            commandController.SetDiagnosticPollingOwnership(true);
            var initialGeneration =
                DMN_GetNativeMascotCommandGeneration();
            var initialDragGeneration =
                DMN_GetNativeMascotCompletedDragGeneration();
            var initialPositionWrites =
                positionPersistence?.WriteCount ?? -1;
            var settingsStamp = ReadStamp(settingsManager?.StoragePath);

            var nativeLifecycle =
                DMN_RunNativeTrayFocusedDiagnostic() == 1
                && DMN_IsNativeTrayIconRunning() == 1
                && DMN_IsNativeTrayOwnerWindowAvailable() == 1
                && DMN_IsNativeTrayIconRegistered() == 1
                && DMN_WasNativeTrayTooltipConfigured() == 1
                && DMN_GetNativeTrayInitialAddRequestCount() == 1
                && DMN_GetNativeTraySetVersionRequestCount() == 3
                && DMN_GetNativeTrayReregisterRequestCount() == 2
                && DMN_GetNativeTrayTaskbarCreatedCount() == 2;

            var cancelled =
                DMN_PublishNativeTrayCommandForDiagnostics(0) == 0
                && DMN_GetNativeMascotCommandGeneration()
                    == initialGeneration
                && DMN_TryConsumeNativeMascotCommand(out _, out _) == 0;

            var openPublished =
                DMN_PublishNativeTrayCommandForDiagnostics(
                    NativeMascotCommandDispatcher.OpenSettingsCommand) == 1;
            var openConsumed =
                DMN_TryConsumeNativeMascotCommand(
                    out var openGeneration,
                    out var openCommand) == 1
                && openGeneration == initialGeneration + 1
                && openCommand
                    == NativeMascotCommandDispatcher.OpenSettingsCommand
                && DMN_GetNativeApplicationCommandLastSource()
                    == NativeMascotCommandDispatcher.SystemTraySource;
            var controllerCountBefore =
                FindObjectsByType<SettingsWindowController>(
                    FindObjectsSortMode.None).Length;
            var dispatched =
                openConsumed
                && commandController.Dispatcher.Dispatch(
                    openCommand,
                    NativeMascotCommandDispatcher.SystemTraySource);
            var dispatchedAgain =
                commandController.Dispatcher.Dispatch(
                    NativeMascotCommandDispatcher.OpenSettingsCommand,
                    NativeMascotCommandDispatcher.SystemTraySource);
            var controllerCountAfter =
                FindObjectsByType<SettingsWindowController>(
                    FindObjectsSortMode.None).Length;
            var settingsReuse =
                openPublished
                && dispatched
                && dispatchedAgain
                && settingsWindow.IsOpen
                && controllerCountBefore == 1
                && controllerCountAfter == 1;
            settingsWindow.Close();
            var repeatedPollEmpty =
                DMN_TryConsumeNativeMascotCommand(out _, out _) == 0;

            var firstStop = DMN_StopNativeTrayIcon() == 1;
            var secondStop = DMN_StopNativeTrayIcon() == 1;
            var cleanup =
                firstStop
                && secondStop
                && DMN_IsNativeTrayIconRunning() == 0
                && DMN_IsNativeTrayOwnerWindowAvailable() == 0
                && DMN_IsNativeTrayIconRegistered() == 0
                && DMN_GetNativeTrayDeleteRequestCount() == 1
                && DMN_GetNativeTrayOwnerCreatedCount() == 1
                && DMN_GetNativeTrayOwnerDestroyedCount() == 1
                && DMN_GetNativeTrayMenuCreatedCount()
                    == DMN_GetNativeTrayMenuDestroyedCount()
                && DMN_GetNativeTrayMenuLiveOwnedCount() == 0
                && DMN_GetNativeTrayIconCreatedCount() == 1
                && DMN_GetNativeTrayIconDestroyedCount() == 1
                && DMN_GetNativeTrayIconLiveOwnedCount() == 0;
            var shutdownRejects =
                DMN_PublishNativeTrayCommandForDiagnostics(
                    NativeMascotCommandDispatcher.RequestExitCommand) == 0;
            var restarted = DMN_StartNativeTrayIcon() == 1;

            var persistenceUnchanged =
                cancelled
                && DMN_GetNativeMascotCompletedDragGeneration()
                    == initialDragGeneration
                && (positionPersistence?.WriteCount ?? -2)
                    == initialPositionWrites
                && ReadStamp(settingsManager?.StoragePath) == settingsStamp;
            var sharedBoundary =
                openConsumed
                && repeatedPollEmpty
                && commandController.Dispatcher.HasOrderlyQuitTarget;

            Passed =
                nativeLifecycle
                && cancelled
                && settingsReuse
                && sharedBoundary
                && cleanup
                && shutdownRejects
                && restarted
                && persistenceUnchanged;
            commandController.SetDiagnosticPollingOwnership(false);
            LogFinal(
                nativeLifecycle,
                cancelled,
                settingsReuse,
                sharedBoundary,
                cleanup,
                persistenceUnchanged);
        }

        internal static bool PublishExitForOrderlyShutdown()
        {
            return DMN_PublishNativeTrayCommandForDiagnostics(
                NativeMascotCommandDispatcher.RequestExitCommand) == 1;
        }

        private static string ReadStamp(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "missing";
            var info = new FileInfo(path);
            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }

        private static void LogFinal(
            bool nativeLifecycle,
            bool cancelled,
            bool settingsReuse,
            bool sharedBoundary,
            bool cleanup,
            bool persistenceUnchanged)
        {
            Log($"Owner/icon/version/tooltip lifecycle: {nativeLifecycle}");
            Log($"TaskbarCreated controlled recovery: {nativeLifecycle}");
            Log($"Cancelled menu publishes no command: {cancelled}");
            Log($"Settings controller reused: {settingsReuse}");
            Log($"Shared command boundary/one consumption: {sharedBoundary}");
            Log($"Idempotent cleanup and ownership: {cleanup}");
            Log($"Persistence unchanged: {persistenceUnchanged}");
            Log($"Automated diagnostics passed: {Passed}");
            Log("Visual verification pending: True");
        }

        private static void Log(string message) =>
            Debug.Log($"{Prefix} {message}");
#endif
    }
}
