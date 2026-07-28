using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotValidatedPreview
    {
        internal static bool UsesValidatedUvTransform => true;

        internal static void DrawValidatedTopLeftPreview(
            Rect destination,
            Texture texture)
        {
            // The RenderTexture, GPU readback, DirectComposition output, and
            // Window Region already use the validated orientation. Only the
            // IMGUI preview UV is flipped here. Do not move this correction
            // into a diagnostic shader.
            GUI.DrawTextureWithTexCoords(
                destination,
                texture,
                new Rect(0.0f, 1.0f, 1.0f, -1.0f),
                true);
        }
    }
}
