using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "BroadcastAction", menuName = "Honami Animation/Linked Animator Nodes/Broadcast Action")]
    public sealed class LinkedAnimatorBroadcastActionNode : HonamiLinkedAnimatorNodeBase
    {
        public HonamiActionID actionId;
        public float transitionDuration = 0.25f;
        public HonamiBroadcastTargetMode targetMode = HonamiBroadcastTargetMode.AllLinked;
        public HonamiTagID targetTag;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            if (actionId == null || ctx.Brain == null) return LinkedAnimatorNodeResult.Done;

            ctx.Brain.SetActionID(actionId, transitionDuration, new HonamiLinkedTarget(targetMode, targetTag));

            return LinkedAnimatorNodeResult.Done;
        }
    }
}




