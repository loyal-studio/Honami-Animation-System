#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed class TimelineToolbarView : VisualElement
    {
        private readonly TimelineState _state;
        private readonly Action _rebuild;

        private ObjectField _stateControllerField;
        private ObjectField _timelineField;
        private DropdownField _stateDropdown;
        private DropdownField _randomDropdown;
        private Slider _blendSlider;
        private Toggle _propsToggle;
        private TimelineMode _lastMode;

        public TimelineToolbarView(TimelineState state, Action rebuild)
        {
            _state = state;
            _rebuild = rebuild;

            name = "honami-timeline-toolbar";
            style.height = TimelineTheme.ToolbarHeight;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.backgroundColor = TimelineTheme.ToolbarBg;
            style.borderBottomWidth = 1;
            style.borderBottomColor = TimelineTheme.Divider;

            Build();
        }

        public void Refresh()
        {
            if (_state.Mode != _lastMode)
            {
                _lastMode = _state.Mode;
                Build();
                return;
            }

            _propsToggle?.SetValueWithoutNotify(_state.ShowProperties);
            if (_stateControllerField != null) _stateControllerField.SetValueWithoutNotify(_state.Controller);
            if (_timelineField != null) _timelineField.SetValueWithoutNotify(_state.ActiveTimeline);
            RefreshStateDropdown();
            RefreshRandomDropdown();
            RefreshBlendSlider();
        }

        private void Build()
        {
            _lastMode = _state.Mode;
            Clear();
            Add(AccentStrip());
            Add(ModeButton());

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.flexGrow = 1;
            scroll.style.height = Length.Percent(100);
            scroll.style.marginTop = scroll.style.marginBottom = 0;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.contentContainer.style.flexDirection = FlexDirection.Row;
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.height = Length.Percent(100);
            Add(scroll);

            scroll.Add(PickerRoot());
            scroll.Add(FlexibleSpace());
            scroll.Add(ViewOptionsRoot());
            scroll.Add(Separator());
            scroll.Add(PanelOptionsRoot());
        }

        private VisualElement AccentStrip()
        {
            return new VisualElement
            {
                style =
                {
                    width = 3,
                    height = Length.Percent(100),
                    backgroundColor = TimelineTheme.Accent,
                    marginRight = 8
                }
            };
        }

        private static VisualElement Separator()
        {
            return new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 18,
                    backgroundColor = TimelineTheme.SubtleLine,
                    marginLeft = 6,
                    marginRight = 6,
                    flexShrink = 0
                }
            };
        }

        private Button ModeButton()
        {
            string modeName = _state.Mode switch
            {
                TimelineMode.HonamiState => "Honami State",
                TimelineMode.HonamiTimeline => "Honami Timeline",
                TimelineMode.HonamiClipEdit => "Honami Clip Edit",
                _ => "Mode"
            };
            var button = HonamiToolbarControls.ToolbarButton(modeName, () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Honami State"), _state.Mode == TimelineMode.HonamiState, () =>
                {
                    _state.Mode = TimelineMode.HonamiState;
                    _state.SaveSettings();
                    _rebuild();
                });
                menu.AddItem(new GUIContent("Honami Timeline"), _state.Mode == TimelineMode.HonamiTimeline, () =>
                {
                    _state.Mode = TimelineMode.HonamiTimeline;
                    _state.SaveSettings();
                    _rebuild();
                });
                menu.AddItem(new GUIContent("Honami Clip Edit"), _state.Mode == TimelineMode.HonamiClipEdit, () =>
                {
                    _state.Mode = TimelineMode.HonamiClipEdit;
                    _state.SaveSettings();
                    _rebuild();
                });
                menu.ShowAsContext();
            });
            button.style.minWidth = 110;
            return button;
        }

        private VisualElement PickerRoot()
        {
            var root = Row();
            root.style.flexShrink = 1;
            if (_state.Mode == TimelineMode.HonamiState)
            {
                _stateControllerField = new ObjectField { objectType = typeof(HonamiController), value = _state.Controller };
                _stateControllerField.style.width = StyleKeyword.Auto;
                _stateControllerField.style.minWidth = 80;
                _stateControllerField.style.maxWidth = 155;
                _stateControllerField.style.flexGrow = 1;
                _stateControllerField.RegisterValueChangedCallback(evt =>
                {
                    _state.Controller = evt.newValue as HonamiController;
                    _state.SelectedState = _state.Controller?.states?
                        .FirstOrDefault(IsPreviewableState)
                        ?? _state.Controller?.states?.FirstOrDefault();
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _rebuild();
                });
                root.Add(_stateControllerField);

                _stateDropdown = new DropdownField { style = { width = StyleKeyword.Auto, minWidth = 100, maxWidth = 190, flexGrow = 1 } };
                _stateDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_state.Controller?.states == null) return;
                    _state.SelectedState = _state.Controller.states.FirstOrDefault(st => st != null && st.stateName == evt.newValue);
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _state.RandomPreviewIdx = 0;
                    _state.ClearSelection();
                    _rebuild();
                });
                root.Add(_stateDropdown);

                _randomDropdown = new DropdownField { style = { width = StyleKeyword.Auto, minWidth = 80, maxWidth = 150, flexGrow = 1 } };
                _randomDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_state.RandomPreviewOptions == null) return;
                    int idx = Array.IndexOf(_state.RandomPreviewOptions, evt.newValue);
                    if (idx >= 0) _state.RandomPreviewIdx = idx;
                    _rebuild();
                });
                root.Add(_randomDropdown);

                _blendSlider = new Slider("Blend") { style = { width = StyleKeyword.Auto, minWidth = 200, flexGrow = 1 } };
                _blendSlider.RegisterValueChangedCallback(evt =>
                {
                    _state.BlendPreviewValue = evt.newValue;
                    _rebuild();
                });
                root.Add(_blendSlider);

                RefreshStateDropdown();
                RefreshRandomDropdown();
                RefreshBlendSlider();
            }
            else if (_state.Mode == TimelineMode.HonamiTimeline)
            {
                _timelineField = new ObjectField { objectType = typeof(HonamiTimeline), value = _state.ActiveTimeline };
                _timelineField.style.width = StyleKeyword.Auto;
                _timelineField.style.minWidth = 120;
                _timelineField.style.maxWidth = 220;
                _timelineField.style.flexGrow = 1;
                _timelineField.RegisterValueChangedCallback(evt =>
                {
                    _state.ActiveTimeline = evt.newValue as HonamiTimeline;
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _state.ClearSelection();
                    _rebuild();
                });
                root.Add(_timelineField);
            }
            else if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                var clipField = new ObjectField { objectType = typeof(AnimationClip), value = _state.ActiveClip };
                clipField.style.width = StyleKeyword.Auto;
                clipField.style.minWidth = 100;
                clipField.style.maxWidth = 180;
                clipField.style.flexGrow = 1;
                clipField.RegisterValueChangedCallback(evt =>
                {
                    _state.ActiveClip = evt.newValue as AnimationClip;
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _rebuild();
                });
                root.Add(clipField);

                var contextField = new ObjectField { objectType = typeof(GameObject), value = _state.RecordingContext };
                contextField.style.width = StyleKeyword.Auto;
                contextField.style.minWidth = 100;
                contextField.style.maxWidth = 180;
                contextField.style.flexGrow = 1;
                contextField.tooltip = "Context (Target GameObject for Recording)";
                contextField.RegisterValueChangedCallback(evt =>
                {
                    _state.RecordingContext = evt.newValue as GameObject;
                    _rebuild();
                });
                root.Add(contextField);

                var search = new ToolbarSearchField { value = _state.ClipEditFilter };
                search.style.minWidth = 120;
                search.style.maxWidth = 240;
                search.style.flexGrow = 1;
                search.style.alignSelf = Align.Center;
                search.RegisterValueChangedCallback(evt =>
                {
                    _state.ClipEditFilter = evt.newValue ?? string.Empty;
                    _rebuild();
                });
                root.Add(search);

                root.Add(HonamiToolbarControls.ToolbarButton("Expand", () =>
                {
                    if (_state.ActiveClip == null) return;
                    foreach (var b in AnimationUtility.GetCurveBindings(_state.ActiveClip))
                        _state.ExpandedClipBones.Add(b.path);
                    _rebuild();
                }));
                root.Add(HonamiToolbarControls.ToolbarButton("Collapse", () =>
                {
                    _state.ExpandedClipBones.Clear();
                    _state.ExpandedClipGroups.Clear();
                    _rebuild();
                }));
            }
            return root;
        }

        private VisualElement ViewOptionsRoot()
        {
            var root = Row();
            root.Add(HonamiToolbarControls.ToolbarToggle("Snap", _state.SnapEnabled, value => { _state.SnapEnabled = value; _state.SaveSettings(); _rebuild(); }));
            root.Add(HonamiToolbarControls.ToolbarToggle("Frames", _state.ShowFrames, value => { _state.ShowFrames = value; _state.SaveSettings(); _rebuild(); }));
            root.Add(HonamiToolbarControls.ToolbarToggle("Keys", _state.ShowKeyframes, value => { _state.ShowKeyframes = value; _state.SaveSettings(); _rebuild(); }));
            return root;
        }

        private VisualElement PanelOptionsRoot()
        {
            var root = Row();
            _propsToggle = HonamiToolbarControls.ToolbarToggle("Props", _state.ShowProperties, value =>
            {
                _state.ShowProperties = value;
                _state.SaveSettings();
                _rebuild();
            });
            _propsToggle.tooltip = "Show / hide the properties panel";
            root.Add(_propsToggle);
            return root;
        }

        private void RefreshStateDropdown()
        {
            if (_stateDropdown == null || _state.Controller?.states == null) return;

            var states = _state.Controller.states;
            var choices = new List<string>();

            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];
                if (IsPreviewableState(st))
                    choices.Add(st.stateName);
            }

            _stateDropdown.choices = choices;
            string selectedName = string.Empty;
            if (_state.SelectedState != null)
                selectedName = _state.SelectedState.stateName;
            else if (choices.Count > 0)
                selectedName = choices[0];

            _stateDropdown.SetValueWithoutNotify(selectedName);
        }

        private void RefreshRandomDropdown()
        {
            if (_randomDropdown == null) return;
            bool visible = _state.IsRandomState && _state.RandomNode.randomClips != null && _state.RandomNode.randomClips.Count > 0;
            _randomDropdown.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;

            var clips = _state.RandomNode.randomClips;
            int count = clips.Count;
            if (_state.RandomPreviewOptions == null || _state.RandomPreviewOptions.Length != count)
                _state.RandomPreviewOptions = new string[count];

            var choices = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                var rc = clips[i];
                string opt = rc.clip != null ? $"{i}: {rc.clip.name}" : $"{i}: Empty";
                _state.RandomPreviewOptions[i] = opt;
                choices.Add(opt);
            }

            _randomDropdown.choices = choices;
            _state.RandomPreviewIdx = Mathf.Clamp(_state.RandomPreviewIdx, 0, count - 1);
            _randomDropdown.SetValueWithoutNotify(_state.RandomPreviewOptions[_state.RandomPreviewIdx]);
        }

        private void RefreshBlendSlider()
        {
            if (_blendSlider == null) return;

            bool visible = _state.IsBlendState && _state.BlendNode.blendMotions != null && _state.BlendNode.blendMotions.Count > 0;
            _blendSlider.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var motion in _state.BlendNode.blendMotions)
            {
                min = Mathf.Min(min, motion.threshold);
                max = Mathf.Max(max, motion.threshold);
            }

            if (min > max)
            {
                min = -1f;
                max = 1f;
            }
            else if (Mathf.Approximately(min, max))
            {
                min -= 1f;
                max += 1f;
            }

            _blendSlider.lowValue = min;
            _blendSlider.highValue = max;
            _state.BlendPreviewValue = Mathf.Clamp(_state.BlendPreviewValue, min, max);
            _blendSlider.SetValueWithoutNotify(_state.BlendPreviewValue);
            _blendSlider.label = string.IsNullOrEmpty(_state.BlendNode.blendParameter)
                ? "Blend"
                : _state.BlendNode.blendParameter;
        }

        private static bool IsPreviewableState(HonamiState state)
        {
            return state != null
                && (state.node is HonamiAnimationNode
                    || state.node is HonamiBlendTreeNode
                    || state.node is HonamiRandomAnimationNode
                    || state.node is HonamiSequencerNode);
        }

        private static VisualElement Row()
        {
            return new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minWidth = 0, flexShrink = 1 } };
        }

        private static VisualElement FlexibleSpace()
        {
            return new VisualElement { style = { flexGrow = 1 } };
        }
    }

}
#endif
