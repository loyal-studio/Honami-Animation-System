using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using System.Collections.Generic;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiBoneDebugWindow : EditorWindow
    {
        private const string PREFS_PREFIX = "HonamiBoneDebug_";

        private bool _drawBones = true;
        private bool _drawAxes = true;
        private bool _drawLabels = false;
        private bool _drawSpheres = true;
        private bool _avatarOnly = true;
        private bool _onlyShowSelected = false;
        private bool _showBoneList = true;

        private float _axisScale = 0.1f;
        private float _sphereSize = 0.015f;
        private Color _boneColor = new Color(0.2f, 0.8f, 1f, 0.8f);

        private HonamiAnimator _targetAnimator;
        private List<Transform> _boneTransforms = new();
        private readonly HashSet<Transform> _boneTransformSet = new();
        private Dictionary<Transform, BoneRowRefs> _boneRowRefs = new();
        private readonly List<Component> _componentBuffer = new();

        private struct BoneRowRefs
        {
            public Label statusIcon;
            public Label nameLabel;
            public Label valuesLabel;
            public Label reasonLabel;
            public VisualElement row;
            public Vector3 lastRot;
            public Vector3 lastScale;
            public bool hasLastValues;
            public string lastReason;
        }

        [MenuItem("Window/Honami/Honami Bone Debug")]
        public static void Open() => GetWindow<HonamiBoneDebugWindow>("Honami Bone Debug");

        private void OnEnable()
        {
            LoadSettings();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = HonamiGraphStyles.PanelBg;
            rootVisualElement.style.paddingLeft = rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = rootVisualElement.style.paddingBottom = 10;

            rootVisualElement.Add(HonamiGraphStyles.Title("Honami Bone Debug"));

            var settingsBox = HonamiGraphStyles.Box();
            settingsBox.Add(HonamiGraphStyles.SubTitle("Visualization"));

            var drawBonesToggle = CreateToggle("Draw Bone Lines", _drawBones, v => { _drawBones = v; SaveSettings(); });
            var drawSpheresToggle = CreateToggle("Draw Bone Points", _drawSpheres, v => { _drawSpheres = v; SaveSettings(); });
            var drawAxesToggle = CreateToggle("Draw Local Axes", _drawAxes, v => { _drawAxes = v; SaveSettings(); });
            var drawLabelsToggle = CreateToggle("Draw Bone Names", _drawLabels, v => { _drawLabels = v; SaveSettings(); });
            var avatarOnlyToggle = CreateToggle("Avatar Only", _avatarOnly, v => { _avatarOnly = v; SaveSettings(); });
            var onlyShowSelectedToggle = CreateToggle("Only Show Selected", _onlyShowSelected, v => { _onlyShowSelected = v; SaveSettings(); });
            var showBoneListToggle = CreateToggle("Show Health List", _showBoneList, v => { _showBoneList = v; RebuildUI(); });

            settingsBox.Add(drawBonesToggle);
            settingsBox.Add(drawSpheresToggle);
            settingsBox.Add(drawAxesToggle);
            settingsBox.Add(drawLabelsToggle);
            settingsBox.Add(avatarOnlyToggle);
            settingsBox.Add(onlyShowSelectedToggle);
            settingsBox.Add(showBoneListToggle);

            rootVisualElement.Add(settingsBox);

            var slidersBox = HonamiGraphStyles.Box();
            slidersBox.Add(HonamiGraphStyles.SubTitle("Settings"));

            var axisScaleSlider = new Slider("Axis Scale", 0.01f, 1f) { value = _axisScale };
            axisScaleSlider.RegisterValueChangedCallback(evt => { _axisScale = evt.newValue; SaveSettings(); });
            slidersBox.Add(axisScaleSlider);

            var sphereSizeSlider = new Slider("Point Size", 0.005f, 0.1f) { value = _sphereSize };
            sphereSizeSlider.RegisterValueChangedCallback(evt => { _sphereSize = evt.newValue; SaveSettings(); });
            slidersBox.Add(sphereSizeSlider);

            var boneColorField = new ColorField("Bone Color") { value = _boneColor };
            boneColorField.RegisterValueChangedCallback(evt => { _boneColor = evt.newValue; SaveSettings(); });
            slidersBox.Add(boneColorField);

            rootVisualElement.Add(slidersBox);

            var infoBox = HonamiGraphStyles.ListBox();
            var statusLabel = new Label("No Animator Selected");
            statusLabel.style.color = HonamiGraphStyles.GreyText;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusLabel.style.fontSize = 11;
            statusLabel.style.paddingTop = statusLabel.style.paddingBottom = 4;
            infoBox.Add(statusLabel);
            rootVisualElement.Add(infoBox);

            var boneListContainer = new VisualElement();
            boneListContainer.name = "BoneListContainer";
            rootVisualElement.Add(boneListContainer);

            var controlsRow = HonamiGraphStyles.Row();
            controlsRow.style.justifyContent = Justify.Center;
            controlsRow.style.marginTop = 10;
            var refreshBtn = new Button(() => { UpdateTarget(); RefreshBones(); RebuildUI(); SceneView.RepaintAll(); }) { text = "Force Refresh" };
            refreshBtn.style.height = 24;
            refreshBtn.style.paddingLeft = refreshBtn.style.paddingRight = 12;
            controlsRow.Add(refreshBtn);
            rootVisualElement.Add(controlsRow);

            rootVisualElement.schedule.Execute(() =>
            {
                UpdateTarget();
                UpdateAnimatorInfo(statusLabel);
                UpdateBoneListLive();
            }).Every(100);

            RebuildUI();
        }

        private void UpdateBoneListLive()
        {
            if (!_showBoneList || _targetAnimator == null) return;

            // Detect if bone count or selection changed significantly
            if (_boneTransforms.Count != _boneRowRefs.Count)
            {
                RefreshBones();
                RebuildUI();
                return;
            }

            var activeT = Selection.activeTransform;
            foreach (var kvp in _boneRowRefs)
            {
                var t = kvp.Key;
                if (t == null) continue;

                var refs = kvp.Value;
                var status = GetBoneHealth(t, out string reason);

                // Update Icon
                Color iconColor = Color.white;
                string iconText = "";
                switch (status)
                {
                    case Health.Good: iconText = "OK"; iconColor = new Color(0.3f, 1f, 0.4f); break;
                    case Health.Normal: iconText = "!"; iconColor = new Color(1f, 0.8f, 0.2f); break;
                    case Health.Bad: iconText = "X"; iconColor = new Color(1f, 0.3f, 0.3f); break;
                }

                if (refs.statusIcon.text != iconText) refs.statusIcon.text = iconText;
                refs.statusIcon.style.color = iconColor;

                // Update Values
                var rot = t.localEulerAngles;
                var scl = t.localScale;
                if (!refs.hasLastValues || refs.lastRot != rot || refs.lastScale != scl)
                {
                    refs.valuesLabel.text = $"R:({rot.x:0},{rot.y:0},{rot.z:0}) S:({scl.x:0.##},{scl.y:0.##},{scl.z:0.##})";
                    refs.lastRot = rot;
                    refs.lastScale = scl;
                    refs.hasLastValues = true;
                }

                // Update Reason
                if (refs.lastReason != reason)
                {
                    refs.reasonLabel.text = reason;
                    refs.lastReason = reason;
                }
                refs.reasonLabel.style.color = status == Health.Bad ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.7f, 0.3f);

                // Update highlight
                refs.nameLabel.style.unityFontStyleAndWeight = (t == activeT) ? FontStyle.Bold : FontStyle.Normal;

                _boneRowRefs[t] = refs;
            }
        }

        private void RebuildUI()
        {
            var container = rootVisualElement.Q<VisualElement>("BoneListContainer");
            if (container == null) return;
            container.Clear();
            _boneRowRefs.Clear();

            if (!_showBoneList || _targetAnimator == null) return;

            container.Add(HonamiGraphStyles.Separator());
            container.Add(HonamiGraphStyles.SubTitle("Bone Health Analysis (Live)"));

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.height = 300;
            scroll.style.marginTop = 4;
            HonamiGraphStyles.ApplyListBox(scroll);

            foreach (var t in _boneTransforms)
            {
                if (t == null) continue;
                var row = CreateBoneHealthRow(t);
                scroll.Add(row);
            }
            container.Add(scroll);
        }

        private VisualElement CreateBoneHealthRow(Transform t)
        {
            var row = HonamiGraphStyles.Row();
            row.style.paddingLeft = row.style.paddingRight = 4;
            row.style.paddingTop = row.style.paddingBottom = 2;
            row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            row.style.borderBottomWidth = 1;

            var statusIcon = new Label();
            statusIcon.style.width = 16;
            statusIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusIcon.style.marginRight = 6;

            var nameLabel = new Label(t.name);
            nameLabel.style.width = 140;
            nameLabel.style.fontSize = 10;
            nameLabel.style.overflow = Overflow.Hidden;

            var valuesLabel = new Label();
            valuesLabel.style.fontSize = 9;
            valuesLabel.style.color = HonamiGraphStyles.GreyText;
            valuesLabel.style.flexGrow = 1;

            var reasonLabel = new Label();
            reasonLabel.style.fontSize = 9;
            reasonLabel.style.width = 90;
            reasonLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(statusIcon);
            row.Add(nameLabel);
            row.Add(valuesLabel);
            row.Add(reasonLabel);

            _boneRowRefs[t] = new BoneRowRefs
            {
                statusIcon = statusIcon,
                nameLabel = nameLabel,
                valuesLabel = valuesLabel,
                reasonLabel = reasonLabel,
                row = row
            };

            row.RegisterCallback<MouseDownEvent>(evt => Selection.activeTransform = t);
            return row;
        }

        private enum Health { Good, Normal, Bad }

        private Health GetBoneHealth(Transform t, out string reason)
        {
            reason = "";
            var s = t.localScale;
            var r = t.localRotation;

            // Scaler checks
            if (float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.z)) { reason = "NaN Scale"; return Health.Bad; }
            if (s.magnitude < 0.001f) { reason = "Zero Scale"; return Health.Bad; }
            if (s.x <= 0 || s.y <= 0 || s.z <= 0) { reason = "Neg/Zero Scale"; return Health.Bad; }

            // Rotation checks
            float ang = Quaternion.Angle(r, Quaternion.identity);
            if (float.IsNaN(ang)) { reason = "NaN Rotation"; return Health.Bad; }

            // Components check
            t.GetComponents(_componentBuffer);
            if (_componentBuffer.Count > 2) // Transform + 1 other is okay, more is "Normal" (e.g. constraints)
            {
                for (int i = 0; i < _componentBuffer.Count; i++)
                {
                    var comp = _componentBuffer[i];
                    if (comp is Transform) continue;
                    if (comp.GetType().Name.Contains("Honami")) { reason = "Has Honami Constraint"; return Health.Normal; }
                }
            }

            // Suspect rotation
            if (ang > 0.1f)
            {
                if (ang > 89f && ang < 91f) { reason = "90° Rotation"; return Health.Normal; }
                if (ang > 179f) { reason = "180° Flip"; return Health.Bad; }
                reason = $"Rotation {ang:0}°"; return Health.Normal;
            }

            // Scale warnings
            bool uniform = Mathf.Approximately(s.x, s.y) && Mathf.Approximately(s.y, s.z);
            if (!uniform) { reason = "Non-uniform Scale"; return Health.Normal; }
            if (!Mathf.Approximately(s.x, 1f)) { reason = $"Scale {s.x:F2}"; return Health.Normal; }

            // Position checks
            if (t.parent != null)
            {
                var p = t.localPosition;
                if (p.sqrMagnitude > 40000) { reason = "Offset Too Large"; return Health.Normal; }
                if (p.sqrMagnitude < 0.000001f) { reason = "Zero Offset (Overlap)"; return Health.Normal; }
            }

            return Health.Good;
        }

        private void UpdateAnimatorInfo(Label label)
        {
            if (_targetAnimator == null)
            {
                label.text = "Select an object with <b>HonamiAnimator</b>";
                return;
            }

            string info = $"Animator: <b>{_targetAnimator.gameObject.name}</b>\n";
            info += $"Bones: <b>{_boneTransforms.Count}</b> | ";

            var so = new SerializedObject(_targetAnimator);
            var updateMode = (HonamiUpdateMode)so.FindProperty("updateMode").enumValueIndex;
            var timeScale = so.FindProperty("timeScale").floatValue;

            info += $"Mode: <b>{updateMode}</b> | Speed: <b>{timeScale:F1}x</b>";

            label.text = info;
        }

        private Toggle CreateToggle(string label, bool initialValue, System.Action<bool> onValueChanged)
        {
            var t = new Toggle(label) { value = initialValue };
            t.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            return t;
        }

        private void UpdateTarget()
        {
            var activeGO = Selection.activeGameObject;
            var animator = activeGO != null ? activeGO.GetComponentInParent<HonamiAnimator>() : null;

            if (animator != _targetAnimator)
            {
                _targetAnimator = animator;
                RefreshBones();
            }
        }

        private void RefreshBones()
        {
            _boneTransforms.Clear();
            _boneTransformSet.Clear();
            if (_targetAnimator == null) return;

            var avatar = _targetAnimator.Avatar;

            if (_avatarOnly && avatar != null)
            {
                var root = _targetAnimator.transform;
                foreach (var entry in avatar.bones)
                {
                    if (!entry.enabled) continue;
                    var t = string.IsNullOrEmpty(entry.bonePath) ? root : root.Find(entry.bonePath);
                    if (t != null) _boneTransforms.Add(t);
                }
            }
            else
            {
                _boneTransforms.AddRange(_targetAnimator.GetComponentsInChildren<Transform>());
            }

            foreach (var t in _boneTransforms) _boneTransformSet.Add(t);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_targetAnimator == null)
            {
                // If no animator in parent, check if selection is just a transform and draw it if desired
                var selectedT = Selection.activeTransform;
                if (selectedT != null) DrawBoneDebug(selectedT, true);
                return;
            }

            RefreshBones();

            var activeT = Selection.activeTransform;
            bool hasValidSelection = activeT != null && (_targetAnimator == null || activeT.IsChildOf(_targetAnimator.transform) || _boneTransformSet.Contains(activeT));

            if (_onlyShowSelected && hasValidSelection)
            {
                DrawBoneDebug(activeT, true);
                return;
            }

            foreach (var t in _boneTransforms)
            {
                if (t == null) continue;
                bool isSelected = (t == activeT);
                DrawBoneDebug(t, isSelected);
            }

            // Ensure selected bone is drawn even if not in _boneTransforms (e.g. Avatar Only is on)
            if (activeT != null && activeT.IsChildOf(_targetAnimator.transform) && !_boneTransformSet.Contains(activeT))
            {
                DrawBoneDebug(activeT, true);
            }
        }

        private void DrawBoneDebug(Transform t, bool isSelected)
        {
            Vector3 pos = t.position;
            Color color = isSelected ? Color.yellow : _boneColor;
            Handles.color = color;

            if (_drawSpheres)
            {
                Handles.SphereHandleCap(0, pos, Quaternion.identity, _sphereSize * (isSelected ? 1.5f : 1f), EventType.Repaint);
            }

            if (_drawBones && t.parent != null && (_boneTransformSet.Contains(t.parent) || isSelected))
            {
                Handles.DrawLine(t.parent.position, pos);
            }

            if (_drawAxes || isSelected)
            {
                float scale = _axisScale * (isSelected ? 1.5f : 1f);
                Handles.color = HonamiGraphStyles.AxisRed;
                Handles.DrawLine(pos, pos + t.right * scale);
                Handles.color = HonamiGraphStyles.AxisGreen;
                Handles.DrawLine(pos, pos + t.up * scale);
                Handles.color = HonamiGraphStyles.AxisBlue;
                Handles.DrawLine(pos, pos + t.forward * scale);
            }

            if (_drawLabels || isSelected)
            {
                GUIStyle style = isSelected ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                Handles.Label(pos + Vector3.up * (_sphereSize * 2f), t.name, style);
            }

            Handles.color = _boneColor;
        }

        private void LoadSettings()
        {
            _drawBones = EditorPrefs.GetBool(PREFS_PREFIX + "DrawBones", true);
            _drawAxes = EditorPrefs.GetBool(PREFS_PREFIX + "DrawAxes", true);
            _drawLabels = EditorPrefs.GetBool(PREFS_PREFIX + "DrawLabels", false);
            _drawSpheres = EditorPrefs.GetBool(PREFS_PREFIX + "DrawSpheres", true);
            _avatarOnly = EditorPrefs.GetBool(PREFS_PREFIX + "AvatarOnly", true);
            _onlyShowSelected = EditorPrefs.GetBool(PREFS_PREFIX + "OnlyShowSelected", false);
            _axisScale = EditorPrefs.GetFloat(PREFS_PREFIX + "AxisScale", 0.1f);
            _sphereSize = EditorPrefs.GetFloat(PREFS_PREFIX + "SphereSize", 0.015f);

            string colorHex = EditorPrefs.GetString(PREFS_PREFIX + "BoneColor", "");
            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString("#" + colorHex, out var c))
                _boneColor = c;
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool(PREFS_PREFIX + "DrawBones", _drawBones);
            EditorPrefs.SetBool(PREFS_PREFIX + "DrawAxes", _drawAxes);
            EditorPrefs.SetBool(PREFS_PREFIX + "DrawLabels", _drawLabels);
            EditorPrefs.SetBool(PREFS_PREFIX + "DrawSpheres", _drawSpheres);
            EditorPrefs.SetBool(PREFS_PREFIX + "AvatarOnly", _avatarOnly);
            EditorPrefs.SetBool(PREFS_PREFIX + "OnlyShowSelected", _onlyShowSelected);
            EditorPrefs.SetFloat(PREFS_PREFIX + "AxisScale", _axisScale);
            EditorPrefs.SetFloat(PREFS_PREFIX + "SphereSize", _sphereSize);
            EditorPrefs.SetString(PREFS_PREFIX + "BoneColor", ColorUtility.ToHtmlStringRGBA(_boneColor));

            SceneView.RepaintAll();
        }
    }
}
