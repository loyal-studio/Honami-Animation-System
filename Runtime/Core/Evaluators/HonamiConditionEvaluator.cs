using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiConditionEvaluator
    {
        public static bool EvaluateConditionTentative(HonamiCondition cond, HonamiParameterStore paramStore, int paramHash, List<int> triggersTentative)
        {
            switch (cond.mode)
            {
                case HonamiConditionMode.If:
                    if (paramStore.TryGetBoolIndex(paramHash, out int boolIdx) && paramStore.GetBoolByIndex(boolIdx)) return true;
                    if (paramStore.TryGetTriggerIndex(paramHash, out int trigIdx) && paramStore.GetTriggerByIndex(trigIdx))
                    {
                        if (!triggersTentative.Contains(paramHash)) triggersTentative.Add(paramHash);
                        return true;
                    }
                    return false;

                case HonamiConditionMode.IfNot:
                    if (paramStore.TryGetBoolIndex(paramHash, out int boolIdxN)) return !paramStore.GetBoolByIndex(boolIdxN);
                    if (paramStore.TryGetTriggerIndex(paramHash, out int trigIdxN)) return !paramStore.GetTriggerByIndex(trigIdxN);
                    return false;

                case HonamiConditionMode.Greater:
                    if (paramStore.TryGetFloatIndex(paramHash, out int fIdxG)) return paramStore.GetFloatByIndex(fIdxG) > cond.threshold;
                    if (paramStore.TryGetIntIndex(paramHash, out int iIdxG)) return paramStore.GetIntByIndex(iIdxG) > (int)cond.threshold;
                    break;

                case HonamiConditionMode.Less:
                    if (paramStore.TryGetFloatIndex(paramHash, out int fIdxL)) return paramStore.GetFloatByIndex(fIdxL) < cond.threshold;
                    if (paramStore.TryGetIntIndex(paramHash, out int iIdxL)) return paramStore.GetIntByIndex(iIdxL) < (int)cond.threshold;
                    break;

                case HonamiConditionMode.Equals:
                    if (paramStore.TryGetFloatIndex(paramHash, out int fIdxE)) return Mathf.Approximately(paramStore.GetFloatByIndex(fIdxE), cond.threshold);
                    if (paramStore.TryGetIntIndex(paramHash, out int iIdxE)) return paramStore.GetIntByIndex(iIdxE) == (int)cond.threshold;
                    break;

                case HonamiConditionMode.NotEqual:
                    if (paramStore.TryGetFloatIndex(paramHash, out int fIdxNE)) return !Mathf.Approximately(paramStore.GetFloatByIndex(fIdxNE), cond.threshold);
                    if (paramStore.TryGetIntIndex(paramHash, out int iIdxNE)) return paramStore.GetIntByIndex(iIdxNE) != (int)cond.threshold;
                    break;
            }
            return false;
        }

        public static void ApplyParameterAssignments(HonamiState state, bool onExit, HonamiParameterStore paramStore, Dictionary<HonamiParameterAssignment, int> assignmentParamHashes)
        {
            if (state?.parameterAssignments == null) return;
            int count = state.parameterAssignments.Count;
            for (int i = 0; i < count; i++)
            {
                var assignment = state.parameterAssignments[i];
                if (assignment.executeOnExit == onExit)
                    paramStore.ApplyAssignment(assignment, assignmentParamHashes);
            }
        }

        public static void ApplyTransitionAssignments(HonamiTransition transition, HonamiParameterStore paramStore, Dictionary<HonamiParameterAssignment, int> assignmentParamHashes)
        {
            if (transition?.parameterAssignments == null) return;
            int count = transition.parameterAssignments.Count;
            for (int i = 0; i < count; i++)
                paramStore.ApplyAssignment(transition.parameterAssignments[i], assignmentParamHashes);
        }
    }
}
