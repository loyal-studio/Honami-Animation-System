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
        private VisualElement _nodeControls;
        private Toggle _propsToggle;
        private Toggle _selectTool;
        private Toggle _bladeTool;
        private Toggle _framesToggle;
        private Toggle _linksToggle;
        private DropdownField _frameRateField;
        private VisualElement _directorRoot;
        private TimelineMode _lastMode;

        private static readonly float[] FrameRatePresets = { 24f, 25f, 30f, 48f, 50f, 60f, 120f };

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
            _selectTool?.SetValueWithoutNotify(_state.Tool == TimelineEditTool.Select);
            _bladeTool?.SetValueWithoutNotify(_state.Tool == TimelineEditTool.Blade);
            _framesToggle?.SetValueWithoutNotify(_state.TimeFormat == TimelineTimeFormat.Frames);
            _linksToggle?.SetValueWithoutNotify(_state.ShowLinks);
            RefreshFrameRate();
            RefreshDirector();
            if (_stateControllerField != null) _stateControllerField.SetValueWithoutNotify(_state.Controller);
            if (_timelineField != null) _timelineField.SetValueWithoutNotify(_state.ActiveTimeline);
            RefreshStateDropdown();
            RefreshRandomDropdown();
            RefreshBlendSlider();
            RefreshNodeControls();
        }

        private void Build()
        {
            _lastMode = _state.Mode;
            _directorShown = false;
            _shownDirector = null;
            Clear();
            Add(HonamiToolbarControls.AccentStrip());
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
            scroll.Add(HonamiToolbarControls.FlexibleSpace());
            scroll.Add(ToolsRoot());
            scroll.Add(HonamiToolbarControls.Separator());
            scroll.Add(ViewOptionsRoot());
            scroll.Add(HonamiToolbarControls.Separator());
            scroll.Add(PanelOptionsRoot());
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
                _stateControllerField = new ObjectField { objectType = typeof(HonamiRuntimeController), value = _state.Controller };
                _stateControllerField.style.width = StyleKeyword.Auto;
                _stateControllerField.style.minWidth = 80;
                _stateControllerField.style.maxWidth = 155;
                _stateControllerField.style.flexGrow = 1;
                _stateControllerField.RegisterValueChangedCallback(evt =>
                {
                    _state.Controller = evt.newValue as HonamiRuntimeController;
                    _state.SelectedState = _state.Controller?.ActiveStates?
                        .FirstOrDefault(IsPreviewableState)
                        ?? _state.Controller?.ActiveStates?.FirstOrDefault();
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _rebuild();
                });
                root.Add(_stateControllerField);

                _stateDropdown = new DropdownField { style = { width = StyleKeyword.Auto, minWidth = 100, maxWidth = 190, flexGrow = 1 } };
                _stateDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_state.Controller?.ActiveStates == null) return;
                    _state.SelectedState = _state.Controller.ActiveStates.FirstOrDefault(st => st != null && st.stateName == evt.newValue);
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

                _nodeControls = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexGrow = 1 }
                };
                root.Add(_nodeControls);

                RefreshStateDropdown();
                RefreshRandomDropdown();
                RefreshBlendSlider();
                RefreshNodeControls();
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
                    _state.ForgetDirector();
                    _rebuild();
                });
                root.Add(_timelineField);

                _frameRateField = new DropdownField { tooltip = "Frame rate of the timeline's edit grid, snapping and timecode", style = { width = 82 } };
                _frameRateField.RegisterValueChangedCallback(evt =>
                {
                    var timeline = _state.ActiveTimeline;
                    if (timeline == null) return;
                    int index = _frameRateField.choices.IndexOf(evt.newValue);
                    if (index < 0 || index >= FrameRatePresets.Length) return;
                    Undo.RecordObject(timeline, "Set Frame Rate");
                    timeline.FrameRate = FrameRatePresets[index];
                    EditorUtility.SetDirty(timeline);
                    _rebuild();
                });
                root.Add(_frameRateField);

                _directorRoot = Row();
                root.Add(_directorRoot);
                RefreshFrameRate();
                RefreshDirector();
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

        private VisualElement ToolsRoot()
        {
            var root = Row();
            _selectTool = HonamiToolbarControls.ToolbarToggle("Select", _state.Tool == TimelineEditTool.Select, value =>
            {
                _state.Tool = TimelineEditTool.Select;
                _rebuild();
            });
            _selectTool.tooltip = "Select, move and trim items (V)";
            root.Add(_selectTool);

            _bladeTool = HonamiToolbarControls.ToolbarToggle("Blade", _state.Tool == TimelineEditTool.Blade, value =>
            {
                _state.Tool = value ? TimelineEditTool.Blade : TimelineEditTool.Select;
                _rebuild();
            });
            _bladeTool.tooltip = "Click an item to cut it in two (B). Esc returns to Select.";
            root.Add(_bladeTool);

            var links = HonamiToolbarControls.ToolbarButton("Links", () =>
            {
                var panel = parent?.Q<TimelinePanelView>();
                panel?.OpenLinksMenu();
            });
            links.tooltip = "Link selected items so they move together on the timeline, or lay them out one after another (Ctrl+L).";
            root.Add(links);
            return root;
        }

        private VisualElement ViewOptionsRoot()
        {
            var root = Row();
            var snap = HonamiToolbarControls.ToolbarToggle("Snap", _state.SnapEnabled, value => { _state.SnapEnabled = value; _state.SaveSettings(); _rebuild(); });
            snap.tooltip = "Snap to item edges, markers, the playhead and frames. Hold Shift while dragging to invert.";
            root.Add(snap);
            _framesToggle = HonamiToolbarControls.ToolbarToggle("Frames", _state.TimeFormat == TimelineTimeFormat.Frames, value =>
            {
                _state.ShowFrames = value;
                _state.SaveSettings();
                _rebuild();
            });
            _framesToggle.tooltip = "On: seconds and frame numbers. Off: timecode (minutes:seconds:frames); right-click the ruler for plain seconds.";
            root.Add(_framesToggle);
            root.Add(HonamiToolbarControls.ToolbarToggle("Keys", _state.ShowKeyframes, value => { _state.ShowKeyframes = value; _state.SaveSettings(); _rebuild(); }));

            _linksToggle = HonamiToolbarControls.ToolbarToggle("Show Links", _state.ShowLinks, value => { _state.ShowLinks = value; _state.SaveSettings(); _rebuild(); });
            _linksToggle.tooltip = "Highlight linked items with a coloured strip and badge.";
            root.Add(_linksToggle);

            return root;
        }

        private void RefreshFrameRate()
        {
            if (_frameRateField == null) return;
            var timeline = _state.ActiveTimeline;
            _frameRateField.SetEnabled(timeline != null);

            var choices = new List<string>();
            foreach (float preset in FrameRatePresets) choices.Add($"{preset:0.##} fps");
            _frameRateField.choices = choices;
            float rate = timeline != null ? timeline.FrameRate : HonamiTimeline.DefaultFrameRate;
            _frameRateField.SetValueWithoutNotify($"{rate:0.##} fps");
        }

        private HonamiDirector _shownDirector;
        private bool _directorShown;

        private void RefreshDirector()
        {
            if (_directorRoot == null) return;
            var director = _state.ResolveDirector();
            if (_directorShown && director == _shownDirector && _directorRoot.childCount > 0) return;
            _directorShown = true;
            _shownDirector = director;

            _directorRoot.Clear();
            if (_state.ActiveTimeline == null) return;

            if (director == null)
            {
                var create = HonamiToolbarControls.ToolbarButton("+ Director", () =>
                {
                    var created = HonamiTimelineEditContext.CreateDirector(_state.ActiveTimeline);
                    Selection.activeGameObject = created != null ? created.gameObject : null;
                    _state.ForgetDirector();
                    _directorShown = false;
                    _rebuild();
                });
                create.tooltip = "No Honami Director in the open scenes plays this timeline. Add one to bind scene objects and play it at runtime.";
                create.style.color = TimelineTheme.EndBoundary;
                _directorRoot.Add(create);
                return;
            }

            var ping = HonamiToolbarControls.ToolbarButton(director.name, () =>
            {
                EditorGUIUtility.PingObject(director);
                Selection.activeGameObject = director.gameObject;
            });
            ping.tooltip = "Director that plays this timeline and stores its scene bindings. Click to select it.";
            _directorRoot.Add(ping);
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

            var help = HonamiToolbarControls.ToolbarButton("?", () =>
            {
                var panel = parent?.Q<TimelinePanelView>();
                panel?.ToggleHelpFromToolbar();
            });
            help.tooltip = "Keyboard shortcuts (? or F1)";
            root.Add(help);
            return root;
        }

        private void RefreshStateDropdown()
        {
            if (_stateDropdown == null || _state.Controller?.ActiveStates == null) return;
            
            var states = _state.Controller.ActiveStates;
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

        private void RefreshNodeControls()
        {
            if (_nodeControls == null) return;
            _nodeControls.Clear();

            var node = _state.SelectedState?.node;
            if (node == null) return;

            HonamiNodeEditorRegistry.GetEditor(node)?.BuildTimelineControls(_state, _nodeControls, _rebuild);
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

        internal static bool IsPreviewableState(HonamiState state)
        {
            if (state?.node == null) return false;
            return HonamiNodeEditorRegistry.GetEditor(state.node)?.SupportsTimelinePreview == true;
        }

        private static VisualElement Row()
        {
            return new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, minWidth = 0, flexShrink = 1 } };
        }

    }

}
