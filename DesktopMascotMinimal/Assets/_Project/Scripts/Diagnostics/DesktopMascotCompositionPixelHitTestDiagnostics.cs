using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionPixelHitTestDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Threshold = 128;
        private const float VisualTestDurationSeconds = 30.0f;
        private const float OverallTimeoutSeconds = 60.0f;
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionFailed = 15;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int AlphaCompleted = 6;
        private const int PixelVisualTestRunning = 5;
        private const int PixelCompleted = 6;
        private const int PixelStopped = 8;
        private const int PixelFailed = 9;
        private const ulong WsExLayered = 0x00080000;
        private const ulong WsExTransparent = 0x00000020;
        private const ulong CsOwnDc = 0x0020;
        private const ulong CsClassDc = 0x0040;

        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool visualTestRunning;
        private bool shutdownRequested;
        private float visualTestEndsAt;
        private int cachedCompletedState;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionAlphaMaskSampleAlpha(int x, int y);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionAlphaMaskPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionAlphaMaskAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionPixelHitTestDiagnostics(int alphaThreshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_CompleteCompositionPixelHitTestDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StopCompositionPixelHitTestDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionPixelHitTestEnabled();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestTotalCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestTransparentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestOpaqueCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestOutsideClientCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestMaskUnavailableCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestScreenToClientFailureCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestLastX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestLastY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestLastAlpha();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionPixelHitTestLastResult();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPixelHitTestPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionPixelHitTestLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPixelHitTestStyleRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionPixelHitTestRequestPending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionPixelHitTestLastRequestSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPixelHitTestInitialExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPixelHitTestDiagnosticExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPixelHitTestCurrentExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern ulong DMN_GetCompositionPixelHitTestWindowClassStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionPixelHitTestDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionPixelHitTestDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Debug.Log(
                $"[DesktopMascotPixelHitTestDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log(
                $"[DesktopMascotPixelHitTestDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
        }

        private IEnumerator Start()
        {
            yield return null;
            var startedAt = Time.realtimeSinceStartup;
            while (!TimedOut(startedAt) && !PrerequisitesReady())
            {
                if (DMN_GetCompositionInitializationState()
                    == CompositionFailed)
                {
                    yield return ShutdownAndQuit();
                    yield break;
                }
                yield return null;
            }
            if (TimedOut(startedAt))
            {
                LogError("Composition prerequisites timed out.");
                yield return ShutdownAndQuit();
                yield break;
            }

            DesktopMascotCompositionAlphaMaskDiagnostics.
                ExternalLifecycleOwnerForNextRun = true;
            var alphaObject = new GameObject(
                nameof(DesktopMascotCompositionAlphaMaskDiagnostics));
            DontDestroyOnLoad(alphaObject);
            alphaObject.AddComponent<
                DesktopMascotCompositionAlphaMaskDiagnostics>();

            while (!TimedOut(startedAt)
                   && DMN_GetCompositionAlphaMaskState() != AlphaCompleted)
            {
                if (DMN_GetCompositionAlphaMaskFailureStage() != 0)
                {
                    LogError("Alpha mask transport failed.");
                    yield return ShutdownAndQuit();
                    yield break;
                }
                yield return null;
            }
            if (DMN_GetCompositionAlphaMaskState() != AlphaCompleted
                || DMN_IsCompositionAlphaMaskAvailable() == 0)
            {
                LogError("Published alpha mask did not become available.");
                yield return ShutdownAndQuit();
                yield break;
            }
            if (!ValidateRepresentativeSamples())
            {
                LogError("Representative alpha validation failed.");
                yield return ShutdownAndQuit();
                yield break;
            }

            var startResult =
                DMN_StartCompositionPixelHitTestDiagnostics(Threshold);
            Debug.Log(
                $"[DesktopMascotPixelHitTestDiagnostics] Start result: {startResult}");
            if (startResult != 1)
            {
                yield return ShutdownAndQuit();
                yield break;
            }
            while (!TimedOut(startedAt))
            {
                var state = DMN_GetCompositionPixelHitTestState();
                if (state == PixelVisualTestRunning || state == PixelFailed)
                {
                    break;
                }
                yield return null;
            }
            if (DMN_GetCompositionPixelHitTestState() != PixelVisualTestRunning)
            {
                LogError("Pixel hit-test did not become ready.");
                yield return ShutdownAndQuit();
                yield break;
            }

            LogStartState();
            visualTestEndsAt =
                Time.realtimeSinceStartup + VisualTestDurationSeconds;
            visualTestRunning = true;
            while (Time.realtimeSinceStartup < visualTestEndsAt
                   && !TimedOut(startedAt))
            {
                if (DMN_GetCompositionPixelHitTestState() == PixelFailed
                    || DMN_GetContinuousCompositionState()
                        == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            visualTestRunning = false;
            if (DMN_GetCompositionPixelHitTestState() == PixelFailed)
            {
                LogError("Pixel hit-test failed during visual test.");
            }
            else
            {
                DMN_CompleteCompositionPixelHitTestDiagnostics();
            }
            cachedCompletedState =
                DMN_GetCompositionPixelHitTestState();
            LogCounters(false);

            var stopResult =
                DMN_StopCompositionPixelHitTestDiagnostics();
            if (stopResult != 1)
            {
                LogError("Pixel hit-test stop request was rejected.");
            }
            while (!TimedOut(startedAt)
                   && DMN_GetCompositionPixelHitTestState()
                       != PixelStopped
                   && DMN_GetCompositionPixelHitTestState()
                       != PixelFailed)
            {
                yield return null;
            }
            LogCounters(true);

            DMN_StopCompositionAlphaMaskDiagnostics();
            while (!TimedOut(startedAt))
            {
                var continuousState =
                    DMN_GetContinuousCompositionState();
                if (continuousState == ContinuousCompleted
                    || continuousState == ContinuousStopped
                    || continuousState == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while (!TimedOut(startedAt)
                   && DMN_GetContinuousCompositionState()
                       != ContinuousStopped
                   && DMN_GetContinuousCompositionState()
                       != ContinuousFailed)
            {
                yield return null;
            }
            LogContinuousState();

            yield return ShutdownAndQuit();
        }

        private void OnGUI()
        {
            if (!visualTestRunning)
            {
                return;
            }
            const float scale = 8.0f;
            const float size = 64.0f * scale;
            var x = Math.Max(16.0f, (Screen.width - size) * 0.5f);
            var y = 70.0f;
            var half = size * 0.5f;
            DrawRegion(
                new Rect(x, y, half, half),
                new Color(0.35f, 0.08f, 0.08f, 1.0f),
                "TOP-LEFT\nalpha 0\nTRANSPARENT");
            DrawRegion(
                new Rect(x + half, y, half, half),
                new Color(0.08f, 0.35f, 0.08f, 1.0f),
                "TOP-RIGHT\nalpha 64\nTRANSPARENT");
            DrawRegion(
                new Rect(x, y + half, half, half),
                new Color(0.08f, 0.08f, 0.45f, 1.0f),
                "BOTTOM-LEFT\nalpha 128\nOPAQUE");
            DrawRegion(
                new Rect(x + half, y + half, half, half),
                new Color(0.35f, 0.35f, 0.35f, 1.0f),
                "BOTTOM-RIGHT\nalpha 255\nOPAQUE");
            DrawRegion(
                new Rect(
                    x + 24.0f * scale,
                    y + 24.0f * scale,
                    16.0f * scale,
                    16.0f * scale),
                new Color(0.5f, 0.18f, 0.5f, 1.0f),
                "CENTER\n192\nOPAQUE");
            GUI.color = Color.white;
            var remaining = Math.Max(
                0,
                Mathf.CeilToInt(
                    visualTestEndsAt - Time.realtimeSinceStartup));
            GUI.Label(
                new Rect(16, 12, Screen.width - 32, 52),
                $"Pixel Hit-Test Diagnostics — {remaining}s remaining\n"
                + $"Composition window: ({DMN_GetCompositionWindowInitialX()}, "
                + $"{DMN_GetCompositionWindowInitialY()}) size 64x64. "
                + "Place Notepad behind it and click all five regions.");
        }

        private static void DrawRegion(
            Rect rectangle,
            Color color,
            string label)
        {
            GUI.color = color;
            GUI.Box(rectangle, GUIContent.none);
            GUI.color = Color.white;
            GUI.Box(rectangle, label);
        }

        private static bool ValidateRepresentativeSamples()
        {
            var topLeft = DMN_GetCompositionAlphaMaskSampleAlpha(8, 8);
            var topRight = DMN_GetCompositionAlphaMaskSampleAlpha(55, 8);
            var bottomLeft =
                DMN_GetCompositionAlphaMaskSampleAlpha(8, 55);
            var bottomRight =
                DMN_GetCompositionAlphaMaskSampleAlpha(55, 55);
            var center = DMN_GetCompositionAlphaMaskSampleAlpha(32, 32);
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Top-left alpha: {topLeft}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Top-right alpha: {topRight}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Bottom-left alpha: {bottomLeft}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Bottom-right alpha: {bottomRight}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Center alpha: {center}");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Top-left expected result: HTTRANSPARENT");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Top-right expected result: HTTRANSPARENT");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Bottom-left expected result: HTCLIENT");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Bottom-right expected result: HTCLIENT");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Center expected result: HTCLIENT");
            return topLeft == 0
                && topRight == 64
                && bottomLeft == 128
                && bottomRight == 255
                && center == 192;
        }

        private static void LogStartState()
        {
            var initial =
                DMN_GetCompositionPixelHitTestInitialExtendedStyle();
            var diagnostic =
                DMN_GetCompositionPixelHitTestDiagnosticExtendedStyle();
            var classStyle =
                DMN_GetCompositionPixelHitTestWindowClassStyle();
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Threshold: {DMN_GetCompositionPixelHitTestThreshold()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Initial extended style: {H(initial)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Diagnostic extended style: {H(diagnostic)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] WS_EX_LAYERED active: {Has(diagnostic, WsExLayered)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] WS_EX_TRANSPARENT active: {Has(diagnostic, WsExTransparent)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] CS_OWNDC present: {Has(classStyle, CsOwnDc)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] CS_CLASSDC present: {Has(classStyle, CsClassDc)}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Published generation: {DMN_GetCompositionAlphaMaskPublishedGeneration()}");
            Debug.Log("[DesktopMascotPixelHitTestDiagnostics] Published origin: TopLeft");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Composition window position: ({DMN_GetCompositionWindowInitialX()}, {DMN_GetCompositionWindowInitialY()})");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Visual test duration seconds: {VisualTestDurationSeconds:F0}");
        }

        private void LogCounters(bool afterRestore)
        {
            var transparent =
                DMN_GetCompositionPixelHitTestTransparentCount();
            var opaque = DMN_GetCompositionPixelHitTestOpaqueCount();
            var verificationComplete = transparent > 0 && opaque > 0;
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] State: {StateName(afterRestore ? cachedCompletedState : DMN_GetCompositionPixelHitTestState())}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Failure stage: {DMN_GetCompositionPixelHitTestFailureStage()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Total hit-test count: {DMN_GetCompositionPixelHitTestTotalCount()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Transparent hit-test count: {transparent}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Opaque hit-test count: {opaque}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Outside-client hit-test count: {DMN_GetCompositionPixelHitTestOutsideClientCount()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Mask-unavailable hit-test count: {DMN_GetCompositionPixelHitTestMaskUnavailableCount()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] ScreenToClient failure count: {DMN_GetCompositionPixelHitTestScreenToClientFailureCount()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last x: {DMN_GetCompositionPixelHitTestLastX()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last y: {DMN_GetCompositionPixelHitTestLastY()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last alpha: {DMN_GetCompositionPixelHitTestLastAlpha()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last result: {ResultName(DMN_GetCompositionPixelHitTestLastResult())}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Published generation: {DMN_GetCompositionPixelHitTestPublishedGeneration()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Visual verification counters complete: {verificationComplete}");
            if (!verificationComplete)
            {
                Debug.LogWarning(
                    "[DesktopMascotPixelHitTestDiagnostics] Visual verification incomplete: both transparent and opaque regions were not observed.");
            }
            if (afterRestore)
            {
                Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Final extended style: {H(DMN_GetCompositionPixelHitTestCurrentExtendedStyle())}");
                Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Initial extended style restored: {B(DMN_DidCompositionPixelHitTestStyleRestoreSucceed())}");
                Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last request succeeded: {B(DMN_DidCompositionPixelHitTestLastRequestSucceed())}");
                Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Request pending: {B(DMN_IsCompositionPixelHitTestRequestPending())}");
                Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last Win32 error: {DMN_GetCompositionPixelHitTestLastWin32Error()}");
            }
        }

        private static void LogContinuousState()
        {
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Continuous state: {DMN_GetContinuousCompositionState()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Continuous failure stage: {DMN_GetContinuousCompositionFailureStage()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Completed normally: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private IEnumerator ShutdownAndQuit()
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                visualTestRunning = false;
                try
                {
                    DMN_StopCompositionPixelHitTestDiagnostics();
                    DMN_StopCompositionAlphaMaskDiagnostics();
                    DMN_RequestContinuousCompositionDiagnosticsStop();
                    DMN_RequestCompositionDiagnosticsShutdown();
                }
                catch (Exception exception) when (IsInteropException(exception))
                {
                    LogInteropError(exception);
                }
            }
            var shutdownStartedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - shutdownStartedAt < 10.0f
                   && DMN_GetCompositionInitializationState()
                       != CompositionStopped)
            {
                yield return null;
            }
            var shutdownComplete =
                DMN_GetCompositionInitializationState()
                    == CompositionStopped;
            Debug.Log($"[DesktopMascotPixelHitTestDiagnostics] Shutdown complete: {shutdownComplete}");
            RestoreRunInBackground();
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                try
                {
                    DMN_StopCompositionPixelHitTestDiagnostics();
                    DMN_StopCompositionAlphaMaskDiagnostics();
                    DMN_RequestContinuousCompositionDiagnosticsStop();
                    DMN_RequestCompositionDiagnosticsShutdown();
                }
                catch (Exception exception) when (IsInteropException(exception))
                {
                    LogInteropError(exception);
                }
            }
            RestoreRunInBackground();
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
                $"[DesktopMascotPixelHitTestDiagnostics] runInBackground restored: {runInBackgroundRestored}");
        }

        private static bool PrerequisitesReady()
        {
            return DMN_GetCompositionInitializationState()
                       == CompositionMessageLoopRunning
                && DMN_IsDestinationTextureAvailable() != 0
                && DMN_WasReadbackValidationCompleted() != 0
                && DMN_DidReadbackExpectedOrientationMatch() != 0;
        }

        private static bool TimedOut(float startedAt)
        {
            return Time.realtimeSinceStartup - startedAt
                >= OverallTimeoutSeconds;
        }

        private static string StateName(int state)
        {
            return state switch
            {
                0 => "NotStarted",
                1 => "WaitingForComposition",
                2 => "WaitingForMask",
                3 => "ApplyingWindowStyle",
                4 => "ReadyForVisualTest",
                5 => "VisualTestRunning",
                6 => "Completed",
                7 => "RestoreRequested",
                8 => "Stopped",
                9 => "Failed",
                _ => $"Unknown({state})"
            };
        }

        private static string ResultName(int result)
        {
            return result switch
            {
                0 => "None",
                1 => "Transparent",
                2 => "Opaque",
                3 => "OutsideClient",
                4 => "MaskUnavailable",
                5 => "CoordinateConversionFailed",
                _ => $"Unknown({result})"
            };
        }

        private static bool Has(ulong value, ulong bit)
        {
            return (value & bit) != 0;
        }

        private static bool B(int value)
        {
            return value != 0;
        }

        private static string H(ulong value)
        {
            return $"0x{value:X16}";
        }

        private static string H(int value)
        {
            return $"0x{unchecked((uint)value):X8}";
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
                $"[DesktopMascotPixelHitTestDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError(
                $"[DesktopMascotPixelHitTestDiagnostics] {message}");
        }
#endif
    }
}
