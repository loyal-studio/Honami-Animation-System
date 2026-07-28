using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public sealed class HonamiTimelineWindow : EditorWindow
    {
        private const string TabsSessionKey = "Honami_Timeline_Tabs";

        private readonly List<TimelineState> _tabs = new();
        private readonly List<TimelineState> _sampleBuffer = new();
        private int _activeIndex;

        private HonamiNotificationPanel _notificationPanel;
        private TimelineToolbarView _toolbarView;
        private HonamiTabBar _tabBar;
        private TimelinePanelView _panel;
        private bool _refreshQueued;

        private TimelineState Active
        {
            get
            {
                if (_tabs.Count == 0) return null;
                _activeIndex = Mathf.Clamp(_activeIndex, 0, _tabs.Count - 1);
                return _tabs[_activeIndex];
            }
        }

        [MenuItem("Window/Honami/Honami Timeline")]
        public static void OpenWindow()
        {
            var w = GetWindow<HonamiTimelineWindow>();
            w.titleContent = HonamiEditorIcons.IconContent("HonamiTimelineWhite", "Honami Timeline");
            w.minSize = new Vector2(600, 300);
        }

        public static void InspectState(HonamiRuntimeController controller, string stateGuid)
        {
            var w = GetWindow<HonamiTimelineWindow>();
            if (w == null || controller == null) return;
            w.FocusOrOpenState(controller, stateGuid);
        }

        public static void InspectTimeline(HonamiTimeline timeline)
        {
            var w = GetWindow<HonamiTimelineWindow>();
            if (w == null || timeline == null) return;
            w.FocusOrOpenTimeline(timeline);
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceID);
            if (obj is HonamiTimeline timeline)
            {
                InspectTimeline(timeline);
                return true;
            }
            return false;
        }

        private void OnEnable()
        {
            titleContent = HonamiEditorIcons.IconContent("HonamiTimelineWhite", "Honami Timeline");

            RestoreTabs();

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed -= Repaint;
            Undo.undoRedoPerformed += Repaint;
            HonamiAppearanceSettings.Changed -= OnAppearanceChanged;
            HonamiAppearanceSettings.Changed += OnAppearanceChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            ConstructLayout();
        }

        private void OnDisable()
        {
            SaveTabs();

            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= Repaint;
            Selection.selectionChanged -= OnSelectionChanged;
            HonamiAppearanceSettings.Changed -= OnAppearanceChanged;
            _appearanceRebuild?.Cancel();

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i]?.ClearCache();

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private static TimelineState NewTabState()
        {
            var s = new TimelineState();
            s.LoadSettings();
            return s;
        }

        private static bool IsTabEmpty(TimelineState s) =>
            s.Controller == null && s.ActiveTimeline == null && s.ActiveClip == null;

        private void FocusOrOpenState(HonamiRuntimeController controller, string stateGuid)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t.Mode == TimelineMode.HonamiState && t.Controller == controller && t.SelectedState?.guid == stateGuid)
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

            target.Mode = TimelineMode.HonamiState;
            target.Controller = controller;
            target.SelectedState = controller.ActiveStates.FirstOrDefault(s => s != null && s.guid == stateGuid);
            target.SelectedLayerIndex = target.SelectedState != null ? target.SelectedState.layerIndex : -1;
            SelectTab(_tabs.IndexOf(target));
        }

        private void FocusOrOpenTimeline(HonamiTimeline timeline)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t.Mode == TimelineMode.HonamiTimeline && t.ActiveTimeline == timeline)
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

            target.Mode = TimelineMode.HonamiTimeline;
            target.ActiveTimeline = timeline;
            target.PlayheadTime = 0f;
            target.IsPlaying = false;
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
            _tabs[index].ClearCache();
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

        private static bool IsSameTabContent(TimelineState a, TimelineState b)
        {
            if (a.Mode != b.Mode) return false;
            return a.Mode switch
            {
                TimelineMode.HonamiTimeline => a.ActiveTimeline != null && a.ActiveTimeline == b.ActiveTimeline,
                TimelineMode.HonamiClipEdit => a.ActiveClip != null && a.ActiveClip == b.ActiveClip,
                _ => a.Controller != null && a.Controller == b.Controller &&
                     (a.SelectedState?.guid ?? string.Empty) == (b.SelectedState?.guid ?? string.Empty)
            };
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
                    _tabs[j].ClearCache();
                    _tabs.RemoveAt(j);
                    removedAny = true;
                }
            }

            if (removedAny)
                _activeIndex = Mathf.Max(0, _tabs.IndexOf(active));
            return removedAny;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is HonamiTimeline timeline)
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].Mode == TimelineMode.HonamiTimeline && _tabs[i].ActiveTimeline == timeline)
                    {
                        if (i != _activeIndex) SelectTab(i);
                        Repaint();
                        RequestRefreshViews();
                        return;
                    }
                }

                var s = Active;
                if (s == null)
                {
                    FocusOrOpenTimeline(timeline);
                    return;
                }
                s.Mode = TimelineMode.HonamiTimeline;
                if (s.ActiveTimeline != timeline)
                {
                    s.ActiveTimeline = timeline;
                    s.PlayheadTime = 0f;
                    s.IsPlaying = false;
                    s.ClearCache();
                }
                Repaint();
                RequestRefreshViews();
            }
            else
            {
                Active?.ClearCache();
                RequestRefreshViews();
            }
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnEditorUpdate()
        {
            bool anyPlaying = false;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t != null && t.IsPlaying && !t.IsDraggingPlayhead)
                {
                    if (EditorApplication.timeSinceStartup - t.LastUpdateTime < 1.0 / 60.0) return;
                    anyPlaying = true;
                    break;
                }
            }

            var active = Active;
            bool activeWasPlaying = active != null && active.IsPlaying && !active.IsDraggingPlayhead;

            for (int i = 0; i < _tabs.Count; i++)
                TickState(_tabs[i]);

            _sampleBuffer.Clear();
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] != null && _tabs[i].PreviewEnabled)
                    _sampleBuffer.Add(_tabs[i]);
            }
            TimelinePreview.SampleMany(_sampleBuffer);

            if (!anyPlaying) return;

            if (activeWasPlaying && active.IsPlaying)
                _panel?.TickRefresh();
            else
                RequestRefreshViews();
        }

        private void TickState(TimelineState s)
        {
            if (s == null) return;
            if (!s.IsPlaying || s.IsDraggingPlayhead) return;
            float dt = (float)(EditorApplication.timeSinceStartup - s.LastUpdateTime);
            s.LastUpdateTime = EditorApplication.timeSinceStartup;

            float loopStart = 0f;
            float loopEnd = -1f;
            if (s.Mode == TimelineMode.HonamiTimeline && s.ActiveTimeline?.markers != null)
            {
                foreach (var marker in s.ActiveTimeline.markers)
                {
                    if (marker is HonamiTimelineLoopMarker lm &&
                        s.PlayheadTime >= lm.time - dt && s.PlayheadTime <= lm.endTime)
                    {
                        loopStart = lm.time;
                        loopEnd = lm.endTime;
                        break;
                    }
                }
            }

            float duration = s.GetDuration();
            bool loop = s.SelectedState?.loop ?? false;
            if (s.ForceLoop) loop = true;
            bool reversed = s.SelectedState?.isReversed ?? false;

            if (loopEnd > 0)
            {
                if (reversed) { s.PlayheadTime -= dt; if (s.PlayheadTime < loopStart) s.PlayheadTime += loopEnd - loopStart; }
                else { s.PlayheadTime += dt; if (s.PlayheadTime > loopEnd) s.PlayheadTime -= loopEnd - loopStart; }
            }
            else
            {
                if (reversed)
                {
                    s.PlayheadTime -= dt;
                    if (s.PlayheadTime < 0f)
                    {
                        if (loop && duration > 0) s.PlayheadTime += duration;
                        else { s.PlayheadTime = 0f; s.IsPlaying = false; }
                    }
                }
                else
                {
                    s.PlayheadTime += dt;
                    if (s.PlayheadTime > duration)
                    {
                        if (loop && duration > 0) s.PlayheadTime -= duration;
                        else { s.PlayheadTime = duration; s.IsPlaying = false; }
                    }
                }
            }

            if (s.IsPlaying && s == Active)
            {
                bool hasProps = s.ShowProperties &&
                             (s.Mode == TimelineMode.HonamiTimeline && s.ActiveTimeline != null
                             || s.Mode == TimelineMode.HonamiState && s.SelectedState != null);
                float viewW = position.width - s.TrackHeaderWidth - (hasProps ? s.PropsWidth : 0f);
                float phPos = s.PlayheadTime * s.TimeScale;

                if (phPos > s.ScrollPos.x + viewW - 50f)
                    s.ScrollPos.x = Mathf.Lerp(s.ScrollPos.x, phPos - viewW + 50f, dt * 10f);
                else if (phPos < s.ScrollPos.x)
                    s.ScrollPos.x = Mathf.Max(0f, phPos - 50f);
            }
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

        private void ConstructLayout()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = TimelineTheme.WindowBg;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            _tabBar = new HonamiTabBar(
                () => _tabs.Count, () => _activeIndex, SelectTab, CloseTab, AddTab,
                i => TabTitle(_tabs[i]), i => TabIcon(_tabs[i]),
                i => _tabs[i].PreviewEnabled, "Preview enabled — contributes to scene sampling")
            {
                ActiveTabColor = TimelineTheme.PanelBg
            };
            rootVisualElement.Add(_tabBar);

            var active = Active;
            if (active == null)
            {
                _toolbarView = null;
                _panel = null;
                rootVisualElement.Add(HonamiEditorLayout.EmptyState(
                    HonamiEditorIcons.TimelineWhite, "Honami Timeline", "No Tabs Open",
                    "Open a Honami Timeline asset or a state from the Graph window,\nor create a new tab with +."));
            }
            else
            {
                _toolbarView = new TimelineToolbarView(active, RequestRefreshViews);
                rootVisualElement.Add(_toolbarView);

                _panel = new TimelinePanelView(active, () => true, () => { }, RequestRefreshViews);
                rootVisualElement.Add(_panel);
            }

            _notificationPanel = new HonamiNotificationPanel();
            rootVisualElement.Add(_notificationPanel);
        }

        private void RefreshViews()
        {
            DeduplicateTabs();
            _toolbarView?.Refresh();
            _tabBar?.Refresh();
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

        [Serializable]
        private sealed class TabSessionData
        {
            public int mode;
            public string controllerId;
            public string stateGuid;
            public int layerIndex;
            public string timelineId;
            public string clipId;
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
                data.tabs.Add(new TabSessionData
                {
                    mode = (int)t.Mode,
                    controllerId = ToObjectId(t.Controller),
                    stateGuid = t.SelectedState?.guid ?? string.Empty,
                    layerIndex = t.SelectedLayerIndex,
                    timelineId = ToObjectId(t.ActiveTimeline),
                    clipId = ToObjectId(t.ActiveClip)
                });
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
                var s = NewTabState();
                s.Mode = (TimelineMode)td.mode;

                switch (s.Mode)
                {
                    case TimelineMode.HonamiState:
                        s.Controller = FromObjectId<HonamiRuntimeController>(td.controllerId);
                        if (s.Controller == null) continue;
                        s.SelectedState = s.Controller.ActiveStates?.FirstOrDefault(st => st != null && st.guid == td.stateGuid);
                        s.SelectedLayerIndex = td.layerIndex;
                        break;
                    case TimelineMode.HonamiTimeline:
                        s.ActiveTimeline = FromObjectId<HonamiTimeline>(td.timelineId);
                        if (s.ActiveTimeline == null) continue;
                        break;
                    case TimelineMode.HonamiClipEdit:
                        s.ActiveClip = FromObjectId<AnimationClip>(td.clipId);
                        if (s.ActiveClip == null) continue;
                        break;
                }
                _tabs.Add(s);
            }

            _activeIndex = Mathf.Clamp(data.activeIndex, 0, Mathf.Max(0, _tabs.Count - 1));
            DeduplicateTabs();
        }

        private static string TabTitle(TimelineState state)
        {
            return state.Mode switch
            {
                TimelineMode.HonamiTimeline => state.ActiveTimeline != null ? state.ActiveTimeline.name : "Timeline",
                TimelineMode.HonamiClipEdit => state.ActiveClip != null ? state.ActiveClip.name : "Clip",
                _ => state.SelectedState?.stateName
                     ?? (state.Controller != null ? state.Controller.name : "State")
            };
        }

        private static Texture TabIcon(TimelineState state)
        {
            return state.Mode switch
            {
                TimelineMode.HonamiTimeline => HonamiEditorIcons.TimelineWhite,
                TimelineMode.HonamiClipEdit => EditorGUIUtility.IconContent("AnimationClip Icon").image,
                _ => HonamiEditorIcons.Controller
            };
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
