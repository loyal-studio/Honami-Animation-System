using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Serializable]
    public sealed class HonamiTimelineBindingEntry
    {
        public string key;
        public UnityEngine.Object target;
    }

    [AddComponentMenu("Honami Animation/Honami Timeline Binding Receiver")]
    public sealed class HonamiTimelineBindingReceiver : MonoBehaviour, IHonamiTimelineBindingProvider
    {
        [SerializeField] private List<HonamiTimelineBindingEntry> bindings = new();

        public IReadOnlyList<HonamiTimelineBindingEntry> Bindings => bindings;

        public bool TryGetBinding(string key, Type bindingType, out UnityEngine.Object binding)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
                if (entry == null || entry.key != key) continue;

                binding = HonamiTimelineModuleBinding.Convert(entry.target, bindingType);
                return binding != null;
            }

            binding = null;
            return false;
        }

        public void CollectBindingKeys(List<string> keys)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var key = bindings[i]?.key;
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
                    keys.Add(key);
            }
        }

        public void SetBinding(string key, UnityEngine.Object target)
        {
            var entry = Find(key);
            if (entry == null)
            {
                entry = new HonamiTimelineBindingEntry { key = key };
                bindings.Add(entry);
            }
            entry.target = target;
            NotifyAnimator();
        }

        public bool RemoveBinding(string key)
        {
            var entry = Find(key);
            if (entry == null) return false;
            bindings.Remove(entry);
            NotifyAnimator();
            return true;
        }

        private HonamiTimelineBindingEntry Find(string key)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] != null && bindings[i].key == key)
                    return bindings[i];
            }
            return null;
        }

        private void OnValidate() => NotifyAnimator();

        private void NotifyAnimator()
        {
            if (TryGetComponent(out HonamiAnimator animator))
                animator.RefreshModuleBindings();
        }
    }
}
