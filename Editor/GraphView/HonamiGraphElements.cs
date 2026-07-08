using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiGroup : Group
    {
        private System.Action _onChanged;

        public HonamiGroup(System.Action onChanged = null)
        {
            capabilities |= Capabilities.Resizable;
            _onChanged = onChanged;
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            if (userData is HonamiGroupData data)
            {
                data.position = newPos.position;
                data.size = newPos.size;
                _onChanged?.Invoke();
            }
        }

        public override void OnSelected()
        {
            base.OnSelected();
            HonamiGraphAccent.SetGroupSelected(this, true);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            HonamiGraphAccent.SetGroupSelected(this, false);
        }
    }

    public sealed class HonamiStickyNote : GraphElement
    {
        private System.Action _onChanged;
        private Vector2 _preCollapseSize;
        private Label _titleLabel;
        private TextField _titleField;
        private Label _contentsLabel;
        private TextField _contentsField;

        public new string title
        {
            get => _titleLabel.text;
            set { _titleLabel.text = value; _titleField.value = value; }
        }

        public string contents
        {
            get => _contentsLabel.text;
            set { _contentsLabel.text = value; _contentsField.value = value; }
        }

        public HonamiStickyNote(System.Action onChanged = null)
        {
            _onChanged = onChanged;
            capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Ascendable | Capabilities.Copiable;

            this.AddToClassList("honami-sticky-note");

            var header = new VisualElement { name = "header" };

            var closeBtn = new VisualElement { name = "close-btn" };
            closeBtn.AddManipulator(new Clickable(() =>
            {
                var graph = this.GetFirstAncestorOfType<HonamiGraphView>();
                if (graph != null) graph.DeleteElements(new[] { this });
            }));
            header.Add(closeBtn);

            _titleLabel = new Label { name = "title-label" };
            _titleField = new TextField { name = "title-field" };
            _titleField.style.display = DisplayStyle.None;

            _titleLabel.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 2)
                {
                    _titleLabel.style.display = DisplayStyle.None;
                    _titleField.style.display = DisplayStyle.Flex;
                    _titleField.value = _titleLabel.text;
                    _titleField.schedule.Execute(() => _titleField.Q("unity-text-input")?.Focus());
                }
            });

            _titleField.RegisterCallback<FocusOutEvent>(e =>
            {
                _titleField.style.display = DisplayStyle.None;
                _titleLabel.style.display = DisplayStyle.Flex;
            });

            _titleField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Escape)
                {
                    _titleField.style.display = DisplayStyle.None;
                    _titleLabel.style.display = DisplayStyle.Flex;
                }
            });

            _titleField.RegisterValueChangedCallback(e =>
            {
                _titleLabel.text = e.newValue;
                if (userData is HonamiStickyNoteData data)
                {
                    data.title = e.newValue;
                    _onChanged?.Invoke();
                }
            });
            header.Add(_titleLabel);
            header.Add(_titleField);

            var collapseBtn = new VisualElement { name = "collapse-btn" };
            collapseBtn.AddManipulator(new Clickable(() =>
            {
                bool isCollapsed = this.ClassListContains("collapsed");
                if (!isCollapsed)
                {
                    this.AddToClassList("collapsed");
                    _preCollapseSize = this.GetPosition().size;

                    Rect r = this.GetPosition();
                    r.height = 22;
                    this.SetPosition(r);
                }
                else
                {
                    this.RemoveFromClassList("collapsed");

                    Rect r = this.GetPosition();
                    r.height = _preCollapseSize.y > 0 ? _preCollapseSize.y : 150;
                    this.SetPosition(r);
                }
            }));
            header.Add(collapseBtn);

            this.Add(header);

            var contentContainer = new VisualElement { name = "contents-container" };

            _contentsLabel = new Label { name = "contents-label" };
            _contentsField = new TextField { name = "contents-field", multiline = true };
            _contentsField.style.display = DisplayStyle.None;

            contentContainer.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.clickCount == 2)
                {
                    _contentsLabel.style.display = DisplayStyle.None;
                    _contentsField.style.display = DisplayStyle.Flex;
                    _contentsField.value = _contentsLabel.text;
                    _contentsField.schedule.Execute(() => _contentsField.Q("unity-text-input")?.Focus());
                }
            });

            _contentsField.RegisterCallback<FocusOutEvent>(e =>
            {
                _contentsField.style.display = DisplayStyle.None;
                _contentsLabel.style.display = DisplayStyle.Flex;
            });

            _contentsField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    _contentsField.style.display = DisplayStyle.None;
                    _contentsLabel.style.display = DisplayStyle.Flex;
                }
            });

            _contentsField.RegisterValueChangedCallback(e =>
            {
                _contentsLabel.text = e.newValue;
                if (userData is HonamiStickyNoteData data)
                {
                    data.contents = e.newValue;
                    _onChanged?.Invoke();
                }
            });
            contentContainer.Add(_contentsLabel);
            contentContainer.Add(_contentsField);

            this.Add(contentContainer);

            string[] dirs = { "top", "bottom", "left", "right", "top-left", "top-right", "bottom-left", "bottom-right" };
            foreach (var d in dirs)
            {
                var resizer = new VisualElement();
                resizer.AddToClassList("sticky-resizer");
                resizer.AddToClassList(d);
                this.Add(resizer);

                var cName = d.Replace("-", "");
                var dirEnum = (ResizeDirection)System.Enum.Parse(typeof(ResizeDirection), cName, true);
                resizer.AddManipulator(new EdgeResizer(dirEnum, this));
            }
        }

        public override void SetPosition(Rect newPos)
        {
            if (this.ClassListContains("collapsed"))
            {
                newPos.height = 22;
                base.SetPosition(newPos);
                if (userData is HonamiStickyNoteData data)
                {
                    data.position = newPos.position;
                    _onChanged?.Invoke();
                }
            }
            else
            {
                base.SetPosition(newPos);
                if (userData is HonamiStickyNoteData data)
                {
                    data.position = newPos.position;
                    data.size = newPos.size;
                    _onChanged?.Invoke();
                }
            }
        }

        public override void OnSelected()
        {
            base.OnSelected();
            this.BringToFront();
        }
    }

    public enum ResizeDirection { Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight }

    public sealed class EdgeResizer : PointerManipulator
    {
        private ResizeDirection _dir;
        private bool _active;
        private Vector2 _startMouse;
        private Rect _startRect;
        private GraphElement _target;

        public EdgeResizer(ResizeDirection dir, GraphElement target)
        {
            _dir = dir;
            _target = target;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent e)
        {
            if (_active || e.button != 0) return;
            _active = true;
            target.CapturePointer(e.pointerId);
            _startMouse = e.position;
            _startRect = _target.GetPosition();
            e.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent e)
        {
            if (!_active) return;

            var startWorld = target.LocalToWorld(_startMouse);
            var endWorld = target.LocalToWorld(e.position);
            var delta = _target.hierarchy.parent.WorldToLocal(endWorld) - _target.hierarchy.parent.WorldToLocal(startWorld);

            Rect r = _startRect;

            if (_dir == ResizeDirection.Left || _dir == ResizeDirection.TopLeft || _dir == ResizeDirection.BottomLeft)
            { r.xMin += delta.x; }
            if (_dir == ResizeDirection.Right || _dir == ResizeDirection.TopRight || _dir == ResizeDirection.BottomRight)
            { r.xMax += delta.x; }
            if (_dir == ResizeDirection.Top || _dir == ResizeDirection.TopLeft || _dir == ResizeDirection.TopRight)
            { r.yMin += delta.y; }
            if (_dir == ResizeDirection.Bottom || _dir == ResizeDirection.BottomLeft || _dir == ResizeDirection.BottomRight)
            { r.yMax += delta.y; }

            if (r.width < 100) { if (_dir.ToString().Contains("Left")) r.xMin = r.xMax - 100; else r.xMax = r.xMin + 100; }
            if (r.height < 50) { if (_dir.ToString().Contains("Top")) r.yMin = r.yMax - 50; else r.yMax = r.yMin + 50; }

            _target.SetPosition(r);
            e.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent e)
        {
            if (!_active || target.HasPointerCapture(e.pointerId) == false) return;
            _active = false;
            target.ReleasePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent e) { _active = false; }
    }
}
