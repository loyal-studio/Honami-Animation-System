using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    [CreateAssetMenu(fileName = "Wait", menuName = "Honami Animation/Linked Animator Nodes/Wait")]
    public sealed class LinkedAnimatorWaitNode : HonamiLinkedAnimatorNodeBase
    {
        public float duration = 1f;
        private float _elapsed;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            _elapsed += ctx.DeltaTime;
            return _elapsed >= duration ? LinkedAnimatorNodeResult.Done : LinkedAnimatorNodeResult.Running;
        }

        public override void OnBegin(HonamiLinkedAnimatorContext ctx)
        {
            _elapsed = 0f;
        }

        public override void Reset()
        {
            _elapsed = 0f;
        }
    }
}




