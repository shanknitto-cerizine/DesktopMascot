using System;
using System.Collections;
using UnityEngine;

namespace DesktopMascot.Desktop
{
    /// <summary>
    /// Unity Playerのネイティブウィンドウを初期化します。
    ///
    /// ウィンドウハンドルの取得に成功すると、
    /// Initializedイベントを発行します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NativeWindowController : MonoBehaviour
    {
        [Header("Initialization")]

        [Tooltip(
            "起動直後はWindowsウィンドウの準備が完了していない" +
            "場合があるため、初期化前に待機するフレーム数です。")]
        [SerializeField]
        [Min(0)]
        private int initializationDelayFrames = 1;

        [Tooltip(
            "ウィンドウハンドルを取得できなかった場合の" +
            "最大試行回数です。")]
        [SerializeField]
        [Min(1)]
        private int maximumAttempts = 10;

        private Coroutine initializationCoroutine;

        /// <summary>
        /// NativeWindowの初期化に成功したときに発行されます。
        /// </summary>
        public event Action Initialized;

        /// <summary>
        /// Unity Playerのウィンドウを利用できる状態かどうかです。
        /// </summary>
        public bool IsInitialized =>
            NativeWindow.IsAvailable;

        private void Start()
        {
            initializationCoroutine =
                StartCoroutine(InitializeNativeWindow());
        }

        private void OnDestroy()
        {
            if (initializationCoroutine != null)
            {
                StopCoroutine(initializationCoroutine);
                initializationCoroutine = null;
            }

            NativeWindow.Clear();
        }

        private IEnumerator InitializeNativeWindow()
        {
            for (int frame = 0;
                 frame < initializationDelayFrames;
                 frame++)
            {
                yield return null;
            }

            /*
             * この判定をプリプロセッサの外側に置くことで、
             * Unity Editor上でもmaximumAttemptsが
             * 使用されているコードとしてコンパイルされます。
             *
             * そのためCS0414警告も発生しません。
             */
            if (Application.platform !=
                RuntimePlatform.WindowsPlayer)
            {
                Debug.Log(
                    $"{nameof(NativeWindowController)}: " +
                    "NativeWindowはWindows向けビルドでのみ" +
                    "初期化されます。",
                    this);

                initializationCoroutine = null;
                yield break;
            }

            for (int attempt = 1;
                 attempt <= maximumAttempts;
                 attempt++)
            {
                if (NativeWindow.TryInitialize())
                {
                    Debug.Log(
                        $"{nameof(NativeWindowController)}: " +
                        "Unity Playerのウィンドウを取得しました。 " +
                        $"Handle = " +
                        $"0x{NativeWindow.Handle.ToInt64():X}",
                        this);

                    /*
                     * ウィンドウを使用する各Controllerへ
                     * 初期化完了を通知します。
                     */
                    Initialized?.Invoke();

                    initializationCoroutine = null;
                    yield break;
                }

                Debug.LogWarning(
                    $"{nameof(NativeWindowController)}: " +
                    "ウィンドウを取得できなかったため" +
                    "再試行します。 " +
                    $"Attempt = {attempt}/{maximumAttempts}",
                    this);

                yield return null;
            }

            Debug.LogError(
                $"{nameof(NativeWindowController)}: " +
                "Unity Playerのウィンドウを取得できませんでした。",
                this);

            initializationCoroutine = null;
        }
    }
}