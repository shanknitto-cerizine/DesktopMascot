using System;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class NativeMascotCommandDispatcher
    {
        internal const int OpenSettingsCommand = 1;
        internal const int RequestExitCommand = 2;
        internal const int MascotContextMenuSource = 1;
        internal const int SystemTraySource = 2;

        private readonly SettingsWindowController settingsWindow;
        private readonly Func<bool> makePlayerInteractive;
        private readonly Action<string> requestOrderlyQuit;

        internal NativeMascotCommandDispatcher(
            SettingsWindowController controller,
            Func<bool> playerActivation,
            Action<string> orderlyQuit)
        {
            settingsWindow = controller;
            makePlayerInteractive = playerActivation;
            requestOrderlyQuit = orderlyQuit;
        }

        internal int OpenSettingsDispatchCount { get; private set; }
        internal int ExitDispatchCount { get; private set; }
        internal bool HasOrderlyQuitTarget => requestOrderlyQuit != null;

        internal bool Dispatch(
            int command,
            int source = MascotContextMenuSource)
        {
            switch (command)
            {
                case OpenSettingsCommand:
                    if (settingsWindow == null)
                        return false;
                    makePlayerInteractive?.Invoke();
                    if (!settingsWindow.IsOpen)
                        settingsWindow.Open();
                    OpenSettingsDispatchCount++;
                    return settingsWindow.IsOpen;
                case RequestExitCommand:
                    if (requestOrderlyQuit == null)
                        return false;
                    ExitDispatchCount++;
                    requestOrderlyQuit(
                        source == SystemTraySource
                            ? "system tray exit selected"
                            : "native mascot context-menu exit selected");
                    return true;
                default:
                    return false;
            }
        }
    }
}
