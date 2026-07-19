using DesktopMascot.Character;
using UnityEngine;

namespace DesktopMascot.Interaction
{
    /// <summary>
    /// マスコットのドラッグ移動を担当します。
    ///
    /// 現在はUnity内でモデルのルート位置を移動します。
    /// 将来的にはWindowsウィンドウの移動処理へ差し替えます。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotCharacter))]
    public sealed class MascotDragController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private MascotCharacter character;

        [SerializeField]
        private Camera targetCamera;

        [Header("Movement")]

        [SerializeField]
        private bool allowHorizontalMovement = true;

        [SerializeField]
        private bool allowVerticalMovement = true;

        [SerializeField]
        [Min(0.0f)]
        private float movementSmoothTime = 0.02f;

        [Header("Screen Limits")]

        [Tooltip("モデルのルート位置を画面内へ制限します。")]
        [SerializeField]
        private bool keepInsideScreen = true;

        [Tooltip("画面端から確保する余白です。ピクセル単位です。")]
        [SerializeField]
        [Min(0.0f)]
        private float screenPadding = 40.0f;

        [Header("Optional World Position Limits")]

        [SerializeField]
        private bool limitWorldPosition;

        [SerializeField]
        private Vector2 horizontalLimits =
            new Vector2(-5.0f, 5.0f);

        [SerializeField]
        private Vector2 verticalLimits =
            new Vector2(-1.0f, 5.0f);

        private Plane dragPlane;
        private Vector3 dragOffset;
        private Vector3 dragTargetPosition;
        private Vector3 movementVelocity;

        private bool isDragging;

        public bool IsDragging => isDragging;

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
            if (!isDragging)
            {
                return;
            }

            MoveTowardsTarget();
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        [ContextMenu("Find Drag Components")]
        public void FindComponents()
        {
            if (character == null)
            {
                character = GetComponent<MascotCharacter>();
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
                    $"{nameof(MascotDragController)}: " +
                    "MascotCharacterが見つかりません。",
                    this);

                isValid = false;
            }

            if (targetCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MascotDragController)}: " +
                    "ドラッグに使用するCameraが見つかりません。",
                    this);

                isValid = false;
            }

            return isValid;
        }

        public bool BeginDrag(Vector2 screenPosition)
        {
            if (!enabled || targetCamera == null)
            {
                return false;
            }

            dragPlane = new Plane(
                -targetCamera.transform.forward,
                transform.position);

            if (!TryGetPointOnDragPlane(
                    screenPosition,
                    out Vector3 pointerWorldPosition))
            {
                return false;
            }

            dragOffset =
                transform.position - pointerWorldPosition;

            dragTargetPosition = transform.position;
            movementVelocity = Vector3.zero;
            isDragging = true;

            return true;
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!isDragging)
            {
                return;
            }

            if (!TryGetPointOnDragPlane(
                    screenPosition,
                    out Vector3 pointerWorldPosition))
            {
                return;
            }

            Vector3 requestedPosition =
                pointerWorldPosition + dragOffset;

            dragTargetPosition =
                ApplyMovementRestrictions(requestedPosition);
        }

        public void EndDrag()
        {
            if (!isDragging)
            {
                return;
            }

            transform.position = dragTargetPosition;
            movementVelocity = Vector3.zero;
            isDragging = false;
        }

        public void CancelDrag()
        {
            isDragging = false;
            movementVelocity = Vector3.zero;
        }

        private bool TryGetPointOnDragPlane(
            Vector2 screenPosition,
            out Vector3 worldPosition)
        {
            Ray ray =
                targetCamera.ScreenPointToRay(screenPosition);

            if (dragPlane.Raycast(ray, out float distance))
            {
                worldPosition = ray.GetPoint(distance);
                return true;
            }

            worldPosition = default;
            return false;
        }

        private void MoveTowardsTarget()
        {
            if (movementSmoothTime <= 0.0f)
            {
                transform.position = dragTargetPosition;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                dragTargetPosition,
                ref movementVelocity,
                movementSmoothTime);
        }

        private Vector3 ApplyMovementRestrictions(
            Vector3 requestedPosition)
        {
            Vector3 currentPosition = transform.position;
            Vector3 result = requestedPosition;

            if (!allowHorizontalMovement)
            {
                result.x = currentPosition.x;
            }

            if (!allowVerticalMovement)
            {
                result.y = currentPosition.y;
            }

            /*
             * ドラッグ平面上で移動しているため、
             * カメラ方向の奥行きは基本的に変化しません。
             */

            if (keepInsideScreen)
            {
                result = ClampOriginInsideScreen(result);
            }

            if (limitWorldPosition)
            {
                result.x = Mathf.Clamp(
                    result.x,
                    horizontalLimits.x,
                    horizontalLimits.y);

                result.y = Mathf.Clamp(
                    result.y,
                    verticalLimits.x,
                    verticalLimits.y);
            }

            return result;
        }

        /// <summary>
        /// モデルのルート座標を画面内へ収めます。
        ///
        /// これはUnity内での暫定処理です。
        /// 将来はWindowsウィンドウ座標による制限へ移行します。
        /// </summary>
        private Vector3 ClampOriginInsideScreen(
            Vector3 worldPosition)
        {
            Vector3 screenPosition =
                targetCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z <= 0.0f)
            {
                return worldPosition;
            }

            Rect cameraRect = targetCamera.pixelRect;

            float paddingX = Mathf.Min(
                screenPadding,
                cameraRect.width * 0.5f);

            float paddingY = Mathf.Min(
                screenPadding,
                cameraRect.height * 0.5f);

            screenPosition.x = Mathf.Clamp(
                screenPosition.x,
                cameraRect.xMin + paddingX,
                cameraRect.xMax - paddingX);

            screenPosition.y = Mathf.Clamp(
                screenPosition.y,
                cameraRect.yMin + paddingY,
                cameraRect.yMax - paddingY);

            return targetCamera.ScreenToWorldPoint(
                screenPosition);
        }
    }
}