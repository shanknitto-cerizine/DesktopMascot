using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class NativeSystemTrayController : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotSystemTray]";

        private bool running;
        private bool shutdownStarted;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const double StartupProbeIntervalSeconds = 0.2;
        private const double StartupRecoveryTimeoutSeconds = 20.0;

        private bool startAttempted;
        private bool startupPending;
        private double nextStartupProbeTime;
        private double startupRecoveryDeadline;
#endif

        internal bool Running => running;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool CleanupSucceeded { get; private set; } = true;
        internal bool IconRemoved { get; private set; } = true;
        internal bool OwnerDestroyed { get; private set; } = true;
        internal bool PopupClosed { get; private set; } = true;
        internal int LiveOwnedMenuCount { get; private set; }
        internal int LiveOwnedIconCount { get; private set; }
        internal uint DeleteRequestCount { get; private set; }
        internal uint OwnerCreatedCount { get; private set; }
        internal uint OwnerDestroyedCount { get; private set; }

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
        private static extern int DMN_IsNativeTrayPopupActive();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeTrayMenuLiveOwnedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeTrayIconLiveOwnedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayDeleteRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayOwnerCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayOwnerDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTraySetVersionRequestCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayTaskbarCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayNimAddAttemptCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayNimAddSuccessCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayNimAddLastError();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetNativeTrayNimSetVersionSuccessCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayNimSetVersionLastError();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeTrayRegistrationRetryCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetNativeTrayShutdownRetrySuppressedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_GetNativeTrayRegistrationFinalResult();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_WasNativeTrayRegistrationRetryExhausted();
#endif

        internal void Configure()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            startAttempted = false;
            startupPending = false;
#endif
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            startupPending = false;
            LogRegistrationDiagnostics("before cleanup");
            var stopResult = DMN_StopNativeTrayIcon();
            running = DMN_IsNativeTrayIconRunning() != 0;
            IconRemoved = DMN_IsNativeTrayIconRegistered() == 0;
            OwnerDestroyed =
                DMN_IsNativeTrayOwnerWindowAvailable() == 0;
            PopupClosed = DMN_IsNativeTrayPopupActive() == 0;
            LiveOwnedMenuCount = DMN_GetNativeTrayMenuLiveOwnedCount();
            LiveOwnedIconCount = DMN_GetNativeTrayIconLiveOwnedCount();
            DeleteRequestCount = DMN_GetNativeTrayDeleteRequestCount();
            OwnerCreatedCount = DMN_GetNativeTrayOwnerCreatedCount();
            OwnerDestroyedCount = DMN_GetNativeTrayOwnerDestroyedCount();
            CleanupSucceeded =
                stopResult == 1
                && !running
                && IconRemoved
                && OwnerDestroyed
                && PopupClosed
                && LiveOwnedMenuCount == 0
                && LiveOwnedIconCount == 0
                && DeleteRequestCount == 1
                && OwnerCreatedCount == 1
                && OwnerDestroyedCount == 1;
            LogRegistrationDiagnostics("after cleanup");
            Debug.Log($"{Prefix} Cleanup result: {CleanupSucceeded}");
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (shutdownStarted)
                return;
            if (!startAttempted)
            {
                startAttempted = true;
                var threadStarted = DMN_StartNativeTrayIcon() == 1;
                CleanupSucceeded = false;
                IconRemoved = false;
                OwnerDestroyed = false;
                if (!threadStarted)
                {
                    CompleteStartup(false);
                    return;
                }
                if (DMN_IsNativeTrayIconRegistered() != 0)
                {
                    CompleteStartup(true);
                    return;
                }
                startupPending = true;
                var now = Time.realtimeSinceStartupAsDouble;
                nextStartupProbeTime =
                    now + StartupProbeIntervalSeconds;
                startupRecoveryDeadline =
                    now + StartupRecoveryTimeoutSeconds;
                Debug.Log($"{Prefix} Startup recovery pending: True");
                return;
            }
            if (!startupPending)
                return;
            var currentTime = Time.realtimeSinceStartupAsDouble;
            if (currentTime < nextStartupProbeTime)
                return;
            nextStartupProbeTime =
                currentTime + StartupProbeIntervalSeconds;
            if (DMN_IsNativeTrayIconRegistered() != 0)
            {
                CompleteStartup(true);
            }
            else if (currentTime >= startupRecoveryDeadline)
            {
                CompleteStartup(false);
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void CompleteStartup(bool succeeded)
        {
            startupPending = false;
            running = succeeded;
            Debug.Log($"{Prefix} Startup result: {running}");
            LogRegistrationDiagnostics("startup");
        }

        private static void LogRegistrationDiagnostics(string phase)
        {
            Debug.Log(
                $"{Prefix} Registration diagnostics phase: {phase}");
            Debug.Log(
                $"{Prefix} NIM_ADD attempted/succeeded: " +
                $"{DMN_GetNativeTrayNimAddAttemptCount()}/" +
                $"{DMN_GetNativeTrayNimAddSuccessCount()}");
            Debug.Log(
                $"{Prefix} NIM_ADD GetLastError: " +
                DMN_GetNativeTrayNimAddLastError());
            Debug.Log(
                $"{Prefix} NIM_SETVERSION attempted/succeeded: " +
                $"{DMN_GetNativeTraySetVersionRequestCount()}/" +
                $"{DMN_GetNativeTrayNimSetVersionSuccessCount()}");
            Debug.Log(
                $"{Prefix} NIM_SETVERSION GetLastError: " +
                DMN_GetNativeTrayNimSetVersionLastError());
            Debug.Log(
                $"{Prefix} TaskbarCreated received count: " +
                DMN_GetNativeTrayTaskbarCreatedCount());
            Debug.Log(
                $"{Prefix} Registration retry count: " +
                DMN_GetNativeTrayRegistrationRetryCount());
            Debug.Log(
                $"{Prefix} Registration final result: " +
                (DMN_GetNativeTrayRegistrationFinalResult() != 0));
            Debug.Log(
                $"{Prefix} Retry exhaustion: " +
                (DMN_WasNativeTrayRegistrationRetryExhausted() != 0));
            Debug.Log(
                $"{Prefix} Shutdown retry suppressed count: " +
                DMN_GetNativeTrayShutdownRetrySuppressedCount());
            Debug.Log(
                $"{Prefix} Icon currently registered: " +
                (DMN_IsNativeTrayIconRegistered() != 0));
        }
#endif

        private void OnDisable()
        {
            BeginShutdown();
        }
    }
}
