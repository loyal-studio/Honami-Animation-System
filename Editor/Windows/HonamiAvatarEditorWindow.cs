using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Editor window for configuring and extracting skeleton bone structures 
    /// for the HonamiAvatar assets.
    /// </summary>
    public sealed class HonamiAvatarEditorWindow : EditorWindow
    {
        private HonamiAvatar _avatar;
        private GameObject _modelSource;
        private SerializedObject _so;

        private ScrollView _boneListView;
        private IMGUIContainer _dropZone;
        private Label _dropLabel;
        private VisualElement _mainContent;
        private Label _statusLabel;
        private string _searchString = "";
        private ObjectField _assetField;

        private enum Tab { Bones, MirrorBones }
        private Tab _currentTab = Tab.Bones;

        private sealed class BoneNode
        {
            public int index;
            public string path;
            public string name;
            public int depth;
            public bool isExpanded = true;
            public BoneNode parent;
            public List<BoneNode> children = new();
            public VisualElement element;
        }
        private List<BoneNode> _dfSNodes = new();

        private static readonly Color ColEnabled = HonamiGraphStyles.Green;
        private static readonly Color ColDisabled = HonamiGraphStyles.Red;
        private static readonly Color ColBg = HonamiGraphStyles.WindowBg;

        [MenuItem("Window/Honami/Honami Avatar Editor")]
        public static void Open() => GetWindow<HonamiAvatarEditorWindow>("Honami Avatar Editor");

        public static void OpenWithAsset(HonamiAvatar target)
        {
            var win = GetWindow<HonamiAvatarEditorWindow>("Honami Avatar Editor");
            win.LoadAvatar(target);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.EntityIdToObject(instanceID);
            if (obj is HonamiAvatar avatar)
            {
                OpenWithAsset(avatar);
                return true;
            }
            return false;
        }

        private void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = ColBg;
            rootVisualElement.style.paddingLeft = 12;
            rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12;
            rootVisualElement.style.paddingBottom = 12;

            var titleLabel = HonamiGraphStyles.Title("Honami Avatar Editor");
            titleLabel.style.marginBottom = 15;
            rootVisualElement.Add(titleLabel);

            var assetBox = HonamiGraphStyles.Box();
            assetBox.style.paddingTop = assetBox.style.paddingBottom = 10;
            assetBox.style.marginTop = 0;

            var assetRow = HonamiGraphStyles.Row();
            var assetLabel = new Label("Avatar Asset");
            assetLabel.style.width = 90;
            assetLabel.style.color = HonamiGraphStyles.GreyText;
            assetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            _assetField = new ObjectField { objectType = typeof(HonamiAvatar), allowSceneObjects = false };
            _assetField.style.flexGrow = 1;
            _assetField.value = _avatar;
            _assetField.RegisterValueChangedCallback(evt => LoadAvatar(evt.newValue as HonamiAvatar));

            var newBtn = new Button(CreateNewAvatar) { text = "+ New" };
            newBtn.style.width = 60;
            newBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            assetRow.Add(assetLabel);
            assetRow.Add(_assetField);
            assetRow.Add(newBtn);
            assetBox.Add(assetRow);
            rootVisualElement.Add(assetBox);

            _dropZone = BuildDropZone();
            rootVisualElement.Add(_dropZone);

            var extractRow = HonamiGraphStyles.Row();
            extractRow.style.justifyContent = Justify.Center;
            extractRow.style.marginTop = 2;
            extractRow.style.marginBottom = 8;
            var extractBtn = HonamiGraphStyles.TallButton("Extract Bones from Model", ExtractBonesFromModel);
            extractBtn.style.flexGrow = 1;
            extractRow.Add(extractBtn);

            var clearAllBonesBtn = HonamiGraphStyles.TallButton("Clear All Bones", ClearAllBones);
            clearAllBonesBtn.style.flexGrow = 0;
            clearAllBonesBtn.style.width = 120;
            clearAllBonesBtn.style.marginLeft = 4;
            extractRow.Add(clearAllBonesBtn);

            rootVisualElement.Add(extractRow);

            rootVisualElement.Add(HonamiGraphStyles.Separator());

            var tabsRow = HonamiGraphStyles.Row();
            tabsRow.style.marginTop = 6;
            tabsRow.style.marginBottom = 8;

            var bonesTabBtn = new Button(() => { _currentTab = Tab.Bones; RebuildBoneList(); }) { text = "Bones", name = "bonesTab" };
            var mirrorTabBtn = new Button(() => { _currentTab = Tab.MirrorBones; RebuildBoneList(); }) { text = "Mirror Bones", name = "mirrorTab" };
            bonesTabBtn.style.flexGrow = 1; mirrorTabBtn.style.flexGrow = 1;
            bonesTabBtn.style.height = 28; mirrorTabBtn.style.height = 28;
            bonesTabBtn.style.unityFontStyleAndWeight = mirrorTabBtn.style.unityFontStyleAndWeight = FontStyle.Bold;

            tabsRow.Add(bonesTabBtn);
            tabsRow.Add(mirrorTabBtn);
            rootVisualElement.Add(tabsRow);

            _mainContent = new VisualElement();
            _mainContent.style.flexGrow = 1;
            _mainContent.style.flexShrink = 1;
            rootVisualElement.Add(_mainContent);

            _statusLabel = new Label();
            _statusLabel.style.color = HonamiGraphStyles.GreyText;
            _statusLabel.style.fontSize = 10;
            _statusLabel.style.marginTop = 6;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            rootVisualElement.Add(_statusLabel);

            RebuildBoneList();
        }

        private GUIStyle _dropZoneStyle;

        private IMGUIContainer BuildDropZone()
        {
            var zone = new IMGUIContainer(() =>
            {
                var rect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
                _dropZoneStyle ??= new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                _dropZoneStyle.normal.textColor = HonamiGraphStyles.GreyText;
                var style = _dropZoneStyle;

                string dropText = _modelSource != null
                    ? $"Model: {_modelSource.name}"
                    : "Drop Model / Prefab here to Auto-Extract";

                GUI.Box(rect, dropText, style);

                var evt = Event.current;
                if (rect.Contains(evt.mousePosition))
                {
                    if (evt.type == EventType.DragUpdated)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use();
                    }
                    else if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go)
                            {
                                _modelSource = go;
                                SetStatus($"Model set: {go.name}");
                                break;
                            }
                        }
                        evt.Use();
                    }
                }
            });
            zone.style.marginTop = 4;
            zone.style.marginBottom = 4;
            zone.style.height = 52;
            zone.style.backgroundColor = HonamiGraphStyles.ListBoxBg;
            zone.style.borderTopColor = zone.style.borderBottomColor =
            zone.style.borderLeftColor = zone.style.borderRightColor = HonamiGraphStyles.ListBoxBorder;
            zone.style.borderTopWidth = zone.style.borderBottomWidth =
            zone.style.borderLeftWidth = zone.style.borderRightWidth = 1;
            zone.style.borderTopLeftRadius = zone.style.borderTopRightRadius =
            zone.style.borderBottomLeftRadius = zone.style.borderBottomRightRadius = 6;

            return zone;
        }

        private void LoadAvatar(HonamiAvatar target)
        {
            _avatar = target;
            _so = target != null ? new SerializedObject(target) : null;
            if (_assetField != null && _assetField.value != target)
            {
                _assetField.SetValueWithoutNotify(target);
            }
            RebuildBoneList();
        }

        private void RebuildBoneList()
        {
            _mainContent.Clear();

            if (_avatar == null)
            {
                _mainContent.Add(HonamiGraphStyles.MiniLabel("Select or create a HonamiAvatar asset above.", new Color(0.5f, 0.5f, 0.5f)));
                return;
            }

            var bonesTabBtn = rootVisualElement.Q<Button>(null, "bonesTab");
            var mirrorTabBtn = rootVisualElement.Q<Button>(null, "mirrorTab");
            if (bonesTabBtn != null && mirrorTabBtn != null)
            {
                bonesTabBtn.style.backgroundColor = _currentTab == Tab.Bones ? HonamiGraphStyles.Accent : new Color(0.24f, 0.25f, 0.26f);
                mirrorTabBtn.style.backgroundColor = _currentTab == Tab.MirrorBones ? HonamiGraphStyles.Accent : new Color(0.24f, 0.25f, 0.26f);
                bonesTabBtn.style.color = _currentTab == Tab.Bones ? Color.white : HonamiGraphStyles.GreyText;
                mirrorTabBtn.style.color = _currentTab == Tab.MirrorBones ? Color.white : HonamiGraphStyles.GreyText;
            }

            var toolsRow = HonamiGraphStyles.Row();
            toolsRow.style.marginTop = 2;
            toolsRow.style.marginBottom = 6;

            var searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.value = _searchString;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchString = evt.newValue;
                UpdateVisibility();
            });
            toolsRow.Add(searchField);

            if (_currentTab == Tab.Bones)
            {
                var enableAllBtn = new Button(() => SetAllEnabled(true)) { text = "Enable Flt" };
                enableAllBtn.style.height = 20;
                enableAllBtn.tooltip = "Enable all filtered bones";
                var disableAllBtn = new Button(() => SetAllEnabled(false)) { text = "Disable Flt" };
                disableAllBtn.style.height = 20;
                disableAllBtn.tooltip = "Disable all filtered bones";
                toolsRow.Add(enableAllBtn);
                toolsRow.Add(disableAllBtn);
            }
            else
            {
                var autoMirrorBtn = new Button(AutoBindMirrors) { text = "Auto Bind Mirrors" };
                autoMirrorBtn.style.height = 20;
                toolsRow.Add(autoMirrorBtn);

                var clearMirrorBtn = new Button(ClearMirrors) { text = "Clear All" };
                clearMirrorBtn.style.height = 20;
                toolsRow.Add(clearMirrorBtn);
            }
            _mainContent.Add(toolsRow);
            _mainContent.Add(HonamiGraphStyles.Separator());

            var headerRow = HonamiGraphStyles.Row();
            headerRow.style.marginBottom = 4;
            var hdrLabel = HonamiGraphStyles.SubTitle(_currentTab == Tab.Bones ? $"Bones  ({_avatar.bones.Count})" : $"Mirror Pairs  ({_avatar.mirrorBones.Count})");
            headerRow.Add(hdrLabel);
            headerRow.Add(HonamiGraphStyles.Spacer());

            var addBtn = new Button(_currentTab == Tab.Bones ? AddBoneManually : AddMirrorPair) { text = "+ Add" };
            addBtn.style.height = 20;
            headerRow.Add(addBtn);

            _mainContent.Add(headerRow);

            var colHdr = HonamiGraphStyles.Row();
            colHdr.style.paddingLeft = colHdr.style.paddingRight = 4;
            colHdr.style.marginBottom = 2;
            if (_currentTab == Tab.Bones)
            {
                AddColLabel(colHdr, "EN", 34);
                AddColLabel(colHdr, "Name", 160);
                AddColLabel(colHdr, "Path", 0, true);
            }
            else
            {
                colHdr.style.justifyContent = Justify.Center;
                AddColLabel(colHdr, "Bone A", 360, false);
                AddColLabel(colHdr, "<->", 30);
                AddColLabel(colHdr, "Bone B", 360, false);
                AddColLabel(colHdr, "", 22);
            }
            _mainContent.Add(colHdr);

            _boneListView = new ScrollView(ScrollViewMode.Vertical);
            _boneListView.style.flexGrow = 1;
            _boneListView.style.flexShrink = 1;
            _mainContent.Add(_boneListView);

            if (_currentTab == Tab.Bones)
            {
                BuildHierarchy();
                for (int i = 0; i < _dfSNodes.Count; i++)
                {
                    var row = BuildBoneRow(_dfSNodes[i], i);
                    _boneListView.Add(row);
                }
                UpdateVisibility();
            }
            else
            {
                for (int i = 0; i < _avatar.mirrorBones.Count; i++)
                {
                    var row = BuildMirrorRow(i);
                    _boneListView.Add(row);
                }
            }

            var saveRow = HonamiGraphStyles.Row();
            saveRow.style.justifyContent = Justify.Center;
            saveRow.style.marginTop = 10;
            saveRow.style.flexShrink = 0;
            var saveBtn = new Button(SaveAsset) { text = "Save Asset" };
            saveBtn.style.height = 28;
            saveBtn.style.paddingLeft = saveBtn.style.paddingRight = 14;
            saveBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            saveRow.Add(saveBtn);
            _mainContent.Add(saveRow);
        }

        private void BuildHierarchy()
        {
            _dfSNodes.Clear();
            if (_avatar == null) return;
            var dict = new Dictionary<string, BoneNode>();
            var roots = new List<BoneNode>();

            for (int i = 0; i < _avatar.bones.Count; i++)
            {
                var b = _avatar.bones[i];
                dict[b.bonePath] = new BoneNode { index = i, path = b.bonePath, name = b.boneName };
            }

            foreach (var kvp in dict)
            {
                var n = kvp.Value;
                int slash = n.path.LastIndexOf('/');
                if (slash >= 0)
                {
                    string p = n.path.Substring(0, slash);
                    if (dict.TryGetValue(p, out var pNode))
                    {
                        pNode.children.Add(n);
                        n.parent = pNode;
                        continue;
                    }
                }
                roots.Add(n);
            }

            void DFS(BoneNode n, int d)
            {
                n.depth = d;
                _dfSNodes.Add(n);
                foreach (var c in n.children) DFS(c, d + 1);
            }

            foreach (var r in roots) DFS(r, 0);
        }

        private VisualElement BuildBoneRow(BoneNode node, int displayIndex)
        {
            int i = node.index;
            var entry = _avatar.bones[i];
            var row = new VisualElement();
            node.element = row;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = displayIndex % 2 == 0 ? new Color(0.18f, 0.19f, 0.21f) : new Color(0.15f, 0.16f, 0.18f);
            row.style.borderBottomColor = new Color(0.12f, 0.12f, 0.13f);
            row.style.borderBottomWidth = 1;
            row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.paddingLeft = row.style.paddingRight = 8;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius =
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;
            row.style.marginBottom = 2;

            var indent = new VisualElement();
            indent.style.width = node.depth * 14;
            row.Add(indent);

            var expandBtn = new Button();
            expandBtn.style.width = 16;
            expandBtn.style.height = 16;
            expandBtn.style.backgroundColor = Color.clear;
            expandBtn.style.borderTopWidth = expandBtn.style.borderBottomWidth =
            expandBtn.style.borderLeftWidth = expandBtn.style.borderRightWidth = 0;
            expandBtn.style.paddingLeft = expandBtn.style.paddingRight = 0;
            expandBtn.style.paddingTop = expandBtn.style.paddingBottom = 0;
            expandBtn.style.fontSize = 10;
            expandBtn.name = "expandBtn";
            if (node.children.Count > 0)
            {
                expandBtn.text = node.isExpanded ? HonamiEditorSymbols.Collapse : HonamiEditorSymbols.Expand;
                expandBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    bool targetState = !node.isExpanded;
                    if (evt.altKey)
                    {
                        void SetExpandedRec(BoneNode n, bool s)
                        {
                            n.isExpanded = s;
                            if (n.element != null)
                            {
                                var btn = n.element.Q<Button>("expandBtn");
                                if (btn != null) btn.text = s ? HonamiEditorSymbols.Collapse : HonamiEditorSymbols.Expand;
                            }
                            foreach (var c in n.children) SetExpandedRec(c, s);
                        }
                        SetExpandedRec(node, targetState);
                    }
                    else
                    {
                        node.isExpanded = targetState;
                        expandBtn.text = node.isExpanded ? HonamiEditorSymbols.Collapse : HonamiEditorSymbols.Expand;
                    }
                    UpdateVisibility();
                });
            }
            row.Add(expandBtn);

            int captured = i;
            var toggleContainer = new VisualElement();
            toggleContainer.style.width = 30;
            toggleContainer.style.alignItems = Align.Center;

            var nameField = new TextField { value = entry.boneName };
            nameField.style.width = 155;
            nameField.style.marginLeft = 4;
            nameField.style.fontSize = 11;
            nameField.style.color = entry.enabled ? HonamiGraphStyles.TitleClr : HonamiGraphStyles.GreyText;
            nameField.style.unityFontStyleAndWeight = entry.enabled ? FontStyle.Bold : FontStyle.Normal;
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_avatar, "Rename Bone");
                _avatar.bones[captured].boneName = evt.newValue;
                EditorUtility.SetDirty(_avatar);
            });

            var enabledBtn = new Button();
            enabledBtn.style.width = 22;
            enabledBtn.style.height = 22;
            enabledBtn.style.borderTopLeftRadius = enabledBtn.style.borderTopRightRadius =
            enabledBtn.style.borderBottomLeftRadius = enabledBtn.style.borderBottomRightRadius = 11;
            enabledBtn.style.paddingLeft = enabledBtn.style.paddingRight = 0;
            enabledBtn.style.paddingTop = enabledBtn.style.paddingBottom = 0;
            enabledBtn.style.fontSize = 10;
            enabledBtn.tooltip = "Click to toggle\nShift-Click to toggle this and all children";
            RefreshToggleBtn(enabledBtn, entry.enabled);

            enabledBtn.RegisterCallback<ClickEvent>(evt =>
            {
                bool newState = !_avatar.bones[captured].enabled;
                if (evt.shiftKey)
                {
                    SetChildrenEnabled(_avatar.bones[captured].bonePath, newState);
                }
                else
                {
                    Undo.RecordObject(_avatar, "Toggle Bone");
                    _avatar.bones[captured].enabled = newState;
                    EditorUtility.SetDirty(_avatar);
                    RefreshToggleBtn(enabledBtn, newState);
                    row.style.opacity = newState ? 1f : 0.5f;
                    nameField.style.color = newState ? HonamiGraphStyles.TitleClr : HonamiGraphStyles.GreyText;
                }
            });
            toggleContainer.Add(enabledBtn);
            row.Add(toggleContainer);

            row.style.opacity = entry.enabled ? 1f : 0.5f;
            row.Add(nameField);

            var pathLabel = new Label(entry.bonePath);
            pathLabel.style.flexGrow = 1;
            pathLabel.style.flexShrink = 1;
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            pathLabel.style.marginLeft = 8;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            row.Add(pathLabel);

            var delBtn = new Button(() =>
            {
                Undo.RecordObject(_avatar, "Remove Bone");
                _avatar.bones.RemoveAt(captured);
                EditorUtility.SetDirty(_avatar);
                RebuildBoneList();
            })
            { text = HonamiEditorSymbols.Remove };
            delBtn.style.width = 22;
            delBtn.style.height = 22;
            delBtn.style.fontSize = 11;
            delBtn.style.paddingLeft = delBtn.style.paddingRight = 0;
            delBtn.style.paddingTop = delBtn.style.paddingBottom = 0;
            delBtn.style.color = new Color(0.9f, 0.4f, 0.4f);
            delBtn.style.borderTopLeftRadius = delBtn.style.borderTopRightRadius =
            delBtn.style.borderBottomLeftRadius = delBtn.style.borderBottomRightRadius = 4;
            row.Add(delBtn);

            row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                menuEvent.menu.AppendAction("Enable This & Children", x => SetChildrenEnabled(entry.bonePath, true));
                menuEvent.menu.AppendAction("Disable This & Children", x => SetChildrenEnabled(entry.bonePath, false));
            }));

            return row;
        }

        private VisualElement BuildMirrorRow(int index)
        {
            var entry = _avatar.mirrorBones[index];
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.backgroundColor = index % 2 == 0 ? new Color(0.18f, 0.19f, 0.21f) : new Color(0.15f, 0.16f, 0.18f);
            row.style.borderBottomColor = new Color(0.12f, 0.12f, 0.13f);
            row.style.borderBottomWidth = 1;
            row.style.paddingTop = row.style.paddingBottom = 6;
            row.style.paddingLeft = row.style.paddingRight = 8;
            row.style.marginBottom = 2;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius =
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

            var allPaths = new List<string> { "None" };
            for (int i = 0; i < _avatar.bones.Count; i++) allPaths.Add(_avatar.bones[i].bonePath);

            string valA = string.IsNullOrEmpty(entry.boneA) ? "None" : entry.boneA;
            if (!allPaths.Contains(valA)) allPaths.Add(valA);

            var dropA = new PopupField<string>(allPaths, valA, FormatBonePathDisplay, FormatBonePathDisplay);
            dropA.style.flexGrow = 1;
            dropA.style.flexShrink = 1;
            dropA.style.maxWidth = 360;
            dropA.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_avatar, "Set Mirror Bone A");
                _avatar.mirrorBones[index].boneA = evt.newValue == "None" ? "" : evt.newValue;
                EditorUtility.SetDirty(_avatar);
            });
            row.Add(dropA);

            var separatorLabel = new Label("<->");
            separatorLabel.style.width = 24;
            separatorLabel.style.flexShrink = 0;
            separatorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            separatorLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row.Add(separatorLabel);

            string valB = string.IsNullOrEmpty(entry.boneB) ? "None" : entry.boneB;
            if (!allPaths.Contains(valB)) allPaths.Add(valB);
            var dropB = new PopupField<string>(allPaths, valB, FormatBonePathDisplay, FormatBonePathDisplay);
            dropB.style.flexGrow = 1;
            dropB.style.flexShrink = 1;
            dropB.style.maxWidth = 360;
            dropB.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_avatar, "Set Mirror Bone B");
                _avatar.mirrorBones[index].boneB = evt.newValue == "None" ? "" : evt.newValue;
                EditorUtility.SetDirty(_avatar);
            });
            row.Add(dropB);

            var spacer = HonamiGraphStyles.Spacer();
            spacer.style.width = 6;
            spacer.style.flexGrow = 0;
            row.Add(spacer);

            var delBtn = new Button(() =>
            {
                Undo.RecordObject(_avatar, "Remove Mirror Pair");
                _avatar.mirrorBones.RemoveAt(index);
                EditorUtility.SetDirty(_avatar);
                RebuildBoneList();
            })
            { text = HonamiEditorSymbols.Remove };
            delBtn.style.width = 22;
            delBtn.style.height = 22;
            delBtn.style.fontSize = 11;
            delBtn.style.paddingLeft = delBtn.style.paddingRight = 0;
            delBtn.style.paddingTop = delBtn.style.paddingBottom = 0;
            delBtn.style.color = new Color(0.9f, 0.4f, 0.4f);
            delBtn.style.borderTopLeftRadius = delBtn.style.borderTopRightRadius =
            delBtn.style.borderBottomLeftRadius = delBtn.style.borderBottomRightRadius = 4;
            row.Add(delBtn);

            return row;
        }

        private string FormatBonePathDisplay(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "None") return "None";

            var parts = path.Split('/');
            if (parts.Length <= 3) return path;

            return ".../" + parts[parts.Length - 3] + "/" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
        }

        private static void RefreshToggleBtn(Button btn, bool enabled)
        {
            btn.text = enabled ? HonamiEditorSymbols.Enabled : HonamiEditorSymbols.Disabled;
            btn.style.backgroundColor = enabled ? ColEnabled : ColDisabled;
        }

        private static void AddColLabel(VisualElement row, string text, float width, bool grow = false)
        {
            var l = new Label(text);
            l.style.fontSize = 10;
            l.style.color = new Color(0.5f, 0.5f, 0.5f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (grow) l.style.flexGrow = 1;
            else l.style.width = width;
            row.Add(l);
        }

        private void UpdateVisibility()
        {
            if (_avatar == null || _dfSNodes == null) return;
            bool isSearching = !string.IsNullOrEmpty(_searchString);
            string q = _searchString?.ToLower() ?? "";

            for (int i = 0; i < _dfSNodes.Count; i++)
            {
                var n = _dfSNodes[i];
                bool visible = true;

                if (isSearching)
                {
                    visible = (n.name != null && n.name.ToLower().Contains(q)) ||
                              (n.path != null && n.path.ToLower().Contains(q));
                }
                else
                {
                    var p = n.parent;
                    while (p != null)
                    {
                        if (!p.isExpanded)
                        {
                            visible = false;
                            break;
                        }
                        p = p.parent;
                    }
                }

                if (n.element != null)
                {
                    n.element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void SetAllEnabled(bool state)
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, state ? "Enable Filtered Bones" : "Disable Filtered Bones");
            string q = _searchString?.ToLower() ?? "";
            int c = 0;
            for (int i = 0; i < _avatar.bones.Count; i++)
            {
                var b = _avatar.bones[i];
                bool match = string.IsNullOrEmpty(q) ||
                             (b.boneName != null && b.boneName.ToLower().Contains(q)) ||
                             (b.bonePath != null && b.bonePath.ToLower().Contains(q));
                if (match)
                {
                    b.enabled = state;
                    c++;
                }
            }
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus($"{(state ? "Enabled" : "Disabled")} {c} bones.");
        }

        private void SetChildrenEnabled(string parentPath, bool state)
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, state ? "Enable Children" : "Disable Children");
            int c = 0;
            for (int i = 0; i < _avatar.bones.Count; i++)
            {
                var b = _avatar.bones[i];
                if (b.bonePath == parentPath || b.bonePath.StartsWith(parentPath + "/"))
                {
                    b.enabled = state;
                    c++;
                }
            }
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus($"{(state ? "Enabled" : "Disabled")} {c} bones in hierarchy.");
        }

        private void ExtractBonesFromModel()
        {
            if (_modelSource == null)
            {
                SetStatus("Drop a model/prefab first.");
                return;
            }
            if (_avatar == null)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_modelSource);
                if (string.IsNullOrEmpty(assetPath)) assetPath = AssetDatabase.GetAssetPath(_modelSource);

                string dir = "Assets";
                if (!string.IsNullOrEmpty(assetPath))
                {
                    int lastSlash = assetPath.LastIndexOf('/');
                    if (lastSlash >= 0) dir = assetPath.Substring(0, lastSlash);
                }

                string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{_modelSource.name}_Avatar.asset");
                var newAvatar = CreateInstance<HonamiAvatar>();
                AssetDatabase.CreateAsset(newAvatar, targetPath);
                AssetDatabase.SaveAssets();
                LoadAvatar(newAvatar);
                SetStatus($"Auto-created avatar at {targetPath}");
            }

            var allTransforms = _modelSource.GetComponentsInChildren<Transform>(true);
            Transform root = _modelSource.transform;

            Undo.RecordObject(_avatar, "Extract Bones");

            var existingPaths = new HashSet<string>();
            for (int i = 0; i < _avatar.bones.Count; i++)
                existingPaths.Add(_avatar.bones[i].bonePath);

            int added = 0;
            foreach (var t in allTransforms)
            {
                if (t == root) continue;
                string path = GetRelativePath(root, t);
                if (existingPaths.Contains(path)) continue;

                _avatar.bones.Add(new HonamiAvatar.BoneEntry
                {
                    boneName = t.name,
                    bonePath = path,
                    enabled = true
                });
                added++;
            }

            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus($"Extracted {added} new bones. Total: {_avatar.bones.Count}");
        }

        private void ClearAllBones()
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, "Clear All Bones");
            _avatar.bones.Clear();
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus("Cleared all bones.");
        }

        private void AddBoneManually()
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, "Add Bone");
            _avatar.bones.Add(new HonamiAvatar.BoneEntry { boneName = "new_bone", bonePath = "", enabled = true });
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
        }

        private void AddMirrorPair()
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, "Add Mirror Pair");
            _avatar.mirrorBones.Add(new HonamiAvatar.MirrorEntry { boneA = "", boneB = "" });
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
        }

        private void CreateNewAvatar()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create HonamiAvatar", "NewHonamiAvatar", "asset", "Choose save location");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<HonamiAvatar>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            LoadAvatar(asset);
        }

        private void SaveAsset()
        {
            if (_avatar == null) return;
            EditorUtility.SetDirty(_avatar);
            AssetDatabase.SaveAssets();
            SetStatus("Saved.");
        }

        private void AutoBindMirrors()
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, "Auto Bind Mirrors");

            var existingSet = new HashSet<string>();
            foreach (var entry in _avatar.mirrorBones)
            {
                if (!string.IsNullOrEmpty(entry.boneA)) existingSet.Add(entry.boneA);
                if (!string.IsNullOrEmpty(entry.boneB)) existingSet.Add(entry.boneB);
            }

            int addedPairs = 0;
            for (int i = 0; i < _avatar.bones.Count; i++)
            {
                var b = _avatar.bones[i];
                if (!existingSet.Contains(b.bonePath))
                {
                    string m = FindMirrorBone(b.boneName, b.bonePath);
                    if (!string.IsNullOrEmpty(m) && !existingSet.Contains(m))
                    {
                        _avatar.mirrorBones.Add(new HonamiAvatar.MirrorEntry { boneA = b.bonePath, boneB = m });
                        existingSet.Add(b.bonePath);
                        existingSet.Add(m);
                        addedPairs++;
                    }
                }
            }
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus($"Auto-bound {addedPairs} mirror pairs.");
        }

        private void ClearMirrors()
        {
            if (_avatar == null) return;
            Undo.RecordObject(_avatar, "Clear Mirrors");
            _avatar.mirrorBones.Clear();
            EditorUtility.SetDirty(_avatar);
            RebuildBoneList();
            SetStatus("Cleared mirror bones.");
        }

        private string FindMirrorBone(string name, string path)
        {
            string searchName = name.ToLower();
            string mirrorName = null;

            if (searchName.EndsWith("_l")) mirrorName = name.Substring(0, name.Length - 2) + "_r";
            else if (searchName.EndsWith("_r")) mirrorName = name.Substring(0, name.Length - 2) + "_l";
            else if (searchName.EndsWith(".l")) mirrorName = name.Substring(0, name.Length - 2) + ".r";
            else if (searchName.EndsWith(".r")) mirrorName = name.Substring(0, name.Length - 2) + ".l";
            else if (searchName.StartsWith("left")) mirrorName = "Right" + name.Substring(4);
            else if (searchName.StartsWith("right")) mirrorName = "Left" + name.Substring(5);
            else if (searchName.StartsWith("l_")) mirrorName = "R_" + name.Substring(2);
            else if (searchName.StartsWith("r_")) mirrorName = "L_" + name.Substring(2);

            if (mirrorName != null)
            {
                for (int i = 0; i < _avatar.bones.Count; i++)
                {
                    if (_avatar.bones[i].boneName.Equals(mirrorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return _avatar.bones[i].bonePath;
                    }
                }
            }
            return "";
        }

        private void SetStatus(string msg) => _statusLabel.text = msg;

        private static string GetRelativePath(Transform root, Transform bone)
        {
            if (bone == root) return "";
            string path = bone.name;
            Transform p = bone.parent;
            while (p != null && p != root)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }
    }
}
