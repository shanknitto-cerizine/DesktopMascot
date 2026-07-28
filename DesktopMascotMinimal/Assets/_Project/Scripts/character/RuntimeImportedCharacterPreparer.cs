using System;
using DesktopMascot.Interaction;
using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace DesktopMascot.Character
{
    internal enum RuntimeCharacterPreparationStatus
    {
        Success = 0,
        InvalidImportResult = 1,
        MissingBundledDescriptor = 2,
        MissingAnimator = 3,
        MissingAvatar = 4,
        InvalidAvatar = 5,
        NonHumanoidAnimator = 6,
        MissingAnimatorController = 7,
        MissingStandingIdleState = 8,
        MissingVrm10Instance = 9,
        MissingRuntimeGltfInstance = 10,
        MissingCamera = 11,
        InvalidRendererBounds = 12,
        OutsideCameraFrustum = 13,
        ComponentPreparationFailed = 14
    }

    internal sealed class PreparedRuntimeCharacter
    {
        private readonly RuntimeAnimatorController previousController;
        private bool activated;
        private bool rollbackCompleted;

        internal PreparedRuntimeCharacter(
            RuntimeVrmCharacterSource source,
            RuntimeGltfInstance runtimeInstance,
            CharacterDescriptor descriptor,
            RuntimeAnimatorController replacedController,
            int standingIdleStateHash)
        {
            Source = source;
            RuntimeInstance = runtimeInstance;
            Descriptor = descriptor;
            previousController = replacedController;
            StandingIdleStateHash = standingIdleStateHash;
        }

        internal RuntimeVrmCharacterSource Source { get; }
        internal RuntimeGltfInstance RuntimeInstance { get; }
        internal CharacterDescriptor Descriptor { get; }
        internal int StandingIdleStateHash { get; }
        internal bool Activated => activated;

        internal void MarkActivated()
        {
            activated = true;
        }

        internal void Rollback()
        {
            if (activated || rollbackCompleted)
                return;
            rollbackCompleted = true;
            if (Descriptor?.Animator != null)
            {
                Descriptor.Animator.runtimeAnimatorController =
                    previousController;
            }
            Source?.Cleanup();
        }
    }

    internal readonly struct RuntimeCharacterPreparationResult
    {
        internal RuntimeCharacterPreparationResult(
            RuntimeCharacterPreparationStatus status,
            PreparedRuntimeCharacter prepared,
            string detail)
        {
            Status = status;
            Prepared = prepared;
            Detail = detail;
        }

        internal RuntimeCharacterPreparationStatus Status { get; }
        internal PreparedRuntimeCharacter Prepared { get; }
        internal string Detail { get; }
        internal bool Succeeded =>
            Status == RuntimeCharacterPreparationStatus.Success
            && Prepared != null;
    }

    internal static class RuntimeImportedCharacterPreparer
    {
        private const string Prefix =
            "[DesktopMascotRuntimeVrmImport]";
        internal static RuntimeCharacterPreparationResult Prepare(
            RuntimeVrmCharacterSource source,
            RuntimeVrmImportResult importResult,
            CharacterDescriptor bundledDescriptor,
            Camera targetCamera)
        {
            if (source == null || !importResult.Succeeded)
                return Failure(
                    RuntimeCharacterPreparationStatus.InvalidImportResult);
            if (bundledDescriptor == null
                || !bundledDescriptor.IsAvailable)
            {
                return Failure(
                    RuntimeCharacterPreparationStatus
                        .MissingBundledDescriptor);
            }
            if (targetCamera == null)
                return Failure(
                    RuntimeCharacterPreparationStatus.MissingCamera);

            var vrmInstance = importResult.VrmInstance;
            var runtimeInstance = importResult.RuntimeInstance;
            var root = vrmInstance != null
                ? vrmInstance.gameObject
                : null;
            if (vrmInstance == null || root == null)
                return Failure(
                    RuntimeCharacterPreparationStatus
                        .MissingVrm10Instance);
            if (runtimeInstance == null
                || runtimeInstance.Root != root)
            {
                return Failure(
                    RuntimeCharacterPreparationStatus
                        .MissingRuntimeGltfInstance);
            }

            var animator = root.GetComponent<Animator>();
            if (animator == null)
                return Failure(
                    RuntimeCharacterPreparationStatus.MissingAnimator);
            if (animator.avatar == null)
                return Failure(
                    RuntimeCharacterPreparationStatus.MissingAvatar);
            if (!animator.avatar.isValid)
                return Failure(
                    RuntimeCharacterPreparationStatus.InvalidAvatar);
            if (!animator.isHuman)
                return Failure(
                    RuntimeCharacterPreparationStatus.NonHumanoidAnimator);

            var controller =
                bundledDescriptor.Animator.runtimeAnimatorController;
            if (controller == null)
            {
                return Failure(
                    RuntimeCharacterPreparationStatus
                        .MissingAnimatorController);
            }

            var previousController = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = controller;
            var idleHash =
                CharacterAnimationContract.StandingIdleStateHash;
            if (!bundledDescriptor.Animator.HasState(0, idleHash))
            {
                animator.runtimeAnimatorController = previousController;
                return Failure(
                    RuntimeCharacterPreparationStatus
                        .MissingStandingIdleState);
            }

            try
            {
                ApplyBundledTransform(
                    root.transform,
                    bundledDescriptor.Root.transform);

                if (!TryCalculateBounds(
                        root.transform,
                        out var worldBounds,
                        out var localBounds))
                {
                    animator.runtimeAnimatorController =
                        previousController;
                    return Failure(
                        RuntimeCharacterPreparationStatus
                            .InvalidRendererBounds);
                }
                if (!GeometryUtility.TestPlanesAABB(
                        GeometryUtility.CalculateFrustumPlanes(
                            targetCamera),
                        worldBounds))
                {
                    animator.runtimeAnimatorController =
                        previousController;
                    return Failure(
                        RuntimeCharacterPreparationStatus
                            .OutsideCameraFrustum);
                }

                var character =
                    root.GetComponent<MascotCharacter>()
                    ?? root.AddComponent<MascotCharacter>();
                character.FindComponents();
                if (character.VrmInstance != vrmInstance
                    || character.Animator != animator)
                {
                    animator.runtimeAnimatorController =
                        previousController;
                    return Failure(
                        RuntimeCharacterPreparationStatus
                            .ComponentPreparationFailed);
                }

                _ = root.GetComponent<MascotExpressionController>()
                    ?? root.AddComponent<MascotExpressionController>();
                _ = root.GetComponent<MascotDragController>()
                    ?? root.AddComponent<MascotDragController>();
                var collider = root.GetComponent<BoxCollider>()
                    ?? root.AddComponent<BoxCollider>();
                collider.center = localBounds.center;
                collider.size = localBounds.size;
                _ = root.GetComponent<MascotInteractionController>()
                    ?? root.AddComponent<MascotInteractionController>();

                var descriptor = new CharacterDescriptor(
                    importResult.StableSourceId,
                    importResult.DisplayName,
                    CharacterAssetSourceType.RuntimeImported,
                    character,
                    animator,
                    vrmInstance);
                if (!descriptor.IsStructurallyAvailable)
                {
                    animator.runtimeAnimatorController =
                        previousController;
                    return Failure(
                        RuntimeCharacterPreparationStatus
                            .ComponentPreparationFailed);
                }

                Debug.Log(
                    $"{Prefix} Preparation completed: True");
                return new RuntimeCharacterPreparationResult(
                    RuntimeCharacterPreparationStatus.Success,
                    new PreparedRuntimeCharacter(
                        source,
                        runtimeInstance,
                        descriptor,
                        previousController,
                        idleHash),
                    string.Empty);
            }
            catch (Exception exception)
            {
                animator.runtimeAnimatorController = previousController;
                Debug.LogError(
                    $"{Prefix} Preparation exception type/status: " +
                    $"{exception.GetType().Name}/" +
                    RuntimeCharacterPreparationStatus
                        .ComponentPreparationFailed);
                return new RuntimeCharacterPreparationResult(
                    RuntimeCharacterPreparationStatus
                        .ComponentPreparationFailed,
                    null,
                    exception.GetType().Name);
            }
        }

        private static void ApplyBundledTransform(
            Transform imported,
            Transform bundled)
        {
            imported.SetPositionAndRotation(
                bundled.position,
                bundled.rotation);
            imported.localScale = bundled.localScale;
        }

        private static bool TryCalculateBounds(
            Transform root,
            out Bounds worldBounds,
            out Bounds localBounds)
        {
            worldBounds = default;
            localBounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var initialized = false;
            var localInitialized = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var bounds = renderer.bounds;
                if (!IsFinite(bounds.center)
                    || !IsFinite(bounds.size)
                    || bounds.size.sqrMagnitude <= Mathf.Epsilon)
                {
                    continue;
                }

                if (!initialized)
                {
                    worldBounds = bounds;
                    initialized = true;
                }
                else
                {
                    worldBounds.Encapsulate(bounds);
                }

                foreach (var corner in EnumerateCorners(bounds))
                {
                    var local = root.InverseTransformPoint(corner);
                    if (!IsFinite(local))
                        return false;
                    if (!localInitialized)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        localInitialized = true;
                    }
                    else
                        localBounds.Encapsulate(local);
                }
            }

            return initialized
                && IsFinite(worldBounds.center)
                && IsFinite(worldBounds.size)
                && worldBounds.size.sqrMagnitude > Mathf.Epsilon
                && IsFinite(localBounds.center)
                && IsFinite(localBounds.size)
                && localInitialized
                && localBounds.size.sqrMagnitude > Mathf.Epsilon;
        }

        private static Vector3[] EnumerateCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static RuntimeCharacterPreparationResult Failure(
            RuntimeCharacterPreparationStatus status)
        {
            Debug.LogError($"{Prefix} Preparation failed: {status}");
            return new RuntimeCharacterPreparationResult(
                status,
                null,
                string.Empty);
        }
    }
}
