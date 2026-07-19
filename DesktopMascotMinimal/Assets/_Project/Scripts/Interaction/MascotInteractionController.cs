using System.Collections;
using DesktopMascot.Character;
using UnityEngine;

namespace DesktopMascot.Interaction
{
    /// <summary>
    /// マスコットに対するマウス操作を検出します。
    /// 現在はクリック時の表情リアクションを担当します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotCharacter))]
    [RequireComponent(typeof(MascotExpressionController))]
    public sealed class MascotInteractionController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private MascotCharacter character;

        [SerializeField]
        private MascotExpressionController expressionController;

        [SerializeField]
        private Camera targetCamera;

        [Header("Click Detection")]

        [SerializeField]
        private LayerMask clickableLayers = ~0;

        [SerializeField]
        [Min(0.1f)]
        private float maximumRayDistance = 100.0f;

        [Header("Click Reaction")]

        [SerializeField]
        [Min(0.0f)]
        private float happyDuration = 1.5f;

        [SerializeField]
        private bool outputDebugLog = true;

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
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            DetectClick();
        }

        /// <summary>
        /// 必要な参照を自動取得します。
        /// </summary>
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

            if (targetCamera == null)
            {
                Debug.LogError(
                    $"{nameof(MascotInteractionController)}: " +
                    "クリック判定に使用するCameraが見つかりません。",
                    this);

                isValid = false;
            }

            return isValid;
        }

        private void DetectClick()
        {
            Ray ray = targetCamera.ScreenPointToRay(
                Input.mousePosition);

            bool hitSomething = Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumRayDistance,
                clickableLayers);

            if (!hitSomething)
            {
                return;
            }

            /*
             * Rayが別のColliderに当たる可能性があるため、
             * 命中したColliderがこのキャラクターに属するか確認します。
             */
            MascotCharacter clickedCharacter =
                hit.collider.GetComponentInParent<MascotCharacter>();

            if (clickedCharacter != character)
            {
                return;
            }

            OnMascotClicked(hit);
        }

        private void OnMascotClicked(RaycastHit hit)
        {
            if (outputDebugLog)
            {
                Debug.Log(
                    $"Mascot clicked: {hit.collider.name}",
                    hit.collider);
            }

            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
            }

            reactionCoroutine =
                StartCoroutine(PlayClickReaction());
        }

        private IEnumerator PlayClickReaction()
        {
            expressionController.SetHappy();

            if (happyDuration > 0.0f)
            {
                yield return new WaitForSeconds(happyDuration);
            }

            expressionController.SetNeutral();
            reactionCoroutine = null;
        }

        private void OnDisable()
        {
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
    }
}