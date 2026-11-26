using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiNodeEditorRegistry
    {
        private static Dictionary<Type, HonamiNodeEditorBase> _editorByNodeType;
        private static List<(HonamiNodeEditorAttribute attr, Type editorType)> _allEntries;

        public static Dictionary<Type, HonamiNodeEditorBase> EditorByNodeType
        {
            get
            {
                if (_editorByNodeType == null) Build();
                return _editorByNodeType;
            }
        }

        public static List<(HonamiNodeEditorAttribute attr, Type editorType)> AllEntries
        {
            get
            {
                if (_allEntries == null) Build();
                return _allEntries;
            }
        }

        [InitializeOnLoadMethod]
        private static void Build()
        {
            _editorByNodeType = new();
            _allEntries = new();

            foreach (Type editorType in TypeCache.GetTypesDerivedFrom<HonamiNodeEditorBase>())
            {
                if (editorType.IsAbstract) continue;

                object[] attrs = editorType.GetCustomAttributes(typeof(HonamiNodeEditorAttribute), false);
                if (attrs.Length == 0) continue;

               HonamiNodeEditorAttribute attr = (HonamiNodeEditorAttribute)attrs[0];

                if (editorType.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"[Honami] Node editor '{editorType.FullName}' needs a public parameterless constructor.");
                    continue;
                }

                HonamiNodeEditorBase instance;
                try
                {
                    instance = (HonamiNodeEditorBase)Activator.CreateInstance(editorType);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Honami] Node editor '{editorType.FullName}' failed to construct: {exception}");
                    continue;
                }

                _editorByNodeType[attr.NodeType] = instance;
                _allEntries.Add((attr, editorType));
            }

            _allEntries.Sort((a, b) => a.attr.MenuOrder.CompareTo(b.attr.MenuOrder));
        }

        public static HonamiNodeEditorBase GetEditor(HonamiNodeBase node)
        {
            if (node == null) return null;
            EditorByNodeType.TryGetValue(node.GetType(), out HonamiNodeEditorBase editor);
            return editor;
        }

        public static HonamiNodeEditorBase GetEditorByType(Type nodeType)
        {
            EditorByNodeType.TryGetValue(nodeType, out HonamiNodeEditorBase editor);
            return editor;
        }
    }
}
