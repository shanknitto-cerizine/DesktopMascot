using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class NativeMascotContextMenuController : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotContextMenu]";

        private NativeMascotCommandDispatcher dispatcher;
        private bool configured;
        private bool nativeEnabled;
        private bool shutdownStarted;
        private bool diagnosticOwnsPolling;
        private ulong lastConsumedGeneration;

        internal bool NativeEnabled => nativeEnabled;
        internal bool ShutdownStarted => shutdownStarted;
        internal ulong LastConsumedGeneration => lastConsumedGeneration;
        internal int LastConsumedSource { get; private set; }
        internal NativeMascotCommandDispatcher Dispatcher => dispatcher;

        internal void SetDiagnosticPollingOwnership(bool value)
        {
            diagnosticOwnsPolling = value;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_EnableNativeMascotContextMenu();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DisableNativeMascotContextMenu();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_TryConsumeNativeMascotCommand(
            out ulong generation,
            out int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeApplicationCommandLastSource();
#endif

        internal void Configure(
            SettingsWindowController settingsWindow,
            DesktopMascotRuntimePipeline runtime,
            UnityPlayerWindowVisibilityController visibility = null)
        {
            dispatcher = new NativeMascotCommandDispatcher(
                settingsWindow,
                visibility != null
                    ? (Func<bool>)visibility.TryOpenSettings
                    : null,
                runtime != null ? runtime.RequestOrderlyQuit : null);
            configured = settingsWindow != null
                && runtime != null
                && visibility != null;
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (nativeEnabled)
                DMN_DisableNativeMascotContextMenu();
#endif
            nativeEnabled = false;
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!configured || shutdownStarted)
                return;
            if (!nativeEnabled)
            {
                nativeEnabled = DMN_EnableNativeMascotContextMenu() == 1;
                if (nativeEnabled)
                    Debug.Log($"{Prefix} Native context menu enabled.");
                return;
            }
            if (diagnosticOwnsPolling)
                return;

            if (DMN_TryConsumeNativeMascotCommand(
                    out var generation,
                    out var command) != 1
                || generation <= lastConsumedGeneration)
            {
                return;
            }
            lastConsumedGeneration = generation;
            LastConsumedSource = DMN_GetNativeApplicationCommandLastSource();
            Debug.Log(
                $"{Prefix} Consumed command {command}, generation " +
                $"{generation}, source {LastConsumedSource}.");
            if (!dispatcher.Dispatch(command, LastConsumedSource))
            {
                Debug.LogWarning(
                    $"{Prefix} Ignored native command: {command}.");
            }
#endif
        }

        private void OnDisable()
        {
            BeginShutdown();
        }
    }
}
