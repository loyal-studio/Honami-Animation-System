using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public static class HonamiLinkedAction
    {
        private static readonly Dictionary<HonamiActionID, HashSet<HonamiAnimatorBase>> _registry = new();

        public static void PlayGlobal(HonamiActionID actionId, float transitionDuration = 0.25f, int limit = int.MaxValue)
        {
            if (actionId == null || limit <= 0) return;
            
            if (_registry.TryGetValue(actionId, out var animators))
            {
                int count = 0;
                foreach (var anim in animators)
                {
                    if (anim == null) continue;
                    anim.ReactToAction(actionId, transitionDuration);
                    if (++count >= limit) break;
                }
            }
        }

        public static void PlayByTag(HonamiActionID actionId, HonamiTagID tag, float transitionDuration = 0.25f)
        {
            if (actionId == null || tag == null) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            foreach (var anim in animators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public static void PlayByLayer(HonamiActionID actionId, UnityEngine.LayerMask layerMask, float transitionDuration = 0.25f)
        {
            if (actionId == null) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            int mask = layerMask.value;
            foreach (var anim in animators)
            {
                if (anim != null && ((1 << anim.gameObject.layer) & mask) != 0)
                    anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public static void PlayInHierarchy(HonamiActionID actionId, UnityEngine.Transform root, float transitionDuration = 0.25f)
        {
            if (actionId == null || root == null) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            foreach (var anim in animators)
            {
                if (anim != null && anim.transform.IsChildOf(root))
                    anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public static void PlayNearby(HonamiActionID actionId, Vector3 origin, float radius, float transitionDuration = 0.25f, int limit = int.MaxValue)
        {
            if (actionId == null || limit <= 0) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            float radiusSq = radius * radius;
            int count = 0;
            foreach (var anim in animators)
            {
                if (anim == null) continue;
                if (Vector3.SqrMagnitude(anim.transform.position - origin) <= radiusSq)
                {
                    anim.ReactToAction(actionId, transitionDuration);
                    if (++count >= limit) break;
                }
            }
        }

        public static void PlayNearby(HonamiActionID actionId, Vector3 origin, float radius, UnityEngine.LayerMask layerMask, float transitionDuration = 0.25f)
        {
            if (actionId == null) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            float radiusSq = radius * radius;
            int mask = layerMask.value;
            foreach (var anim in animators)
            {
                if (anim == null) continue;
                if (((1 << anim.gameObject.layer) & mask) != 0 && Vector3.SqrMagnitude(anim.transform.position - origin) <= radiusSq)
                    anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public static void PlayPropagated(HonamiActionID actionId, Vector3 origin, float radius, float speed, float transitionDuration = 0.25f)
        {
            if (actionId == null || speed <= 0f) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            float radiusSq = radius * radius;
            foreach (var anim in animators)
            {
                if (anim == null) continue;
                float distSq = Vector3.SqrMagnitude(anim.transform.position - origin);
                if (distSq <= radiusSq)
                {
                    float delay = Mathf.Sqrt(distSq) / speed;
                    anim.ReactToAction(actionId, transitionDuration, delay);
                }
            }
        }

        private static readonly List<(float dist, HonamiAnimatorBase anim)> _sortBuffer = new();

        public static void PlayClosest(HonamiActionID actionId, Vector3 origin, float radius, int limit, float transitionDuration = 0.25f)
        {
            if (actionId == null || limit <= 0) return;
            if (!_registry.TryGetValue(actionId, out var animators)) return;

            _sortBuffer.Clear();
            float radiusSq = radius * radius;

            foreach (var anim in animators)
            {
                if (anim == null) continue;
                float distSq = Vector3.SqrMagnitude(anim.transform.position - origin);
                if (distSq <= radiusSq)
                    _sortBuffer.Add((distSq, anim));
            }

            if (_sortBuffer.Count == 0) return;

            _sortBuffer.Sort((a, b) => a.dist.CompareTo(b.dist));

            int actualLimit = Mathf.Min(limit, _sortBuffer.Count);
            for (int i = 0; i < actualLimit; i++)
            {
                _sortBuffer[i].anim.ReactToAction(actionId, transitionDuration);
            }
        }

        internal static void Register(HonamiAnimatorBase animator, HonamiActionID actionId)
        {
            if (animator == null || actionId == null) return;

            if (!_registry.TryGetValue(actionId, out var set))
            {
                set = new HashSet<HonamiAnimatorBase>();
                _registry[actionId] = set;
            }
            set.Add(animator);
        }

        internal static void Unregister(HonamiAnimatorBase animator, HonamiActionID actionId)
        {
            if (animator == null || actionId == null) return;
            if (!_registry.TryGetValue(actionId, out var set)) return;

            set.Remove(animator);
            if (set.Count == 0)
                _registry.Remove(actionId);
        }

        private static readonly List<HonamiActionID> _emptyKeysScratch = new();

        internal static void UnregisterAll(HonamiAnimatorBase animator)
        {
            if (animator == null) return;

            _emptyKeysScratch.Clear();
            foreach (var kvp in _registry)
            {
                if (kvp.Value.Remove(animator) && kvp.Value.Count == 0)
                {
                    _emptyKeysScratch.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _emptyKeysScratch.Count; i++)
            {
                _registry.Remove(_emptyKeysScratch[i]);
            }
            _emptyKeysScratch.Clear();
        }
    }
}
