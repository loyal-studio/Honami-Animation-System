using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Virtual global node that allows a state to re-fire repeatedly with cooldown limits.
    /// </summary>
    public sealed class HonamiRepeaterNode : HonamiNodeBase
    {
        [Tooltip("Minimum time (in seconds) between repeats.")]
        public float repeatCooldown = 0.1f;

        [Tooltip("Max repeats. 0 = infinite.")]
        public int maxRepeats = 0;

        [Tooltip("Each fire advances to the next outgoing transition in order, forming an attack combo chain.")]
        public bool comboMode;

        [Tooltip("Normalized time of the current combo attack before which a new fire is buffered instead of interrupting it. 0 = interrupt instantly.")]
        [Range(0f, 1f)]
        public float cancelWindow = 0.6f;

        [Tooltip("Seconds without a fire after which the combo returns to the first attack. 0 = never reset by time.")]
        public float comboResetTime = 1f;

        [Tooltip("If true the combo wraps to the first attack after the last one; otherwise it locks until Combo Reset Time passes.")]
        public bool loopCombo = true;

        public sealed class Runtime : HonamiNodeRuntime
        {
            public double LastFireTime = -999.0;
            public int FireCount;
            public int ComboStep;
            public bool HasBufferedFire;

            public override void Reset()
            {
                LastFireTime = -999.0;
                FireCount = 0;
                ComboStep = 0;
                HasBufferedFire = false;
            }
        }

        public override bool IsVirtual => true;
        public override bool IsGlobal => true;

        public override Playable CreatePlayable(PlayableGraph graph, HonamiState state) => Playable.Null;

        public override float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam) => 0f;

        public override void UpdateRuntime(in HonamiExecutionContext ctx)
        {
        }

        public override HonamiNodeRuntime CreateRuntime() => new Runtime();

        public override void OnBuildMetadata(int stateIndex, HonamiState state, List<int> anyStateIndices)
        {
            anyStateIndices.Add(stateIndex);
        }

        public override bool ForcesRestartOnGlobalFire => true;

        public override bool CanFireGlobal(HonamiNodeRuntime runtime)
        {
            if (runtime is not Runtime repeaterRuntime) return false;
            if (Time.timeAsDouble - repeaterRuntime.LastFireTime < repeatCooldown) return false;
            return maxRepeats <= 0 || repeaterRuntime.FireCount < maxRepeats;
        }

        public override bool SelectGlobalTransition(
            HonamiAnimator animator,
            int layer,
            int currentStateIndex,
            HonamiNodeRuntime runtime,
            IReadOnlyList<HonamiTransition> transitions,
            out int transitionIndex,
            out bool forceConditionsMet)
        {
            transitionIndex = -1;
            forceConditionsMet = false;

            if (!comboMode) return true;
            if (runtime is not Runtime repeaterRuntime) return false;

            return TryResolveComboFire(animator, layer, currentStateIndex, repeaterRuntime, transitions, out transitionIndex, out forceConditionsMet);
        }

        public override void OnGlobalFired(HonamiNodeRuntime runtime, int transitionCount)
        {
            if (runtime is not Runtime repeaterRuntime) return;

            repeaterRuntime.LastFireTime = Time.timeAsDouble;
            repeaterRuntime.FireCount++;

            if (!comboMode || transitionCount <= 0) return;

            int step = repeaterRuntime.ComboStep + 1;
            if (step >= transitionCount)
                step = loopCombo ? 0 : transitionCount;
            repeaterRuntime.ComboStep = step;
        }

        private bool TryResolveComboFire(HonamiAnimator animator, int layer, int currentStateIndex, Runtime runtime, IReadOnlyList<HonamiTransition> transitions, out int transitionIndex, out bool bufferedFire)
        {
            transitionIndex = -1;
            bufferedFire = false;

            if (runtime.ComboStep != 0 && comboResetTime > 0f && Time.timeAsDouble - runtime.LastFireTime >= comboResetTime)
            {
                runtime.ComboStep = 0;
                runtime.HasBufferedFire = false;
            }

            // ComboStep == transitions.Count marks a finished non-looping combo waiting for the reset timeout.
            if (runtime.ComboStep >= transitions.Count)
            {
                runtime.HasBufferedFire = false;
                return false;
            }

            HonamiTransition stepTransition = transitions[runtime.ComboStep];
            if (stepTransition == null) return false;

            bool pressed = AreConditionsMetTentative(animator, stepTransition);
            bool windowLocked = IsInsideCancelLock(animator, layer, currentStateIndex, transitions, out bool currentIsComboTarget);

            if (pressed)
            {
                if (windowLocked)
                {
                    // Triggers are cleared every frame, so the press is remembered here until the cancel window opens.
                    runtime.HasBufferedFire = true;
                    return false;
                }

                runtime.HasBufferedFire = false;
                transitionIndex = runtime.ComboStep;
                return true;
            }

            if (runtime.HasBufferedFire)
            {
                if (!currentIsComboTarget)
                {
                    runtime.HasBufferedFire = false;
                    return false;
                }

                if (!windowLocked)
                {
                    runtime.HasBufferedFire = false;
                    transitionIndex = runtime.ComboStep;
                    bufferedFire = true;
                    return true;
                }
            }

            return false;
        }

        private static bool AreConditionsMetTentative(HonamiAnimator animator, HonamiTransition transition)
        {
            if (transition.conditions == null || transition.conditions.Count == 0) return false;

            animator._tentativeBuffer.Clear();
            int count = transition.conditions.Count;
            for (int i = 0; i < count; i++)
            {
                if (!animator.EvaluateConditionTentative(transition.conditions[i], animator._tentativeBuffer)) return false;
            }
            return true;
        }

        private bool IsInsideCancelLock(HonamiAnimator animator, int layer, int currentStateIndex, IReadOnlyList<HonamiTransition> transitions, out bool currentIsComboTarget)
        {
            currentIsComboTarget = false;
            if (currentStateIndex < 0) return false;

            int trCount = transitions.Count;
            for (int i = 0; i < trCount; i++)
            {
                var tr = transitions[i];
                if (tr == null || !tr.hasTargetGuid) continue;
                if (animator._stateGuidToIndex.TryGetValue(tr.targetStateGuidHash, out int targetIdx) && targetIdx == currentStateIndex)
                {
                    currentIsComboTarget = true;
                    break;
                }
            }
            if (!currentIsComboTarget || cancelWindow <= 0f) return false;

            var playable = animator._layerMixers[layer].GetInput(currentStateIndex);
            if (!playable.IsValid()) return false;

            var currentState = animator._runtimeStates[currentStateIndex];
            float unscaledDuration = HonamiStateEvaluator.GetUnscaledStateDuration(
                animator.controller, currentState, currentStateIndex,
                animator.GetNodeRuntime(currentStateIndex), animator.GetStateBlendParam(currentState));
            if (unscaledDuration <= 0f) return false;

            float normalizedTime = (float)(playable.GetTime() / unscaledDuration);
            float progress = Mathf.Clamp01(currentState.isReversed ? 1f - normalizedTime : normalizedTime);
            return progress < cancelWindow;
        }
    }
}
