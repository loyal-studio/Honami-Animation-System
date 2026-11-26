using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiLinkedAnimatorLeftPanel
    {
        public VisualElement Root { get; }

        private readonly HonamiGraphWindow _window;
        private VisualElement _eventsContent;
        private readonly List<VisualElement> _eventRows = new();

        public HonamiLinkedAnimatorLeftPanel(HonamiGraphWindow window)
        {
            _window = window;
            Root = BuildShell();
        }

        private VisualElement BuildShell()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f);
            header.style.height = 26;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0, 0, 0, 0.3f);

            var title = new Label("Event Triggers");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = HonamiGraphStyles.SubTitleClr;
            title.style.flexGrow = 1;
            header.Add(title);

            var addBtn = HonamiGraphStyles.SmallButton("+", () => AddEvent());
            addBtn.tooltip = "Add new event trigger point";
            addBtn.style.marginRight = 4;
            header.Add(addBtn);

            root.Add(header);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var pad = new VisualElement();
            pad.Padding(6);
            scroll.Add(pad);

            _eventsContent = new VisualElement();
            pad.Add(_eventsContent);

            root.Add(scroll);
            return root;
        }

        public void Rebuild()
        {
            _eventsContent.Clear();
            _eventRows.Clear();

            var graph = _window.LinkedGraph;
            if (graph == null) return;

            for (int i = 0; i < graph.events.Count; i++)
            {
                var evt = graph.events[i];
                if (evt == null) continue;

                var row = CreateEventRow(evt, i);
                _eventsContent.Add(row);
                _eventRows.Add(row);
            }

            if (graph.events.Count == 0)
            {
                var empty = new Label("No triggers defined. Click + to add one.");
                empty.style.color = HonamiGraphStyles.GreyText;
                empty.style.fontSize = 10;
                empty.style.marginTop = 12;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _eventsContent.Add(empty);
            }
        }

        private VisualElement CreateEventRow(HonamiLinkedAnimatorEvent evt, int index)
        {
            var row = HonamiGraphStyles.ListBox();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = row.style.paddingBottom = 2;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 4;
            row.style.marginBottom = 3;

            row.RegisterCallback<MouseEnterEvent>(_ =>
                row.style.backgroundColor = HonamiGraphStyles.Accent);
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                row.style.backgroundColor = HonamiGraphStyles.ListBoxBg);

            row.RegisterCallback<ClickEvent>(clickEvt =>
            {
                if (clickEvt.target != row) return;

                var graphView = FindGraphView();
                if (graphView != null)
                {
                    var node = graphView.graphElements.OfType<HonamiLinkedAnimatorEventNode>()
                        .FirstOrDefault(n => n.BrainEvent == evt);
                    if (node != null)
                    {
                        graphView.ClearSelection();
                        graphView.AddToSelection(node);
                        graphView.FrameSelection();
                    }
                }
            });

            var nameField = new TextField();
            nameField.value = evt.eventName;
            nameField.style.flexGrow = 1;
            nameField.style.fontSize = 11;
            nameField.style.minWidth = 50;
            nameField.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            nameField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(evt, "Rename Brain Event");
                evt.eventName = e.newValue;
                EditorUtility.SetDirty(evt);

                var graphView = FindGraphView();
                graphView?.UpdateLinkedAnimatorEventUI(evt);
            });
            row.Add(nameField);

            var deleteBtn = HonamiGraphStyles.SmallButton(HonamiEditorSymbols.Remove, () => DeleteEvent(evt, index));
            deleteBtn.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            deleteBtn.tooltip = "Remove this trigger";
            row.Add(deleteBtn);

            return row;
        }

        private void AddEvent()
        {
            var graphView = FindGraphView();
            if (graphView == null) return;

            var pos = graphView.contentViewContainer.WorldToLocal(new Vector2(100, 100));
            graphView.CreateBrainEvent(pos);
            Rebuild();
        }

        private void DeleteEvent(HonamiLinkedAnimatorEvent evt, int index)
        {
            if (evt == null) return;

            if (!EditorUtility.DisplayDialog("Delete Trigger",
                $"Delete event trigger point '{evt.eventName}'?", "Delete", "Cancel")) return;

            var graphView = FindGraphView();
            if (graphView != null)
            {
                var node = graphView.graphElements.OfType<HonamiLinkedAnimatorEventNode>()
                    .FirstOrDefault(n => n.BrainEvent == evt);
                if (node != null)
                {
                    graphView.DeleteBrainEvent(node);
                }
            }

            Rebuild();
        }

        private HonamiLinkedAnimatorGraphView FindGraphView()
        {
            return _window.rootVisualElement.Q<HonamiLinkedAnimatorGraphView>();
        }
    }
}

