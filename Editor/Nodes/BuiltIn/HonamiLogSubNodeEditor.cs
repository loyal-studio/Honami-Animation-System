using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    [CustomEditor(typeof(HonamiLogSubNode))]
    public sealed class HonamiLogSubNodeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();

            SerializedProperty logEnter = serializedObject.FindProperty("_logOnEnter");
            SerializedProperty logUpdate = serializedObject.FindProperty("_logOnUpdate");
            SerializedProperty logExit = serializedObject.FindProperty("_logOnExit");
            SerializedProperty logType = serializedObject.FindProperty("_logType");
            SerializedProperty format = serializedObject.FindProperty("_messageFormat");

            VisualElement settingsBox = HonamiGraphStyles.Box();
            settingsBox.Add(HonamiGraphStyles.SubTitle("Logging Settings"));

            settingsBox.Add(new PropertyField(logEnter, "On State Enter"));
            settingsBox.Add(new PropertyField(logUpdate, "On State Update"));
            settingsBox.Add(new PropertyField(logExit, "On State Exit"));
            settingsBox.Add(new PropertyField(logType, "Log Type"));
            root.Add(settingsBox);

            VisualElement formatBox = HonamiGraphStyles.Box();
            formatBox.Add(HonamiGraphStyles.SubTitle("Message Format"));

            PropertyField formatField = new PropertyField(format, "Format String");
            formatBox.Add(formatField);

            VisualElement tokenContainer = HonamiGraphStyles.ListBox();
            tokenContainer.Add(HonamiGraphStyles.MiniLabel("Quick Tokens:", HonamiGraphStyles.SubTitleClr));

            VisualElement row1 = HonamiGraphStyles.Row();
            row1.style.flexWrap = Wrap.Wrap;
            row1.Add(TokenButton("{state}", "Current State Name", format));
            row1.Add(TokenButton("{previousState}", "Previous State Name", format));
            row1.Add(TokenButton("{controller}", "Controller Name", format));
            row1.Add(TokenButton("{layer}", "Layer Index", format));
            row1.Add(TokenButton("{time}", "Runtime Time", format));
            row1.Add(TokenButton("{normalizedTime}", "0.0 - 1.0 Progress", format));
            tokenContainer.Add(row1);

            VisualElement row2 = HonamiGraphStyles.Row();
            row2.style.marginTop = 4;
            row2.Add(TokenButton("{param:NAME}", "Parameter Value", format));
            tokenContainer.Add(row2);

            formatBox.Add(tokenContainer);
            root.Add(formatBox);

            root.Bind(serializedObject);
            return root;
        }

        private Button TokenButton(string token, string tooltip, SerializedProperty prop)
        {
            Button btn = new Button(() =>
            {
                serializedObject.Update();
                prop.stringValue += token;
                serializedObject.ApplyModifiedProperties();
            });
            btn.text = token;
            btn.tooltip = tooltip;
            btn.style.fontSize = 10;
            btn.style.paddingLeft = btn.style.paddingRight = 6;
            btn.style.height = 20;
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            return btn;
        }
    }
}
