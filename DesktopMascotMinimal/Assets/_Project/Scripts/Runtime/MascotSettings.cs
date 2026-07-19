using System;

namespace DesktopMascot
{
    [Serializable]
    public sealed class MascotSettings
    {
        public int windowX = 100;
        public int windowY = 100;
        public bool alwaysOnTop = true;
        public float modelScale = 1f;
    }
}
