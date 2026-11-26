using System.Collections;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        // A link group is an editing aid: members keep their own offsets and move together when any
        // one of them is dragged. Items and markers share the same group space, so a clip can be
        // linked to the LOOP region or the WAIT marker that belongs to it and they move as one unit.
        private static readonly Color[] LinkPalette =
        {
            new(0.36f, 0.72f, 0.96f),
            new(0.98f, 0.70f, 0.30f),
            new(0.62f, 0.86f, 0.44f),
            new(0.94f, 0.52f, 0.66f),
            new(0.72f, 0.62f, 0.98f),
            new(0.34f, 0.86f, 0.80f),
            new(0.96f, 0.58f, 0.38f),
            new(0.86f, 0.86f, 0.44f)
        };

        private static readonly Color LinkStripShade = new(0f, 0f, 0f, 0.55f);

        private static string NewLinkGroup() => System.Guid.NewGuid().ToString("N");

        internal static Color LinkColor(string group)
        {
            if (string.IsNullOrEmpty(group)) return Color.white;

            // FNV-1a keeps a group's colour stable across sessions.
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < group.Length; i++)
                {
                    hash ^= group[i];
                    hash *= 16777619u;
                }
                return LinkPalette[hash % (uint)LinkPalette.Length];
            }
        }

        #region Group lookup

        private List<HonamiTimelineMarker> EditableMarkers()
        {
            var markers = new List<HonamiTimelineMarker>();
            if (_state.Mode != TimelineMode.HonamiTimeline) return markers;
            foreach (var marker in _state.SelectedMarkers)
            {
                if (marker != null) markers.Add(marker);
            }
            return markers;
        }

        private static HashSet<string> CollectGroups(IEnumerable targets)
        {
            var groups = new HashSet<string>();
            foreach (var target in targets)
            {
                switch (target)
                {
                    case HonamiTimelineItem item when item.IsLinked:
                        groups.Add(item.LinkGroup);
                        break;
                    case HonamiTimelineMarker marker when marker.IsLinked:
                        groups.Add(marker.LinkGroup);
                        break;
                }
            }
            return groups;
        }

        // Adds every linked item that shares a group with the seeds but is not already present.
        private void AddLinkedItems(IList targets, HashSet<string> groups)
        {
            if (groups.Count == 0) return;
            var modules = _state.ActiveModules;
            if (modules == null) return;

            foreach (var module in modules)
            {
                if (module == null || _state.IsModuleLocked(module)) continue;
                int count = module.ItemCount;
                for (int i = 0; i < count; i++)
                {
                    var item = module.GetItem(i);
                    if (item == null || !item.IsLinked) continue;
                    if (!groups.Contains(item.LinkGroup)) continue;
                    if (!targets.Contains(item)) targets.Add(item);
                }
            }
        }

        private void AddLinkedMarkers(List<HonamiTimelineMarker> targets, HashSet<string> groups)
        {
            if (groups.Count == 0) return;
            var timeline = _state.ActiveTimeline;
            if (timeline == null) return;

            foreach (var marker in timeline.Markers)
            {
                if (marker == null || !marker.IsLinked) continue;
                if (!groups.Contains(marker.LinkGroup)) continue;
                if (!targets.Contains(marker)) targets.Add(marker);
            }
        }

        // Grows the two seed lists to the full link group, in both directions (clips and markers).
        private void ExpandLinkGroup(IList itemTargets, List<HonamiTimelineMarker> markerTargets)
        {
            var groups = CollectGroups(itemTargets);
            foreach (var group in CollectGroups(markerTargets)) groups.Add(group);
            AddLinkedItems(itemTargets, groups);
            AddLinkedMarkers(markerTargets, groups);
        }

        private int CountLinkGroupMembers(string group)
        {
            if (string.IsNullOrEmpty(group)) return 0;

            int total = 0;
            var modules = _state.ActiveModules;
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module == null) continue;
                    int count = module.ItemCount;
                    for (int i = 0; i < count; i++)
                    {
                        var item = module.GetItem(i);
                        if (item != null && item.LinkGroup == group) total++;
                    }
                }
            }

            var timeline = _state.ActiveTimeline;
            if (timeline != null)
            {
                foreach (var marker in timeline.Markers)
                {
                    if (marker != null && marker.LinkGroup == group) total++;
                }
            }
            return total;
        }

        private bool HasLinkedSelection()
        {
            foreach (var item in _state.SelectedTimelineItems)
            {
                if (item != null && item.IsLinked) return true;
            }
            foreach (var marker in _state.SelectedMarkers)
            {
                if (marker != null && marker.IsLinked) return true;
            }
            return false;
        }

        #endregion

        #region Commands

        private void LinkSelection(bool sequence)
        {
            var items = EditableSelection();
            var markers = EditableMarkers();
            if (items.Count + markers.Count < 2) return;

            // Sequence only the things the user actually picked; group members that get pulled in
            // afterwards keep their own offsets.
            var sequenceItems = new List<HonamiTimelineItem>(items);
            var sequenceMarkers = new List<HonamiTimelineMarker>(markers);

            // Linking never pulls a member out of its old group: the selection is grown to the full
            // groups it touches and they all merge, reusing an existing id so colours stay stable.
            string group = FirstLinkGroup(items, markers);
            ExpandLinkGroup(items, markers);
            if (string.IsNullOrEmpty(group)) group = NewLinkGroup();

            var container = _state.ActiveContainer;
            var timeline = _state.ActiveTimeline;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            if (items.Count > 0 && container != null) HonamiEditorController.RecordUndo(container, "Link Selection");
            if (markers.Count > 0 && timeline != null) Undo.RecordObject(timeline, "Link Selection");

            // Sequencing only makes sense within one kind; a clip and the loop region that sits over
            // it should keep their relative offsets, so mixed selections are linked without re-laying out.
            if (sequence)
            {
                if (sequenceItems.Count >= 2) SequenceItems(sequenceItems);
                if (sequenceMarkers.Count >= 2) SequenceMarkers(sequenceMarkers);
            }

            foreach (var item in items)
            {
                if (item != null) item.SetLinkGroup(group);
            }
            foreach (var marker in markers)
            {
                if (marker != null) marker.SetLinkGroup(group);
            }

            if (items.Count > 0 && container != null) HonamiEditorController.MarkModulesChanged(container);
            if (markers.Count > 0 && timeline != null) EditorUtility.SetDirty(timeline);
            Undo.CollapseUndoOperations(undoGroup);
            _rebuild();
        }

        private static string FirstLinkGroup(List<HonamiTimelineItem> items, List<HonamiTimelineMarker> markers)
        {
            foreach (var item in items)
            {
                if (item != null && item.IsLinked) return item.LinkGroup;
            }
            foreach (var marker in markers)
            {
                if (marker != null && marker.IsLinked) return marker.LinkGroup;
            }
            return null;
        }

        private void UnlinkSelection()
        {
            var items = new List<HonamiTimelineItem>();
            foreach (var item in EditableSelection())
            {
                if (item != null && item.IsLinked) items.Add(item);
            }

            var markers = new List<HonamiTimelineMarker>();
            foreach (var marker in EditableMarkers())
            {
                if (marker != null && marker.IsLinked) markers.Add(marker);
            }
            if (items.Count == 0 && markers.Count == 0) return;

            var container = _state.ActiveContainer;
            var timeline = _state.ActiveTimeline;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            if (items.Count > 0 && container != null) HonamiEditorController.RecordUndo(container, "Unlink Selection");
            if (markers.Count > 0 && timeline != null) Undo.RecordObject(timeline, "Unlink Selection");

            foreach (var item in items) item.ClearLinkGroup();
            foreach (var marker in markers) marker.ClearLinkGroup();

            if (items.Count > 0 && container != null) HonamiEditorController.MarkModulesChanged(container);
            if (markers.Count > 0 && timeline != null) EditorUtility.SetDirty(timeline);
            Undo.CollapseUndoOperations(undoGroup);
            _rebuild();
        }

        private void SelectLinkedGroup()
        {
            var groups = CollectGroups(_state.SelectedTimelineItems);
            foreach (var group in CollectGroups(_state.SelectedMarkers)) groups.Add(group);
            if (groups.Count == 0) return;

            _state.ClearSelection();

            var modules = _state.ActiveModules;
            if (modules != null)
            {
                foreach (var module in modules)
                {
                    if (module == null) continue;
                    int count = module.ItemCount;
                    for (int i = 0; i < count; i++)
                    {
                        var item = module.GetItem(i);
                        if (item != null && item.IsLinked && groups.Contains(item.LinkGroup))
                            _state.SelectedTimelineItems.Add(item);
                    }
                }
            }

            var timeline = _state.ActiveTimeline;
            if (timeline != null)
            {
                foreach (var marker in timeline.Markers)
                {
                    if (marker != null && marker.IsLinked && groups.Contains(marker.LinkGroup))
                        _state.SelectedMarkers.Add(marker);
                }
            }

            if (_state.SelectedTimelineItems.Count == 1)
                _state.SelectedModule = _state.FindModuleOf(_state.SelectedTimelineItems[0]);

            RefreshSelectionVisuals();
            UpdatePropertiesPanel(PropertiesVisible());
            _rebuild();
        }

        // Lays the given items end to end in their current order, leaving the configured gap between them.
        private void SequenceItems(List<HonamiTimelineItem> items)
        {
            var ordered = new List<HonamiTimelineItem>(items);
            ordered.Sort((a, b) => (_state.GetItemTimeOffset(a) + a.time).CompareTo(_state.GetItemTimeOffset(b) + b.time));

            float earliest = float.MaxValue;
            float maxOffset = 0f;
            foreach (var item in ordered)
            {
                float offset = _state.GetItemTimeOffset(item);
                if (offset > maxOffset) maxOffset = offset;
                float start = offset + item.time;
                if (start < earliest) earliest = start;
            }
            if (earliest == float.MaxValue) return;

            float gap = Mathf.Max(0f, HonamiTimelineSettings.LinkGap);
            float instant = Mathf.Max(_state.FrameDuration, HonamiTimelineItem.MinDuration);
            float cursor = Mathf.Max(earliest, maxOffset);
            foreach (var item in ordered)
            {
                float offset = _state.GetItemTimeOffset(item);
                item.time = Mathf.Max(0f, cursor - offset);
                cursor += Mathf.Max(item.Duration, instant) + gap;
            }
        }

        // Lays the given markers out in their current order, leaving the configured gap between them.
        private void SequenceMarkers(List<HonamiTimelineMarker> markers)
        {
            var ordered = new List<HonamiTimelineMarker>(markers);
            ordered.Sort((a, b) => a.time.CompareTo(b.time));
            if (ordered.Count == 0) return;

            float gap = Mathf.Max(0f, HonamiTimelineSettings.LinkGap);
            float instant = Mathf.Max(_state.FrameDuration, HonamiTimelineItem.MinDuration);
            float cursor = Mathf.Max(0f, ordered[0].time);
            foreach (var marker in ordered)
            {
                float length = marker is HonamiTimelineLoopMarker loop
                    ? Mathf.Max(loop.endTime - loop.time, instant)
                    : instant;

                marker.time = Mathf.Max(0f, cursor);
                if (marker is HonamiTimelineLoopMarker moved) moved.endTime = marker.time + length;
                cursor = marker.time + length + gap;
            }
        }

        private void SetLinkGap(float gap)
        {
            HonamiTimelineSettings.LinkGap = gap;
            _rebuild();
        }

        #endregion

        #region Badges

        private void DecorateLink(VisualElement element, HonamiTimelineItem item)
        {
            if (!_state.ShowLinks || element == null || item == null || !item.IsLinked) return;
            DecorateLinkedElement(element, item.LinkGroup, "item");
        }

        private void DecorateMarkerLink(VisualElement element, HonamiTimelineMarker marker)
        {
            if (!_state.ShowLinks || element == null || marker == null || !marker.IsLinked) return;
            DecorateLinkedElement(element, marker.LinkGroup, "marker");
        }

        private void DecorateLinkedElement(VisualElement element, string group, string kind)
        {
            Color color = LinkColor(group);
            int members = CountLinkGroupMembers(group);
            string hint = members > 1
                ? $"\n[Linked] 1 of {members} members. Drag any member to move them together."
                : $"\n[Linked] this {kind} is alone in its group.";

            if (!string.IsNullOrEmpty(element.tooltip)) element.tooltip += hint;
            else element.tooltip = hint.TrimStart('\n');

            var strip = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    bottom = 0,
                    width = 3,
                    backgroundColor = color
                }
            };
            element.Add(strip);

            var badge = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    right = 4,
                    top = 4,
                    width = 8,
                    height = 8,
                    backgroundColor = color
                }
            };
            badge.Radius(4);
            badge.Border(1, LinkStripShade);
            element.Add(badge);
        }

        #endregion

        internal void OpenLinksMenu()
        {
            var menu = new GenericMenu();
            var items = EditableSelection();
            var markers = EditableMarkers();
            int count = items.Count + markers.Count;
            bool canLink = count >= 2;
            bool hasLinked = HasLinkedSelection();

            AddMenuItem(menu, "Link Selected  (Ctrl+L)", canLink, () => LinkSelection(false));
            AddMenuItem(menu, "Link And Sequence Selected  (Ctrl+Shift+L)", canLink, () => LinkSelection(true));
            AddMenuItem(menu, "Unlink Selected", hasLinked, UnlinkSelection);
            menu.AddSeparator("");
            AddMenuItem(menu, "Select Linked Group", hasLinked, SelectLinkedGroup);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Show Link Badges"), _state.ShowLinks, () =>
            {
                _state.ShowLinks = !_state.ShowLinks;
                _state.SaveSettings();
                _rebuild();
            });

            float gap = HonamiTimelineSettings.LinkGap;
            menu.AddItem(new GUIContent("Sequence Gap/None"), gap <= 0.0001f, () => SetLinkGap(0f));
            menu.AddItem(new GUIContent("Sequence Gap/0.1 s"), Mathf.Abs(gap - 0.1f) < 0.0001f, () => SetLinkGap(0.1f));
            menu.AddItem(new GUIContent("Sequence Gap/0.25 s"), Mathf.Abs(gap - 0.25f) < 0.0001f, () => SetLinkGap(0.25f));
            menu.AddItem(new GUIContent("Sequence Gap/0.5 s"), Mathf.Abs(gap - 0.5f) < 0.0001f, () => SetLinkGap(0.5f));

            menu.ShowAsContext();
        }
    }
}
