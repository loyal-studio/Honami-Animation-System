using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiPreferenceCategory
    {
        private readonly List<Action> _drawers = new();
        private readonly HashSet<string> _keywords = new();
        private string _description;

        internal string Name { get; }

        internal HonamiPreferenceCategory(string name)
        {
            Name = name;
        }

        public HonamiPreferenceCategory WithDescription(string text)
        {
            _description = text;
            return CollectKeywords(text);
        }

        public HonamiPreferenceCategory AddToggle(string label, Func<bool> get, Action<bool> set, string tooltip = "", Action onReset = null)
        {
            var content = new GUIContent(label, tooltip);
            _drawers.Add(() =>
            {
                EditorGUI.BeginChangeCheck();
                bool value = EditorGUILayout.Toggle(content, get());
                if (EditorGUI.EndChangeCheck()) set(value);
                HandleContextMenu(onReset);
            });
            return CollectKeywords(label);
        }

        public HonamiPreferenceCategory AddSlider(string label, float min, float max, Func<float> get, Action<float> set, string tooltip = "", Action onReset = null)
        {
            var content = new GUIContent(label, tooltip);
            _drawers.Add(() =>
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.Slider(content, get(), min, max);
                if (EditorGUI.EndChangeCheck()) set(value);
                HandleContextMenu(onReset);
            });
            return CollectKeywords(label);
        }

        public HonamiPreferenceCategory AddIntSlider(string label, int min, int max, Func<int> get, Action<int> set, string tooltip = "", Action onReset = null)
        {
            var content = new GUIContent(label, tooltip);
            _drawers.Add(() =>
            {
                EditorGUI.BeginChangeCheck();
                int value = EditorGUILayout.IntSlider(content, get(), min, max);
                if (EditorGUI.EndChangeCheck()) set(value);
                HandleContextMenu(onReset);
            });
            return CollectKeywords(label);
        }

        public HonamiPreferenceCategory AddEnum<T>(string label, Func<T> get, Action<T> set, string tooltip = "", Action onReset = null) where T : Enum
        {
            var content = new GUIContent(label, tooltip);
            _drawers.Add(() =>
            {
                EditorGUI.BeginChangeCheck();
                var value = (T)EditorGUILayout.EnumPopup(content, get());
                if (EditorGUI.EndChangeCheck()) set(value);
                HandleContextMenu(onReset);
            });
            return CollectKeywords(label);
        }

        public HonamiPreferenceCategory AddColor(string label, Func<Color> get, Action<Color> set, string tooltip = "", Action onReset = null)
        {
            var content = new GUIContent(label, tooltip);
            _drawers.Add(() =>
            {
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.ColorField(content, get());
                if (EditorGUI.EndChangeCheck()) set(value);
                HandleContextMenu(onReset);
            });
            return CollectKeywords(label);
        }

        private static void HandleContextMenu(Action onReset)
        {
            if (onReset == null) return;

            var evt = Event.current;
            if (evt.type != EventType.ContextClick) return;
            if (!GUILayoutUtility.GetLastRect().Contains(evt.mousePosition)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reset"), false, () => onReset());
            menu.ShowAsContext();
            evt.Use();
        }

        public HonamiPreferenceCategory AddButton(string label, Action onClick)
        {
            _drawers.Add(() =>
            {
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(label, GUILayout.Width(220))) onClick();
                    GUILayout.FlexibleSpace();
                }
            });
            return CollectKeywords(label);
        }

        public HonamiPreferenceCategory AddHeader(string text)
        {
            _drawers.Add(() =>
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            });
            return CollectKeywords(text);
        }

        public HonamiPreferenceCategory AddCustom(Action drawGui, params string[] keywords)
        {
            _drawers.Add(drawGui);
            foreach (var keyword in keywords) CollectKeywords(keyword);
            return this;
        }

        internal SettingsProvider CreateProvider(string rootPath)
        {
            return new SettingsProvider($"{rootPath}/{Name}", SettingsScope.User)
            {
                label = Name,
                keywords = _keywords,
                guiHandler = _ => Draw()
            };
        }

        private void Draw()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250f;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(10);
                    if (!string.IsNullOrEmpty(_description))
                    {
                        EditorGUILayout.LabelField(_description, EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.Space(6);
                    }
                    foreach (var drawer in _drawers) drawer();
                }
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private HonamiPreferenceCategory CollectKeywords(string label)
        {
            foreach (var word in label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                _keywords.Add(word);
            }

            return this;
        }
    }
}
