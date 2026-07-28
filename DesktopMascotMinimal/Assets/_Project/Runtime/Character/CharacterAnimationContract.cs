using UnityEngine;

namespace DesktopMascot.Character
{
    internal static class CharacterAnimationContract
    {
        internal const string StandingIdleStateName =
            "Base Layer.Standing Idle";

        internal static readonly int StandingIdleStateHash =
            Animator.StringToHash(StandingIdleStateName);
    }
}
