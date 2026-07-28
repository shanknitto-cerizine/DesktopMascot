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
        private bool startAttempted;
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
#endif

        internal void Configure()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            startAttempted = false;
#endif
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
            Debug.Log($"{Prefix} Cleanup result: {CleanupSucceeded}");
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (startAttempted || shutdownStarted)
                return;
            startAttempted = true;
            running = DMN_StartNativeTrayIcon() == 1;
            CleanupSucceeded = false;
            IconRemoved = false;
            OwnerDestroyed = false;
            Debug.Log($"{Prefix} Startup result: {running}");
#endif
        }

        private void OnDisable()
        {
            BeginShutdown();
        }
    }
}
