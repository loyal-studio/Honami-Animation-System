using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Global registry of animators that can react to a <see cref="HonamiActionID"/>, plus the broadcast
    /// helpers that fire one. Every helper funnels through <see cref="Broadcast"/>, so custom targeting
    /// only needs a predicate instead of a new method here.
    /// </summary>
    public static class HonamiLinkedAction
    {
        private static readonly Dictionary<HonamiActionID, HashSet<HonamiAnimatorBase>> _registry = new();
        private static readonly List<HonamiAnimatorBase> _iterationBuffer = new();
        private static readonly List<(float distanceSq, HonamiAnimatorBase animator)> _sortBuffer = new();
        private static readonly List<HonamiActionID> _emptyKeysScratch = new();
        private static int _broadcastDepth;

        /// <summary>
        /// Fires an action on every registered animator matching <paramref name="filter"/>, up to <paramref name="limit"/> of them.
        /// A null filter accepts everything. Returns how many animators reacted.
        /// </summary>
        public static int Broadcast(
            HonamiActionID actionId,
            Predicate<HonamiAnimatorBase> filter = null,
            float transitionDuration = 0.25f,
            int limit = int.MaxValue)
        {
            int fired = 0;
            List<HonamiAnimatorBase> buffer = Snapshot(actionId, limit);
            _broadcastDepth++;

            try
            {
                for (int i = 0; i < buffer.Count && fired < limit; i++)
                {
                    HonamiAnimatorBase animator = buffer[i];
                    if (filter != null && !filter(animator)) continue;

                    animator.ReactToAction(actionId, transitionDuration);
                    fired++;
                }
            }
            finally
            {
                buffer.Clear();
                _broadcastDepth--;
            }

            return fired;
        }

        /// <summary>
        /// Copies the animators registered for an action into <paramref name="buffer"/> and returns how many were written.
        /// Use this to build targeting rules Honami does not ship.
        /// </summary>
        public static int GetRegisteredNonAlloc(HonamiActionID actionId, Span<HonamiAnimatorBase> buffer)
        {
            if (actionId == null || !_registry.TryGetValue(actionId, out var animators)) return 0;

            int written = 0;
            foreach (var animator in animators)
            {
                if (written >= buffer.Length) break;
                if (animator == null) continue;
                buffer[written++] = animator;
            }

            return written;
        }

        /// <summary>
        /// How many live animators are registered for an action.
        /// </summary>
        public static int GetRegisteredCount(HonamiActionID actionId)
        {
            if (actionId == null || !_registry.TryGetValue(actionId, out var animators)) return 0;

            int count = 0;
            foreach (var animator in animators)
            {
                if (animator != null) count++;
            }

            return count;
        }

        public static void PlayGlobal(HonamiActionID actionId, float transitionDuration = 0.25f, int limit = int.MaxValue)
            => Broadcast(actionId, null, transitionDuration, limit);

        public static void PlayByTag(HonamiActionID actionId, HonamiTagID tag, float transitionDuration = 0.25f)
        {
            if (tag == null) return;
            Broadcast(actionId, animator => animator.linkingTag == tag, transitionDuration);
        }

        public static void PlayByLayer(HonamiActionID actionId, LayerMask layerMask, float transitionDuration = 0.25f)
        {
            int mask = layerMask.value;
            Broadcast(actionId, animator => ((1 << animator.gameObject.layer) & mask) != 0, transitionDuration);
        }

        public static void PlayInHierarchy(HonamiActionID actionId, Transform root, float transitionDuration = 0.25f)
        {
            if (root == null) return;
            Broadcast(actionId, animator => animator.transform.IsChildOf(root), transitionDuration);
        }

        public static void PlayNearby(HonamiActionID actionId, Vector3 origin, float radius, float transitionDuration = 0.25f, int limit = int.MaxValue)
        {
            float radiusSq = radius * radius;
            Broadcast(actionId, animator => Vector3.SqrMagnitude(animator.transform.position - origin) <= radiusSq, transitionDuration, limit);
        }

        public static void PlayNearby(HonamiActionID actionId, Vector3 origin, float radius, LayerMask layerMask, float transitionDuration = 0.25f)
        {
            float radiusSq = radius * radius;
            int mask = layerMask.value;
            Broadcast(
                actionId,
                animator => ((1 << animator.gameObject.layer) & mask) != 0
                            && Vector3.SqrMagnitude(animator.transform.position - origin) <= radiusSq,
                transitionDuration);
        }

        /// <summary>
        /// Fires an action outward from <paramref name="origin"/> as a wave travelling at <paramref name="speed"/> units per second.
        /// </summary>
        public static void PlayPropagated(HonamiActionID actionId, Vector3 origin, float radius, float speed, float transitionDuration = 0.25f)
        {
            if (speed <= 0f) return;

            float radiusSq = radius * radius;
            List<HonamiAnimatorBase> buffer = Snapshot(actionId, int.MaxValue);
            _broadcastDepth++;

            try
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    HonamiAnimatorBase animator = buffer[i];
                    float distanceSq = Vector3.SqrMagnitude(animator.transform.position - origin);
                    if (distanceSq > radiusSq) continue;

                    animator.ReactToAction(actionId, transitionDuration, Mathf.Sqrt(distanceSq) / speed);
                }
            }
            finally
            {
                buffer.Clear();
                _broadcastDepth--;
            }
        }

        /// <summary>
        /// Fires an action on the <paramref name="limit"/> registered animators closest to <paramref name="origin"/>.
        /// </summary>
        public static void PlayClosest(HonamiActionID actionId, Vector3 origin, float radius, int limit, float transitionDuration = 0.25f)
        {
            if (limit <= 0) return;

            float radiusSq = radius * radius;
            List<HonamiAnimatorBase> buffer = Snapshot(actionId, int.MaxValue);

            List<(float distanceSq, HonamiAnimatorBase animator)> sorted =
                _broadcastDepth == 0 ? _sortBuffer : new List<(float distanceSq, HonamiAnimatorBase animator)>();
            sorted.Clear();

            for (int i = 0; i < buffer.Count; i++)
            {
                HonamiAnimatorBase animator = buffer[i];
                float distanceSq = Vector3.SqrMagnitude(animator.transform.position - origin);
                if (distanceSq <= radiusSq) sorted.Add((distanceSq, animator));
            }

            buffer.Clear();
            if (sorted.Count == 0) return;

            sorted.Sort(static (a, b) => a.distanceSq.CompareTo(b.distanceSq));

            _broadcastDepth++;
            try
            {
                int actualLimit = Mathf.Min(limit, sorted.Count);
                for (int i = 0; i < actualLimit; i++)
                {
                    sorted[i].animator.ReactToAction(actionId, transitionDuration);
                }
            }
            finally
            {
                sorted.Clear();
                _broadcastDepth--;
            }
        }

        // Snapshotting first keeps a reaction free to register or unregister animators mid-broadcast.
        // A reaction that broadcasts again re-enters here, so only the outermost sweep owns the shared buffer.
        private static List<HonamiAnimatorBase> Snapshot(HonamiActionID actionId, int limit)
        {
            List<HonamiAnimatorBase> buffer = _broadcastDepth == 0 ? _iterationBuffer : new List<HonamiAnimatorBase>();
            buffer.Clear();

            if (actionId == null || limit <= 0 || !_registry.TryGetValue(actionId, out var animators)) return buffer;

            foreach (var animator in animators)
            {
                if (animator != null) buffer.Add(animator);
            }

            return buffer;
        }

        /// <summary>
        /// Makes an animator reachable by broadcasts of <paramref name="actionId"/>.
        /// Custom animators call this from their RegisterLinkedActions implementation.
        /// </summary>
        public static void Register(HonamiAnimatorBase animator, HonamiActionID actionId)
        {
            if (animator == null || actionId == null) return;

            if (!_registry.TryGetValue(actionId, out var set))
            {
                set = new HashSet<HonamiAnimatorBase>();
                _registry[actionId] = set;
            }

            set.Add(animator);
        }

        /// <summary>
        /// Removes a single action registration made by <see cref="Register"/>.
        /// </summary>
        public static void Unregister(HonamiAnimatorBase animator, HonamiActionID actionId)
        {
            if (animator == null || actionId == null) return;
            if (!_registry.TryGetValue(actionId, out var set)) return;

            set.Remove(animator);
            if (set.Count == 0) _registry.Remove(actionId);
        }

        /// <summary>
        /// Removes every registration for an animator. The base class calls this on disable.
        /// </summary>
        public static void UnregisterAll(HonamiAnimatorBase animator)
        {
            if (animator == null) return;

            _emptyKeysScratch.Clear();
            foreach (var pair in _registry)
            {
                if (pair.Value.Remove(animator) && pair.Value.Count == 0)
                {
                    _emptyKeysScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < _emptyKeysScratch.Count; i++)
            {
                _registry.Remove(_emptyKeysScratch[i]);
            }

            _emptyKeysScratch.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _registry.Clear();
            _iterationBuffer.Clear();
            _sortBuffer.Clear();
            _emptyKeysScratch.Clear();
            _broadcastDepth = 0;
        }
    }
}
