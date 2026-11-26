using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Flags]
    public enum HonamiTimelineModuleHosts
    {
        None = 0,
        Timeline = 1,
        State = 2,
        All = Timeline | State
    }

    public enum HonamiTimelineModuleHost
    {
        Timeline,
        State
    }

    public interface IHonamiTimelineModuleContainer
    {
        HonamiTimelineModuleHost Host { get; }
        List<HonamiTimelineModule> Modules { get; }
        int ModuleRevision { get; }
        void MarkModulesChanged();
    }

    public readonly struct HonamiTimelineModuleContext
    {
        public readonly HonamiTimelineModuleHost Host;
        public readonly GameObject Owner;
        public readonly UnityEngine.Object Binding;
        public readonly HonamiDirector Director;
        public readonly HonamiAnimator Animator;
        public readonly HonamiState State;

        public HonamiTimelineModuleContext(HonamiTimelineModuleHost host, GameObject owner, UnityEngine.Object binding, HonamiDirector director, HonamiAnimator animator, HonamiState state)
        {
            Host = host;
            Owner = owner;
            Binding = binding;
            Director = director;
            Animator = animator;
            State = state;
        }

        public UnityEngine.Object Resolve(HonamiSceneReference reference, Type type)
        {
            if (!reference.IsAssigned || type == null) return null;
            if (Director != null) return HonamiTimelineModuleBinding.Convert(Director.GetReference(reference.Key), type);
            return HonamiTimelineModuleBinding.Resolve(Owner, reference.Key, type);
        }

        public T Resolve<T>(HonamiSceneReference reference) where T : UnityEngine.Object => Resolve(reference, typeof(T)) as T;
    }

    [Preserve]
    public abstract class HonamiTimelineModule : ScriptableObject
    {
        [SerializeField, HideInInspector] private string id = NewId();
        public string moduleName = "Module";
        public bool muted;
        [HideInInspector] public bool locked;
        [HideInInspector] public string bindingKey;
        [HideInInspector] public string parentId;

        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(id)) id = NewId();
                return id;
            }
        }
        public bool HasParent => !string.IsNullOrEmpty(parentId);
        public virtual Type BindingType => null;
        public virtual HonamiTimelineModuleHosts SupportedHosts => HonamiTimelineModuleHosts.All;
        public virtual bool RequiresBinding(HonamiTimelineModuleHost host) => false;

        public virtual string GetBindingProblem(HonamiTimelineModuleHost host, UnityEngine.Object binding, GameObject owner)
        {
            if (BindingType == null || binding != null) return null;
            if (host == HonamiTimelineModuleHost.State && !string.IsNullOrEmpty(bindingKey))
                return $"no binding '{bindingKey}' with a {BindingType.Name} on the animator's GameObject";
            if (!RequiresBinding(host)) return null;
            return host == HonamiTimelineModuleHost.State
                ? $"the animator's GameObject has no {BindingType.Name}"
                : "nothing is bound to this module on the Honami Director";
        }

        public abstract Type ItemType { get; }
        public abstract int ItemCount { get; }
        public abstract HonamiTimelineItem GetItem(int index);
        public abstract int IndexOf(HonamiTimelineItem item);
        public abstract HonamiTimelineItem CreateItem(float time);
        public abstract bool AddItem(HonamiTimelineItem item);
        public abstract bool RemoveItem(HonamiTimelineItem item);

        public abstract HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context);

        public virtual bool CanHostModule(HonamiTimelineItem item, Type moduleType) => item != null && !item.IsInstant;

        public HonamiTimelineItem FindItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            int count = ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = GetItem(i);
                if (item != null && item.Id == itemId) return item;
            }
            return null;
        }

        public bool Contains(HonamiTimelineItem item) => IndexOf(item) >= 0;
        public bool SupportsHost(HonamiTimelineModuleHost host) => (SupportedHosts & (host == HonamiTimelineModuleHost.State ? HonamiTimelineModuleHosts.State : HonamiTimelineModuleHosts.Timeline)) != 0;

        public virtual float GetDuration()
        {
            float max = 0f;
            int count = ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = GetItem(i);
                if (item != null && item.EndTime > max)
                    max = item.EndTime;
            }
            return max;
        }

        public void RegenerateId() => id = NewId();

        private static string NewId() => Guid.NewGuid().ToString("N");
    }

    [Preserve]
    public abstract class HonamiTimelineModule<TItem> : HonamiTimelineModule where TItem : HonamiTimelineItem, new()
    {
        [SerializeField] protected List<TItem> items = new();

        public List<TItem> Items => items;

        public sealed override Type ItemType => typeof(TItem);
        public sealed override int ItemCount => items.Count;
        public sealed override HonamiTimelineItem GetItem(int index) => items[index];
        public sealed override int IndexOf(HonamiTimelineItem item) => item is TItem typed ? items.IndexOf(typed) : -1;

        public override HonamiTimelineItem CreateItem(float time) => new TItem { time = time };

        public sealed override bool AddItem(HonamiTimelineItem item)
        {
            if (item is not TItem typed) return false;
            items.Add(typed);
            return true;
        }

        public sealed override bool RemoveItem(HonamiTimelineItem item) => item is TItem typed && items.Remove(typed);
    }

    public static class HonamiTimelineModuleBinding
    {
        private static readonly HashSet<string> ReportedProblems = new();

        public static void ReportProblem(HonamiTimelineModule module, string problem, string where, UnityEngine.Object context)
        {
            if (string.IsNullOrEmpty(problem) || !ReportedProblems.Add(module.Id + problem)) return;
            Debug.LogWarning($"[Honami] Module '{module.moduleName}' in {where}: {problem}. The module does nothing until this is fixed.", context);
        }

        private static readonly List<IHonamiTimelineBindingProvider> ProviderBuffer = new();

        public static UnityEngine.Object Resolve(GameObject owner, string key, Type bindingType)
        {
            if (owner == null || bindingType == null) return null;
            if (string.IsNullOrEmpty(key)) return Convert(owner, bindingType);

            ProviderBuffer.Clear();
            owner.GetComponents(ProviderBuffer);
            for (int i = 0; i < ProviderBuffer.Count; i++)
            {
                if (ProviderBuffer[i].TryGetBinding(key, bindingType, out var binding) && binding != null)
                {
                    ProviderBuffer.Clear();
                    return binding;
                }
            }
            ProviderBuffer.Clear();
            return null;
        }

        public static void CollectKeys(GameObject owner, List<string> keys)
        {
            if (owner == null) return;
            ProviderBuffer.Clear();
            owner.GetComponents(ProviderBuffer);
            for (int i = 0; i < ProviderBuffer.Count; i++)
                ProviderBuffer[i].CollectBindingKeys(keys);
            ProviderBuffer.Clear();
        }

        public static UnityEngine.Object Convert(UnityEngine.Object target, Type bindingType)
        {
            if (target == null || bindingType == null) return null;
            if (bindingType.IsInstanceOfType(target)) return target;

            var gameObject = target is Component component ? component.gameObject : target as GameObject;
            if (gameObject == null) return null;
            if (bindingType == typeof(GameObject)) return gameObject;
            if (typeof(Component).IsAssignableFrom(bindingType))
                return gameObject.TryGetComponent(bindingType, out var found) ? found : null;
            return null;
        }
    }
}
