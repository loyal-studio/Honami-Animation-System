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
        private static bool IsInButton(IEventHandler target)
        {
            var ve = target as VisualElement;
            while (ve != null)
            {
                if (ve is Button) return true;
                ve = ve.parent;
            }
            return false;
        }

        private void AttachRowDrag(
            VisualElement hdr,
            VisualElement row,
            Label nameLabel,
            SerializedObject sc,
            SerializedProperty layersProp,
            int arrayIdx,
            int globalIdx,
            bool canDrag,
            bool isLockedBase)
        {
            hdr.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || IsInButton(evt.target)) return;

                if (evt.clickCount == 2)
                {
                    if (!isLockedBase)
                    {
                        evt.StopPropagation();
                        StartLayerRename(nameLabel, sc, layersProp, arrayIdx);
                    }
                    return;
                }

                if (!canDrag) return;
                hdr.CapturePointer(evt.pointerId);
                _dragActive = true;
                _dragVisual = false;
                _dragSourceLayer = globalIdx;
                _dragStartPos = evt.position;
                _lastDragPos = _dragStartPos;
                _dropGapIndex = -1;
            });

            hdr.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx || !hdr.HasPointerCapture(evt.pointerId)) return;
                _lastDragPos = evt.position;

                if (!_dragVisual)
                {
                    if ((_lastDragPos - _dragStartPos).sqrMagnitude < 16f) return;
                    BeginDragVisual(row, nameLabel.text);
                }

                UpdateDragVisual(_lastDragPos);
            });

            hdr.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx) return;
                if (hdr.HasPointerCapture(evt.pointerId)) hdr.ReleasePointer(evt.pointerId);

                bool dropped = _dragVisual;
                int gap = _dropGapIndex;
                int from = _dragSourceLayer;
                EndDragVisual();
                _dragActive = false;
                _dragSourceLayer = -1;
                _dropGapIndex = -1;

                if (dropped)
                {
                    _suppressNextLayerClick = true;
                    CommitDrop(from, gap);
                }
            });

            hdr.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (!_dragActive || _dragSourceLayer != globalIdx) return;
                EndDragVisual();
                _dragActive = false;
                _dragSourceLayer = -1;
                _dropGapIndex = -1;
            });
        }

        private void BeginDragVisual(VisualElement sourceRow, string layerName)
        {
            _dragVisual = true;
            _dragSourceRow = sourceRow;
            sourceRow.style.opacity = 0.35f;

            _dropLine = new VisualElement();
            _dropLine.pickingMode = PickingMode.Ignore;
            _dropLine.style.position = Position.Absolute;
            _dropLine.style.left = 4;
            _dropLine.style.right = 4;
            _dropLine.style.height = 3;
            _dropLine.style.backgroundColor = HonamiGraphStyles.Accent;
            _dropLine.Radius(2);
            _dropLine.style.display = DisplayStyle.None;
            _layersContent.Add(_dropLine);

            _dragGhost = new Label(layerName);
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.position = Position.Absolute;
            _dragGhost.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 0.95f);
            _dragGhost.style.color = Color.white;
            _dragGhost.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dragGhost.style.fontSize = 11;
            _dragGhost.Padding(4, 8);
            _dragGhost.Radius(4);
            _dragGhost.Border(1, HonamiGraphStyles.Accent);
            Root.Add(_dragGhost);

            _autoScrollItem = Root.schedule.Execute(AutoScrollTick).Every(16);
        }

        private void UpdateDragVisual(Vector2 panelPos)
        {
            if (!_dragVisual) return;

            if (_dragGhost != null)
            {
                var local = Root.WorldToLocal(panelPos);
                _dragGhost.style.left = local.x + 10;
                _dragGhost.style.top = local.y - 9;
            }

            int sourceDisplay = -1;
            for (int i = 0; i < _layerRows.Count; i++)
            {
                if (_layerRows[i].layerIndex == _dragSourceLayer)
                {
                    sourceDisplay = i;
                    break;
                }
            }

            int gap = -1;
            float lineY = 0f;
            for (int i = 0; i < _layerRows.Count; i++)
            {
                var rowBound = _layerRows[i].row.worldBound;
                if (panelPos.y < rowBound.center.y)
                {
                    gap = i;
                    lineY = _layerRows[i].row.layout.yMin - 2.5f;
                    break;
                }
            }
            if (gap < 0 && _layerRows.Count > 0)
            {
                gap = _layerRows.Count;
                lineY = _layerRows[_layerRows.Count - 1].row.layout.yMax + 0.5f;
            }

            if (gap == sourceDisplay || gap == sourceDisplay + 1) gap = -1;

            _dropGapIndex = gap;
            if (_dropLine != null)
            {
                if (gap >= 0)
                {
                    _dropLine.style.display = DisplayStyle.Flex;
                    _dropLine.style.top = lineY;
                }
                else
                {
                    _dropLine.style.display = DisplayStyle.None;
                }
            }
        }

        private void EndDragVisual()
        {
            _dragVisual = false;

            if (_dragSourceRow != null)
            {
                _dragSourceRow.style.opacity = 1f;
                _dragSourceRow = null;
            }

            _dragGhost?.RemoveFromHierarchy();
            _dragGhost = null;
            _dropLine?.RemoveFromHierarchy();
            _dropLine = null;
            _autoScrollItem?.Pause();
            _autoScrollItem = null;
        }

        private void AutoScrollTick()
        {
            if (!_dragVisual || _mainScroll == null) return;

            var bound = _mainScroll.worldBound;
            float speed = 0f;
            const float edge = 28f;

            if (_lastDragPos.y < bound.yMin + edge)
                speed = -Mathf.Lerp(2f, 12f, Mathf.Clamp01((bound.yMin + edge - _lastDragPos.y) / edge));
            else if (_lastDragPos.y > bound.yMax - edge)
                speed = Mathf.Lerp(2f, 12f, Mathf.Clamp01((_lastDragPos.y - (bound.yMax - edge)) / edge));

            if (speed != 0f)
            {
                var offset = _mainScroll.scrollOffset;
                _mainScroll.scrollOffset = new Vector2(offset.x, Mathf.Max(0f, offset.y + speed));
                UpdateDragVisual(_lastDragPos);
            }
        }

        private void CommitDrop(int fromGlobal, int gap)
        {
            if (gap < 0 || _window.Controller == null) return;

            int count = _window.Controller.layers.Count;
            int to;
            if (gap <= 0)
            {
                to = 0;
            }
            else
            {
                int above = gap - 1 < _layerRows.Count ? _layerRows[gap - 1].layerIndex : fromGlobal;
                to = fromGlobal > above ? above + 1 : above;
            }

            to = Mathf.Clamp(to, 0, count - 1);
            if (to == fromGlobal) return;
            _window.MoveLayer(fromGlobal, to);
        }
    }
}
