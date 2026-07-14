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

        public virtual double GetCurrentTime(HonamiState state, int stateIndex, Playable playable)
            => playable.GetTime();

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
    }
}
