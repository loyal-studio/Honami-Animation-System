using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendSpace
{
    /// <summary>
    /// Authoring window for <see cref="HonamiBlendSpaceNode"/>: plots the motions of a blend space of
    /// any dimension on a draggable field, with a properties panel and a live skeleton preview.
    /// Hosts multiple tabs, each inspecting one blend space state.
    /// </summary>
    public sealed class HonamiBlendSpaceWindow : EditorWindow
    {
        private const string TabsSessionKey = "Honami_BlendSpace_Tabs";

        private readonly List<BlendSpaceState> _tabs = new();
        private int _activeIndex;

        private HonamiTabBar _tabBar;
        private BlendSpaceToolbarView _toolbarView;
        private BlendSpacePanelView _panel;
        private bool _refreshQueued;

        private BlendSpaceState Active
        {
            get
            {
                if (_tabs.Count == 0) return null;
                _activeIndex = Mathf.Clamp(_activeIndex, 0, _tabs.Count - 1);
                return _tabs[_activeIndex];
            }
        }

        [MenuItem("Window/Honami/Honami Blend Space")]
        public static void OpenWindow()
        {
            var window = GetWindow<HonamiBlendSpaceWindow>();
            window.titleContent = HonamiEditorIcons.IconContent("HonamiBlendtreeWhite", "Honami Blend Space");
            window.minSize = new Vector2(760, 460);
        }

        public static void InspectState(HonamiController controller, string stateGuid)
        {
            if (controller == null) return;
            var window = GetWindow<HonamiBlendSpaceWindow>();
            window.FocusOrOpenState(controller, stateGuid);
        }

        private void OnEnable()
        {
            titleContent = HonamiEditorIcons.IconContent("HonamiBlendtreeWhite", "Honami Blend Space");

            RestoreTabs();

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged -= RequestRefreshViews;
            EditorApplication.projectChanged += RequestRefreshViews;

            ConstructLayout();
        }

        private void OnDisable()
        {
            SaveTabs();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= RequestRefreshViews;
        }

        private void OnFocus() => RequestRefreshViews();

        private void OnUndoRedo()
        {
            RequestRefreshViews();
            Repaint();
        }

        private static BlendSpaceState NewTabState()
        {
            var state = new BlendSpaceState();
            state.LoadSettings();
            return state;
        }

        private static bool IsTabEmpty(BlendSpaceState state) => state.Controller == null;

        private void FocusOrOpenState(HonamiController controller, string stateGuid)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Controller == controller && _tabs[i].StateGuid == stateGuid)
                {
                    SelectTab(i);
                    return;
                }
            }

            var target = Active != null && IsTabEmpty(Active) ? Active : null;
            if (target == null)
            {
                target = NewTabState();
                _tabs.Add(target);
            }

            target.Controller = controller;
            target.StateGuid = stateGuid ?? string.Empty;
            target.ResetForNewTarget();
            SelectTab(_tabs.IndexOf(target));
        }

        private void SelectTab(int index)
        {
            _activeIndex = Mathf.Clamp(index, 0, _tabs.Count - 1);
            ConstructLayout();
            RequestRefreshViews();
        }

        private void CloseTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            _tabs.RemoveAt(index);

            if (index < _activeIndex) _activeIndex--;
            _activeIndex = Mathf.Clamp(_activeIndex, 0, _tabs.Count - 1);

            ConstructLayout();
            RequestRefreshViews();
        }

        private void AddTab()
        {
            _tabs.Add(NewTabState());
            SelectTab(_tabs.Count - 1);
        }

        private string TabTitle(int index)
        {
            if (index < 0 || index >= _tabs.Count) return "Blend Space";
            var tab = _tabs[index];
            tab.Resolve();
            return tab.State != null ? tab.State.stateName : "Blend Space";
        }

        private void ConstructLayout()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = HonamiEditorTheme.WindowBg;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            HonamiGraphStyleSheets.Apply(rootVisualElement);

            var active = Active;
            active?.Resolve();

            _tabBar = new HonamiTabBar(
                () => _tabs.Count, () => _activeIndex, SelectTab, CloseTab, AddTab,
                TabTitle, _ => HonamiEditorIcons.BlendTreeWhite);
            rootVisualElement.Add(_tabBar);

            if (active == null)
            {
                _panel = null;
                _toolbarView = null;
                rootVisualElement.Add(HonamiEditorLayout.EmptyState(
                    HonamiEditorIcons.BlendTreeWhite, "Honami Blend Space", "No Tabs Open",
                    "Open a blend space state from the Graph window\nor create a new tab with +."));
                return;
            }

            _toolbarView = new BlendSpaceToolbarView(active, RequestRefreshViews);
            rootVisualElement.Add(_toolbarView);

            _panel = new BlendSpacePanelView(active, RequestRefreshViews);
            rootVisualElement.Add(_panel);
        }

        private void RequestRefreshViews()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;

            rootVisualElement.schedule.Execute(() =>
            {
                _refreshQueued = false;
                RefreshViews();
            }).ExecuteLater(1);
        }

        private void RefreshViews()
        {
            var active = Active;
            if (active == null)
            {
                ConstructLayout();
                return;
            }

            active.Resolve();
            _tabBar?.Refresh();
            _toolbarView?.Refresh();
            _panel?.Refresh();
        }

        [Serializable]
        private sealed class TabSessionData
        {
            public string controllerId;
            public string stateGuid;
            public int axisX;
            public int axisY;
            public List<float> sample = new();
        }

        [Serializable]
        private sealed class TabSessionList
        {
            public List<TabSessionData> tabs = new();
            public int activeIndex;
        }

        private void SaveTabs()
        {
            var data = new TabSessionList { activeIndex = _activeIndex };

            foreach (var tab in _tabs)
            {
                if (tab == null) continue;

                var entry = new TabSessionData
                {
                    controllerId = ToObjectId(tab.Controller),
                    stateGuid = tab.StateGuid ?? string.Empty,
                    axisX = tab.AxisX,
                    axisY = tab.AxisY
                };
                entry.sample.AddRange(tab.Sample);
                data.tabs.Add(entry);
            }

            SessionState.SetString(TabsSessionKey, JsonUtility.ToJson(data));
        }

        private void RestoreTabs()
        {
            _tabs.Clear();
            _activeIndex = 0;

            string json = SessionState.GetString(TabsSessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            TabSessionList data;
            try { data = JsonUtility.FromJson<TabSessionList>(json); }
            catch { return; }
            if (data?.tabs == null) return;

            foreach (var entry in data.tabs)
            {
                var controller = FromObjectId<HonamiController>(entry.controllerId);
                if (controller == null) continue;

                var tab = NewTabState();
                tab.Controller = controller;
                tab.StateGuid = entry.stateGuid ?? string.Empty;
                tab.AxisX = entry.axisX;
                tab.AxisY = entry.axisY;
                tab.Sample = entry.sample != null ? entry.sample.ToArray() : Array.Empty<float>();
                _tabs.Add(tab);
            }

            _activeIndex = Mathf.Clamp(data.activeIndex, 0, Mathf.Max(0, _tabs.Count - 1));
        }

        private static string ToObjectId(UnityEngine.Object target)
        {
            if (target == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static T FromObjectId<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            string path = AssetDatabase.GUIDToAssetPath(id);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
