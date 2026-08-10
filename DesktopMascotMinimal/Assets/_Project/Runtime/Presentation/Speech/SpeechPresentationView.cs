using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace DesktopMascot.Runtime.Presentation.Speech
{
    internal sealed class SpeechPresentationView
    {
        internal const int Width = 170;
        internal const int Height = 64;
        internal const int SpeechLayer = 30;
        internal const float SpeakerFontSize = 12.0f;
        internal const float BodyFontSize = 11.0f;

        private GameObject root;
        private Camera renderCamera;
        private TMP_Text speakerText;
        private TMP_Text bodyText;
        private RenderTexture renderTexture;
        private bool cleanupComplete;
        private uint renderedFrameCount;

        internal RenderTexture RenderTexture => renderTexture;
        internal uint RenderedFrameCount => renderedFrameCount;
        internal bool IsCreated => root != null && renderTexture != null;
        internal bool CleanupComplete => cleanupComplete;
        internal bool TextLayoutContained =>
            IsContained(speakerText?.rectTransform)
            && IsContained(bodyText?.rectTransform);
        internal bool TextContentFits =>
            speakerText != null
            && bodyText != null
            && TextBoundsContained(speakerText)
            && TextBoundsContained(bodyText);
        internal bool TextRegionsSeparated =>
            speakerText != null
            && bodyText != null
            && speakerText.rectTransform.anchoredPosition.y
                - speakerText.rectTransform.rect.height * 0.5f
                >= bodyText.rectTransform.anchoredPosition.y
                    + bodyText.rectTransform.rect.height * 0.5f;
        internal float BodyPreferredHeight => bodyText?.preferredHeight ?? 0.0f;
        internal float SpeakerRenderedHeight =>
            speakerText?.textBounds.size.y ?? 0.0f;
        internal float BodyRenderedHeight => bodyText?.textBounds.size.y ?? 0.0f;
        internal float BodyRectHeight => bodyText?.rectTransform.rect.height ?? 0.0f;
        internal int BodyLineCount => bodyText?.textInfo.lineCount ?? 0;
        internal float BodyMaximumUnwrappedLineWidth
        {
            get
            {
                if (bodyText == null || string.IsNullOrEmpty(bodyText.text))
                    return 0.0f;
                var maximum = 0.0f;
                foreach (var line in bodyText.text.Split('\n'))
                {
                    maximum = Mathf.Max(
                        maximum,
                        bodyText.GetPreferredValues(
                            line,
                            float.PositiveInfinity,
                            float.PositiveInfinity).x);
                }
                return maximum;
            }
        }
        internal bool SpeakerOverflowing => speakerText?.isTextOverflowing ?? true;
        internal bool BodyOverflowing => bodyText?.isTextOverflowing ?? true;
        internal bool SpeechHierarchyUsesDedicatedLayer =>
            root != null && HierarchyUsesLayer(root.transform, SpeechLayer);

        internal bool Initialize()
        {
            var font = Resources.Load<TMP_FontAsset>(
                "DesktopMascotSpeech/NotoSansCJKjp-Regular SDF");
            if (font == null)
                return false;
            renderTexture = new RenderTexture(
                Width,
                Height,
                0,
                GraphicsFormat.B8G8R8A8_SRGB)
            {
                name = "DesktopMascotSpeechRenderTexture",
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!renderTexture.Create())
                return false;
            root = new GameObject("DesktopMascotSpeechPresentationView");
            Object.DontDestroyOnLoad(root);
            SetLayerRecursively(root, SpeechLayer);
            var cameraObject = new GameObject("SpeechCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.layer = SpeechLayer;
            renderCamera = cameraObject.AddComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.clear;
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = Height * 0.5f;
            renderCamera.transform.position = new Vector3(0, 0, -10);
            renderCamera.cullingMask = 1 << SpeechLayer;
            renderCamera.targetTexture = renderTexture;

            var canvasObject = new GameObject("SpeechCanvas");
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.layer = SpeechLayer;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = renderCamera;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(Width, Height);

            CreatePanel(canvasObject.transform, "Card", new Vector2(0, 0),
                new Vector2(Width - 4, Height - 12), new Color(0.10f, 0.12f, 0.18f, 0.96f));
            CreatePanel(canvasObject.transform, "Accent", new Vector2(-79, 16),
                new Vector2(4, 20), new Color(0.36f, 0.82f, 0.92f, 1));
            speakerText = CreateText(canvasObject.transform, "Speaker", font,
                new Vector2(2, 16), new Vector2(154, 18),
                SpeakerFontSize, FontStyles.Bold);
            bodyText = CreateText(canvasObject.transform, "Body", font,
                new Vector2(0, -12.5f), new Vector2(166, 37),
                BodyFontSize, FontStyles.Normal);
            bodyText.lineSpacing = -4.0f;
            SetLayerRecursively(root, SpeechLayer);
            root.SetActive(false);
            return true;
        }

        internal bool Apply(SpeechMessage message)
        {
            if (!IsCreated || message == null || !message.IsValid)
                return false;
            speakerText.text = message.Speaker.DisplayName;
            bodyText.text = message.Body;
            root.SetActive(true);
            speakerText.ForceMeshUpdate(true, true);
            bodyText.ForceMeshUpdate(true, true);
            SetLayerRecursively(root, SpeechLayer);
            renderCamera.Render();
            ++renderedFrameCount;
            return true;
        }

        internal void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }

        internal bool Cleanup()
        {
            if (cleanupComplete)
                return true;
            if (renderCamera != null)
                renderCamera.targetTexture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }
            if (root != null)
                Object.Destroy(root);
            renderTexture = null;
            root = null;
            cleanupComplete = true;
            return true;
        }

        private static void CreatePanel(
            Transform parent, string name, Vector2 position,
            Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.layer = SpeechLayer;
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = color;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, TMP_FontAsset font,
            Vector2 position, Vector2 size, float fontSize, FontStyles style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = SpeechLayer;
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            foreach (Transform child in value.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static bool HierarchyUsesLayer(Transform value, int layer)
        {
            if (value.gameObject.layer != layer)
                return false;
            foreach (Transform child in value)
            {
                if (!HierarchyUsesLayer(child, layer))
                    return false;
            }
            return true;
        }

        private static bool IsContained(RectTransform rect)
        {
            if (rect == null)
                return false;
            var halfSize = rect.rect.size * 0.5f;
            var position = rect.anchoredPosition;
            return position.x - halfSize.x >= -Width * 0.5f
                && position.x + halfSize.x <= Width * 0.5f
                && position.y - halfSize.y >= -Height * 0.5f
                && position.y + halfSize.y <= Height * 0.5f;
        }

        private static bool TextBoundsContained(TMP_Text text)
        {
            if (text == null)
                return false;
            var bounds = text.textBounds;
            var rect = text.rectTransform.rect;
            return bounds.min.x >= rect.xMin - 0.5f
                && bounds.max.x <= rect.xMax + 0.5f
                && bounds.min.y >= rect.yMin - 0.5f
                && bounds.max.y <= rect.yMax + 0.5f;
        }
    }
}
