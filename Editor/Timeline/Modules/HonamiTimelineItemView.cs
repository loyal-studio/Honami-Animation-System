using System;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal interface ITimelineItemViewHost
    {
        float PixelsPerSecond { get; }
        float SnapItemTime(float time, HonamiTimelineItem ignore, bool shiftHeld);
        void ClearSnap();
        void SetHandleDragging(bool dragging);
        float TimeAtWorldX(float worldX);
        bool IsLocked(HonamiTimelineModule module);
        void SelectItem(HonamiTimelineItem item, bool additive);
        void BringItemToFront(HonamiTimelineItem item);
    }

    public readonly struct HonamiTimelineHandleDrag
    {
        public readonly Vector2 LocalPosition;
        public readonly Vector2 StartLocalPosition;
        public readonly float PointerTime;
        public readonly bool ShiftHeld;

        public HonamiTimelineHandleDrag(Vector2 localPosition, Vector2 startLocalPosition, float pointerTime, bool shiftHeld)
        {
            LocalPosition = localPosition;
            StartLocalPosition = startLocalPosition;
            PointerTime = pointerTime;
            ShiftHeld = shiftHeld;
        }

        public Vector2 Delta => LocalPosition - StartLocalPosition;
    }

    public sealed class HonamiTimelineItemView
    {
        private const float TrimHandleWidth = 7f;
        private const float BlendHandleSize = 9f;
        private static readonly Color TrimHandleColor = new(1f, 1f, 1f, 0.10f);
        private static readonly Color TrimHandleHot = new(1f, 1f, 1f, 0.38f);
        private static readonly Color BlendRampFill = new(0f, 0f, 0f, 0.32f);
        private static readonly Color BlendRampLine = new(1f, 1f, 1f, 0.55f);
        private static readonly Color BlendHandleColor = new(1f, 1f, 1f, 0.85f);
        private static readonly Color CycleLine = new(0f, 0f, 0f, 0.45f);

        private readonly ITimelineItemViewHost _host;

        internal HonamiTimelineItemView(ITimelineItemViewHost host, HonamiTimelineEditContext context, HonamiTimelineItem item, VisualElement element)
        {
            _host = host;
            Context = context;
            Item = item;
            Element = element;
        }

        public HonamiTimelineEditContext Context { get; }
        public HonamiTimelineModule Module => Context.Module;
        public HonamiTimelineItem Item { get; }
        public VisualElement Element { get; }
        public float PixelsPerSecond => _host.PixelsPerSecond;
        public bool IsLocked => _host.IsLocked(Module);

        public float TimeToLocalX(float time) => (time - Item.time) * PixelsPerSecond;
        public float LocalXToTime(float localX) => Item.time + localX / PixelsPerSecond;

        public float SnapTime(float time, bool shiftHeld) =>
            _host.SnapItemTime(time + Context.TimeOffset, Item, shiftHeld) - Context.TimeOffset;

        public void Modify(string undoName, Action change)
        {
            if (IsLocked) return;
            Context.RecordUndo(undoName);
            change();
            Context.MarkChanged();
        }

        public void Refresh()
        {
            float duration = Mathf.Max(Item.Duration, 0.1f);
            if (!Item.IsInstant)
            {
                Element.style.left = (Context.TimeOffset + Item.time) * PixelsPerSecond;
                Element.style.width = Mathf.Max(1f, duration * PixelsPerSecond);
            }
            RepaintTree(Element);
        }

        // One undo step per drag: the group opened on press is collapsed on release.
        public void AddHandle(VisualElement handle, string undoName, Action<HonamiTimelineHandleDrag> onDrag, Action onEnd = null)
        {
            bool dragging = false;
            int undoGroup = -1;
            Vector2 start = default;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || IsLocked) return;
                dragging = true;
                _host.SelectItem(Item, evt.shiftKey || evt.ctrlKey || evt.commandKey);
                _host.BringItemToFront(Item);
                start = Element.WorldToLocal(evt.position);
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                _host.SetHandleDragging(true);
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || !handle.HasPointerCapture(evt.pointerId)) return;
                Context.RecordUndo(undoName);
                var local = (Vector2)Element.WorldToLocal(evt.position);
                onDrag(new HonamiTimelineHandleDrag(local, start, _host.TimeAtWorldX(evt.position.x) - Context.TimeOffset, evt.shiftKey));
                EditorUtility.SetDirty(Context.ContainerObject);
                Context.Container.MarkModulesChanged();
                Refresh();
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging) return;
                dragging = false;
                if (handle.HasPointerCapture(evt.pointerId)) handle.ReleasePointer(evt.pointerId);
                if (undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
                _host.ClearSnap();
                _host.SetHandleDragging(false);
                onEnd?.Invoke();
                Context.MarkChanged();
                evt.StopPropagation();
            });
        }

        public void AddTrimHandles()
        {
            if (Item.IsInstant || !Item.CanResize || IsLocked) return;

            var startHandle = CreateTrimHandle(true);
            AddHandle(startHandle, "Trim Timeline Item", drag =>
                Item.TrimStart(SnapTime(drag.PointerTime, drag.ShiftHeld)));

            var endHandle = CreateTrimHandle(false);
            AddHandle(endHandle, "Trim Timeline Item", drag =>
            {
                float end = Mathf.Max(SnapTime(drag.PointerTime, drag.ShiftHeld), Item.time + HonamiTimelineItem.MinDuration);
                Item.SetDuration(end - Item.time);
            });

            Element.Add(startHandle);
            Element.Add(endHandle);
        }

        public void AddBlendHandles(bool fadeOutUnderOverlap = false)
        {
            if (Item is not IHonamiTimelineBlendItem blend) return;

            var ramps = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };
            ramps.generateVisualContent += mesh => DrawRamps(mesh, ramps.contentRect, fadeOutUnderOverlap);
            Element.Add(ramps);

            if (IsLocked) return;

            var inHandle = CreateBlendHandle("Drag to set the blend in");
            var outHandle = CreateBlendHandle("Drag to set the blend out");
            PlaceBlendHandles(inHandle, outHandle, blend);
            AddHandle(inHandle, "Blend In", drag =>
            {
                float time = SnapTime(drag.PointerTime, drag.ShiftHeld);
                blend.BlendIn = Mathf.Clamp(time - Item.time, 0f, Item.Duration - blend.BlendOut);
                PlaceBlendHandles(inHandle, outHandle, blend);
                ramps.MarkDirtyRepaint();
            });
            AddHandle(outHandle, "Blend Out", drag =>
            {
                float time = SnapTime(drag.PointerTime, drag.ShiftHeld);
                blend.BlendOut = Mathf.Clamp(Item.EndTime - time, 0f, Item.Duration - blend.BlendIn);
                PlaceBlendHandles(inHandle, outHandle, blend);
                ramps.MarkDirtyRepaint();
            });
            Element.Add(inHandle);
            Element.Add(outHandle);

            Element.RegisterCallback<PointerEnterEvent>(_ => inHandle.style.opacity = outHandle.style.opacity = 1f);
            Element.RegisterCallback<PointerLeaveEvent>(_ => inHandle.style.opacity = outHandle.style.opacity = 0f);
        }

        public void AddCycleMarkers(float firstBoundary, float period)
        {
            if (period <= 0f) return;
            var markers = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
            };
            markers.generateVisualContent += mesh =>
            {
                var rect = markers.contentRect;
                float pixels = period * PixelsPerSecond;
                if (pixels < 4f) return;
                var painter = mesh.painter2D;
                painter.strokeColor = CycleLine;
                painter.lineWidth = 1f;
                for (float x = firstBoundary * PixelsPerSecond; x < rect.width; x += pixels)
                {
                    if (x <= 1f) continue;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, rect.yMin + 3f));
                    painter.LineTo(new Vector2(x, rect.yMax - 3f));
                    painter.Stroke();
                }
            };
            Element.Add(markers);
        }

        private void DrawRamps(MeshGenerationContext mesh, Rect rect, bool fadeOutUnderOverlap)
        {
            if (rect.width < 2f || rect.height < 2f) return;
            float blendIn = HonamiTimelineBlend.GetBlendIn(Module, Item) * PixelsPerSecond;
            float blendOut = HonamiTimelineBlend.GetBlendOut(Module, Item, fadeOutUnderOverlap) * PixelsPerSecond;
            var painter = mesh.painter2D;

            if (blendIn > 1f)
                DrawRamp(painter, rect, rect.xMin, Mathf.Min(rect.xMax, rect.xMin + blendIn), HonamiTimelineBlend.GetBlendInCurve(Item), true);
            if (blendOut > 1f)
                DrawRamp(painter, rect, Mathf.Max(rect.xMin, rect.xMax - blendOut), rect.xMax, HonamiTimelineBlend.GetBlendOutCurve(Item), false);
        }

        private static void DrawRamp(Painter2D painter, Rect rect, float x0, float x1, in HonamiBlendCurve curve, bool rising)
        {
            const int steps = 24;
            painter.fillColor = BlendRampFill;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rising ? x0 : x1, rect.yMin));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float weight = rising
                    ? HonamiTimelineBlend.BlendInWeight(curve, t)
                    : HonamiTimelineBlend.BlendOutWeight(curve, 1f - t);
                painter.LineTo(new Vector2(Mathf.Lerp(x0, x1, t), Mathf.Lerp(rect.yMax, rect.yMin, weight)));
            }
            painter.LineTo(new Vector2(rising ? x0 : x1, rect.yMin));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = BlendRampLine;
            painter.lineWidth = 1f;
            painter.BeginPath();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float weight = rising
                    ? HonamiTimelineBlend.BlendInWeight(curve, t)
                    : HonamiTimelineBlend.BlendOutWeight(curve, 1f - t);
                var point = new Vector2(Mathf.Lerp(x0, x1, t), Mathf.Lerp(rect.yMax, rect.yMin, weight));
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.Stroke();
        }

        private void PlaceBlendHandles(VisualElement inHandle, VisualElement outHandle, IHonamiTimelineBlendItem blend)
        {
            inHandle.style.left = Mathf.Max(0f, blend.BlendIn * PixelsPerSecond - BlendHandleSize * 0.5f);
            outHandle.style.right = Mathf.Max(0f, blend.BlendOut * PixelsPerSecond - BlendHandleSize * 0.5f);
        }

        private static VisualElement CreateBlendHandle(string tooltip)
        {
            var handle = new VisualElement
            {
                tooltip = tooltip,
                style =
                {
                    position = Position.Absolute,
                    top = 1,
                    width = BlendHandleSize,
                    height = BlendHandleSize,
                    backgroundColor = BlendHandleColor,
                    opacity = 0f
                }
            };
            handle.Radius(BlendHandleSize * 0.5f);
            handle.AddManipulator(new CursorManipulator(MouseCursor.SlideArrow));
            return handle;
        }

        private static VisualElement CreateTrimHandle(bool atStart)
        {
            var handle = new VisualElement
            {
                tooltip = atStart ? "Drag to trim the start" : "Drag to trim the end",
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    bottom = 0,
                    width = TrimHandleWidth,
                    backgroundColor = TrimHandleColor
                }
            };
            if (atStart) handle.style.left = 0;
            else handle.style.right = 0;

            handle.RegisterCallback<PointerEnterEvent>(_ => handle.style.backgroundColor = TrimHandleHot);
            handle.RegisterCallback<PointerLeaveEvent>(_ => handle.style.backgroundColor = TrimHandleColor);
            handle.AddManipulator(new CursorManipulator(MouseCursor.ResizeHorizontal));
            return handle;
        }

        private static void RepaintTree(VisualElement element)
        {
            element.MarkDirtyRepaint();
            foreach (var child in element.Children())
                RepaintTree(child);
        }

        internal sealed class CursorManipulator : Manipulator
        {
            // The editor maps a texture-less Cursor's internal id to MouseCursor, the same path USS cursor keywords take.
            private static readonly System.Reflection.PropertyInfo DefaultCursorId = typeof(UnityEngine.UIElements.Cursor).GetProperty(
                "defaultCursorId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            private static readonly System.Collections.Generic.Dictionary<MouseCursor, UnityEngine.UIElements.Cursor> Cursors = new();

            private readonly MouseCursor _cursor;

            public CursorManipulator(MouseCursor cursor) => _cursor = cursor;

            protected override void RegisterCallbacksOnTarget()
            {
                if (DefaultCursorId != null && DefaultCursorId.CanWrite)
                {
                    if (!Cursors.TryGetValue(_cursor, out var cursor))
                    {
                        object boxed = new UnityEngine.UIElements.Cursor();
                        DefaultCursorId.SetValue(boxed, (int)_cursor);
                        cursor = (UnityEngine.UIElements.Cursor)boxed;
                        Cursors[_cursor] = cursor;
                    }
                    target.style.cursor = cursor;
                    return;
                }

                target.Add(new IMGUIContainer(() => EditorGUIUtility.AddCursorRect(new Rect(0, 0, target.layout.width, target.layout.height), _cursor))
                {
                    pickingMode = PickingMode.Ignore,
                    style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 }
                });
            }

            protected override void UnregisterCallbacksFromTarget() { }
        }
    }
}
