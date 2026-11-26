using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineCameraModule), "Camera Shots", 5)]
    public sealed class TimelineCameraModuleEditor : HonamiTimelineModuleEditor
    {
        private static readonly Color CameraColor = new(0.72f, 0.48f, 0.20f, 0.96f);
        private static readonly Dictionary<Camera, HonamiCameraPose> GameplayPoses = new();
        private static HonamiTimelinePreviewContext _previewContext;
        private static readonly Func<HonamiSceneReference, Transform> PreviewResolve = reference => _previewContext?.Resolve<Transform>(reference);
        private static int[] _order;

        public override Color ModuleColor => CameraColor;
        public override Texture Icon => EditorGUIUtility.IconContent("Camera Icon").image;
        public override string ItemDisplayName => "Shot";

        public override string GetItemLabel(HonamiTimelineEditContext context, HonamiTimelineItem item)
        {
            var shot = (TimelineCameraShotItem)item;
            if (!shot.anchor.IsAssigned) return "(No Anchor)";
            var anchor = context.Resolve<Transform>(shot.anchor);
            if (anchor != null) return anchor.name;
            return context.Director == null ? "Shot (no Director)" : "(Missing Anchor)";
        }

        public override HonamiTimelineItem CreateItem(HonamiTimelineEditContext context, float time) =>
            new TimelineCameraShotItem { time = time };

        public override void OnItemDoubleClicked(HonamiTimelineEditContext context, HonamiTimelineItem item, float time)
        {
            var anchor = context.Resolve<Transform>(((TimelineCameraShotItem)item).anchor);
            if (anchor == null) return;
            Selection.activeTransform = anchor;
            MoveSceneViewTo(anchor);
        }

        public override void AddContextMenuItems(HonamiTimelineEditContext context, GenericMenu menu, float time)
        {
            menu.AddItem(new GUIContent("New Shot From Scene View"), false, () =>
            {
                var shot = new TimelineCameraShotItem { time = Mathf.Max(0f, time) };
                context.RecordUndo("New Shot");
                context.Module.AddItem(shot);
                CreateAnchorFromSceneView(context, shot);
                context.MarkChanged();
            });

            TimelineCameraShotItem selected = null;
            foreach (var item in context.SelectedItems)
            {
                if (item is TimelineCameraShotItem shot && context.Module.Contains(shot))
                {
                    selected = shot;
                    break;
                }
            }
            if (selected == null) return;

            var anchor = context.Resolve<Transform>(selected.anchor);
            if (anchor != null)
            {
                menu.AddItem(new GUIContent("Align Shot To Scene View"), false, () => AlignAnchorToSceneView(anchor));
                menu.AddItem(new GUIContent("Move Scene View To Shot"), false, () => MoveSceneViewTo(anchor));
            }
            else
            {
                menu.AddItem(new GUIContent("Create Anchor From Scene View"), false, () =>
                {
                    context.RecordUndo("Create Shot Anchor");
                    CreateAnchorFromSceneView(context, selected);
                    context.MarkChanged();
                });
            }
        }

        public override void BuildItemInspector(HonamiTimelineEditContext context, SerializedProperty itemProperty, VisualElement root)
        {
            base.BuildItemInspector(context, itemProperty, root);
            if (context.Host != HonamiTimelineModuleHost.Timeline) return;

            var shot = FindShot(context, itemProperty);
            if (shot == null) return;

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            var anchor = context.Resolve<Transform>(shot.anchor);
            if (anchor == null)
            {
                row.Add(new Button(() =>
                {
                    context.RecordUndo("Create Shot Anchor");
                    CreateAnchorFromSceneView(context, shot);
                    context.MarkChanged();
                }) { text = "Create Anchor From Scene View", style = { flexGrow = 1 } });
            }
            else
            {
                row.Add(new Button(() => AlignAnchorToSceneView(anchor)) { text = "Align To Scene View", style = { flexGrow = 1 } });
                row.Add(new Button(() => MoveSceneViewTo(anchor)) { text = "View Shot", style = { flexGrow = 1 } });
            }
            root.Add(row);
        }

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            var camera = context.Binding as Camera;
            if (camera == null) camera = Camera.main;
            if (camera == null) return;

            if (!GameplayPoses.TryGetValue(camera, out var gameplay))
            {
                gameplay = HonamiCameraPose.Of(camera);
                GameplayPoses[camera] = gameplay;
            }

            _previewContext = context;
            bool hold = context.Director != null && context.Director.WrapMode == HonamiDirectorWrapMode.Hold;
            bool driven = ((TimelineCameraModule)context.Module).Evaluate(context.Time, gameplay, PreviewResolve, ref _order, out var pose, hold);
            _previewContext = null;

            if (!driven) return;

            HonamiTimelinePreviewContext.DriveTransform(camera.transform);
            HonamiTimelinePreviewContext.DriveProperty(camera, "field of view");
            pose.ApplyTo(camera);
        }

        public override void StopPreview() => GameplayPoses.Clear();

        private static TimelineCameraShotItem FindShot(HonamiTimelineEditContext context, SerializedProperty itemProperty)
        {
            string path = itemProperty.propertyPath;
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            if (open < 0 || close <= open || !int.TryParse(path.Substring(open + 1, close - open - 1), out int index)) return null;
            return index < context.Module.ItemCount ? context.Module.GetItem(index) as TimelineCameraShotItem : null;
        }

        private static void CreateAnchorFromSceneView(HonamiTimelineEditContext context, TimelineCameraShotItem shot)
        {
            var director = context.Director;
            if (director == null && context.Timeline != null)
                director = HonamiTimelineEditContext.CreateDirector(context.Timeline);
            if (director == null) return;

            var sceneView = SceneView.lastActiveSceneView;
            var gameObject = new GameObject($"Shot {context.Module.ItemCount}");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Shot Anchor");
            gameObject.transform.SetParent(director.transform, false);

            var shotCamera = gameObject.AddComponent<Camera>();
            shotCamera.enabled = false;
            if (sceneView != null && sceneView.camera != null)
            {
                gameObject.transform.SetPositionAndRotation(sceneView.camera.transform.position, sceneView.camera.transform.rotation);
                shotCamera.fieldOfView = sceneView.cameraSettings.fieldOfView;
            }

            if (!shot.anchor.IsAssigned) shot.anchor = HonamiSceneReference.NewKey();
            Undo.RecordObject(director, "Assign Shot Anchor");
            director.SetReference(shot.anchor.Key, gameObject.transform);
            EditorUtility.SetDirty(director);
        }

        private static void AlignAnchorToSceneView(Transform anchor)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null || anchor == null) return;

            Undo.RecordObject(anchor, "Align Shot To Scene View");
            anchor.SetPositionAndRotation(sceneView.camera.transform.position, sceneView.camera.transform.rotation);
            if (anchor.TryGetComponent(out Camera shotCamera))
            {
                Undo.RecordObject(shotCamera, "Align Shot To Scene View");
                shotCamera.fieldOfView = sceneView.cameraSettings.fieldOfView;
            }
        }

        private static void MoveSceneViewTo(Transform anchor)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || anchor == null) return;
            sceneView.AlignViewToObject(anchor);
            if (anchor.TryGetComponent(out Camera shotCamera))
                sceneView.cameraSettings.fieldOfView = shotCamera.fieldOfView;
            sceneView.Repaint();
        }
    }
}
