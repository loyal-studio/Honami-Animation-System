using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum BrainConditionType
    {
        FloatGreater,
        FloatLess,
        IntEquals,
        IntNotEqual,
        BoolTrue,
        BoolFalse
    }

    [CreateAssetMenu(fileName = "Condition", menuName = "Honami Animation/Linked Animator Nodes/Condition")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom("BrainConditionNode")]
    public sealed class LinkedAnimatorConditionNode : HonamiLinkedAnimatorNodeBase
    {
        public string parameterName;
        public BrainConditionType conditionType = BrainConditionType.BoolTrue;
        public float floatThreshold;
        public int intThreshold;

        public HonamiLinkedAnimatorNodeBase onTrue;
        public HonamiLinkedAnimatorNodeBase onFalse;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            return LinkedAnimatorNodeResult.Done;
        }

        public HonamiLinkedAnimatorNodeBase GetResultBranch(HonamiLinkedAnimatorContext ctx)
        {
            return Evaluate(ctx) ? onTrue : onFalse;
        }

        private bool Evaluate(HonamiLinkedAnimatorContext ctx)
        {
            if (string.IsNullOrEmpty(parameterName)) return false;

            if (ctx.FullAnimators == null) return false;

            foreach (var anim in ctx.FullAnimators)
            {
                if (anim == null) continue;

                switch (conditionType)
                {
                    case BrainConditionType.FloatGreater:
                        return anim.GetFloat(parameterName) > floatThreshold;
                    case BrainConditionType.FloatLess:
                        return anim.GetFloat(parameterName) < floatThreshold;
                    case BrainConditionType.IntEquals:
                        return anim.GetInteger(parameterName) == intThreshold;
                    case BrainConditionType.IntNotEqual:
                        return anim.GetInteger(parameterName) != intThreshold;
                    case BrainConditionType.BoolTrue:
                        return anim.GetBool(parameterName);
                    case BrainConditionType.BoolFalse:
                        return !anim.GetBool(parameterName);
                }
                break;
            }

            return false;
        }

        public override void Reset()
        {
            if (onTrue != null) onTrue.Reset();
            if (onFalse != null) onFalse.Reset();
        }
    }
}




