using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Top toolbar for the blend space window: controller and state pickers, the plotted axis pair
    /// for spaces with more than two axes, and the panel toggles.
    /// </summary>
    internal sealed class BlendSpaceToolbarView : VisualElement
    {
        private readonly BlendSpaceState _state;
        private readonly Action _rebuild;

        private ObjectField _controllerField;
        private DropdownField _stateDropdown;
        private Label _spaceTypeLabel;
        private VisualElement _plotGroup;
        private Toggle _propsToggle;
        private Toggle _previewToggle;

        private readonly List<string> _stateChoices = new();
        private readonly List<string> _stateGuids = new();

        public BlendSpaceToolbarView(BlendSpaceState state, Action rebuild)
        {
            _state = state;
            _rebuild = rebuild;

            name = "honami-blendspace-toolbar";
            style.height = BlendSpaceSettings.ToolbarHeight;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.backgroundColor = HonamiEditorTheme.ToolbarBg;
            style.borderBottomWidth = 1;
            style.borderBottomColor = HonamiEditorTheme.Divider;

            Build();
        }

        private void Build()
        {
            Clear();
            Add(HonamiToolbarControls.AccentStrip());

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
                _state.StateGuid = FirstBlendSpaceGuid(_state.Controller);
                _state.ResetForNewTarget();
                _rebuild();
            });
            scroll.Add(_controllerField);

            _stateDropdown = new DropdownField { style = { width = StyleKeyword.Auto, minWidth = 110, maxWidth = 200, flexGrow = 1 } };
            _stateDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = _stateChoices.IndexOf(evt.newValue);
                if (index < 0 || index >= _stateGuids.Count) return;
                _state.StateGuid = _stateGuids[index];
                _state.ResetForNewTarget();
                _rebuild();
            });
            scroll.Add(_stateDropdown);

            _spaceTypeLabel = new Label
            {
                style =
                {
                    color = HonamiEditorTheme.MutedText,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 6,
                    flexShrink = 0
                }
            };
            scroll.Add(_spaceTypeLabel);

            _plotGroup = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 10, flexShrink = 0 }
            };
            scroll.Add(_plotGroup);

            scroll.Add(HonamiToolbarControls.FlexibleSpace());

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

        public void Refresh()
        {
            _controllerField?.SetValueWithoutNotify(_state.Controller);
            _propsToggle?.SetValueWithoutNotify(_state.ShowProperties);
            _previewToggle?.SetValueWithoutNotify(_state.ShowPreview);
            RefreshStateDropdown();
            RefreshSpaceType();
            RefreshPlotGroup();
        }

        private void RefreshStateDropdown()
        {
            if (_stateDropdown == null) return;

            _stateChoices.Clear();
            _stateGuids.Clear();

            if (_state.Controller?.states != null)
            {
                foreach (var candidate in _state.Controller.states)
                {
                    if (candidate == null || candidate.node is not HonamiBlendSpaceNode) continue;
                    _stateChoices.Add(candidate.stateName);
                    _stateGuids.Add(candidate.guid);
                }
            }

            _stateDropdown.choices = _stateChoices;
            _stateDropdown.SetEnabled(_stateChoices.Count > 0);

            int selected = _stateGuids.IndexOf(_state.StateGuid);
            _stateDropdown.SetValueWithoutNotify(selected >= 0 ? _stateChoices[selected] : string.Empty);
        }

        private void RefreshSpaceType()
        {
            if (_spaceTypeLabel == null) return;
            _spaceTypeLabel.text = _state.Node != null
                ? $"{ObjectNames.NicifyVariableName(_state.Node.spaceType.ToString()).ToUpperInvariant()}  ·  {_state.AxisCount}D"
                : string.Empty;
        }

        private void RefreshPlotGroup()
        {
            if (_plotGroup == null) return;
            _plotGroup.Clear();

            if (_state.Node == null || _state.AxisCount <= 2) return;

            _plotGroup.Add(new Label("Plot")
            {
                style = { fontSize = 10, color = HonamiEditorTheme.MutedText, marginRight = 4, flexShrink = 0 }
            });

            _plotGroup.Add(AxisPopup(_state.AxisX, value =>
            {
                _state.AxisX = value;
                _rebuild();
            }));

            _plotGroup.Add(new Label("/")
            {
                style = { fontSize = 10, color = HonamiEditorTheme.MutedText, marginLeft = 2, marginRight = 2, flexShrink = 0 }
            });

            _plotGroup.Add(AxisPopup(_state.AxisY, value =>
            {
                _state.AxisY = value;
                _rebuild();
            }));
        }

        private PopupField<int> AxisPopup(int current, Action<int> onChanged)
        {
            var axes = new List<int>();
            for (int i = 0; i < _state.AxisCount; i++) axes.Add(i);

            var popup = new PopupField<int>(axes, Mathf.Clamp(current, 0, axes.Count - 1), _state.AxisName, _state.AxisName)
            {
                style = { width = 110, flexShrink = 0 }
            };
            popup.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return popup;
        }

        private static string FirstBlendSpaceGuid(HonamiController controller)
        {
            if (controller?.states == null) return string.Empty;
            foreach (var candidate in controller.states)
            {
                if (candidate != null && candidate.node is HonamiBlendSpaceNode) return candidate.guid;
            }
            return string.Empty;
        }
    }
}
