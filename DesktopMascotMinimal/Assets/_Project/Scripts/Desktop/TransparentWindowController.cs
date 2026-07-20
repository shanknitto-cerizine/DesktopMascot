
using UnityEngine;

namespace DesktopMascot.Desktop
{
    /// <summary>
    /// Unity Playerウィンドウの背景透明化を管理します。
    ///
    /// NativeWindowControllerの初期化完了後に、
    /// ウィンドウ枠の除去とカラーキー透明化を適用します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NativeWindowController))]
    public sealed class TransparentWindowController : MonoBehaviour
    {
        private static readonly Color32 TransparencyColor =
            new Color32(
                255,
                0,
                255,
                255);

        [Header("References")]

        [SerializeField]
        private NativeWindowController nativeWindowController;

        [SerializeField]
        private Camera targetCamera;

        [Header("Transparency")]

        [Tooltip("Windowsビルド起動時に背景を透明化します。")]
        [SerializeField]
        private bool transparentOnStart = true;

        [Tooltip("透明化時にタイトルバーとウィンドウ枠を除去します。")]
        [SerializeField]
        private bool borderless = true;

        [Tooltip(
            "透明化時にCamera背景色を" +
            "カラーキー用の色へ変更します。")]
        [SerializeField]
        private bool configureCamera = true;

        private Color originalBackgroundColor;
        private CameraClearFlags originalClearFlags;

        private bool cameraStateStored;
        private bool initialStateApplied;
        private bool isTransparent;

        /// <summary>
        /// 最後に正常適用された透明化状態です。
        /// </summary>
        public bool IsTransparent =>
            isTransparent;

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
             * NativeWindowControllerの初期化が
             * すでに完了している場合にも対応します。
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
            if (NativeWindow.IsAvailable)
            {
                NativeWindow.SetColorKeyTransparency(
                    false,
                    TransparencyColor.r,
                    TransparencyColor.g,
                    TransparencyColor.b);

                NativeWindow.SetBorderless(false);
            }
#endif

            RestoreCameraSettings();
        }

        /// <summary>
        /// 背景透明化を切り替えます。
        /// </summary>
        public bool SetTransparent(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!NativeWindow.IsAvailable)
            {
                Debug.LogWarning(
                    $"{nameof(TransparentWindowController)}: " +
                    "NativeWindowが初期化されていません。",
                    this);

                return false;
            }

            if (enabled)
            {
                return EnableTransparency();
            }

            return DisableTransparency();
#else
            isTransparent = false;
            return false;
#endif
        }

        [ContextMenu("Enable Transparency")]
        private void EnableTransparencyFromContextMenu()
        {
            SetTransparent(true);
        }

        [ContextMenu("Disable Transparency")]
        private void DisableTransparencyFromContextMenu()
        {
            SetTransparent(false);
        }

        [ContextMenu("Find Desktop Components")]
        private void FindComponents()
        {
            if (nativeWindowController == null)
            {
                nativeWindowController =
                    GetComponent<NativeWindowController>();
            }

            if (targetCamera == null)
            {
                targetCamera =
                    Camera.main;
            }
        }

        private bool ValidateComponents()
        {
            bool isValid = true;

            if (nativeWindowController == null)
            {
                Debug.LogError(
                    $"{nameof(TransparentWindowController)}: " +
                    "NativeWindowControllerが見つかりません。",
                    this);

                isValid = false;
            }

            if (configureCamera &&
                targetCamera == null)
            {
                Debug.LogError(
                    $"{nameof(TransparentWindowController)}: " +
                    "透明化に使用するCameraが見つかりません。",
                    this);

                isValid = false;
            }

            return isValid;
        }

        private void HandleNativeWindowInitialized()
        {
            ApplyInitialState();
        }

        private void ApplyInitialState()
        {
            if (initialStateApplied)
            {
                return;
            }

            if (!NativeWindow.IsAvailable)
            {
                return;
            }

            if (SetTransparent(transparentOnStart))
            {
                initialStateApplied = true;
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

        private bool EnableTransparency()
        {
            if (borderless &&
                !NativeWindow.SetBorderless(true))
            {
                Debug.LogError(
                    $"{nameof(TransparentWindowController)}: " +
                    "ウィンドウ枠の除去に失敗しました。",
                    this);

                return false;
            }

            ApplyTransparentCameraSettings();

            bool transparencyEnabled =
                NativeWindow.SetColorKeyTransparency(
                    true,
                    TransparencyColor.r,
                    TransparencyColor.g,
                    TransparencyColor.b);

            if (!transparencyEnabled)
            {
                RestoreCameraSettings();

                if (borderless)
                {
                    NativeWindow.SetBorderless(false);
                }

                Debug.LogError(
                    $"{nameof(TransparentWindowController)}: " +
                    "カラーキー透明化の適用に失敗しました。",
                    this);

                return false;
            }

            isTransparent = true;

            Debug.Log(
                $"{nameof(TransparentWindowController)}: " +
                "背景透明化を有効にしました。",
                this);

            return true;
        }

        private bool DisableTransparency()
        {
            bool transparencyDisabled =
                NativeWindow.SetColorKeyTransparency(
                    false,
                    TransparencyColor.r,
                    TransparencyColor.g,
                    TransparencyColor.b);

            bool borderRestored =
                !borderless ||
                NativeWindow.SetBorderless(false);

            RestoreCameraSettings();

            isTransparent = false;

            if (!transparencyDisabled ||
                !borderRestored)
            {
                Debug.LogWarning(
                    $"{nameof(TransparentWindowController)}: " +
                    "透明化の解除処理の一部に失敗しました。",
                    this);

                return false;
            }

            Debug.Log(
                $"{nameof(TransparentWindowController)}: " +
                "背景透明化を無効にしました。",
                this);

            return true;
        }

#endif

        private void ApplyTransparentCameraSettings()
        {
            if (!configureCamera ||
                targetCamera == null)
            {
                return;
            }

            if (!cameraStateStored)
            {
                originalBackgroundColor =
                    targetCamera.backgroundColor;

                originalClearFlags =
                    targetCamera.clearFlags;

                cameraStateStored = true;
            }

            targetCamera.clearFlags =
                CameraClearFlags.SolidColor;

            /*
             * Windows側のカラーキーと完全に一致する
             * 不透明なマゼンタで背景をクリアします。
             */
            targetCamera.backgroundColor =
                TransparencyColor;
        }

        private void RestoreCameraSettings()
        {
            if (!cameraStateStored ||
                targetCamera == null)
            {
                return;
            }

            targetCamera.backgroundColor =
                originalBackgroundColor;

            targetCamera.clearFlags =
                originalClearFlags;

            cameraStateStored = false;
        }
    }
}
