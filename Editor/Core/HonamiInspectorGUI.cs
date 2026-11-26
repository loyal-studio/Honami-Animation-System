using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiInspectorGUI
    {
        public static readonly Color Accent = new(0.18f, 0.76f, 0.9f);
        public static readonly Color Highlight = new(0.2f, 1f, 0.6f);
        public static readonly Color Warning = new(1f, 0.7f, 0.2f);
        public static readonly Color Debug = new(0.9f, 0.6f, 0.2f);
        public static readonly Color Ok = new(0.3f, 0.9f, 0.4f);

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _foldoutStyle;

        public static void Title(string title, string subtitle) => Title(title, subtitle, Accent);

        public static void Title(string title, string subtitle, Color accent)
        {
            _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = accent;

            EditorGUILayout.Space(10);
            GUILayout.Label(title, _titleStyle);
            if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, _subtitleStyle);

            Rule(accent);
            EditorGUILayout.Space(5);
        }

        public static void Rule(Color color, float height = 2f, float alpha = 0.5f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, alpha));
        }

        public static void SubHeader(string label) => SubHeader(label, Debug);

        public static void SubHeader(string label, Color color)
        {
            _subHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel);
            _subHeaderStyle.normal.textColor = color;
            GUILayout.Label(label, _subHeaderStyle);
        }

        public static void SectionHeader(string label)
        {
            _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _sectionStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : Color.black;

            Rect rect = GUILayoutUtility.GetRect(18, 18, _sectionStyle);
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            EditorGUI.LabelField(new Rect(rect.x + 4, rect.y, rect.width, rect.height), label, _sectionStyle);
        }

        public static bool FoldoutHeader(string title, bool expanded, Color color)
        {
            _foldoutStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            _foldoutStyle.normal.textColor = color;
            _foldoutStyle.onNormal.textColor = color;
            return EditorGUILayout.Foldout(expanded, title, true, _foldoutStyle);
        }
    }
}
