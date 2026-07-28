using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DesktopMascot.Runtime
{
    internal readonly struct MonitorWorkArea
    {
        internal MonitorWorkArea(
            int left,
            int top,
            int right,
            int bottom,
            bool primary)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            IsPrimary = primary;
        }

        internal int Left { get; }
        internal int Top { get; }
        internal int Right { get; }
        internal int Bottom { get; }
        internal bool IsPrimary { get; }
    }

    internal enum PositionResolutionKind
    {
        DefaultPosition,
        SavedPositionAccepted,
        SavedPositionClamped,
        RecoveredToPrimaryOrDefault
    }

    internal readonly struct PositionResolution
    {
        internal PositionResolution(
            WindowPosition position,
            PositionResolutionKind kind)
        {
            Position = position;
            Kind = kind;
        }

        internal WindowPosition Position { get; }
        internal PositionResolutionKind Kind { get; }
    }

    internal static class WindowsScreenBoundsService
    {
        internal const int MinimumVisiblePixels = 48;

        private const uint MonitorInfoPrimary = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        private struct MonitorInfo
        {
            internal uint size;
            internal NativeRect monitor;
            internal NativeRect work;
            internal uint flags;
        }

        private delegate bool MonitorEnumerationCallback(
            IntPtr monitor,
            IntPtr deviceContext,
            ref NativeRect monitorRectangle,
            IntPtr data);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clipRectangle,
            MonitorEnumerationCallback callback,
            IntPtr data);

        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(
            IntPtr monitor,
            ref MonitorInfo info);
#endif

        internal static IReadOnlyList<MonitorWorkArea> GetCurrentWorkAreas()
        {
            var result = new List<MonitorWorkArea>();
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            MonitorEnumerationCallback callback =
                (IntPtr monitor,
                    IntPtr deviceContext,
                    ref NativeRect rectangle,
                    IntPtr data) =>
                {
                    var info = new MonitorInfo
                    {
                        size = (uint)Marshal.SizeOf<MonitorInfo>()
                    };
                    if (GetMonitorInfo(monitor, ref info))
                    {
                        result.Add(new MonitorWorkArea(
                            info.work.left,
                            info.work.top,
                            info.work.right,
                            info.work.bottom,
                            (info.flags & MonitorInfoPrimary) != 0));
                    }
                    return true;
                };
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                callback,
                IntPtr.Zero);
            GC.KeepAlive(callback);
#endif
            return result;
        }

        internal static PositionResolution Resolve(
            bool hasSavedPosition,
            WindowPosition savedPosition,
            WindowPosition defaultPosition,
            int windowWidth,
            int windowHeight,
            IReadOnlyList<MonitorWorkArea> workAreas)
        {
            if (windowWidth <= 0 || windowHeight <= 0
                || workAreas == null || workAreas.Count == 0)
            {
                return new PositionResolution(
                    defaultPosition,
                    PositionResolutionKind.RecoveredToPrimaryOrDefault);
            }

            if (hasSavedPosition
                && IsMinimumVisible(
                    savedPosition,
                    windowWidth,
                    windowHeight,
                    workAreas))
            {
                return new PositionResolution(
                    savedPosition,
                    PositionResolutionKind.SavedPositionAccepted);
            }

            if (hasSavedPosition)
            {
                var nearest = FindNearest(savedPosition, workAreas);
                return new PositionResolution(
                    ClampForRecovery(
                        savedPosition,
                        nearest,
                        windowWidth,
                        windowHeight),
                    PositionResolutionKind.SavedPositionClamped);
            }

            var primary = FindPrimary(workAreas);
            var resolvedDefault = ClampForRecovery(
                defaultPosition,
                primary,
                windowWidth,
                windowHeight);
            return new PositionResolution(
                resolvedDefault,
                resolvedDefault.Equals(defaultPosition)
                    ? PositionResolutionKind.DefaultPosition
                    : PositionResolutionKind.RecoveredToPrimaryOrDefault);
        }

        internal static bool IsMinimumVisible(
            WindowPosition position,
            int windowWidth,
            int windowHeight,
            IReadOnlyList<MonitorWorkArea> workAreas)
        {
            var requiredWidth = Math.Min(
                MinimumVisiblePixels, windowWidth);
            var requiredHeight = Math.Min(
                MinimumVisiblePixels, windowHeight);
            var right = (long)position.X + windowWidth;
            var bottom = (long)position.Y + windowHeight;
            foreach (var area in workAreas)
            {
                var intersectionWidth = Math.Min(
                    right, area.Right) - Math.Max(position.X, area.Left);
                var intersectionHeight = Math.Min(
                    bottom, area.Bottom) - Math.Max(position.Y, area.Top);
                if (intersectionWidth >= requiredWidth
                    && intersectionHeight >= requiredHeight)
                {
                    return true;
                }
            }
            return false;
        }

        private static MonitorWorkArea FindPrimary(
            IReadOnlyList<MonitorWorkArea> workAreas)
        {
            foreach (var area in workAreas)
            {
                if (area.IsPrimary)
                    return area;
            }
            return workAreas[0];
        }

        private static MonitorWorkArea FindNearest(
            WindowPosition position,
            IReadOnlyList<MonitorWorkArea> workAreas)
        {
            var best = workAreas[0];
            var bestDistance = DistanceSquared(position, best);
            for (var index = 1; index < workAreas.Count; ++index)
            {
                var distance = DistanceSquared(position, workAreas[index]);
                if (distance < bestDistance)
                {
                    best = workAreas[index];
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static double DistanceSquared(
            WindowPosition position,
            MonitorWorkArea area)
        {
            var x = Math.Max(
                area.Left,
                Math.Min((double)position.X, area.Right));
            var y = Math.Max(
                area.Top,
                Math.Min((double)position.Y, area.Bottom));
            var deltaX = position.X - x;
            var deltaY = position.Y - y;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static WindowPosition ClampForRecovery(
            WindowPosition position,
            MonitorWorkArea area,
            int windowWidth,
            int windowHeight)
        {
            var areaWidth = (long)area.Right - area.Left;
            var areaHeight = (long)area.Bottom - area.Top;
            var minimumX = (long)area.Left;
            var maximumX = windowWidth <= areaWidth
                ? (long)area.Right - windowWidth
                : minimumX;
            var minimumY = (long)area.Top;
            var maximumY = windowHeight <= areaHeight
                ? (long)area.Bottom - windowHeight
                : minimumY;
            return new WindowPosition(
                (int)Math.Max(
                    minimumX,
                    Math.Min((long)position.X, maximumX)),
                (int)Math.Max(
                    minimumY,
                    Math.Min((long)position.Y, maximumY)));
        }
    }
}
