#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.BlendTree;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Editor window for authoring 1D blend trees on a pannable node canvas.
    /// Hosts multiple tabs, each inspecting one blend tree state of a Honami controller.
    /// </summary>
    public sealed class HonamiBlendTreeWindow : EditorWindow
    {
        private const string TabsSessionKey = "Honami_BlendTree_Tabs";

        private readonly List<BlendTreeState> _tabs = new();
        private int _activeIndex;

        private HonamiNotificationPanel _notificationPanel;
        private HonamiTabBar _tabBar;
        private BlendTreeToolbarView _toolbarView;
        private BlendTreePanelView _panel;
        private bool _refreshQueued;

        private BlendTreeState Active
        {
            get
            {
                if (_tabs.Count == 0) return null;
                _activeIndex = Mathf.Clamp(_activeIndex, 0, _tabs.Count - 1);
                return _tabs[_activeIndex];
            }
        }

        [MenuItem("Window/Honami/Honami Blend Tree")]
        public static void OpenWindow()
        {
            var w = GetWindow<HonamiBlendTreeWindow>();
            w.titleContent = HonamiEditorIcons.IconContent("HonamiBlendtreeWhite", "Honami Blend Tree");
            w.minSize = new Vector2(700, 420);
        }

        public static void InspectState(HonamiController controller, string stateGuid)
        {
            if (controller == null) return;
            var w = GetWindow<HonamiBlendTreeWindow>();
            w.FocusOrOpenState(controller, stateGuid);
        }

        private void OnEnable()
        {
            titleContent = HonamiEditorIcons.IconContent("HonamiBlendtreeWhite", "Honami Blend Tree");

            DeleteLegacyPrefs();
            RestoreTabs();

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged -= RequestRefreshViews;
            EditorApplication.projectChanged += RequestRefreshViews;
            HonamiAppearanceSettings.Changed -= OnAppearanceChanged;
            HonamiAppearanceSettings.Changed += OnAppearanceChanged;

            ConstructLayout();
        }

        private void OnDisable()
        {
            SaveTabs();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= RequestRefreshViews;
            HonamiAppearanceSettings.Changed -= OnAppearanceChanged;
            _appearanceRebuild?.Cancel();
        }

        private HonamiAppearanceRebuildScheduler _appearanceRebuild;

        private void OnAppearanceChanged()
        {
            Repaint();
            _appearanceRebuild ??= new HonamiAppearanceRebuildScheduler(() =>
            {
                ConstructLayout();
                RequestRefreshViews();
                Repaint();
            });
            _appearanceRebuild.Request();
        }

        private void OnFocus() => RequestRefreshViews();

        private void OnUndoRedo()
        {
            RequestRefreshViews();
            Repaint();
        }

        private static BlendTreeState NewTabState()
        {
            var s = new BlendTreeState();
            s.LoadSettings();
            return s;
        }

        private static bool IsTabEmpty(BlendTreeState s) => s.Controller == null;

        private void FocusOrOpenState(HonamiController controller, string stateGuid)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t.Controller == controller && t.StateGuid == stateGuid)
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

        private static bool IsSameTabContent(BlendTreeState a, BlendTreeState b)
        {
            return a.Controller != null && a.Controller == b.Controller &&
                   (a.StateGuid ?? string.Empty) == (b.StateGuid ?? string.Empty);
        }

        private bool DeduplicateTabs()
        {
            if (_tabs.Count < 2) return false;
            var active = Active;
            bool removedAny = false;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var keeper = _tabs[i];
                if (keeper == null) continue;
                for (int j = _tabs.Count - 1; j > i; j--)
                {
                    var other = _tabs[j];
                    if (other == null || !IsSameTabContent(keeper, other)) continue;
                    if (other == active)
                    {
                        _tabs[i] = other;
                        _tabs[j] = keeper;
                        keeper = other;
                    }
                    _tabs.RemoveAt(j);
                    removedAny = true;
                }
            }

            if (removedAny)
                _activeIndex = Mathf.Max(0, _tabs.IndexOf(active));
            return removedAny;
        }

        private void ConstructLayout()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = BlendTreeTheme.WindowBg;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            var styleSheet = Resources.Load<StyleSheet>("HonamiGraphStyle");
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

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
                    HonamiEditorIcons.BlendTreeWhite, "Honami Blend Tree", "No Tabs Open",
                    "Open a blend tree state from the Graph window\nor create a new tab with +."));
            }
            else
            {
                _panel = new BlendTreePanelView(active, RequestRefreshViews);
                _toolbarView = new BlendTreeToolbarView(active, RequestRefreshViews, () => _panel);

                rootVisualElement.Add(_toolbarView);
                rootVisualElement.Add(_panel);
            }

            _notificationPanel = new HonamiNotificationPanel();
            rootVisualElement.Add(_notificationPanel);
        }

        private string TabTitle(int index)
        {
            var t = _tabs[index];
            t.Resolve();
            if (t.State != null) return t.State.stateName;
            if (t.Controller != null) return t.Controller.name;
            return "Blend Tree";
        }

        private void RefreshViews()
        {
            DeduplicateTabs();
            _tabBar?.Refresh();
            _toolbarView?.Refresh();
            _panel?.Refresh();
        }

        private void RequestRefreshViews()
        {
            if (_refreshQueued || rootVisualElement == null) return;
            _refreshQueued = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _refreshQueued = false;
                RefreshViews();
            }).ExecuteLater(1);
        }

        private static void DeleteLegacyPrefs()
        {
            EditorPrefs.DeleteKey("HonamiBlendTree_PosX");
            EditorPrefs.DeleteKey("HonamiBlendTree_PosY");
            EditorPrefs.DeleteKey("HonamiBlendTree_Scale");
            EditorPrefs.DeleteKey("HonamiBlendTree_Preview");
            EditorPrefs.DeleteKey("HonamiBlendTree_PaneWidth");
            EditorPrefs.DeleteKey("HonamiBlendTree_Controller");
            EditorPrefs.DeleteKey("HonamiBlendTree_State");
        }

        [Serializable]
        private sealed class TabSessionData
        {
            public string controllerId;
            public string stateGuid;
            public float previewValue;
            public float viewX;
            public float viewY;
            public float viewScale = 1f;
            public bool hasView;
            public List<int> nodePosKeys = new();
            public List<float> nodePosXValues = new();
            public List<float> nodePosYValues = new();
            public float outputNodeX;
            public float outputNodeY;
            public bool hasNodePositions;
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
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t == null) continue;

                var td = new TabSessionData
                {
                    controllerId = ToObjectId(t.Controller),
                    stateGuid = t.StateGuid ?? string.Empty,
                    previewValue = t.PreviewValue,
                    viewX = t.ViewPosition.x,
                    viewY = t.ViewPosition.y,
                    viewScale = t.ViewScale,
                    hasView = t.HasViewTransform,
                    outputNodeX = t.OutputNodePosition.x,
                    outputNodeY = t.OutputNodePosition.y,
                    hasNodePositions = t.HasNodePositions
                };

                foreach (var kvp in t.NodePositions)
                {
                    td.nodePosKeys.Add(kvp.Key);
                    td.nodePosXValues.Add(kvp.Value.x);
                    td.nodePosYValues.Add(kvp.Value.y);
                }

                data.tabs.Add(td);
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

            foreach (var td in data.tabs)
            {
                var controller = FromObjectId<HonamiController>(td.controllerId);
                if (controller == null) continue;

                var s = NewTabState();
                s.Controller = controller;
                s.StateGuid = td.stateGuid ?? string.Empty;
                s.PreviewValue = td.previewValue;
                s.ViewPosition = new Vector2(td.viewX, td.viewY);
                s.ViewScale = td.viewScale;
                s.HasViewTransform = td.hasView;
                s.OutputNodePosition = new Vector2(td.outputNodeX, td.outputNodeY);
                s.HasNodePositions = td.hasNodePositions;

                if (td.nodePosKeys != null)
                {
                    int keyCount = td.nodePosKeys.Count;
                    for (int ni = 0; ni < keyCount; ni++)
                    {
                        if (ni < td.nodePosXValues.Count && ni < td.nodePosYValues.Count)
                            s.NodePositions[td.nodePosKeys[ni]] = new Vector2(td.nodePosXValues[ni], td.nodePosYValues[ni]);
                    }
                }

                _tabs.Add(s);
            }

            _activeIndex = Mathf.Clamp(data.activeIndex, 0, Mathf.Max(0, _tabs.Count - 1));
            DeduplicateTabs();
        }

        private static string ToObjectId(UnityEngine.Object obj)
        {
            return obj == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
        }

        private static T FromObjectId<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out var gid)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as T;
        }
    }
}
#endif
