using System;
using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterSelection;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class SettingsWindowController : MonoBehaviour
    {
        private const int WindowId = 0x444D4E;
        private const float WindowWidth = 420f;
        private const float WindowHeight = 410f;
        private const string Prefix = "[DesktopMascotSettingsWindow]";
        private static readonly Color SettingsSurfaceColor =
            new Color(0.075f, 0.085f, 0.105f, 1f);
        private static readonly Color SettingsPanelColor =
            new Color(0.13f, 0.15f, 0.18f, 1f);

        private SettingsManager manager;
        private RuntimeCharacterSelectionController characterSelection;
        private ISettingsPlayerPresentationSource presentationSource;
        private SettingsViewModel viewModel;
        private SettingsBinding binding;
        private Rect windowRect;
        private bool initialized;
        private bool isOpen;
        private int lastPresentedFrame = -1;
        private int presentationAdvanceWhileOpenCount;
        private int presentationAdvanceWhileClosedCount;
        private string statusMessage = string.Empty;

        internal bool IsInitialized => initialized;
        internal bool IsOpen => isOpen;
        internal int LastPresentedFrame => lastPresentedFrame;
        internal int PresentationAdvanceWhileOpenCount =>
            presentationAdvanceWhileOpenCount;
        internal int PresentationAdvanceWhileClosedCount =>
            presentationAdvanceWhileClosedCount;
        internal SettingsBinding Binding => binding;
        internal string StatusMessage => statusMessage;
        internal bool BackgroundOpaque =>
            Mathf.Approximately(SettingsSurfaceColor.a, 1f)
            && Mathf.Approximately(SettingsPanelColor.a, 1f);
        internal int CancelCount { get; private set; }
        internal int CloseCount { get; private set; }
        internal event Action<bool> OpenStateChanged;

        internal void Initialize(
            SettingsManager settingsManager,
            ISettingsPlayerPresentationSource playerPresentation = null,
            RuntimeCharacterSelectionController
                characterSelectionController = null)
        {
            manager = settingsManager;
            presentationSource = playerPresentation;
            characterSelection = characterSelectionController;
            viewModel = new SettingsViewModel();
            binding = new SettingsBinding(viewModel);
            initialized = manager != null && manager.IsInitialized;
            viewModel.Initialize(
                initialized
                    ? manager.Current
                    : SettingsDefaults.Create());
            isOpen = false;
            lastPresentedFrame = -1;
            presentationAdvanceWhileOpenCount = 0;
            presentationAdvanceWhileClosedCount = 0;
            statusMessage = string.Empty;
        }

        internal void Open()
        {
            if (!initialized)
                return;
            if (isOpen)
                return;
            viewModel.Initialize(manager.Current);
            statusMessage = string.Empty;
            windowRect = CenteredWindowRect();
            isOpen = true;
            OpenStateChanged?.Invoke(true);
        }

        internal void Close()
        {
            if (!initialized || !isOpen)
                return;
            CloseCount++;
            ReleaseGuiControl();
            viewModel.CancelChanges();
            statusMessage = string.Empty;
            isOpen = false;
            OpenStateChanged?.Invoke(false);
        }

        internal void Cancel()
        {
            if (initialized && isOpen)
                CancelCount++;
            Close();
        }

        internal void RestoreDefaults()
        {
            if (!initialized)
                return;
            viewModel.RestoreDefaults();
            statusMessage =
                "Defaults restored for editing. Apply to save.";
        }

        internal bool Apply()
        {
            if (!initialized)
                return false;
            var validation = binding.Validation;
            if (!validation.IsValid)
            {
                statusMessage = validation.Message;
                return false;
            }
            if (!binding.IsDirty)
                return true;
            if (!manager.TrySave(viewModel.Values))
            {
                statusMessage = "Unable to save settings.";
                return false;
            }
            viewModel.MarkApplied(manager.Current);
            statusMessage = "Settings applied.";
            return true;
        }

        private void OnGUI()
        {
            if (!initialized)
                return;
            if (Event.current.type == EventType.Repaint)
                ObservePresentationFrameForDiagnostics();
            if (!isOpen)
                return;
            DrawOpaqueSettingsSurface();
            windowRect = GUI.Window(
                WindowId,
                windowRect,
                DrawWindow,
                "Settings");
        }

        private void DrawWindow(int windowId)
        {
            GUI.Label(
                new Rect(20f, 35f, WindowWidth - 40f, 24f),
                "Application settings");
            var nextValue = GUI.Toggle(
                new Rect(20f, 67f, WindowWidth - 40f, 24f),
                binding.FirstRunCompleted,
                "First run completed");
            if (nextValue != binding.FirstRunCompleted)
                binding.FirstRunCompleted = nextValue;

            var validation = binding.Validation;
            var feedback = !validation.IsValid
                ? validation.Message
                : statusMessage;
            GUI.Label(
                new Rect(20f, 98f, WindowWidth - 40f, 24f),
                feedback);

            GUI.Label(
                new Rect(20f, 126f, WindowWidth - 40f, 24f),
                "Character");
            var activeCharacter = characterSelection?.ActiveCharacter;
            var sourceText = activeCharacter == null
                ? "利用できません"
                : activeCharacter.SourceType
                    == CharacterAssetSourceType.BundledScene
                    ? "同梱モデル"
                    : activeCharacter.DisplayName;
            GUI.Label(
                new Rect(20f, 151f, WindowWidth - 40f, 24f),
                $"現在のモデル: {sourceText}");
            GUI.Label(
                new Rect(20f, 177f, WindowWidth - 40f, 42f),
                characterSelection?.StatusMessage ?? string.Empty);

            var previousEnabled = GUI.enabled;
            GUI.enabled =
                previousEnabled
                && characterSelection != null
                && !characterSelection.IsBusy
                && !characterSelection.ShutdownStarted;
            if (GUI.Button(
                    new Rect(20f, 216f, 120f, 30f),
                    "VRMを選択"))
            {
                Debug.Log(
                    "[DesktopMascotSettingsUI] VRM selection button " +
                    "click received: True");
                var routed = characterSelection.RequestFileSelection();
                Debug.Log(
                    "[DesktopMascotSettingsUI] VRM selection command " +
                    $"routed: {routed}");
            }
            GUI.enabled = previousEnabled;

            GUI.enabled =
                previousEnabled
                && characterSelection?.CanActivateBundledCharacter == true;
            if (GUI.Button(
                    new Rect(148f, 216f, 252f, 30f),
                    "同梱モデルに戻す"))
            {
                characterSelection.RequestActivateBundledCharacter();
            }
            GUI.enabled = previousEnabled;

            GUI.enabled =
                previousEnabled
                && characterSelection?.CanClearPersistedSelection == true;
            if (GUI.Button(
                    new Rect(20f, 254f, 380f, 30f),
                    "現在のモデルは維持し、次回起動時だけ同梱モデルを使用"))
            {
                characterSelection.RequestUseBundledOnNextStartup();
            }
            GUI.enabled = previousEnabled;

            GUI.enabled = binding.ApplyEnabled;
            if (GUI.Button(new Rect(20f, 316f, 88f, 30f), "Apply"))
                Apply();
            GUI.enabled = previousEnabled;

            if (GUI.Button(new Rect(116f, 316f, 88f, 30f), "Cancel"))
            {
                ReleaseGuiControl();
                Cancel();
                return;
            }
            if (GUI.Button(
                    new Rect(212f, 316f, 112f, 30f),
                    "Restore Defaults"))
            {
                RestoreDefaults();
            }
            if (GUI.Button(new Rect(332f, 316f, 68f, 30f), "Close"))
            {
                ReleaseGuiControl();
                Close();
                return;
            }

            GUI.Label(
                new Rect(20f, 360f, WindowWidth - 40f, 22f),
                binding.IsDirty ? "Unsaved changes" : "No unsaved changes");
            GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 26f));
        }

        internal bool ObservePresentationFrameForDiagnostics()
        {
            if (presentationSource == null
                || presentationSource.PresentationFrame < 0)
            {
                return false;
            }
            var currentFrame = presentationSource.PresentationFrame;
            var advanced =
                lastPresentedFrame >= 0 && currentFrame > lastPresentedFrame;
            if (advanced)
            {
                if (isOpen)
                    presentationAdvanceWhileOpenCount++;
                else
                    presentationAdvanceWhileClosedCount++;
            }
            lastPresentedFrame = currentFrame;
            return advanced;
        }

        private static void ReleaseGuiControl()
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        private static Rect CenteredWindowRect() =>
            new Rect(
                Mathf.Max(0f, (Screen.width - WindowWidth) * 0.5f),
                Mathf.Max(0f, (Screen.height - WindowHeight) * 0.5f),
                WindowWidth,
                WindowHeight);

        private void DrawOpaqueSettingsSurface()
        {
            DrawOpaqueRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                SettingsSurfaceColor);
            DrawOpaqueRect(windowRect, SettingsPanelColor);
        }

        private static void DrawOpaqueRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                rect,
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = previousColor;
        }

    }
}
