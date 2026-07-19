using System.Collections;
using UnityEngine;
using UniVRM10;

namespace DesktopMascot.Character
{
    /// <summary>
    /// VRM 1.0モデルのExpressionと自動まばたきを制御します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotCharacter))]
    public sealed class MascotExpressionController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField]
        private MascotCharacter character;

        [Header("Automatic Blink")]

        [SerializeField]
        private bool automaticBlink = true;

        [SerializeField]
        [Min(0.1f)]
        private float minimumBlinkInterval = 2.5f;

        [SerializeField]
        [Min(0.1f)]
        private float maximumBlinkInterval = 6.0f;

        [SerializeField]
        [Min(0.01f)]
        private float closingDuration = 0.08f;

        [SerializeField]
        [Min(0.01f)]
        private float closedDuration = 0.06f;

        [SerializeField]
        [Min(0.01f)]
        private float openingDuration = 0.12f;

        private Vrm10RuntimeExpression expression;
        private Coroutine blinkCoroutine;

        private static readonly ExpressionKey BlinkKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.blink);

        private static readonly ExpressionKey HappyKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.happy);

        private static readonly ExpressionKey RelaxedKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.relaxed);

        private static readonly ExpressionKey SurprisedKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.surprised);

        private static readonly ExpressionKey AngryKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.angry);

        private static readonly ExpressionKey SadKey =
            ExpressionKey.CreateFromPreset(ExpressionPreset.sad);

        private void Reset()
        {
            character = GetComponent<MascotCharacter>();
        }

        private IEnumerator Start()
        {
            if (character == null)
            {
                character = GetComponent<MascotCharacter>();
            }

            if (character == null)
            {
                Debug.LogError(
                    $"{nameof(MascotExpressionController)}: " +
                    "MascotCharacterが見つかりません。",
                    this);

                enabled = false;
                yield break;
            }

            /*
             * Vrm10Instance.Runtimeは、Start直後には
             * 初期化が完了していない場合があるため待機します。
             */
            while (!character.IsVrmRuntimeReady)
            {
                yield return null;
            }

            expression = character.VrmInstance.Runtime.Expression;

            if (expression == null)
            {
                Debug.LogError(
                    $"{nameof(MascotExpressionController)}: " +
                    "VRM Expressionを取得できませんでした。",
                    this);

                enabled = false;
                yield break;
            }

            if (automaticBlink)
            {
                blinkCoroutine = StartCoroutine(BlinkLoop());
            }
        }

        private void OnDisable()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }

            if (expression != null)
            {
                expression.SetWeight(BlinkKey, 0.0f);
                ResetEmotion();
            }
        }

        private void OnValidate()
        {
            if (maximumBlinkInterval < minimumBlinkInterval)
            {
                maximumBlinkInterval = minimumBlinkInterval;
            }
        }

        private IEnumerator BlinkLoop()
        {
            while (true)
            {
                float interval = Random.Range(
                    minimumBlinkInterval,
                    maximumBlinkInterval);

                yield return new WaitForSeconds(interval);

                yield return AnimateWeight(
                    BlinkKey,
                    0.0f,
                    1.0f,
                    closingDuration);

                yield return new WaitForSeconds(closedDuration);

                yield return AnimateWeight(
                    BlinkKey,
                    1.0f,
                    0.0f,
                    openingDuration);
            }
        }

        private IEnumerator AnimateWeight(
            ExpressionKey key,
            float startWeight,
            float endWeight,
            float duration)
        {
            if (expression == null)
            {
                yield break;
            }

            if (duration <= 0.0f)
            {
                expression.SetWeight(key, endWeight);
                yield break;
            }

            float elapsedTime = 0.0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;

                float progress = Mathf.Clamp01(
                    elapsedTime / duration);

                float weight = Mathf.Lerp(
                    startWeight,
                    endWeight,
                    progress);

                expression.SetWeight(key, weight);

                yield return null;
            }

            expression.SetWeight(key, endWeight);
        }

        public void SetNeutral()
        {
            ResetEmotion();
        }

        public void SetHappy()
        {
            SetEmotion(HappyKey);
        }

        public void SetRelaxed()
        {
            SetEmotion(RelaxedKey);
        }

        public void SetSurprised()
        {
            SetEmotion(SurprisedKey);
        }

        public void SetAngry()
        {
            SetEmotion(AngryKey);
        }

        public void SetSad()
        {
            SetEmotion(SadKey);
        }

        private void SetEmotion(ExpressionKey target)
        {
            if (expression == null)
            {
                return;
            }

            ResetEmotion();
            expression.SetWeight(target, 1.0f);
        }

        private void ResetEmotion()
        {
            if (expression == null)
            {
                return;
            }

            expression.SetWeight(HappyKey, 0.0f);
            expression.SetWeight(RelaxedKey, 0.0f);
            expression.SetWeight(SurprisedKey, 0.0f);
            expression.SetWeight(AngryKey, 0.0f);
            expression.SetWeight(SadKey, 0.0f);
        }
    }
}