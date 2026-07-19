using DesktopMascot.Character;
using UnityEngine;

namespace DesktopMascot.Interaction
{
    /// <summary>
    /// マスコットのドラッグ移動を担当します。
    ///
    /// マウス入力の判定自体はMascotInteractionControllerが担当し、
    /// このクラスは移動処理だけを受け持ちます。
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

        [Tooltip("横方向への移動を許可します。")]
        [SerializeField]
        private bool allowHorizontalMovement = true;

        [Tooltip("縦方向への移動を許可します。")]
        [SerializeField]
        private bool allowVerticalMovement = true;

        [Tooltip("奥行き方向への移動を許可します。")]
        [SerializeField]
        private bool allowDepthMovement;

        [Tooltip("ドラッグ位置へ追従する速さです。0なら即座に追従します。")]
        [SerializeField]
        [Min(0.0f)]
        private float movementSmoothTime = 0.02f;

        [Header("Optional Position Limits")]

        [SerializeField]
        private bool limitPosition;

        [SerializeField]
        private Vector2 horizontalLimits = new Vector2(-5.0f, 5.0f);

        [SerializeField]
        private Vector2 verticalLimits = new Vector2(-1.0f, 5.0f);

        private Plane dragPlane;
        private Vector3 dragOffset;
        private Vector3 dragTargetPosition;
        private Vector3 movementVelocity;

        private bool isDragging;

        /// <summary>
        /// 現在ドラッグ中かどうかを返します。
        /// </summary>
        public bool IsDragging => isDragging;

        private void Reset()
        {
            FindComponents();
        }

        private void Awake()
        {
            FindComponents();

            if (character == null)
            {
                Debug.LogError(
                    $"{nameof(MascotDragController)}: " +
                    "MascotCharacterが見つかりません。",
                    this);

                enabled = false;
                return;
            }

            if (targetCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MascotDragController)}: " +
                    "ドラッグに使用するCameraが見つかりません。",
                    this);

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

        /// <summary>
        /// 指定された画面座標からドラッグを開始します。
        /// </summary>
        public bool BeginDrag(Vector2 screenPosition)
        {
            if (!enabled || targetCamera == null)
            {
                return false;
            }

            /*
             * カメラの正面方向を法線とする平面を、
             * キャラクターの現在位置に作ります。
             *
             * この平面上をマウスで移動することで、
             * カメラから見た左右・上下方向へ自然に動かせます。
             */
            dragPlane = new Plane(
                -targetCamera.transform.forward,
                transform.position);

            if (!TryGetPointOnDragPlane(
                    screenPosition,
                    out Vector3 pointerWorldPosition))
            {
                return false;
            }

            /*
             * 掴んだ位置とオブジェクト中心との差を保存します。
             * これにより、ドラッグ開始時にモデルの中心へ
             * 突然移動することを防ぎます。
             */
            dragOffset = transform.position - pointerWorldPosition;
            dragTargetPosition = transform.position;
            movementVelocity = Vector3.zero;
            isDragging = true;

            return true;
        }

        /// <summary>
        /// ドラッグ中の目標位置を更新します。
        /// </summary>
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

        /// <summary>
        /// ドラッグを終了します。
        /// </summary>
        public void EndDrag()
        {
            if (!isDragging)
            {
                return;
            }

            /*
             * 終了時は最終目標位置へ正確に合わせます。
             */
            transform.position = dragTargetPosition;
            movementVelocity = Vector3.zero;
            isDragging = false;
        }

        /// <summary>
        /// ドラッグ状態を即座に解除します。
        /// </summary>
        public void CancelDrag()
        {
            isDragging = false;
            movementVelocity = Vector3.zero;
        }

        private bool TryGetPointOnDragPlane(
            Vector2 screenPosition,
            out Vector3 worldPosition)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

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

            if (!allowDepthMovement)
            {
                result.z = currentPosition.z;
            }

            if (limitPosition)
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
    }
}