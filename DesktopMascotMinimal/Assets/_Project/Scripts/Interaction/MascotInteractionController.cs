using System.Collections;
using DesktopMascot.Character;
using UnityEngine;

namespace DesktopMascot.Interaction
{
    /// <summary>
    /// マスコットに対するマウス入力を一元管理します。
    ///
    /// 短い操作はクリック、
    /// 一定距離以上の移動はドラッグとして判定します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotCharacter))]
    [RequireComponent(typeof(MascotExpressionController))]
    [RequireComponent(typeof(MascotDragController))]
    public sealed class MascotInteractionController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private MascotCharacter character;

        [SerializeField]
        private MascotExpressionController expressionController;

        [SerializeField]
        private MascotDragController dragController;

        [SerializeField]
        private Camera targetCamera;

        [Header("Pointer Detection")]

        [SerializeField]
        private LayerMask clickableLayers = ~0;

        [SerializeField]
        [Min(0.1f)]
        private float maximumRayDistance = 100.0f;

        [Tooltip("このピクセル数以上動いた場合、クリックではなくドラッグになります。")]
        [SerializeField]
        [Min(0.0f)]
        private float dragThresholdPixels = 8.0f;

        [Header("Click Reaction")]

        [SerializeField]
        [Min(0.0f)]
        private float happyDuration = 1.5f;

        [SerializeField]
        private bool outputDebugLog = true;

        private bool pointerPressedOnMascot;
        private bool dragStarted;

        private Vector2 pointerDownPosition;

        private Coroutine reactionCoroutine;

        private void Reset()
        {
            FindComponents();
        }

        private void Awake()
        {
            FindComponents();

            if (!ValidateComponents())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            HandlePointerInput();
        }

        private void OnDisable()
        {
            ResetPointerState();

            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
                reactionCoroutine = null;
            }

            if (expressionController != null)
            {
                expressionController.SetNeutral();
            }
        }

        [ContextMenu("Find Interaction Components")]
        public void FindComponents()
        {
            if (character == null)
            {
                character = GetComponent<MascotCharacter>();
            }

            if (expressionController == null)
            {
                expressionController =
                    GetComponent<MascotExpressionController>();
            }

            if (dragController == null)
            {
                dragController =
                    GetComponent<MascotDragController>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private bool ValidateComponents()
        {
            bool isValid = true;

            if (character == null)
            {
                Debug.LogError(
                    $"{nameof(MascotInteractionController)}: " +
                    "MascotCharacterが見つかりません。",
                    this);

                isValid = false;
            }

            if (expressionController == null)
            {
                Debug.LogError(
                    $"{nameof(MascotInteractionController)}: " +
                    "MascotExpressionControllerが見つかりません。",
                    this);

                isValid = false;
            }

            if (dragController == null)
            {
                Debug.LogError(
                    $"{nameof(MascotInteractionController)}: " +
                    "MascotDragControllerが見つかりません。",
                    this);

                isValid = false;
            }

            if (targetCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MascotInteractionController)}: " +
                    "入力判定に使用するCameraが見つかりません。",
                    this);

                isValid = false;
            }

            return isValid;
        }

        private void HandlePointerInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandlePointerDown();
            }

            if (Input.GetMouseButton(0))
            {
                HandlePointerHeld();
            }

            if (Input.GetMouseButtonUp(0))
            {
                HandlePointerUp();
            }
        }

        private void HandlePointerDown()
        {
            ResetPointerState();

            if (!TryRaycastMascot(out RaycastHit hit))
            {
                return;
            }

            pointerPressedOnMascot = true;
            pointerDownPosition = Input.mousePosition;

            if (outputDebugLog)
            {
                Debug.Log(
                    $"Mascot pointer down: {hit.collider.name}",
                    hit.collider);
            }
        }

        private void HandlePointerHeld()
        {
            if (!pointerPressedOnMascot)
            {
                return;
            }

            Vector2 currentPosition = Input.mousePosition;

            if (!dragStarted)
            {
                float movedDistance = Vector2.Distance(
                    pointerDownPosition,
                    currentPosition);

                if (movedDistance >= dragThresholdPixels)
                {
                    dragStarted = dragController.BeginDrag(
                        pointerDownPosition);

                    if (dragStarted && outputDebugLog)
                    {
                        Debug.Log(
                            "Mascot drag started.",
                            this);
                    }
                }
            }

            if (dragStarted)
            {
                dragController.UpdateDrag(currentPosition);
            }
        }

        private void HandlePointerUp()
        {
            if (!pointerPressedOnMascot)
            {
                ResetPointerState();
                return;
            }

            if (dragStarted)
            {
                dragController.UpdateDrag(Input.mousePosition);
                dragController.EndDrag();

                if (outputDebugLog)
                {
                    Debug.Log(
                        "Mascot drag ended.",
                        this);
                }
            }
            else
            {
                PlayClickReaction();
            }

            ResetPointerState();
        }

        private bool TryRaycastMascot(
            out RaycastHit mascotHit)
        {
            mascotHit = default;

            Ray ray = targetCamera.ScreenPointToRay(
                Input.mousePosition);

            bool hitSomething = Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumRayDistance,
                clickableLayers);

            if (!hitSomething)
            {
                return false;
            }

            MascotCharacter clickedCharacter =
                hit.collider.GetComponentInParent<MascotCharacter>();

            if (clickedCharacter != character)
            {
                return false;
            }

            mascotHit = hit;
            return true;
        }

        private void PlayClickReaction()
        {
            if (outputDebugLog)
            {
                Debug.Log(
                    "Mascot clicked.",
                    this);
            }

            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
            }

            reactionCoroutine =
                StartCoroutine(PlayClickReactionCoroutine());
        }

        private IEnumerator PlayClickReactionCoroutine()
        {
            expressionController.SetHappy();

            if (happyDuration > 0.0f)
            {
                yield return new WaitForSeconds(happyDuration);
            }

            expressionController.SetNeutral();
            reactionCoroutine = null;
        }

        private void ResetPointerState()
        {
            if (dragController != null &&
                dragController.IsDragging)
            {
                dragController.CancelDrag();
            }

            pointerPressedOnMascot = false;
            dragStarted = false;
            pointerDownPosition = Vector2.zero;
        }
    }
}