using System;
using System.Runtime.InteropServices;

namespace DesktopMascot.Runtime
{
    internal static class NativeMascotWindowPositionBridge
    {
        private const string Dll = "DesktopMascotNative";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_SetCompositionInitialPosition(
            int x,
            int y);

        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetNativeMascotCompletedDragGeneration();

        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_TryGetNativeMascotWindowPosition(
            out int x,
            out int y);
#endif

        internal static bool TrySetInitialPosition(
            WindowPosition position,
            out string error)
        {
            error = string.Empty;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                if (DMN_SetCompositionInitialPosition(
                        position.X, position.Y) != 0)
                {
                    return true;
                }
                error = "native composition already started";
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                error = exception.GetType().Name + ": " + exception.Message;
            }
#else
            error = "Windows Player native bridge unavailable";
#endif
            return false;
        }

        internal static ulong GetCompletedDragGeneration()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return DMN_GetNativeMascotCompletedDragGeneration();
#else
            return 0;
#endif
        }

        internal static bool TryGetCurrentPosition(
            out WindowPosition position)
        {
            position = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (DMN_TryGetNativeMascotWindowPosition(
                    out var x, out var y) != 0)
            {
                position = new WindowPosition(x, y);
                return true;
            }
#endif
            return false;
        }

        private static bool IsInteropException(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;
    }
}
