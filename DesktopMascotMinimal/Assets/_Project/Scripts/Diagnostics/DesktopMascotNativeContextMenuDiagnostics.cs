using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime;
using DesktopMascot.Runtime.Settings;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotNativeContextMenuDiagnostics :
        MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix =
            "[DesktopMascotContextMenuDiagnostics]";

        private NativeMascotContextMenuController controller;
        private SettingsWindowController settingsWindow;
        private WindowPositionPersistence positionPersistence;
        private SettingsManager settingsManager;

        internal static bool Passed { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_EnableNativeMascotContextMenu();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DisableNativeMascotContextMenu();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong DMN_GetNativeMascotCommandGeneration();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_TryConsumeNativeMascotCommand(
            out ulong generation,
            out int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_PublishNativeMascotCommandForDiagnostics(int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_RunNativeMascotMenuResourceDiagnostic();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotCommandPublishCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotCommandConsumeCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotMenuCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotMenuDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotMenuLiveOwnedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetNativeMascotCompletedDragGeneration();
#endif

        internal void Configure(
            NativeMascotContextMenuController contextMenu,
            SettingsWindowController window,
            WindowPositionPersistence persistence,
            SettingsManager manager)
        {
            controller = contextMenu;
            settingsWindow = window;
            positionPersistence = persistence;
            settingsManager = manager;
            Passed = false;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            for (var frame = 0;
                 frame < 900 && (controller == null
                     || !controller.NativeEnabled);
                 ++frame)
            {
                yield return null;
            }
            if (controller == null || !controller.NativeEnabled)
            {
                LogFinal(false, false, false, false, false, false, false);
                yield break;
            }

            controller.SetDiagnosticPollingOwnership(true);
            var initialGeneration =
                DMN_GetNativeMascotCommandGeneration();
            var initialDragGeneration =
                DMN_GetNativeMascotCompletedDragGeneration();
            var initialPositionWrites =
                positionPersistence?.WriteCount ?? -1;
            var settingsStamp = ReadStamp(settingsManager?.StoragePath);

            var startsEmpty =
                DMN_TryConsumeNativeMascotCommand(
                    out _,
                    out _) == 0;
            var resourceOwnership =
                DMN_RunNativeMascotMenuResourceDiagnostic() == 1
                && DMN_GetNativeMascotMenuLiveOwnedCount() == 0
                && DMN_GetNativeMascotMenuCreatedCount()
                    == DMN_GetNativeMascotMenuDestroyedCount();

            var cancelPublished =
                DMN_PublishNativeMascotCommandForDiagnostics(0);
            var cancelNoCommand =
                cancelPublished == 0
                && DMN_GetNativeMascotCommandGeneration()
                    == initialGeneration
                && DMN_TryConsumeNativeMascotCommand(
                    out _,
                    out _) == 0;

            var publishBefore =
                DMN_GetNativeMascotCommandPublishCount();
            var consumeBefore =
                DMN_GetNativeMascotCommandConsumeCount();
            var openPublished =
                DMN_PublishNativeMascotCommandForDiagnostics(
                    NativeMascotCommandDispatcher.OpenSettingsCommand) == 1;
            var openConsumed =
                DMN_TryConsumeNativeMascotCommand(
                    out var openGeneration,
                    out var openCommand) == 1
                && openGeneration == initialGeneration + 1
                && openCommand
                    == NativeMascotCommandDispatcher.OpenSettingsCommand;
            var openDispatched =
                openConsumed && controller.Dispatcher.Dispatch(openCommand);
            var repeatedPollEmpty =
                DMN_TryConsumeNativeMascotCommand(
                    out _,
                    out _) == 0;
            var controllerCountBefore =
                FindObjectsByType<SettingsWindowController>(
                    FindObjectsSortMode.None).Length;
            var existingController = settingsWindow;
            var alreadyOpenDispatch =
                controller.Dispatcher.Dispatch(
                    NativeMascotCommandDispatcher.OpenSettingsCommand);
            var controllerCountAfter =
                FindObjectsByType<SettingsWindowController>(
                    FindObjectsSortMode.None).Length;
            var settingsReuse =
                openDispatched
                && alreadyOpenDispatch
                && settingsWindow.IsOpen
                && ReferenceEquals(existingController, settingsWindow)
                && controllerCountBefore == 1
                && controllerCountAfter == 1;
            settingsWindow.Close();

            DMN_DisableNativeMascotContextMenu();
            var shutdownRejects =
                DMN_PublishNativeMascotCommandForDiagnostics(
                    NativeMascotCommandDispatcher.RequestExitCommand) == 0;
            var reenabled = DMN_EnableNativeMascotContextMenu() == 1;

            var commandCounts =
                openPublished
                && openConsumed
                && repeatedPollEmpty
                && DMN_GetNativeMascotCommandPublishCount()
                    == publishBefore + 1
                && DMN_GetNativeMascotCommandConsumeCount()
                    == consumeBefore + 1;
            var noDragOrWrites =
                DMN_GetNativeMascotCompletedDragGeneration()
                    == initialDragGeneration
                && (positionPersistence?.WriteCount ?? -2)
                    == initialPositionWrites
                && ReadStamp(settingsManager?.StoragePath) == settingsStamp;
            var exitRoutingAvailable =
                controller.Dispatcher.HasOrderlyQuitTarget;

            Passed =
                startsEmpty
                && resourceOwnership
                && cancelNoCommand
                && commandCounts
                && settingsReuse
                && shutdownRejects
                && reenabled
                && noDragOrWrites
                && exitRoutingAvailable;
            controller.SetDiagnosticPollingOwnership(false);
            LogFinal(
                startsEmpty,
                commandCounts,
                cancelNoCommand,
                shutdownRejects,
                noDragOrWrites,
                settingsReuse,
                resourceOwnership);
        }

        internal static bool PublishExitForOrderlyShutdown()
        {
            return DMN_PublishNativeMascotCommandForDiagnostics(
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
            bool startsEmpty,
            bool commandCounts,
            bool cancelNoCommand,
            bool shutdownRejects,
            bool noDragOrWrites,
            bool settingsReuse,
            bool resourceOwnership)
        {
            Log($"Starts without pending command: {startsEmpty}");
            Log($"One selection/one consumption: {commandCounts}");
            Log($"Repeated polling duplicates: {!commandCounts}");
            Log($"Cancelled selection published no command: {cancelNoCommand}");
            Log($"Shutdown rejects commands: {shutdownRejects}");
            Log($"No drag generation or persistence writes: {noDragOrWrites}");
            Log($"Existing Settings controller reused: {settingsReuse}");
            Log($"Orderly-exit route available: " +
                (FindFirstObjectByType<NativeMascotContextMenuController>()
                    ?.Dispatcher.HasOrderlyQuitTarget ?? false));
            Log($"HMENU ownership balanced: {resourceOwnership}");
            Log($"HMENU created/destroyed/live: " +
                $"{DMN_GetNativeMascotMenuCreatedCount()}/" +
                $"{DMN_GetNativeMascotMenuDestroyedCount()}/" +
                $"{DMN_GetNativeMascotMenuLiveOwnedCount()}");
            Log($"Automated diagnostics passed: {Passed}");
            Log("Visual verification pending: True");
        }

        private static void Log(string message) =>
            Debug.Log($"{Prefix} {message}");
#endif
    }
}
