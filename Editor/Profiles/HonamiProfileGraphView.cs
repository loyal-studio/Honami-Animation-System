using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiProfileGraphView : GraphView
    {
        private HonamiControllerProfileGraph _graph;
        private EditorWindow _window;
        private VisualElement _treeRoot;
        private TextField _searchField;
        private VisualElement _breadcrumbContainer;
        private string _searchQuery = string.Empty;
        private HonamiProfileState _selectedState;

        public HonamiControllerProfileGraph Graph => _graph;
        public HonamiProfileState SelectedState => _selectedState;
        public Action<HonamiProfileState> OnStateSelected;
        public Action OnGraphChanged;

        public HonamiProfileGraphView()
        {
            Insert(0, new HonamiGridBackground());

            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());

            var styleSheet = Resources.Load<StyleSheet>("HonamiGraphStyle");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            _treeRoot = new VisualElement { name = "tree-root" };
            _treeRoot.style.paddingLeft = 50;
            _treeRoot.style.paddingTop = 100;
            contentViewContainer.Add(_treeRoot);

            GenerateUI();

            SetGridVisible(HonamiGraphSettings.ShowGrid);

            RegisterCallback<DragUpdatedEvent>(OnCanvasDragUpdated);
            RegisterCallback<DragPerformEvent>(OnCanvasDragPerform);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<MouseDownEvent>(OnCanvasMouseDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsEditingText()) return;

            if (evt.keyCode == KeyCode.Delete && _selectedState != null)
            {
                DeleteState(_selectedState);
            }
        }

        private bool IsEditingText()
        {
            var focused = panel?.focusController?.focusedElement as VisualElement;
            if (focused == null) return false;
            return focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null;
        }

        private void OnCanvasMouseDown(MouseDownEvent evt)
        {
            if (evt.target == this || evt.target == contentViewContainer || evt.target == _treeRoot)
            {
                SelectState(null);
            }
        }

        public void SetGridVisible(bool visible)
        {
            var grid = this.Q<HonamiGridBackground>();
            if (grid != null)
            {
                grid.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnCanvasDragUpdated(DragUpdatedEvent e)
        {
            var dragObj = DragAndDrop.objectReferences.FirstOrDefault() as HonamiProfileState;
            if (dragObj != null && !string.IsNullOrEmpty(dragObj.parentGuid))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
        }

        private void OnCanvasDragPerform(DragPerformEvent e)
        {
            var dragObj = DragAndDrop.objectReferences.FirstOrDefault() as HonamiProfileState;
            if (dragObj != null && !string.IsNullOrEmpty(dragObj.parentGuid))
            {
                DragAndDrop.AcceptDrag();
                ReparentState(dragObj, null);
            }
        }

        private void GenerateUI()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = toolbar.style.paddingRight = 10;
            toolbar.style.paddingTop = toolbar.style.paddingBottom = 8;
            toolbar.style.backgroundColor = HonamiGraphStyles.PanelBg;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = HonamiGraphStyles.ListBoxBorder;
            toolbar.style.position = Position.Absolute;
            toolbar.style.top = 0;
            toolbar.style.left = 0;
            toolbar.style.right = 0;
            toolbar.pickingMode = PickingMode.Position;
            Add(toolbar);

            var searchIcon = new Image { image = EditorGUIUtility.IconContent("Search Icon").image };
            searchIcon.style.width = searchIcon.style.height = 14;
            searchIcon.style.marginRight = 6;
            searchIcon.tintColor = HonamiGraphStyles.GreyText;
            toolbar.Add(searchIcon);

            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            _searchField.style.height = 22;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue.ToLower();
                RefreshTree();
            });
            toolbar.Add(_searchField);

            var addRootBtn = new Button(() => CreateState(null)) { text = "+ Add Root Profile" };
            addRootBtn.style.marginLeft = 10;
            addRootBtn.style.height = 22;
            addRootBtn.style.fontSize = 11;
            addRootBtn.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            addRootBtn.style.borderLeftWidth = addRootBtn.style.borderRightWidth =
            addRootBtn.style.borderTopWidth = addRootBtn.style.borderBottomWidth = 0;
            addRootBtn.style.color = HonamiGraphStyles.TitleClr;
            toolbar.Add(addRootBtn);

            _breadcrumbContainer = new VisualElement();
            _breadcrumbContainer.style.flexDirection = FlexDirection.Row;
            _breadcrumbContainer.style.position = Position.Absolute;
            _breadcrumbContainer.style.bottom = 12;
            _breadcrumbContainer.style.left = 12;
            _breadcrumbContainer.style.right = 12;
            _breadcrumbContainer.style.height = 28;
            _breadcrumbContainer.style.paddingLeft = 12;
            _breadcrumbContainer.style.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 0.9f);
            _breadcrumbContainer.style.borderTopLeftRadius = _breadcrumbContainer.style.borderTopRightRadius =
            _breadcrumbContainer.style.borderBottomLeftRadius = _breadcrumbContainer.style.borderBottomRightRadius = 6;
            _breadcrumbContainer.style.borderTopWidth = _breadcrumbContainer.style.borderBottomWidth =
            _breadcrumbContainer.style.borderLeftWidth = _breadcrumbContainer.style.borderRightWidth = 1;
            _breadcrumbContainer.style.borderTopColor = _breadcrumbContainer.style.borderBottomColor =
            _breadcrumbContainer.style.borderLeftColor = _breadcrumbContainer.style.borderRightColor = HonamiGraphStyles.ListBoxBorder;
            _breadcrumbContainer.style.alignItems = Align.Center;
            _breadcrumbContainer.pickingMode = PickingMode.Ignore;
            Add(_breadcrumbContainer);
        }

        public void Init(EditorWindow window)
        {
            _window = window;
        }

        public void PopulateView(HonamiControllerProfileGraph graph)
        {
            _graph = graph;
            RefreshTree();
        }

        private void RefreshTree()
        {
            _treeRoot.Clear();
            if (_graph == null) return;

            var statesByGuid = new Dictionary<string, HonamiProfileState>(_graph.states.Count);
            foreach (var s in _graph.states)
            {
                if (s != null && !string.IsNullOrEmpty(s.guid)) statesByGuid.TryAdd(s.guid, s);
            }

            var roots = _graph.states.Where(s => string.IsNullOrEmpty(s.parentGuid)).ToList();
            float currentY = 0;

            foreach (var root in roots)
            {
                currentY += RenderItem(root, _treeRoot, 0, currentY, statesByGuid);
            }

            UpdateBreadcrumbs();
        }

        private float RenderItem(HonamiProfileState state, VisualElement container, int depth, float yOffset, Dictionary<string, HonamiProfileState> statesByGuid)
        {
            if (state == null) return 0;

            bool matchesSearch = string.IsNullOrEmpty(_searchQuery) || state.stateName.ToLower().Contains(_searchQuery);
            bool hasMatchingChild = false;

            if (!matchesSearch)
            {
                foreach (var childGuid in state.childrenGuids)
                {
                    if (statesByGuid.TryGetValue(childGuid, out var child) && ChildMatchesSearch(child, statesByGuid))
                    {
                        hasMatchingChild = true;
                        break;
                    }
                }
            }

            if (!matchesSearch && !hasMatchingChild) return 0;

            var item = new HonamiProfileTreeItem(state, depth, this);
            item.style.position = Position.Absolute;
            item.style.left = depth * 32 + 20;
            item.style.top = yOffset;
            container.Add(item);
            item.SetSelected(_selectedState == state);

            float itemHeight = 44;
            float childrenHeight = 0;

            if (state.isExpanded || !string.IsNullOrEmpty(_searchQuery))
            {
                var children = state.childrenGuids
                    .Select(guid => statesByGuid.TryGetValue(guid, out var s) ? s : null)
                    .Where(s => s != null)
                    .ToList();

                float currentChildrenHeight = 0;
                float lastChildBranchY = 0;

                for (int i = 0; i < children.Count; i++)
                {
                    float childTop = yOffset + itemHeight + currentChildrenHeight;
                    float childHeight = RenderItem(children[i], container, depth + 1, childTop, statesByGuid);

                    if (childHeight > 0)
                    {
                        var branch = new VisualElement();
                        branch.style.position = Position.Absolute;
                        branch.style.left = depth * 32 + 32;
                        branch.style.top = childTop + (itemHeight / 2);
                        branch.style.width = 20;
                        branch.style.height = 1;
                        branch.style.backgroundColor = new Color(1, 1, 1, 0.1f);
                        container.Add(branch);
                        branch.SendToBack();

                        lastChildBranchY = currentChildrenHeight + (itemHeight / 2);
                        currentChildrenHeight += childHeight;
                    }
                }

                if (currentChildrenHeight > 0)
                {
                    var trunk = new VisualElement();
                    trunk.style.position = Position.Absolute;
                    trunk.style.left = depth * 32 + 32;
                    trunk.style.top = yOffset + itemHeight - 10;
                    trunk.style.width = 1;
                    trunk.style.height = lastChildBranchY + 10;
                    trunk.style.backgroundColor = new Color(1, 1, 1, 0.1f);
                    container.Add(trunk);
                    trunk.SendToBack();
                }

                childrenHeight = currentChildrenHeight;
            }

            return itemHeight + childrenHeight;
        }

        private bool ChildMatchesSearch(HonamiProfileState state, Dictionary<string, HonamiProfileState> statesByGuid)
        {
            if (state.stateName.ToLower().Contains(_searchQuery)) return true;
            foreach (var childGuid in state.childrenGuids)
            {
                if (statesByGuid.TryGetValue(childGuid, out var child) && ChildMatchesSearch(child, statesByGuid)) return true;
            }
            return false;
        }

        public void CreateState(HonamiProfileState parent)
        {
            if (_graph == null) return;

            Undo.RecordObject(_graph, "Create Profile State");

            var state = ScriptableObject.CreateInstance<HonamiProfileState>();
            state.name = "New Profile State";
            state.stateName = "New State";
            state.guid = Guid.NewGuid().ToString();

            if (parent != null)
            {
                Undo.RecordObject(parent, "Add Child Profile");
                state.parentGuid = parent.guid;
                parent.childrenGuids.Add(state.guid);
                EditorUtility.SetDirty(parent);
            }

            AssetDatabase.AddObjectToAsset(state, _graph);
            _graph.states.Add(state);

            EditorUtility.SetDirty(_graph);
            HonamiGraphView.DeferredSave();

            RefreshTree();
            SelectState(state);
            OnGraphChanged?.Invoke();
        }

        public void ReparentState(HonamiProfileState state, HonamiProfileState newParent)
        {
            if (state == null || state == newParent) return;

            var p = newParent;
            while (p != null)
            {
                if (p == state) return;
                p = _graph.states.FirstOrDefault(s => s.guid == p.parentGuid);
            }

            Undo.RecordObject(_graph, "Reparent Profile State");

            if (!string.IsNullOrEmpty(state.parentGuid))
            {
                var oldParent = _graph.states.FirstOrDefault(s => s.guid == state.parentGuid);
                if (oldParent != null)
                {
                    Undo.RecordObject(oldParent, "Remove Child Profile");
                    oldParent.childrenGuids.Remove(state.guid);
                    EditorUtility.SetDirty(oldParent);
                }
            }

            if (newParent != null)
            {
                Undo.RecordObject(newParent, "Add Child Profile");
                state.parentGuid = newParent.guid;
                newParent.childrenGuids.Add(state.guid);
                EditorUtility.SetDirty(newParent);
            }
            else
            {
                state.parentGuid = string.Empty;
            }

            Undo.RecordObject(state, "Change Parent");
            EditorUtility.SetDirty(state);
            HonamiGraphView.DeferredSave();

            RefreshTree();
            OnGraphChanged?.Invoke();
        }

        public void DeleteState(HonamiProfileState state)
        {
            if (_graph == null || state == null) return;

            Undo.RecordObject(_graph, "Delete Profile State");

            if (!string.IsNullOrEmpty(state.parentGuid))
            {
                var parent = _graph.states.FirstOrDefault(s => s.guid == state.parentGuid);
                if (parent != null)
                {
                    Undo.RecordObject(parent, "Remove Child Profile");
                    parent.childrenGuids.Remove(state.guid);
                    EditorUtility.SetDirty(parent);
                }
            }

            foreach (var childGuid in state.childrenGuids.ToList())
            {
                var child = _graph.states.FirstOrDefault(s => s.guid == childGuid);
                if (child != null) DeleteState(child);
            }

            _graph.states.Remove(state);
            if (_graph.defaultState == state) _graph.defaultState = null;

            Undo.DestroyObjectImmediate(state);
            EditorUtility.SetDirty(_graph);
            HonamiGraphView.DeferredSave();

            RefreshTree();
            OnGraphChanged?.Invoke();
        }

        public void SelectState(HonamiProfileState state)
        {
            _selectedState = state;
            foreach (var item in _treeRoot.Children().OfType<HonamiProfileTreeItem>())
            {
                item.SetSelected(item.State == state);
            }
            OnStateSelected?.Invoke(state);
            UpdateBreadcrumbs();

            if (state != null)
                Selection.activeObject = state;
        }

        private void UpdateBreadcrumbs()
        {
            _breadcrumbContainer.Clear();
            if (_selectedState == null) return;

            var path = new List<HonamiProfileState>();
            var current = _selectedState;
            while (current != null)
            {
                path.Insert(0, current);
                current = _graph.states.FirstOrDefault(s => s.guid == current.parentGuid);
            }

            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                {
                    var sep = new Label(">") { style = { color = HonamiGraphStyles.GreyText, marginLeft = 5, marginRight = 5, unityTextAlign = TextAnchor.MiddleCenter, fontSize = 10 } };
                    _breadcrumbContainer.Add(sep);
                }

                var step = new Label(path[i].stateName);
                step.style.color = (i == path.Count - 1) ? HonamiGraphStyles.Accent : Color.white;
                step.style.unityFontStyleAndWeight = (i == path.Count - 1) ? FontStyle.Bold : FontStyle.Normal;
                step.style.fontSize = 11;
                _breadcrumbContainer.Add(step);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add Root Profile", _ => CreateState(null));
        }
    }

    public sealed class HonamiProfileTreeItem : VisualElement
    {
        private HonamiProfileState _state;
        private HonamiProfileGraphView _view;
        private TextField _nameField;
        private VisualElement _bg;
        private VisualElement _accent;
        private VisualElement _dragIndicator;
        private Vector2 _dragStartPos;
        private bool _isMouseDown;

        public HonamiProfileState State => _state;

        public HonamiProfileTreeItem(HonamiProfileState state, int depth, HonamiProfileGraphView view)
        {
            _state = state;
            _view = view;

            style.height = 40;
            style.width = 340;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 4;

            _bg = new VisualElement();
            _bg.style.position = Position.Absolute;
            _bg.style.left = _bg.style.right = _bg.style.top = _bg.style.bottom = 0;
            _bg.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            _bg.style.borderTopLeftRadius = _bg.style.borderTopRightRadius =
            _bg.style.borderBottomLeftRadius = _bg.style.borderBottomRightRadius = 8;
            _bg.style.borderTopWidth = _bg.style.borderBottomWidth = _bg.style.borderRightWidth = 1;
            _bg.style.borderLeftWidth = 0;
            _bg.style.borderTopColor = _bg.style.borderBottomColor = _bg.style.borderRightColor = new Color(1, 1, 1, 0.05f);
            Add(_bg);

            _accent = new VisualElement();
            _accent.style.position = Position.Absolute;
            _accent.style.left = 0;
            _accent.style.top = 8;
            _accent.style.bottom = 8;
            _accent.style.width = 3;
            _accent.style.backgroundColor = new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.4f);
            _accent.style.borderTopRightRadius = _accent.style.borderBottomRightRadius = 2;
            Add(_accent);

            _dragIndicator = new VisualElement();
            _dragIndicator.style.position = Position.Absolute;
            _dragIndicator.style.left = _dragIndicator.style.right = 0;
            _dragIndicator.style.bottom = -2;
            _dragIndicator.style.height = 2;
            _dragIndicator.style.backgroundColor = HonamiGraphStyles.Accent;
            _dragIndicator.style.display = DisplayStyle.None;
            Add(_dragIndicator);

            var expContainer = new VisualElement();
            expContainer.style.width = 24;
            expContainer.style.height = 24;
            expContainer.style.marginLeft = 8;
            expContainer.style.justifyContent = Justify.Center;
            expContainer.style.alignItems = Align.Center;
            Add(expContainer);

            if (state.childrenGuids.Count > 0)
            {
                var expBtn = new Label(state.isExpanded ? "▼" : "▶");
                expBtn.style.fontSize = 9;
                expBtn.style.color = HonamiGraphStyles.GreyText;
                expBtn.RegisterCallback<MouseDownEvent>(e =>
                {
                    _state.isExpanded = !_state.isExpanded;
                    _view.PopulateView(_view.Graph);
                    e.StopPropagation();
                });
                expBtn.RegisterCallback<MouseEnterEvent>(_ => expBtn.style.color = Color.white);
                expBtn.RegisterCallback<MouseLeaveEvent>(_ => expBtn.style.color = HonamiGraphStyles.GreyText);
                expContainer.Add(expBtn);
            }

            var icon = new Image { image = HonamiEditorIcons.Profile };
            icon.style.width = icon.style.height = 16;
            icon.style.marginRight = 10;
            icon.tintColor = HonamiGraphStyles.Accent;
            Add(icon);

            _nameField = new TextField { value = state.stateName };
            _nameField.style.flexGrow = 1;
            _nameField.isDelayed = true;

            var textInput = _nameField.Q(className: "unity-text-field__input");
            if (textInput != null)
            {
                textInput.style.color = HonamiGraphStyles.TitleClr;
                textInput.style.fontSize = 13;
                textInput.style.unityFontStyleAndWeight = FontStyle.Bold;
                textInput.style.backgroundColor = Color.clear;
                textInput.style.borderTopWidth = textInput.style.borderBottomWidth = textInput.style.borderLeftWidth = textInput.style.borderRightWidth = 0;
            }

            _nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_state, "Rename Profile");
                _state.stateName = evt.newValue;
                _state.name = evt.newValue;
                EditorUtility.SetDirty(_state);
                HonamiGraphView.DeferredSave();
            });
            Add(_nameField);

            if (_view.Graph.defaultState == state)
            {
                var defBadge = new Label("DEFAULT");
                defBadge.style.fontSize = 8;
                defBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                defBadge.style.backgroundColor = new Color(HonamiGraphStyles.Orange.r, HonamiGraphStyles.Orange.g, HonamiGraphStyles.Orange.b, 0.1f);
                defBadge.style.color = HonamiGraphStyles.Orange;
                defBadge.style.paddingLeft = defBadge.style.paddingRight = 6;
                defBadge.style.paddingTop = defBadge.style.paddingBottom = 1;
                defBadge.style.borderTopLeftRadius = defBadge.style.borderTopRightRadius =
                defBadge.style.borderBottomLeftRadius = defBadge.style.borderBottomRightRadius = 4;
                defBadge.style.borderTopWidth = defBadge.style.borderBottomWidth = defBadge.style.borderLeftWidth = defBadge.style.borderRightWidth = 1;
                defBadge.style.borderTopColor = defBadge.style.borderBottomColor = defBadge.style.borderLeftColor = defBadge.style.borderRightColor = new Color(HonamiGraphStyles.Orange.r, HonamiGraphStyles.Orange.g, HonamiGraphStyles.Orange.b, 0.2f);
                defBadge.style.marginRight = 8;
                Add(defBadge);
            }

            var addBtn = new VisualElement();
            addBtn.style.width = 24;
            addBtn.style.height = 24;
            addBtn.style.marginRight = 8;
            addBtn.style.justifyContent = Justify.Center;
            addBtn.style.alignItems = Align.Center;
            addBtn.style.borderTopLeftRadius = addBtn.style.borderTopRightRadius = addBtn.style.borderBottomLeftRadius = addBtn.style.borderBottomRightRadius = 4;

            var addLabel = new Label("+");
            addLabel.style.fontSize = 16;
            addLabel.style.color = new Color(1, 1, 1, 0.3f);
            addBtn.Add(addLabel);

            addBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                addBtn.style.backgroundColor = new Color(1, 1, 1, 0.05f);
                addLabel.style.color = Color.white;
            });
            addBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                addBtn.style.backgroundColor = Color.clear;
                addLabel.style.color = new Color(1, 1, 1, 0.3f);
            });
            addBtn.RegisterCallback<MouseDownEvent>(e =>
            {
                _view.CreateState(_state);
                e.StopPropagation();
            });
            Add(addBtn);

            RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_view.SelectedState != _state)
                {
                    _bg.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1.0f);
                    _bg.style.borderTopColor = _bg.style.borderBottomColor = _bg.style.borderRightColor = new Color(1, 1, 1, 0.1f);
                }
            });
            RegisterCallback<MouseLeaveEvent>(_ =>
            {
                SetSelected(_view.SelectedState == _state);
            });

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            _view.SelectState(_state);
            if (e.button == 0)
            {
                _isMouseDown = true;
                _dragStartPos = e.localMousePosition;
            }
            else if (e.button == 1)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Child"), false, () => _view.CreateState(_state));
                menu.AddItem(new GUIContent("Delete"), false, () => _view.DeleteState(_state));
                menu.AddSeparator("");

                bool isDefault = _view.Graph.defaultState == _state;
                menu.AddItem(new GUIContent(isDefault ? "Remove Default" : "Set as Default"), isDefault, () =>
                {
                    Undo.RecordObject(_view.Graph, "Set Default State");
                    _view.Graph.defaultState = isDefault ? null : _state;
                    EditorUtility.SetDirty(_view.Graph);
                    _view.PopulateView(_view.Graph);
                });
                menu.ShowAsContext();
            }
            e.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (_isMouseDown && (e.localMousePosition - _dragStartPos).sqrMagnitude > 100f)
            {
                _isMouseDown = false;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { _state };
                DragAndDrop.StartDrag(_state.stateName);
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            _isMouseDown = false;
        }

        private void OnDragUpdated(DragUpdatedEvent e)
        {
            var dragObj = DragAndDrop.objectReferences.FirstOrDefault() as HonamiProfileState;
            if (dragObj != null && dragObj != _state)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                _bg.style.backgroundColor = new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.1f);
                _dragIndicator.style.display = DisplayStyle.Flex;
                e.StopPropagation();
            }
        }

        private void OnDragLeave(DragLeaveEvent e)
        {
            _bg.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            _dragIndicator.style.display = DisplayStyle.None;
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            var dragObj = DragAndDrop.objectReferences.FirstOrDefault() as HonamiProfileState;
            if (dragObj != null && dragObj != _state)
            {
                DragAndDrop.AcceptDrag();
                _view.ReparentState(dragObj, _state);
                e.StopPropagation();
            }
            _bg.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            _dragIndicator.style.display = DisplayStyle.None;
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                _bg.style.backgroundColor = new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.12f);
                _bg.style.borderTopColor = _bg.style.borderBottomColor = _bg.style.borderRightColor = new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.3f);
                _accent.style.backgroundColor = HonamiGraphStyles.Accent;
                _accent.style.width = 4;
            }
            else
            {
                _bg.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 0.95f);
                _bg.style.borderTopColor = _bg.style.borderBottomColor = _bg.style.borderRightColor = new Color(1, 1, 1, 0.05f);
                _accent.style.backgroundColor = new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.4f);
                _accent.style.width = 3;
            }
        }
    }
}

