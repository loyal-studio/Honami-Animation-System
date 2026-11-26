using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Base asset type for graph nodes that create and update playable state output.
    /// </summary>
    public abstract class HonamiNodeBase : ScriptableObject
    {
        [Tooltip("If true, non-deterministic node behaviors will be synchronized across linked animators during the exact same frame.")]
        public bool syncWhenLinked;

        public virtual bool IsVirtual => false;
        public virtual bool IsGlobal => false;
        public virtual bool IsExit => false;

        public abstract Playable CreatePlayable(PlayableGraph graph, HonamiState state);

        public virtual Playable CreatePlayable(PlayableGraph graph, HonamiState state, Func<PlayableGraph, Playable> mirrorFactory)
            => CreatePlayable(graph, state);

        public abstract float GetDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam);

        public virtual float GetUnscaledDuration(HonamiState state, int stateIndex, HonamiNodeRuntime runtime, float blendParam)
            => GetDuration(state, stateIndex, runtime, blendParam);

        public abstract void UpdateRuntime(in HonamiExecutionContext ctx);

        public virtual void OnEnter(in HonamiExecutionContext ctx)
        {
        }

        public virtual void OnExit(in HonamiExecutionContext ctx)
        {
        }

        public virtual HonamiNodeRuntime CreateRuntime() => null;

        public virtual void OnBuildMetadata(int stateIndex, HonamiState state, List<int> anyStateIndices)
        {
        }

        // If true, transitions fired from this global node force-restart their target even mid-transition.
        public virtual bool ForcesRestartOnGlobalFire => false;

        public virtual bool CanFireGlobal(HonamiNodeRuntime runtime) => true;

        public virtual bool SelectGlobalTransition(
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
            return true;
        }

        public virtual void OnGlobalFired(HonamiNodeRuntime runtime, int transitionCount)
        {
        }

        public virtual string GetBlendParameterName() => null;

        public virtual void OnValidateNode()
        {
        }

        public virtual int MotionCount => 0;

        // Bound modules store this id, so it has to survive reordering and stay unique within the node.
        public virtual string GetMotionId(int index) => null;

        public virtual string GetMotionName(int index) => null;

        public virtual float GetMotionStart(int index) => 0f;

        public virtual float GetMotionDuration(int index) => float.PositiveInfinity;

        public virtual bool CanHostModule(int index, Type moduleType) => true;

        public virtual bool IsMotionPlaying(int index, HonamiNodeRuntime runtime) => true;

        public virtual bool EnsureMotionIds() => false;

        public int FindMotion(string motionId)
        {
            if (string.IsNullOrEmpty(motionId)) return -1;
            int count = MotionCount;
            for (int i = 0; i < count; i++)
            {
                if (GetMotionId(i) == motionId) return i;
            }
            return -1;
        }

        public static string NewMotionId() => Guid.NewGuid().ToString("N");

        protected static bool EnsureUniqueMotionIds<T>(List<T> motions, Func<T, string> getId, Action<T, string> setId) where T : class
        {
            if (motions == null) return false;

            bool changed = false;
            var seen = new HashSet<string>();
            for (int i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion == null) continue;

                string id = getId(motion);
                if (!string.IsNullOrEmpty(id) && seen.Add(id)) continue;

                id = NewMotionId();
                setId(motion, id);
                seen.Add(id);
                changed = true;
            }
            return changed;
        }
    }
}
