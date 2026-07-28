using UnityEngine;
using UniVRM10;

namespace DesktopMascot.Character
{
    internal enum CharacterAssetSourceType
    {
        BundledScene = 1,
        RuntimeImported = 2
    }

    internal sealed class CharacterDescriptor
    {
        internal const string BundledDefaultInternalId =
            "desktop-mascot.bundled-default";

        internal CharacterDescriptor(
            string internalId,
            string displayName,
            CharacterAssetSourceType sourceType,
            MascotCharacter character)
            : this(
                internalId,
                displayName,
                sourceType,
                character,
                character != null ? character.Animator : null,
                character != null ? character.VrmInstance : null)
        {
        }

        internal CharacterDescriptor(
            string internalId,
            string displayName,
            CharacterAssetSourceType sourceType,
            MascotCharacter character,
            Animator animator,
            Vrm10Instance vrmInstance)
        {
            InternalId = internalId;
            DisplayName = displayName;
            SourceType = sourceType;
            Character = character;
            Root = character != null ? character.gameObject : null;
            Animator = animator;
            VrmInstance = vrmInstance;
            RootInstanceId = Root != null ? Root.GetInstanceID() : 0;
        }

        internal string InternalId { get; }
        internal string DisplayName { get; }
        internal CharacterAssetSourceType SourceType { get; }
        internal GameObject Root { get; }
        internal MascotCharacter Character { get; }
        internal Animator Animator { get; }
        internal Vrm10Instance VrmInstance { get; }
        internal int RootInstanceId { get; }

        internal bool IsAvailable =>
            Root != null
            && Root.activeInHierarchy
            && IsStructurallyAvailable;

        internal bool IsStructurallyAvailable =>
            Character != null
            && Animator != null
            && VrmInstance != null;

        internal bool IsVrmRuntimeReady =>
            VrmInstance != null && VrmInstance.Runtime != null;
    }
}
