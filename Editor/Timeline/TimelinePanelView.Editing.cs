using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;
using Object = UnityEngine.Object;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private const float DragThreshold = 3f;

        private static readonly List<HonamiTimelineItem> ClipboardItems = new();
        private static readonly List<HonamiTimelineModule> ClipboardSources = new();

        private sealed class ItemDrag
        {
            public readonly List<object> Targets = new();
            public readonly List<float> Starts = new();
            public readonly List<float> Ends = new();
            public readonly List<float> SnapPoints = new();
            // Markers pulled along by the same link groups as the dragged items.
            public readonly List<HonamiTimelineMarker> MarkerTargets = new();
            public readonly List<float> MarkerStarts = new();
            public readonly List<float> MarkerEnds = new();
            public object Primary;
            public VisualElement Element;
            public float PointerStartTime;
            public float PointerStartY;
            public float Delta;
            public bool Moved;
            public bool Additive;
            public int UndoGroup;
            public HonamiTimelineModule SourceModule;
            public HonamiTimelineModule TargetModule;
        }

        private ItemDrag _itemDrag;
        private VisualElement _dropGhost;
        private VisualElement _bladeLine;
        private VisualElement _boxSelection;
        private VisualElement _rowHighlight;
        private VisualElement _helpOverlay;

        private HonamiTimelineModule _gapClickModule;
        private float _gapClickStart;
        private float _gapClickEnd;

        #region Canvas input

        private void RegisterCanvasEvents(VisualElement canvas)
        {
            canvas.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (IsStateEmpty()) return;
                var local = canvas.WorldToLocal(evt.position);

                if (evt.button == 2)
                {
                    _panning = true;
                    _panStartMouse = evt.position;
                    _panStartScroll = _state.ScrollPos;
                    evt.StopPropagation();
                    return;
                }

                if (evt.button == 1)
                {
                    _focus();
                    if (_state.Mode == TimelineMode.HonamiClipEdit)
                        ShowClipEditContextMenu(local.y);
                    else
                        ShowAddMenu(Mathf.Max(0f, TimeAtCanvasX(local.x)), local.y);
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0) return;
                _focus();
                Focus();

                bool additive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
                if (!additive)
                {
                    _state.ClearSelection();
                    RefreshSelectionVisuals();
                    UpdatePropertiesPanel(PropertiesVisible());
                }

                _state.IsBoxSelecting = true;
                _state.BoxSelectStartPos = local;
                _state.BoxSelectRect = new Rect(local.x, local.y, 0, 0);
                BeginGapClick(local);
                evt.StopPropagation();
            });

            canvas.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_state.Tool != TimelineEditTool.Blade || _itemDrag != null) return;
                UpdateBladeLine(TimeAtPointer(evt), evt.shiftKey);
            });
            canvas.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_bladeLine != null) _bladeLine.style.display = DisplayStyle.None;
            });
        }

        private void UpdateBladeLine(float time, bool shiftHeld)
        {
            float snapped = SnapTime(Mathf.Max(0f, time), null, ShouldSnap(shiftHeld));
            _state.SnapLineTime = -1f;
            if (_bladeLine == null || _bladeLine.parent != _canvas)
            {
                _bladeLine = RectElement(0f, 0f, 1f, _canvasHeight, new Color(1f, 0.35f, 0.35f, 0.9f));
                IgnorePicking(_bladeLine);
                _canvas.Add(_bladeLine);
            }
            _bladeLine.style.display = DisplayStyle.Flex;
            _bladeLine.style.left = snapped * _state.TimeScale;
            _bladeLine.BringToFront();
        }

        private void UpdateBoxSelection(bool append)
        {
            Rect box = _state.BoxSelectRect;
            _state.ClearSelectedGap();
            if (!append)
            {
                _state.SelectedTimelineItems.Clear();
                _state.SelectedSeqClips.Clear();
                _state.SelectedKeyframes.Clear();
            }

            foreach (var kv in _state.TimelineItemRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedTimelineItems.Contains(kv.Key)) _state.SelectedTimelineItems.Add(kv.Key);

            foreach (var kv in _state.SeqClipRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedSeqClips.Contains(kv.Key)) _state.SelectedSeqClips.Add(kv.Key);

            foreach (var kv in _state.KeyframeRects)
                if (box.Overlaps(kv.Value) && !_state.SelectedKeyframes.Contains(kv.Key)) _state.SelectedKeyframes.Add(kv.Key);

            if (_state.SelectedTimelineItems.Count == 1)
                _state.SelectedModule = _state.FindModuleOf(_state.SelectedTimelineItems[0]);

            if (_state.Mode == TimelineMode.HonamiClipEdit)
                RefreshKeyframeSelectionVisual();
        }

        private void UpdateBoxSelectionVisual()
        {
            if (!_state.IsBoxSelecting)
            {
                _boxSelection?.RemoveFromHierarchy();
                _boxSelection = null;
                return;
            }

            if (_boxSelection == null || _boxSelection.parent != _canvas)
            {
                var borderColor = new Color(0.4f, 0.7f, 1f, 0.8f);
                _boxSelection = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, backgroundColor = new Color(0.18f, 0.45f, 0.85f, 0.22f) }
                };
                _boxSelection.Border(1, borderColor);
                _canvas.Add(_boxSelection);
            }

            var rect = _state.BoxSelectRect;
            _boxSelection.style.left = rect.x;
            _boxSelection.style.top = rect.y;
            _boxSelection.style.width = rect.width;
            _boxSelection.style.height = rect.height;
            _boxSelection.BringToFront();
        }

        // Clicking the empty stretch of a track selects that gap, the way Premiere lets you pick a gap
        // and ripple delete it. Movement past the drag threshold turns the gesture into a box selection.
        private void BeginGapClick(Vector2 local)
        {
            _gapClickModule = null;
            if (_state.Tool != TimelineEditTool.Select || _state.Mode == TimelineMode.HonamiClipEdit) return;

            int rowIndex = RowIndexAtCanvasY(local.y);
            if (rowIndex < 0) return;
            var module = _cachedRows[rowIndex].Module;
            if (module == null || _state.IsModuleLocked(module)) return;

            if (!TryFindGap(module, TimeAtCanvasX(local.x), out float start, out float end)) return;
            _gapClickModule = module;
            _gapClickStart = start;
            _gapClickEnd = end;
        }

        private bool TryFindGap(HonamiTimelineModule module, float time, out float start, out float end)
        {
            start = 0f;
            end = 0f;
            if (module == null) return false;

            float offset = _state.GetModuleTimeOffset(module);
            float local = time - offset;
            if (local < 0f) return false;

            const float epsilon = 0.0001f;
            float previousEnd = 0f;
            float nextStart = float.MaxValue;
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item == null) continue;

                float itemStart = item.time;
                float itemEnd = item.IsInstant ? item.time : item.EndTime;
                if (itemEnd <= local + epsilon)
                {
                    if (itemEnd > previousEnd) previousEnd = itemEnd;
                }
                else if (itemStart >= local - epsilon)
                {
                    if (itemStart < nextStart) nextStart = itemStart;
                }
                else
                {
                    return false;
                }
            }

            // A gap only counts when an item follows it: without one there is nothing to slide left.
            if (nextStart == float.MaxValue || nextStart - previousEnd <= epsilon) return false;
            start = offset + previousEnd;
            end = offset + nextStart;
            return true;
        }

        private void SelectGap(HonamiTimelineModule module, float start, float end)
        {
            _state.ClearSelection();
            _state.SelectedGapModule = module;
            _state.SelectedGapStart = start;
            _state.SelectedGapEnd = end;
            UpdatePropertiesPanel(PropertiesVisible());
        }

        private bool RippleDeleteSelectedGap()
        {
            var module = _state.SelectedGapModule;
            if (module == null || _state.ActiveContainer == null || _state.IsModuleLocked(module))
            {
                _state.ClearSelectedGap();
                return false;
            }

            float offset = _state.GetModuleTimeOffset(module);
            float start = _state.SelectedGapStart - offset;
            float end = _state.SelectedGapEnd - offset;
            ModifyModules(container => HonamiEditorController.RippleDeleteGap(container, module, start, end));
            _state.ClearSelectedGap();
            return true;
        }

        #endregion

        #region Item interaction

        private void RegisterItemInteraction(VisualElement element, object target)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 2) return;
                _focus();
                Focus();

                if (evt.button == 1)
                {
                    if (!IsTargetSelected(target)) SelectTarget(target, false);
                    RefreshSelectionVisuals();
                    if (IsTargetSelected(target) && element.parent == _canvas)
                    {
                        element.BringToFront();
                        BringOverlaysToFront();
                    }
                    ShowItemMenu(target, TimeAtPointer(evt));
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0) return;

                if (evt.clickCount == 2)
                {
                    OnItemDoubleClicked(target, TimeAtPointer(evt));
                    evt.StopPropagation();
                    return;
                }

                if (_state.Tool == TimelineEditTool.Blade && target is HonamiTimelineItem bladeItem)
                {
                    float cut = SnapTime(TimeAtPointer(evt), null, ShouldSnap(evt.shiftKey));
                    _state.SnapLineTime = -1f;
                    SplitItemsAt(new List<HonamiTimelineItem> { bladeItem }, cut);
                    evt.StopPropagation();
                    return;
                }

                bool additive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
                if (additive) ToggleTarget(target);
                else if (!IsTargetSelected(target)) SelectTarget(target, false);
                RefreshSelectionVisuals();
                if (IsTargetSelected(target) && element.parent == _canvas)
                {
                    element.BringToFront();
                    BringOverlaysToFront();
                }
                UpdatePropertiesPanel(PropertiesVisible());

                if (IsTargetSelected(target) && !IsTargetLocked(target))
                    BeginItemDrag(target, element, evt, additive);
                else
                    _rebuild();
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_itemDrag == null || _itemDrag.Element != element || !element.HasPointerCapture(evt.pointerId)) return;
                UpdateItemDrag(evt);
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_itemDrag == null || _itemDrag.Element != element) return;
                EndItemDrag(evt.altKey);
                if (element.HasPointerCapture(evt.pointerId)) element.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            element.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_itemDrag != null && _itemDrag.Element == element)
                    EndItemDrag(false);
            });
        }

        private bool IsTargetLocked(object target) =>
            target is HonamiTimelineItem item && _state.IsModuleLocked(_state.FindModuleOf(item));

        private void SelectTarget(object target, bool additive)
        {
            _state.ClearSelectedGap();
            switch (target)
            {
                case HonamiSequencedAnimationClip clip:
                    SelectOnly(_state.SelectedSeqClips, clip, additive);
                    // Additive clicks keep a mixed selection, which is how a clip and a marker are
                    // linked together; a plain click resets the other lanes.
                    if (!additive)
                    {
                        _state.SelectedTimelineItems.Clear();
                        _state.SelectedMarkers.Clear();
                    }
                    break;
                case HonamiTimelineItem item:
                    SelectOnly(_state.SelectedTimelineItems, item, additive);
                    if (!additive)
                    {
                        _state.SelectedSeqClips.Clear();
                        _state.SelectedMarkers.Clear();
                    }
                    _state.SelectedModule = _state.FindModuleOf(item);
                    break;
            }
        }

        private void ToggleTarget(object target) => SelectTarget(target, true);

        private void OnItemDoubleClicked(object target, float time)
        {
            if (target is HonamiTimelineItem item)
            {
                var owner = _state.FindModuleOf(item);
                if (owner == null) return;
                var context = CreateEditContext(owner);
                HonamiTimelineModuleEditorRegistry.GetEditor(owner).OnItemDoubleClicked(context, item, time - context.TimeOffset);
                return;
            }

            if (target is HonamiSequencedAnimationClip clip && clip.clip != null)
                OpenClipEdit(clip.clip);
        }

        private void BeginItemDrag(object target, VisualElement element, PointerDownEvent evt, bool additive)
        {
            _state.PreModifyState();

            var drag = new ItemDrag
            {
                Primary = target,
                Element = element,
                PointerStartTime = TimeAtPointer(evt),
                PointerStartY = _canvas.WorldToLocal(evt.position).y,
                Additive = additive
            };

            if (target is HonamiTimelineItem primaryItem)
            {
                drag.SourceModule = _state.FindModuleOf(primaryItem);
                foreach (var item in _state.SelectedTimelineItems)
                {
                    if (_state.IsModuleLocked(_state.FindModuleOf(item))) continue;
                    drag.Targets.Add(item);
                    if (_state.FindModuleOf(item) != drag.SourceModule) drag.SourceModule = null;
                }
            }
            else
            {
                foreach (var clip in _state.SelectedSeqClips)
                    drag.Targets.Add(clip);
            }

            if (!drag.Targets.Contains(target)) drag.Targets.Add(target);
            if (target is HonamiTimelineItem) ExpandLinkGroup(drag.Targets, drag.MarkerTargets);
            foreach (var dragged in drag.Targets)
            {
                drag.Starts.Add(GetStartTime(dragged));
                drag.Ends.Add(GetEndTime(dragged));
            }
            foreach (var marker in drag.MarkerTargets)
            {
                drag.MarkerStarts.Add(marker.time);
                drag.MarkerEnds.Add(marker is HonamiTimelineLoopMarker loop ? loop.endTime : marker.time);
            }

            CollectSnapPoints(drag.SnapPoints, target);
            drag.SnapPoints.Add(_state.PlayheadTime);

            Undo.IncrementCurrentGroup();
            drag.UndoGroup = Undo.GetCurrentGroup();
            _itemDrag = drag;
            element.CapturePointer(evt.pointerId);
        }

        private void UpdateItemDrag(PointerMoveEvent evt)
        {
            var drag = _itemDrag;
            float pointerTime = TimeAtPointer(evt);
            float localY = _canvas.WorldToLocal(evt.position).y;
            float raw = pointerTime - drag.PointerStartTime;
            if (!drag.Moved && Mathf.Abs(raw * _state.TimeScale) < DragThreshold && Mathf.Abs(localY - drag.PointerStartY) < DragThreshold) return;
            drag.Moved = true;

            float delta = ShouldSnap(evt.shiftKey) ? SnapDragDelta(drag, raw) : raw;
            if (!ShouldSnap(evt.shiftKey)) _state.SnapLineTime = -1f;

            float earliest = float.MaxValue;
            foreach (float start in drag.Starts) earliest = Mathf.Min(earliest, start);
            foreach (float start in drag.MarkerStarts) earliest = Mathf.Min(earliest, start);
            delta = Mathf.Max(delta, -earliest);
            drag.Delta = delta;

            for (int i = 0; i < drag.Targets.Count; i++)
            {
                SetStartTime(drag.Targets[i], drag.Starts[i] + delta);
                if (_elements.TryGetValue(drag.Targets[i], out var element))
                {
                    float left = (drag.Starts[i] + delta) * _state.TimeScale;
                    element.style.left = drag.Targets[i] is HonamiTimelineItem { IsInstant: true } ? left - 8f : left;
                }
            }

            for (int i = 0; i < drag.MarkerTargets.Count; i++)
            {
                var marker = drag.MarkerTargets[i];
                float start = Mathf.Max(0f, drag.MarkerStarts[i] + delta);
                marker.time = start;
                if (marker is HonamiTimelineLoopMarker loop)
                    loop.endTime = start + (drag.MarkerEnds[i] - drag.MarkerStarts[i]);
                if (_markerElements.TryGetValue(marker, out var markerElement))
                    RepositionRulerMarker(markerElement, marker, null);
            }
            if (drag.MarkerTargets.Count > 0) EditorUtility.SetDirty(_state.ActiveTimeline);

            UpdateDragTargetModule(drag, localY);
            UpdateSnapLineVisual();
            UpdatePlayheadVisual();
        }

        private float SnapDragDelta(ItemDrag drag, float raw)
        {
            float threshold = SnapThreshold;
            float bestAdjust = 0f;
            float bestDistance = float.MaxValue;
            float bestPoint = -1f;

            for (int i = 0; i < drag.Targets.Count; i++)
            {
                float start = drag.Starts[i] + raw;
                float end = drag.Ends[i] + raw;
                foreach (float point in drag.SnapPoints)
                {
                    float toStart = point - start;
                    if (Mathf.Abs(toStart) < threshold && Mathf.Abs(toStart) < bestDistance)
                    {
                        bestDistance = Mathf.Abs(toStart);
                        bestAdjust = toStart;
                        bestPoint = point;
                    }

                    if (Mathf.Approximately(start, end)) continue;
                    float toEnd = point - end;
                    if (Mathf.Abs(toEnd) < threshold && Mathf.Abs(toEnd) < bestDistance)
                    {
                        bestDistance = Mathf.Abs(toEnd);
                        bestAdjust = toEnd;
                        bestPoint = point;
                    }
                }
            }

            if (bestPoint >= 0f)
            {
                _state.SnapLineTime = bestPoint;
                return raw + bestAdjust;
            }

            _state.SnapLineTime = -1f;
            int primary = drag.Targets.IndexOf(drag.Primary);
            float primaryStart = drag.Starts[Mathf.Max(0, primary)];
            return _state.SnapToFrame(primaryStart + raw) - primaryStart;
        }

        private void UpdateDragTargetModule(ItemDrag drag, float localY)
        {
            drag.TargetModule = null;
            if (drag.SourceModule != null)
            {
                int rowIndex = RowIndexAtCanvasY(localY);
                var module = rowIndex >= 0 ? _cachedRows[rowIndex].Module : null;
                if (module != null && module != drag.SourceModule && module.ItemType == drag.SourceModule.ItemType
                    && !_state.IsModuleLocked(module) && _state.GetModuleTimeOffset(module) == _state.GetModuleTimeOffset(drag.SourceModule))
                    drag.TargetModule = module;
            }

            int sourceRow = drag.SourceModule != null ? RowOfModule(drag.SourceModule) : -1;
            int targetRow = drag.TargetModule != null ? RowOfModule(drag.TargetModule) : sourceRow;
            float shift = sourceRow >= 0 && targetRow >= 0 ? RowY(targetRow) - RowY(sourceRow) : 0f;
            foreach (var target in drag.Targets)
            {
                if (_elements.TryGetValue(target, out var element))
                    element.style.translate = new Translate(0f, shift);
            }
            ShowRowHighlight(drag.TargetModule != null ? targetRow : -1);
        }

        private void ShowRowHighlight(int rowIndex)
        {
            if (rowIndex < 0)
            {
                _rowHighlight?.RemoveFromHierarchy();
                _rowHighlight = null;
                return;
            }

            if (_rowHighlight == null || _rowHighlight.parent != _canvas)
            {
                var color = TimelineTheme.Accent;
                color.a = 0.1f;
                _rowHighlight = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, right = 0, height = TimelineTheme.RowHeight, backgroundColor = color }
                };
                _rowHighlight.Border(1, TimelineTheme.AccentDim);
                _canvas.Insert(0, _rowHighlight);
            }
            _rowHighlight.style.top = RowY(rowIndex);
        }

        private void EndItemDrag(bool duplicate)
        {
            var drag = _itemDrag;
            _itemDrag = null;
            if (drag == null) return;

            ShowRowHighlight(-1);
            _state.SnapLineTime = -1f;

            if (drag.Moved)
            {
                var container = _state.ActiveContainer;
                var items = new List<HonamiTimelineItem>();
                foreach (var target in drag.Targets)
                {
                    if (target is HonamiTimelineItem item) items.Add(item);
                }

                if (duplicate && container != null && items.Count > 0)
                    DuplicateDraggedItems(container, drag, items);
                else if (drag.TargetModule != null && container != null)
                    HonamiEditorController.MoveItemsToModule(container, items, drag.TargetModule);

                // Alt-drag copies only the clips; linked markers stay where they were with the originals.
                if (duplicate && drag.MarkerTargets.Count > 0)
                {
                    for (int i = 0; i < drag.MarkerTargets.Count; i++)
                    {
                        var marker = drag.MarkerTargets[i];
                        marker.time = drag.MarkerStarts[i];
                        if (marker is HonamiTimelineLoopMarker loop) loop.endTime = drag.MarkerEnds[i];
                    }
                    EditorUtility.SetDirty(_state.ActiveTimeline);
                }

                if (drag.TargetModule != null) _state.SelectedModule = drag.TargetModule;
                Undo.CollapseUndoOperations(drag.UndoGroup);
                _state.PostModifyState();
            }
            else if (!drag.Additive)
            {
                SelectTarget(drag.Primary, false);
            }

            _rebuild();
        }

        // Alt-drag leaves the originals where they were and drops copies at the new position.
        private void DuplicateDraggedItems(IHonamiTimelineModuleContainer container, ItemDrag drag, List<HonamiTimelineItem> items)
        {
            var target = drag.TargetModule;
            HonamiEditorController.RecordUndo(container, "Duplicate Timeline Items");
            var copies = new List<HonamiTimelineItem>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var owner = target ?? _state.FindModuleOf(item);
                if (owner == null) continue;
                var copy = HonamiEditorController.CloneTimelineItem(item);
                owner.AddItem(copy);
                copies.Add(copy);
                int index = drag.Targets.IndexOf(item);
                item.time = drag.Starts[index] - _state.GetItemTimeOffset(item);
            }
            HonamiEditorController.MarkModulesChanged(container);
            _state.SelectedTimelineItems.Clear();
            _state.SelectedTimelineItems.AddRange(copies);
        }

        #endregion

        #region Menus

        private void ShowAddMenu(float time, float canvasY)
        {
            var menu = new GenericMenu();

            int rowIndex = RowIndexAtCanvasY(canvasY);
            var rowModule = rowIndex >= 0 ? _cachedRows[rowIndex].Module : null;
            var hostItem = HostItemAt(rowModule, new Vector2(time * _state.TimeScale, canvasY));
            ParentChoice? nestTarget = null;
            if (hostItem != null && CanHostAnyModule(rowModule, hostItem))
                nestTarget = ItemChoice(rowModule, hostItem);
            else if (rowIndex >= 0 && _cachedRows[rowIndex].IsNodeRow && _cachedRows[rowIndex].MotionIndex >= 0)
                nestTarget = MotionChoice(_cachedRows[rowIndex].MotionIndex);
            else if (rowModule != null)
                nestTarget = CurrentParentChoice(rowModule);
            if (rowModule != null && rowModule != _state.SelectedModule)
            {
                _state.SelectedModule = rowModule;
                _state.SelectedTimelineItems.Clear();
                _state.ClearSelectedGap();
                _rebuild();
            }

            float snapped = SnapTime(time, null, ShouldSnap(false));
            _state.SnapLineTime = -1f;

            float gapStart = 0f;
            float gapEnd = 0f;
            bool hasGap = rowModule != null && !_state.IsModuleLocked(rowModule)
                && TryFindGap(rowModule, time, out gapStart, out gapEnd);
            if (hasGap)
            {
                var gapModule = rowModule;
                menu.AddItem(new GUIContent("Ripple Delete Gap"), false, () =>
                {
                    SelectGap(gapModule, gapStart, gapEnd);
                    if (RippleDeleteSelectedGap()) _rebuild();
                });
                menu.AddSeparator("");
            }

            var module = rowModule ?? _state.SelectedModule;
            if (module != null && _state.ActiveContainer != null)
            {
                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
                var context = CreateEditContext(module, snapped);
                float localTime = Mathf.Max(0f, snapped - context.TimeOffset);
                if (_state.IsModuleLocked(module))
                {
                    menu.AddDisabledItem(new GUIContent($"{editor.DisplayName} is locked"));
                }
                else
                {
                    menu.AddItem(new GUIContent($"Add {editor.ItemDisplayName}"), false, () =>
                    {
                        HonamiTimelineItem added = null;
                        ModifyModules(target =>
                        {
                            added = editor.CreateItem(context, localTime);
                            HonamiEditorController.AddTimelineItem(target, module, added);
                        });
                        if (added != null)
                        {
                            _state.SelectedTimelineItems.Clear();
                            _state.SelectedTimelineItems.Add(added);
                        }
                        _rebuild();
                    });
                    editor.AddContextMenuItems(context, menu, localTime);
                    if (ClipboardItems.Count > 0)
                        menu.AddItem(new GUIContent("Paste  (Ctrl+V)"), false, () => PasteAt(snapped, module));
                }
                menu.AddItem(new GUIContent("Select All On Track"), false, () =>
                {
                    SelectAllOnModule(module);
                    _rebuild();
                });
                menu.AddSeparator("");
            }

            if (_state.Mode == TimelineMode.HonamiState && _state.IsSeqState)
            {
                menu.AddItem(new GUIContent("Add Sequencer Clip"), false, () => AddSequencedClip(snapped));
                menu.AddSeparator("");
            }

            if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
            {
                menu.AddItem(new GUIContent("Add Marker Here"), false, () =>
                {
                    var marker = HonamiEditorController.AddCueMarker(_state.ActiveTimeline, snapped);
                    _state.ClearSelection();
                    _state.SelectedMarkers.Add(marker);
                    _rebuild();
                });
                menu.AddSeparator("");
            }

            if (nestTarget.HasValue)
                AddModuleTypeItems(menu, $"Add Module Inside {nestTarget.Value.Label.Replace('/', '∕')}/", nestTarget.Value);
            AddModuleTypeItems(menu, "Add Module/");

            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }

        private void ShowItemMenu(object target, float time)
        {
            var menu = new GenericMenu();
            float snapped = SnapTime(time, null, ShouldSnap(false));
            _state.SnapLineTime = -1f;

            if (target is HonamiTimelineItem item)
            {
                var module = _state.FindModuleOf(item);
                if (module == null) return;
                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
                var context = CreateEditContext(module);
                bool locked = _state.IsModuleLocked(module);
                var selection = new List<HonamiTimelineItem>(_state.SelectedTimelineItems);

                if (!locked)
                {
                    AddMenuItem(menu, "Split At Playhead  (S)", CanSplitAny(selection, _state.PlayheadTime), () => SplitItemsAt(selection, _state.PlayheadTime));
                    AddMenuItem(menu, "Split Here", CanSplitAny(selection, snapped), () => SplitItemsAt(selection, snapped));
                    AddMenuItem(menu, "Trim Start To Playhead  ([)", !item.IsInstant, () => TrimToPlayhead(true));
                    AddMenuItem(menu, "Trim End To Playhead  (])", !item.IsInstant, () => TrimToPlayhead(false));
                    menu.AddSeparator("");
                }

                menu.AddItem(new GUIContent("Copy  (Ctrl+C)"), false, CopySelection);
                if (!locked)
                {
                    menu.AddItem(new GUIContent("Cut  (Ctrl+X)"), false, () => { CopySelection(); DeleteSelection(false); });
                    menu.AddItem(new GUIContent("Duplicate  (Ctrl+D)"), false, DuplicateSelection);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Delete  (Del)"), false, () => DeleteSelection(false));
                    menu.AddItem(new GUIContent("Ripple Delete  (Shift+Del)"), false, () => DeleteSelection(true));
                }

                menu.AddSeparator("");
                editor.AddContextMenuItems(context, menu, snapped - context.TimeOffset);

                bool canLink = _state.SelectedTimelineItems.Count + _state.SelectedMarkers.Count >= 2;
                bool hasLinked = HasLinkedSelection();

                menu.AddSeparator("");
                AddMenuItem(menu, "Link Selected  (Ctrl+L)", canLink, () => LinkSelection(false));
                AddMenuItem(menu, "Link And Sequence Selected  (Ctrl+Shift+L)", canLink, () => LinkSelection(true));
                AddMenuItem(menu, "Unlink Selected", hasLinked, UnlinkSelection);
                AddMenuItem(menu, "Select Linked Group", hasLinked, SelectLinkedGroup);

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Frame Selection  (F)"), false, FrameSelection);
            }
            else if (target is HonamiSequencedAnimationClip)
            {
                menu.AddItem(new GUIContent("Delete  (Del)"), false, () => DeleteSelection(false));
            }

            menu.ShowAsContext();
            _rebuild();
        }

        private static void AddMenuItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction action)
        {
            if (enabled) menu.AddItem(new GUIContent(label), false, action);
            else menu.AddDisabledItem(new GUIContent(label));
        }

        private void ShowModuleHeaderMenu(HonamiTimelineModule module, VisualElement header = null)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename  (Double-click)"), false, () => BeginModuleRename(module, null));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(module.muted ? "Unmute" : "Mute"), false, () =>
            {
                ModifyModules(target => HonamiEditorController.ToggleModuleMute(target, module));
                _rebuild();
            });
            menu.AddItem(new GUIContent(module.locked ? "Unlock" : "Lock"), false, () =>
            {
                ModifyModules(target => HonamiEditorController.SetModuleLocked(target, module, !module.locked));
                _rebuild();
            });
            menu.AddItem(new GUIContent("Select All Items"), false, () =>
            {
                SelectAllOnModule(module);
                _rebuild();
            });
            AddParentItems(menu, "Parent/", module);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove Module"), false, () =>
            {
                ModifyModules(target => HonamiEditorController.DeleteModule(target, module));
                if (_state.SelectedModule == module) _state.SelectedModule = null;
                _rebuild();
            });
            menu.ShowAsContext();
        }

        private void AddSequencedClip(float time)
        {
            if (!_state.IsSeqState) return;
            _state.PreModifyState();
            Undo.RecordObject(_state.SeqNode, "Add Sequenced Clip");
            _state.SeqNode.sequencedClips.Add(new HonamiSequencedAnimationClip { id = HonamiNodeBase.NewMotionId(), startTime = time });
            EditorUtility.SetDirty(_state.SeqNode);
            _state.PostModifyState();
            _rebuild();
        }

        private TextField _renameField;
        private HonamiTimelineModule _renameModule;
        private string _renameValue;

        private void CommitActiveRename(bool save)
        {
            if (_renameModule == null) return;
            var module = _renameModule;
            string value = _renameField != null ? _renameField.value : _renameValue;
            _renameField?.RemoveFromHierarchy();
            _renameField = null;
            _renameModule = null;
            _renameValue = null;
            if (!save) return;
            string entered = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(entered)) return;
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            bool isDefault = entered == editor.DisplayName;
            ModifyModules(target =>
            {
                HonamiEditorController.RecordUndo(target, "Rename Module");
                module.moduleName = isDefault ? string.Empty : entered;
            });
        }

        private void BeginModuleRename(HonamiTimelineModule module, VisualElement header)
        {
            CommitActiveRename(true);

            // Find header: if stale or null, search live _headers children.
            if (header == null || header.panel == null)
            {
                header = null;
                foreach (var child in _headers.Children())
                {
                    if (child.userData is int idx && idx >= 0 && idx < _cachedRows.Count
                        && _cachedRows[idx].Module == module)
                    { header = child; break; }
                }
            }
            if (header == null) return;

            _state.SelectedModule = module;
            _state.SelectedTimelineItems.Clear();
            _state.SelectedMarkers.Clear();
            RefreshHeaderSelection();

            var foundLabel = header.Q<Label>(RowLabelName);
            string currentName = string.IsNullOrEmpty(module.moduleName)
                ? HonamiTimelineModuleEditorRegistry.GetEditor(module).DisplayName
                : module.moduleName;

            _renameModule = module;
            _renameValue = currentName;

            float fieldLeft = foundLabel != null ? foundLabel.style.left.value.value - 2f : 34f;
            float fieldRight = foundLabel != null ? foundLabel.style.right.value.value : 30f;

            _renameField = new TextField
            {
                value = currentName,
                style =
                {
                    position = Position.Absolute,
                    left = fieldLeft, right = fieldRight, top = 5, bottom = 5
                }
            };

            // Enter / Escape
            _renameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                { CommitActiveRename(true);  _rebuild(); evt.StopPropagation(); }
                else if (evt.keyCode == KeyCode.Escape)
                { CommitActiveRename(false); _rebuild(); evt.StopPropagation(); }
            }, TrickleDown.TrickleDown);

            // FocusOutEvent with a time guard to discard spurious immediate fires.
            // UIToolkit can fire FocusOutEvent on the TextField container within the same
            // event dispatch frame (e.g. on double-click or after IMGUI menu closes).
            // Any FocusOut arriving within 200 ms of opening is treated as spurious.
            double openTime = EditorApplication.timeSinceStartup;
            _renameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (EditorApplication.timeSinceStartup - openTime < 0.2) return;
                CommitActiveRename(true);
                _rebuild();
            });

            if (foundLabel != null) foundLabel.style.display = DisplayStyle.None;
            header.Add(_renameField);
            _renameField.Focus();
            _renameField.SelectAll();
        }

        #endregion

        #region Keyboard

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target is VisualElement target &&
                (target is TextField || target is FloatField || target is IntegerField || target.GetType().Name.Contains("TextInput")))
                return;
            if (IsStateEmpty()) return;

            bool ctrl = evt.ctrlKey || evt.commandKey;
            if (HandleShortcut(evt.keyCode, evt.character, ctrl, evt.shiftKey, evt.altKey))
                evt.StopPropagation();
        }

        private bool HandleShortcut(KeyCode key, char character, bool ctrl, bool shift, bool alt)
        {
            if (_helpOverlay != null && (key == KeyCode.Escape || key == KeyCode.F1 || character == '?'))
            {
                ToggleHelp();
                return true;
            }

            if (ctrl)
            {
                switch (key)
                {
                    case KeyCode.C: CopySelection(); return true;
                    case KeyCode.X: CopySelection(); DeleteSelection(false); return true;
                    case KeyCode.V: PasteSelection(); return true;
                    case KeyCode.D: DuplicateSelection(); return true;
                    case KeyCode.A: SelectAll(); return true;
                    case KeyCode.K: SplitAtPlayhead(); return true;
                    case KeyCode.L: LinkSelection(shift); return true;
                    case KeyCode.LeftArrow: SetPlayhead(0f); return true;
                    case KeyCode.RightArrow: SetPlayhead(_state.GetDuration()); return true;
                }
                return false;
            }

            float frame = _state.FrameDuration;
            switch (key)
            {
                case KeyCode.Space:
                    TimelineState.TogglePlayback(_state);
                    _rebuild();
                    return true;
                case KeyCode.J: Shuttle(-1); return true;
                case KeyCode.K: Shuttle(0); return true;
                case KeyCode.L: Shuttle(1); return true;
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    float step = (shift ? 10f : 1f) * frame * (key == KeyCode.LeftArrow ? -1f : 1f);
                    if (alt) NudgeSelection(step);
                    else SetPlayhead(_state.SnapToFrame(_state.PlayheadTime) + step);
                    return true;
                case KeyCode.UpArrow: JumpToEditPoint(-1); return true;
                case KeyCode.DownArrow: JumpToEditPoint(1); return true;
                case KeyCode.Home: SetPlayhead(0f); return true;
                case KeyCode.End: SetPlayhead(_state.GetDuration()); return true;
                case KeyCode.S: SplitAtPlayhead(); return true;
                case KeyCode.B: SetTool(TimelineEditTool.Blade); return true;
                case KeyCode.V: SetTool(TimelineEditTool.Select); return true;
                case KeyCode.M: AddMarkerAtPlayhead(); return true;
                case KeyCode.I: if (_state.Mode == TimelineMode.HonamiTimeline) SetLoopPoint(_state.PlayheadTime, true); return true;
                case KeyCode.O: if (_state.Mode == TimelineMode.HonamiTimeline) SetLoopPoint(_state.PlayheadTime, false); return true;
                case KeyCode.LeftBracket:
                case KeyCode.Q: TrimToPlayhead(true); return true;
                case KeyCode.RightBracket:
                case KeyCode.W: TrimToPlayhead(false); return true;
                case KeyCode.F: FrameSelection(); return true;
                case KeyCode.Backslash: FitAll(); return true;
                case KeyCode.Equals:
                case KeyCode.KeypadPlus: ZoomAt(1.5f, PlayheadViewportX()); return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus: ZoomAt(1f / 1.5f, PlayheadViewportX()); return true;
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteSelection(shift); return true;
                case KeyCode.Escape:
                    if (_state.Tool != TimelineEditTool.Select) SetTool(TimelineEditTool.Select);
                    else { _state.ClearSelection(); _rebuild(); }
                    return true;
                case KeyCode.F1: ToggleHelp(); return true;
            }

            if (character == '?')
            {
                ToggleHelp();
                return true;
            }
            return false;
        }

        private void Shuttle(int direction)
        {
            TimelineState.Shuttle(_state, direction);
            _rebuild();
        }

        private void SetTool(TimelineEditTool tool)
        {
            _state.Tool = tool;
            if (tool != TimelineEditTool.Blade && _bladeLine != null) _bladeLine.style.display = DisplayStyle.None;
            _rebuild();
        }

        private void SetPlayhead(float time)
        {
            _state.PlayheadTime = Mathf.Clamp(time, 0f, _state.GetDuration());
            UpdatePlayheadVisual();
            RefreshTimeLabel();
            FollowPlayhead();
            _rebuild();
        }

        private void JumpToEditPoint(int direction)
        {
            var points = _snapPointsBuffer;
            CollectSnapPoints(points, null);
            foreach (var item in _state.SelectedTimelineItems)
            {
                float offset = _state.GetItemTimeOffset(item);
                points.Add(offset + item.time);
                points.Add(offset + item.EndTime);
            }

            float current = _state.PlayheadTime;
            float epsilon = _state.FrameDuration * 0.5f;
            float best = direction > 0 ? float.MaxValue : float.MinValue;
            foreach (float point in points)
            {
                if (direction > 0 && point > current + epsilon && point < best) best = point;
                if (direction < 0 && point < current - epsilon && point > best) best = point;
            }
            if (best == float.MaxValue || best == float.MinValue) return;
            SetPlayhead(best);
        }

        private void AddMarkerAtPlayhead()
        {
            if (_state.Mode != TimelineMode.HonamiTimeline || _state.ActiveTimeline == null) return;
            var marker = HonamiEditorController.AddCueMarker(_state.ActiveTimeline, _state.SnapToFrame(_state.PlayheadTime));
            _state.ClearSelection();
            _state.SelectedMarkers.Add(marker);
            _rebuild();
        }

        private void FrameSelection()
        {
            float start = float.MaxValue;
            float end = float.MinValue;
            foreach (var item in _state.SelectedTimelineItems)
            {
                float offset = _state.GetItemTimeOffset(item);
                start = Mathf.Min(start, offset + item.time);
                end = Mathf.Max(end, offset + item.EndTime);
            }
            foreach (var clip in _state.SelectedSeqClips)
            {
                start = Mathf.Min(start, GetStartTime(clip));
                end = Mathf.Max(end, GetEndTime(clip));
            }
            foreach (var marker in _state.SelectedMarkers)
            {
                start = Mathf.Min(start, marker.time);
                end = Mathf.Max(end, marker is HonamiTimelineLoopMarker loop ? loop.endTime : marker.time);
            }

            if (start == float.MaxValue) FitAll();
            else ZoomToRange(start, end);
        }

        internal void ToggleHelpFromToolbar() => ToggleHelp();

        private void ToggleHelp()
        {
            if (_helpOverlay != null)
            {
                _helpOverlay.RemoveFromHierarchy();
                _helpOverlay = null;
                return;
            }
            _helpOverlay = BuildHelpOverlay();
            _timelineRoot.Add(_helpOverlay);
        }

        private VisualElement BuildHelpOverlay()
        {
            var overlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 0,
                    bottom = 0,
                    backgroundColor = new Color(0f, 0f, 0f, 0.55f),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                ToggleHelp();
                evt.StopPropagation();
            });

            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = TimelineTheme.PanelBg,
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingTop = 16,
                    paddingBottom = 16,
                    maxWidth = Length.Percent(92)
                }
            };
            card.Radius(8).Border(1, TimelineTheme.AccentDim);
            overlay.Add(card);

            card.Add(HelpColumn("Playback", new[]
            {
                ("Space", "Play / pause"),
                ("J  K  L", "Shuttle back / stop / forward, press again to go faster"),
                ("← →", "Previous / next frame (Shift: 10 frames)"),
                ("↑ ↓", "Previous / next edit point"),
                ("Home  End", "Start / end"),
                ("I  O", "Loop region in / out"),
                ("M", "Add marker (double-click the ruler too)")
            }));
            card.Add(HelpColumn("Editing", new[]
            {
                ("V  B", "Select tool / blade tool"),
                ("S  Ctrl+K", "Split at playhead"),
                ("[  ]  Q  W", "Trim start / end to playhead"),
                ("Alt+← →", "Nudge selection by a frame"),
                ("Alt+drag", "Duplicate while dragging"),
                ("Ctrl+L", "Link selected items or markers (Ctrl+Shift+L: link and lay them out in sequence)"),
                ("Del  Shift+Del", "Delete / ripple delete; click a gap to close it"),
                ("Ctrl+C X V D A", "Copy, cut, paste, duplicate, select all")
            }));
            card.Add(HelpColumn("View", new[]
            {
                ("Ctrl+wheel", "Zoom at cursor"),
                ("Shift+wheel", "Scroll horizontally"),
                ("Middle drag", "Pan"),
                ("= -", "Zoom in / out"),
                ("F", "Frame selection"),
                ("\\", "Fit whole timeline"),
                ("Shift", "Hold while dragging to toggle snapping"),
                ("? F1", "This help")
            }));
            return overlay;
        }

        private static VisualElement HelpColumn(string title, (string keys, string action)[] rows)
        {
            var column = new VisualElement { style = { marginRight = 24, minWidth = 220 } };
            column.Add(new Label(title) { style = { color = TimelineTheme.Accent, fontSize = 13, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });
            foreach (var (keys, action) in rows)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                row.Add(new Label(keys) { style = { width = 110, color = TimelineTheme.Text, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
                row.Add(new Label(action) { style = { flexShrink = 1, color = TimelineTheme.MutedText, fontSize = 11, whiteSpace = WhiteSpace.Normal, maxWidth = 240 } });
                column.Add(row);
            }
            return column;
        }

        #endregion

        #region Edit operations

        private List<HonamiTimelineItem> EditableSelection()
        {
            var items = new List<HonamiTimelineItem>();
            foreach (var item in _state.SelectedTimelineItems)
            {
                if (!_state.IsModuleLocked(_state.FindModuleOf(item))) items.Add(item);
            }
            return items;
        }

        // Without a selection, edits act on every unlocked item under the playhead.
        private List<HonamiTimelineItem> ItemsForPlayheadEdit()
        {
            var items = EditableSelection();
            if (items.Count > 0 || _state.ActiveModules == null) return items;

            float playhead = _state.PlayheadTime;
            foreach (var module in _state.ActiveModules)
            {
                if (module == null || _state.IsModuleLocked(module)) continue;
                if (_state.SelectedModule != null && module != _state.SelectedModule) continue;
                float offset = _state.GetModuleTimeOffset(module);
                int count = module.ItemCount;
                for (int i = 0; i < count; i++)
                {
                    var item = module.GetItem(i);
                    if (item != null && !item.IsInstant && playhead > offset + item.time && playhead < offset + item.EndTime)
                        items.Add(item);
                }
            }
            return items;
        }

        private bool CanSplitAny(List<HonamiTimelineItem> items, float time)
        {
            foreach (var item in items)
            {
                var module = _state.FindModuleOf(item);
                if (module == null) continue;
                float offset = _state.GetModuleTimeOffset(module);
                if (HonamiTimelineModuleEditorRegistry.GetEditor(module).CanSplit(item) && time > offset + item.time && time < offset + item.EndTime)
                    return true;
            }
            return false;
        }

        private void SplitAtPlayhead() => SplitItemsAt(ItemsForPlayheadEdit(), _state.PlayheadTime);

        private void SplitItemsAt(List<HonamiTimelineItem> items, float time)
        {
            if (items.Count == 0 || _state.ActiveContainer == null) return;

            var targets = new List<HonamiTimelineItem>();
            var localTimes = new List<float>();
            foreach (var item in items)
            {
                var module = _state.FindModuleOf(item);
                if (module == null || _state.IsModuleLocked(module)) continue;
                targets.Add(item);
                localTimes.Add(time - _state.GetModuleTimeOffset(module));
            }
            if (targets.Count == 0) return;

            List<HonamiTimelineItem> tails = null;
            var context = CreateEditContext(_state.FindModuleOf(targets[0]));
            ModifyModules(container => tails = HonamiEditorController.SplitTimelineItems(container, targets, localTimes, context));
            if (tails != null && tails.Count > 0)
            {
                _state.SelectedTimelineItems.Clear();
                _state.SelectedTimelineItems.AddRange(tails);
            }
            _rebuild();
        }

        private void TrimToPlayhead(bool trimStart)
        {
            var items = ItemsForPlayheadEdit();
            if (items.Count == 0 || _state.ActiveContainer == null) return;

            var localTimes = new List<float>();
            foreach (var item in items)
                localTimes.Add(_state.PlayheadTime - _state.GetItemTimeOffset(item));
            ModifyModules(container => HonamiEditorController.TrimTimelineItems(container, items, localTimes, trimStart));
            _rebuild();
        }

        private void NudgeSelection(float delta)
        {
            var items = EditableSelection();
            var markers = EditableMarkers();
            if (items.Count == 0 && markers.Count == 0) return;

            ExpandLinkGroup(items, markers);

            var container = _state.ActiveContainer;
            var timeline = _state.ActiveTimeline;
            bool moveItems = items.Count > 0 && container != null;
            bool moveMarkers = markers.Count > 0 && timeline != null;
            if (!moveItems && !moveMarkers) return;

            if (moveItems) ModifyModules(c => HonamiEditorController.NudgeTimelineItems(c, items, delta));
            if (moveMarkers) HonamiEditorController.MoveMarkers(timeline, markers, delta);
            _rebuild();
        }

        private void SelectAll()
        {
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
                        if (item != null) _state.SelectedTimelineItems.Add(item);
                    }
                }
            }
            if (_state.IsSeqState && _state.SeqNode?.sequencedClips != null)
                _state.SelectedSeqClips.AddRange(_state.SeqNode.sequencedClips);
            _rebuild();
        }

        private void SelectAllOnModule(HonamiTimelineModule module)
        {
            _state.ClearSelection();
            _state.SelectedModule = module;
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item != null) _state.SelectedTimelineItems.Add(item);
            }
        }

        private void DuplicateSelection()
        {
            var items = EditableSelection();
            if (items.Count == 0 || _state.ActiveContainer == null) return;
            List<HonamiTimelineItem> clones = null;
            ModifyModules(container => clones = HonamiEditorController.DuplicateTimelineItems(container, items));
            if (clones != null)
            {
                _state.SelectedTimelineItems.Clear();
                _state.SelectedTimelineItems.AddRange(clones);
            }
            _rebuild();
        }

        private void DeleteSelection(bool ripple)
        {
            bool changed = false;

            if (_state.HasSelectedGap)
            {
                if (RippleDeleteSelectedGap()) changed = true;
            }

            var items = EditableSelection();
            if (items.Count > 0 && _state.ActiveContainer != null)
            {
                if (ripple) ModifyModules(container => HonamiEditorController.RippleDeleteTimelineItems(container, items));
                else ModifyModules(container => HonamiEditorController.DeleteTimelineItems(container, items));
                _state.SelectedTimelineItems.Clear();
                changed = true;
            }

            if (_state.SelectedSeqClips.Count > 0)
            {
                _state.PreModifyState();
                for (int i = _state.SelectedSeqClips.Count - 1; i >= 0; i--)
                    HonamiEditorController.DeleteSequencedClip(_state.SeqNode, _state.SelectedSeqClips[i]);
                _state.PostModifyState();
                _state.SelectedSeqClips.Clear();
                changed = true;
            }

            if (_state.SelectedMarkers.Count > 0 && _state.ActiveTimeline != null)
            {
                HonamiEditorController.DeleteMarkers(_state.ActiveTimeline, new List<HonamiTimelineMarker>(_state.SelectedMarkers));
                _state.SelectedMarkers.Clear();
                changed = true;
            }

            if (_state.Mode == TimelineMode.HonamiClipEdit && _state.SelectedKeyframes.Count > 0)
            {
                DeleteSelectedKeyframes();
                changed = true;
            }

            if (changed) _rebuild();
        }

        private void CopySelection()
        {
            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                CopySelectedKeyframes();
                return;
            }

            if (_state.SelectedTimelineItems.Count == 0) return;
            ClipboardItems.Clear();
            ClipboardSources.Clear();
            TimelineState.CopiedTimelineItems.Clear();
            foreach (var item in _state.SelectedTimelineItems)
            {
                var clone = HonamiEditorController.CloneTimelineItem(item);
                clone.time = _state.GetItemTimeOffset(item) + item.time;
                ClipboardItems.Add(clone);
                ClipboardSources.Add(_state.FindModuleOf(item));
                TimelineState.CopiedTimelineItems.Add(clone);
            }
        }

        private void PasteSelection()
        {
            if (_state.Mode == TimelineMode.HonamiClipEdit)
            {
                PasteKeyframes();
                return;
            }
            PasteAt(_state.PlayheadTime, _state.SelectedModule);
        }

        private void PasteAt(float time, HonamiTimelineModule preferred)
        {
            var container = _state.ActiveContainer;
            if (container == null || ClipboardItems.Count == 0) return;

            float earliest = float.MaxValue;
            foreach (var item in ClipboardItems) earliest = Mathf.Min(earliest, item.time);

            var pasted = new List<HonamiTimelineItem>();
            ModifyModules(target =>
            {
                HonamiEditorController.RecordUndo(target, "Paste Timeline Items");
                for (int i = 0; i < ClipboardItems.Count; i++)
                {
                    var source = ClipboardItems[i];
                    var module = preferred != null && preferred.ItemType.IsInstanceOfType(source) && target.Modules.Contains(preferred) && !_state.IsModuleLocked(preferred)
                        ? preferred
                        : ClipboardSources[i];
                    if (module == null || !target.Modules.Contains(module) || _state.IsModuleLocked(module)) continue;

                    var clone = HonamiEditorController.CloneTimelineItem(source);
                    clone.time = Mathf.Max(0f, time + (source.time - earliest) - _state.GetModuleTimeOffset(module));
                    if (module.AddItem(clone)) pasted.Add(clone);
                }
                HonamiEditorController.MarkModulesChanged(target);
            });

            if (pasted.Count > 0)
            {
                _state.SelectedTimelineItems.Clear();
                _state.SelectedTimelineItems.AddRange(pasted);
            }
            _rebuild();
        }

        #endregion

        #region Drag and drop

        private void ClearDropGhost()
        {
            _dropGhost?.RemoveFromHierarchy();
            _dropGhost = null;
            ShowRowHighlight(-1);
        }

        private void ShowDropGhost(float time, float duration, int rowIndex)
        {
            if (_dropGhost == null || _dropGhost.parent != _canvas)
            {
                _dropGhost = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, height = TimelineTheme.ClipHeight, backgroundColor = new Color(1f, 1f, 1f, 0.12f) }
                };
                _dropGhost.Radius(4).Border(1, TimelineTheme.Accent);
                _canvas.Add(_dropGhost);
            }
            float y = rowIndex >= 0 ? RowY(rowIndex) : RowY(_cachedRows.Count);
            _dropGhost.style.left = time * _state.TimeScale;
            _dropGhost.style.width = Mathf.Max(8f, duration * _state.TimeScale);
            _dropGhost.style.top = y + (TimelineTheme.RowHeight - TimelineTheme.ClipHeight) * 0.5f;
            ShowRowHighlight(rowIndex);
        }

        private static float DroppedDuration(Object dropped) => dropped switch
        {
            AnimationClip clip => clip.length,
            AudioClip audio => audio.length,
            _ => 1f
        };

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var objects = DragAndDrop.objectReferences;
            if (objects.Length == 0) return;

            if (_state.Mode == TimelineMode.HonamiClipEdit || IsStateEmpty() || _state.ActiveContainer == null)
            {
                if (objects[0] is AnimationClip)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    evt.StopPropagation();
                }
                return;
            }

            var local = _canvas.WorldToLocal(evt.mousePosition);
            float time = SnapTime(Mathf.Max(0f, TimeAtCanvasX(local.x)), null, ShouldSnap(evt.shiftKey));
            int rowIndex = RowIndexAtCanvasY(local.y);
            var module = rowIndex >= 0 ? _cachedRows[rowIndex].Module : null;

            if (module != null && !_state.IsModuleLocked(module) && HonamiTimelineModuleEditorRegistry.GetEditor(module).AcceptsDrop(objects[0]))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                ShowDropGhost(time, DroppedDuration(objects[0]), rowIndex);
            }
            else if (HonamiTimelineModuleEditorRegistry.FindModuleTypeForDrop(objects[0], _state.ActiveContainer.Host) != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                ShowDropGhost(time, DroppedDuration(objects[0]), -1);
            }
            else if (_state.Mode == TimelineMode.HonamiTimeline && objects[0] is GameObject go && !EditorUtility.IsPersistent(go))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                ShowRowHighlight(rowIndex);
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                ClearDropGhost();
            }
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var objects = DragAndDrop.objectReferences;
            ClearDropGhost();
            if (objects.Length == 0) return;

            if (_state.Mode == TimelineMode.HonamiClipEdit || IsStateEmpty() || _state.ActiveContainer == null)
            {
                if (objects[0] is AnimationClip clip)
                {
                    DragAndDrop.AcceptDrag();
                    _state.Mode = TimelineMode.HonamiClipEdit;
                    _state.ActiveClip = clip;
                    _state.PlayheadTime = 0f;
                    _state.IsPlaying = false;
                    _state.PreviewEnabled = true;
                    _rebuild();
                    evt.StopPropagation();
                }
                return;
            }

            var local = _canvas.WorldToLocal(evt.mousePosition);
            float time = SnapTime(Mathf.Max(0f, TimeAtCanvasX(local.x)), null, ShouldSnap(evt.shiftKey));
            _state.SnapLineTime = -1f;
            int rowIndex = RowIndexAtCanvasY(local.y);
            var module = rowIndex >= 0 ? _cachedRows[rowIndex].Module : null;

            if (objects[0] is GameObject sceneObject && !EditorUtility.IsPersistent(sceneObject) && _state.Mode == TimelineMode.HonamiTimeline)
            {
                DragAndDrop.AcceptDrag();
                ShowGameObjectDropMenu(sceneObject, module, time);
                evt.StopPropagation();
                return;
            }

            if (module == null || _state.IsModuleLocked(module) || !HonamiTimelineModuleEditorRegistry.GetEditor(module).AcceptsDrop(objects[0]))
            {
                var moduleType = HonamiTimelineModuleEditorRegistry.FindModuleTypeForDrop(objects[0], _state.ActiveContainer.Host);
                if (moduleType == null) return;
                HonamiTimelineModule created = null;
                ModifyModules(container => created = HonamiEditorController.AddModule(container, moduleType));
                module = created;
                if (module == null) return;
            }

            DragAndDrop.AcceptDrag();
            DropObjectsOnModule(module, objects, time);
            evt.StopPropagation();
        }

        private void DropObjectsOnModule(HonamiTimelineModule module, Object[] objects, float time)
        {
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            var context = CreateEditContext(module, time);
            float cursor = Mathf.Max(0f, time - context.TimeOffset);
            var added = new List<HonamiTimelineItem>();

            ModifyModules(container =>
            {
                HonamiEditorController.RecordUndo(container, "Drop On Timeline");
                foreach (var dropped in objects)
                {
                    if (!editor.AcceptsDrop(dropped)) continue;
                    var item = editor.CreateItemFromDrop(context, dropped, cursor);
                    if (item == null || !module.AddItem(item)) continue;
                    added.Add(item);
                    cursor = item.EndTime;
                }
                HonamiEditorController.MarkModulesChanged(container);
            });

            _state.ClearSelection();
            _state.SelectedModule = module;
            _state.SelectedTimelineItems.AddRange(added);
            _rebuild();
        }

        private void ShowGameObjectDropMenu(GameObject sceneObject, HonamiTimelineModule rowModule, float time)
        {
            var timeline = _state.ActiveTimeline;
            if (timeline == null) return;
            var menu = new GenericMenu();

            if (rowModule is TimelineCameraModule cameraModule && !_state.IsModuleLocked(cameraModule))
            {
                menu.AddItem(new GUIContent($"Add Shot Anchored To '{sceneObject.name}'"), false, () =>
                {
                    var director = EnsureDirector();
                    if (director == null) return;
                    var shot = new TimelineCameraShotItem { time = time - _state.GetModuleTimeOffset(cameraModule), anchor = HonamiSceneReference.NewKey() };
                    Undo.RecordObject(director, "Assign Shot Anchor");
                    director.SetReference(shot.anchor.Key, sceneObject.transform);
                    EditorUtility.SetDirty(director);
                    ModifyModules(container => HonamiEditorController.AddTimelineItem(container, cameraModule, shot));
                    _rebuild();
                });
                menu.AddSeparator("");
            }

            if (rowModule != null && rowModule.BindingType != null && HonamiTimelineModuleBinding.Convert(sceneObject, rowModule.BindingType) != null)
            {
                var captured = rowModule;
                menu.AddItem(new GUIContent($"Bind '{captured.moduleName}' To '{sceneObject.name}'"), false, () => BindModule(captured, sceneObject));
                menu.AddSeparator("");
            }

            foreach (var moduleType in HonamiTimelineModuleEditorRegistry.ModuleTypesFor(HonamiTimelineModuleHost.Timeline))
            {
                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(moduleType);
                var prototype = editor.CreateModule();
                if (prototype.BindingType == null || HonamiTimelineModuleBinding.Convert(sceneObject, prototype.BindingType) == null) continue;

                var capturedType = moduleType;
                menu.AddItem(new GUIContent($"New {editor.DisplayName} Track For '{sceneObject.name}'"), false, () =>
                {
                    HonamiTimelineModule created = null;
                    ModifyModules(container => created = HonamiEditorController.AddModule(container, capturedType));
                    if (created == null) return;
                    created.moduleName = $"{sceneObject.name} {editor.DisplayName}";
                    BindModule(created, sceneObject);
                    _state.SelectedModule = created;
                });
            }

            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent($"No module can use '{sceneObject.name}'"));
            menu.ShowAsContext();
        }

        private HonamiDirector EnsureDirector()
        {
            var director = _state.ResolveDirector();
            if (director != null || _state.ActiveTimeline == null) return director;
            director = HonamiTimelineEditContext.CreateDirector(_state.ActiveTimeline);
            _state.ForgetDirector();
            return director;
        }

        private void BindModule(HonamiTimelineModule module, GameObject sceneObject)
        {
            var director = EnsureDirector();
            if (director == null) return;
            HonamiEditorController.SetTimelineBinding(director, module, HonamiTimelineModuleBinding.Convert(sceneObject, module.BindingType));
            _state.ClearCache();
            _rebuild();
        }

        #endregion
    }
}
