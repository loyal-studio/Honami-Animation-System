using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [AddComponentMenu("Honami Animation/Honami Rigging Processor")]
    [ExecuteAlways]
    public sealed class HonamiRiggingProcessor : MonoBehaviour
    {
        private HonamiRig[] _rigs = new HonamiRig[8];
        private int _rigsCount;
        
        private HonamiRig[] _graphBoundRigs = new HonamiRig[8];
        private int _graphBoundRigsCount;
        
        private HonamiRig[] _legacyRigs = new HonamiRig[8];
        private int _legacyRigsCount;
        
        private bool _boundToGraph;
        private static readonly List<HonamiRig> s_RigBuffer = new();

        private void OnEnable()
        {
            RefreshRigs();
        }

        private static bool ContainsRig(HonamiRig[] array, int count, HonamiRig rig)
        {
            var span = new ReadOnlySpan<HonamiRig>(array, 0, count);
            for (int i = 0; i < span.Length; i++)
            {
                if (object.ReferenceEquals(span[i], rig)) return true;
            }
            return false;
        }

        private static void AddRig(ref HonamiRig[] array, ref int count, HonamiRig rig)
        {
            if (count == array.Length)
                System.Array.Resize(ref array, array.Length == 0 ? 4 : array.Length * 2);
            array[count++] = rig;
        }

        private static void RemoveRig(HonamiRig[] array, ref int count, HonamiRig rig)
        {
            var span = new Span<HonamiRig>(array, 0, count);
            for (int i = 0; i < span.Length; i++)
            {
                if (object.ReferenceEquals(span[i], rig))
                {
                    array[i] = array[count - 1];
                    array[count - 1] = null;
                    count--;
                    return;
                }
            }
        }

        public void RegisterRig(HonamiRig rig)
        {
            if (rig == null || ContainsRig(_rigs, _rigsCount, rig)) return;
            
            AddRig(ref _rigs, ref _rigsCount, rig);
            rig.ResetRig();
            
            if (_boundToGraph)
            {
                if (!ContainsRig(_legacyRigs, _legacyRigsCount, rig) && !ContainsRig(_graphBoundRigs, _graphBoundRigsCount, rig))
                {
                    AddRig(ref _legacyRigs, ref _legacyRigsCount, rig);
                }
            }
        }

        public void UnregisterRig(HonamiRig rig)
        {
            if (rig == null) return;
            
            RemoveRig(_rigs, ref _rigsCount, rig);
            RemoveRig(_graphBoundRigs, ref _graphBoundRigsCount, rig);
            RemoveRig(_legacyRigs, ref _legacyRigsCount, rig);
        }

        private void LateUpdate()
        {
            if (_boundToGraph && Application.isPlaying) return;

            float dt = Time.deltaTime;
            var span = new ReadOnlySpan<HonamiRig>(_rigs, 0, _rigsCount);
            for (int i = 0; i < span.Length; i++)
            {
                var rig = span[i];
                if (rig && rig.enabled && rig.gameObject.activeInHierarchy)
                    rig.ProcessRig(dt);
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

            var span = new ReadOnlySpan<HonamiRig>(_rigs, 0, _rigsCount);
            for (int i = 0; i < span.Length; i++)
            {
                var rig = span[i];
                if (rig == null || !rig.gameObject.activeInHierarchy) continue;

                Playable rigPlayable = rig.CreatePlayable(animator, graph);
                if (!rigPlayable.IsValid())
                {
                    AddRig(ref _legacyRigs, ref _legacyRigsCount, rig);
                    continue;
                }

                graph.Connect(current, 0, rigPlayable, 0);
                rigPlayable.SetInputWeight(0, 1f);
                current = rigPlayable;

                AddRig(ref _graphBoundRigs, ref _graphBoundRigsCount, rig);
            }

            // the animator drives every rig from here on, even when none of them produced a playable —
            // letting LateUpdate run them too would tick stateful rigs twice per frame
            _boundToGraph = true;
            return current;
        }

        public void PrepareAllRigs(float deltaTime)
        {
            var span = new ReadOnlySpan<HonamiRig>(_graphBoundRigs, 0, _graphBoundRigsCount);
            for (int i = 0; i < span.Length; i++)
            {
                var rig = span[i];
                if (rig != null)
                    rig.PrepareJobData(deltaTime);
            }
        }

        public void ProcessLegacyRigs(float deltaTime)
        {
            var span = new ReadOnlySpan<HonamiRig>(_legacyRigs, 0, _legacyRigsCount);
            for (int i = 0; i < span.Length; i++)
            {
                var rig = span[i];
                if (rig != null && rig.enabled)
                    rig.ProcessRig(deltaTime);
            }
        }

        private bool IsMyRig(HonamiRig rig)
        {
            Transform p = rig.transform;
            while (p != null)
            {
                if (p == transform) return true;
                if (p.TryGetComponent<HonamiRiggingProcessor>(out _) || p.TryGetComponent<HonamiAnimatorBase>(out _)) return false;
                p = p.parent;
            }
            return false;
        }

        public void RefreshRigs()
        {
            if (!this) return;
            
            s_RigBuffer.Clear();
            GetComponentsInChildren<HonamiRig>(true, s_RigBuffer);
            
            if (_rigs.Length < s_RigBuffer.Count)
                System.Array.Resize(ref _rigs, s_RigBuffer.Count);
                
            _rigsCount = 0;
            for (int i = 0; i < s_RigBuffer.Count; i++)
            {
                var rig = s_RigBuffer[i];
                if (rig == null || !IsMyRig(rig)) continue;
                _rigs[_rigsCount++] = rig;
                
                rig.ResetRig();
                if (rig is IEarlyInitRig earlyInit) earlyInit.EarlyInit();
            }
        }

        public void RebuildGraphRigs(Animator animator, PlayableGraph graph)
        {
            DisposeAllJobs();
        }

        private void DisposeAllJobs()
        {
            var span = new ReadOnlySpan<HonamiRig>(_graphBoundRigs, 0, _graphBoundRigsCount);
            for (int i = 0; i < span.Length; i++)
            {
                if (!object.ReferenceEquals(span[i], null))
                    span[i].DisposeJobData();
            }
            
            for (int i = 0; i < _graphBoundRigsCount; i++) _graphBoundRigs[i] = null;
            _graphBoundRigsCount = 0;
            
            for (int i = 0; i < _legacyRigsCount; i++) _legacyRigs[i] = null;
            _legacyRigsCount = 0;
            
            _boundToGraph = false;
        }
    }
}
