using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot
{
    public sealed class Win32Window : MonoBehaviour
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_EX_LAYERED = 0x00080000L;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const uint LWA_COLORKEY = 0x00000001;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }
#endif

        private IntPtr _window;
        private bool _clickThrough;

        public void Initialize(bool alwaysOnTop)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _window = GetActiveWindow();
            var style = GetWindowLongPtr64(_window, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME);
            SetWindowLongPtr64(_window, GWL_STYLE, new IntPtr(style));

            var exStyle = GetWindowLongPtr64(_window, GWL_EXSTYLE).ToInt64() | WS_EX_LAYERED;
            SetWindowLongPtr64(_window, GWL_EXSTYLE, new IntPtr(exStyle));
            SetLayeredWindowAttributes(_window, 0x000000, 0, LWA_COLORKEY);
            SetAlwaysOnTop(alwaysOnTop);
            SetWindowPos(_window, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
#endif
        }

        public void SetAlwaysOnTop(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            SetWindowPos(_window, enabled ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
#endif
        }

        public void SetClickThrough(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_window == IntPtr.Zero || _clickThrough == enabled) return;
            _clickThrough = enabled;
            var exStyle = GetWindowLongPtr64(_window, GWL_EXSTYLE).ToInt64();
            exStyle = enabled ? exStyle | WS_EX_TRANSPARENT : exStyle & ~WS_EX_TRANSPARENT;
            SetWindowLongPtr64(_window, GWL_EXSTYLE, new IntPtr(exStyle));
#endif
        }

        public void BeginNativeDrag()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            const uint WM_NCLBUTTONDOWN = 0x00A1;
            const int HTCAPTION = 2;
            SetClickThrough(false);
            ReleaseCapture();
            SendMessage(_window, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
#endif
        }

        public Vector2Int GetPosition()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_window != IntPtr.Zero && GetWindowRect(_window, out var rect)) return new Vector2Int(rect.Left, rect.Top);
#endif
            return Vector2Int.zero;
        }

        public void SetPosition(int x, int y)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_window != IntPtr.Zero) SetWindowPos(_window, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE);
#endif
        }
    }
}
