using System;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiColorPref
    {
        private readonly string _key;
        private readonly Action _onChanged;
        private Color _value;

        public Color DefaultValue { get; }

        internal HonamiColorPref(string key, Color defaultValue, Action onChanged)
        {
            _key = key;
            DefaultValue = defaultValue;
            _onChanged = onChanged;
            _value = Load();
        }

        public Color Value
        {
            get => _value;
            set
            {
                if (SetSilently(value)) _onChanged?.Invoke();
            }
        }

        internal bool SetSilently(Color value)
        {
            if (_value == value) return false;
            _value = value;
            EditorPrefs.SetString(_key, ColorUtility.ToHtmlStringRGBA(value));
            return true;
        }

        internal void Reset()
        {
            EditorPrefs.DeleteKey(_key);
            _value = DefaultValue;
        }

        public void ResetToDefault()
        {
            bool changed = _value != DefaultValue;
            Reset();
            if (changed) _onChanged?.Invoke();
        }

        private Color Load()
        {
            string stored = EditorPrefs.GetString(_key, string.Empty);
            return ColorUtility.TryParseHtmlString("#" + stored, out var color) ? color : DefaultValue;
        }
    }

    public static class HonamiAppearanceSettings
    {
        public static event Action Changed;

        public static readonly HonamiColorPref Accent = new("Honami_Appearance_Accent", new Color(1.00f, 0.278f, 0.522f, 1f), RaiseChanged);
        public static readonly HonamiColorPref AccentDim = new("Honami_Appearance_AccentDim", new Color(0.72f, 0.16f, 0.36f, 1f), RaiseChanged);
        public static readonly HonamiColorPref WindowBg = new("Honami_Appearance_WindowBg", new Color(0.055f, 0.058f, 0.064f), RaiseChanged);
        public static readonly HonamiColorPref PanelBg = new("Honami_Appearance_PanelBg", new Color(0.095f, 0.102f, 0.112f), RaiseChanged);
        public static readonly HonamiColorPref ToolbarBg = new("Honami_Appearance_ToolbarBg", new Color(0.086f, 0.09f, 0.098f), RaiseChanged);
        public static readonly HonamiColorPref BoxBg = new("Honami_Appearance_BoxBg", new Color(0.128f, 0.138f, 0.152f), RaiseChanged);
        public static readonly HonamiColorPref Text = new("Honami_Appearance_Text", new Color(0.88f, 0.90f, 0.92f), RaiseChanged);
        public static readonly HonamiColorPref MutedText = new("Honami_Appearance_MutedText", new Color(0.58f, 0.61f, 0.65f), RaiseChanged);
        public static readonly HonamiColorPref CanvasBg = new("Honami_Appearance_CanvasBg", new Color(0.071f, 0.078f, 0.094f), RaiseChanged);
        public static readonly HonamiColorPref GridLine = new("Honami_Appearance_GridLine", new Color(1f, 1f, 1f, 0.03f), RaiseChanged);
        public static readonly HonamiColorPref GridThickLine = new("Honami_Appearance_GridThickLine", new Color(1f, 1f, 1f, 0.08f), RaiseChanged);

        private static readonly HonamiColorPref[] All = { Accent, AccentDim, WindowBg, PanelBg, ToolbarBg, BoxBg, Text, MutedText, CanvasBg, GridLine, GridThickLine };

        private static void RaiseChanged() => Changed?.Invoke();

        public static void ResetToDefaults()
        {
            foreach (var pref in All) pref.Reset();
            Changed?.Invoke();
        }

        internal static void ApplyPreset(HonamiThemePreset preset) => ApplyColors(preset.Colors);

        private static void ApplyColors(Color[] colors)
        {
            bool changed = false;
            for (int i = 0; i < All.Length; i++)
            {
                changed |= All[i].SetSilently(colors[i]);
            }

            if (changed) Changed?.Invoke();
        }

        private static int FindMatchingPreset()
        {
            var presets = HonamiThemePresets.All;
            for (int p = 0; p < presets.Length; p++)
            {
                bool match = true;
                for (int i = 0; i < All.Length && match; i++)
                {
                    match = ColorUtility.ToHtmlStringRGBA(All[i].Value) ==
                            ColorUtility.ToHtmlStringRGBA(presets[p].Colors[i]);
                }

                if (match) return p;
            }

            return -1;
        }

        private const string ClipboardHeader = "HonamiTheme";

        private static GUIStyle _previewText;
        private static GUIStyle PreviewText => _previewText ??= new GUIStyle(EditorStyles.miniBoldLabel) { clipping = TextClipping.Clip };

        private static void DrawPreviewLabel(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            PreviewText.normal.textColor = color;
            PreviewText.alignment = anchor;
            GUI.Label(rect, text, PreviewText);
        }

        private static void DrawThemePreview()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 116f, GUILayout.ExpandWidth(true));
            rect.width = Mathf.Min(rect.width, 430f);

            EditorGUI.DrawRect(rect, WindowBg.Value);

            var toolbar = new Rect(rect.x, rect.y, rect.width, 24f);
            EditorGUI.DrawRect(toolbar, ToolbarBg.Value);
            DrawPreviewLabel(new Rect(toolbar.x + 10f, toolbar.y, 130f, toolbar.height - 2f), "Honami Graph", Text.Value);
            EditorGUI.DrawRect(new Rect(toolbar.x + 10f, toolbar.yMax - 2f, 58f, 2f), Accent.Value);

            var panel = new Rect(rect.xMax - 158f, toolbar.yMax, 158f, rect.height - toolbar.height);
            EditorGUI.DrawRect(panel, PanelBg.Value);

            var box = new Rect(panel.x + 10f, panel.y + 10f, panel.width - 20f, 38f);
            EditorGUI.DrawRect(box, BoxBg.Value);
            DrawPreviewLabel(new Rect(box.x + 7f, box.y + 3f, box.width - 14f, 16f), "Inspector", Text.Value);
            DrawPreviewLabel(new Rect(box.x + 7f, box.y + 19f, box.width - 14f, 16f), "Muted label", MutedText.Value);

            var button = new Rect(panel.x + 10f, box.yMax + 10f, 64f, 20f);
            EditorGUI.DrawRect(button, Accent.Value);
            DrawPreviewLabel(button, "Apply", Color.white, TextAnchor.MiddleCenter);

            var buttonDim = new Rect(button.xMax + 8f, button.y, 64f, 20f);
            EditorGUI.DrawRect(buttonDim, AccentDim.Value);
            DrawPreviewLabel(buttonDim, "Pressed", Color.white, TextAnchor.MiddleCenter);

            var canvas = new Rect(rect.x, toolbar.yMax, rect.width - panel.width, rect.height - toolbar.height);
            EditorGUI.DrawRect(canvas, CanvasBg.Value);
            for (float x = canvas.x + 22f; x < canvas.xMax; x += 30f)
            {
                EditorGUI.DrawRect(new Rect(x, canvas.y, 1f, canvas.height), GridThickLine.Value);
            }
            for (float y = canvas.y + 16f; y < canvas.yMax; y += 30f)
            {
                EditorGUI.DrawRect(new Rect(canvas.x, y, canvas.width, 1f), GridThickLine.Value);
            }

            var nodeBorder = new Rect(canvas.x + 22f, canvas.y + (canvas.height - 44f) * 0.5f, 136f, 44f);
            EditorGUI.DrawRect(nodeBorder, Accent.Value);
            var nodeBody = new Rect(nodeBorder.x + 2f, nodeBorder.y + 2f, nodeBorder.width - 4f, nodeBorder.height - 4f);
            EditorGUI.DrawRect(nodeBody, BoxBg.Value);
            DrawPreviewLabel(new Rect(nodeBody.x + 9f, nodeBody.y + 4f, nodeBody.width - 18f, 16f), "Selected State", Text.Value);
            DrawPreviewLabel(new Rect(nodeBody.x + 9f, nodeBody.y + 20f, nodeBody.width - 18f, 16f), "ANIMATION", MutedText.Value);

            EditorGUILayout.Space(8);
        }

        private static void DrawShareRow()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Theme", GUILayout.Width(107))) CopyThemeToClipboard();
                if (GUILayout.Button("Paste Theme", GUILayout.Width(107))) PasteThemeFromClipboard();
                GUILayout.FlexibleSpace();
            }
        }

        private static void CopyThemeToClipboard()
        {
            var builder = new System.Text.StringBuilder(ClipboardHeader);
            foreach (var pref in All)
            {
                builder.Append(';').Append(ColorUtility.ToHtmlStringRGBA(pref.Value));
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private static void PasteThemeFromClipboard()
        {
            string buffer = EditorGUIUtility.systemCopyBuffer;
            string[] parts = string.IsNullOrEmpty(buffer) ? null : buffer.Trim().Split(';');

            if (parts == null || parts.Length != All.Length + 1 || parts[0] != ClipboardHeader)
            {
                EditorUtility.DisplayDialog("Honami Appearance", "Clipboard does not contain a valid Honami theme.", "OK");
                return;
            }

            var colors = new Color[All.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                if (!ColorUtility.TryParseHtmlString("#" + parts[i + 1], out colors[i]))
                {
                    EditorUtility.DisplayDialog("Honami Appearance", "Clipboard does not contain a valid Honami theme.", "OK");
                    return;
                }
            }

            ApplyColors(colors);
        }

        private static void DrawPresetPopup()
        {
            var presets = HonamiThemePresets.All;
            int match = FindMatchingPreset();

            int index = match < 0 ? presets.Length : match;
            var options = new string[match < 0 ? presets.Length + 1 : presets.Length];
            for (int i = 0; i < presets.Length; i++) options[i] = presets[i].Name;
            if (match < 0) options[presets.Length] = "Custom";

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Theme", index, options);
            if (EditorGUI.EndChangeCheck() && selected != index && selected < presets.Length)
            {
                ApplyPreset(presets[selected]);
            }

            EditorGUILayout.Space(4);
        }

        [InitializeOnLoadMethod]
        private static void RegisterPreferences()
        {
            HonamiPreferences.Category("Appearance")
                .WithDescription("Colors of the Honami editor windows. Changes apply to open Honami windows automatically. Right-click a field to reset it.")
                .AddCustom(DrawPresetPopup, "Theme", "Preset")
                .AddCustom(DrawThemePreview)
                .AddHeader("Accent")
                .AddColor("Accent", () => Accent.Value, value => Accent.Value = value,
                    "Primary highlight color used across all Honami windows", Accent.ResetToDefault)
                .AddColor("Accent Dim", () => AccentDim.Value, value => AccentDim.Value = value,
                    "Darker accent used for pressed states and secondary highlights", AccentDim.ResetToDefault)
                .AddHeader("Backgrounds")
                .AddColor("Window", () => WindowBg.Value, value => WindowBg.Value = value, onReset: WindowBg.ResetToDefault)
                .AddColor("Panel", () => PanelBg.Value, value => PanelBg.Value = value, onReset: PanelBg.ResetToDefault)
                .AddColor("Toolbar", () => ToolbarBg.Value, value => ToolbarBg.Value = value, onReset: ToolbarBg.ResetToDefault)
                .AddColor("Box", () => BoxBg.Value, value => BoxBg.Value = value, onReset: BoxBg.ResetToDefault)
                .AddHeader("Canvas")
                .AddColor("Canvas Background", () => CanvasBg.Value, value => CanvasBg.Value = value,
                    "Background of the graph canvas", CanvasBg.ResetToDefault)
                .AddColor("Grid Line", () => GridLine.Value, value => GridLine.Value = value,
                    "Minor grid lines of the graph canvas", GridLine.ResetToDefault)
                .AddColor("Grid Thick Line", () => GridThickLine.Value, value => GridThickLine.Value = value,
                    "Major grid lines of the graph canvas", GridThickLine.ResetToDefault)
                .AddHeader("Text")
                .AddColor("Text", () => Text.Value, value => Text.Value = value, onReset: Text.ResetToDefault)
                .AddColor("Muted Text", () => MutedText.Value, value => MutedText.Value = value, onReset: MutedText.ResetToDefault)
                .AddCustom(DrawShareRow, "Copy", "Paste", "Share")
                .AddButton("Reset to Defaults", () =>
                {
                    if (EditorUtility.DisplayDialog("Honami Appearance", "Reset all appearance colors to their default values?", "Reset", "Cancel"))
                        ResetToDefaults();
                });
        }
    }
}
