#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    /// <summary>
    /// Top toolbar for the blend tree window: controller and blend-state pickers plus
    /// framing, threshold distribution and properties-panel toggle actions.
    /// </summary>
    internal sealed class BlendTreeToolbarView : VisualElement
    {
        private readonly BlendTreeState _state;
        private readonly Action _rebuild;
        private readonly Func<BlendTreePanelView> _getPanel;

        private ObjectField _controllerField;
        private DropdownField _stateDropdown;
        private Label _blendTypeLabel;
        private Button _distributeButton;
        private Toggle _propsToggle;
        private Toggle _previewToggle;
        private readonly List<string> _stateChoices = new();
        private readonly List<string> _stateGuids = new();

        public BlendTreeToolbarView(BlendTreeState state, Action rebuild, Func<BlendTreePanelView> getPanel)
        {
            _state = state;
            _rebuild = rebuild;
            _getPanel = getPanel;

            name = "honami-blendtree-toolbar";
            style.height = BlendTreeTheme.ToolbarHeight;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.backgroundColor = BlendTreeTheme.ToolbarBg;
            style.borderBottomWidth = 1;
            style.borderBottomColor = BlendTreeTheme.Divider;

            Build();
        }

        public void Refresh()
        {
            _controllerField?.SetValueWithoutNotify(_state.Controller);
            _propsToggle?.SetValueWithoutNotify(_state.ShowProperties);
            _previewToggle?.SetValueWithoutNotify(_state.ShowPreview);
            RefreshStateDropdown();
            RefreshBlendTypeLabel();
            _distributeButton?.SetEnabled(!_state.IsDirectional2D);
        }

        private void Build()
        {
            Clear();
            Add(AccentStrip());

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.flexGrow = 1;
            scroll.style.height = Length.Percent(100);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.contentContainer.style.flexDirection = FlexDirection.Row;
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.height = Length.Percent(100);
            Add(scroll);

            _controllerField = new ObjectField { objectType = typeof(HonamiController), value = _state.Controller };
            _controllerField.style.width = StyleKeyword.Auto;
            _controllerField.style.minWidth = 100;
            _controllerField.style.maxWidth = 190;
            _controllerField.style.flexGrow = 1;
            _controllerField.RegisterValueChangedCallback(evt =>
            {
                _state.Controller = evt.newValue as HonamiController;
                _state.StateGuid = FirstBlendStateGuid(_state.Controller);
                _state.ResetForNewTarget();
                _rebuild();
            });
            scroll.Add(_controllerField);

            _stateDropdown = new DropdownField { style = { width = StyleKeyword.Auto, minWidth = 110, maxWidth = 200, flexGrow = 1 } };
            _stateDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = _stateChoices.IndexOf(evt.newValue);
                if (idx < 0 || idx >= _stateGuids.Count) return;
                _state.StateGuid = _stateGuids[idx];
                _state.ResetForNewTarget();
                _rebuild();
            });
            scroll.Add(_stateDropdown);

            _blendTypeLabel = new Label
            {
                style =
                {
                    color = BlendTreeTheme.MutedText,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 6,
                    flexShrink = 0
                }
            };
            scroll.Add(_blendTypeLabel);

            scroll.Add(FlexibleSpace());

            scroll.Add(HonamiToolbarControls.ToolbarButton("Frame All", () => _getPanel()?.FrameAll()));
            _distributeButton = HonamiToolbarControls.ToolbarButton("Distribute Evenly", () => _getPanel()?.DistributeThresholds());
            scroll.Add(_distributeButton);
            scroll.Add(Separator());

            _propsToggle = HonamiToolbarControls.ToolbarToggle("Props", _state.ShowProperties, value =>
            {
                _state.ShowProperties = value;
                _state.SaveSettings();
                _rebuild();
            });
            _propsToggle.tooltip = "Show / hide the properties panel";
            scroll.Add(_propsToggle);

            _previewToggle = HonamiToolbarControls.ToolbarToggle("Preview", _state.ShowPreview, value =>
            {
                _state.ShowPreview = value;
                _state.SaveSettings();
                _rebuild();
            });
            _previewToggle.tooltip = "Show / hide the live skeleton preview";
            scroll.Add(_previewToggle);

            Refresh();
        }

        private void RefreshStateDropdown()
        {
            if (_stateDropdown == null) return;

            _stateChoices.Clear();
            _stateGuids.Clear();

            if (_state.Controller != null && _state.Controller.states != null)
            {
                foreach (var st in _state.Controller.states)
                {
                    if (st == null || st.node is not HonamiBlendTreeNode) continue;
                    _stateChoices.Add(st.stateName);
                    _stateGuids.Add(st.guid);
                }
            }

            _stateDropdown.choices = _stateChoices;
            _stateDropdown.SetEnabled(_stateChoices.Count > 0);

            int selected = _stateGuids.IndexOf(_state.StateGuid);
            _stateDropdown.SetValueWithoutNotify(selected >= 0 ? _stateChoices[selected] : string.Empty);
        }

        private void RefreshBlendTypeLabel()
        {
            if (_blendTypeLabel == null) return;
            _blendTypeLabel.text = _state.Node != null ? _state.Node.blendType.ToString().ToUpperInvariant() : string.Empty;
        }

        private static string FirstBlendStateGuid(HonamiController controller)
        {
            if (controller == null || controller.states == null) return string.Empty;
            foreach (var st in controller.states)
            {
                if (st != null && st.node is HonamiBlendTreeNode)
                    return st.guid;
            }
            return string.Empty;
        }

        private static VisualElement AccentStrip()
        {
            return new VisualElement
            {
                style =
                {
                    width = 3,
                    height = Length.Percent(100),
                    backgroundColor = BlendTreeTheme.Accent,
                    marginRight = 8,
                    flexShrink = 0
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
                    backgroundColor = BlendTreeTheme.SubtleLine,
                    marginLeft = 6,
                    marginRight = 6,
                    flexShrink = 0
                }
            };
        }

        private static VisualElement FlexibleSpace()
        {
            return new VisualElement { style = { flexGrow = 1 } };
        }
    }
}
#endif
