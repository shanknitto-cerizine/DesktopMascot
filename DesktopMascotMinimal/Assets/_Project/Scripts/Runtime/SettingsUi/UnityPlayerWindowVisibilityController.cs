using System.Collections;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal interface IUnityPlayerWindowVisibilityBackend
    {
        bool TryFind(out System.IntPtr window);
        bool IsVisible();
        bool IsRenderingSurfaceVisible();
        bool IsMinimized();
        bool TryShowInteractive();
        bool TryHide();
    }

    internal sealed class UnityPlayerWindowVisibilityBackend :
        IUnityPlayerWindowVisibilityBackend
    {
        public bool TryFind(out System.IntPtr window) =>
            UnityPlayerWindowVisibilityController
                .TryFindCurrentPlayerWindow(out window);

        public bool IsVisible() =>
            UnityPlayerWindowVisibilityController
                .IsCurrentPlayerWindowVisible();

        public bool IsRenderingSurfaceVisible() =>
            UnityPlayerWindowVisibilityController
                .IsCurrentPlayerRenderingSurfaceVisible();

        public bool IsMinimized() =>
            UnityPlayerWindowVisibilityController
                .IsCurrentPlayerWindowMinimized();

        public bool TryShowInteractive() =>
            UnityPlayerWindowVisibilityController.TryMakeInteractive();

        public bool TryHide() =>
            UnityPlayerWindowVisibilityController.TryHide();
    }

    internal sealed partial class UnityPlayerWindowVisibilityController :
        MonoBehaviour
    {
        private const string Prefix =
            "[DesktopMascotPlayerVisibility]";

        private SettingsWindowController settingsWindow;
        private IUnityPlayerWindowVisibilityBackend backend;
        private bool productionHiddenByDefault;
        private bool initialized;
        private bool runtimeReady;
        private bool shutdownStarted;
        private bool requestedVisible = true;
        private int settingsVisibilityGeneration;
        private bool settingsCloseHidePending;

        internal bool Initialized => initialized;
        internal bool RuntimeReady => runtimeReady;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool RequestedVisible => requestedVisible;
        internal int StartupHideRequestCount { get; private set; }
        internal int LogicalShowRequestCount { get; private set; }
        internal int LogicalHideRequestCount { get; private set; }
        internal int RedundantShowRequestCount { get; private set; }
        internal int RedundantHideRequestCount { get; private set; }
        internal int RejectedShowRequestCount { get; private set; }
        internal int ShowExecutionCount { get; private set; }
        internal int HideExecutionCount { get; private set; }
        internal bool LastShowSucceeded { get; private set; }
        internal bool LastHideSucceeded { get; private set; }
        internal bool SettingsCloseHidePending =>
            settingsCloseHidePending;

        internal void Initialize(
            SettingsWindowController window,
            bool hiddenByDefault,
            IUnityPlayerWindowVisibilityBackend visibilityBackend = null)
        {
            settingsWindow = window;
            var usesWindowsBackend = visibilityBackend == null;
            backend = visibilityBackend
                ?? new UnityPlayerWindowVisibilityBackend();
            productionHiddenByDefault = hiddenByDefault;
            requestedVisible = true;
            initialized = settingsWindow != null && backend != null;
            if (initialized)
            {
                settingsWindow.OpenStateChanged += OnSettingsOpenStateChanged;
            }
            if (initialized && productionHiddenByDefault)
                ApplyStartupHiddenState(usesWindowsBackend);
        }

        private void ApplyStartupHiddenState(bool usesWindowsBackend)
        {
            StartupHideRequestCount++;
            LogicalHideRequestCount++;
            HideExecutionCount++;
            LastHideSucceeded = usesWindowsBackend
                && EnsureStartupCloak();
            if (!LastHideSucceeded)
                LastHideSucceeded = backend.TryHide();
            requestedVisible = !LastHideSucceeded;
            Debug.Log(
                $"{Prefix} Startup hide result: {LastHideSucceeded}");
            var cachedLookup =
                backend.TryFind(out var playerWindow)
                && playerWindow != System.IntPtr.Zero;
            Debug.Log(
                $"{Prefix} Cached exact UnityWndClass after hide: " +
                $"{cachedLookup}; HWND=0x{playerWindow.ToInt64():X}");
            Debug.Log(
                $"{Prefix} Rendering surface retained after hide: " +
                backend.IsRenderingSurfaceVisible());
            if (usesWindowsBackend)
            {
                Debug.Log(
                    $"{Prefix} Player Flash Measured: " +
                    $"{StartupFlashMeasuredMilliseconds} ms");
                Debug.Log(
                    $"{Prefix} Startup cloak attempted/succeeded/lookups: " +
                    $"{StartupCloakAttempted}/{StartupCloakSucceeded}/" +
                    StartupWindowLookupAttempts);
                Debug.Log(
                    "[DesktopMascotPlayerVisibilityDiagnostics] " +
                    $"Player Flash Measured: " +
                    $"{StartupFlashMeasuredMilliseconds} ms");
            }
        }

        internal void NotifyRuntimeReady()
        {
            if (!initialized || runtimeReady)
                return;
            runtimeReady = true;
        }

        internal bool TryOpenSettings()
        {
            if (!initialized || shutdownStarted)
            {
                RejectedShowRequestCount++;
                return false;
            }
            if (settingsWindow.IsOpen)
                return ReactivateOpenSettings();
            var shown = RequestVisible("TryOpenSettings");
            settingsWindow.Open();
            return shown && settingsWindow.IsOpen;
        }

        private bool ReactivateOpenSettings()
        {
            requestedVisible = true;
            LogicalShowRequestCount++;
            ShowExecutionCount++;
            LastShowSucceeded = backend.TryShowInteractive();
            Debug.Log(
                $"{Prefix} Existing Settings activation result: " +
                LastShowSucceeded);
            return LastShowSucceeded && settingsWindow.IsOpen;
        }

        internal bool TryGetFileDialogOwner(out System.IntPtr ownerWindow)
        {
            ownerWindow = System.IntPtr.Zero;
            if (!initialized || shutdownStarted)
                return false;
            if (!RequestVisible("FileDialogOwner"))
                return false;
            return backend.TryFind(out ownerWindow)
                && ownerWindow != System.IntPtr.Zero;
        }

        internal bool TryActivateSettingsFromSecondary()
        {
            if (!initialized || shutdownStarted)
            {
                RejectedShowRequestCount++;
                return false;
            }
            requestedVisible = true;
            LogicalShowRequestCount++;
            ShowExecutionCount++;
            LastShowSucceeded = backend.TryShowInteractive();
            Debug.Log(
                $"{Prefix} Secondary activation show result: " +
                LastShowSucceeded);
            if (!settingsWindow.IsOpen)
                settingsWindow.Open();
            return LastShowSucceeded && settingsWindow.IsOpen;
        }

        internal bool RequestVisible(
            string source = "DirectRequestVisible")
        {
            if (!initialized || shutdownStarted)
            {
                RejectedShowRequestCount++;
                return false;
            }
            if (requestedVisible && backend.IsVisible())
            {
                RedundantShowRequestCount++;
                LastShowSucceeded = true;
                return true;
            }
            requestedVisible = true;
            LogicalShowRequestCount++;
            ShowExecutionCount++;
            LastShowSucceeded = backend.TryShowInteractive();
            Debug.Log($"{Prefix} Show result: {LastShowSucceeded}");
            return LastShowSucceeded;
        }

        internal bool RequestHidden(
            string source = "DirectRequestHidden")
        {
            if (!initialized || shutdownStarted)
                return false;
            if (!requestedVisible && !backend.IsVisible())
            {
                RedundantHideRequestCount++;
                LastHideSucceeded = true;
                return true;
            }
            requestedVisible = false;
            LogicalHideRequestCount++;
            HideExecutionCount++;
            LastHideSucceeded = backend.TryHide();
            Debug.Log($"{Prefix} Hide result: {LastHideSucceeded}");
            return LastHideSucceeded;
        }

        internal bool TryHandleOrdinaryPlayerClose()
        {
            if (!initialized
                || !productionHiddenByDefault
                || shutdownStarted
                || !backend.IsVisible())
            {
                return false;
            }
            if (settingsWindow.IsOpen)
                settingsWindow.Close();
            else
                RequestHidden("OrdinaryPlayerClose");
            return true;
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
            settingsVisibilityGeneration++;
            settingsCloseHidePending = false;
            Debug.Log($"{Prefix} Shutdown visibility barrier closed.");
        }

        private void OnSettingsOpenStateChanged(bool open)
        {
            settingsVisibilityGeneration++;
            if (open)
            {
                settingsCloseHidePending = false;
                RequestVisible("SettingsOpenStateChanged");
            }
            else
            {
                settingsCloseHidePending = true;
                var generation = settingsVisibilityGeneration;
                Debug.Log(
                    $"{Prefix} Settings close hide scheduled: True");
                StartCoroutine(
                    HideAfterSettingsFreeFrame(generation));
            }
        }

        private IEnumerator HideAfterSettingsFreeFrame(int generation)
        {
            // Let the current GUI pass finish and present one frame without
            // Settings before the validated application-cloak path runs.
            yield return new WaitForEndOfFrame();
            TryCompletePendingSettingsCloseHide(generation);
        }

        internal bool TryCompletePendingSettingsCloseHide(
            int generation)
        {
            if (!settingsCloseHidePending
                || generation != settingsVisibilityGeneration
                || !initialized
                || shutdownStarted
                || settingsWindow == null
                || settingsWindow.IsOpen)
            {
                return false;
            }
            settingsCloseHidePending = false;
            var hidden = RequestHidden(
                "SettingsCloseAfterFreshFrame");
            Debug.Log(
                $"{Prefix} Settings-free frame hide result: {hidden}");
            return hidden;
        }

        internal bool CompletePendingSettingsCloseHideForDiagnostics()
        {
            return TryCompletePendingSettingsCloseHide(
                settingsVisibilityGeneration);
        }

        private void OnDestroy()
        {
            settingsVisibilityGeneration++;
            settingsCloseHidePending = false;
            if (settingsWindow != null)
            {
                settingsWindow.OpenStateChanged -=
                    OnSettingsOpenStateChanged;
            }
        }

    }
}
