using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum LinkedAnimatorParameterAction
    {
        SetFloat,
        SetInteger,
        SetBool,
        SetTrigger,
        ResetTrigger
    }

    [CreateAssetMenu(fileName = "SetParameter", menuName = "Honami Animation/Linked Animator Nodes/Set Parameter")]
    public sealed class LinkedAnimatorSetParameterNode : HonamiLinkedAnimatorNodeBase
    {
        public string parameterName;
        public LinkedAnimatorParameterAction action = LinkedAnimatorParameterAction.SetFloat;
        public float floatValue;
        public int intValue;
        public bool boolValue;

        public override LinkedAnimatorNodeResult Execute(HonamiLinkedAnimatorContext ctx)
        {
            if (string.IsNullOrEmpty(parameterName) || ctx.Brain == null) return LinkedAnimatorNodeResult.Done;

            switch (action)
            {
                case LinkedAnimatorParameterAction.SetFloat:
                    ctx.Brain.SetFloat(parameterName, floatValue);
                    break;
                case LinkedAnimatorParameterAction.SetInteger:
                    ctx.Brain.SetInteger(parameterName, intValue);
                    break;
                case LinkedAnimatorParameterAction.SetBool:
                    ctx.Brain.SetBool(parameterName, boolValue);
                    break;
                case LinkedAnimatorParameterAction.SetTrigger:
                    ctx.Brain.SetTrigger(parameterName);
                    break;
                case LinkedAnimatorParameterAction.ResetTrigger:
                    ctx.Brain.ResetTrigger(parameterName);
                    break;
            }

            return LinkedAnimatorNodeResult.Done;
        }
    }
}




