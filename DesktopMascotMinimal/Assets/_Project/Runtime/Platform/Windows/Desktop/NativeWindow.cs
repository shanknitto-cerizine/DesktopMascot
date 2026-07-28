using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopMascot.Desktop
{
    /// <summary>
    /// Unity Playerのネイティブウィンドウと
    /// Win32 APIの間を仲介します。
    /// </summary>
    public static class NativeWindow
    {
        /// <summary>
        /// Unity Playerのウィンドウハンドルです。
        /// </summary>
        public static IntPtr Handle { get; private set; }

        /// <summary>
        /// 有効なウィンドウハンドルを保持しているかどうかです。
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return
                    Handle != IntPtr.Zero &&
                    IsWindow(Handle);
#else
                return false;
#endif
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        private static bool originalWindowStyleStored;
        private static IntPtr originalWindowStyle;

        private static bool originalExtendedStyleStored;
        private static IntPtr originalExtendedStyle;

#endif

        /// <summary>
        /// 現在のUnity Playerに対応する
        /// トップレベルウィンドウの取得を試みます。
        /// </summary>
        public static bool TryInitialize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            IntPtr windowHandle =
                FindCurrentProcessWindow();

            if (windowHandle == IntPtr.Zero)
            {
                Handle = IntPtr.Zero;
                return false;
            }

            Handle = windowHandle;

            originalWindowStyleStored = false;
            originalWindowStyle = IntPtr.Zero;

            originalExtendedStyleStored = false;
            originalExtendedStyle = IntPtr.Zero;

            return true;
#else
            Handle = IntPtr.Zero;
            return false;
#endif
        }

        /// <summary>
        /// 保持しているウィンドウハンドルを破棄します。
        ///
        /// Windowsウィンドウ自体を閉じる処理ではありません。
        /// </summary>
        public static void Clear()
        {
            Handle = IntPtr.Zero;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            originalWindowStyleStored = false;
            originalWindowStyle = IntPtr.Zero;

            originalExtendedStyleStored = false;
            originalExtendedStyle = IntPtr.Zero;
#endif
        }

        /// <summary>
        /// Unity Playerウィンドウの
        /// 常に最前面表示を切り替えます。
        /// </summary>
        public static bool SetTopMost(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return false;
            }

            IntPtr insertAfter =
                enabled
                    ? WindowInsertAfter.TopMost
                    : WindowInsertAfter.NoTopMost;

            SetWindowPositionFlags flags =
                SetWindowPositionFlags.NoMove |
                SetWindowPositionFlags.NoSize |
                SetWindowPositionFlags.NoActivate |
                SetWindowPositionFlags.ShowWindow;

            return SetWindowPos(
                Handle,
                insertAfter,
                0,
                0,
                0,
                0,
                flags);
#else
            return false;
#endif
        }

        /// <summary>
        /// タイトルバーやサイズ変更枠の有無を切り替えます。
        /// </summary>
        public static bool SetBorderless(bool borderless)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return false;
            }

            if (!originalWindowStyleStored)
            {
                originalWindowStyle =
                    GetWindowLongPtr(
                        Handle,
                        WindowLongIndex.Style);

                originalWindowStyleStored = true;
            }

            IntPtr newStyle;

            if (borderless)
            {
                long currentStyle =
                    GetWindowLongPtr(
                        Handle,
                        WindowLongIndex.Style)
                    .ToInt64();

                currentStyle &=
                    ~(long)WindowStyle.Caption;

                currentStyle &=
                    ~(long)WindowStyle.ThickFrame;

                currentStyle &=
                    ~(long)WindowStyle.MinimizeBox;

                currentStyle &=
                    ~(long)WindowStyle.MaximizeBox;

                currentStyle &=
                    ~(long)WindowStyle.SystemMenu;

                currentStyle |=
                    (long)WindowStyle.Popup;

                newStyle =
                    new IntPtr(currentStyle);
            }
            else
            {
                if (!originalWindowStyleStored)
                {
                    return false;
                }

                newStyle =
                    originalWindowStyle;
            }

            if (!TrySetWindowLongPtr(
                    WindowLongIndex.Style,
                    newStyle))
            {
                return false;
            }

            return RefreshWindowFrame();
#else
            return false;
#endif
        }

        /// <summary>
        /// 指定したRGB色を完全に透明にする
        /// カラーキー透明化を切り替えます。
        /// </summary>
        public static bool SetColorKeyTransparency(
            bool enabled,
            byte red,
            byte green,
            byte blue)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsAvailable)
            {
                return false;
            }

            IntPtr currentExtendedStyle =
                GetWindowLongPtr(
                    Handle,
                    WindowLongIndex.ExtendedStyle);

            if (!originalExtendedStyleStored)
            {
                originalExtendedStyle =
                    currentExtendedStyle;

                originalExtendedStyleStored = true;
            }

            IntPtr newExtendedStyle;

            if (enabled)
            {
                long styleValue =
                    currentExtendedStyle.ToInt64();

                styleValue |=
                    (long)ExtendedWindowStyle.Layered;

                newExtendedStyle =
                    new IntPtr(styleValue);
            }
            else
            {
                if (!originalExtendedStyleStored)
                {
                    return false;
                }

                newExtendedStyle =
                    originalExtendedStyle;
            }

            if (!TrySetWindowLongPtr(
                    WindowLongIndex.ExtendedStyle,
                    newExtendedStyle))
            {
                return false;
            }

            if (!RefreshWindowFrame())
            {
                return false;
            }

            if (!enabled)
            {
                return true;
            }

            uint colorKey =
                CreateColorReference(
                    red,
                    green,
                    blue);

            return SetLayeredWindowAttributes(
                Handle,
                colorKey,
                255,
                LayeredWindowFlags.ColorKey);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        private delegate bool EnumWindowsCallback(
            IntPtr windowHandle,
            IntPtr parameter);

        /// <summary>
        /// 現在のUnityプロセスに属する
        /// 表示中のトップレベルウィンドウを検索します。
        /// </summary>
        private static IntPtr FindCurrentProcessWindow()
        {
            uint currentProcessId =
                (uint)Process.GetCurrentProcess().Id;

            IntPtr foundWindow =
                IntPtr.Zero;

            EnumWindows(
                delegate(
                    IntPtr windowHandle,
                    IntPtr parameter)
                {
                    GetWindowThreadProcessId(
                        windowHandle,
                        out uint windowProcessId);

                    if (windowProcessId !=
                        currentProcessId)
                    {
                        return true;
                    }

                    if (!IsWindowVisible(
                            windowHandle))
                    {
                        return true;
                    }

                    IntPtr owner =
                        GetWindow(
                            windowHandle,
                            GetWindowCommand.Owner);

                    if (owner != IntPtr.Zero)
                    {
                        return true;
                    }

                    foundWindow =
                        windowHandle;

                    return false;
                },
                IntPtr.Zero);

            return foundWindow;
        }

        /// <summary>
        /// SetWindowLongPtrを呼び出し、
        /// Win32エラーも含めて成功判定します。
        /// </summary>
        private static bool TrySetWindowLongPtr(
            WindowLongIndex index,
            IntPtr newValue)
        {
            SetLastError(0);

            IntPtr previousValue =
                SetWindowLongPtr(
                    Handle,
                    index,
                    newValue);

            int errorCode =
                Marshal.GetLastWin32Error();

            /*
             * 以前の値が0だった場合、
             * 成功していても戻り値がIntPtr.Zeroになります。
             */
            return
                previousValue != IntPtr.Zero ||
                errorCode == 0;
        }

        /// <summary>
        /// ウィンドウスタイルの変更を反映します。
        /// </summary>
        private static bool RefreshWindowFrame()
        {
            SetWindowPositionFlags flags =
                SetWindowPositionFlags.NoMove |
                SetWindowPositionFlags.NoSize |
                SetWindowPositionFlags.NoActivate |
                SetWindowPositionFlags.FrameChanged |
                SetWindowPositionFlags.ShowWindow;

            return SetWindowPos(
                Handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                flags);
        }

        /// <summary>
        /// RGB値をWin32のCOLORREF形式へ変換します。
        ///
        /// COLORREFは0x00BBGGRRです。
        /// </summary>
        private static uint CreateColorReference(
            byte red,
            byte green,
            byte blue)
        {
            return
                red |
                ((uint)green << 8) |
                ((uint)blue << 16);
        }

        /// <summary>
        /// 32bit・64bitの双方で使用できる
        /// GetWindowLongPtrラッパーです。
        /// </summary>
        private static IntPtr GetWindowLongPtr(
            IntPtr windowHandle,
            WindowLongIndex index)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr64(
                    windowHandle,
                    (int)index);
            }

            int result =
                GetWindowLong32(
                    windowHandle,
                    (int)index);

            return new IntPtr(result);
        }

        /// <summary>
        /// 32bit・64bitの双方で使用できる
        /// SetWindowLongPtrラッパーです。
        /// </summary>
        private static IntPtr SetWindowLongPtr(
            IntPtr windowHandle,
            WindowLongIndex index,
            IntPtr newValue)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(
                    windowHandle,
                    (int)index,
                    newValue);
            }

            int result =
                SetWindowLong32(
                    windowHandle,
                    (int)index,
                    newValue.ToInt32());

            return new IntPtr(result);
        }

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(
            EnumWindowsCallback callback,
            IntPtr parameter);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(
            IntPtr windowHandle,
            out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(
            IntPtr windowHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(
            IntPtr windowHandle,
            GetWindowCommand command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(
            IntPtr windowHandle);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            SetWindowPositionFlags flags);

        [DllImport(
            "user32.dll",
            EntryPoint = "GetWindowLong",
            SetLastError = true)]
        private static extern int GetWindowLong32(
            IntPtr windowHandle,
            int index);

        [DllImport(
            "user32.dll",
            EntryPoint = "GetWindowLongPtr",
            SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(
            IntPtr windowHandle,
            int index);

        [DllImport(
            "user32.dll",
            EntryPoint = "SetWindowLong",
            SetLastError = true)]
        private static extern int SetWindowLong32(
            IntPtr windowHandle,
            int index,
            int newValue);

        [DllImport(
            "user32.dll",
            EntryPoint = "SetWindowLongPtr",
            SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(
            IntPtr windowHandle,
            int index,
            IntPtr newValue);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr windowHandle,
            uint colorKey,
            byte alpha,
            LayeredWindowFlags flags);

        [DllImport("kernel32.dll")]
        private static extern void SetLastError(
            uint errorCode);

        private static class WindowInsertAfter
        {
            public static readonly IntPtr TopMost =
                new IntPtr(-1);

            public static readonly IntPtr NoTopMost =
                new IntPtr(-2);
        }

        private enum WindowLongIndex
        {
            ExtendedStyle = -20,
            Style = -16
        }

        [Flags]
        private enum WindowStyle : uint
        {
            Popup = 0x80000000,
            Caption = 0x00C00000,
            SystemMenu = 0x00080000,
            ThickFrame = 0x00040000,
            MinimizeBox = 0x00020000,
            MaximizeBox = 0x00010000
        }

        [Flags]
        private enum ExtendedWindowStyle : long
        {
            Layered = 0x00080000L
        }

        [Flags]
        private enum LayeredWindowFlags : uint
        {
            ColorKey = 0x00000001,
            Alpha = 0x00000002
        }

        [Flags]
        private enum SetWindowPositionFlags : uint
        {
            NoSize = 0x0001,
            NoMove = 0x0002,
            NoActivate = 0x0010,
            FrameChanged = 0x0020,
            ShowWindow = 0x0040
        }

        private enum GetWindowCommand : uint
        {
            Owner = 4
        }

#endif
    }
}