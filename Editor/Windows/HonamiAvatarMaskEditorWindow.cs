using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Editor window for creating and modifying HonamiAvatarMask assets.
    /// Allows masking specific bones or IK effectors for layered animations.
    /// </summary>
    public sealed class HonamiAvatarMaskEditorWindow : EditorWindow
    {
        private HonamiAvatarMask _mask;
        private SerializedObject _so;

        private VisualElement _mainContent;
        private Label _statusLabel;
        private string _searchString = "";
        private ObjectField _assetField;
        private bool _isDraggingSlider;
        private bool _symmetryEnabled = false;
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

        private static readonly Color ColBg = HonamiGraphStyles.WindowBg;
        private static readonly Color ColFull = HonamiGraphStyles.Green;
        private static readonly Color ColHalf = HonamiGraphStyles.Orange;
        private static readonly Color ColNone = HonamiGraphStyles.Red;
        private static readonly Color ColTrack = new(0.18f, 0.18f, 0.20f, 1f);

        [MenuItem("Window/Honami/Honami Avatar Mask Editor")]
        public static void Open() => GetWindow<HonamiAvatarMaskEditorWindow>("Honami Mask Editor");

        public static void OpenWithAsset(HonamiAvatarMask target)
        {
            var win = GetWindow<HonamiAvatarMaskEditorWindow>("Honami Mask Editor");
            win.LoadMask(target);
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
#if UNITY_6000_6_OR_NEWER
        public static bool OnOpenAsset(EntityId entityId, int line)
        {
            var obj = EditorUtility.EntityIdToObject(entityId);
#else
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
#endif
            if (obj is HonamiAvatarMask mask)
            {
                OpenWithAsset(mask);
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

            var titleLabel = HonamiGraphStyles.Title("Honami Avatar Mask Editor");
            titleLabel.style.marginBottom = 15;
            rootVisualElement.Add(titleLabel);

            rootVisualElement.RegisterCallback<PointerUpEvent>(evt => _isDraggingSlider = false, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerLeaveEvent>(evt => _isDraggingSlider = false);

            var assetBox = HonamiGraphStyles.Box();
            assetBox.style.paddingTop = assetBox.style.paddingBottom = 10;
            assetBox.style.marginTop = 0;

            var maskRow = HonamiGraphStyles.Row();
            var maskLabel = new Label("Mask Asset");
            maskLabel.style.width = 90;
            maskLabel.style.color = HonamiGraphStyles.GreyText;
            maskLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _assetField = new ObjectField { objectType = typeof(HonamiAvatarMask), allowSceneObjects = false };
            _assetField.style.flexGrow = 1;
            _assetField.value = _mask;
            _assetField.RegisterValueChangedCallback(evt => LoadMask(evt.newValue as HonamiAvatarMask));
            var newMaskBtn = new Button(CreateNewMask) { text = "+ New" };
            newMaskBtn.style.width = 60;
            newMaskBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            maskRow.Add(maskLabel);
            maskRow.Add(_assetField);
            maskRow.Add(newMaskBtn);
            assetBox.Add(maskRow);
            rootVisualElement.Add(assetBox);

            var toolsRow = HonamiGraphStyles.Row();
            toolsRow.style.marginTop = 6;
            toolsRow.style.marginBottom = 8;
            var searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchString = evt.newValue;
                UpdateVisibility();
            });
            toolsRow.Add(searchField);

            var symmetryToggle = new Toggle("Symmetry") { value = _symmetryEnabled };
            symmetryToggle.style.marginLeft = 10;
            symmetryToggle.style.width = 90;
            symmetryToggle.style.alignSelf = Align.Center;
            symmetryToggle.RegisterValueChangedCallback(evt => _symmetryEnabled = evt.newValue);
            toolsRow.Add(symmetryToggle);

            var allBtn = new Button(() => SetFilteredWeights(1f)) { text = "All = 1.0" };
            allBtn.style.height = 20;
            allBtn.style.backgroundColor = new Color(0.20f, 0.55f, 0.35f);
            allBtn.style.color = Color.white;
            allBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolsRow.Add(allBtn);

            var noneBtn = new Button(() => SetFilteredWeights(0f)) { text = "None = 0.0" };
            noneBtn.style.height = 20;
            noneBtn.style.backgroundColor = new Color(0.55f, 0.20f, 0.20f);
            noneBtn.style.color = Color.white;
            noneBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolsRow.Add(noneBtn);

            rootVisualElement.Add(toolsRow);
            rootVisualElement.Add(HonamiGraphStyles.Separator());

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

            RebuildMaskUI();
        }

        private void LoadMask(HonamiAvatarMask target)
        {
            _mask = target;
            _so = target != null ? new SerializedObject(target) : null;
            if (_assetField != null && _assetField.value != target)
            {
                _assetField.SetValueWithoutNotify(target);
            }
            if (_mask != null && _mask.avatar != null) _mask.SyncWithAvatar();
            RebuildMaskUI();
        }

        private void RebuildMaskUI()
        {
            _mainContent.Clear();

            HonamiAvatar currentAvatar = _mask != null ? _mask.avatar : null;
            var avatarRow = HonamiGraphStyles.Row();
            avatarRow.style.marginBottom = 10;
            var avatarLabel = new Label("Avatar");
            avatarLabel.style.width = 90;
            avatarLabel.style.color = HonamiGraphStyles.GreyText;
            avatarLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            var avatarField = new ObjectField { objectType = typeof(HonamiAvatar), allowSceneObjects = false };
            avatarField.style.flexGrow = 1;
            avatarField.value = currentAvatar;

            avatarField.RegisterValueChangedCallback(evt =>
            {
                var newAvatar = evt.newValue as HonamiAvatar;
                if (_mask == null && newAvatar != null)
                {
                    string avatarPath = AssetDatabase.GetAssetPath(newAvatar);
                    string dir = "Assets";
                    if (!string.IsNullOrEmpty(avatarPath))
                    {
                        int lastSlash = avatarPath.LastIndexOf('/');
                        if (lastSlash >= 0) dir = avatarPath.Substring(0, lastSlash);
                    }
                    string newName = newAvatar.name + "_Mask.asset";
                    if (newName.EndsWith("_Avatar_Mask.asset")) newName = newName.Replace("_Avatar_Mask.asset", "_Mask.asset");

                    string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{newName}");
                    var newMask = CreateInstance<HonamiAvatarMask>();
                    newMask.avatar = newAvatar;
                    AssetDatabase.CreateAsset(newMask, targetPath);
                    AssetDatabase.SaveAssets();

                    newMask.SyncWithAvatar();
                    EditorUtility.SetDirty(newMask);
                    AssetDatabase.SaveAssets();

                    LoadMask(newMask);
                    SetStatus($"Auto-created mask at {targetPath}");
                    return;
                }

                if (_mask != null)
                {
                    Undo.RecordObject(_mask, "Set Avatar");
                    _mask.avatar = newAvatar;
                    EditorUtility.SetDirty(_mask);
                    if (_mask.avatar != null)
                    {
                        _mask.SyncWithAvatar();
                        EditorUtility.SetDirty(_mask);
                    }
                    RebuildMaskUI();
                }
            });

            avatarRow.Add(avatarLabel);
            avatarRow.Add(avatarField);

            if (_mask != null)
            {
                var syncBtn = new Button(() =>
                {
                    if (_mask.avatar == null) { SetStatus("No avatar assigned."); return; }
                    Undo.RecordObject(_mask, "Sync with Avatar");
                    _mask.SyncWithAvatar();
                    EditorUtility.SetDirty(_mask);
                    RebuildMaskUI();
                    SetStatus("Synchronized with avatar.");
                })
                { text = "Sync" };
                syncBtn.style.width = 60;
                syncBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                avatarRow.Add(syncBtn);
            }

            _mainContent.Add(avatarRow);

            if (_mask == null)
            {
                var hint = HonamiGraphStyles.MiniLabel("Select Mask to edit, or assign an Avatar above to auto-create.", new Color(0.6f, 0.5f, 0.3f));
                hint.style.marginTop = 6;
                _mainContent.Add(hint);
                return;
            }

            if (_mask.avatar == null)
            {
                _mainContent.Add(HonamiGraphStyles.MiniLabel("Assign an Avatar and press Sync.", new Color(0.6f, 0.5f, 0.3f)));
                return;
            }

            _mainContent.Add(HonamiGraphStyles.Separator());

            if (_mask.boneWeights.Count == 0)
            {
                var hint = HonamiGraphStyles.MiniLabel("No bones - press Sync to populate from Avatar.", new Color(0.6f, 0.5f, 0.3f));
                hint.style.marginTop = 6;
                _mainContent.Add(hint);
            }
            else
            {
                BuildWeightList();
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

        private void BuildWeightList()
        {
            var headerRow = HonamiGraphStyles.Row();
            headerRow.style.paddingLeft = headerRow.style.paddingRight = 6;
            headerRow.style.marginBottom = 2;
            AddColLabel(headerRow, "Bone", 170);
            AddColLabel(headerRow, "Weight", 0, true);
            AddColLabel(headerRow, "Val", 52);
            _mainContent.Add(headerRow);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;

            BuildHierarchy();

            for (int i = 0; i < _dfSNodes.Count; i++)
            {
                var row = BuildWeightRow(_dfSNodes[i], i);
                scroll.Add(row);
            }
            UpdateVisibility();

            _mainContent.Add(scroll);
        }

        private void BuildHierarchy()
        {
            _dfSNodes.Clear();
            if (_mask == null) return;
            var dict = new Dictionary<string, BoneNode>();
            var roots = new List<BoneNode>();

            for (int i = 0; i < _mask.boneWeights.Count; i++)
            {
                var bw = _mask.boneWeights[i];
                string dName = GetBoneDisplayName(bw.bonePath);
                dict[bw.bonePath] = new BoneNode { index = i, path = bw.bonePath, name = dName };
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

        private VisualElement BuildWeightRow(BoneNode node, int displayIndex)
        {
            int i = node.index;
            var bw = _mask.boneWeights[i];
            bool active = IsActiveInAvatar(bw.bonePath);

            var row = new VisualElement();
            node.element = row;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = displayIndex % 2 == 0 ? new Color(0.18f, 0.19f, 0.21f) : new Color(0.15f, 0.16f, 0.18f);
            row.style.borderBottomColor = new Color(0.12f, 0.12f, 0.13f);
            row.style.borderBottomWidth = 1;
            row.style.paddingTop = row.style.paddingBottom = 7;
            row.style.paddingLeft = row.style.paddingRight = 8;
            row.style.marginBottom = 2;
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius =
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;
            row.style.opacity = active ? 1f : 0.45f;

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

            string displayName = GetBoneDisplayName(bw.bonePath);

            var nameLabel = new Label(displayName);
            nameLabel.style.width = 165;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = active ? HonamiGraphStyles.TitleClr : HonamiGraphStyles.GreyText;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.unityTextOverflowPosition = TextOverflowPosition.End;
            row.Add(nameLabel);

            var trackContainer = new VisualElement();
            trackContainer.style.flexGrow = 1;
            trackContainer.style.height = 20;
            trackContainer.style.backgroundColor = ColTrack;
            trackContainer.style.borderTopLeftRadius = trackContainer.style.borderTopRightRadius =
            trackContainer.style.borderBottomLeftRadius = trackContainer.style.borderBottomRightRadius = 10;
            trackContainer.style.marginLeft = 8;
            trackContainer.style.marginRight = 8;
            trackContainer.style.overflow = Overflow.Hidden;

            var fill = new VisualElement();
            fill.name = "fill";
            fill.style.height = 20;
            fill.style.backgroundColor = WeightToColor(bw.weight);
            fill.style.width = Length.Percent(bw.weight * 100f);
            fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius =
            fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 10;

            var shine = new VisualElement();
            shine.style.position = Position.Absolute;
            shine.style.top = 2;
            shine.style.left = 4;
            shine.style.right = 4;
            shine.style.height = 8;
            shine.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            shine.style.borderTopLeftRadius = shine.style.borderTopRightRadius = 4;
            fill.Add(shine);

            trackContainer.Add(fill);
            row.Add(trackContainer);

            int captured = i;

            if (active)
            {
                var slider = new Slider(0f, 1f) { value = bw.weight, name = "slider" };
                slider.style.flexGrow = 1;
                slider.style.marginLeft = 0;
                slider.style.marginRight = 0;
                slider.style.display = DisplayStyle.None;

                slider.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_mask, "Set Bone Weight");
                    SetWeightWithSymmetry(captured, evt.newValue);
                    EditorUtility.SetDirty(_mask);
                });

                trackContainer.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (!active) return;
                    _isDraggingSlider = true;
                    float t = Mathf.Clamp01(evt.localPosition.x / trackContainer.resolvedStyle.width);
                    Undo.RecordObject(_mask, "Set Bone Weight");
                    SetWeightWithSymmetry(captured, t);
                    EditorUtility.SetDirty(_mask);
                });

                trackContainer.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!active || !_isDraggingSlider || (evt.pressedButtons & 1) == 0) return;
                    float t = Mathf.Clamp01(evt.localPosition.x / trackContainer.resolvedStyle.width);
                    Undo.RecordObject(_mask, "Set Bone Weight");
                    SetWeightWithSymmetry(captured, t);
                    EditorUtility.SetDirty(_mask);
                });
            }

            var valLabel = new Label($"{bw.weight:F2}");
            valLabel.name = "val";
            valLabel.style.width = 36;
            valLabel.style.fontSize = 10;
            valLabel.style.color = WeightToColor(bw.weight);
            valLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valLabel);

            if (active)
            {
                var allBtn = new Button();
                allBtn.text = "All";
                allBtn.tooltip = "Click to set 1.0\nShift-Click to set 1.0 for this and all children";
                allBtn.style.width = 32;
                allBtn.style.height = 18;
                allBtn.style.fontSize = 9;
                allBtn.style.paddingLeft = allBtn.style.paddingRight = 2;
                allBtn.style.paddingTop = allBtn.style.paddingBottom = 0;
                allBtn.style.marginLeft = 3;
                allBtn.style.backgroundColor = new Color(0.24f, 0.25f, 0.26f);
                allBtn.style.color = Color.white;
                allBtn.style.borderTopLeftRadius = allBtn.style.borderTopRightRadius =
                allBtn.style.borderBottomLeftRadius = allBtn.style.borderBottomRightRadius = 4;
                allBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.shiftKey)
                    {
                        SetChildrenWeight(_mask.boneWeights[captured].bonePath, 1f);
                    }
                    else
                    {
                        Undo.RecordObject(_mask, "Set Bone Weight");
                        SetWeightWithSymmetry(captured, 1f);
                        EditorUtility.SetDirty(_mask);
                    }
                });
                row.Add(allBtn);

                var noneBtn = new Button();
                noneBtn.text = "None";
                noneBtn.tooltip = "Click to set 0.0\nShift-Click to set 0.0 for this and all children";
                noneBtn.style.width = 36;
                noneBtn.style.height = 18;
                noneBtn.style.fontSize = 9;
                noneBtn.style.paddingLeft = noneBtn.style.paddingRight = 2;
                noneBtn.style.paddingTop = noneBtn.style.paddingBottom = 0;
                noneBtn.style.marginLeft = 2;
                noneBtn.style.backgroundColor = new Color(0.24f, 0.25f, 0.26f);
                noneBtn.style.color = Color.white;
                noneBtn.style.borderTopLeftRadius = noneBtn.style.borderTopRightRadius =
                noneBtn.style.borderBottomLeftRadius = noneBtn.style.borderBottomRightRadius = 4;
                noneBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.shiftKey)
                    {
                        SetChildrenWeight(_mask.boneWeights[captured].bonePath, 0f);
                    }
                    else
                    {
                        Undo.RecordObject(_mask, "Set Bone Weight");
                        SetWeightWithSymmetry(captured, 0f);
                        EditorUtility.SetDirty(_mask);
                    }
                });
                row.Add(noneBtn);
            }

            row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                menuEvent.menu.AppendAction("Set 1.0 (This & Children)", x => SetChildrenWeight(bw.bonePath, 1f));
                menuEvent.menu.AppendAction("Set 0.5 (This & Children)", x => SetChildrenWeight(bw.bonePath, 0.5f));
                menuEvent.menu.AppendAction("Set 0.0 (This & Children)", x => SetChildrenWeight(bw.bonePath, 0f));
            }));

            return row;
        }

        private void SetWeightWithSymmetry(int idx, float w)
        {
            if (_mask == null || idx < 0 || idx >= _mask.boneWeights.Count) return;
            _mask.boneWeights[idx].weight = w;
            UpdateRowVisuals(idx, w);

            if (_symmetryEnabled && _mask.avatar != null)
            {
                string path = _mask.boneWeights[idx].bonePath;
                string mirrorPath = _mask.avatar.GetMirrorPath(path);
                if (!string.IsNullOrEmpty(mirrorPath))
                {
                    int mIdx = _mask.boneWeights.FindIndex(b => b.bonePath == mirrorPath);
                    if (mIdx >= 0)
                    {
                        _mask.boneWeights[mIdx].weight = w;
                        UpdateRowVisuals(mIdx, w);
                    }
                }
            }
        }

        private void UpdateRowVisuals(int index, float t)
        {
            var node = _dfSNodes.Find(n => n.index == index);
            if (node == null || node.element == null) return;

            var fill = node.element.Q<VisualElement>("fill");
            if (fill != null)
            {
                fill.style.backgroundColor = WeightToColor(t);
                fill.style.width = Length.Percent(t * 100f);
            }

            var slider = node.element.Q<Slider>("slider");
            if (slider != null) slider.SetValueWithoutNotify(t);

            UpdateValLabel(node.element, t);
        }

        private void UpdateVisibility()
        {
            if (_mask == null || _dfSNodes == null) return;
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

        private void SetFilteredWeights(float value)
        {
            if (_mask == null) return;
            Undo.RecordObject(_mask, value > 0.5f ? "Set Filtered Weights 1" : "Set Filtered Weights 0");
            string q = _searchString?.ToLower() ?? "";
            int c = 0;
            for (int i = 0; i < _mask.boneWeights.Count; i++)
            {
                var bw = _mask.boneWeights[i];
                string displayName = GetBoneDisplayName(bw.bonePath).ToLower();
                string path = bw.bonePath != null ? bw.bonePath.ToLower() : "";
                bool match = string.IsNullOrEmpty(q) || displayName.Contains(q) || path.Contains(q);
                if (match)
                {
                    _mask.boneWeights[i].weight = value;
                    c++;
                }
            }
            EditorUtility.SetDirty(_mask);
            RebuildMaskUI();
            SetStatus($"Updated {c} filtered bones.");
        }

        private void SetChildrenWeight(string parentPath, float weight)
        {
            if (_mask == null) return;
            Undo.RecordObject(_mask, "Set Children Weight");
            int c = 0;
            for (int i = 0; i < _mask.boneWeights.Count; i++)
            {
                var bw = _mask.boneWeights[i];
                if (bw.bonePath == parentPath || bw.bonePath.StartsWith(parentPath + "/"))
                {
                    bw.weight = weight;
                    c++;
                }
            }
            EditorUtility.SetDirty(_mask);
            RebuildMaskUI();
            SetStatus($"Updated weight for {c} bones in hierarchy.");
        }

        private bool IsActiveInAvatar(string path)
        {
            if (_mask.avatar == null) return true;
            var entry = _mask.avatar.FindByPath(path);
            return entry == null || entry.enabled;
        }

        private string GetBoneDisplayName(string path)
        {
            if (_mask.avatar != null)
            {
                var entry = _mask.avatar.FindByPath(path);
                if (entry != null && !string.IsNullOrEmpty(entry.boneName))
                    return entry.boneName;
            }
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path[(slash + 1)..] : path;
        }

        private static void UpdateValLabel(VisualElement row, float val)
        {
            var lbl = row.Q<Label>("val");
            if (lbl == null) return;
            lbl.text = $"{val:F2}";
            lbl.style.color = WeightToColor(val);
        }

        private static Color WeightToColor(float w)
        {
            if (w >= 0.999f) return ColFull;
            if (w <= 0.001f) return ColNone;
            return w >= 0.5f
                ? Color.Lerp(ColHalf, ColFull, (w - 0.5f) * 2f)
                : Color.Lerp(ColNone, ColHalf, w * 2f);
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

        private void CreateNewMask()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create HonamiAvatarMask", "NewHonamiAvatarMask", "asset", "Choose save location");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<HonamiAvatarMask>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            LoadMask(asset);
        }

        private void SaveAsset()
        {
            if (_mask == null) return;
            EditorUtility.SetDirty(_mask);
            AssetDatabase.SaveAssets();
            SetStatus("Saved.");
        }

        private void SetStatus(string msg) => _statusLabel.text = msg;
    }
}
