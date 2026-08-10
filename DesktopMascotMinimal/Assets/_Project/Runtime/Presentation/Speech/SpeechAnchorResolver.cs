using DesktopMascot.Character;
using UnityEngine;

namespace DesktopMascot.Runtime.Presentation.Speech
{
    internal enum SpeechAnchorSource { None, HumanoidBones, RendererBounds }

    internal sealed class SpeechAnchorResolver
    {
        private CharacterAssetManager manager;
        private Animator animator;
        private Transform hips;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftLowerLeg;
        private Transform rightLowerLeg;
        private ulong boundGeneration;
        private int rebindCount;
        private SpeechAnchorSource source;
        private Vector2Int lastResolvedAnchor = new(128, 128);
        private Vector2Int lastHipsAnchor;
        private Vector2Int lastUpperLegAnchor;
        private Vector2Int lastLowerLegAnchor;
        private Vector2Int lastRendererBoundsFallbackAnchor;

        internal int RebindCount => rebindCount;
        internal SpeechAnchorSource Source => source;
        internal ulong CharacterGeneration => boundGeneration;
        internal Vector2Int LastResolvedAnchor => lastResolvedAnchor;
        internal Vector2Int LastHipsAnchor => lastHipsAnchor;
        internal Vector2Int LastUpperLegAnchor => lastUpperLegAnchor;
        internal Vector2Int LastLowerLegAnchor => lastLowerLegAnchor;
        internal Vector2Int LastRendererBoundsFallbackAnchor =>
            lastRendererBoundsFallbackAnchor;

        internal void Configure(CharacterAssetManager characterManager)
        {
            manager = characterManager;
            Rebind();
        }

        internal bool TryResolve(Camera camera, out Vector2Int anchor)
        {
            anchor = new Vector2Int(128, 128);
            if (manager == null || camera == null)
                return false;
            if (manager.ActiveCharacterGeneration != boundGeneration)
                Rebind();
            var rendererBoundsAvailable = TryGetRendererBoundsAnchor(
                camera,
                out var rendererBoundsWorld,
                out lastRendererBoundsFallbackAnchor);
            Vector3 world;
            if (source == SpeechAnchorSource.HumanoidBones
                && hips != null
                && leftUpperLeg != null
                && rightUpperLeg != null)
            {
                var upper = (hips.position
                    + leftUpperLeg.position
                    + rightUpperLeg.position) / 3.0f;
                var lower = leftLowerLeg != null && rightLowerLeg != null
                    ? (leftLowerLeg.position + rightLowerLeg.position) * 0.5f
                    : (leftUpperLeg.position + rightUpperLeg.position) * 0.5f;
                lastHipsAnchor = Project(camera, hips.position);
                lastUpperLegAnchor = Project(camera,
                    (leftUpperLeg.position + rightUpperLeg.position) * 0.5f);
                lastLowerLegAnchor = Project(camera, lower);
                world = Vector3.Lerp(upper, lower, 0.38f);
            }
            else
            {
                if (!rendererBoundsAvailable)
                    return false;
                world = rendererBoundsWorld;
            }
            var viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0)
                return false;
            anchor = new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(viewport.x * 256.0f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt((1.0f - viewport.y) * 256.0f), 0, 255));
            lastResolvedAnchor = anchor;
            return true;
        }

        private void Rebind()
        {
            animator = manager?.ActiveCharacter?.Animator;
            boundGeneration = manager?.ActiveCharacterGeneration ?? 0;
            hips = Bone(HumanBodyBones.Hips);
            leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
            leftLowerLeg = Bone(HumanBodyBones.LeftLowerLeg);
            rightLowerLeg = Bone(HumanBodyBones.RightLowerLeg);
            source = hips != null && leftUpperLeg != null && rightUpperLeg != null
                ? SpeechAnchorSource.HumanoidBones
                : SpeechAnchorSource.RendererBounds;
            ++rebindCount;
        }

        private Transform Bone(HumanBodyBones bone) =>
            animator != null && animator.isHuman
                ? animator.GetBoneTransform(bone)
                : null;

        private bool TryGetRendererBoundsAnchor(
            Camera camera,
            out Vector3 world,
            out Vector2Int projected)
        {
            world = default;
            projected = default;
            var root = manager?.ActiveCharacter?.Root;
            if (root == null)
                return false;
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                return false;
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; ++index)
                bounds.Encapsulate(renderers[index].bounds);
            world = new Vector3(
                bounds.center.x,
                Mathf.Lerp(bounds.min.y, bounds.max.y, 0.38f),
                bounds.center.z);
            var viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0)
                return false;
            projected = Project(camera, world);
            return true;
        }

        private static Vector2Int Project(Camera camera, Vector3 world)
        {
            var viewport = camera.WorldToViewportPoint(world);
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(viewport.x * 256.0f), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt((1.0f - viewport.y) * 256.0f), 0, 255));
        }
    }
}
