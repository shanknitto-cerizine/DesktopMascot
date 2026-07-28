using System;
using DesktopMascot.Character;
using DesktopMascot.Runtime;
using UnityEngine;

namespace DesktopMascot.Diagnostics
{
    internal static class DesktopMascotCharacterAssetDiagnostics
    {
        private const string Prefix =
            "[DesktopMascotCharacterAssetDiagnostics]";

        internal static void RunFocusedTests()
        {
            var passed = true;
            var productionSeparated =
                CharacterAssetManager.ProductionInstanceCount == 0;
            Check(
                "Diagnostic mode production manager separated",
                productionSeparated,
                ref passed);

            var candidates =
                UnityEngine.Object.FindObjectsByType<MascotCharacter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var oneSceneCandidate = candidates.Length == 1;
            Check(
                "Bundled Scene candidate count is one",
                oneSceneCandidate,
                ref passed);

            var manager =
                CharacterAssetManager.CreateForFocusedDiagnostics();
            var first = manager.InitializeBundledDefault(candidates);
            var descriptor = first.Descriptor;
            Check(
                "Bundled default initialization succeeds",
                first.Succeeded,
                ref passed);
            Check(
                "Logical internal ID is stable",
                descriptor != null
                && descriptor.InternalId
                    == CharacterDescriptor.BundledDefaultInternalId,
                ref passed);
            Check(
                "Active root is existing TEST_MODEL",
                oneSceneCandidate
                && descriptor != null
                && ReferenceEquals(
                    descriptor.Root,
                    candidates[0].gameObject)
                && descriptor.DisplayName == "TEST_MODEL",
                ref passed);
            Check(
                "Animator reference matches MascotCharacter",
                descriptor != null
                && ReferenceEquals(
                    descriptor.Animator,
                    descriptor.Character.Animator),
                ref passed);
            Check(
                "Vrm10Instance reference matches MascotCharacter",
                descriptor != null
                && ReferenceEquals(
                    descriptor.VrmInstance,
                    descriptor.Character.VrmInstance),
                ref passed);

            var generation = manager.ActiveCharacterGeneration;
            var second = manager.InitializeBundledDefault(
                Array.Empty<MascotCharacter>());
            Check(
                "Repeated initialization preserves descriptor",
                second.Succeeded
                && ReferenceEquals(first.Descriptor, second.Descriptor)
                && manager.ActiveCharacterGeneration == generation
                && generation == 1,
                ref passed);

            var missing =
                CharacterAssetManager.ResolveBundledDefault(
                    Array.Empty<MascotCharacter>());
            Check(
                "Missing candidate fails explicitly",
                missing.Status
                    == CharacterAssetInitializationStatus
                        .MissingCandidate
                && !DesktopMascotRuntimeBootstrap
                    .ShouldCreateRuntimeSurfaces(missing),
                ref passed);

            var duplicateCandidates = oneSceneCandidate
                ? new[] { candidates[0], candidates[0] }
                : Array.Empty<MascotCharacter>();
            var duplicate =
                CharacterAssetManager.ResolveBundledDefault(
                    duplicateCandidates);
            Check(
                "Multiple candidates fail explicitly",
                oneSceneCandidate
                && duplicate.Status
                    == CharacterAssetInitializationStatus
                        .MultipleCandidates,
                ref passed);

            var invalidRoot =
                new GameObject("M039 Missing Reference Candidate");
            invalidRoot.SetActive(false);
            var invalidCharacter =
                invalidRoot.AddComponent<MascotCharacter>();
            var missingReference =
                CharacterAssetManager.ResolveBundledDefault(
                    new[] { invalidCharacter });
            Check(
                "Missing required reference fails explicitly",
                missingReference.Status
                    == CharacterAssetInitializationStatus
                        .MissingVrm10Instance,
                ref passed);
            UnityEngine.Object.Destroy(invalidRoot);

            manager.BeginShutdown();
            var firstCleanup = manager.Cleanup();
            var secondCleanup = manager.Cleanup();
            var rejectedAfterShutdown =
                manager.InitializeBundledDefault(candidates);
            Check(
                "Cleanup is idempotent",
                firstCleanup
                && secondCleanup
                && manager.CleanupCompleted
                && manager.ActiveCharacter == null,
                ref passed);
            Check(
                "Activation rejected after shutdown",
                rejectedAfterShutdown.Status
                    == CharacterAssetInitializationStatus
                        .ShutdownStarted,
                ref passed);
            Check(
                "Character foundation has no persistence",
                !CharacterAssetManager.UsesPersistence,
                ref passed);
            Check(
                "Diagnostic mode remains production-manager free",
                CharacterAssetManager.ProductionInstanceCount == 0,
                ref passed);

            Debug.Log(
                $"{Prefix} Focused diagnostics passed: {passed}");
        }

        private static void Check(
            string name,
            bool condition,
            ref bool passed)
        {
            if (!condition)
                passed = false;
            Debug.Log($"{Prefix} {name}: {condition}");
        }
    }
}
