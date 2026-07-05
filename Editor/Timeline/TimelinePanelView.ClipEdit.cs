#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private const float KeySize = 11f;
        private const float TimeEpsilon = 1e-4f;

        private struct KeyColumn
        {
            public VisualElement Element;
            public float Time;
            public List<KeyframeRef> Members;
            public Color RowColor;
            public bool IsSummary;
        }

        private readonly List<KeyColumn> _keyColumns = new();
        private VisualElement _playheadBand;

        private bool _draggingKeys;
        private bool _keyDragMoved;
        private float _keyDragStartMouseTime;
        private float _keyDragPrimaryTime;
        private float _keyDragDelta;
        private readonly List<KeyframeRef> _keyDragRefs = new();

        private void DrawClipEditContent(float y)
        {
            if (_state.ActiveClip == null) return;

            EnsureCurveCache();
            _keyColumns.Clear();

            var rows = GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                DrawRowKeyframes(row, y);
                y += TimelineTheme.RowHeight;
            }
        }

        private void DrawRowKeyframes(RowData row, float rowY)
        {
            if (row.BindingCount == 0) return;
            if (!row.IsSummaryRow && IsBindingMuted(row.BindingStart)) return;

            var columns = GatherColumns(row.BindingStart, row.BindingCount);
            bool isSummary = row.IsGroup || row.IsSummaryRow;
            for (int i = 0; i < columns.Count; i++)
                AddKeyDiamond(columns[i].time, columns[i].refs, rowY, row.Color, isSummary);
        }

        private static string BoneChannelKey(string path) => "b:" + path;
        private static string PropChannelKey(string groupId) => "p:" + groupId;

        private string RowChannelKey(RowData row)
        {
            if (row.IsBoneGroup) return BoneChannelKey(row.BonePath);
            if (row.IsGroup) return PropChannelKey(row.GroupId);
            var b = _curveCache.Bindings[row.BindingStart];
            return "k:" + b.path + "|" + b.propertyName;
        }

        private bool ChannelFlagged(HashSet<string> set, int binding)
        {
            if (set.Count == 0 || binding >= _curveCache.Bindings.Length) return false;
            var b = _curveCache.Bindings[binding];
            if (set.Contains("b:" + b.path)) return true;
            if (set.Contains("k:" + b.path + "|" + b.propertyName)) return true;

            string prop = b.propertyName;
            if (prop.StartsWith("m_LocalPosition") || prop.StartsWith("m_LocalRotation") || prop.StartsWith("m_LocalScale"))
            {
                int dot = prop.LastIndexOf('.');
                if (dot > 0 && set.Contains("p:" + b.path + ":" + prop.Substring(0, dot))) return true;
            }
            return false;
        }

        private bool IsBindingMuted(int binding) => ChannelFlagged(_state.MutedChannels, binding);
        private bool IsBindingLocked(int binding) => ChannelFlagged(_state.LockedChannels, binding);

        private void ToggleChannel(HashSet<string> set, string key)
        {
            if (!set.Remove(key)) set.Add(key);
        }

        private void FillClipChannelHeader(VisualElement header, RowData row)
        {
            if (row.IsSummaryRow)
            {
                header.style.backgroundColor = TimelineTheme.SummaryHeaderBg;
                var sdot = RectElement(10f, 16f, 8f, 8f, row.Color);
                sdot.style.borderTopLeftRadius = sdot.style.borderTopRightRadius =
                    sdot.style.borderBottomLeftRadius = sdot.style.borderBottomRightRadius = 2;
                IgnorePicking(sdot);
                header.Add(sdot);

                var slabel = RowLabel(row.Title);
                slabel.style.left = 24f;
                slabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(slabel);
                return;
            }

            bool muted = IsBindingMuted(row.BindingStart);
            bool locked = IsBindingLocked(row.BindingStart);

            float labelLeft = 10f + row.Depth * 15f;

            if (row.IsGroup)
            {
                var foldout = new Label(row.IsExpanded ? "▼" : "▶")
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = labelLeft, top = 12, fontSize = 9, color = TimelineTheme.MutedText, unityFontStyleAndWeight = FontStyle.Bold }
                };
                header.Add(foldout);
                labelLeft += 14f;
            }

            var dot = RectElement(labelLeft, 16f, 8f, 8f, row.Color);
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 2;
            IgnorePicking(dot);
            header.Add(dot);
            labelLeft += 14f;

            var label = RowLabel(row.Title);
            label.style.left = labelLeft;
            label.style.right = 52;
            if (row.IsBoneGroup) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (muted) label.style.opacity = 0.4f;
            header.Add(label);

            var lockToggle = ChannelIconToggle("AssemblyLock", locked, "▪", locked ? "Locked — protected from edits" : "Lock channel", () =>
            {
                ToggleChannel(_state.LockedChannels, RowChannelKey(row));
                _rebuild();
            });
            lockToggle.style.right = 6;
            header.Add(lockToggle);

            var eyeToggle = ChannelIconToggle(muted ? "animationvisibilitytoggleoff" : "animationvisibilitytoggleon", !muted, muted ? "○" : "◉", muted ? "Hidden — click to show keys" : "Visible — click to hide keys", () =>
            {
                ToggleChannel(_state.MutedChannels, RowChannelKey(row));
                _rebuild();
            });
            eyeToggle.style.right = 28;
            header.Add(eyeToggle);
        }

        private VisualElement ChannelIconToggle(string iconName, bool active, string glyphFallback, string tooltip, System.Action onClick)
        {
            var el = new VisualElement
            {
                tooltip = tooltip,
                style =
                {
                    position = Position.Absolute,
                    top = 11,
                    width = 18,
                    height = 18,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var icon = EditorGUIUtility.IconContent(iconName)?.image;
            if (icon != null)
            {
                el.Add(new Image { image = icon, pickingMode = PickingMode.Ignore, style = { width = 14, height = 14 } });
            }
            else
            {
                el.Add(new Label(glyphFallback)
                {
                    pickingMode = PickingMode.Ignore,
                    style = { fontSize = 12, color = TimelineTheme.Text, unityTextAlign = TextAnchor.MiddleCenter }
                });
            }

            el.style.opacity = active ? 0.9f : 0.28f;
            el.RegisterCallback<PointerEnterEvent>(_ => el.style.opacity = active ? 1f : 0.55f);
            el.RegisterCallback<PointerLeaveEvent>(_ => el.style.opacity = active ? 0.9f : 0.28f);
            el.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                onClick();
                evt.StopPropagation();
            });
            return el;
        }

        private void DrawCurrentFrameBand(float height)
        {
            _playheadBand = null;
            if (_state.Mode != TimelineMode.HonamiClipEdit) return;

            float rulerHeight = TimelineTheme.RulerHeight;
            float frameW = CurrentFrameWidth();
            _playheadBand = RectElement(_state.PlayheadTime * _state.TimeScale - frameW * 0.5f, rulerHeight, frameW, Mathf.Max(0f, height - rulerHeight), TimelineTheme.CurrentFrameBand);
            IgnorePicking(_playheadBand);
            _canvas.Add(_playheadBand);
        }

        private float CurrentFrameWidth()
        {
            float fps = DisplayFps();
            return fps > 0f ? _state.TimeScale / fps : 6f;
        }

        private void UpdateCurrentFrameBand()
        {
            if (_playheadBand == null) return;
            float frameW = CurrentFrameWidth();
            _playheadBand.style.left = _state.PlayheadTime * _state.TimeScale - frameW * 0.5f;
            _playheadBand.style.width = frameW;
        }

        private List<(float time, List<KeyframeRef> refs)> GatherColumns(int bindingStart, int bindingCount)
        {
            var map = new SortedDictionary<int, (float time, List<KeyframeRef> refs)>();
            var bindings = _curveCache.Bindings;
            int end = Mathf.Min(bindingStart + bindingCount, bindings.Length);

            for (int b = bindingStart; b < end; b++)
            {
                if (IsBindingMuted(b)) continue;
                var curve = _curveCache.Curves[b];
                if (curve == null) continue;
                var keys = curve.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    float t = keys[k].time;
                    int q = Mathf.RoundToInt(t * 1000f);
                    if (!map.TryGetValue(q, out var col))
                    {
                        col = (t, new List<KeyframeRef>());
                        map[q] = col;
                    }
                    col.refs.Add(new KeyframeRef(b, t));
                }
            }

            var result = new List<(float, List<KeyframeRef>)>(map.Count);
            foreach (var kv in map.Values)
                result.Add(kv);
            return result;
        }

        private void AddKeyDiamond(float time, List<KeyframeRef> members, float rowY, Color rowColor, bool isSummary)
        {
            float size = isSummary ? KeySize - 2f : KeySize;
            float x = time * _state.TimeScale - size * 0.5f;
            float top = rowY + (TimelineTheme.RowHeight - size) * 0.5f;

            var diamond = RectElement(x, top, size, size, Color.clear);
            diamond.style.rotate = new Rotate(45);
            diamond.style.borderTopLeftRadius = diamond.style.borderTopRightRadius =
                diamond.style.borderBottomLeftRadius = diamond.style.borderBottomRightRadius = 2;

            bool selected = AnyMemberSelected(members);
            StyleDiamond(diamond, rowColor, selected, isSummary);
            diamond.tooltip = KeyTooltip(members, time);

            diamond.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_draggingKeys) return;
                diamond.style.scale = new Scale(new Vector2(1.25f, 1.25f));
            });
            diamond.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_draggingKeys) return;
                diamond.style.scale = new Scale(Vector2.one);
            });

            RegisterKeyDiamondInput(diamond, members);

            var column = new KeyColumn { Element = diamond, Time = time, Members = members, RowColor = rowColor, IsSummary = isSummary };
            _keyColumns.Add(column);
            foreach (var m in members)
                _state.KeyframeRects[m] = new Rect(x - 2f, top - 2f, size + 4f, size + 4f);

            _canvas.Add(diamond);
        }

        private void StyleDiamond(VisualElement diamond, Color rowColor, bool selected, bool isSummary)
        {
            Color border = selected ? TimelineTheme.KeyframeSelected : rowColor;
            Color fill = selected ? TimelineTheme.KeyframeSelected : (isSummary ? Color.clear : TimelineTheme.KeyframeFill);

            diamond.style.backgroundColor = fill;
            diamond.style.borderTopWidth = diamond.style.borderRightWidth =
                diamond.style.borderBottomWidth = diamond.style.borderLeftWidth = isSummary ? 1.5f : 2f;
            diamond.style.borderTopColor = diamond.style.borderRightColor =
                diamond.style.borderBottomColor = diamond.style.borderLeftColor = border;
        }

        private void RegisterKeyDiamondInput(VisualElement diamond, List<KeyframeRef> members)
        {
            diamond.RegisterCallback<PointerDownEvent>(evt =>
            {
                Focus();
                _focus();

                if (evt.button == 1)
                {
                    if (!AllMembersSelected(members))
                        SelectKeyColumn(members, false);
                    ShowKeyframeContextMenu();
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 0) return;

                bool additive = evt.shiftKey || evt.ctrlKey || evt.commandKey;
                if (additive)
                    ToggleKeyColumn(members);
                else if (!AllMembersSelected(members))
                    SelectKeyColumn(members, false);

                RefreshKeyframeSelectionVisual();

                if (_state.IsClipReadOnly) { evt.StopPropagation(); return; }

                _draggingKeys = true;
                _keyDragMoved = false;
                _keyDragDelta = 0f;
                _keyDragPrimaryTime = members.Count > 0 ? members[0].Time : 0f;
                _keyDragStartMouseTime = TimeAtPointer(evt);
                _keyDragRefs.Clear();
                foreach (var r in _state.SelectedKeyframes)
                    if (!IsBindingLocked(r.Binding)) _keyDragRefs.Add(r);
                diamond.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            diamond.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingKeys || !diamond.HasPointerCapture(evt.pointerId)) return;

                float rawDelta = TimeAtPointer(evt) - _keyDragStartMouseTime;
                float snappedPrimary = SnapTime(Mathf.Max(0f, _keyDragPrimaryTime + rawDelta), null, ShouldSnap(evt.shiftKey));
                _keyDragDelta = snappedPrimary - _keyDragPrimaryTime;
                if (Mathf.Abs(_keyDragDelta) > TimeEpsilon) _keyDragMoved = true;

                RepositionDraggedKeys();
                UpdateSnapLineVisual();
                evt.StopPropagation();
            });

            diamond.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingKeys) return;
                if (diamond.HasPointerCapture(evt.pointerId)) diamond.ReleasePointer(evt.pointerId);

                _draggingKeys = false;
                _state.SnapLineTime = -1f;
                if (_keyDragMoved) CommitKeyDrag();
                _keyDragRefs.Clear();
                evt.StopPropagation();
            });
        }

        private void RepositionDraggedKeys()
        {
            float scale = _state.TimeScale;
            for (int i = 0; i < _keyColumns.Count; i++)
            {
                var col = _keyColumns[i];
                if (!ColumnInDrag(col)) continue;
                float newTime = Mathf.Max(0f, col.Time + _keyDragDelta);
                float size = col.IsSummary ? KeySize - 2f : KeySize;
                col.Element.style.left = newTime * scale - size * 0.5f;
            }
        }

        private bool ColumnInDrag(KeyColumn col)
        {
            for (int i = 0; i < col.Members.Count; i++)
                if (_keyDragRefs.Contains(col.Members[i])) return true;
            return false;
        }

        private void CommitKeyDrag()
        {
            if (_state.IsClipReadOnly || _keyDragRefs.Count == 0) return;

            Undo.RecordObject(_state.ActiveClip, "Move Keyframes");
            float duration = _state.GetDuration();

            var byBinding = new Dictionary<int, List<(float from, float to)>>();
            foreach (var r in _keyDragRefs)
            {
                float to = Mathf.Clamp(r.Time + _keyDragDelta, 0f, duration);
                if (!byBinding.TryGetValue(r.Binding, out var list))
                {
                    list = new List<(float, float)>();
                    byBinding[r.Binding] = list;
                }
                list.Add((r.Time, to));
            }

            foreach (var kv in byBinding)
            {
                if (IsBindingLocked(kv.Key)) continue;
                var curve = _curveCache.Curves[kv.Key];
                if (curve == null) continue;
                var keys = curve.keys;
                foreach (var move in kv.Value)
                {
                    int idx = FindKeyIndex(keys, move.from);
                    if (idx >= 0) keys[idx].time = move.to;
                }
                System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));
                var newCurve = new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                AnimationUtility.SetEditorCurve(_state.ActiveClip, _curveCache.Bindings[kv.Key], newCurve);
            }

            var reselect = new List<KeyframeRef>();
            foreach (var r in _keyDragRefs)
                reselect.Add(new KeyframeRef(r.Binding, Mathf.Clamp(r.Time + _keyDragDelta, 0f, duration)));

            FinishClipEdit(reselect);
        }

        private void ShowKeyframeContextMenu()
        {
            var menu = new GenericMenu();
            bool readOnly = _state.IsClipReadOnly;
            bool hasSelection = _state.SelectedKeyframes.Count > 0;

            if (readOnly)
            {
                menu.AddDisabledItem(new GUIContent("Clip is read-only (embedded in FBX)"));
                menu.ShowAsContext();
                return;
            }

            if (hasSelection)
            {
                menu.AddItem(new GUIContent("Delete Keys"), false, () => { DeleteSelectedKeyframes(); _rebuild(); });
                menu.AddItem(new GUIContent("Copy Keys"), false, CopySelectedKeyframes);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Tangents/Auto"), false, () => SetSelectedKeyTangents(AnimationUtility.TangentMode.ClampedAuto, false));
                menu.AddItem(new GUIContent("Tangents/Free Smooth"), false, () => SetSelectedKeyTangents(AnimationUtility.TangentMode.Free, false));
                menu.AddItem(new GUIContent("Tangents/Flat"), false, () => SetSelectedKeyTangents(AnimationUtility.TangentMode.Free, true));
                menu.AddItem(new GUIContent("Tangents/Linear"), false, () => SetSelectedKeyTangents(AnimationUtility.TangentMode.Linear, false));
                menu.AddItem(new GUIContent("Tangents/Constant"), false, () => SetSelectedKeyTangents(AnimationUtility.TangentMode.Constant, false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("No keys selected"));
            }

            if (_state.CopiedKeyframes.Count > 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Paste Keys at Playhead"), false, PasteKeyframes);
            }

            menu.ShowAsContext();
        }

        private void ShowClipEditContextMenu(float localY)
        {
            var rows = GetRows();
            int idx = Mathf.FloorToInt((localY - TimelineTheme.RulerHeight) / TimelineTheme.RowHeight);
            var menu = new GenericMenu();
            bool readOnly = _state.IsClipReadOnly;

            if (idx >= 0 && idx < rows.Count && !readOnly)
            {
                var row = rows[idx];
                string label = row.IsSummaryRow ? "Add Key at Playhead (all channels)"
                    : row.IsBoneGroup ? "Add Key at Playhead (whole bone)"
                    : "Add Key at Playhead";
                menu.AddItem(new GUIContent(label), false, () => AddKeyAtPlayhead(row.BindingStart, row.BindingCount));
            }
            else if (readOnly)
            {
                menu.AddDisabledItem(new GUIContent("Clip is read-only (embedded in FBX)"));
            }

            if (_state.SelectedKeyframes.Count > 0 && !readOnly)
            {
                menu.AddItem(new GUIContent("Delete Selected Keys"), false, () => { DeleteSelectedKeyframes(); _rebuild(); });
                menu.AddItem(new GUIContent("Copy Selected Keys"), false, CopySelectedKeyframes);
            }

            if (_state.CopiedKeyframes.Count > 0 && !readOnly)
                menu.AddItem(new GUIContent("Paste Keys at Playhead"), false, PasteKeyframes);

            if (menu.GetItemCount() > 0)
                menu.ShowAsContext();
        }

        private void AddKeyAtPlayhead(int bindingStart, int bindingCount)
        {
            if (_state.IsClipReadOnly) return;

            Undo.RecordObject(_state.ActiveClip, "Add Keyframe");
            float t = _state.PlayheadTime;
            var bindings = _curveCache.Bindings;
            int end = Mathf.Min(bindingStart + bindingCount, bindings.Length);
            var added = new List<KeyframeRef>();

            for (int b = bindingStart; b < end; b++)
            {
                if (IsBindingLocked(b)) continue;
                var curve = _curveCache.Curves[b] ?? new AnimationCurve();
                var keys = curve.keys;
                if (FindKeyIndex(keys, t) >= 0) continue;

                float value = keys.Length > 0 ? curve.Evaluate(t) : 0f;
                curve.AddKey(new Keyframe(t, value));
                AnimationUtility.SetEditorCurve(_state.ActiveClip, bindings[b], curve);
                added.Add(new KeyframeRef(b, t));
            }

            FinishClipEdit(added);
        }

        private void DeleteSelectedKeyframes()
        {
            if (_state.IsClipReadOnly || _state.SelectedKeyframes.Count == 0) return;

            Undo.RecordObject(_state.ActiveClip, "Delete Keyframes");

            var byBinding = new Dictionary<int, List<float>>();
            foreach (var r in _state.SelectedKeyframes)
            {
                if (!byBinding.TryGetValue(r.Binding, out var list))
                {
                    list = new List<float>();
                    byBinding[r.Binding] = list;
                }
                list.Add(r.Time);
            }

            foreach (var kv in byBinding)
            {
                if (IsBindingLocked(kv.Key)) continue;
                var curve = _curveCache.Curves[kv.Key];
                if (curve == null) continue;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    int idx = FindKeyIndex(curve.keys, kv.Value[i]);
                    if (idx >= 0) curve.RemoveKey(idx);
                }
                AnimationUtility.SetEditorCurve(_state.ActiveClip, _curveCache.Bindings[kv.Key], curve.length > 0 ? curve : null);
            }

            EditorUtility.SetDirty(_state.ActiveClip);
            _state.SelectedKeyframes.Clear();
            _curveCache.Clear();
        }

        private void SetSelectedKeyTangents(AnimationUtility.TangentMode mode, bool flat)
        {
            if (_state.IsClipReadOnly || _state.SelectedKeyframes.Count == 0) return;

            Undo.RecordObject(_state.ActiveClip, "Set Key Tangents");

            var byBinding = new Dictionary<int, List<float>>();
            foreach (var r in _state.SelectedKeyframes)
            {
                if (!byBinding.TryGetValue(r.Binding, out var list))
                {
                    list = new List<float>();
                    byBinding[r.Binding] = list;
                }
                list.Add(r.Time);
            }

            foreach (var kv in byBinding)
            {
                if (IsBindingLocked(kv.Key)) continue;
                var curve = _curveCache.Curves[kv.Key];
                if (curve == null) continue;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    int idx = FindKeyIndex(curve.keys, kv.Value[i]);
                    if (idx < 0) continue;

                    AnimationUtility.SetKeyLeftTangentMode(curve, idx, mode);
                    AnimationUtility.SetKeyRightTangentMode(curve, idx, mode);
                    if (flat)
                    {
                        var key = curve[idx];
                        key.inTangent = 0f;
                        key.outTangent = 0f;
                        curve.MoveKey(idx, key);
                    }
                }
                AnimationUtility.SetEditorCurve(_state.ActiveClip, _curveCache.Bindings[kv.Key], curve);
            }

            EditorUtility.SetDirty(_state.ActiveClip);
            _curveCache.Clear();
            _rebuild();
        }

        private void CopySelectedKeyframes()
        {
            _state.CopiedKeyframes.Clear();
            if (_state.SelectedKeyframes.Count == 0) return;

            float min = float.MaxValue;
            foreach (var r in _state.SelectedKeyframes)
                if (r.Time < min) min = r.Time;

            foreach (var r in _state.SelectedKeyframes)
            {
                var curve = _curveCache.Curves[r.Binding];
                if (curve == null) continue;
                int idx = FindKeyIndex(curve.keys, r.Time);
                if (idx < 0) continue;
                _state.CopiedKeyframes.Add(new KeyframeClipboardEntry { Binding = r.Binding, RelTime = r.Time - min, Key = curve[idx] });
            }
        }

        private void PasteKeyframes()
        {
            if (_state.IsClipReadOnly || _state.CopiedKeyframes.Count == 0) return;

            Undo.RecordObject(_state.ActiveClip, "Paste Keyframes");
            float baseT = _state.PlayheadTime;
            float duration = _state.GetDuration();
            var bindings = _curveCache.Bindings;
            var pasted = new List<KeyframeRef>();
            var touched = new HashSet<int>();

            foreach (var entry in _state.CopiedKeyframes)
            {
                if (entry.Binding < 0 || entry.Binding >= bindings.Length) continue;
                if (IsBindingLocked(entry.Binding)) continue;
                var curve = _curveCache.Curves[entry.Binding] ?? new AnimationCurve();
                float t = Mathf.Clamp(baseT + entry.RelTime, 0f, duration);

                int existing = FindKeyIndex(curve.keys, t);
                if (existing >= 0) curve.RemoveKey(existing);

                var key = entry.Key;
                key.time = t;
                curve.AddKey(key);
                _curveCache.Curves[entry.Binding] = curve;
                touched.Add(entry.Binding);
                pasted.Add(new KeyframeRef(entry.Binding, t));
            }

            foreach (int b in touched)
                AnimationUtility.SetEditorCurve(_state.ActiveClip, bindings[b], _curveCache.Curves[b]);

            FinishClipEdit(pasted);
        }

        private void FinishClipEdit(List<KeyframeRef> reselect)
        {
            EditorUtility.SetDirty(_state.ActiveClip);
            _curveCache.Clear();
            _state.SelectedKeyframes.Clear();
            if (reselect != null) _state.SelectedKeyframes.AddRange(reselect);
            _rebuild();
        }

        private void RefreshKeyframeSelectionVisual()
        {
            for (int i = 0; i < _keyColumns.Count; i++)
            {
                var col = _keyColumns[i];
                StyleDiamond(col.Element, col.RowColor, AnyMemberSelected(col.Members), col.IsSummary);
            }
        }

        private bool AnyMemberSelected(List<KeyframeRef> members)
        {
            for (int i = 0; i < members.Count; i++)
                if (_state.SelectedKeyframes.Contains(members[i])) return true;
            return false;
        }

        private bool AllMembersSelected(List<KeyframeRef> members)
        {
            for (int i = 0; i < members.Count; i++)
                if (!_state.SelectedKeyframes.Contains(members[i])) return false;
            return members.Count > 0;
        }

        private void SelectKeyColumn(List<KeyframeRef> members, bool additive)
        {
            if (!additive) _state.SelectedKeyframes.Clear();
            _state.ClearSelectionExceptKeyframes();
            for (int i = 0; i < members.Count; i++)
                if (!_state.SelectedKeyframes.Contains(members[i])) _state.SelectedKeyframes.Add(members[i]);
        }

        private void ToggleKeyColumn(List<KeyframeRef> members)
        {
            bool allSelected = AllMembersSelected(members);
            for (int i = 0; i < members.Count; i++)
            {
                if (allSelected) _state.SelectedKeyframes.Remove(members[i]);
                else if (!_state.SelectedKeyframes.Contains(members[i])) _state.SelectedKeyframes.Add(members[i]);
            }
        }

        private static int FindKeyIndex(Keyframe[] keys, float time)
        {
            for (int i = 0; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].time - time) < TimeEpsilon) return i;
            return -1;
        }

        private string KeyTooltip(List<KeyframeRef> members, float time)
        {
            if (members.Count == 1)
            {
                var binding = _curveCache.Bindings[members[0].Binding];
                var curve = _curveCache.Curves[members[0].Binding];
                int idx = curve != null ? FindKeyIndex(curve.keys, time) : -1;
                string prop = binding.propertyName;
                float value = idx >= 0 ? curve[idx].value : 0f;
                return $"{prop}\n{value:F3} @ {time:F3}s";
            }
            return $"{members.Count} keys @ {time:F3}s";
        }
    }
}
#endif
