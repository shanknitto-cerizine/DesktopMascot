using System;
using System.Runtime.InteropServices;
using DesktopMascot.Runtime.Presentation.Speech;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Runtime.Platform.Windows.Speech
{
    internal sealed class WindowsSpeechPresentationHost : ISpeechPresentationHost
    {
        private const string Dll = "DesktopMascotNative";
        private const int SpeechRenderEvent = 9;
        private IntPtr renderEvent;
        private bool shutdown;
        private CommandBuffer commandBuffer;
        private bool initializeRequestAccepted;

        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr DMN_GetRenderEventAndDataFunc();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_InitializeSpeechPresentation();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_ShowSpeechPresentation(uint generation, int x, int y);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_HideSpeechPresentation(uint generation);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_UpdateSpeechPresentationAnchor(int x, int y);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern void DMN_PollSpeechPresentation();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_ConsumeSpeechClickGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_BeginSpeechPresentationShutdown();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_IsSpeechPresentationReady();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern uint DMN_GetSpeechPresentCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetSpeechPresentHRESULT();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetSpeechDeviceRemovedHRESULT();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        private static extern int DMN_GetSpeechFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechWindowCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechWindowDestroyedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechRegionCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechRegionTransferredCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechRegionCallerDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechFollowUpdateCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechEdgeFlipCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechClampCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechDpi();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern int DMN_DidSpeechOwnerMatchMascot();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern int DMN_DidSpeechCleanupSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechContextAvailabilityMask();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechInitializeRequestCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechInitializePostSuccessCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechInitializeHandleCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechRegionLiveOwnedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechCompositionTargetCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechVisualCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechSwapChainCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechMaximumFollowErrorPixels();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)]
        internal static extern uint DMN_GetSpeechLiveResourceCount();

        public bool Initialize()
        {
            renderEvent = DMN_GetRenderEventAndDataFunc();
            commandBuffer = new CommandBuffer
            {
                name = "Desktop Mascot Speech Presentation"
            };
            initializeRequestAccepted =
                DMN_InitializeSpeechPresentation() != 0;
            return renderEvent != IntPtr.Zero;
        }
        public bool IsReady => !shutdown && DMN_IsSpeechPresentationReady() != 0;
        public bool Show(uint generation, Vector2Int anchor) =>
            !shutdown && DMN_ShowSpeechPresentation(generation, anchor.x, anchor.y) != 0;
        public bool Hide(uint generation) =>
            DMN_HideSpeechPresentation(generation) != 0;
        public bool UpdateAnchor(Vector2Int anchor) =>
            !shutdown && DMN_UpdateSpeechPresentationAnchor(anchor.x, anchor.y) != 0;
        public uint ConsumeClickGeneration() =>
            shutdown ? 0 : DMN_ConsumeSpeechClickGeneration();
        public void Poll()
        {
            if (shutdown) return;
            if (!IsReady)
                initializeRequestAccepted =
                    DMN_InitializeSpeechPresentation() != 0
                    || initializeRequestAccepted;
            DMN_PollSpeechPresentation();
        }
        public void IssueRenderEvent(IntPtr nativeTexture)
        {
            if (!shutdown && renderEvent != IntPtr.Zero && nativeTexture != IntPtr.Zero)
            {
                commandBuffer.Clear();
                commandBuffer.IssuePluginEventAndData(
                    renderEvent,
                    SpeechRenderEvent,
                    nativeTexture);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }
        }
        public bool BeginShutdown()
        {
            if (shutdown) return true;
            shutdown = true;
            return DMN_BeginSpeechPresentationShutdown() != 0;
        }
        public bool Cleanup()
        {
            commandBuffer?.Release();
            commandBuffer = null;
            renderEvent = IntPtr.Zero;
            var nativeCleanup = DMN_DidSpeechCleanupSucceed() != 0;
            Debug.Log(
                "[DesktopMascotSpeech] Native cleanup/live resources/" +
                "window destroyed/HRGN live-owned: " +
                $"{nativeCleanup}/{DMN_GetSpeechLiveResourceCount()}/" +
                $"{DMN_GetSpeechWindowDestroyedCount()}/" +
                DMN_GetSpeechRegionLiveOwnedCount());
            return nativeCleanup
                && DMN_GetSpeechLiveResourceCount() == 0
                && DMN_GetSpeechRegionLiveOwnedCount() == 0;
        }
        public uint PresentCount => DMN_GetSpeechPresentCount();
        public int PresentHResult => DMN_GetSpeechPresentHRESULT();
        public int DeviceRemovedHResult => DMN_GetSpeechDeviceRemovedHRESULT();
        public int FailureStage => DMN_GetSpeechFailureStage();
    }
}
