using System;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal interface ISettingsPlayerPresentationSource : IDisposable
    {
        int PresentationFrame { get; }
        int PresentationWidth { get; }
        int PresentationHeight { get; }
        int RenderTextureAllocationCount { get; }
        bool CleanupComplete { get; }
    }
}
