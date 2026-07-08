using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Editor.Windows
{
    public sealed class HonamiHumanoidBakerWindow : EditorWindow
    {
        private enum RootMotionMode
        {
            Discard,
            BakeIntoHips
        }

        private const float ConstantCurveEpsilon = 1e-5f;

        private readonly List<AnimationClip> _sourceClips = new();
        private GameObject _targetCharacter;
        private bool _useClipFrameRate = true;
        private float _customFrameRate = 30f;
        private bool _applyFootIK = true;
        private RootMotionMode _rootMotionMode = RootMotionMode.Discard;
        private bool _bakeScaleCurves = false;
        private bool _compressConstantCurves = true;
        private string _outputSuffix = "_Generic";
        private Vector2 _scroll;

        [MenuItem("Window/Honami/Tools/Honami Humanoid Baker")]
        public static void ShowWindow()
        {
            GetWindow<HonamiHumanoidBakerWindow>("Honami Humanoid Baker");
        }

        [MenuItem("Assets/Bake Humanoid Clips to Generic (Honami)", true)]
        private static bool ValidateContextBake()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is AnimationClip) return true;
            }
            return false;
        }

        [MenuItem("Assets/Bake Humanoid Clips to Generic (Honami)")]
        private static void ContextBake()
        {
            var window = GetWindow<HonamiHumanoidBakerWindow>("Honami Humanoid Baker");
            window.AddSelectedClips();
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Humanoid to Generic Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Retargets Humanoid clips onto the target character through Unity's muscle space, then bakes the result into Generic transform-path clips that Honami plays natively.",
                MessageType.Info);
            EditorGUILayout.Space();

            _targetCharacter = (GameObject)EditorGUILayout.ObjectField("Target Character", _targetCharacter, typeof(GameObject), true);
            DrawTargetValidation();

            EditorGUILayout.Space();
            GUILayout.Label("Source Humanoid Clips", EditorStyles.boldLabel);
            DrawClipList();

            EditorGUILayout.Space();
            GUILayout.Label("Bake Settings", EditorStyles.boldLabel);
            _useClipFrameRate = EditorGUILayout.Toggle("Use Clip Frame Rate", _useClipFrameRate);
            if (!_useClipFrameRate)
            {
                _customFrameRate = Mathf.Max(1f, EditorGUILayout.FloatField("Sample Rate", _customFrameRate));
            }
            _applyFootIK = EditorGUILayout.Toggle("Apply Foot IK", _applyFootIK);
            _rootMotionMode = (RootMotionMode)EditorGUILayout.EnumPopup("Root Motion", _rootMotionMode);
            _bakeScaleCurves = EditorGUILayout.Toggle("Bake Scale Curves", _bakeScaleCurves);
            _compressConstantCurves = EditorGUILayout.Toggle("Compress Constant Curves", _compressConstantCurves);
            _outputSuffix = EditorGUILayout.TextField("Output Suffix", _outputSuffix);

            EditorGUILayout.Space();

            GUI.enabled = IsReadyToBake();
            if (GUILayout.Button("Bake to Generic Clips", GUILayout.Height(30)))
            {
                BakeAll();
            }
            GUI.enabled = true;
        }

        private void DrawTargetValidation()
        {
            if (_targetCharacter == null)
            {
                EditorGUILayout.HelpBox("Assign a character: a prefab, a scene object, or the character FBX dragged straight from the Project window. The model must have a valid Humanoid Avatar. The character can stay Humanoid at runtime - just keep the Avatar assigned in the Unity Animator's Avatar field. Only the baked clips are Generic.", MessageType.None);
                return;
            }

            var animator = FindTargetAnimator();
            if (animator == null)
            {
                EditorGUILayout.HelpBox("No Animator found on the target or its children. For an FBX this usually means its import Animation Type is set to None.", MessageType.Error);
                DrawHumanoidImportFix();
            }
            else if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                EditorGUILayout.HelpBox("The target's Animator needs a valid Humanoid Avatar so Unity can retarget the source clips onto it.", MessageType.Error);
                DrawHumanoidImportFix();
            }
        }

        private void DrawHumanoidImportFix()
        {
            string assetPath = AssetDatabase.GetAssetPath(_targetCharacter);
            if (string.IsNullOrEmpty(assetPath)) return;
            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer) return;
            if (importer.animationType == ModelImporterAnimationType.Human) return;

            if (GUILayout.Button("Switch Model Import to Humanoid"))
            {
                bool proceed = EditorUtility.DisplayDialog("Switch to Humanoid",
                    "This changes the model's import Animation Type to Humanoid and reimports it.\n\nIf this model is already used at runtime with a Generic rig, references and clips bound to it may break. In that case bake from a duplicate of the FBX instead.",
                    "Switch", "Cancel");
                if (proceed)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                }
            }
        }

        private void DrawClipList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(180));
            int removeIndex = -1;
            for (int i = 0; i < _sourceClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _sourceClips[i] = (AnimationClip)EditorGUILayout.ObjectField(_sourceClips[i], typeof(AnimationClip), false);
                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
            {
                _sourceClips.RemoveAt(removeIndex);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = TargetIsValid();
            if (GUILayout.Button(new GUIContent("Auto Fill", "Scans the project for Humanoid clips. Assign a valid target character first.")))
            {
                AutoFillHumanoidClips();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Add Selected Clips"))
            {
                AddSelectedClips();
            }
            if (GUILayout.Button("Add Slot"))
            {
                _sourceClips.Add(null);
            }
            if (GUILayout.Button("Clear"))
            {
                _sourceClips.Clear();
            }
            EditorGUILayout.EndHorizontal();

            int nonHumanoid = 0;
            foreach (var clip in _sourceClips)
            {
                if (clip != null && !clip.isHumanMotion) nonHumanoid++;
            }
            if (nonHumanoid > 0)
            {
                EditorGUILayout.HelpBox(nonHumanoid + " clip(s) are not Humanoid and will be skipped. Set their import Animation Type to Humanoid first.", MessageType.Warning);
            }
        }

        private void AutoFillHumanoidClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            int added = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if ((i & 31) == 0)
                    {
                        EditorUtility.DisplayProgressBar("Auto Fill", path, (float)i / guids.Length);
                    }

                    var mainClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (TryAddHumanoidClip(mainClip)) added++;

                    foreach (var representation in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                    {
                        if (representation is AnimationClip subClip && TryAddHumanoidClip(subClip)) added++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Repaint();
            EditorUtility.DisplayDialog("Auto Fill",
                added > 0
                    ? "Added " + added + " Humanoid clip(s) found in the project."
                    : "No new Humanoid clips found in the project.",
                "OK");
        }

        private bool TryAddHumanoidClip(AnimationClip clip)
        {
            if (clip == null) return false;
            if (!clip.isHumanMotion) return false;
            if (clip.name.StartsWith("__preview__")) return false;
            if (_sourceClips.Contains(clip)) return false;

            _sourceClips.Add(clip);
            return true;
        }

        private void AddSelectedClips()
        {
            foreach (var clip in Selection.GetFiltered<AnimationClip>(SelectionMode.Deep))
            {
                if (clip.name.StartsWith("__preview__")) continue;
                if (!_sourceClips.Contains(clip))
                {
                    _sourceClips.Add(clip);
                }
            }
            Repaint();
        }

        private Animator FindTargetAnimator()
        {
            return _targetCharacter == null ? null : _targetCharacter.GetComponentInChildren<Animator>(true);
        }

        private bool TargetIsValid()
        {
            var animator = FindTargetAnimator();
            return animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
        }

        private bool IsReadyToBake()
        {
            if (!TargetIsValid()) return false;
            foreach (var clip in _sourceClips)
            {
                if (clip != null && clip.isHumanMotion) return true;
            }
            return false;
        }

        private void BakeAll()
        {
            string absFolder = EditorUtility.OpenFolderPanel("Output Folder for Baked Clips", "Assets", "");
            if (string.IsNullOrEmpty(absFolder)) return;

            string folder = FileUtil.GetProjectRelativePath(absFolder);
            if (string.IsNullOrEmpty(folder))
            {
                EditorUtility.DisplayDialog("Error", "Please choose a folder inside the project's Assets folder.", "OK");
                return;
            }

            int baked = 0;
            int skipped = 0;
            try
            {
                for (int i = 0; i < _sourceClips.Count; i++)
                {
                    var clip = _sourceClips[i];
                    if (clip == null || !clip.isHumanMotion)
                    {
                        skipped++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Honami Humanoid Baker", clip.name, (float)i / _sourceClips.Count);
                    BakeClip(clip, folder);
                    baked++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = "Baked " + baked + " clip(s) to " + folder + ".";
            if (skipped > 0) message += "\nSkipped " + skipped + " empty or non-Humanoid clip(s).";
            EditorUtility.DisplayDialog("Success", message, "OK");
        }

        private void BakeClip(AnimationClip source, string folder)
        {
            var instance = Instantiate(_targetCharacter);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            var graph = default(PlayableGraph);
            try
            {
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null) behaviour.enabled = false;
                }

                var animator = instance.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = _rootMotionMode == RootMotionMode.BakeIntoHips;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.fireEvents = false;

                float rate = _useClipFrameRate ? Mathf.Max(1f, source.frameRate) : _customFrameRate;
                float dt = 1f / rate;
                int frameCount = Mathf.Max(2, Mathf.CeilToInt(source.length * rate) + 1);

                var recorders = CreateRecorders(animator.transform, frameCount);
                var times = new float[frameCount];

                graph = PlayableGraph.Create("HonamiHumanoidBaker");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "BakeOutput", animator);
                var clipPlayable = AnimationClipPlayable.Create(graph, source);
                clipPlayable.SetApplyFootIK(_applyFootIK);
                output.SetSourcePlayable(clipPlayable);

                float previousTime = 0f;
                for (int f = 0; f < frameCount; f++)
                {
                    float targetTime = Mathf.Min(f * dt, source.length);
                    times[f] = targetTime;
                    graph.Evaluate(f == 0 ? 0f : targetTime - previousTime);
                    previousTime = targetTime;

                    CaptureFrame(recorders, f);
                }

                var bakedClip = BuildClip(source, recorders, times, rate);
                SaveClip(bakedClip, source, folder);
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                DestroyImmediate(instance);
            }
        }

        private sealed class BoneRecorder
        {
            public Transform Bone;
            public string Path;
            public bool RecordInRootSpace;
            public float[] Px, Py, Pz;
            public float[] Rx, Ry, Rz, Rw;
            public float[] Sx, Sy, Sz;
            public Quaternion LastRotation;
            public bool HasLastRotation;
        }

        private List<BoneRecorder> CreateRecorders(Transform root, int frameCount)
        {
            var recorders = new List<BoneRecorder>();
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone == root) continue;

                var recorder = new BoneRecorder
                {
                    Bone = bone,
                    Path = AnimationUtility.CalculateTransformPath(bone, root),
                    RecordInRootSpace = bone.parent == root && _rootMotionMode == RootMotionMode.BakeIntoHips,
                    Px = new float[frameCount], Py = new float[frameCount], Pz = new float[frameCount],
                    Rx = new float[frameCount], Ry = new float[frameCount], Rz = new float[frameCount], Rw = new float[frameCount]
                };
                if (_bakeScaleCurves)
                {
                    recorder.Sx = new float[frameCount];
                    recorder.Sy = new float[frameCount];
                    recorder.Sz = new float[frameCount];
                }
                recorders.Add(recorder);
            }
            return recorders;
        }

        private static void CaptureFrame(List<BoneRecorder> recorders, int frame)
        {
            foreach (var recorder in recorders)
            {
                Vector3 position;
                Quaternion rotation;
                if (recorder.RecordInRootSpace)
                {
                    position = recorder.Bone.position;
                    rotation = recorder.Bone.rotation;
                }
                else
                {
                    position = recorder.Bone.localPosition;
                    rotation = recorder.Bone.localRotation;
                }

                if (recorder.HasLastRotation && Quaternion.Dot(recorder.LastRotation, rotation) < 0f)
                {
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                }
                recorder.LastRotation = rotation;
                recorder.HasLastRotation = true;

                recorder.Px[frame] = position.x;
                recorder.Py[frame] = position.y;
                recorder.Pz[frame] = position.z;
                recorder.Rx[frame] = rotation.x;
                recorder.Ry[frame] = rotation.y;
                recorder.Rz[frame] = rotation.z;
                recorder.Rw[frame] = rotation.w;

                if (recorder.Sx != null)
                {
                    Vector3 scale = recorder.Bone.localScale;
                    recorder.Sx[frame] = scale.x;
                    recorder.Sy[frame] = scale.y;
                    recorder.Sz[frame] = scale.z;
                }
            }
        }

        private AnimationClip BuildClip(AnimationClip source, List<BoneRecorder> recorders, float[] times, float rate)
        {
            var clip = new AnimationClip { frameRate = rate };

            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();

            foreach (var recorder in recorders)
            {
                AddCurve(bindings, curves, recorder.Path, "m_LocalPosition.x", times, recorder.Px);
                AddCurve(bindings, curves, recorder.Path, "m_LocalPosition.y", times, recorder.Py);
                AddCurve(bindings, curves, recorder.Path, "m_LocalPosition.z", times, recorder.Pz);
                AddCurve(bindings, curves, recorder.Path, "m_LocalRotation.x", times, recorder.Rx);
                AddCurve(bindings, curves, recorder.Path, "m_LocalRotation.y", times, recorder.Ry);
                AddCurve(bindings, curves, recorder.Path, "m_LocalRotation.z", times, recorder.Rz);
                AddCurve(bindings, curves, recorder.Path, "m_LocalRotation.w", times, recorder.Rw);

                if (recorder.Sx != null)
                {
                    AddCurve(bindings, curves, recorder.Path, "m_LocalScale.x", times, recorder.Sx);
                    AddCurve(bindings, curves, recorder.Path, "m_LocalScale.y", times, recorder.Sy);
                    AddCurve(bindings, curves, recorder.Path, "m_LocalScale.z", times, recorder.Sz);
                }
            }

            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();

            var sourceSettings = AnimationUtility.GetAnimationClipSettings(source);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = sourceSettings.loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        private void AddCurve(List<EditorCurveBinding> bindings, List<AnimationCurve> curves, string path, string property, float[] times, float[] values)
        {
            bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
            curves.Add(BuildCurve(times, values, _compressConstantCurves));
        }

        private static AnimationCurve BuildCurve(float[] times, float[] values, bool compress)
        {
            int count = values.Length;

            if (compress)
            {
                float min = values[0];
                float max = values[0];
                for (int i = 1; i < count; i++)
                {
                    if (values[i] < min) min = values[i];
                    if (values[i] > max) max = values[i];
                }
                if (max - min < ConstantCurveEpsilon)
                {
                    return new AnimationCurve(
                        new Keyframe(times[0], values[0], 0f, 0f),
                        new Keyframe(times[count - 1], values[0], 0f, 0f));
                }
            }

            var keys = new Keyframe[count];
            for (int i = 0; i < count; i++)
            {
                float inTangent = 0f;
                float outTangent = 0f;
                if (i > 0)
                {
                    inTangent = (values[i] - values[i - 1]) / Mathf.Max(times[i] - times[i - 1], 1e-6f);
                }
                if (i < count - 1)
                {
                    outTangent = (values[i + 1] - values[i]) / Mathf.Max(times[i + 1] - times[i], 1e-6f);
                }
                if (i > 0 && i < count - 1)
                {
                    float smoothed = (inTangent + outTangent) * 0.5f;
                    inTangent = smoothed;
                    outTangent = smoothed;
                }
                keys[i] = new Keyframe(times[i], values[i], inTangent, outTangent);
            }
            return new AnimationCurve(keys);
        }

        private void SaveClip(AnimationClip bakedClip, AnimationClip source, string folder)
        {
            string safeName = string.Join("_", source.name.Split(Path.GetInvalidFileNameChars()));
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + _outputSuffix + ".anim");
            bakedClip.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(bakedClip, path);
        }
    }
}
