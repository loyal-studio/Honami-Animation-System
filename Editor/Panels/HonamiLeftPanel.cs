using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiLeftPanel
    {
        public VisualElement Root { get; }

        private readonly HonamiGraphWindow _window;
        private VisualElement _layersContent;
        private VisualElement _paramsContent;
        private readonly List<(int layerIndex, VisualElement row, VisualElement props, Label nameLabel, Label arrow, bool isInherited)> _layerRows = new();
        private int _currentTab = 0;
        private VisualElement _tabBar;
        private readonly List<Button> _tabButtons = new();

        private ScrollView _mainScroll;
        private int _dragSourceLayer = -1;
        private int _dropGapIndex = -1;
        private bool _dragActive;
        private bool _dragVisual;
        private Vector2 _dragStartPos;
        private Vector2 _lastDragPos;
        private VisualElement _dragGhost;
        private VisualElement _dropLine;
        private VisualElement _dragSourceRow;
        private IVisualElementScheduledItem _autoScrollItem;
        private bool _suppressNextLayerClick;

        private static readonly Color InheritedLayerTint = new(0.5f, 0.65f, 0.9f, 0.55f);
        private static readonly Color InheritedLayerText = new(0.78f, 0.88f, 1f);
        private static readonly Color LockedLayerText = new(0.6f, 0.6f, 0.6f);
        private static readonly Color FloatParamColor = new(0.35f, 0.85f, 0.55f);
        private static readonly Color IntParamColor = new(0.40f, 0.60f, 1.00f);
        private static readonly Color BoolParamColor = new(0.90f, 0.75f, 0.20f);
        private static readonly Color TriggerParamColor = new(0.90f, 0.45f, 0.45f);
        private static readonly Color RandomParamColor = new(0.45f, 0.80f, 0.90f);

        private static readonly Dictionary<int, (HonamiAnimator animator, bool value)> ForcedBools = new();
        private static bool _updateHooked = false;

        private static void GlobalForceUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                ForcedBools.Clear();
                if (_updateHooked)
                {
                    EditorApplication.update -= GlobalForceUpdate;
                    _updateHooked = false;
                }
                return;
            }

            foreach (var kvp in ForcedBools)
            {
                if (kvp.Value.animator != null)
                {
                    kvp.Value.animator.Parameters.SetBool(kvp.Key, kvp.Value.value);
                }
            }
        }

        public HonamiLeftPanel(HonamiGraphWindow window)
        {
            _window = window;
            Root = BuildShell();
        }

        private VisualElement BuildShell()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;

            _tabBar = new VisualElement();
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f);
            _tabBar.style.height = 22;
            root.Add(_tabBar);

            MakeTabButton("Layers", 0, _tabBar);
            MakeTabButton("Parameters", 1, _tabBar);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            _mainScroll = scroll;

            var pad = new VisualElement();
            pad.Padding(6);
            scroll.Add(pad);

            _layersContent = new VisualElement();
            _paramsContent = new VisualElement();
            _paramsContent.style.display = DisplayStyle.None;

            pad.Add(_layersContent);
            pad.Add(_paramsContent);

            root.RegisterCallback<PointerDownEvent>(_ => _suppressNextLayerClick = false, TrickleDown.TrickleDown);

            return root;
        }

        private Button MakeTabButton(string label, int tabIdx, VisualElement parent)
        {
            var btn = new Button(() => SelectTab(tabIdx)) { text = label };
            btn.style.flexGrow = 1;
            btn.style.height = 22;
            btn.BorderWidth(0);
            btn.Radius(0);
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.name = $"tab-btn-{tabIdx}";
            parent.Add(btn);
            _tabButtons.Add(btn);
            UpdateTabButtonStyle(btn, tabIdx == _currentTab);
            return btn;
        }

        private void SelectTab(int idx)
        {
            _currentTab = idx;
            _layersContent.style.display = idx == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _paramsContent.style.display = idx == 1 ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _tabButtons.Count; i++)
                UpdateTabButtonStyle(_tabButtons[i], i == idx);
        }

        private static void UpdateTabButtonStyle(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active
                ? HonamiGraphStyles.Accent
                : new Color(0.18f, 0.18f, 0.19f);
            btn.style.color = active ? Color.white : new Color(0.8f, 0.8f, 0.8f);
        }

        public void Rebuild()
        {
            RebuildLayers();
            RebuildParams();
        }

        public void UpdateLayerHighlight()
        {
            for (int i = 0; i < _layerRows.Count; i++)
            {
                var (layerIndex, row, props, _, arrow, isInherited) = _layerRows[i];
                bool sel = _window.CurrentLayerIndex == layerIndex;
                row.style.backgroundColor = sel
                    ? HonamiGraphStyles.AccentDim
                    : HonamiGraphStyles.ListBoxBg;
                props.style.display = sel ? DisplayStyle.Flex : DisplayStyle.None;

                if (arrow != null)
                    arrow.text = sel ? HonamiEditorSymbols.Collapse : HonamiEditorSymbols.Expand;

                if (sel)
                {
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = HonamiGraphStyles.Accent;
                }
                else if (isInherited)
                {
                    row.style.borderLeftWidth = 2;
                    row.style.borderLeftColor = InheritedLayerTint;
                }
                else
                {
                    row.style.borderLeftWidth = 1;
                    row.style.borderLeftColor = HonamiGraphStyles.ListBoxBorder;
                }
            }
        }

    }
}
