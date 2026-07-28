using System;
using System.Collections.Generic;
using System.IO;
using DesktopMascot.Runtime.Settings;
using DesktopMascot.Runtime.Settings.UI;
using UnityEngine;
using RuntimeSettingsStore =
    DesktopMascot.Runtime.Settings.SettingsStore;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotSettingsUiDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotSettingsUiDiagnostics]";

        internal static bool Passed { get; private set; }

        internal static bool RunFocusedTests()
        {
            var root = Path.Combine(
                Application.temporaryCachePath,
                "DesktopMascotMinimal",
                "M034-tests",
                Guid.NewGuid().ToString("N"));
            GameObject owner = null;
            var results = new List<bool>();
            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "settings.json");
                var manager = Manager(path);
                var initialization = manager.Initialize();
                var presentation = new FakePresentationSource
                {
                    Frame = 1
                };
                owner = new GameObject("M034SettingsUiDiagnostics");
                var controller =
                    owner.AddComponent<SettingsWindowController>();
                controller.Initialize(manager, presentation);
                controller.ObservePresentationFrameForDiagnostics();

                results.Add(Record(
                    "Initialize closed",
                    initialization.Status
                        == SettingsInitializationStatus.DefaultsCreated
                    && controller.IsInitialized
                    && !controller.IsOpen));

                controller.Open();
                presentation.Frame = 2;
                var advancedWhileOpen =
                    controller.ObservePresentationFrameForDiagnostics();
                results.Add(Record(
                    "Window opens",
                    controller.IsOpen
                    && advancedWhileOpen
                    && controller.PresentationAdvanceWhileOpenCount > 0));
                results.Add(Record(
                    "Initial values",
                    !controller.Binding.FirstRunCompleted
                    && !controller.Binding.IsDirty
                    && !controller.Binding.ApplyEnabled));

                controller.Binding.FirstRunCompleted = true;
                results.Add(Record(
                    "Editing and dirty tracking",
                    controller.Binding.FirstRunCompleted
                    && controller.Binding.IsDirty
                    && controller.Binding.ApplyEnabled));

                controller.Cancel();
                presentation.Frame = 3;
                var advancedAfterCancel =
                    controller.ObservePresentationFrameForDiagnostics();
                controller.Open();
                presentation.Frame = 4;
                var advancedAfterReopen =
                    controller.ObservePresentationFrameForDiagnostics();
                results.Add(Record(
                    "Cancel closes and reopens over fresh presentation",
                    controller.IsOpen
                    && advancedAfterCancel
                    && advancedAfterReopen
                    && controller
                        .PresentationAdvanceWhileClosedCount > 0
                    && !controller.Binding.FirstRunCompleted
                    && !controller.Binding.IsDirty
                    && !manager.Current.FirstRunCompleted));

                controller.Binding.FirstRunCompleted = true;
                var applyResult = controller.Apply();
                var reloadedAfterApply = Manager(path);
                var applyReloadResult =
                    reloadedAfterApply.Initialize();
                results.Add(Record(
                    "Apply saves and clears dirty",
                    applyResult
                    && manager.Current.FirstRunCompleted
                    && !controller.Binding.IsDirty
                    && !controller.Binding.ApplyEnabled
                    && applyReloadResult.Status
                        == SettingsInitializationStatus.Loaded
                    && reloadedAfterApply.Current.FirstRunCompleted));

                controller.RestoreDefaults();
                var persistedBeforeDefaultsApply = Manager(path);
                persistedBeforeDefaultsApply.Initialize();
                results.Add(Record(
                    "Restore Defaults edits only ViewModel",
                    !controller.Binding.FirstRunCompleted
                    && controller.Binding.IsDirty
                    && controller.Binding.ApplyEnabled
                    && persistedBeforeDefaultsApply
                        .Current.FirstRunCompleted));

                var defaultsApplyResult = controller.Apply();
                var reloadedDefaults = Manager(path);
                reloadedDefaults.Initialize();
                results.Add(Record(
                    "Restore Defaults applies on request",
                    defaultsApplyResult
                    && !manager.Current.FirstRunCompleted
                    && !reloadedDefaults.Current.FirstRunCompleted
                    && !controller.Binding.IsDirty));

                var valid = controller.Binding.Validation;
                var invalid = SettingsValidation.Validate(
                    new SettingsValues(99, 1, false));
                results.Add(Record(
                    "Reusable validation",
                    valid.IsValid
                    && !invalid.IsValid
                    && !string.IsNullOrEmpty(invalid.Message)));

                controller.Close();
                presentation.Frame = 5;
                var advancedAfterClose =
                    controller.ObservePresentationFrameForDiagnostics();
                results.Add(Record(
                    "Window closes over fresh presentation",
                    !controller.IsOpen
                    && advancedAfterClose
                    && controller
                        .PresentationAdvanceWhileClosedCount > 1));
                results.Add(Record(
                    "Production isolation",
                    !path.StartsWith(
                        Path.GetDirectoryName(
                            SettingsManager.ProductionPath) +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)));
                presentation.Dispose();
                results.Add(Record(
                    "Preview resource contract",
                    presentation.RenderTextureAllocationCount == 0
                    && presentation.CleanupComplete));

                Passed = results.TrueForAll(value => value);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{Prefix} Unexpected test failure: {exception}");
                Passed = false;
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
                try
                {
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"{Prefix} Temporary cleanup failed: " +
                        exception.Message);
                }
            }

            Debug.Log($"{Prefix} Automated tests passed: {Passed}");
            return Passed;
        }

        private static SettingsManager Manager(string path) =>
            new SettingsManager(
                new RuntimeSettingsStore(path),
                new SettingsSerializer(),
                false);

        private static bool Record(string name, bool result)
        {
            Debug.Log($"{Prefix} {name}: {result}");
            return result;
        }

        private sealed class FakePresentationSource :
            ISettingsPlayerPresentationSource
        {
            internal int Frame { get; set; }

            public int PresentationFrame => Frame;
            public int PresentationWidth => 500;
            public int PresentationHeight => 700;
            public int RenderTextureAllocationCount => 0;
            public bool SettingsOnlyRenderingActive { get; private set; }
            public bool CharacterHiddenFromPlayerSurface =>
                SettingsOnlyRenderingActive;
            public bool CleanupComplete { get; private set; }

            public bool ApplySettingsPresentation(bool settingsVisible)
            {
                SettingsOnlyRenderingActive = settingsVisible;
                return true;
            }

            public void Dispose()
            {
                CleanupComplete = true;
            }
        }
    }
}
