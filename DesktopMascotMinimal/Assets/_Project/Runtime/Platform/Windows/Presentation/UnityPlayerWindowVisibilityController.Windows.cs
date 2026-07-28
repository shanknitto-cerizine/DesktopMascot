using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using DesktopMascot.Runtime;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed partial class UnityPlayerWindowVisibilityController
    {
        private const int StartupWindowLookupLimit = 250;
        private const int ShowWindowNormally = 5;
        private const int RestoreWindow = 9;
        private const uint CloseWindowMessage = 0x0010;
        private const uint NoSizeNoZOrderNoActivate = 0x0015;
        private const uint DwmWindowAttributeCloak = 13;
        private const uint DwmWindowAttributeCloaked = 14;
        private const uint DwmCloakedByApplication = 0x00000001;
        private const string UnityWindowClass = "UnityWndClass";
        private static IntPtr cachedPlayerWindow;
        private static bool startupCloakAttempted;
        private static bool startupCloakSucceeded;
        private static long startupFlashMeasuredMilliseconds = -1;
        private static int startupWindowLookupAttempts;

        internal static bool StartupCloakAttempted =>
            startupCloakAttempted;
        internal static bool StartupCloakSucceeded =>
            startupCloakSucceeded;
        internal static long StartupFlashMeasuredMilliseconds =>
            startupFlashMeasuredMilliseconds;
        internal static int StartupWindowLookupAttempts =>
            startupWindowLookupAttempts;

        internal static void ApplyStartupCloakForNormalRuntime()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!DesktopMascotRuntimeBootstrap
                    .IsNormalRuntimeRequested())
            {
                return;
            }
            TryApplyStartupCloak(StartupWindowLookupLimit);
#endif
        }

        private static bool EnsureStartupCloak()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return startupCloakSucceeded
                || TryApplyStartupCloak(1);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static bool TryApplyStartupCloak(int lookupLimit)
        {
            startupCloakAttempted = true;
            for (var attempt = 0; attempt < lookupLimit; ++attempt)
            {
                ++startupWindowLookupAttempts;
                var playerWindow = FindCurrentPlayerWindow();
                if (playerWindow != IntPtr.Zero)
                {
                    startupCloakSucceeded =
                        TrySetApplicationCloak(playerWindow, true)
                        && IsApplicationCloaked(playerWindow)
                        && IsWindowVisible(playerWindow);
                    if (startupCloakSucceeded)
                        break;
                }
                if (attempt + 1 < lookupLimit)
                    Thread.Sleep(1);
            }
            RecordStartupFlashMeasurement();
            return startupCloakSucceeded;
        }

        private static void RecordStartupFlashMeasurement()
        {
            var processStartUtc =
                Process.GetCurrentProcess().StartTime.ToUniversalTime();
            startupFlashMeasuredMilliseconds = Math.Max(
                0,
                (long)(DateTime.UtcNow - processStartUtc)
                    .TotalMilliseconds);
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private delegate bool EnumWindowsCallback(
            IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(
            IntPtr window,
            StringBuilder className,
            int maximumCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr window,
            out NativeRect rectangle);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(
            IntPtr window,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr window,
            uint attribute,
            ref int value,
            uint valueSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr window,
            uint attribute,
            out uint value,
            uint valueSize);

#endif

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }

        internal static bool TryFindCurrentPlayerWindow(out IntPtr window)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            window = FindCurrentPlayerWindow();
            return window != IntPtr.Zero;
#else
            window = IntPtr.Zero;
            return false;
#endif
        }

        internal static bool IsCurrentPlayerWindowVisible()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            return playerWindow != IntPtr.Zero
                && IsWindowVisible(playerWindow)
                && !IsApplicationCloaked(playerWindow);
#else
            return false;
#endif
        }

        internal static bool IsCurrentPlayerRenderingSurfaceVisible()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            return playerWindow != IntPtr.Zero
                && IsWindowVisible(playerWindow);
#else
            return false;
#endif
        }

        internal static bool IsCurrentPlayerWindowMinimized()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            return playerWindow != IntPtr.Zero && IsIconic(playerWindow);
#else
            return false;
#endif
        }

        internal static bool TryMakeInteractive()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            if (playerWindow == IntPtr.Zero)
                return false;
            EnsureUsablePosition(playerWindow);
            if (!TrySetApplicationCloak(playerWindow, false))
                return false;
            ShowWindow(
                playerWindow,
                IsIconic(playerWindow)
                    ? RestoreWindow
                    : ShowWindowNormally);
            var visible = IsWindowVisible(playerWindow)
                && !IsApplicationCloaked(playerWindow);
            if (visible)
                SetForegroundWindow(playerWindow);
            return visible;
#else
            return false;
#endif
        }

        internal static bool TryHide()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            if (playerWindow == IntPtr.Zero)
                return false;
            if (!IsWindowVisible(playerWindow))
            {
                ShowWindow(playerWindow, ShowWindowNormally);
                if (!IsWindowVisible(playerWindow))
                    return false;
            }
            var cloakSet = IsApplicationCloaked(playerWindow)
                || TrySetApplicationCloak(playerWindow, true);
            var hidden =
                cloakSet
                && IsApplicationCloaked(playerWindow)
                && IsWindowVisible(playerWindow);
            return hidden;
#else
            return false;
#endif
        }

        internal static bool TryPostOrderlyClose()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var playerWindow = FindCurrentPlayerWindow();
            return playerWindow != IntPtr.Zero
                && PostMessage(
                    playerWindow,
                    CloseWindowMessage,
                    IntPtr.Zero,
                    IntPtr.Zero);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static IntPtr FindCurrentPlayerWindow()
        {
            var currentProcessId =
                unchecked((uint)Process.GetCurrentProcess().Id);
            if (IsExpectedPlayerWindow(
                    cachedPlayerWindow,
                    currentProcessId))
            {
                return cachedPlayerWindow;
            }
            cachedPlayerWindow = IntPtr.Zero;
            var playerWindow = IntPtr.Zero;
            EnumWindows(
                (window, _) =>
                {
                    if (!IsExpectedPlayerWindow(
                            window,
                            currentProcessId))
                        return true;
                    playerWindow = window;
                    return false;
                },
                IntPtr.Zero);
            cachedPlayerWindow = playerWindow;
            return playerWindow;
        }

        private static bool IsExpectedPlayerWindow(
            IntPtr window,
            uint currentProcessId)
        {
            if (window == IntPtr.Zero || !IsWindow(window))
                return false;
            GetWindowThreadProcessId(window, out var processId);
            if (processId != currentProcessId)
                return false;
            var className = new StringBuilder(128);
            GetClassName(window, className, className.Capacity);
            return string.Equals(
                className.ToString(),
                UnityWindowClass,
                StringComparison.Ordinal);
        }

        private static bool IsApplicationCloaked(IntPtr playerWindow)
        {
            return DwmGetWindowAttribute(
                    playerWindow,
                    DwmWindowAttributeCloaked,
                    out var cloaked,
                    sizeof(uint)) >= 0
                && (cloaked & DwmCloakedByApplication) != 0;
        }

        private static bool TrySetApplicationCloak(
            IntPtr playerWindow,
            bool cloaked)
        {
            var value = cloaked ? 1 : 0;
            return DwmSetWindowAttribute(
                    playerWindow,
                    DwmWindowAttributeCloak,
                    ref value,
                    sizeof(int)) >= 0;
        }

        private static void EnsureUsablePosition(IntPtr playerWindow)
        {
            if (!GetWindowRect(playerWindow, out var rectangle))
                return;
            var width = (long)rectangle.right - rectangle.left;
            var height = (long)rectangle.bottom - rectangle.top;
            if (width <= 0
                || height <= 0
                || width > int.MaxValue
                || height > int.MaxValue)
            {
                return;
            }
            var current = new WindowPosition(
                rectangle.left,
                rectangle.top);
            var resolution = WindowsScreenBoundsService.Resolve(
                true,
                current,
                current,
                (int)width,
                (int)height,
                WindowsScreenBoundsService.GetCurrentWorkAreas());
            if (resolution.Kind
                == PositionResolutionKind.SavedPositionAccepted)
            {
                return;
            }
            SetWindowPos(
                playerWindow,
                IntPtr.Zero,
                resolution.Position.X,
                resolution.Position.Y,
                0,
                0,
                NoSizeNoZOrderNoActivate);
        }
#endif
    }
}
