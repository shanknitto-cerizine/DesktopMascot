namespace DesktopMascot.Runtime.Settings.UI
{
    internal interface ISettingsPresentationHost
    {
        bool IsInitialized { get; }
        bool IsSettingsVisible { get; }
        int PresentationFailureStage { get; }
        int PresentationRequestCount { get; }
        int PresentationVisibleCount { get; }
        int PresentationClosedCount { get; }

        bool ShowSettings();
        bool BringSettingsToFront();
        bool CloseSettings();
        bool ApplyPresentationState(bool settingsVisible);
    }
}
