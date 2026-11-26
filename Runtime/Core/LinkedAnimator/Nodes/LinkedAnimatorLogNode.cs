using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "Log", menuName = "Honami Animation/Linked Animator Nodes/Log")]
    public sealed class LinkedAnimatorLogNode : HonamiLinkedAnimatorNodeBase
    {
        public string message = "Brain Event";

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            Debug.Log($"[HonamiBrain] {message}");
            return LinkedAnimatorNodeResult.Done;
        }
    }
}




