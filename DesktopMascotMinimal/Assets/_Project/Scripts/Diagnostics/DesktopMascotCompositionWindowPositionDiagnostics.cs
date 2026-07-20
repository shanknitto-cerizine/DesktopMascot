using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotCompositionWindowPositionDiagnostics :
        MonoBehaviour
    {
        private const string PluginName = "DesktopMascotNative";
        private const int CompositionMessageLoopRunning = 14;
        private const int CompositionFailed = 15;
        private const int CompositionStopped = 17;
        private const int PositionVerified = 6;
        private const int PositionCompleted = 7;
        private const int PositionFailed = 10;
        private const int ContinuousCompleted = 10;
        private const int ContinuousFailed = 13;
        private const int Margin = 32;
        private bool shutdownRequested;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionInitializationState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_StartCompositionWindowPositionDiagnostics();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionWindowPosition(int x, int y);
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowPositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowPositionFailureStage();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowRequestedX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowRequestedY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowActualX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowActualY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowMoveRequestCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowMoveAppliedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowMoveRejectedCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetCompositionWindowLastSetWindowPosLastError();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_IsCompositionWindowMovePending();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_DidCompositionWindowLastMoveSucceed();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWorkAreaLeft();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWorkAreaTop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWorkAreaRight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWorkAreaBottom();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialX();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialY();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialWidth();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetCompositionWindowInitialHeight();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_GetContinuousCompositionState();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)] private static extern int DMN_RequestCompositionDiagnosticsShutdown();
#endif

        private static void StartDiagnostics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var diagnosticsObject = new GameObject(
                nameof(DesktopMascotCompositionWindowPositionDiagnostics));
            DontDestroyOnLoad(diagnosticsObject);
            diagnosticsObject.AddComponent<
                DesktopMascotCompositionWindowPositionDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            yield return null;
            var prerequisitesReady = false;
            for (var frame = 0; frame < 600; frame++)
            {
                if (!TryGetPrerequisites(out prerequisitesReady))
                {
                    Cleanup();
                    yield break;
                }
                if (prerequisitesReady)
                {
                    break;
                }
                yield return null;
            }
            if (!prerequisitesReady)
            {
                Debug.LogError(
                    "[DesktopMascotWindowPositionDiagnostics] Prerequisites did not become ready.");
                RequestShutdown();
                Cleanup();
                yield break;
            }

            int startResult;
            try
            {
                startResult =
                    DMN_StartCompositionWindowPositionDiagnostics();
                Debug.Log(
                    $"[DesktopMascotWindowPositionDiagnostics] Start result: {startResult}");
                Debug.Log(
                    $"[DesktopMascotWindowPositionDiagnostics] Work area: left={DMN_GetCompositionWorkAreaLeft()}, top={DMN_GetCompositionWorkAreaTop()}, right={DMN_GetCompositionWorkAreaRight()}, bottom={DMN_GetCompositionWorkAreaBottom()}");
                Debug.Log(
                    $"[DesktopMascotWindowPositionDiagnostics] Initial position: x={DMN_GetCompositionWindowInitialX()}, y={DMN_GetCompositionWindowInitialY()}");
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                RequestShutdown();
                Cleanup();
                yield break;
            }
            if (startResult != 1)
            {
                RequestShutdown();
                Cleanup();
                yield break;
            }

            var continuousObject = new GameObject(
                nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuousObject);
            continuousObject.AddComponent<
                DesktopMascotContinuousCompositionDiagnostics>();

            var displayStarted = false;
            for (var frame = 0; frame < 600; frame++)
            {
                var continuousState = DMN_GetContinuousCompositionState();
                if (DMN_GetContinuousCompositionPresentCount() > 0)
                {
                    displayStarted = true;
                    break;
                }
                if (continuousState == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            if (!displayStarted)
            {
                Debug.LogError(
                    "[DesktopMascotWindowPositionDiagnostics] Continuous display did not start.");
                RequestShutdown();
                Cleanup();
                yield break;
            }

            var left = DMN_GetCompositionWorkAreaLeft();
            var top = DMN_GetCompositionWorkAreaTop();
            var right = DMN_GetCompositionWorkAreaRight();
            var bottom = DMN_GetCompositionWorkAreaBottom();
            var width = DMN_GetCompositionWindowInitialWidth();
            var height = DMN_GetCompositionWindowInitialHeight();
            var initialX = DMN_GetCompositionWindowInitialX();
            var initialY = DMN_GetCompositionWindowInitialY();
            var positions = new[]
            {
                new Vector2Int(left + Margin, top + Margin),
                new Vector2Int(
                    left + (right - left - width) / 2,
                    top + (bottom - top - height) / 2),
                new Vector2Int(
                    right - width - Margin,
                    bottom - height - Margin),
                new Vector2Int(initialX, initialY)
            };

            var moveSucceeded = true;
            for (var index = 0; index < positions.Length; index++)
            {
                yield return MoveAndVerify(index + 1, positions[index]);
                var state = DMN_GetCompositionWindowPositionState();
                if (state == PositionFailed)
                {
                    moveSucceeded = false;
                    break;
                }
                yield return new WaitForSecondsRealtime(1.0f);
            }

            var completedState = DMN_GetCompositionWindowPositionState();
            var failureStage =
                DMN_GetCompositionWindowPositionFailureStage();
            var requestCount = DMN_GetCompositionWindowMoveRequestCount();
            var appliedCount = DMN_GetCompositionWindowMoveAppliedCount();
            var rejectedCount = DMN_GetCompositionWindowMoveRejectedCount();
            var pending = DMN_IsCompositionWindowMovePending();
            var lastSucceeded =
                DMN_DidCompositionWindowLastMoveSucceed();
            if (!moveSucceeded || completedState != PositionCompleted)
            {
                RequestShutdown();
            }

            for (var frame = 0; frame < 900; frame++)
            {
                var continuousState = DMN_GetContinuousCompositionState();
                if (continuousState == ContinuousCompleted
                    || continuousState == ContinuousFailed)
                {
                    break;
                }
                yield return null;
            }
            for (var frame = 0; frame < 900; frame++)
            {
                if (DMN_GetCompositionInitializationState()
                    == CompositionStopped)
                {
                    break;
                }
                yield return null;
            }
            var shutdownComplete =
                DMN_GetCompositionInitializationState() == CompositionStopped;

            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] State: {StateName(completedState)}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Failure stage: {failureStage}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Move requests: {requestCount}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Moves applied: {appliedCount}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Moves rejected: {rejectedCount}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Move pending: {B(pending)}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Last move succeeded: {B(lastSucceeded)}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Last SetWindowPos LastError: {DMN_GetCompositionWindowLastSetWindowPosLastError()}");
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Shutdown complete: {shutdownComplete}");
            Cleanup();
        }

        private static IEnumerator MoveAndVerify(
            int moveNumber,
            Vector2Int position)
        {
            if (DMN_RequestCompositionWindowPosition(
                    position.x,
                    position.y)
                != 1)
            {
                Debug.LogError(
                    $"[DesktopMascotWindowPositionDiagnostics] Move {moveNumber}/4 request was rejected.");
                yield break;
            }
            for (var frame = 0; frame < 300; frame++)
            {
                var state = DMN_GetCompositionWindowPositionState();
                if (state == PositionVerified
                    || state == PositionCompleted
                    || state == PositionFailed)
                {
                    break;
                }
                yield return null;
            }
            var requestedX = DMN_GetCompositionWindowRequestedX();
            var requestedY = DMN_GetCompositionWindowRequestedY();
            var actualX = DMN_GetCompositionWindowActualX();
            var actualY = DMN_GetCompositionWindowActualY();
            Debug.Log(
                $"[DesktopMascotWindowPositionDiagnostics] Move {moveNumber}/4 completed: requested=({requestedX},{requestedY}), actual=({actualX},{actualY}), delta=({actualX - requestedX},{actualY - requestedY})");
        }

        private static bool TryGetPrerequisites(out bool ready)
        {
            ready = false;
            try
            {
                var compositionState =
                    DMN_GetCompositionInitializationState();
                ready =
                    compositionState == CompositionMessageLoopRunning
                    && DMN_IsDestinationTextureAvailable() != 0
                    && DMN_WasReadbackValidationCompleted() != 0
                    && DMN_DidReadbackExpectedOrientationMatch() != 0;
                if (compositionState == CompositionFailed)
                {
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (IsInteropException(exception))
            {
                LogInteropError(exception);
                return false;
            }
        }

        private void RequestShutdown()
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
            RequestShutdown();
        }

        private void Cleanup()
        {
            Destroy(gameObject);
        }

        private static string StateName(int state)
        {
            return state switch
            {
                0 => "NotStarted",
                1 => "Ready",
                2 => "MoveRequested",
                3 => "MoveMessagePosted",
                4 => "MoveMessageReceived",
                5 => "SetWindowPosApplied",
                6 => "PositionVerified",
                7 => "Completed",
                8 => "ShutdownRequested",
                9 => "Stopped",
                10 => "Failed",
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
                $"[DesktopMascotWindowPositionDiagnostics] Native interop failed: {exception.Message}");
        }

        private static bool B(int value)
        {
            return value != 0;
        }
#endif
    }
}
