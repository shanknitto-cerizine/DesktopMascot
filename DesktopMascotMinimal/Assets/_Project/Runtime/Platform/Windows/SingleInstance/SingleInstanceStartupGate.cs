using System;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Runtime.SingleInstance
{
    internal static class SingleInstanceStartupGate
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotSingleInstance]";
        private const int Primary = 1;
        private const int SecondarySignalSent = 2;

        private static bool evaluated;
        private static bool normalRuntime;
        private static bool primary;
        private static bool secondary;
        private static bool signalSent;
        private static bool secondaryExitRequested;
        private static bool primaryStartupAbortRequested;

        internal static bool IsPrimary => normalRuntime && primary;
        internal static bool IsSecondary => normalRuntime && secondary;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_InitializeSingleInstance();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetSingleInstanceFailureStage();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_BeginSingleInstanceShutdown();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_ShutdownSingleInstance();
#endif

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Evaluate()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (evaluated)
                return;
            evaluated = true;
            normalRuntime =
                DesktopMascotRuntimeBootstrap.IsNormalRuntimeRequested();
            if (!normalRuntime)
                return;

            // M-037 startup-flash ownership remains first in this single
            // normal-runtime startup orchestration.
            UnityPlayerWindowVisibilityController
                .ApplyStartupCloakForNormalRuntime();
            try
            {
                var result = DMN_InitializeSingleInstance();
                primary = result == Primary;
                secondary = !primary;
                signalSent = result == SecondarySignalSent;
                Debug.Log($"{Prefix} Primary instance acquired: {primary}");
                Debug.Log(
                    $"{Prefix} Secondary instance detected: {secondary}");
                Debug.Log(
                    $"{Prefix} Secondary activation signal sent: " +
                    signalSent);
                Debug.Log(
                    $"{Prefix} Single instance failure stage: " +
                    DMN_GetSingleInstanceFailureStage());
            }
            catch (Exception exception)
            {
                secondary = true;
                Debug.LogError(
                    $"{Prefix} Startup coordination failed: {exception}");
                Debug.Log($"{Prefix} Primary instance acquired: False");
                Debug.Log($"{Prefix} Secondary instance detected: True");
                Debug.Log(
                    $"{Prefix} Secondary activation signal sent: False");
                Debug.Log($"{Prefix} Single instance failure stage: 100");
            }
#endif
        }

        internal static bool ShouldSkipNormalRuntimeStartup()
        {
            return normalRuntime && !primary;
        }

        internal static void RequestSecondaryOrderlyExit()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!secondary || secondaryExitRequested)
                return;
            secondaryExitRequested = true;
            Application.quitting += OnSecondaryQuitting;
            Debug.Log(
                $"{Prefix} Single instance validation passed: {signalSent}");
            Application.Quit(0);
#endif
        }

        internal static void RequestPrimaryStartupAbortOrderlyExit(
            string reason)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!normalRuntime
                || !primary
                || primaryStartupAbortRequested)
            {
                return;
            }
            primaryStartupAbortRequested = true;
            var barrierClosed = false;
            var resourcesReleased = false;
            try
            {
                barrierClosed =
                    DMN_BeginSingleInstanceShutdown() != 0;
                resourcesReleased =
                    DMN_ShutdownSingleInstance() != 0;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{Prefix} Primary startup abort cleanup failed: " +
                    exception);
            }
            primary = false;
            Debug.Log(
                $"{Prefix} Primary startup abort reason: {reason}");
            Debug.Log(
                $"{Prefix} Primary startup abort barrier closed: " +
                barrierClosed);
            Debug.Log(
                $"{Prefix} Primary startup abort resources released: " +
                resourcesReleased);
            var closePosted =
                UnityPlayerWindowVisibilityController
                    .TryPostOrderlyClose();
            Debug.Log(
                $"{Prefix} Primary startup abort UnityWndClass " +
                $"WM_CLOSE posted: {closePosted}");
            Debug.Log(
                $"{Prefix} Primary startup abort Application.Quit " +
                "requested.");
            Application.Quit(1);
#endif
        }

        private static void OnSecondaryQuitting()
        {
            Application.quitting -= OnSecondaryQuitting;
            Debug.Log(
                $"{Prefix} Secondary exited normally: " +
                secondaryExitRequested);
        }
    }
}
