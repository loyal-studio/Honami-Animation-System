using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Windows
{
    public sealed class HonamiAnimationClipFixerWindow : EditorWindow
    {
        private enum FixMode
        {
            RemoveChildTranslations,
            SmoothCurves,
            RemoveMicroJitter,
            ConvertEulerToQuaternion,
            FixAnomalousRotations
        }

        private FixMode _mode = FixMode.RemoveMicroJitter;
        private float _microJitterThreshold = 0.001f;
        private int _smoothWindowSize = 3;
        private float _anomalyThreshold = 45f;
        private AnimationClip _targetClip;

        [MenuItem("Window/Honami/Tools/Honami Animation Clip Fixer")]
        public static void ShowWindow()
        {
            GetWindow<HonamiAnimationClipFixerWindow>("Honami Clip Fixer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Blender to Unity Jitter Fixer", EditorStyles.boldLabel);

            _targetClip = (AnimationClip)EditorGUILayout.ObjectField("Target Clip", _targetClip, typeof(AnimationClip), false);
            GUILayout.Space(5);

            _mode = (FixMode)EditorGUILayout.EnumPopup("Fix Mode", _mode);

            if (_mode == FixMode.RemoveMicroJitter)
            {
                _microJitterThreshold = EditorGUILayout.FloatField("Jitter Threshold", _microJitterThreshold);
            }
            else if (_mode == FixMode.SmoothCurves)
            {
                _smoothWindowSize = EditorGUILayout.IntSlider("Smooth Window", _smoothWindowSize, 2, 10);
            }
            else if (_mode == FixMode.FixAnomalousRotations)
            {
                _anomalyThreshold = EditorGUILayout.FloatField("Anomaly Angle Threshold", _anomalyThreshold);
            }

            GUILayout.Space(10);

            GUI.enabled = _targetClip != null;
            if (GUILayout.Button("Fix Target Animation Clip"))
            {
                FixSelectedClip();
            }
            GUI.enabled = true;
        }

        private void FixSelectedClip()
        {
            if (_targetClip == null) return;

            ProcessClip(_targetClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void ProcessClip(AnimationClip original)
        {
            string path = AssetDatabase.GetAssetPath(original);
            bool isSubAsset = AssetDatabase.IsSubAsset(original);

            AnimationClip targetClip = original;

            if (isSubAsset)
            {
                targetClip = new AnimationClip();
                EditorUtility.CopySerialized(original, targetClip);
                string dir = Path.GetDirectoryName(path) ?? "Assets";
                dir = dir.Replace("\\", "/");
                string safeName = string.Join("_", original.name.Split(Path.GetInvalidFileNameChars()));
                string newPath = dir + "/" + safeName + "_Fixed.anim";
                AssetDatabase.CreateAsset(targetClip, newPath);
            }

            var bindings = AnimationUtility.GetCurveBindings(targetClip);

            if (_mode == FixMode.ConvertEulerToQuaternion || _mode == FixMode.FixAnomalousRotations)
            {
                var paths = new HashSet<string>();
                foreach (var b in bindings)
                {
                    if (b.propertyName.StartsWith("localEulerAnglesRaw"))
                        paths.Add(b.path);
                }

                foreach (var p in paths)
                {
                    var bx = EditorCurveBinding.FloatCurve(p, typeof(Transform), "localEulerAnglesRaw.x");
                    var by = EditorCurveBinding.FloatCurve(p, typeof(Transform), "localEulerAnglesRaw.y");
                    var bz = EditorCurveBinding.FloatCurve(p, typeof(Transform), "localEulerAnglesRaw.z");

                    var cx = AnimationUtility.GetEditorCurve(targetClip, bx);
                    var cy = AnimationUtility.GetEditorCurve(targetClip, by);
                    var cz = AnimationUtility.GetEditorCurve(targetClip, bz);

                    var times = new HashSet<float>();
                    if (cx != null) { foreach (var k in cx.keys) times.Add(k.time); }
                    if (cy != null) { foreach (var k in cy.keys) times.Add(k.time); }
                    if (cz != null) { foreach (var k in cz.keys) times.Add(k.time); }

                    if (times.Count == 0) continue;

                    var sortedTimes = new List<float>(times);
                    sortedTimes.Sort();

                    var qx = new AnimationCurve();
                    var qy = new AnimationCurve();
                    var qz = new AnimationCurve();
                    var qw = new AnimationCurve();

                    Quaternion lastValidRotation = Quaternion.identity;
                    bool hasFirst = false;

                    for (int i = 0; i < sortedTimes.Count; i++)
                    {
                        float t = sortedTimes[i];
                        float x = cx != null ? cx.Evaluate(t) : 0f;
                        float y = cy != null ? cy.Evaluate(t) : 0f;
                        float z = cz != null ? cz.Evaluate(t) : 0f;

                        Quaternion currentRotation = Quaternion.Euler(x, y, z);

                        if (_mode == FixMode.FixAnomalousRotations)
                        {
                            if (!hasFirst)
                            {
                                lastValidRotation = currentRotation;
                                hasFirst = true;
                            }
                            else
                            {
                                if (Quaternion.Angle(lastValidRotation, currentRotation) > _anomalyThreshold)
                                {
                                    currentRotation = lastValidRotation;
                                }
                                else
                                {
                                    lastValidRotation = currentRotation;
                                }
                            }
                        }

                        qx.AddKey(new Keyframe(t, currentRotation.x));
                        qy.AddKey(new Keyframe(t, currentRotation.y));
                        qz.AddKey(new Keyframe(t, currentRotation.z));
                        qw.AddKey(new Keyframe(t, currentRotation.w));
                    }

                    AnimationUtility.SetEditorCurve(targetClip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "m_LocalRotation.x"), qx);
                    AnimationUtility.SetEditorCurve(targetClip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "m_LocalRotation.y"), qy);
                    AnimationUtility.SetEditorCurve(targetClip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "m_LocalRotation.z"), qz);
                    AnimationUtility.SetEditorCurve(targetClip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "m_LocalRotation.w"), qw);

                    if (cx != null) AnimationUtility.SetEditorCurve(targetClip, bx, null);
                    if (cy != null) AnimationUtility.SetEditorCurve(targetClip, by, null);
                    if (cz != null) AnimationUtility.SetEditorCurve(targetClip, bz, null);
                }
            }

            Span<EditorCurveBinding> bindingsSpan = new Span<EditorCurveBinding>(bindings);

            for (int b = 0; b < bindingsSpan.Length; b++)
            {
                EditorCurveBinding binding = bindingsSpan[b];
                bool isTranslation = binding.propertyName.StartsWith("m_LocalPosition");
                bool isRoot = string.IsNullOrEmpty(binding.path);

                if (_mode == FixMode.RemoveChildTranslations && isTranslation && !isRoot)
                {
                    AnimationUtility.SetEditorCurve(targetClip, binding, null);
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(targetClip, binding);
                if (curve == null || curve.keys.Length == 0) continue;

                if (_mode == FixMode.RemoveMicroJitter)
                {
                    Keyframe[] keys = curve.keys;
                    Span<Keyframe> keysSpan = new Span<Keyframe>(keys);
                    for (int i = 1; i < keysSpan.Length; i++)
                    {
                        if (Mathf.Abs(keysSpan[i].value - keysSpan[i - 1].value) < _microJitterThreshold)
                        {
                            keysSpan[i].value = keysSpan[i - 1].value;
                        }
                    }
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(targetClip, binding, curve);
                }
                else if (_mode == FixMode.SmoothCurves)
                {
                    Keyframe[] keys = curve.keys;
                    Keyframe[] newKeys = new Keyframe[keys.Length];
                    Span<Keyframe> keysSpan = new Span<Keyframe>(keys);
                    Span<Keyframe> newKeysSpan = new Span<Keyframe>(newKeys);

                    for (int i = 0; i < keysSpan.Length; i++)
                    {
                        float sum = 0f;
                        int count = 0;
                        int start = Mathf.Max(0, i - _smoothWindowSize / 2);
                        int end = Mathf.Min(keysSpan.Length - 1, i + _smoothWindowSize / 2);

                        for (int j = start; j <= end; j++)
                        {
                            sum += keysSpan[j].value;
                            count++;
                        }

                        newKeysSpan[i] = keysSpan[i];
                        newKeysSpan[i].value = sum / count;
                    }
                    curve.keys = newKeys;
                    AnimationUtility.SetEditorCurve(targetClip, binding, curve);
                }
            }

            targetClip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(targetClip);
        }
    }
}
