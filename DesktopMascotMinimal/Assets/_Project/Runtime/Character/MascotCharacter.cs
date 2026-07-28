using UnityEngine;
using UniVRM10;

namespace DesktopMascot.Character
{
    /// <summary>
    /// デスクトップマスコットのVRMモデルを一元管理します。
    /// Animator、VRM Expression、モデルルートへの参照を
    /// 他の制御スクリプトへ提供します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MascotCharacter : MonoBehaviour
    {
        [Header("Character Components")]

        [SerializeField]
        private Vrm10Instance vrmInstance;

        [SerializeField]
        private Animator animator;

        [Header("Initialization")]

        [SerializeField]
        private bool validateOnStart = true;

        /// <summary>
        /// VRM 1.0モデルのルートコンポーネントです。
        /// </summary>
        public Vrm10Instance VrmInstance => vrmInstance;

        /// <summary>
        /// Humanoidアニメーションを制御するAnimatorです。
        /// </summary>
        public Animator Animator => animator;

        /// <summary>
        /// VRMランタイムが初期化済みかどうかを返します。
        /// </summary>
        public bool IsVrmRuntimeReady
        {
            get
            {
                return vrmInstance != null &&
                       vrmInstance.Runtime != null;
            }
        }

        private void Reset()
        {
            FindComponents();
        }

        private void Awake()
        {
            FindComponents();

            if (validateOnStart)
            {
                ValidateComponents();
            }
        }

        /// <summary>
        /// 同じGameObjectおよび子オブジェクトから
        /// 必要なコンポーネントを自動取得します。
        /// </summary>
        [ContextMenu("Find Character Components")]
        public void FindComponents()
        {
            if (vrmInstance == null)
            {
                vrmInstance = GetComponent<Vrm10Instance>();
            }

            if (vrmInstance == null)
            {
                vrmInstance = GetComponentInChildren<Vrm10Instance>(true);
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        /// <summary>
        /// 必須コンポーネントが設定されているか確認します。
        /// </summary>
        [ContextMenu("Validate Character Components")]
        public bool ValidateComponents()
        {
            bool isValid = true;

            if (vrmInstance == null)
            {
                Debug.LogError(
                    $"{nameof(MascotCharacter)}: " +
                    "Vrm10Instanceが見つかりません。",
                    this);

                isValid = false;
            }

            if (animator == null)
            {
                Debug.LogError(
                    $"{nameof(MascotCharacter)}: " +
                    "Animatorが見つかりません。",
                    this);

                isValid = false;
            }
            else
            {
                if (animator.runtimeAnimatorController == null)
                {
                    Debug.LogWarning(
                        $"{nameof(MascotCharacter)}: " +
                        "Animator Controllerが設定されていません。",
                        this);
                }

                if (animator.avatar == null)
                {
                    Debug.LogWarning(
                        $"{nameof(MascotCharacter)}: " +
                        "AnimatorのAvatarが設定されていません。",
                        this);
                }
                else if (!animator.avatar.isValid)
                {
                    Debug.LogWarning(
                        $"{nameof(MascotCharacter)}: " +
                        "AnimatorのAvatarが有効ではありません。",
                        this);
                }

                if (!animator.isHuman)
                {
                    Debug.LogWarning(
                        $"{nameof(MascotCharacter)}: " +
                        "AnimatorがHumanoidとして認識されていません。",
                        this);
                }
            }

            return isValid;
        }
    }
}