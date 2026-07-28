using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionWindowRegionDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int Threshold = 128;
        private const float VisualTestDurationSeconds = 30.0f;
        private const float OverallTimeoutSeconds = 75.0f;
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionFailed = 15;
        private const int CompositionStopped = 17;
        private const int ContinuousCompleted = 10;
        private const int ContinuousStopped = 12;
        private const int ContinuousFailed = 13;
        private const int AlphaCompleted = 6;
        private const int RegionVisualTestRunning = 4;
        private const int RegionStopped = 7;
        private const int RegionFailed = 8;
        private const ulong WsExLayered = 0x00080000;
        private const ulong WsExTransparent = 0x00000020;

        private bool previousRunInBackground;
        private bool runInBackgroundRestored;
        private bool visualTestRunning;
        private bool shutdownRequested;
        private float visualTestEndsAt;
        private int completedState;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionAlphaMaskState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionAlphaMaskFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionAlphaMaskSampleAlpha(
            int x,
            int y);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_StartCompositionWindowRegionDiagnostics(int threshold);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_CompleteCompositionWindowRegionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_StopCompositionWindowRegionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowRegionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_GetCompositionWindowRegionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowRegionThreshold();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsCompositionWindowRegionApplied();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowRegionType();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetCompositionWindowRegionRectangleCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetCompositionWindowRegionCoveredPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetCompositionWindowRegionExcludedPixelCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowInitialRegionType();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidCompositionWindowInitialRegionRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsCompositionWindowRegionApplyRequestPending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidCompositionWindowRegionLastApplySucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetCompositionWindowRegionLastWin32Error();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetCompositionWindowRegionPublishedGeneration();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsCompositionWindowRegionTopLeftInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsCompositionWindowRegionTopRightInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsCompositionWindowRegionBottomLeftInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_IsCompositionWindowRegionBottomRightInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_IsCompositionWindowRegionCenterInside();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetCompositionWindowRegionInitialExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetCompositionWindowRegionDiagnosticExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetCompositionWindowRegionCurrentExtendedStyle();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidCompositionWindowRegionStyleRestoreSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowInitialX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetCompositionWindowInitialY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_GetContinuousCompositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int
            DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int DMN_RequestCompositionDiagnosticsShutdown();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionWindowRegionDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionWindowRegionDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private void Awake()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Previous runInBackground: {previousRunInBackground}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Diagnostic runInBackground: {Application.runInBackground}");
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
                || !ValidateRepresentativeAlpha())
            {
                LogError("Published alpha mask validation failed.");
                yield return ShutdownAndQuit();
                yield break;
            }

            var startResult =
                DMN_StartCompositionWindowRegionDiagnostics(Threshold);
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Start result: {startResult}");
            if (startResult != 1)
            {
                yield return ShutdownAndQuit();
                yield break;
            }
            while (!TimedOut(startedAt))
            {
                var state = DMN_GetCompositionWindowRegionState();
                if (state == RegionVisualTestRunning
                    || state == RegionFailed)
                {
                    break;
                }
                yield return null;
            }
            if (DMN_GetCompositionWindowRegionState()
                != RegionVisualTestRunning)
            {
                LogError("Window region did not become ready.");
                yield return ShutdownAndQuit();
                yield break;
            }

            LogRegionState(false);
            visualTestEndsAt =
                Time.realtimeSinceStartup + VisualTestDurationSeconds;
            visualTestRunning = true;
            while (Time.realtimeSinceStartup < visualTestEndsAt
                   && !TimedOut(startedAt))
            {
                if (DMN_GetCompositionWindowRegionState() == RegionFailed
                    || DMN_GetContinuousCompositionState()
                        == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            visualTestRunning = false;
            if (DMN_GetCompositionWindowRegionState() != RegionFailed)
            {
                DMN_CompleteCompositionWindowRegionDiagnostics();
            }
            completedState = DMN_GetCompositionWindowRegionState();
            LogRegionState(false);

            var stopResult =
                DMN_StopCompositionWindowRegionDiagnostics();
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Stop result: {stopResult}");
            while (!TimedOut(startedAt)
                   && DMN_GetCompositionWindowRegionState() != RegionStopped
                   && DMN_GetCompositionWindowRegionState() != RegionFailed)
            {
                yield return null;
            }
            LogRegionState(true);

            DMN_StopCompositionAlphaMaskDiagnostics();
            while (!TimedOut(startedAt))
            {
                var state = DMN_GetContinuousCompositionState();
                if (state == ContinuousCompleted
                    || state == ContinuousStopped
                    || state == ContinuousFailed)
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
            DrawGuide(
                new Rect(x, y, half, half),
                new Color(0.35f, 0.08f, 0.08f, 1.0f),
                "TOP-LEFT\nalpha 0\nOUTSIDE REGION");
            DrawGuide(
                new Rect(x + half, y, half, half),
                new Color(0.08f, 0.35f, 0.08f, 1.0f),
                "TOP-RIGHT\nalpha 64\nOUTSIDE REGION");
            DrawGuide(
                new Rect(x, y + half, half, half),
                new Color(0.08f, 0.08f, 0.45f, 1.0f),
                "BOTTOM-LEFT\nalpha 128\nINSIDE REGION");
            DrawGuide(
                new Rect(x + half, y + half, half, half),
                new Color(0.35f, 0.35f, 0.35f, 1.0f),
                "BOTTOM-RIGHT\nalpha 255\nINSIDE REGION");
            DrawGuide(
                new Rect(
                    x + 24.0f * scale,
                    y + 24.0f * scale,
                    16.0f * scale,
                    16.0f * scale),
                new Color(0.5f, 0.18f, 0.5f, 1.0f),
                "CENTER\n192\nINSIDE");
            GUI.color = Color.white;
            var remaining = Math.Max(
                0,
                Mathf.CeilToInt(
                    visualTestEndsAt - Time.realtimeSinceStartup));
            GUI.Label(
                new Rect(16, 12, Screen.width - 32, 52),
                $"Window Region Diagnostics - {remaining}s remaining\n"
                + $"Click the actual 64x64 composition window at "
                + $"({DMN_GetCompositionWindowInitialX()}, "
                + $"{DMN_GetCompositionWindowInitialY()}); "
                + "this 8x view is only a guide.");
        }

        private static void DrawGuide(
            Rect rectangle,
            Color color,
            string label)
        {
            GUI.color = color;
            GUI.Box(rectangle, GUIContent.none);
            GUI.color = Color.white;
            GUI.Box(rectangle, label);
        }

        private static bool ValidateRepresentativeAlpha()
        {
            var topLeft = DMN_GetCompositionAlphaMaskSampleAlpha(8, 8);
            var topRight = DMN_GetCompositionAlphaMaskSampleAlpha(55, 8);
            var bottomLeft =
                DMN_GetCompositionAlphaMaskSampleAlpha(8, 55);
            var bottomRight =
                DMN_GetCompositionAlphaMaskSampleAlpha(55, 55);
            var center = DMN_GetCompositionAlphaMaskSampleAlpha(32, 32);
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Top-left alpha: {topLeft}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Top-right alpha: {topRight}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Bottom-left alpha: {bottomLeft}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Bottom-right alpha: {bottomRight}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Center alpha: {center}");
            return topLeft == 0
                && topRight == 64
                && bottomLeft == 128
                && bottomRight == 255
                && center == 192;
        }

        private void LogRegionState(bool afterRestore)
        {
            var diagnosticStyle =
                DMN_GetCompositionWindowRegionDiagnosticExtendedStyle();
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] State: {StateName(afterRestore ? completedState : DMN_GetCompositionWindowRegionState())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Failure stage: {DMN_GetCompositionWindowRegionFailureStage()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Threshold: {DMN_GetCompositionWindowRegionThreshold()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Region applied: {B(DMN_IsCompositionWindowRegionApplied())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Region type: {RegionTypeName(DMN_GetCompositionWindowRegionType())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Region rectangle count: {DMN_GetCompositionWindowRegionRectangleCount()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Region covered pixel count: {DMN_GetCompositionWindowRegionCoveredPixelCount()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Region excluded pixel count: {DMN_GetCompositionWindowRegionExcludedPixelCount()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Initial region type: {RegionTypeName(DMN_GetCompositionWindowInitialRegionType())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Top-left in region: {B(DMN_IsCompositionWindowRegionTopLeftInside())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Top-right in region: {B(DMN_IsCompositionWindowRegionTopRightInside())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Bottom-left in region: {B(DMN_IsCompositionWindowRegionBottomLeftInside())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Bottom-right in region: {B(DMN_IsCompositionWindowRegionBottomRightInside())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Center in region: {B(DMN_IsCompositionWindowRegionCenterInside())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Apply request pending: {B(DMN_IsCompositionWindowRegionApplyRequestPending())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Last apply succeeded: {B(DMN_DidCompositionWindowRegionLastApplySucceed())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Last Win32 error: {DMN_GetCompositionWindowRegionLastWin32Error()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Published generation: {DMN_GetCompositionWindowRegionPublishedGeneration()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Initial extended style: {H(DMN_GetCompositionWindowRegionInitialExtendedStyle())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Diagnostic extended style: {H(diagnosticStyle)}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] WS_EX_LAYERED active: {Has(diagnosticStyle, WsExLayered)}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] WS_EX_TRANSPARENT active: {Has(diagnosticStyle, WsExTransparent)}");
            if (afterRestore)
            {
                Debug.Log(
                    $"[DesktopMascotWindowRegionDiagnostics] Initial region restored: {B(DMN_DidCompositionWindowInitialRegionRestoreSucceed())}");
                Debug.Log(
                    $"[DesktopMascotWindowRegionDiagnostics] Final extended style: {H(DMN_GetCompositionWindowRegionCurrentExtendedStyle())}");
                Debug.Log(
                    $"[DesktopMascotWindowRegionDiagnostics] Initial extended style restored: {B(DMN_DidCompositionWindowRegionStyleRestoreSucceed())}");
            }
        }

        private static void LogContinuousState()
        {
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Continuous state: {DMN_GetContinuousCompositionState()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Continuous failure stage: {DMN_GetContinuousCompositionFailureStage()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Last Present HRESULT: {H(DMN_GetContinuousCompositionLastPresentHRESULT())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Device removed reason: {H(DMN_GetContinuousCompositionLastDeviceRemovedReason())}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Continuous Present successful: {B(DMN_DidContinuousCompositionCompleteNormally())}");
        }

        private IEnumerator ShutdownAndQuit()
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                visualTestRunning = false;
                try
                {
                    DMN_StopCompositionWindowRegionDiagnostics();
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
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] Shutdown complete: {shutdownComplete}");
            Debug.Log(
                $"[DesktopMascotWindowRegionDiagnostics] UI thread stopped: {shutdownComplete}");
            RestoreRunInBackground();
            yield return null;
            yield return new WaitForEndOfFrame();
            Application.Quit();
        }

        private void OnApplicationQuit()
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                try
                {
                    DMN_StopCompositionWindowRegionDiagnostics();
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
                $"[DesktopMascotWindowRegionDiagnostics] runInBackground restored: {runInBackgroundRestored}");
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
                3 => "ApplyRequested",
                4 => "VisualTestRunning",
                5 => "Completed",
                6 => "RestoreRequested",
                7 => "Stopped",
                8 => "Failed",
                _ => $"Unknown({state})"
            };
        }

        private static string RegionTypeName(int type)
        {
            return type switch
            {
                0 => "ERROR",
                1 => "NULLREGION",
                2 => "SIMPLEREGION",
                3 => "COMPLEXREGION",
                _ => $"Unknown({type})"
            };
        }

        private static bool Has(ulong value, ulong flag)
        {
            return (value & flag) != 0;
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
                $"[DesktopMascotWindowRegionDiagnostics] Native interop failed ({exception.GetType().Name}): {exception.Message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError(
                $"[DesktopMascotWindowRegionDiagnostics] {message}");
        }
#endif
    }
}
