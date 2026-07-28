using System;
using System.Collections;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotNativeDragDiagnostics : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const string Prefix =
            "[DesktopMascotDragDiagnostics]";
        private const int DiagnosticMoved = 2;
        private const int DiagnosticCompleted = 4;
        private const int DiagnosticFailed = 5;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeMascotWindowDragEnabled();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_IsNativeMascotWindowAvailableForDrag();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsNativeMascotWindowDragging();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_StartNativeMascotWindowDragDiagnostic(
            int deltaX,
            int deltaY);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_CompleteNativeMascotWindowDragDiagnostic();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotDragDiagnosticState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotDragFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotDragStartCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotDragMoveCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetNativeMascotDragEndCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetNativeMascotDragCaptureAcquiredCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint
            DMN_GetNativeMascotDragCaptureReleasedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern ulong
            DMN_GetNativeMascotCompletedDragGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotDragLastWindowX();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetNativeMascotDragLastWindowY();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticMoveSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticSizeRemainUnchanged();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticRegionRemainApplied();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticPresentContinue();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticRegionPublicationContinue();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticAvoidCompositionRestart();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticPreserveZOrder();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticAvoidActivation();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticReleaseCapture();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int
            DMN_DidNativeMascotDragDiagnosticRestoreInitialPosition();

        private IEnumerator Start()
        {
            for (var frame = 0;
                 frame < 1200
                 && (!DesktopMascotNativeContextMenuDiagnostics.Passed
                     || !DesktopMascotSystemTrayDiagnostics.Passed);
                 ++frame)
            {
                yield return null;
            }
            if (!DesktopMascotNativeContextMenuDiagnostics.Passed
                || !DesktopMascotSystemTrayDiagnostics.Passed)
            {
                Log("Context-menu diagnostics passed: False");
                Log("System-tray diagnostics passed: False");
                LogFinal(false);
                RequestOrderlyQuit();
                yield break;
            }

            for (var frame = 0; frame < 900; ++frame)
            {
                if (DMN_IsNativeMascotWindowDragEnabled() != 0
                    && DMN_IsNativeMascotWindowAvailableForDrag() != 0
                    && DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount()
                        > 0)
                {
                    break;
                }
                yield return null;
            }

            var startResult =
                DMN_StartNativeMascotWindowDragDiagnostic(48, 32);
            Log($"Start result: {startResult}");
            if (startResult != 1)
            {
                LogFinal(false);
                RequestOrderlyQuit();
                yield break;
            }

            for (var frame = 0; frame < 300; ++frame)
            {
                var state = DMN_GetNativeMascotDragDiagnosticState();
                if (state == DiagnosticMoved || state == DiagnosticFailed)
                    break;
                yield return null;
            }
            if (DMN_GetNativeMascotDragDiagnosticState()
                != DiagnosticMoved)
            {
                LogFinal(false);
                RequestOrderlyQuit();
                yield break;
            }

            yield return new WaitForSecondsRealtime(3.0f);
            var completionResult =
                DMN_CompleteNativeMascotWindowDragDiagnostic();
            Log($"Completion request result: {completionResult}");
            for (var frame = 0; frame < 300; ++frame)
            {
                var state = DMN_GetNativeMascotDragDiagnosticState();
                if (state == DiagnosticCompleted
                    || state == DiagnosticFailed)
                {
                    break;
                }
                yield return null;
            }

            var passed =
                completionResult == 1
                && DMN_GetNativeMascotDragDiagnosticState()
                    == DiagnosticCompleted
                && DMN_GetNativeMascotDragFailureStage() == 0
                && B(DMN_DidNativeMascotDragDiagnosticMoveSucceed())
                && B(DMN_DidNativeMascotDragDiagnosticSizeRemainUnchanged())
                && B(DMN_DidNativeMascotDragDiagnosticRegionRemainApplied())
                && B(DMN_DidNativeMascotDragDiagnosticPresentContinue())
                && B(DMN_DidNativeMascotDragDiagnosticRegionPublicationContinue())
                && B(DMN_DidNativeMascotDragDiagnosticAvoidCompositionRestart())
                && B(DMN_DidNativeMascotDragDiagnosticPreserveZOrder())
                && B(DMN_DidNativeMascotDragDiagnosticAvoidActivation())
                && B(DMN_DidNativeMascotDragDiagnosticReleaseCapture())
                && B(DMN_DidNativeMascotDragDiagnosticRestoreInitialPosition())
                && !B(DMN_IsNativeMascotWindowDragging())
                && DesktopMascotWindowPositionPersistenceDiagnostics.Passed
                && DesktopMascotNativeContextMenuDiagnostics.Passed
                && DesktopMascotSystemTrayDiagnostics.Passed
                && DesktopMascotSettingsUiPresentationDiagnostics.Passed;
            LogFinal(passed);
            yield return null;
            RequestOrderlyQuit();
        }

        private static void LogFinal(bool passed)
        {
            Log($"State: {DMN_GetNativeMascotDragDiagnosticState()}");
            Log($"Failure stage: {DMN_GetNativeMascotDragFailureStage()}");
            Log($"Native mascot HWND available: {B(DMN_IsNativeMascotWindowAvailableForDrag())}");
            Log($"Initial window rectangle available: {B(DMN_DidNativeMascotDragDiagnosticMoveSucceed())}");
            Log($"Requested X/Y move succeeded: {B(DMN_DidNativeMascotDragDiagnosticMoveSucceed())}");
            Log($"Width and height unchanged: {B(DMN_DidNativeMascotDragDiagnosticSizeRemainUnchanged())}");
            Log($"Region remained applied: {B(DMN_DidNativeMascotDragDiagnosticRegionRemainApplied())}");
            Log($"Present continued during movement: {B(DMN_DidNativeMascotDragDiagnosticPresentContinue())}");
            Log($"Region publication continued during movement: {B(DMN_DidNativeMascotDragDiagnosticRegionPublicationContinue())}");
            Log($"Composition restart avoided: {B(DMN_DidNativeMascotDragDiagnosticAvoidCompositionRestart())}");
            Log($"Z-order preserved: {B(DMN_DidNativeMascotDragDiagnosticPreserveZOrder())}");
            Log($"Window activation avoided: {B(DMN_DidNativeMascotDragDiagnosticAvoidActivation())}");
            Log($"Capture released: {B(DMN_DidNativeMascotDragDiagnosticReleaseCapture())}");
            Log($"Initial position restored: {B(DMN_DidNativeMascotDragDiagnosticRestoreInitialPosition())}");
            Log($"Dragging after completion: {B(DMN_IsNativeMascotWindowDragging())}");
            Log($"Drag start/move/end counts: {DMN_GetNativeMascotDragStartCount()}/{DMN_GetNativeMascotDragMoveCount()}/{DMN_GetNativeMascotDragEndCount()}");
            Log($"Capture acquired/released counts: {DMN_GetNativeMascotDragCaptureAcquiredCount()}/{DMN_GetNativeMascotDragCaptureReleasedCount()}");
            Log(
                $"Completed drag generation: {DMN_GetNativeMascotCompletedDragGeneration()}");
            Log($"Last window X/Y: {DMN_GetNativeMascotDragLastWindowX()}/{DMN_GetNativeMascotDragLastWindowY()}");
            Log(
                $"Position persistence diagnostics passed: {DesktopMascotWindowPositionPersistenceDiagnostics.Passed}");
            Log(
                $"Settings UI presentation diagnostics passed: " +
                DesktopMascotSettingsUiPresentationDiagnostics.Passed);
            Log(
                $"Context-menu diagnostics passed: " +
                DesktopMascotNativeContextMenuDiagnostics.Passed);
            Log(
                $"System-tray diagnostics passed: " +
                DesktopMascotSystemTrayDiagnostics.Passed);
            Log($"Automated diagnostics passed: {passed}");
            Log("Visual verification pending: True");
        }

        private static bool B(int value) => value != 0;
        private static void RequestOrderlyQuit()
        {
            if (DesktopMascotSystemTrayDiagnostics.Passed)
            {
                var published = DesktopMascotSystemTrayDiagnostics
                    .PublishExitForOrderlyShutdown();
                Log($"System-tray exit command published: {published}");
                if (published)
                    return;
            }
            if (DesktopMascotNativeContextMenuDiagnostics.Passed)
            {
                var published = DesktopMascotNativeContextMenuDiagnostics
                    .PublishExitForOrderlyShutdown();
                Log($"Context-menu exit command published: {published}");
                if (published)
                    return;
            }
            var runtime =
                FindFirstObjectByType<DesktopMascotRuntimePipeline>();
            if (runtime != null)
                runtime.RequestOrderlyQuit("drag diagnostic complete");
            else
                Application.Quit();
        }

        private static void Log(string message) =>
            Debug.Log($"{Prefix} {message}");
#endif
    }
}
