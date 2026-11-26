using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiBlendTreeEvaluator
    {
        public static void Update(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            Update(btNode, state, mixer, paramValue);
        }

        public static void Update(HonamiBlendTreeNode btNode, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            var motions = btNode.blendMotions;
            if (motions == null || motions.Count == 0) return;
            int count = motions.Count;
            if (count == 1)
            {
                mixer.SetInputWeight(0, 1f);
                UpdateChildSpeeds(motions, state, mixer, GetMotionDuration(motions[0]), btNode.blendType);
                return;
            }

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            for (int i = 0; i < count; i++) mixer.SetInputWeight(i, 0f);

            if (minIdx == -1)
            {
                int idx = paramValue <= motions[0].threshold ? 0 : count - 1;
                mixer.SetInputWeight(idx, 1f);
                UpdateChildSpeeds(motions, state, mixer, GetMotionDuration(motions[idx]), btNode.blendType);
            }
            else
            {
                mixer.SetInputWeight(minIdx, 1f - weight);
                mixer.SetInputWeight(maxIdx, weight);
                float minDur = GetMotionDuration(motions[minIdx]);
                float dur = minDur + (GetMotionDuration(motions[maxIdx]) - minDur) * weight;
                UpdateChildSpeeds(motions, state, mixer, dur, btNode.blendType);
            }
        }

        public static void UpdateWeights(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            UpdateWeightsFromMotions(btNode.blendMotions, mixer, paramValue);
        }

        public static void UpdateWeightsFromMotions(List<HonamiBlendTreeMotion> motions, AnimationMixerPlayable mixer, float paramValue)
        {
            if (motions == null || motions.Count == 0) return;
            int count = motions.Count;
            if (count == 1) { mixer.SetInputWeight(0, 1f); return; }

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            for (int i = 0; i < count; i++) mixer.SetInputWeight(i, 0f);

            if (minIdx == -1) mixer.SetInputWeight(paramValue <= motions[0].threshold ? 0 : count - 1, 1f);
            else { mixer.SetInputWeight(minIdx, 1f - weight); mixer.SetInputWeight(maxIdx, weight); }
        }

        /// <summary>
        /// Per-frame path: resolves the blend bracket once and drives both the input weights and the
        /// child speeds from it, instead of searching the thresholds twice.
        /// </summary>
        public static void UpdateWeightsAndSpeeds(HonamiState state, List<HonamiBlendTreeMotion> motions, AnimationMixerPlayable mixer, float paramValue, HonamiBlendTreeType blendType)
        {
            if (motions == null || motions.Count == 0) return;
            int count = motions.Count;

            if (count == 1)
            {
                mixer.SetInputWeight(0, 1f);
                UpdateChildSpeeds(motions, state, mixer, GetMotionDuration(motions[0]), blendType);
                return;
            }

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);

            float duration;
            if (minIdx == -1)
            {
                int edgeIdx = paramValue <= motions[0].threshold ? 0 : count - 1;
                for (int i = 0; i < count; i++) mixer.SetInputWeight(i, i == edgeIdx ? 1f : 0f);
                duration = GetMotionDuration(motions[edgeIdx]);
            }
            else
            {
                for (int i = 0; i < count; i++)
                    mixer.SetInputWeight(i, i == minIdx ? 1f - weight : i == maxIdx ? weight : 0f);

                float minDur = GetMotionDuration(motions[minIdx]);
                duration = minDur + (GetMotionDuration(motions[maxIdx]) - minDur) * weight;
            }

            UpdateChildSpeeds(motions, state, mixer, duration, blendType);
        }

        public static void UpdateChildSpeedsFromParam(HonamiRuntimeController controller, HonamiState state, AnimationMixerPlayable mixer, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return;
            float dur = GetDurationFromMotions(btNode.blendMotions, paramValue);
            UpdateChildSpeeds(btNode.blendMotions, state, mixer, dur, btNode.blendType);
        }

        public static void UpdateChildSpeedsFromMotions(HonamiState state, List<HonamiBlendTreeMotion> motions, AnimationMixerPlayable mixer, float paramValue, HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D)
        {
            float dur   = GetDurationFromMotions(motions, paramValue);
            UpdateChildSpeeds(motions, state, mixer, dur, blendType);
        }

        private static void UpdateChildSpeeds(List<HonamiBlendTreeMotion> motions, HonamiState state, AnimationMixerPlayable mixer, float avgDur, HonamiBlendTreeType blendType = HonamiBlendTreeType.Standard1D)
        {
            int count = motions.Count;
            for (int i = 0; i < count; i++)
            {
                Playable child = mixer.GetInput(i);
                if (!child.IsValid()) continue;

                HonamiBlendTreeMotion motion = motions[i];
                AnimationClip clip = motion.clip;
                float clipLength = clip != null ? clip.length : 0f;

                float motionSpeed = blendType == HonamiBlendTreeType.Standard1D && avgDur > 0.0001f && clip != null
                    ? clipLength / avgDur
                    : motion.speed;

                if (state.loop && clipLength > 0f)
                {
                    Playable actualCp = child;
                    if (actualCp.GetPlayableType() == typeof(AnimationScriptPlayable) && actualCp.GetInputCount() > 0)
                        actualCp = actualCp.GetInput(0);

                    double t = actualCp.GetTime();
                    if (t >= clipLength || t < 0.0)
                    {
                        t %= clipLength;
                        if (t < 0.0) t += clipLength;
                        actualCp.SetTime(t);
                    }
                }

                child.SetSpeed(motionSpeed);
            }
        }

        public static float GetDuration(HonamiRuntimeController controller, HonamiState state, float paramValue)
        {
            if (controller.GetActiveNode(state) is not HonamiBlendTreeNode btNode) return 1f;
            return GetDurationFromMotions(btNode.blendMotions, paramValue);
        }

        public static float GetDurationFromMotions(List<HonamiBlendTreeMotion> motions, float paramValue)
        {
            if (motions == null || motions.Count == 0) return 1f;
            int count = motions.Count;
            if (count == 1) return GetMotionDuration(motions[0]);

            FindBlendIndices(motions, paramValue, out int minIdx, out int maxIdx, out float weight);
            if (minIdx == -1) return GetMotionDuration(motions[paramValue <= motions[0].threshold ? 0 : count - 1]);

            float d1 = GetMotionDuration(motions[minIdx]);
            float d2 = GetMotionDuration(motions[maxIdx]);
            return d1 + (d2 - d1) * weight;
        }

        private static void FindBlendIndices(List<HonamiBlendTreeMotion> motions, float paramValue,
            out int minIdx, out int maxIdx, out float weight)
        {
            minIdx = -1; maxIdx = -1; weight = 0f;
            int count = motions.Count;

            if (paramValue <= motions[0].threshold || paramValue >= motions[count - 1].threshold) return;

            for (int i = 0; i < count - 1; i++)
            {
                float t1 = motions[i].threshold;
                float t2 = motions[i + 1].threshold;
                if (paramValue >= t1 && paramValue <= t2)
                {
                    minIdx = i; maxIdx = i + 1;
                    weight = t1 == t2 ? 0f : (paramValue - t1) / (t2 - t1);
                    return;
                }
            }
        }

        private static float GetMotionDuration(HonamiBlendTreeMotion m)
        {
            if (m.clip == null) return 0f;
            float s = Mathf.Abs(m.speed);
            return m.clip.length / (s > 0f ? s : 1f);
        }
    }
}
