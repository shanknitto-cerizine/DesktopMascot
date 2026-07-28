using System;
using UnityEngine;

namespace DesktopMascot.Runtime
{
    [Serializable]
    internal sealed class DesktopMascotRuntimeConfiguration
    {
        [Min(1)] public int textureWidth = 256;
        [Min(1)] public int textureHeight = 256;
        [Range(0, 255)] public int alphaThreshold = 128;
        [Min(250)] public int regionUpdateIntervalMilliseconds = 250;
        [Min(1)] public int targetPresentFps = 30;

        internal bool IsValid =>
            textureWidth == 256
            && textureHeight == 256
            && alphaThreshold == 128
            && regionUpdateIntervalMilliseconds >= 250
            && targetPresentFps > 0;
    }
}
