using System;

namespace DesktopMascot.Runtime
{
    internal readonly struct WindowPosition : IEquatable<WindowPosition>
    {
        internal WindowPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal int X { get; }
        internal int Y { get; }

        public bool Equals(WindowPosition other) =>
            X == other.X && Y == other.Y;

        public override bool Equals(object value) =>
            value is WindowPosition other && Equals(other);

        public override int GetHashCode() =>
            unchecked((X * 397) ^ Y);

        public override string ToString() => $"({X}, {Y})";
    }

    internal enum WindowPositionLoadStatus
    {
        Loaded,
        Missing,
        ParseFailure,
        NumericValidationFailure,
        UnsupportedSchema,
        ReadFailure
    }

    internal readonly struct WindowPositionLoadResult
    {
        internal WindowPositionLoadResult(
            WindowPositionLoadStatus status,
            WindowPosition position,
            string detail)
        {
            Status = status;
            Position = position;
            Detail = detail;
        }

        internal WindowPositionLoadStatus Status { get; }
        internal WindowPosition Position { get; }
        internal string Detail { get; }
        internal bool HasPosition =>
            Status == WindowPositionLoadStatus.Loaded;
    }
}
