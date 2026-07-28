using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotSingleInstanceDiagnostics
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix =
            "[DesktopMascotSingleInstanceDiagnostics]";

        internal static bool Passed { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_RunSingleInstanceCoalescingFocusedDiagnostic();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsSingleInstanceActivationPending();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetSingleInstanceActivationGeneration();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceSignalReceivedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceGenerationPublishCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceActivationConsumeCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceActivationCoalescedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceActivationRejectedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceMaximumPendingCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetSingleInstanceFailureStage();
#endif

        internal static bool RunFocusedTests()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var nativePassed =
                    DMN_RunSingleInstanceCoalescingFocusedDiagnostic() != 0;
                var countersPassed =
                    DMN_GetSingleInstanceSignalReceivedCount() == 12
                    && DMN_GetSingleInstanceGenerationPublishCount() == 2
                    && DMN_GetSingleInstanceActivationConsumeCount() == 2
                    && DMN_GetSingleInstanceActivationCoalescedCount() == 9
                    && DMN_GetSingleInstanceActivationRejectedCount() == 1
                    && DMN_GetSingleInstanceMaximumPendingCount() == 1
                    && DMN_GetSingleInstanceActivationGeneration() == 2
                    && DMN_IsSingleInstanceActivationPending() == 0
                    && DMN_GetSingleInstanceFailureStage() == 0;
                Passed = nativePassed && countersPassed;
                Debug.Log(
                    $"{Prefix} Burst coalesces to pending generations: " +
                    countersPassed);
                Debug.Log(
                    $"{Prefix} Native focused diagnostic passed: " +
                    nativePassed);
            }
            catch (Exception exception)
            {
                Passed = false;
                Debug.LogError(
                    $"{Prefix} Unexpected test failure: {exception}");
            }
#else
            Passed = true;
#endif
            Debug.Log($"{Prefix} Automated tests passed: {Passed}");
            return Passed;
        }
    }
}
