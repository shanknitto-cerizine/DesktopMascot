using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopMascot.Character
{
    internal enum CharacterAssetInitializationStatus
    {
        Success = 0,
        MissingCandidate = 1,
        MultipleCandidates = 2,
        InactiveRoot = 3,
        MissingVrm10Instance = 4,
        MissingAnimator = 5,
        MissingAnimatorController = 6,
        ShutdownStarted = 7
    }

    internal readonly struct CharacterAssetInitializationResult
    {
        internal CharacterAssetInitializationResult(
            CharacterAssetInitializationStatus status,
            CharacterDescriptor descriptor)
        {
            Status = status;
            Descriptor = descriptor;
        }

        internal CharacterAssetInitializationStatus Status { get; }
        internal CharacterDescriptor Descriptor { get; }
        internal bool Succeeded =>
            Status == CharacterAssetInitializationStatus.Success
            && Descriptor != null;
    }

    internal enum CharacterActivationStatus
    {
        Success = 0,
        NotInitialized = 1,
        ShutdownStarted = 2,
        InvalidPreparedCharacter = 3,
        RuntimeSourceUnavailable = 4,
        OwnershipTransferFailed = 5,
        CommitFailed = 6,
        RuntimeCharacterAlreadyActive = 7,
        BundledCharacterAlreadyActive = 8,
        BundledCharacterUnavailable = 9,
        BundledCharacterValidationFailed = 10,
        BundledAnimatorNotReady = 11,
        OwnershipInvariantFailed = 12
    }

    internal readonly struct CharacterActivationResult
    {
        internal CharacterActivationResult(
            CharacterActivationStatus status,
            CharacterDescriptor descriptor)
        {
            Status = status;
            Descriptor = descriptor;
        }

        internal CharacterActivationStatus Status { get; }
        internal CharacterDescriptor Descriptor { get; }
        internal bool Succeeded =>
            Status == CharacterActivationStatus.Success
            && Descriptor != null;
    }

    internal sealed class CharacterAssetManager
    {
        private const string Prefix = "[DesktopMascotCharacterAsset]";

        private static CharacterAssetManager productionInstance;
        private static bool productionShutdownBarrier;

        private readonly bool production;
        private CharacterDescriptor activeCharacter;
        private CharacterDescriptor bundledCharacter;
        private RuntimeImportedCharacterHandle activeOwnedRuntimeCharacter;
        private readonly RuntimeRetiredCharacterDisposalQueue
            retiredOwnedRuntimeCharacters = new();
        private bool initialized;
        private bool shutdownStarted;
        private bool cleanupCompleted;
        private ulong activeCharacterGeneration;

        private CharacterAssetManager(bool isProduction)
        {
            production = isProduction;
        }

        internal static CharacterAssetManager ProductionInstance =>
            productionInstance;

        internal static int ProductionInstanceCount =>
            productionInstance != null ? 1 : 0;

        internal static bool UsesPersistence => false;

        internal CharacterDescriptor ActiveCharacter => activeCharacter;
        internal CharacterDescriptor BundledCharacter => bundledCharacter;
        internal ulong ActiveCharacterGeneration =>
            activeCharacterGeneration;
        internal bool IsInitialized =>
            initialized
            && activeCharacter != null
            && activeCharacter.IsAvailable;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool CleanupCompleted => cleanupCompleted;
        internal int ActiveOwnedRuntimeHandleCount =>
            activeOwnedRuntimeCharacter != null ? 1 : 0;
        internal int RetiredOwnedRuntimeHandleCount =>
            retiredOwnedRuntimeCharacters.Count;
        internal int MaximumRetiredQueueCount =>
            retiredOwnedRuntimeCharacters.MaximumCount;
        internal int RetiredDisposeRequestedCount =>
            retiredOwnedRuntimeCharacters.DisposeRequestedCount;
        internal int RetiredDisposeFailureCount =>
            retiredOwnedRuntimeCharacters.DisposeRequestFailureCount;
        internal int RetiredReleaseConfirmedCount =>
            retiredOwnedRuntimeCharacters.ReleaseConfirmedCount;
        internal int RetiredFenceCohortCount =>
            retiredOwnedRuntimeCharacters.FenceCohortCount;
        internal int RetiredFencePassedCount =>
            retiredOwnedRuntimeCharacters.FencePassedCount;
        internal int RetiredFenceTimeoutCount =>
            retiredOwnedRuntimeCharacters.FenceTimeoutCount;
        internal int RetiredUnsupportedFallbackCount =>
            retiredOwnedRuntimeCharacters.UnsupportedFallbackCount;
        internal int RetiredSafetyValidationFailureCount =>
            retiredOwnedRuntimeCharacters.SafetyValidationFailureCount;
        internal bool RetiredGraphicsFenceSupported =>
            retiredOwnedRuntimeCharacters.GraphicsFenceSupported;
        internal int TotalOwnedRuntimeHandleCount =>
            ActiveOwnedRuntimeHandleCount
            + RetiredOwnedRuntimeHandleCount;

        internal static CharacterAssetManager
            GetOrCreateProduction()
        {
            if (productionShutdownBarrier)
                return null;
            if (productionInstance == null)
            {
                productionInstance =
                    new CharacterAssetManager(isProduction: true);
            }
            return productionInstance;
        }

        internal static CharacterAssetManager
            CreateForFocusedDiagnostics()
        {
            return new CharacterAssetManager(isProduction: false);
        }

        internal CharacterAssetInitializationResult
            InitializeBundledDefault()
        {
            var candidates =
                UnityEngine.Object.FindObjectsByType<MascotCharacter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            return InitializeBundledDefault(candidates);
        }

        internal CharacterAssetInitializationResult
            InitializeBundledDefault(
                IReadOnlyList<MascotCharacter> candidates)
        {
            if (shutdownStarted || cleanupCompleted)
            {
                return Result(
                    CharacterAssetInitializationStatus.ShutdownStarted);
            }
            if (initialized)
            {
                return new CharacterAssetInitializationResult(
                    CharacterAssetInitializationStatus.Success,
                    activeCharacter);
            }

            var structuralResult = ResolveBundledDefault(candidates);
            if (!structuralResult.Succeeded)
                return structuralResult;

            activeCharacter = structuralResult.Descriptor;
            bundledCharacter = structuralResult.Descriptor;
            activeCharacterGeneration = 1;
            initialized = true;
            Log(
                "Initialization status/root/source/generation: " +
                $"{structuralResult.Status}/" +
                $"{activeCharacter.DisplayName}/" +
                $"{activeCharacter.SourceType}/" +
                activeCharacterGeneration);
            Log(
                "Animator/Vrm10Instance/VRM runtime available: " +
                $"{(activeCharacter.Animator != null)}/" +
                $"{(activeCharacter.VrmInstance != null)}/" +
                activeCharacter.IsVrmRuntimeReady);
            return structuralResult;
        }

        internal CharacterActivationResult
            TryActivateImportedCharacter(
                PreparedRuntimeCharacter prepared)
        {
            if (shutdownStarted || cleanupCompleted)
                return ActivationFailure(
                    CharacterActivationStatus.ShutdownStarted,
                    prepared);
            if (!initialized
                || activeCharacter == null
                || bundledCharacter == null)
            {
                return ActivationFailure(
                    CharacterActivationStatus.NotInitialized,
                    prepared);
            }
            if (prepared == null
                || prepared.Activated
                || prepared.Descriptor == null
                || !prepared.Descriptor.IsStructurallyAvailable
                || prepared.RuntimeInstance == null
                || prepared.RuntimeInstance.Root
                    != prepared.Descriptor.Root)
            {
                return ActivationFailure(
                    CharacterActivationStatus.InvalidPreparedCharacter,
                    prepared);
            }
            if (prepared.Source == null
                || prepared.Source.ShutdownStarted)
            {
                return ActivationFailure(
                    CharacterActivationStatus.RuntimeSourceUnavailable,
                    prepared);
            }

            var previous = activeCharacter;
            var previousOwnedRuntimeCharacter =
                activeOwnedRuntimeCharacter;
            var previousRoot = previous.Root;
            var importedRoot = prepared.Descriptor.Root;
            var previousWasActive =
                previousRoot != null && previousRoot.activeSelf;
            var ownershipTransferFailed = false;
            try
            {
                importedRoot.SetActive(true);
                if (!prepared.Descriptor.Animator.HasState(
                        0,
                        prepared.StandingIdleStateHash))
                {
                    throw new InvalidOperationException(
                        nameof(RuntimeCharacterPreparationStatus
                            .MissingStandingIdleState));
                }
                previousRoot.SetActive(false);
                prepared.RuntimeInstance.ShowMeshes();
                prepared.Descriptor.Animator.Play(
                    prepared.StandingIdleStateHash,
                    0,
                    0.0f);

                var ownership =
                    prepared.Source.TransferPreparedOwnership();
                if (ownership == null)
                {
                    ownershipTransferFailed = true;
                    throw new InvalidOperationException(
                        nameof(CharacterActivationStatus
                            .OwnershipTransferFailed));
                }

                activeOwnedRuntimeCharacter = ownership;
                if (previousOwnedRuntimeCharacter != null)
                {
                    retiredOwnedRuntimeCharacters.Enqueue(
                        previousOwnedRuntimeCharacter);
                }
                activeCharacter = prepared.Descriptor;
                ++activeCharacterGeneration;
                prepared.MarkActivated();
                Log(
                    "Activation status/source/generation: " +
                    $"{CharacterActivationStatus.Success}/" +
                    $"{activeCharacter.SourceType}/" +
                    activeCharacterGeneration);
                Log(
                    "Active/retired/total runtime handles: " +
                    $"{ActiveOwnedRuntimeHandleCount}/" +
                    $"{RetiredOwnedRuntimeHandleCount}/" +
                    TotalOwnedRuntimeHandleCount);
                return new CharacterActivationResult(
                    CharacterActivationStatus.Success,
                    activeCharacter);
            }
            catch (Exception exception)
            {
                foreach (var renderer
                         in prepared.RuntimeInstance.VisibleRenderers)
                {
                    if (renderer != null)
                        renderer.enabled = false;
                }
                if (importedRoot != null)
                    importedRoot.SetActive(false);
                if (previousRoot != null)
                    previousRoot.SetActive(previousWasActive);
                activeCharacter = previous;
                prepared.Rollback();
                Log(
                    "Activation rollback completed: True; exception: " +
                    exception.GetType().Name);
                return new CharacterActivationResult(
                    ownershipTransferFailed
                        ? CharacterActivationStatus
                            .OwnershipTransferFailed
                        : CharacterActivationStatus.CommitFailed,
                    null);
            }
        }

        internal CharacterActivationResult TryActivateBundledCharacter()
        {
            if (shutdownStarted || cleanupCompleted)
            {
                return BundledActivationFailure(
                    CharacterActivationStatus.ShutdownStarted);
            }
            if (!initialized
                || activeCharacter == null
                || bundledCharacter == null)
            {
                return BundledActivationFailure(
                    CharacterActivationStatus.NotInitialized);
            }

            var validationStatus = ValidateBundledCharacterForActivation(
                bundledCharacter);
            if (validationStatus != CharacterActivationStatus.Success)
                return BundledActivationFailure(validationStatus);

            var bundledRoot = bundledCharacter.Root;
            if (activeCharacter.SourceType
                    == CharacterAssetSourceType.BundledScene)
            {
                if (!ReferenceEquals(activeCharacter, bundledCharacter)
                    || activeOwnedRuntimeCharacter != null
                    || !bundledRoot.activeInHierarchy)
                {
                    return BundledActivationFailure(
                        CharacterActivationStatus
                            .OwnershipInvariantFailed);
                }
                Log(
                    "Bundled activation status/source/generation: " +
                    $"{CharacterActivationStatus.BundledCharacterAlreadyActive}/" +
                    $"{activeCharacter.SourceType}/" +
                    activeCharacterGeneration);
                return new CharacterActivationResult(
                    CharacterActivationStatus
                        .BundledCharacterAlreadyActive,
                    bundledCharacter);
            }

            if (activeCharacter.SourceType
                    != CharacterAssetSourceType.RuntimeImported
                || activeOwnedRuntimeCharacter == null
                || activeCharacter.Root == null
                || ReferenceEquals(
                    activeCharacter.Root,
                    bundledRoot)
                || bundledRoot.activeSelf)
            {
                return BundledActivationFailure(
                    CharacterActivationStatus.OwnershipInvariantFailed);
            }

            var previous = activeCharacter;
            var previousRoot = previous.Root;
            var previousRootWasActive = previousRoot.activeSelf;
            var bundledRootWasActive = bundledRoot.activeSelf;
            var previousOwnedRuntimeCharacter =
                activeOwnedRuntimeCharacter;
            var retiredHandleAdded = false;
            try
            {
                retiredOwnedRuntimeCharacters.EnsureCapacityForOneMore();

                if (shutdownStarted || cleanupCompleted)
                {
                    return BundledActivationFailure(
                        CharacterActivationStatus.ShutdownStarted);
                }

                previousRoot.SetActive(false);
                bundledRoot.SetActive(true);
                var animator = bundledCharacter.Animator;
                if (!animator.isActiveAndEnabled
                    || !animator.isInitialized
                    || !animator.HasState(
                        0,
                        CharacterAnimationContract
                            .StandingIdleStateHash))
                {
                    RestoreCharacterRoots(
                        previousRoot,
                        previousRootWasActive,
                        bundledRoot,
                        bundledRootWasActive);
                    return BundledActivationFailure(
                        CharacterActivationStatus
                            .BundledAnimatorNotReady);
                }
                animator.Play(
                    CharacterAnimationContract.StandingIdleStateHash,
                    0,
                    0.0f);

                retiredOwnedRuntimeCharacters.Enqueue(
                    previousOwnedRuntimeCharacter);
                retiredHandleAdded = true;
                activeOwnedRuntimeCharacter = null;
                activeCharacter = bundledCharacter;
                ++activeCharacterGeneration;

                Log(
                    "Bundled activation status/source/generation: " +
                    $"{CharacterActivationStatus.Success}/" +
                    $"{activeCharacter.SourceType}/" +
                    activeCharacterGeneration);
                Log(
                    "Active/retired/total runtime handles: " +
                    $"{ActiveOwnedRuntimeHandleCount}/" +
                    $"{RetiredOwnedRuntimeHandleCount}/" +
                    TotalOwnedRuntimeHandleCount);
                return new CharacterActivationResult(
                    CharacterActivationStatus.Success,
                    activeCharacter);
            }
            catch (Exception exception)
            {
                if (retiredHandleAdded)
                {
                    retiredOwnedRuntimeCharacters.Remove(
                        previousOwnedRuntimeCharacter);
                }
                activeOwnedRuntimeCharacter =
                    previousOwnedRuntimeCharacter;
                activeCharacter = previous;
                RestoreCharacterRoots(
                    previousRoot,
                    previousRootWasActive,
                    bundledRoot,
                    bundledRootWasActive);
                Log(
                    "Bundled activation rollback completed: True; " +
                    $"exception type: {exception.GetType().Name}");
                return new CharacterActivationResult(
                    CharacterActivationStatus.CommitFailed,
                    null);
            }
        }

        internal static CharacterAssetInitializationResult
            ResolveBundledDefault(
                IReadOnlyList<MascotCharacter> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Result(
                    CharacterAssetInitializationStatus.MissingCandidate);
            }
            if (candidates.Count != 1)
            {
                return Result(
                    CharacterAssetInitializationStatus.MultipleCandidates);
            }

            var character = candidates[0];
            if (character == null)
            {
                return Result(
                    CharacterAssetInitializationStatus.MissingCandidate);
            }
            character.FindComponents();
            if (character.VrmInstance == null)
            {
                return Result(
                    CharacterAssetInitializationStatus.MissingVrm10Instance);
            }
            if (character.Animator == null)
            {
                return Result(
                    CharacterAssetInitializationStatus.MissingAnimator);
            }
            if (character.Animator.runtimeAnimatorController == null)
            {
                return Result(
                    CharacterAssetInitializationStatus
                        .MissingAnimatorController);
            }
            if (!character.gameObject.activeInHierarchy)
            {
                return Result(
                    CharacterAssetInitializationStatus.InactiveRoot);
            }

            var descriptor = new CharacterDescriptor(
                CharacterDescriptor.BundledDefaultInternalId,
                character.gameObject.name,
                CharacterAssetSourceType.BundledScene,
                character);
            return new CharacterAssetInitializationResult(
                CharacterAssetInitializationStatus.Success,
                descriptor);
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
            if (production)
                productionShutdownBarrier = true;
            Log("Shutdown barrier closed: True");
        }

        internal bool Cleanup()
        {
            if (cleanupCompleted)
                return true;
            BeginShutdown();
            if (activeOwnedRuntimeCharacter != null
                || retiredOwnedRuntimeCharacters.Count != 0)
            {
                Log(
                    "Cleanup rejected before owned character release: True");
                return false;
            }
            activeCharacter = null;
            bundledCharacter = null;
            initialized = false;
            cleanupCompleted = true;
            if (production
                && ReferenceEquals(productionInstance, this))
            {
                productionInstance = null;
            }
            Log("Cleanup result: True");
            return true;
        }

        internal bool ReleaseActiveOwnedCharacterAfterPipelineStop()
        {
            if (!shutdownStarted)
                return false;
            var succeeded = true;
            if (activeOwnedRuntimeCharacter != null
                && !activeOwnedRuntimeCharacter.TryRequestDispose(
                    out var exceptionType))
            {
                succeeded = false;
                Log(
                    "Runtime active shutdown dispose failed; " +
                    $"exception type/status: {exceptionType}/DisposeFailed");
            }
            if (succeeded)
                activeOwnedRuntimeCharacter = null;
            succeeded &=
                retiredOwnedRuntimeCharacters
                    .DisposeAllAfterPipelineStop();
            Log(
                "Runtime-owned active/retired handles after release: " +
                $"{ActiveOwnedRuntimeHandleCount}/" +
                RetiredOwnedRuntimeHandleCount);
            return succeeded;
        }

        internal void PumpRetiredDisposalsAfterEndOfFrame()
        {
            if (shutdownStarted || cleanupCompleted)
                return;
            retiredOwnedRuntimeCharacters.PumpAfterEndOfFrame(
                activeOwnedRuntimeCharacter,
                activeCharacter?.Root);
        }

        private static CharacterActivationResult ActivationFailure(
            CharacterActivationStatus status,
            PreparedRuntimeCharacter prepared)
        {
            prepared?.Rollback();
            Log($"Activation rejected: {status}");
            return new CharacterActivationResult(status, null);
        }

        private static CharacterActivationResult
            BundledActivationFailure(CharacterActivationStatus status)
        {
            Log($"Bundled activation rejected: {status}");
            return new CharacterActivationResult(status, null);
        }

        private static CharacterActivationStatus
            ValidateBundledCharacterForActivation(
                CharacterDescriptor descriptor)
        {
            if (descriptor == null
                || descriptor.SourceType
                    != CharacterAssetSourceType.BundledScene
                || descriptor.Root == null
                || descriptor.Character == null)
            {
                return CharacterActivationStatus
                    .BundledCharacterUnavailable;
            }

            var root = descriptor.Root;
            if (!root.scene.IsValid()
                || !root.scene.isLoaded)
            {
                return CharacterActivationStatus
                    .BundledCharacterUnavailable;
            }

            descriptor.Character.FindComponents();
            if (descriptor.Character.gameObject != root
                || descriptor.Character.Animator
                    != descriptor.Animator
                || descriptor.Character.VrmInstance
                    != descriptor.VrmInstance
                || descriptor.Animator == null
                || descriptor.VrmInstance == null
                || !descriptor.Animator.enabled
                || !descriptor.VrmInstance.enabled
                || descriptor.Animator.runtimeAnimatorController == null
                || root.GetComponent<MascotExpressionController>() == null
                || root.GetComponent<
                    DesktopMascot.Interaction
                        .MascotInteractionController>() == null)
            {
                return CharacterActivationStatus
                    .BundledCharacterValidationFailed;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return CharacterActivationStatus
                    .BundledCharacterValidationFailed;
            }
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                    return CharacterActivationStatus.Success;
            }
            return CharacterActivationStatus
                .BundledCharacterValidationFailed;
        }

        private static void RestoreCharacterRoots(
            GameObject previousRoot,
            bool previousRootWasActive,
            GameObject bundledRoot,
            bool bundledRootWasActive)
        {
            if (bundledRoot != null)
                bundledRoot.SetActive(bundledRootWasActive);
            if (previousRoot != null)
                previousRoot.SetActive(previousRootWasActive);
        }

        private static CharacterAssetInitializationResult Result(
            CharacterAssetInitializationStatus status)
        {
            return new CharacterAssetInitializationResult(status, null);
        }

        private static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }
    }
}
