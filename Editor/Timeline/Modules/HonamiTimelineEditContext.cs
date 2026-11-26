using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public sealed class HonamiTimelineEditContext
    {
        private readonly Action _afterChange;
        private readonly Action<AnimationClip> _openClip;

        internal HonamiTimelineEditContext(
            IHonamiTimelineModuleContainer container,
            HonamiTimelineModule module,
            HonamiDirector director,
            GameObject previewRoot,
            float timeOffset,
            float playheadTime,
            IReadOnlyList<HonamiTimelineItem> selectedItems,
            Action afterChange,
            Action<AnimationClip> openClip,
            float frameRate = 0f)
        {
            Container = container;
            Module = module;
            Director = director;
            PreviewRoot = previewRoot;
            TimeOffset = timeOffset;
            PlayheadTime = playheadTime;
            SelectedItems = selectedItems;
            FrameRate = frameRate;
            _afterChange = afterChange;
            _openClip = openClip;
        }

        public IHonamiTimelineModuleContainer Container { get; }
        public HonamiTimelineModule Module { get; }
        public HonamiDirector Director { get; }
        public GameObject PreviewRoot { get; }
        public float TimeOffset { get; }
        public float PlayheadTime { get; }
        public float FrameRate { get; }
        public IReadOnlyList<HonamiTimelineItem> SelectedItems { get; }

        public HonamiTimelineModuleHost Host => Container.Host;
        public HonamiTimeline Timeline => Container as HonamiTimeline;
        public HonamiState State => Container as HonamiState;
        public HonamiTimelineModuleParent Parent => HonamiTimelineModuleParent.Resolve(Container.Modules, State != null ? State.node : null, Module);
        public UnityEngine.Object ContainerObject => (UnityEngine.Object)Container;

        public UnityEngine.Object Binding => ResolveBinding(Container, Module, Director, PreviewRoot);

        // Items are serialized on the module sub-asset, so recording the container alone captures nothing.
        public void RecordUndo(string actionName)
        {
            if (ContainerObject != null) Undo.RecordObject(ContainerObject, actionName);
            if (Module != null) Undo.RecordObject(Module, actionName);
        }

        public void MarkChanged()
        {
            EditorUtility.SetDirty(ContainerObject);
            Container.MarkModulesChanged();
            _afterChange?.Invoke();
        }

        public void Rebuild() => _afterChange?.Invoke();

        public void OpenClip(AnimationClip clip)
        {
            if (clip != null)
                _openClip?.Invoke(clip);
        }

        public UnityEngine.Object Resolve(HonamiSceneReference reference, Type type) =>
            ResolveReference(Container, Director, PreviewRoot, reference, type);

        public T Resolve<T>(HonamiSceneReference reference) where T : UnityEngine.Object => Resolve(reference, typeof(T)) as T;

        // A reference without a key gets one here, so the caller has to store the returned reference back on the item.
        public HonamiSceneReference AssignReference(HonamiSceneReference reference, UnityEngine.Object value)
        {
            if (Director == null) return reference;
            if (!reference.IsAssigned) reference = HonamiSceneReference.NewKey();
            Undo.RecordObject(Director, "Assign Scene Reference");
            Director.SetReference(reference.Key, value);
            EditorUtility.SetDirty(Director);
            return reference;
        }

        internal static UnityEngine.Object ResolveBinding(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, HonamiDirector director, GameObject previewRoot)
        {
            if (container == null || module == null || module.BindingType == null) return null;
            if (container.Host == HonamiTimelineModuleHost.Timeline)
                return director != null ? director.GetBinding(module) : null;
            return HonamiTimelineModuleBinding.Resolve(previewRoot, module.bindingKey, module.BindingType);
        }

        internal static UnityEngine.Object ResolveReference(IHonamiTimelineModuleContainer container, HonamiDirector director, GameObject previewRoot, HonamiSceneReference reference, Type type)
        {
            if (!reference.IsAssigned || type == null || container == null) return null;
            if (container.Host == HonamiTimelineModuleHost.Timeline)
                return director != null ? director.Resolve(reference, type) : null;
            return HonamiTimelineModuleBinding.Resolve(previewRoot, reference.Key, type);
        }

        // The selected director wins, so two directors playing the same timeline can each be previewed.
        public static HonamiDirector FindDirector(HonamiTimeline timeline)
        {
            if (timeline == null) return null;

            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var selectedDirector = selected.GetComponentInParent<HonamiDirector>(true);
                if (selectedDirector != null && selectedDirector.Timeline == timeline)
                    return selectedDirector;
            }

            var directors = UnityEngine.Object.FindObjectsByType<HonamiDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < directors.Length; i++)
            {
                if (directors[i].Timeline == timeline && !EditorUtility.IsPersistent(directors[i]))
                    return directors[i];
            }
            return null;
        }

        public static HonamiDirector CreateDirector(HonamiTimeline timeline)
        {
            if (timeline == null) return null;
            var gameObject = new GameObject($"{timeline.name} Director");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Honami Director");
            var director = Undo.AddComponent<HonamiDirector>(gameObject);
            director.PlayTrigger = HonamiDirectorPlayTrigger.Manual;
            director.Timeline = timeline;
            EditorUtility.SetDirty(director);
            return director;
        }
    }
}
