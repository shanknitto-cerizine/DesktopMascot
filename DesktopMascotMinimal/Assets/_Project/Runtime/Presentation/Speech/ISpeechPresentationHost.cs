using System;
using UnityEngine;

namespace DesktopMascot.Runtime.Presentation.Speech
{
    internal interface ISpeechPresentationHost
    {
        bool Initialize();
        bool IsReady { get; }
        bool Show(uint generation, Vector2Int anchor);
        bool Hide(uint generation);
        bool UpdateAnchor(Vector2Int anchor);
        uint ConsumeClickGeneration();
        void Poll();
        void IssueRenderEvent(IntPtr nativeTexture);
        bool BeginShutdown();
        bool Cleanup();
        uint PresentCount { get; }
        int PresentHResult { get; }
        int DeviceRemovedHResult { get; }
        int FailureStage { get; }
    }
}
