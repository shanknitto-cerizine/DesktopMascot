using System.Runtime.InteropServices;

namespace DesktopMascot.Runtime
{
    internal static class NativeMascotClickCompletionBridge
    {
        private const string Dll = "DesktopMascotNative";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetNativeMascotCompletedClickGeneration();
#endif

        internal static ulong GetCompletedClickGeneration()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return DMN_GetNativeMascotCompletedClickGeneration();
#else
            return 0;
#endif
        }
    }
}
