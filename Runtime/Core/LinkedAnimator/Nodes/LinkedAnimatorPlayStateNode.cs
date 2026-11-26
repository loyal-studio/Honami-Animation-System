using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "PlayState", menuName = "Honami Animation/Linked Animator Nodes/Play State")]
    public sealed class LinkedAnimatorPlayStateNode : HonamiLinkedAnimatorNodeBase
    {
        public string stateName;
        public float transitionDuration = 0.25f;
        public HonamiBroadcastTargetMode targetMode = HonamiBroadcastTargetMode.AllLinked;
        public HonamiTagID targetTag;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            if (string.IsNullOrEmpty(stateName) || ctx.Brain == null) return LinkedAnimatorNodeResult.Done;

            ctx.Brain.PlayState(stateName, transitionDuration, new HonamiLinkedTarget(targetMode, targetTag));
            return LinkedAnimatorNodeResult.Done;
        }
    }
}




