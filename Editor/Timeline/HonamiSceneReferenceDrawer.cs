using System.Reflection;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    // Timeline assets cannot hold scene objects, so the field edits the Director's reference table under the reference's key.
    [CustomPropertyDrawer(typeof(HonamiSceneReference))]
    public sealed class HonamiSceneReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var keyProperty = property.FindPropertyRelative("key");
            var type = fieldInfo?.GetCustomAttribute<HonamiSceneReferenceTypeAttribute>()?.Type ?? typeof(Object);
            string label = property.displayName;

            void Build()
            {
                root.Clear();
                var target = property.serializedObject.targetObject;
                var timeline = target as HonamiTimeline;
                if (timeline == null && target is HonamiTimelineModule module)
                {
                    string assetPath = AssetDatabase.GetAssetPath(module);
                    if (!string.IsNullOrEmpty(assetPath))
                        timeline = AssetDatabase.LoadAssetAtPath<HonamiTimeline>(assetPath);
                }

                if (timeline == null)
                {
                    var keyField = new PropertyField(keyProperty, label)
                    {
                        tooltip = "Binding key looked up on the animator's binding providers (Honami Timeline Binding Receiver)."
                    };
                    keyField.BindProperty(keyProperty);
                    root.Add(keyField);
                    return;
                }

                var director = HonamiTimelineEditContext.FindDirector(timeline);
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var field = new ObjectField(label)
                {
                    objectType = type,
                    allowSceneObjects = true,
                    style = { flexGrow = 1, flexShrink = 1 }
                };
                field.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
                row.Add(field);
                root.Add(row);

                if (director == null)
                {
                    field.SetEnabled(false);
                    field.tooltip = "Scene objects are stored on a Honami Director that plays this timeline.";
                    row.Add(new Button(() =>
                    {
                        HonamiTimelineEditContext.CreateDirector(timeline);
                        Build();
                    }) { text = "Create Director", tooltip = "Adds a Honami Director for this timeline to the open scene" });
                    return;
                }

                field.value = HonamiTimelineModuleBinding.Convert(director.GetReference(keyProperty.stringValue), type);
                field.tooltip = $"Stored on '{director.name}'";
                field.RegisterValueChangedCallback(evt =>
                {
                    keyProperty.serializedObject.Update();
                    if (string.IsNullOrEmpty(keyProperty.stringValue))
                    {
                        keyProperty.stringValue = HonamiSceneReference.NewKey().Key;
                        keyProperty.serializedObject.ApplyModifiedProperties();
                    }

                    Undo.RecordObject(director, "Assign Scene Reference");
                    director.SetReference(keyProperty.stringValue, evt.newValue);
                    EditorUtility.SetDirty(director);
                });
            }

            Build();
            return root;
        }
    }
}
