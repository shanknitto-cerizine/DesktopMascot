using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionClickThroughDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionFailed = 15;
        private const int CompositionStopped = 17;
        private const int ClickThroughEnabled = 5;
        private const int ClickThroughCompleted = 10;
        private const int ClickThroughFailed = 13;
        private const int ContinuousCompleted = 10;
        private const int ContinuousFailed = 13;
        private const float OverallTimeoutSeconds = 30.0f;
        private const ulong CsOwnDc = 0x00000020;
        private const ulong CsClassDc = 0x00000040;
        private const ulong WsExTransparent = 0x00000020;
        private const ulong WsExToolWindow = 0x00000080;
        private const ulong WsExNoActivate = 0x08000000;
        private const ulong WsExLayered = 0x00080000;
        private bool shutdownRequested;
        private bool waitSucceeded;
        private bool previousRunInBackground;
        private bool runInBackgroundRestored;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionUiThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionClickThroughDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionClickThroughEnabled(int enabled);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionClickThroughState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionClickThroughFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionClickThroughEnabled();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionClickThroughRequestPending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionClickThroughLastRequestSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughEnableRequestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughDisableRequestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughAppliedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughRejectedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionWindowInitialStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionWindowCurrentStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionWindowInitialExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionWindowCurrentExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionWindowClassStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionInitialExtendedStyleRestored();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughHitTestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughTransparentHitTestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughLastRequestThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionClickThroughLastAppliedThreadId();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionClickThroughDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionClickThroughDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
        }

        private IEnumerator Start()
        {
            yield return null;
            var startedAt = Time.realtimeSinceStartup;
            yield return WaitForPrerequisites(startedAt);
            if (!waitSucceeded)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }

            int startResult;
            try
            {
                startResult =
                    DMN_StartCompositionClickThroughDiagnostics();
                var initialExtendedStyle =
                    DMN_GetCompositionWindowInitialExtendedStyle();
                var windowClassStyle =
                    DMN_GetCompositionWindowClassStyle();
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Start result: {startResult}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Initial window style: {H(DMN_GetCompositionWindowInitialStyle())}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Initial extended style: {H(initialExtendedStyle)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Window class style: {H(windowClassStyle)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] CS_OWNDC present: {Has(windowClassStyle, CsOwnDc)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] CS_CLASSDC present: {Has(windowClassStyle, CsClassDc)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Initial click-through enabled: {B(DMN_IsCompositionClickThroughEnabled())}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] WS_EX_LAYERED present: {Has(initialExtendedStyle, WsExLayered)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] WS_EX_NOACTIVATE present: {Has(initialExtendedStyle, WsExNoActivate)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] WS_EX_TOOLWINDOW present: {Has(initialExtendedStyle, WsExToolWindow)}");
                Debug.Log(
                    $"[DesktopMascotClickThroughDiagnostics] Composition UI thread ID: {DMN_GetCompositionUiThreadId()}");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                SafeShutdown();
                Cleanup();
                yield break;
            }
            if (startResult != 1
                || DMN_IsCompositionClickThroughEnabled() != 0)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }

            DesktopMascotContinuousCompositionDiagnostics.
                TargetFpsForNextRun = 30;
            DesktopMascotContinuousCompositionDiagnostics.
                PatternBorderMode = 0;
            var continuousObject = new GameObject(
                nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuousObject);
            continuousObject.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();

            yield return WaitForFirstPresent(startedAt);
            if (!waitSucceeded)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }

            yield return new WaitForSecondsRealtime(3.0f);
            if (TimedOut(startedAt)
                || DMN_RequestCompositionClickThroughEnabled(1) != 1)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }
            yield return WaitForRequest(
                true,
                ClickThroughEnabled,
                startedAt);
            if (!waitSucceeded)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }
            DesktopMascotContinuousCompositionDiagnostics.
                PatternBorderMode = 1;
            var enabledStyle =
                DMN_GetCompositionWindowCurrentExtendedStyle();
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] Enable completed.");
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] Primary mechanism: WS_EX_LAYERED | WS_EX_TRANSPARENT");
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] Requested enabled: True");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Actual enabled: {B(DMN_IsCompositionClickThroughEnabled())}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Extended style after enable: {H(enabledStyle)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] WS_EX_LAYERED after enable: {Has(enabledStyle, WsExLayered)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] WS_EX_TRANSPARENT after enable: {Has(enabledStyle, WsExTransparent)}");

            yield return new WaitForSecondsRealtime(8.0f);
            if (TimedOut(startedAt)
                || DMN_RequestCompositionClickThroughEnabled(0) != 1)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }
            yield return WaitForRequest(
                false,
                ClickThroughCompleted,
                startedAt);
            if (!waitSucceeded)
            {
                SafeShutdown();
                Cleanup();
                yield break;
            }
            DesktopMascotContinuousCompositionDiagnostics.
                PatternBorderMode = 2;
            var disabledStyle =
                DMN_GetCompositionWindowCurrentExtendedStyle();
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] Disable completed.");
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] Requested enabled: False");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Actual enabled: {B(DMN_IsCompositionClickThroughEnabled())}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Extended style after disable: {H(disabledStyle)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Initial extended style restored: {B(DMN_IsCompositionInitialExtendedStyleRestored())}");

            var completedState = DMN_GetCompositionClickThroughState();
            var failureStage =
                DMN_GetCompositionClickThroughFailureStage();
            var enableRequests =
                DMN_GetCompositionClickThroughEnableRequestCount();
            var disableRequests =
                DMN_GetCompositionClickThroughDisableRequestCount();
            var appliedRequests =
                DMN_GetCompositionClickThroughAppliedCount();
            var rejectedRequests =
                DMN_GetCompositionClickThroughRejectedCount();
            var pending =
                DMN_IsCompositionClickThroughRequestPending();
            var finalEnabled =
                DMN_IsCompositionClickThroughEnabled();
            var lastSucceeded =
                DMN_DidCompositionClickThroughLastRequestSucceed();
            var lastError =
                DMN_GetCompositionClickThroughLastWin32Error();
            var hitTestCount =
                DMN_GetCompositionClickThroughHitTestCount();
            var transparentHitTestCount =
                DMN_GetCompositionClickThroughTransparentHitTestCount();
            var requestThread =
                DMN_GetCompositionClickThroughLastRequestThreadId();
            var appliedThread =
                DMN_GetCompositionClickThroughLastAppliedThreadId();
            var initialStyleRestored =
                DMN_IsCompositionInitialExtendedStyleRestored();

            yield return new WaitForSecondsRealtime(8.0f);
            while (!TimedOut(startedAt))
            {
                var continuousState =
                    DMN_GetContinuousCompositionState();
                if (continuousState == ContinuousCompleted
                    || continuousState == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            while (!TimedOut(startedAt))
            {
                if (DMN_GetCompositionInitializationState()
                    == CompositionStopped)
                {
                    break;
                }
                yield return null;
            }
            var shutdownComplete =
                DMN_GetCompositionInitializationState()
                    == CompositionStopped;

            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] State: {StateName(completedState)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Failure stage: {failureStage}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Enable requests: {enableRequests}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Disable requests: {disableRequests}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Applied requests: {appliedRequests}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Rejected requests: {rejectedRequests}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Request pending: {B(pending)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Click-through enabled: {B(finalEnabled)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Initial extended style restored: {B(initialStyleRestored)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Last request succeeded: {B(lastSucceeded)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Last Win32 error: {lastError}");
            Debug.Log(
                "[DesktopMascotClickThroughDiagnostics] HTTRANSPARENT fallback enabled: True");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Hit-test count: {hitTestCount}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Transparent hit-test count: {transparentHitTestCount}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Request thread ID: {requestThread}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Style applied thread ID: {appliedThread}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Final window style: {H(DMN_GetCompositionWindowCurrentStyle())}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Final extended style: {H(disabledStyle)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] WS_EX_TRANSPARENT present after disable: {Has(disabledStyle, WsExTransparent)}");
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] Shutdown complete: {shutdownComplete}");
            RestoreRunInBackground();
            Cleanup();
        }

        private IEnumerator WaitForPrerequisites(float startedAt)
        {
            waitSucceeded = false;
            while (!TimedOut(startedAt))
            {
                var state = DMN_GetCompositionInitializationState();
                if (state == CompositionMessageLoopRunning
                    && DMN_IsDestinationTextureAvailable() != 0
                    && DMN_WasReadbackValidationCompleted() != 0
                    && DMN_DidReadbackExpectedOrientationMatch() != 0)
                {
                    waitSucceeded = true;
                    yield break;
                }
                if (state == CompositionFailed)
                {
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator WaitForFirstPresent(float startedAt)
        {
            waitSucceeded = false;
            while (!TimedOut(startedAt))
            {
                if (DMN_GetContinuousCompositionPresentCount() > 0)
                {
                    waitSucceeded = true;
                    yield break;
                }
                if (DMN_GetContinuousCompositionState()
                    == ContinuousFailed)
                {
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator WaitForRequest(
            bool expectedEnabled,
            int expectedState,
            float startedAt)
        {
            waitSucceeded = false;
            while (!TimedOut(startedAt))
            {
                var state = DMN_GetCompositionClickThroughState();
                if (state == ClickThroughFailed)
                {
                    yield break;
                }
                if (DMN_IsCompositionClickThroughRequestPending() == 0
                    && state == expectedState
                    && B(DMN_IsCompositionClickThroughEnabled())
                        == expectedEnabled)
                {
                    waitSucceeded = true;
                    yield break;
                }
                yield return null;
            }
        }

        private static bool TimedOut(float startedAt)
        {
            return Time.realtimeSinceStartup - startedAt
                >= OverallTimeoutSeconds;
        }

        private void SafeShutdown()
        {
            if (shutdownRequested)
            {
                return;
            }
            shutdownRequested = true;
            try
            {
                DMN_RequestContinuousCompositionDiagnosticsStop();
                DMN_RequestCompositionDiagnosticsShutdown();
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
            }
        }

        private void OnApplicationQuit()
        {
            SafeShutdown();
            RestoreRunInBackground();
        }

        private void Cleanup()
        {
            DesktopMascotContinuousCompositionDiagnostics.
                PatternBorderMode = 0;
            RestoreRunInBackground();
            Destroy(gameObject);
        }

        private void RestoreRunInBackground()
        {
            if (runInBackgroundRestored)
            {
                return;
            }
            Application.runInBackground = previousRunInBackground;
            runInBackgroundRestored =
                Application.runInBackground == previousRunInBackground;
            Debug.Log(
                $"[DesktopMascotClickThroughDiagnostics] runInBackground restored: {runInBackgroundRestored}");
        }

        private static string StateName(int state)
        {
            return state switch
            {
                0 => "NotStarted",
                1 => "Ready",
                2 => "EnableRequested",
                3 => "EnableMessagePosted",
                4 => "EnableMessageReceived",
                5 => "Enabled",
                6 => "DisableRequested",
                7 => "DisableMessagePosted",
                8 => "DisableMessageReceived",
                9 => "Disabled",
                10 => "Completed",
                11 => "ShutdownRequested",
                12 => "Stopped",
                13 => "Failed",
                _ => $"Unknown({state})"
            };
        }

        private static bool IsInteropException(Exception exception)
        {
            return exception is DllNotFoundException
                || exception is EntryPointNotFoundException
                || exception is BadImageFormatException;
        }

        private static void LogInteropError(Exception exception)
        {
            Debug.LogError(
                $"[DesktopMascotClickThroughDiagnostics] Native interop failed: {exception.Message}");
        }

        private static bool B(int value)
        {
            return value != 0;
        }

        private static bool Has(ulong value, ulong bit)
        {
            return (value & bit) != 0;
        }

        private static string H(ulong value)
        {
            return $"0x{value:X16}";
        }
#endif
    }
}
