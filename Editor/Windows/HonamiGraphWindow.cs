using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Editor.Timeline;
using HonamiAnimationSystem.Editor.Handlers;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Main node-based graph editor window for defining animation states, 
    /// transitions, parameters, and blend trees within HonamiAnimationSystem.
    /// </summary>
    public sealed class HonamiGraphWindow : EditorWindow
    {
        public HonamiNotificationPanel NotificationPanel { get; private set; }

        [SerializeField] public List<HonamiGraphTab> Tabs = new List<HonamiGraphTab>();
        [SerializeField] public int ActiveTabIndex = -1;

        private HonamiTabBar _tabBar;

        [SerializeField] public Runtime.Core.HonamiRuntimeController RuntimeController;
        [SerializeField] public Runtime.Core.HonamiController Controller;
        public SerializedObject SerializedController;
        public SerializedObject SerializedState;

        [SerializeField] public string SelectedNodeGuid;
        [SerializeField] public string SelectedTransitionGuid;
        [SerializeField] public int SelectedTransitionIndex = -1;
        [SerializeField] public Runtime.Core.HonamiState SelectedTransitionOwner;

        public static Runtime.Core.HonamiController ClipboardController;
        public static int ClipboardLayerIndex = -1;


        public HonamiNode SelectedNode;
        public Runtime.Core.HonamiTransition SelectedTransition;
        public Runtime.Core.HonamiSubNodeBase SelectedSubNode;
        public SerializedObject SerializedSubNode;

        public VisualElement LeftPanel { get; private set; }
        public VisualElement RightPanel { get; private set; }
        public VisualElement RightContent { get; private set; }
        public VisualElement CenterContainer { get; private set; }
        public VisualElement EmptyStateContainer { get; private set; }
        private TwoPaneSplitView _mainSplitView;
        private TwoPaneSplitView _rightSplitView;
        public Toolbar Toolbar { get; private set; }

        [SerializeField] private bool _isLeftPanelVisible = true;
        [SerializeField] private bool _isRightPanelVisible = true;
        [SerializeField] private float _leftPanelWidth = 250;
        [SerializeField] private float _rightPanelWidth = 320;
        [SerializeField] public int CurrentLayerIndex = 0;

        private bool _isLeftPanelSupported = true;
        private bool _isRightPanelSupported = true;

        public Runtime.Core.HonamiAnimator RuntimeAnimator;
        [SerializeField] public bool IsLocked = false;
        public float NextAnimatorSearchTime;
        public Label TitleLabel { get; private set; }
        public Label OverrideBadge { get; private set; }
        public Label LinkedBadge { get; private set; }

        [SerializeField] private string _windowModeId = "Animator";
        [SerializeField] public Runtime.Core.HonamiLinkedAnimatorGraph LinkedGraph;
        [SerializeField] public Runtime.Core.HonamiControllerProfileGraph ProfileGraph;
        public Runtime.Core.HonamiLinkedAnimator RuntimeLinkedAnimator;

        public string WindowModeId => _windowModeId;

        public Runtime.Core.HonamiLinkedAnimatorNodeBase SelectedLinkedNode;
        public Runtime.Core.HonamiLinkedAnimatorEvent SelectedLinkedEvent;
        public Runtime.Core.HonamiProfileState SelectedProfileState;

        private Dictionary<string, IHonamiGraphModeHandler> _handlers;
        private IHonamiGraphModeHandler _currentHandler;

        [MenuItem("Window/Honami/Honami Animator")]
        public static void OpenWindow()
        {
            var w = GetWindow<HonamiGraphWindow>();
            w.titleContent = HonamiEditorIcons.IconContent("HonamiGraphWhite", "Honami Animator");
            w.minSize = new Vector2(800, 600);
        }

        public static void LoadController(Runtime.Core.HonamiRuntimeController ctrl)
            => GetWindow<HonamiGraphWindow>().SetController(ctrl);

        public static void ShowNotification(string title, string msg,
            HonamiNotificationType type = HonamiNotificationType.Info, float dur = 5f)
        {
            if (HasOpenInstances<HonamiGraphWindow>())
                GetWindow<HonamiGraphWindow>().NotificationPanel?.Show(title, msg, type, dur);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceID);
            if (obj is Runtime.Core.HonamiRuntimeController || obj is Runtime.Core.HonamiLinkedAnimatorGraph || obj is Runtime.Core.HonamiControllerProfileGraph)
            {
                var w = GetWindow<HonamiGraphWindow>();
                w.titleContent = HonamiEditorIcons.IconContent("HonamiGraphWhite", "Honami Animator");
                w.minSize = new Vector2(800, 600);
                w.OpenAssetInTab(obj);
                return true;
            }
            return false;
        }

        public void OpenAssetInTab(Object asset)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                var tab = Tabs[i];
                if ((asset is Runtime.Core.HonamiRuntimeController rc && tab.RuntimeController == rc) ||
                    (asset is Runtime.Core.HonamiLinkedAnimatorGraph lg && tab.LinkedGraph == lg) ||
                    (asset is Runtime.Core.HonamiControllerProfileGraph pg && tab.ProfileGraph == pg))
                {
                    SwitchToTab(i);
                    return;
                }
            }

            // Create new tab
            var newTab = new HonamiGraphTab();
            if (asset is Runtime.Core.HonamiRuntimeController rcc) { newTab = new HonamiGraphTab(rcc); }
            else if (asset is Runtime.Core.HonamiLinkedAnimatorGraph lgc) { newTab = new HonamiGraphTab(lgc, null); }
            else if (asset is Runtime.Core.HonamiControllerProfileGraph pgc) { newTab = new HonamiGraphTab(pgc); }

            Tabs.Add(newTab);
            SwitchToTab(Tabs.Count - 1);
        }

        private void OnEnable()
        {
            _handlers = new Dictionary<string, IHonamiGraphModeHandler>();
            var handlerTypes = TypeCache.GetTypesDerivedFrom<IHonamiGraphModeHandler>();

            foreach (var type in handlerTypes)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (System.Activator.CreateInstance(type) is IHonamiGraphModeHandler handler)
                {
                    _handlers[handler.ModeId] = handler;
                }
            }

            ConstructLayout();

            foreach (var h in _handlers.Values)
            {
                h.Initialize(this);

                var mainView = h.GetMainView();
                if (mainView != null)
                {
                    mainView.style.display = DisplayStyle.None;
                    CenterContainer.Add(mainView);
                }

                var leftView = h.GetLeftPanelView();
                if (leftView != null)
                {
                    leftView.style.display = DisplayStyle.None;
                    LeftPanel.Add(leftView);
                }
            }

            if (Tabs == null) Tabs = new List<HonamiGraphTab>();
            if (Tabs.Count > 0 && ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count)
            {
                var tab = Tabs[ActiveTabIndex];
                tab.ApplyTo(this);
            }
            else
            {
                SwitchMode(_windowModeId);
            }

            UpdateTabsUI();

            NotificationPanel?.BringToFront();
            _currentHandler?.OnEnable();

            Selection.selectionChanged += HandleSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            HonamiGraphSettings.Changed += OnGraphSettingsChanged;
            HonamiAppearanceSettings.Changed += OnAppearanceChanged;
            EditorApplication.delayCall += HandleSelectionChanged;
        }

        private void OnGraphSettingsChanged() => _currentHandler?.ApplySettings();

        private HonamiAppearanceRebuildScheduler _appearanceRebuild;

        private void OnAppearanceChanged()
        {
            if (_handlers == null) return;
            foreach (var handler in _handlers.Values)
            {
                if (handler.GetMainView() is UnityEditor.Experimental.GraphView.GraphView graphView)
                    HonamiGraphAccent.RefreshGraphView(graphView);
            }
            Repaint();

            _appearanceRebuild ??= new HonamiAppearanceRebuildScheduler(ReloadUI);
            _appearanceRebuild.Request();
        }

        private void ReloadUI()
        {
            if (this == null) return;
            OnDisable();
            rootVisualElement.Clear();
            OnEnable();
            Repaint();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= HandleSelectionChanged;
            EditorApplication.delayCall -= HandleSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            HonamiGraphSettings.Changed -= OnGraphSettingsChanged;
            HonamiAppearanceSettings.Changed -= OnAppearanceChanged;
            _appearanceRebuild?.Cancel();
            _currentHandler?.OnDisable();
        }

        private void OnInspectorUpdate()
        {
            if (_windowModeId == "Animator")
            {
                if (!ReferenceEquals(RuntimeController, null) && RuntimeController == null) SetController(null);
            }
            else if (_windowModeId == "LinkedAnimator")
            {
                if (!ReferenceEquals(LinkedGraph, null) && LinkedGraph == null) SetLinkedGraph(null, null);
            }
            else if (_windowModeId == "Profile")
            {
                if (!ReferenceEquals(ProfileGraph, null) && ProfileGraph == null) SetProfileGraph(null);
            }

            if (focusedWindow == this || mouseOverWindow == this) Repaint();
        }

        private void Update() => _currentHandler?.Update();

        public void SwitchMode(string modeId)
        {
            _windowModeId = modeId;

            // Hide all views first
            foreach (var h in _handlers.Values)
            {
                if (h.GetMainView() != null) h.GetMainView().style.display = DisplayStyle.None;
                if (h.GetLeftPanelView() != null) h.GetLeftPanelView().style.display = DisplayStyle.None;
            }

            if (_handlers.TryGetValue(modeId, out var handler))
            {
                _currentHandler = handler;

                if (_currentHandler.GetMainView() != null) _currentHandler.GetMainView().style.display = DisplayStyle.Flex;
                if (_currentHandler.GetLeftPanelView() != null) _currentHandler.GetLeftPanelView().style.display = DisplayStyle.Flex;

                _currentHandler.SwitchToMode();
                _currentHandler.ApplySettings();
            }
            else
            {
                _currentHandler = null;
                if (TitleLabel != null) TitleLabel.text = "No Graph Selected";
                if (OverrideBadge != null) OverrideBadge.style.display = DisplayStyle.None;
                if (LinkedBadge != null) LinkedBadge.style.display = DisplayStyle.None;
                if (Toolbar != null)
                {
                    Toolbar.style.display = DisplayStyle.Flex;
                    var cf = Toolbar.Q<UnityEditor.UIElements.ObjectField>();
                    cf?.SetValueWithoutNotify(null);
                }
                SupportLeftPanel(false);
                SupportRightPanel(false);
                UpdateEmptyState();
            }
        }

        private void ConstructLayout()
        {
            var root = rootVisualElement;
            HonamiEditorLayout.PrepareRoot(root);

            var header = HonamiEditorLayout.Header(HonamiEditorIcons.Graph, "Loading...", "Graph Editor", out Label titleLabelOut);
            TitleLabel = titleLabelOut;

            OverrideBadge = HonamiEditorLayout.Badge("OVERRIDE", HonamiEditorLayout.Warning);
            header.Insert(header.childCount - 1, OverrideBadge);

            LinkedBadge = HonamiEditorLayout.Badge("LINKED", new Color(0.3f, 0.7f, 1f));
            header.Insert(header.childCount - 1, LinkedBadge);

            var frameBtn = HonamiEditorLayout.HeaderButton("Frame All", () => _currentHandler?.FrameAll());
            header.Add(frameBtn);
            root.Add(header);

            BuildTabBar(root);

            _mainSplitView = new TwoPaneSplitView(0, _leftPanelWidth, TwoPaneSplitViewOrientation.Horizontal);
            _mainSplitView.style.flexGrow = 1;

            LeftPanel = new VisualElement();
            HonamiEditorLayout.StyleSidePanel(LeftPanel, rightBorder: true);
            LeftPanel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (_isLeftPanelVisible && evt.newRect.width > 20)
                    _leftPanelWidth = evt.newRect.width;
            });
            _mainSplitView.Add(LeftPanel);

            _rightSplitView = new TwoPaneSplitView(1, _rightPanelWidth, TwoPaneSplitViewOrientation.Horizontal);
            _rightSplitView.style.flexGrow = 1;

            CenterContainer = new VisualElement();
            CenterContainer.style.flexDirection = FlexDirection.Column;
            CenterContainer.style.flexGrow = 1;
            CenterContainer.style.minWidth = 0;
            CenterContainer.style.minHeight = 0;

            GenerateToolbar(CenterContainer);

            EmptyStateContainer = HonamiEditorLayout.EmptyState(
                HonamiEditorIcons.Graph, "Honami Animation System", "No Graph Selected",
                "Please select a HonamiRuntimeController or\nHonamiLinkedAnimatorGraph from your Project or Hierarchy.");
            EmptyStateContainer.style.position = Position.Absolute;
            EmptyStateContainer.style.left = 0;
            EmptyStateContainer.style.right = 0;
            EmptyStateContainer.style.top = 24;
            EmptyStateContainer.style.bottom = 0;
            EmptyStateContainer.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f);
            CenterContainer.Add(EmptyStateContainer);

            NotificationPanel = new HonamiNotificationPanel();
            CenterContainer.Add(NotificationPanel);

            RightPanel = new VisualElement();
            HonamiEditorLayout.StyleSidePanel(RightPanel, leftBorder: true);
            RightPanel.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (_isRightPanelVisible && evt.newRect.width > 20)
                    _rightPanelWidth = evt.newRect.width;
            });

            var rightScroll = new ScrollView();
            rightScroll.style.flexGrow = 1;
            RightContent = new VisualElement();
            rightScroll.Add(RightContent);
            RightPanel.Add(rightScroll);

            _rightSplitView.Add(CenterContainer);
            _rightSplitView.Add(RightPanel);

            _mainSplitView.Add(_rightSplitView);
            root.Add(_mainSplitView);

            BuildRightPanel();

            ToggleLeftPanel(_isLeftPanelVisible);
            ToggleRightPanel(_isRightPanelVisible);
            UpdateEmptyState();
        }

        public void BuildRightPanel() => _currentHandler?.BuildRightPanel(RightContent);

        private void BuildTabBar(VisualElement root)
        {
            _tabBar = new HonamiTabBar(
                () => Tabs.Count, () => ActiveTabIndex, SwitchToTab, CloseTab, OpenEmptyTab,
                i => Tabs[i].GetTabName(), i => Tabs[i].GetTabIcon());
            root.Add(_tabBar);
        }

        public void UpdateTabsUI() => _tabBar?.Refresh();

        public void OpenEmptyTab()
        {
            var tab = new HonamiGraphTab();
            Tabs.Add(tab);
            SwitchToTab(Tabs.Count - 1);
        }

        public void CloseTab(int index)
        {
            if (index < 0 || index >= Tabs.Count) return;
            Tabs.RemoveAt(index);
            if (Tabs.Count == 0)
            {
                ActiveTabIndex = -1;
                switch (_windowModeId)
                {
                    case "LinkedAnimator": SetLinkedGraph(null, null); break;
                    case "Profile": SetProfileGraph(null); break;
                    default: SetController(null); break;
                }
                UpdateTabsUI();
                UpdateEmptyState();
            }
            else
            {
                if (ActiveTabIndex == index)
                {
                    SwitchToTab(Mathf.Max(0, index - 1));
                }
                else if (ActiveTabIndex > index)
                {
                    ActiveTabIndex--;
                    UpdateTabsUI();
                }
                else
                {
                    UpdateTabsUI();
                }
            }
        }

        public void SwitchToTab(int index)
        {
            if (index < 0 || index >= Tabs.Count) return;
            if (ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count)
            {
                SaveSelection(); // current to window state
                Tabs[ActiveTabIndex].SaveSelectionFrom(this);
            }

            ActiveTabIndex = index;
            var tab = Tabs[ActiveTabIndex];
            tab.ApplyTo(this);
            UpdateTabsUI();
            UpdateEmptyState();
        }

        public void SetController(Runtime.Core.HonamiRuntimeController ctrl)
        {
            if (RuntimeController == ctrl) return;

            if (ctrl == null)
            {
                RuntimeController = null;
                Controller = null;
                SerializedController = null;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;
                SwitchMode("Animator");
                UpdateTabsUI();
                return;
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].ModeId == "Animator" && Tabs[i].RuntimeController == ctrl)
                {
                    SwitchToTab(i);
                    return;
                }
            }

            if (ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count && Tabs[ActiveTabIndex].IsEmpty())
            {
                var tab = Tabs[ActiveTabIndex];
                tab.ModeId = "Animator";
                tab.RuntimeController = ctrl;
                tab.LinkedGraph = null;
                tab.ProfileGraph = null;

                RuntimeController = ctrl;
                Controller = ctrl != null ? ctrl.BaseController : null;
                SerializedController = Controller != null ? new SerializedObject(Controller) : null;
                CurrentLayerIndex = 0;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;

                SwitchMode("Animator");
                UpdateTabsUI();
            }
            else
            {
                Tabs.Add(new HonamiGraphTab(ctrl));
                SwitchToTab(Tabs.Count - 1);
            }
        }

        public void SetLinkedGraph(Runtime.Core.HonamiLinkedAnimatorGraph bg, Runtime.Core.HonamiLinkedAnimator brain)
        {
            if (LinkedGraph == bg && RuntimeLinkedAnimator == brain) return;

            if (bg == null)
            {
                LinkedGraph = null;
                RuntimeLinkedAnimator = null;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;
                SelectedLinkedNode = null;
                SelectedLinkedEvent = null;
                SwitchMode("LinkedAnimator");
                UpdateTabsUI();
                return;
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].ModeId == "LinkedAnimator" && Tabs[i].LinkedGraph == bg)
                {
                    Tabs[i].RuntimeLinkedAnimator = brain;
                    SwitchToTab(i);
                    return;
                }
            }

            if (ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count && Tabs[ActiveTabIndex].IsEmpty())
            {
                var tab = Tabs[ActiveTabIndex];
                tab.ModeId = "LinkedAnimator";
                tab.RuntimeController = null;
                tab.LinkedGraph = bg;
                tab.RuntimeLinkedAnimator = brain;
                tab.ProfileGraph = null;

                LinkedGraph = bg;
                RuntimeLinkedAnimator = brain;
                RuntimeController = null;
                Controller = null;
                SerializedController = null;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;
                SelectedLinkedNode = null;
                SelectedLinkedEvent = null;

                SwitchMode("LinkedAnimator");
                UpdateTabsUI();
            }
            else
            {
                Tabs.Add(new HonamiGraphTab(bg, brain));
                SwitchToTab(Tabs.Count - 1);
            }
        }

        public void SetProfileGraph(Runtime.Core.HonamiControllerProfileGraph pg)
        {
            if (ProfileGraph == pg) return;

            if (pg == null)
            {
                ProfileGraph = null;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;
                SelectedLinkedNode = null;
                SelectedLinkedEvent = null;
                SelectedProfileState = null;
                SwitchMode("Profile");
                UpdateTabsUI();
                return;
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i].ModeId == "Profile" && Tabs[i].ProfileGraph == pg)
                {
                    SwitchToTab(i);
                    return;
                }
            }

            if (ActiveTabIndex >= 0 && ActiveTabIndex < Tabs.Count && Tabs[ActiveTabIndex].IsEmpty())
            {
                var tab = Tabs[ActiveTabIndex];
                tab.ModeId = "Profile";
                tab.RuntimeController = null;
                tab.LinkedGraph = null;
                tab.ProfileGraph = pg;

                ProfileGraph = pg;
                LinkedGraph = null;
                RuntimeLinkedAnimator = null;
                RuntimeController = null;
                Controller = null;
                SerializedController = null;
                SelectedNode = null;
                SelectedNodeGuid = null;
                SelectedTransition = null;
                SelectedTransitionGuid = null;
                SelectedTransitionOwner = null;
                SelectedLinkedNode = null;
                SelectedLinkedEvent = null;
                SelectedProfileState = null;

                SwitchMode("Profile");
                UpdateTabsUI();
            }
            else
            {
                Tabs.Add(new HonamiGraphTab(pg));
                SwitchToTab(Tabs.Count - 1);
            }
        }

        public void UpdateEmptyState()
        {
            if (EmptyStateContainer == null) return;

            bool isEmpty = _currentHandler == null || !_currentHandler.HasContent();
            EmptyStateContainer.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;

            if (isEmpty)
            {
                foreach (var h in _handlers.Values)
                {
                    if (h.GetMainView() != null) h.GetMainView().style.display = DisplayStyle.None;
                }

                float topOffset = (Toolbar != null && Toolbar.style.display == DisplayStyle.Flex) ? 24 : 0;
                EmptyStateContainer.style.top = topOffset;

                SetLeftPanelActive(false);
                SetRightPanelActive(false);
            }
            else
            {
                SetLeftPanelActive(_isLeftPanelSupported && _isLeftPanelVisible);
                SetRightPanelActive(_isRightPanelSupported && _isRightPanelVisible);
            }
        }

        private struct SelectionState
        {
            public string NodeGuid;
            public string TransGuid;
            public int TransIndex;
            public Runtime.Core.HonamiState TransOwner;
            public Runtime.Core.HonamiSubNodeBase SubNode;

            public Runtime.Core.HonamiLinkedAnimatorNodeBase LinkedNode;
            public Runtime.Core.HonamiLinkedAnimatorEvent LinkedEvent;

            public Runtime.Core.HonamiProfileState ProfileState;
        }

        private SelectionState _savedSelection;

        public void SaveSelection()
        {
            _savedSelection = new SelectionState
            {
                NodeGuid = SelectedNodeGuid,
                TransGuid = SelectedTransitionGuid,
                TransIndex = SelectedTransitionIndex,
                TransOwner = SelectedTransitionOwner,
                SubNode = SelectedSubNode,
                LinkedNode = SelectedLinkedNode,
                LinkedEvent = SelectedLinkedEvent,
                ProfileState = SelectedProfileState
            };
        }

        public void RestoreSelection()
        {
            SelectedNodeGuid = _savedSelection.NodeGuid;
            SelectedTransitionGuid = _savedSelection.TransGuid;
            SelectedTransitionIndex = _savedSelection.TransIndex;
            SelectedTransitionOwner = _savedSelection.TransOwner;
            SelectedSubNode = _savedSelection.SubNode;
            SelectedLinkedNode = _savedSelection.LinkedNode;
            SelectedLinkedEvent = _savedSelection.LinkedEvent;
            SelectedProfileState = _savedSelection.ProfileState;

            if (_currentHandler is HonamiAnimatorGraphHandler h)
            {
                var graphView = h.GetMainView() as HonamiGraphView;
                if (graphView == null) return;

                if (!string.IsNullOrEmpty(SelectedNodeGuid))
                {
                    var node = graphView.nodes.OfType<HonamiNode>().FirstOrDefault(n => n.StateGuid == SelectedNodeGuid);
                    if (node != null)
                    {
                        graphView.AddToSelection(node);
                        SelectedNode = node;
                        if (node.State != null && !node.State.isVirtualInheritedState) SerializedState = new SerializedObject(node.State);
                        if (SelectedSubNode != null && node.State != null && node.State.subNodes != null && node.State.subNodes.Contains(SelectedSubNode))
                        {
                            SerializedSubNode = new SerializedObject(SelectedSubNode);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(SelectedTransitionGuid) && SelectedTransitionOwner != null)
                {
                    var edge = graphView.edges.OfType<HonamiTransitionEdge>().FirstOrDefault(e =>
                        e.userData is Runtime.Core.HonamiTransition t &&
                        t.targetStateGuid == SelectedTransitionGuid &&
                        e.output != null && e.output.node is HonamiNode sn &&
                        sn.StateGuid == SelectedTransitionOwner.guid);

                    if (edge != null)
                    {
                        graphView.AddToSelection(edge);
                        SelectedTransition = edge.userData as Runtime.Core.HonamiTransition;
                        if (!SelectedTransitionOwner.isVirtualInheritedState) SerializedState = new SerializedObject(SelectedTransitionOwner);

                        var transitions = RuntimeController != null ? RuntimeController.GetTransitions(SelectedTransitionOwner) : SelectedTransitionOwner.transitions;
                        if (transitions != null)
                        {
                            SelectedTransitionIndex = -1;
                            for (int i = 0; i < transitions.Count; i++)
                            {
                                if (transitions[i] == SelectedTransition) { SelectedTransitionIndex = i; break; }
                            }
                        }
                    }
                }
            }
            else if (_currentHandler is HonamiLinkedAnimatorGraphHandler lh)
            {
                var graphView = lh.GetMainView() as HonamiLinkedAnimatorGraphView;
                if (graphView == null) return;

                if (SelectedLinkedNode != null)
                {
                    var node = graphView.nodes.OfType<HonamiLinkedAnimatorNode>().FirstOrDefault(n => n.BrainNode == SelectedLinkedNode);
                    if (node != null) graphView.AddToSelection(node);
                }
                else if (SelectedLinkedEvent != null)
                {
                    var evtNode = graphView.nodes.OfType<HonamiLinkedAnimatorEventNode>().FirstOrDefault(n => n.BrainEvent == SelectedLinkedEvent);
                    if (evtNode != null) graphView.AddToSelection(evtNode);
                }
            }
            else if (_currentHandler is HonamiProfileGraphHandler ph)
            {
                var graphView = ph.GetMainView() as HonamiProfileGraphView;
                if (graphView == null) return;

                if (SelectedProfileState != null)
                {
                    graphView.SelectState(SelectedProfileState);
                }
            }
        }

        private void GenerateToolbar(VisualElement parent)
        {
            Toolbar = new Toolbar();

            var cf = new ObjectField { objectType = typeof(Runtime.Core.HonamiRuntimeController), value = RuntimeController };
            cf.style.minWidth = 180;
            cf.RegisterValueChangedCallback(ev => SetController(ev.newValue as Runtime.Core.HonamiRuntimeController));
            Toolbar.Add(cf);

            var lockTgl = new ToolbarToggle { value = IsLocked, tooltip = "Lock to current Animator/Controller" };
            var lockIcon = EditorGUIUtility.IconContent("InspectorLock").image
                        ?? EditorGUIUtility.IconContent("lock").image;
            if (lockIcon != null) lockTgl.Add(new Image { image = lockIcon, style = { width = 16, height = 16 } });
            else lockTgl.text = "Lock";
            lockTgl.RegisterValueChangedCallback(ev => IsLocked = ev.newValue);
            Toolbar.Add(lockTgl);

            Toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var sm = new ToolbarMenu { text = "Settings", style = { width = 80 } };
            sm.menu.AppendAction("Show Minimap",
                _ => { HonamiGraphSettings.ShowMinimap = !HonamiGraphSettings.ShowMinimap; },
                _ => HonamiGraphSettings.ShowMinimap ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sm.menu.AppendSeparator();
            sm.menu.AppendAction("Show Layers Panel",
                _ => ToggleLeftPanel(!_isLeftPanelVisible),
                _ =>
                {
                    if (!_isLeftPanelSupported) return DropdownMenuAction.Status.Hidden;
                    return _isLeftPanelVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                });
            sm.menu.AppendAction("Show Inspector Panel",
                _ => ToggleRightPanel(!_isRightPanelVisible),
                _ =>
                {
                    if (!_isRightPanelSupported) return DropdownMenuAction.Status.Hidden;
                    return _isRightPanelVisible ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
                });
            sm.menu.AppendSeparator();
            sm.menu.AppendAction("Graph Preferences...", _ => HonamiPreferences.Open("Graph"));
            Toolbar.Add(sm);

            parent.Insert(0, Toolbar);
        }

        public void ToggleLeftPanel(bool visible)
        {
            if (!_isLeftPanelSupported) return;
            _isLeftPanelVisible = visible;
            SetLeftPanelActive(visible);
        }

        private void SetLeftPanelActive(bool active)
        {
            if (LeftPanel == null || _mainSplitView == null) return;

            float width = Mathf.Max(_leftPanelWidth, 100);

            if (active)
            {
                LeftPanel.style.display = DisplayStyle.Flex;
                _mainSplitView.fixedPaneInitialDimension = width;
                LeftPanel.style.width = width;
            }
            else
            {
                _mainSplitView.fixedPaneInitialDimension = 0;
                LeftPanel.style.width = 0;
                LeftPanel.style.display = DisplayStyle.None;
            }

            _mainSplitView.schedule.Execute(() =>
            {
                var splitter = _mainSplitView.Q(className: "unity-two-pane-split-view__dragline");
                if (splitter != null) splitter.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }).ExecuteLater(1);
        }

        public void ToggleRightPanel(bool visible)
        {
            if (!_isRightPanelSupported) return;
            _isRightPanelVisible = visible;
            SetRightPanelActive(visible);
        }

        private void SetRightPanelActive(bool active)
        {
            if (RightPanel == null || _rightSplitView == null) return;

            float width = Mathf.Max(_rightPanelWidth, 100);

            if (active)
            {
                RightPanel.style.display = DisplayStyle.Flex;
                _rightSplitView.fixedPaneInitialDimension = width;
                RightPanel.style.width = width;
            }
            else
            {
                _rightSplitView.fixedPaneInitialDimension = 0;
                RightPanel.style.width = 0;
                RightPanel.style.display = DisplayStyle.None;
            }

            _rightSplitView.schedule.Execute(() =>
            {
                var splitter = _rightSplitView.Q(className: "unity-two-pane-split-view__dragline");
                if (splitter != null) splitter.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }).ExecuteLater(1);
        }

        public void SupportLeftPanel(bool support)
        {
            _isLeftPanelSupported = support;
            if (!support) SetLeftPanelActive(false);
            else SetLeftPanelActive(_isLeftPanelVisible);
        }

        public void SupportRightPanel(bool support)
        {
            _isRightPanelSupported = support;
            if (!support) SetRightPanelActive(false);
            else SetRightPanelActive(_isRightPanelVisible);
        }

        // Named differently from the OnSelectionChange EditorWindow message so selection changes are handled exactly once.
        private void HandleSelectionChanged() => _currentHandler?.OnSelectionChange();
        private void OnUndoRedo() => _currentHandler?.OnUndoRedo();

        // ------------------ Animator Logic ------------------ //
        public void SetLayer(int index)
        {
            CurrentLayerIndex = index;
            SelectedNode = null; SelectedNodeGuid = null;
            SelectedTransition = null; SelectedTransitionGuid = null;
            if (_currentHandler is HonamiAnimatorGraphHandler h && h.GetMainView() is HonamiGraphView gv)
            {
                gv.PopulateView(RuntimeController, CurrentLayerIndex);
            }
            _currentHandler?.RebuildLeftPanel();
            BuildRightPanel();
        }

        public void AddParameter(Runtime.Core.HonamiParameterType type)
        {
            Undo.RecordObject(Controller, "Add Parameter");
            Controller.parameters.Add(new Runtime.Core.HonamiParameter { name = "New " + type, type = type });
            EditorUtility.SetDirty(Controller);
        }

        public void DuplicateLayer(int srcIdx)
        {
            if (Controller == null) return;

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);
            var srcLayerProp = layersProp.GetArrayElementAtIndex(srcIdx);

            newLayerProp.FindPropertyRelative("name").stringValue = srcLayerProp.FindPropertyRelative("name").stringValue + " Copy";
            newLayerProp.FindPropertyRelative("weight").floatValue = srcLayerProp.FindPropertyRelative("weight").floatValue;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = srcLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue;
            newLayerProp.FindPropertyRelative("mirror").boolValue = srcLayerProp.FindPropertyRelative("mirror").boolValue;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = -1;

            SerializedController.ApplyModifiedProperties();

            Undo.RecordObject(Controller, "Duplicate Layer");

            var guidMap = new Dictionary<string, string>();
            var newStates = new List<Runtime.Core.HonamiState>();

            foreach (var s in Controller.states.Where(s => s?.layerIndex == srcIdx))
            {
                var ns = Instantiate(s);
                ns.name = s.name;
                ns.guid = System.Guid.NewGuid().ToString();
                ns.layerIndex = newIdx;

                if (ns.node != null)
                {
                    ns.node = Instantiate(ns.node);
                    ns.node.name = s.node.name;
                    AssetDatabase.AddObjectToAsset(ns.node, Controller);
                    Undo.RegisterCreatedObjectUndo(ns.node, "Dup Layer State Node");
                }

                if (ns.subNodes != null)
                {
                    var copiedSubNodes = new List<Runtime.Core.HonamiSubNodeBase>();
                    foreach (var sn in ns.subNodes)
                    {
                        if (sn != null)
                        {
                            var nsn = Instantiate(sn);
                            nsn.name = sn.name;
                            AssetDatabase.AddObjectToAsset(nsn, Controller);
                            Undo.RegisterCreatedObjectUndo(nsn, "Dup Layer State SubNode");
                            copiedSubNodes.Add(nsn);
                        }
                    }
                    ns.subNodes = copiedSubNodes;
                }

                guidMap[s.guid] = ns.guid;
                newStates.Add(ns);
                AssetDatabase.AddObjectToAsset(ns, Controller);
                Undo.RegisterCreatedObjectUndo(ns, "Dup Layer State");
            }

            foreach (var ns in newStates)
            {
                Controller.states.Add(ns);
                HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(RuntimeController, ns);
                foreach (var t in ns.transitions)
                {
                    t.id = System.Guid.NewGuid().ToString();
                    if (guidMap.TryGetValue(t.targetStateGuid, out string ng)) t.targetStateGuid = ng;
                }
                EditorUtility.SetDirty(ns);
            }

            foreach (var g in Controller.groups.Where(g => g.layerIndex == srcIdx).ToList())
                Controller.groups.Add(new Runtime.Core.HonamiGroupData
                {
                    title = g.title,
                    position = g.position,
                    size = g.size,
                    layerIndex = newIdx,
                    containedNodes = g.containedNodes.Select(id => guidMap.TryGetValue(id, out string ng2) ? ng2 : id).ToList()
                });

            foreach (var sn in Controller.stickyNotes.Where(sn => sn.layerIndex == srcIdx).ToList())
                Controller.stickyNotes.Add(new Runtime.Core.HonamiStickyNoteData
                {
                    title = sn.title,
                    contents = sn.contents,
                    position = sn.position,
                    size = sn.size,
                    layerIndex = newIdx,
                    themeColor = sn.themeColor
                });

            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

            CurrentLayerIndex = newIdx;
            SelectedNode = null; SelectedNodeGuid = null;
            SelectedTransition = null; SelectedTransitionGuid = null;

            var srcName = srcLayerProp.FindPropertyRelative("name").stringValue;

            if (_currentHandler is HonamiAnimatorGraphHandler h && h.GetMainView() is HonamiGraphView gv)
            {
                gv.PopulateView(RuntimeController, CurrentLayerIndex);
            }
            _currentHandler?.RebuildLeftPanel();
            BuildRightPanel();
            HonamiNotificationPanel.ShowGlobal("Layer Duplicated", $"Layer '{srcName}' duplicated.", HonamiNotificationType.Success);
        }

        public void CreateInheritedLayer(int parentIdx)
        {
            if (Controller == null || parentIdx < 0 || parentIdx >= Controller.layers.Count) return;

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);
            var parentLayerProp = layersProp.GetArrayElementAtIndex(parentIdx);
            var versionProp = SerializedController.FindProperty("serializedLayerInheritanceVersion");
            if (versionProp != null) versionProp.intValue = 1;

            newLayerProp.FindPropertyRelative("name").stringValue = parentLayerProp.FindPropertyRelative("name").stringValue + " Child";
            newLayerProp.FindPropertyRelative("weight").floatValue = parentLayerProp.FindPropertyRelative("weight").floatValue;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = parentLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue;
            newLayerProp.FindPropertyRelative("mirror").boolValue = parentLayerProp.FindPropertyRelative("mirror").boolValue;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = parentIdx;

            SerializedController.ApplyModifiedProperties();
            Controller.ClearEffectiveStateCache();
            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

            CurrentLayerIndex = newIdx;
            SelectedNode = null; SelectedNodeGuid = null;
            SelectedTransition = null; SelectedTransitionGuid = null;

            if (_currentHandler is HonamiAnimatorGraphHandler h && h.GetMainView() is HonamiGraphView gv)
                gv.PopulateView(RuntimeController, CurrentLayerIndex);

            _currentHandler?.RebuildLeftPanel();
            BuildRightPanel();
            HonamiNotificationPanel.ShowGlobal("Inherited Layer Added", $"Layer now inherits from '{Controller.layers[parentIdx].name}'.", HonamiNotificationType.Success);
        }

        public void CopyLayer(int srcIdx)
        {
            if (Controller == null || srcIdx < 0 || srcIdx >= Controller.layers.Count) return;
            ClipboardController = Controller;
            ClipboardLayerIndex = srcIdx;
            var srcName = Controller.layers[srcIdx].name;
            HonamiNotificationPanel.ShowGlobal("Layer Copied", $"Layer '{srcName}' copied to clipboard.", HonamiNotificationType.Info);
        }

        public void PasteLayer()
        {
            if (Controller == null || ClipboardController == null || ClipboardLayerIndex < 0 || ClipboardLayerIndex >= ClipboardController.layers.Count)
            {
                HonamiNotificationPanel.ShowGlobal("Paste Failed", "Clipboard is empty or invalid.", HonamiNotificationType.Error);
                return;
            }

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);

            var srcLayer = ClipboardController.layers[ClipboardLayerIndex];
            newLayerProp.FindPropertyRelative("name").stringValue = srcLayer.name + " (Pasted)";
            newLayerProp.FindPropertyRelative("weight").floatValue = srcLayer.weight;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = srcLayer.avatarMask;
            newLayerProp.FindPropertyRelative("mirror").boolValue = srcLayer.mirror;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = -1;

            SerializedController.ApplyModifiedProperties();

            Undo.RecordObject(Controller, "Paste Layer");

            var guidMap = new Dictionary<string, string>();
            var newStates = new List<Runtime.Core.HonamiState>();

            foreach (var s in ClipboardController.states.Where(s => s?.layerIndex == ClipboardLayerIndex))
            {
                var ns = Instantiate(s);
                ns.name = s.name;
                ns.guid = System.Guid.NewGuid().ToString();
                ns.layerIndex = newIdx;

                if (ns.node != null)
                {
                    ns.node = Instantiate(ns.node);
                    ns.node.name = s.node.name;
                    AssetDatabase.AddObjectToAsset(ns.node, Controller);
                    Undo.RegisterCreatedObjectUndo(ns.node, "Paste Layer State Node");
                }

                if (ns.subNodes != null)
                {
                    var copiedSubNodes = new List<Runtime.Core.HonamiSubNodeBase>();
                    foreach (var sn in ns.subNodes)
                    {
                        if (sn != null)
                        {
                            var nsn = Instantiate(sn);
                            nsn.name = sn.name;
                            AssetDatabase.AddObjectToAsset(nsn, Controller);
                            Undo.RegisterCreatedObjectUndo(nsn, "Paste Layer State SubNode");
                            copiedSubNodes.Add(nsn);
                        }
                    }
                    ns.subNodes = copiedSubNodes;
                }

                guidMap[s.guid] = ns.guid;
                newStates.Add(ns);
                AssetDatabase.AddObjectToAsset(ns, Controller);
                Undo.RegisterCreatedObjectUndo(ns, "Paste Layer State");
            }

            foreach (var ns in newStates)
            {
                Controller.states.Add(ns);
                HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(RuntimeController, ns);
                foreach (var t in ns.transitions)
                {
                    t.id = System.Guid.NewGuid().ToString();
                    if (guidMap.TryGetValue(t.targetStateGuid, out string ng)) t.targetStateGuid = ng;
                }

                var usedParameterNames = new HashSet<string>();
                try
                {
                    CollectParameterStrings(ns, usedParameterNames);
                    if (ns.node != null) CollectParameterStrings(ns.node, usedParameterNames);
                    if (ns.subNodes != null)
                    {
                        for (int i = 0; i < ns.subNodes.Count; i++) CollectParameterStrings(ns.subNodes[i], usedParameterNames);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[HonamiGraphWindow] Failed to collect parameter strings for state {ns.stateName}: {ex.Message}");
                }

                foreach (var paramName in usedParameterNames)
                {
                    bool exists = false;
                    for (int j = 0; j < Controller.parameters.Count; j++)
                    {
                        if (Controller.parameters[j].name == paramName)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        var srcParam = ClipboardController.parameters.FirstOrDefault(p => p.name == paramName);
                        if (srcParam != null)
                        {
                            Controller.parameters.Add(new Runtime.Core.HonamiParameter
                            {
                                name = srcParam.name,
                                type = srcParam.type,
                                defaultFloat = srcParam.defaultFloat,
                                defaultInt = srcParam.defaultInt,
                                defaultBool = srcParam.defaultBool
                            });
                        }
                    }
                }

                EditorUtility.SetDirty(ns);
            }

            foreach (var g in ClipboardController.groups.Where(g => g.layerIndex == ClipboardLayerIndex).ToList())
                Controller.groups.Add(new Runtime.Core.HonamiGroupData
                {
                    title = g.title,
                    position = g.position,
                    size = g.size,
                    layerIndex = newIdx,
                    containedNodes = g.containedNodes.Select(id => guidMap.TryGetValue(id, out string ng2) ? ng2 : id).ToList()
                });

            foreach (var sn in ClipboardController.stickyNotes.Where(sn => sn.layerIndex == ClipboardLayerIndex).ToList())
                Controller.stickyNotes.Add(new Runtime.Core.HonamiStickyNoteData
                {
                    title = sn.title,
                    contents = sn.contents,
                    position = sn.position,
                    size = sn.size,
                    layerIndex = newIdx,
                    themeColor = sn.themeColor
                });

            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

            Controller.ClearEffectiveStateCache();

            CurrentLayerIndex = newIdx;
            SelectedNode = null; SelectedNodeGuid = null;
            SelectedTransition = null; SelectedTransitionGuid = null;

            if (_currentHandler is HonamiAnimatorGraphHandler h && h.GetMainView() is HonamiGraphView gv)
            {
                gv.PopulateView(RuntimeController, CurrentLayerIndex);
            }
            _currentHandler?.RebuildLeftPanel();
            BuildRightPanel();
            HonamiNotificationPanel.ShowGlobal("Layer Pasted", $"Layer '{srcLayer.name}' pasted successfully.", HonamiNotificationType.Success);
        }

        public void DeleteLayer(int idx, string name)
        {
            if (Controller == null || idx <= 0) return;
            if (!EditorUtility.DisplayDialog("Delete Layer",
                    $"Delete '{name}'? This removes all its states and transitions.", "Delete", "Cancel")) return;

            Undo.RecordObject(Controller, "Delete Layer");
            var guids = new HashSet<string>(Controller.states
                .Where(s => s?.layerIndex == idx).Select(s => s.guid));

            foreach (var s in Controller.states)
            {
                if (s?.transitions == null) continue;
                if (s.transitions.RemoveAll(t => guids.Contains(t.targetStateGuid)) > 0)
                    EditorUtility.SetDirty(s);
            }

            foreach (var s in Controller.states.Where(s => s != null && guids.Contains(s.guid)).ToList())
            {
                Controller.states.Remove(s);
                Undo.DestroyObjectImmediate(s);
            }
            Controller.states.RemoveAll(s => s == null);

            foreach (var s in Controller.states)
            {
                if (s?.layerIndex > idx) { s.layerIndex--; EditorUtility.SetDirty(s); }
            }

            foreach (var layer in Controller.layers)
            {
                if (layer == null) continue;
                if (layer.parentLayerIndex == idx) layer.parentLayerIndex = -1;
                else if (layer.parentLayerIndex > idx) layer.parentLayerIndex--;
            }

            SerializedController.Update();
            SerializedController.FindProperty("layers").DeleteArrayElementAtIndex(idx);
            SerializedController.ApplyModifiedProperties();
            Controller.ClearEffectiveStateCache();

            if (CurrentLayerIndex >= Controller.layers.Count)
                CurrentLayerIndex = Controller.layers.Count - 1;

            SelectedNode = null; SelectedNodeGuid = null;
            SelectedTransition = null; SelectedTransitionGuid = null;

            if (_currentHandler is HonamiAnimatorGraphHandler h && h.GetMainView() is HonamiGraphView gv)
            {
                gv.PopulateView(RuntimeController, CurrentLayerIndex);
            }
            _currentHandler?.RebuildLeftPanel();
            HonamiGraphView.DeferredSave();
            BuildRightPanel();
            HonamiNotificationPanel.ShowGlobal("Layer Deleted", "Layer and states removed.", HonamiNotificationType.Info);
        }

        private void CollectParameterStrings(ScriptableObject obj, HashSet<string> hashSet)
        {
            if (obj == null) return;
            var so = new SerializedObject(obj);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    if (!string.IsNullOrEmpty(prop.stringValue))
                        hashSet.Add(prop.stringValue);
                }
            }
        }

        public void ReloadCurrentGraph()
        {
            if (RuntimeController != null)
            {
                RuntimeController.BaseController?.ClearEffectiveStateCache();
                if (RuntimeController is Runtime.Core.HonamiOverrideController ov) ov.ClearCaches();
            }

            if (_currentHandler != null)
            {
                _currentHandler.SwitchToMode();
            }

            Repaint();
        }
    }

    public sealed class HonamiGraphAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Saves initiated by the graph editor itself must not rebuild the view mid-edit.
            if (HonamiGraphView.IsInternalSave) return;
            if (!HonamiGraphWindow.HasOpenInstances<HonamiGraphWindow>()) return;

            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<HonamiGraphWindow>();
                foreach (var window in windows)
                {
                    if (window == null) continue;

                    bool shouldReload = false;
                    string ctrlPath = window.RuntimeController != null ? AssetDatabase.GetAssetPath(window.RuntimeController) : null;
                    string baseCtrlPath = window.Controller != null ? AssetDatabase.GetAssetPath(window.Controller) : null;
                    string linkedPath = window.LinkedGraph != null ? AssetDatabase.GetAssetPath(window.LinkedGraph) : null;
                    string profilePath = window.ProfileGraph != null ? AssetDatabase.GetAssetPath(window.ProfileGraph) : null;

                    foreach (var path in importedAssets)
                    {
                        if ((!string.IsNullOrEmpty(ctrlPath) && path == ctrlPath) ||
                            (!string.IsNullOrEmpty(baseCtrlPath) && path == baseCtrlPath) ||
                            (!string.IsNullOrEmpty(linkedPath) && path == linkedPath) ||
                            (!string.IsNullOrEmpty(profilePath) && path == profilePath))
                        {
                            shouldReload = true;
                            break;
                        }
                    }

                    if (shouldReload)
                    {
                        window.ReloadCurrentGraph();
                    }
                }
            };
        }
    }
}
