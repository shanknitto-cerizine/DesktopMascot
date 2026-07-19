using UnityEngine;

namespace DesktopMascot
{
    public sealed class ClickThroughController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask mascotLayer;
        [SerializeField] private Win32Window window;

        private void Reset()
        {
            targetCamera = Camera.main;
            window = FindFirstObjectByType<Win32Window>();
            mascotLayer = LayerMask.GetMask("Mascot");
        }

        private void Update()
        {
            if (targetCamera == null || window == null) return;
            var mouse = Input.mousePosition;
            var inside = mouse.x >= 0 && mouse.y >= 0 && mouse.x < Screen.width && mouse.y < Screen.height;
            var hit = false;
            if (inside)
            {
                var ray = targetCamera.ScreenPointToRay(mouse);
                hit = Physics.Raycast(ray, 100f, mascotLayer, QueryTriggerInteraction.Collide);
            }
            window.SetClickThrough(!hit);
        }
    }
}
