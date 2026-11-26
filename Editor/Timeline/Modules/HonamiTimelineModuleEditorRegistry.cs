using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public static class HonamiTimelineModuleEditorRegistry
    {
        private static Dictionary<Type, HonamiTimelineModuleEditor> _editorByModuleType;
        private static List<Type> _moduleTypes;
        private static readonly HashSet<Type> ReportedTypes = new();

        public static IReadOnlyList<Type> ModuleTypes
        {
            get
            {
                if (_moduleTypes == null) Build();
                return _moduleTypes;
            }
        }

        [InitializeOnLoadMethod]
        private static void Build()
        {
            _editorByModuleType = new Dictionary<Type, HonamiTimelineModuleEditor>();
            _moduleTypes = new List<Type>();

            foreach (Type editorType in TypeCache.GetTypesWithAttribute<HonamiTimelineModuleEditorAttribute>())
            {
                if (editorType.IsAbstract || !typeof(HonamiTimelineModuleEditor).IsAssignableFrom(editorType))
                {
                    Debug.LogError($"[Honami] '{editorType.FullName}' has [HonamiTimelineModuleEditor] but does not derive from HonamiTimelineModuleEditor.");
                    continue;
                }

                var attr = (HonamiTimelineModuleEditorAttribute)editorType.GetCustomAttributes(typeof(HonamiTimelineModuleEditorAttribute), false)[0];
                if (!IsCreatableModuleType(attr.ModuleType)) continue;

                HonamiTimelineModuleEditor editor;
                try
                {
                    editor = (HonamiTimelineModuleEditor)Activator.CreateInstance(editorType);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Honami] Timeline module editor '{editorType.FullName}' failed to construct: {exception}");
                    continue;
                }

                Configure(editor, attr.ModuleType, attr.MenuPath, attr.MenuOrder);
                _editorByModuleType[attr.ModuleType] = editor;
            }

            foreach (Type moduleType in TypeCache.GetTypesDerivedFrom<HonamiTimelineModule>())
            {
                if (!IsCreatableModuleType(moduleType)) continue;
                if (!_editorByModuleType.ContainsKey(moduleType))
                    _editorByModuleType[moduleType] = CreateDefaultEditor(moduleType);
                _moduleTypes.Add(moduleType);
            }

            _moduleTypes.Sort((a, b) =>
            {
                var editorA = _editorByModuleType[a];
                var editorB = _editorByModuleType[b];
                int order = editorA.MenuOrder.CompareTo(editorB.MenuOrder);
                return order != 0 ? order : string.CompareOrdinal(editorA.MenuPath, editorB.MenuPath);
            });
        }

        public static IEnumerable<Type> ModuleTypesFor(HonamiTimelineModuleHost host)
        {
            foreach (var moduleType in ModuleTypes)
            {
                if (GetEditor(moduleType).SupportsHost(host))
                    yield return moduleType;
            }
        }

        public static void StopAllPreviews()
        {
            if (_editorByModuleType == null) return;
            foreach (var editor in _editorByModuleType.Values)
            {
                try
                {
                    editor.StopPreview();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static Type FindModuleTypeForDrop(UnityEngine.Object dropped, HonamiTimelineModuleHost host)
        {
            foreach (var moduleType in ModuleTypesFor(host))
            {
                if (GetEditor(moduleType).AcceptsDrop(dropped))
                    return moduleType;
            }
            return null;
        }

        public static HonamiTimelineModuleEditor GetEditor(HonamiTimelineModule module) => module != null ? GetEditor(module.GetType()) : null;

        public static HonamiTimelineModuleEditor GetEditor(Type moduleType)
        {
            if (moduleType == null) return null;
            if (_editorByModuleType == null) Build();
            if (_editorByModuleType.TryGetValue(moduleType, out var editor)) return editor;

            editor = CreateDefaultEditor(moduleType);
            _editorByModuleType[moduleType] = editor;
            return editor;
        }

        private static HonamiTimelineModuleEditor CreateDefaultEditor(Type moduleType)
        {
            var editor = new HonamiTimelineModuleEditor();
            Configure(editor, moduleType, null, 1000);
            return editor;
        }

        private static void Configure(HonamiTimelineModuleEditor editor, Type moduleType, string menuPath, int menuOrder)
        {
            editor.ModuleType = moduleType;
            editor.MenuPath = string.IsNullOrEmpty(menuPath) ? HonamiTimelineModuleEditor.NicifyTypeName(moduleType.Name, "Module") : menuPath;
            editor.MenuOrder = menuOrder;

            var prototype = CreatePrototype(moduleType);
            if (prototype != null)
            {
                editor.ItemType = prototype.ItemType;
                editor.SupportedHosts = prototype.SupportedHosts;
                UnityEngine.Object.DestroyImmediate(prototype);
            }
            else
            {
                editor.ItemType = null;
                editor.SupportedHosts = HonamiTimelineModuleHosts.None;
            }
        }

        private static HonamiTimelineModule CreatePrototype(Type moduleType)
        {
            if (moduleType.IsAbstract || moduleType.ContainsGenericParameters) return null;
            try
            {
                return (HonamiTimelineModule)ScriptableObject.CreateInstance(moduleType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsCreatableModuleType(Type moduleType)
        {
            if (moduleType == null || !typeof(HonamiTimelineModule).IsAssignableFrom(moduleType))
            {
                Debug.LogError($"[Honami] Timeline module editor targets '{moduleType?.FullName ?? "null"}', which is not a HonamiTimelineModule.");
                return false;
            }

            if (moduleType.IsAbstract || moduleType.ContainsGenericParameters) return false;
            return true;
        }
    }
}
