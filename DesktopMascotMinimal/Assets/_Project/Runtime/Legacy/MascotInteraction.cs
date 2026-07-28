using UnityEngine;

namespace DesktopMascot
{
    public sealed class MascotInteraction : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string reactionTrigger = "Reaction";
        [SerializeField] private Win32Window window;

        private Vector3 _mouseDown;
        private float _mouseDownAt;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            window = FindFirstObjectByType<Win32Window>();
        }

        private void OnMouseDown()
        {
            _mouseDown = Input.mousePosition;
            _mouseDownAt = Time.unscaledTime;
        }

        private void OnMouseUp()
        {
            var distance = Vector3.Distance(_mouseDown, Input.mousePosition);
            var elapsed = Time.unscaledTime - _mouseDownAt;
            if (distance < 8f && elapsed < 0.5f) PlayReaction();
        }

        private void OnMouseDrag()
        {
            if (Vector3.Distance(_mouseDown, Input.mousePosition) >= 8f)
            {
                window?.BeginNativeDrag();
            }
        }

        public void PlayReaction()
        {
            if (animator != null && !string.IsNullOrWhiteSpace(reactionTrigger))
                animator.SetTrigger(reactionTrigger);
            else
                StartCoroutine(Bounce());
        }

        private System.Collections.IEnumerator Bounce()
        {
            var original = transform.localScale;
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime * 4f)
            {
                transform.localScale = original * (1f + Mathf.Sin(t * Mathf.PI) * 0.12f);
                yield return null;
            }
            transform.localScale = original;
        }
    }
}
