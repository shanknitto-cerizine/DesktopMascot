using System;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;

namespace DesktopMascot.Runtime.SingleInstance
{
    internal sealed class SingleInstanceController : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix = "[DesktopMascotSingleInstance]";

        private UnityPlayerWindowVisibilityController visibility;
        private bool initialized;
        private bool shutdownStarted;
        private bool shutdownCompleted;
        private ulong lastConsumedGeneration;

        internal bool CleanupSucceeded { get; private set; }
        internal int RoutedOpenSettingsCount { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_TryConsumeSingleInstanceActivation(out ulong generation);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_BeginSingleInstanceShutdown();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_ShutdownSingleInstance();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsSingleInstancePrimary();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsSingleInstanceNotificationReady();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsSingleInstanceActivationPending();
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
        private static extern uint
            DMN_GetSingleInstanceNotificationCreatedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetSingleInstanceNotificationDestroyedCount();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetSingleInstanceFailureStage();
#endif

        internal void Configure(
            UnityPlayerWindowVisibilityController visibilityController)
        {
            visibility = visibilityController;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            initialized = SingleInstanceStartupGate.IsPrimary
                && visibility != null
                && DMN_IsSingleInstancePrimary() != 0
                && DMN_IsSingleInstanceNotificationReady() != 0;
#endif
            Debug.Log(
                $"{Prefix} Primary notification route ready: " +
                initialized);
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!initialized || shutdownStarted)
                return;
            if (DMN_TryConsumeSingleInstanceActivation(
                    out var generation) == 0)
            {
                return;
            }
            if (generation <= lastConsumedGeneration)
                return;
            lastConsumedGeneration = generation;
            var routed =
                visibility.TryActivateSettingsFromSecondary();
            if (routed)
                RoutedOpenSettingsCount++;
            Debug.Log(
                $"{Prefix} Activation generation consumed/routed: " +
                $"{generation}/{routed}");
#endif
        }

        internal void BeginShutdown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!initialized || shutdownStarted)
                return;
            shutdownStarted = true;
            var barrierClosed =
                DMN_BeginSingleInstanceShutdown() != 0;
            Debug.Log(
                $"{Prefix} Activation shutdown barrier closed: " +
                barrierClosed);
#endif
        }

        internal bool CompleteShutdown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!initialized)
                return true;
            if (shutdownCompleted)
                return CleanupSucceeded;
            BeginShutdown();
            var shutdownResult = DMN_ShutdownSingleInstance() != 0;
            shutdownCompleted = true;
            var created =
                DMN_GetSingleInstanceNotificationCreatedCount();
            var destroyed =
                DMN_GetSingleInstanceNotificationDestroyedCount();
            var failureStage = DMN_GetSingleInstanceFailureStage();
            var consumeCount =
                DMN_GetSingleInstanceActivationConsumeCount();
            CleanupSucceeded = shutdownResult
                && DMN_IsSingleInstancePrimary() == 0
                && DMN_IsSingleInstanceNotificationReady() == 0
                && DMN_IsSingleInstanceActivationPending() == 0
                && created == destroyed
                && failureStage == 0;
            Debug.Log(
                $"{Prefix} Activation signals received/published/consumed: " +
                $"{DMN_GetSingleInstanceSignalReceivedCount()}/" +
                $"{DMN_GetSingleInstanceGenerationPublishCount()}/" +
                consumeCount);
            Debug.Log(
                $"{Prefix} Activation signals coalesced/rejected: " +
                $"{DMN_GetSingleInstanceActivationCoalescedCount()}/" +
                DMN_GetSingleInstanceActivationRejectedCount());
            Debug.Log(
                $"{Prefix} Maximum pending activation count: " +
                DMN_GetSingleInstanceMaximumPendingCount());
            Debug.Log(
                $"{Prefix} Notification channel created/destroyed: " +
                $"{created}/{destroyed}");
            Debug.Log(
                $"{Prefix} Secondary activation signal received: " +
                (consumeCount > 0));
            Debug.Log(
                $"{Prefix} Secondary OpenSettings routed: " +
                (RoutedOpenSettingsCount > 0));
            Debug.Log(
                $"{Prefix} Single instance failure stage: {failureStage}");
            Debug.Log(
                $"{Prefix} Single instance validation passed: " +
                CleanupSucceeded);
            return CleanupSucceeded;
#else
            return true;
#endif
        }

        private void OnDestroy()
        {
            if (initialized && !shutdownCompleted)
                BeginShutdown();
        }
    }
}
