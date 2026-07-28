using UnityEngine;

namespace DesktopMascot.Desktop
{
    /// <summary>
    /// Unity Playerウィンドウの最前面表示を管理します。
    ///
    /// NativeWindowControllerから初期化完了通知を受け取り、
    /// その後に最前面状態を適用します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NativeWindowController))]
    public sealed class TopMostWindowController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private NativeWindowController nativeWindowController;

        [Header("Top Most")]

        [Tooltip("Windowsビルドを常に最前面へ表示します。")]
        [SerializeField]
        private bool topMostOnStart = true;

        private bool isTopMost;
        private bool initialStateApplied;

        /// <summary>
        /// このControllerが最後に適用した最前面状態です。
        /// </summary>
        public bool IsTopMost => isTopMost;

        private void Reset()
        {
            FindComponents();
        }

        private void Awake()
        {
            FindComponents();

            if (nativeWindowController == null)
            {
                Debug.LogError(
                    $"{nameof(TopMostWindowController)}: " +
                    "NativeWindowControllerが見つかりません。",
                    this);

                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (nativeWindowController != null)
            {
                nativeWindowController.Initialized +=
                    HandleNativeWindowInitialized;
            }
        }

        private void Start()
        {
            /*
             * Script Execution Orderなどの影響で、
             * イベント登録前に初期化が完了していた場合にも
             * 対応できるよう、現在の状態を確認します。
             */
            if (nativeWindowController != null &&
                nativeWindowController.IsInitialized)
            {
                ApplyInitialState();
            }
        }

        private void OnDisable()
        {
            if (nativeWindowController != null)
            {
                nativeWindowController.Initialized -=
                    HandleNativeWindowInitialized;
            }
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            /*
             * 終了時に可能であれば通常の重なり順へ戻します。
             */
            if (NativeWindow.IsAvailable)
            {
                NativeWindow.SetTopMost(false);
            }
#endif
        }

        [ContextMenu("Find Desktop Components")]
        private void FindComponents()
        {
            if (nativeWindowController == null)
            {
                nativeWindowController =
                    GetComponent<NativeWindowController>();
            }
        }

        /// <summary>
        /// 最前面表示を切り替えます。
        /// </summary>
        public bool SetTopMost(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!NativeWindow.IsAvailable)
            {
                Debug.LogWarning(
                    $"{nameof(TopMostWindowController)}: " +
                    "NativeWindowが初期化されていません。",
                    this);

                return false;
            }

            if (!NativeWindow.SetTopMost(enabled))
            {
                Debug.LogError(
                    $"{nameof(TopMostWindowController)}: " +
                    "最前面表示の変更に失敗しました。",
                    this);

                return false;
            }

            isTopMost = enabled;

            Debug.Log(
                $"{nameof(TopMostWindowController)}: " +
                $"最前面表示を" +
                $"{(enabled ? "有効" : "無効")}にしました。",
                this);

            return true;
#else
            isTopMost = false;
            return false;
#endif
        }

        [ContextMenu("Enable Top Most")]
        private void EnableTopMostFromContextMenu()
        {
            SetTopMost(true);
        }

        [ContextMenu("Disable Top Most")]
        private void DisableTopMostFromContextMenu()
        {
            SetTopMost(false);
        }

        private void HandleNativeWindowInitialized()
        {
            ApplyInitialState();
        }

        private void ApplyInitialState()
        {
            /*
             * イベント受信とStart()の状態確認が同じ起動中に
             * 両方発生しても、適用処理を重複させません。
             */
            if (initialStateApplied)
            {
                return;
            }

            if (!NativeWindow.IsAvailable)
            {
                return;
            }

            if (SetTopMost(topMostOnStart))
            {
                initialStateApplied = true;
            }
        }
    }
}