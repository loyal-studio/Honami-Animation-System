using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    /// <summary>
    /// Collects the <see cref="HonamiRig"/> components under one animator and drives them, either as
    /// playables spliced into the animation graph or, for rigs that produce no playable, as a
    /// post-evaluation pass.
    /// </summary>
    [AddComponentMenu("Honami Animation/Honami Rigging Processor")]
    [ExecuteAlways]
    public sealed class HonamiRiggingProcessor : MonoBehaviour
    {
        private readonly List<HonamiRig> _rigs = new();
        private readonly List<HonamiRig> _graphBoundRigs = new();
        private readonly List<HonamiRig> _postEvaluationRigs = new();

        private bool _boundToGraph;
        private static readonly List<HonamiRig> s_RigBuffer = new();

        private readonly List<HonamiRig> _sweepBuffer = new();
        private int _sweepDepth;

        // Rig callbacks are user code and may enable, disable or reparent rigs, which mutates the very
        // list being walked. Every driving loop iterates a snapshot instead; only the outermost sweep
        // owns the reusable buffer, so a nested one allocates rather than corrupting the outer walk.
        private List<HonamiRig> BeginSweep(List<HonamiRig> source)
        {
            List<HonamiRig> buffer = _sweepDepth == 0 ? _sweepBuffer : new List<HonamiRig>(source.Count);
            buffer.Clear();
            buffer.AddRange(source);
            _sweepDepth++;
            return buffer;
        }

        private void EndSweep(List<HonamiRig> buffer)
        {
            buffer.Clear();
            _sweepDepth--;
        }

        /// <summary>
        /// Every rig this processor owns, in evaluation order.
        /// </summary>
        public IReadOnlyList<HonamiRig> Rigs => _rigs;

        /// <summary>
        /// The rigs that produced an animation playable and run inside the graph.
        /// </summary>
        public IReadOnlyList<HonamiRig> GraphBoundRigs => _graphBoundRigs;

        /// <summary>
        /// The rigs with no playable, driven from <see cref="ProcessPostEvaluationRigs"/> after the graph evaluates.
        /// </summary>
        public IReadOnlyList<HonamiRig> PostEvaluationRigs => _postEvaluationRigs;

        /// <summary>
        /// True once an animator has spliced this processor into its playable graph.
        /// </summary>
        public bool IsBoundToGraph => _boundToGraph;

        private void OnEnable()
        {
            RefreshRigs();
        }

        private static int IndexOfRig(List<HonamiRig> rigs, HonamiRig rig)
        {
            for (int i = 0; i < rigs.Count; i++)
            {
                if (ReferenceEquals(rigs[i], rig)) return i;
            }

            return -1;
        }

        private static void RemoveRig(List<HonamiRig> rigs, HonamiRig rig)
        {
            int index = IndexOfRig(rigs, rig);
            if (index >= 0) rigs.RemoveAt(index);
        }

        public void RegisterRig(HonamiRig rig)
        {
            if (rig == null || IndexOfRig(_rigs, rig) >= 0) return;

            _rigs.Add(rig);
            rig.ResetRig();

            if (_boundToGraph && IndexOfRig(_postEvaluationRigs, rig) < 0 && IndexOfRig(_graphBoundRigs, rig) < 0)
            {
                _postEvaluationRigs.Add(rig);
            }
        }

        // ReferenceEquals, not Unity's ==: a rig already torn down on the native side still has to be
        // pulled out of these lists, and the == overload would report it as null and skip the removal.
        public void UnregisterRig(HonamiRig rig)
        {
            if (ReferenceEquals(rig, null)) return;

            RemoveRig(_rigs, rig);
            RemoveRig(_graphBoundRigs, rig);
            RemoveRig(_postEvaluationRigs, rig);
        }

        private void LateUpdate()
        {
            if (_boundToGraph && Application.isPlaying) return;

            float deltaTime = Time.deltaTime;
            List<HonamiRig> sweep = BeginSweep(_rigs);

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiRig rig = sweep[i];
                    if (rig && rig.enabled && rig.gameObject.activeInHierarchy)
                        rig.ProcessRig(deltaTime);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        private void OnDestroy()
        {
            DisposeAllJobs();
        }

        public Playable InsertIntoGraph(Animator animator, PlayableGraph graph, Playable previousPlayable)
        {
            DisposeAllJobs();
            RefreshRigs();

            Playable current = previousPlayable;
            List<HonamiRig> sweep = BeginSweep(_rigs);

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiRig rig = sweep[i];
                    if (rig == null || !rig.gameObject.activeInHierarchy) continue;

                    Playable rigPlayable = rig.CreatePlayable(animator, graph);
                    if (!rigPlayable.IsValid())
                    {
                        _postEvaluationRigs.Add(rig);
                        continue;
                    }

                    graph.Connect(current, 0, rigPlayable, 0);
                    rigPlayable.SetInputWeight(0, 1f);
                    current = rigPlayable;

                    _graphBoundRigs.Add(rig);
                }
            }
            finally
            {
                EndSweep(sweep);
            }

            // the animator drives every rig from here on, even when none of them produced a playable -
            // letting LateUpdate run them too would tick stateful rigs twice per frame
            _boundToGraph = true;
            return current;
        }

        public void PrepareAllRigs(float deltaTime)
        {
            List<HonamiRig> sweep = BeginSweep(_graphBoundRigs);

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiRig rig = sweep[i];
                    if (rig != null) rig.PrepareJobData(deltaTime);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void ProcessPostEvaluationRigs(float deltaTime)
        {
            List<HonamiRig> sweep = BeginSweep(_postEvaluationRigs);

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiRig rig = sweep[i];
                    if (rig != null && rig.enabled) rig.ProcessRig(deltaTime);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void RefreshRigs()
        {
            if (!this) return;

            s_RigBuffer.Clear();
            GetComponentsInChildren<HonamiRig>(true, s_RigBuffer);

            _rigs.Clear();
            for (int i = 0; i < s_RigBuffer.Count; i++)
            {
                HonamiRig rig = s_RigBuffer[i];
                if (rig != null && IsMyRig(rig)) _rigs.Add(rig);
            }

            s_RigBuffer.Clear();

            // Second pass only after the shared scan buffer is released: ResetRig and EarlyInit run
            // user code, and a nested RefreshRigs would otherwise walk a buffer we are still reading.
            List<HonamiRig> sweep = BeginSweep(_rigs);

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiRig rig = sweep[i];
                    if (rig == null) continue;

                    rig.ResetRig();
                    if (rig is IEarlyInitRig earlyInit) earlyInit.EarlyInit();
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        // A nested processor or animator claims the rigs below it, so stop the walk there.
        private bool IsMyRig(HonamiRig rig)
        {
            Transform current = rig.transform;
            while (current != null)
            {
                if (current == transform) return true;
                if (current.TryGetComponent<HonamiRiggingProcessor>(out _) || current.TryGetComponent<HonamiAnimatorBase>(out _)) return false;
                current = current.parent;
            }

            return false;
        }

        private void DisposeAllJobs()
        {
            List<HonamiRig> sweep = BeginSweep(_graphBoundRigs);

            try
            {
                // ReferenceEquals: a destroyed rig still owns native job memory that must be released.
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (!ReferenceEquals(sweep[i], null)) sweep[i].DisposeJobData();
                }
            }
            finally
            {
                EndSweep(sweep);
            }

            _graphBoundRigs.Clear();
            _postEvaluationRigs.Clear();
            _boundToGraph = false;
        }
    }
}
