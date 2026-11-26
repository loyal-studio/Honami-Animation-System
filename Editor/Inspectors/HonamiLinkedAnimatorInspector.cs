using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiLinkedAnimatorInspector
    {
        public static VisualElement BuildNodeInspector(
            HonamiLinkedAnimatorNodeBase node,
            SerializedObject so,
            HonamiLinkedAnimatorGraph graph,
            HonamiLinkedAnimatorGraphView graphView)
        {
            VisualElement root = new VisualElement();
            root.Padding(12, 10);

            VisualElement breadcrumbs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = -8 } };
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(graph.name, new Color(0.3f, 0.7f, 1f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(" > ", new Color(0.4f, 0.4f, 0.4f)));
            string typeName = node.GetType().Name.Replace("LinkedAnimator", "").Replace("Node", "");
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(typeName, new Color(0.7f, 0.7f, 0.7f)));
            root.Add(breadcrumbs);

            Label title = HonamiGraphStyles.Title(typeName);
            root.Add(title);

            VisualElement propsBox = HonamiGraphStyles.Box();
            root.Add(propsBox);
            propsBox.Add(HonamiGraphStyles.SubTitle("Node Settings"));

            SerializedProperty prop = so.GetIterator();
            prop.NextVisible(true);

            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Script" || prop.name == "next" || prop.name == "editorPosition" || prop.name == "guid")
                    continue;

                if (node is LinkedAnimatorConditionNode && (prop.name == "onTrue" || prop.name == "onFalse"))
                    continue;

                PropertyField field = new PropertyField(prop);
                field.style.marginTop = field.style.marginBottom = 3;
                field.BindProperty(prop);
                propsBox.Add(field);
            }

            root.TrackSerializedObjectValue(so, _ =>
            {
                graphView?.UpdateLinkedAnimatorNodeUI(node);
            });

            root.Add(new VisualElement { style = { height = 12 } });
            TextField guidField = new TextField("GUID") { value = node.guid, isReadOnly = true };
            guidField.style.opacity = 0.5f;
            guidField.style.fontSize = 9;
            root.Add(guidField);

            return root;
        }

        public static VisualElement BuildEventInspector(
            HonamiLinkedAnimatorEvent evt,
            SerializedObject so,
            HonamiLinkedAnimatorGraph graph,
            HonamiLinkedAnimatorGraphView graphView,
            Action onNameChanged)
        {
            VisualElement root = new VisualElement();
            root.Padding(12, 10);

            VisualElement breadcrumbs = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = -8 } };
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(graph.name, new Color(0.3f, 0.7f, 1f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel(" > ", new Color(0.4f, 0.4f, 0.4f)));
            breadcrumbs.Add(HonamiGraphStyles.MiniLabel("Event Trigger", new Color(0.7f, 0.7f, 0.7f)));
            root.Add(breadcrumbs);

            Label title = HonamiGraphStyles.Title("Event: " + evt.eventName);
            root.Add(title);

            VisualElement propsBox = HonamiGraphStyles.Box();
            root.Add(propsBox);
            propsBox.Add(HonamiGraphStyles.SubTitle("Event Configuration"));

            SerializedProperty nameProp = so.FindProperty("eventName");
            PropertyField nameField = new PropertyField(nameProp, "Event Name");
            nameField.style.marginTop = nameField.style.marginBottom = 3;
            nameField.BindProperty(nameProp);
            nameField.RegisterValueChangeCallback(ev =>
            {
                title.text = "Event: " + ev.changedProperty.stringValue;
                onNameChanged?.Invoke();
            });
            propsBox.Add(nameField);

            root.TrackSerializedObjectValue(so, _ =>
            {
                graphView?.UpdateLinkedAnimatorEventUI(evt);
            });

            return root;
        }
    }
}
