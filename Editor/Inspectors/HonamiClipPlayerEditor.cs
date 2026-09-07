using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    [CustomEditor(typeof(HonamiClipPlayer))]
    public sealed class HonamiClipPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty _clips;
        private SerializedProperty _playAutomatically;

        private readonly HonamiSharedAnimatorInspector _shared = new HonamiSharedAnimatorInspector();

        private static GUIStyle _headerStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _debugHeaderStyle;

        private static readonly GUIContent _clipsContent = new GUIContent(" Clips", "Clips this player can play by name. Order defines the mixer input order.");
        private static readonly GUIContent _playAutomaticallyContent = new GUIContent("Play Automatically", "Play the default clip as soon as the player starts.");

        private float _previewFade = 0.2f;
        private ReorderableList _clipList;

        private int _previewIndex = -1;
        private AnimationClip _previewClip;
        private float _previewSpeed = 1f;
        private HonamiClipWrapMode _previewWrap;
        private float _previewTime;
        private bool _previewForward = true;
        private double _previewLastTick;

        private Transform[] _posedTransforms;
        private Vector3[] _posedPositions;
        private Quaternion[] _posedRotations;
        private Vector3[] _posedScales;

        private static readonly GUIContent _playContent = new GUIContent("\u25B6", "Play this clip. In edit mode it previews on the scene object.");
        private static readonly GUIContent _stopContent = new GUIContent("\u25A0", "Stop and restore the pose the object had before the preview.");
        private static readonly GUIContent _previewFadeContent = new GUIContent("Preview Fade", "Crossfade used by the play buttons while in play mode.");
        private static readonly GUIContent _isDefaultOnContent = new GUIContent("Default", "This clip plays on startup. Click to clear it and fall back to the first entry.");
        private static readonly GUIContent _isDefaultOffContent = new GUIContent("Default", "Make this clip the one played on startup.");

        private void OnEnable()
        {
            _clips = serializedObject.FindProperty("clips");
            _playAutomatically = serializedObject.FindProperty("playAutomatically");
            _shared.Bind(serializedObject);
            BuildClipList();
        }

        private void OnDisable() => StopPreview();

        private void BuildClipList()
        {
            _clipList = new ReorderableList(serializedObject, _clips, true, false, true, true);

            _clipList.elementHeightCallback = index =>
            {
                int rows = _clips.GetArrayElementAtIndex(index).isExpanded ? 6 : 1;
                return rows * RowStep + RowPadding * 2f;
            };

            _clipList.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = _clips.GetArrayElementAtIndex(index);

                rect.y += RowPadding;
                rect.height = EditorGUIUtility.singleLineHeight;

                var buttonRect = new Rect(rect.xMax - PlayButtonWidth, rect.y, PlayButtonWidth, rect.height);
                var defaultRect = new Rect(buttonRect.x - DefaultButtonWidth - Gap, rect.y, DefaultButtonWidth, rect.height);
                float available = Mathf.Max(0f, rect.width - PlayButtonWidth - DefaultButtonWidth - Gap * 2f);
                float foldoutWidth = Mathf.Min(available, Mathf.Max(70f, available * 0.45f));
                var foldoutRect = new Rect(rect.x, rect.y, foldoutWidth, rect.height);
                var clipRect = new Rect(rect.x + foldoutWidth + Gap, rect.y, Mathf.Max(0f, available - foldoutWidth - Gap), rect.height);

                // Hierarchy mode makes EditorGUI.Foldout draw its arrow left of the rect, on top of the reorderable drag handle.
                bool previousHierarchyMode = EditorGUIUtility.hierarchyMode;
                EditorGUIUtility.hierarchyMode = false;
                element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, ResolveElementLabel(element, index), true);
                EditorGUIUtility.hierarchyMode = previousHierarchyMode;
                EditorGUI.PropertyField(clipRect, element.FindPropertyRelative("clip"), GUIContent.none);
                DrawDefaultButton(defaultRect, element);
                DrawPlayButton(buttonRect, element, index);

                if (!element.isExpanded) return;

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 96f;

                var row = new Rect(rect.x + FieldIndent, rect.y, rect.width - FieldIndent, rect.height);
                foreach (string field in ExpandedFields)
                {
                    row.y += RowStep;
                    EditorGUI.PropertyField(row, element.FindPropertyRelative(field));
                }

                EditorGUIUtility.labelWidth = previousLabelWidth;
            };

            // Unity zero-fills a new list element instead of running the C# field initializers,
            // so every field has to be seeded here or the entry lands with Speed = 0.
            _clipList.onAddCallback = list =>
            {
                int index = _clips.arraySize;
                _clips.InsertArrayElementAtIndex(index);

                var element = _clips.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("name").stringValue = string.Empty;
                element.FindPropertyRelative("clip").objectReferenceValue = null;
                element.FindPropertyRelative("speed").floatValue = 1f;
                element.FindPropertyRelative("wrapMode").enumValueIndex = (int)HonamiClipWrapMode.Once;
                element.FindPropertyRelative("layer").intValue = 0;
                element.FindPropertyRelative("linkedActionId").objectReferenceValue = null;
                element.FindPropertyRelative("isDefault").boolValue = false;

                list.index = index;
            };
        }

        private const float RowPadding = 2f;
        private const float FieldIndent = 14f;

        private static readonly string[] ExpandedFields = { "name", "speed", "wrapMode", "layer", "linkedActionId" };

        private static float RowStep => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        private const float PlayButtonWidth = 24f;
        private const float DefaultButtonWidth = 58f;
        private const float Gap = 4f;

        private void DrawDefaultButton(Rect rect, SerializedProperty element)
        {
            var isDefault = element.FindPropertyRelative("isDefault");
            bool hasClip = element.FindPropertyRelative("clip").objectReferenceValue != null;

            bool next;
            using (new EditorGUI.DisabledScope(!hasClip))
            {
                next = GUI.Toggle(rect, isDefault.boolValue,
                    isDefault.boolValue ? _isDefaultOnContent : _isDefaultOffContent, EditorStyles.miniButton);
            }

            if (next == isDefault.boolValue) return;

            for (int i = 0; i < _clips.arraySize; i++)
                _clips.GetArrayElementAtIndex(i).FindPropertyRelative("isDefault").boolValue = false;

            isDefault.boolValue = next;
        }

        private void DrawPlayButton(Rect rect, SerializedProperty element, int index)
        {
            var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
            bool previewing = _previewIndex == index;

            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (!GUI.Button(rect, previewing ? _stopContent : _playContent, EditorStyles.miniButton)) return;
            }

            if (previewing)
            {
                StopPreview();
                return;
            }

            if (Application.isPlaying)
            {
                ((HonamiClipPlayer)target).PlayClip(ResolveElementLabel(element, index), _previewFade, forceRestart: true);
                return;
            }

            StartPreview(index, clip, element.FindPropertyRelative("speed").floatValue,
                (HonamiClipWrapMode)element.FindPropertyRelative("wrapMode").enumValueIndex);
        }

        private void StartPreview(int index, AnimationClip clip, float speed, HonamiClipWrapMode wrapMode)
        {
            var player = (HonamiClipPlayer)target;
            if (clip == null || PrefabUtility.IsPartOfPrefabAsset(player.gameObject)) return;

            StopPreview();

            _previewIndex = index;
            _previewClip = clip;
            _previewSpeed = speed;
            _previewWrap = wrapMode;
            _previewForward = speed >= 0f;
            _previewTime = speed < 0f ? clip.length : 0f;
            _previewLastTick = EditorApplication.timeSinceStartup;

            CapturePose(player.gameObject);
            EditorApplication.update += OnEditorUpdate;
        }

        private void StopPreview()
        {
            EditorApplication.update -= OnEditorUpdate;

            _previewIndex = -1;
            _previewClip = null;

            RestorePose();
            SceneView.RepaintAll();
        }

        private void CapturePose(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);

            _posedTransforms = transforms;
            _posedPositions = new Vector3[transforms.Length];
            _posedRotations = new Quaternion[transforms.Length];
            _posedScales = new Vector3[transforms.Length];

            for (int i = 0; i < transforms.Length; i++)
            {
                _posedPositions[i] = transforms[i].localPosition;
                _posedRotations[i] = transforms[i].localRotation;
                _posedScales[i] = transforms[i].localScale;
            }
        }

        private void RestorePose()
        {
            if (_posedTransforms == null) return;

            for (int i = 0; i < _posedTransforms.Length; i++)
            {
                Transform t = _posedTransforms[i];
                if (t == null) continue;

                t.localPosition = _posedPositions[i];
                t.localRotation = _posedRotations[i];
                t.localScale = _posedScales[i];
            }

            _posedTransforms = null;
            _posedPositions = null;
            _posedRotations = null;
            _posedScales = null;
        }

        private void OnEditorUpdate()
        {
            var player = target as HonamiClipPlayer;
            if (player == null || _previewClip == null || Application.isPlaying)
            {
                StopPreview();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _previewLastTick);
            _previewLastTick = now;

            if (!AdvancePreview(delta)) return;

            SamplePreview(player);
            Repaint();
        }

        private bool AdvancePreview(float delta)
        {
            float length = Mathf.Max(0.0001f, _previewClip.length);
            float time = _previewTime + delta * _previewSpeed * (_previewForward ? 1f : -1f);

            switch (_previewWrap)
            {
                case HonamiClipWrapMode.Loop:
                    time = Mathf.Repeat(time, length);
                    break;

                case HonamiClipWrapMode.PingPong:
                    if (time > length)
                    {
                        time = length - (time - length);
                        _previewForward = !_previewForward;
                    }
                    else if (time < 0f)
                    {
                        time = -time;
                        _previewForward = !_previewForward;
                    }
                    time = Mathf.Clamp(time, 0f, length);
                    break;

                default:
                    if (time >= length || time <= 0f)
                    {
                        _previewTime = Mathf.Clamp(time, 0f, length);
                        SamplePreview(target as HonamiClipPlayer);
                        Repaint();
                        // ClampForever holds the last frame until the user presses stop; Once releases like it does at runtime.
                        if (_previewWrap == HonamiClipWrapMode.Once) StopPreview();
                        else EditorApplication.update -= OnEditorUpdate;
                        return false;
                    }
                    break;
            }

            _previewTime = time;
            return true;
        }

        private void SamplePreview(HonamiClipPlayer player)
        {
            if (player == null || _previewClip == null) return;

            _previewClip.SampleAnimation(player.gameObject, Mathf.Min(_previewTime, _previewClip.length - 0.001f));
            SceneView.RepaintAll();
        }

        private static string ResolveElementLabel(SerializedProperty element, int index)
        {
            string name = element.FindPropertyRelative("name").stringValue;
            if (!string.IsNullOrEmpty(name)) return name;

            var clip = element.FindPropertyRelative("clip").objectReferenceValue;
            return clip != null ? clip.name : $"Clip {index}";
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var player = (HonamiClipPlayer)target;

            DrawCustomHeader();
            EditorGUILayout.Space(5);
            DrawClipsSection();
            EditorGUILayout.Space(5);
            _shared.DrawPlaybackSettings();
            EditorGUILayout.Space(5);
            DrawInitialPoseSection(player);
            EditorGUILayout.Space(5);
            _shared.DrawAnimatorSync();
            EditorGUILayout.Space(5);
            _shared.DrawLinkedSystem();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                DrawRuntimeDebugInfo(player);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.18f, 0.76f, 0.9f) }
            };

            EditorGUILayout.Space(10);
            GUILayout.Label("HONAMI CLIP PLAYER", _headerStyle);

            _subtitleStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Clip List Playback Without a Controller", _subtitleStyle);

            Rect rect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.76f, 0.9f, 0.5f));
            EditorGUILayout.Space(5);
        }

        private void DrawClipsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(_clipsContent, EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            _clipList.DoLayoutList();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(3);
                _previewFade = EditorGUILayout.Slider(_previewFadeContent, _previewFade, 0f, 2f);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_playAutomatically, _playAutomaticallyContent);

            EditorGUILayout.EndVertical();
        }

        private void DrawInitialPoseSection(HonamiClipPlayer player)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(" Initial Pose", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.PropertyField(_shared.CaptureInitialPoseOnAwake, HonamiSharedAnimatorInspector.CaptureOnAwakeContent);
            EditorGUILayout.PropertyField(_shared.RestoreInitialPoseWhenIdle, HonamiSharedAnimatorInspector.RestoreWhenIdleContent);
            EditorGUILayout.PropertyField(_shared.IncludeRootTransformInInitialPose, HonamiSharedAnimatorInspector.IncludeRootTransformContent);

            EditorGUILayout.Space(4);
            _shared.DrawGlobalWeightMode(_shared.CaptureInitialPoseOnAwake.boolValue);
            _shared.DrawPoseButtons(player);

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeDebugInfo(HonamiClipPlayer player)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _debugHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.9f, 0.6f, 0.2f) } };
            GUILayout.Label(" Runtime Debug", _debugHeaderStyle);
            EditorGUILayout.Space(3);

            var states = player.States;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(state.Name, GUILayout.Width(140));

                Rect bar = EditorGUILayout.GetControlRect(false, 16);
                EditorGUI.ProgressBar(bar, state.NormalizedTime, $"w {state.Weight:F2}   t {state.NormalizedTime * 100f:F0}%");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Current Clip", player.CurrentClip ?? "None");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Stop All", GUILayout.Height(25))) player.StopAll();
            if (GUILayout.Button(player.IsPaused ? "Resume" : "Pause", GUILayout.Height(25)))
            {
                if (player.IsPaused) player.Resume();
                else player.Pause();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            Repaint();
        }
    }
}
