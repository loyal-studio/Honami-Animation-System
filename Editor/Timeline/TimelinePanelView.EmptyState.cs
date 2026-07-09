#if UNITY_EDITOR
using System;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private void DrawEmptyState()
        {
            string msg = _state.Mode switch
            {
                TimelineMode.HonamiTimeline => "No Timeline Selected",
                TimelineMode.HonamiState => "No State Selected",
                TimelineMode.HonamiClipEdit => "No Clip Selected",
                _ => "Select an item to edit"
            };

            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = _state.TrackHeaderWidth;
            container.style.right = 0;
            container.style.top = 0;
            container.style.bottom = 0;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            IgnorePicking(container);
            _content.Add(container);

            var tex = _state.Mode == TimelineMode.HonamiClipEdit
                ? LoadClipIcon()
                : EditorGUIUtility.Load("Assets/HonamiAnimationSystem/Editor/Resources/Icons/HonamiTimeline.png") as Texture2D;
            var box = HonamiEmptyStatePanel.Box(tex, "Honami Animation System", msg);
            container.Add(box);

            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                box.Add(HonamiEmptyStatePanel.CTAButton("Create New Animation Clip", CreateNewClip));
                box.Add(HonamiEmptyStatePanel.HintLabel("Please select a clip or create a new one to start recording."));
            }
            else if (_state.Mode == TimelineMode.HonamiTimeline)
            {
                box.Add(HonamiEmptyStatePanel.CTAButton("Create New Timeline", CreateNewTimeline));
            }
            else if (_state.Mode == TimelineMode.HonamiState)
            {
                box.Add(HonamiEmptyStatePanel.CTAButton("Select Honami Controller", ShowControllerPicker));
                box.Add(HonamiEmptyStatePanel.HintLabel("You can also drop a Honami Controller into the toolbar field above."));
            }
        }

        private static readonly int ControllerPickerControlId = "HonamiTimelineControllerPicker".GetHashCode();
        private HonamiController _lastPickedController;

        private void ShowControllerPicker()
        {
            EditorApplication.delayCall += () =>
            {
                _lastPickedController = null;
                EditorGUIUtility.ShowObjectPicker<HonamiController>(null, false, "", ControllerPickerControlId);
                EditorApplication.update -= PollControllerPicker;
                EditorApplication.update += PollControllerPicker;
            };
        }

        private void PollControllerPicker()
        {
            if (panel == null)
            {
                EditorApplication.update -= PollControllerPicker;
                return;
            }

            if (EditorGUIUtility.GetObjectPickerControlID() == ControllerPickerControlId)
            {
                if (EditorGUIUtility.GetObjectPickerObject() is HonamiController picked)
                    _lastPickedController = picked;
                return;
            }

            EditorApplication.update -= PollControllerPicker;
            if (EditorGUIUtility.GetObjectPickerObject() is HonamiController closedPick)
                _lastPickedController = closedPick;
            if (_lastPickedController != null)
                SelectController(_lastPickedController);
            _lastPickedController = null;
        }

        private void SelectController(HonamiController controller)
        {
            _state.Controller = controller;
            _state.SelectedState = FirstPreviewableState(controller);
            _state.SelectedLayerIndex = _state.SelectedState != null ? _state.SelectedState.layerIndex : -1;
            _state.PlayheadTime = 0f;
            _state.IsPlaying = false;
            _rebuild();
        }

        private static HonamiState FirstPreviewableState(HonamiController controller)
        {
            if (controller == null || controller.states == null) return null;
            HonamiState first = null;
            foreach (var st in controller.states)
            {
                if (st == null) continue;
                first ??= st;
                if (TimelineToolbarView.IsPreviewableState(st)) return st;
            }
            return first;
        }

        private static Texture2D _clipIcon;

        private static Texture2D LoadClipIcon()
        {
            if (_clipIcon != null) return _clipIcon;

            _clipIcon = EditorGUIUtility.FindTexture("AnimationClip Icon@2x");
            if (_clipIcon == null)
                _clipIcon = EditorGUIUtility.Load("Icons/AnimationClip Icon@2x.png") as Texture2D;
            if (_clipIcon == null)
                _clipIcon = EditorGUIUtility.IconContent("AnimationClip Icon").image as Texture2D;
            return _clipIcon;
        }

        private void CreateNewTimeline()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Honami Timeline", "New Timeline", "asset", "Please enter a file name");
            if (string.IsNullOrEmpty(path)) return;
            var timeline = ScriptableObject.CreateInstance<HonamiTimeline>();
            AssetDatabase.CreateAsset(timeline, path);
            AssetDatabase.SaveAssets();
            _state.ActiveTimeline = timeline;
            _rebuild();
        }

        private void CreateNewClip()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Animation Clip", "New Animation", "anim", "Please enter a file name to save the animation clip to");
            if (string.IsNullOrEmpty(path)) return;

            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            _state.ActiveClip = clip;
            _rebuild();
        }
    }
}
#endif
