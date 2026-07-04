#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Common;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Preview
{
    /// <summary>
    /// Builds a detached, mesh-less transform hierarchy from a set of animation clips and samples
    /// poses onto it without touching the scene or the global AnimationMode. Blend weights reuse the
    /// same solver as the runtime blend tree so the preview matches playback.
    /// </summary>
    internal sealed class HonamiPreviewSkeleton : IDisposable
    {
        private GameObject _root;
        private Transform[] _bones = Array.Empty<Transform>();
        private int[] _parentIndex = Array.Empty<int>();

        private Vector3[] _baseLocalPos = Array.Empty<Vector3>();
        private Quaternion[] _baseLocalRot = Array.Empty<Quaternion>();
        private Vector3[] _baseLocalScale = Array.Empty<Vector3>();

        private Vector3[] _blendPos = Array.Empty<Vector3>();
        private Vector3[] _blendScale = Array.Empty<Vector3>();
        private Quaternion[] _blendRot = Array.Empty<Quaternion>();
        private bool[] _hasRot = Array.Empty<bool>();

        private float[] _weights = Array.Empty<float>();
        private (float threshold, int index)[] _sorted = Array.Empty<(float, int)>();

        private int _topologySignature;

        public Transform Root => _root != null ? _root.transform : null;
        public IReadOnlyList<Transform> Bones => _bones;
        public int[] ParentIndex => _parentIndex;
        public bool IsBuilt => _root != null && _bones.Length > 0;

        public bool NeedsRebuild(IReadOnlyList<AnimationClip> clips) => _topologySignature != Signature(clips);

        public void Build(IReadOnlyList<AnimationClip> clips)
        {
            Dispose();

            _root = new GameObject("HonamiPreviewSkeleton") { hideFlags = HideFlags.HideAndDontSave };
            var pathToTransform = new Dictionary<string, Transform> { [string.Empty] = _root.transform };

            if (clips != null)
            {
                for (int c = 0; c < clips.Count; c++)
                {
                    var clip = clips[c];
                    if (clip == null) continue;

                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    for (int b = 0; b < bindings.Length; b++)
                    {
                        var binding = bindings[b];
                        if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
                        EnsurePath(binding.path, pathToTransform);
                    }
                }
            }

            CacheHierarchy(pathToTransform);
            _topologySignature = Signature(clips);
        }

        private void EnsurePath(string path, Dictionary<string, Transform> pathToTransform)
        {
            if (pathToTransform.ContainsKey(path)) return;

            var parts = path.Split('/');
            Transform current = _root.transform;
            string accumulated = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                accumulated = accumulated.Length == 0 ? parts[i] : accumulated + "/" + parts[i];
                if (!pathToTransform.TryGetValue(accumulated, out var next) || next == null)
                {
                    var go = new GameObject(parts[i]) { hideFlags = HideFlags.HideAndDontSave };
                    next = go.transform;
                    next.SetParent(current, false);
                    next.localPosition = Vector3.zero;
                    next.localRotation = Quaternion.identity;
                    next.localScale = Vector3.one;
                    pathToTransform[accumulated] = next;
                }
                current = next;
            }
        }

        private void CacheHierarchy(Dictionary<string, Transform> pathToTransform)
        {
            var list = new List<Transform>(pathToTransform.Count);
            foreach (var kvp in pathToTransform)
            {
                if (kvp.Key.Length == 0) continue;
                list.Add(kvp.Value);
            }

            int count = list.Count;
            _bones = list.ToArray();
            _parentIndex = new int[count];

            var indexOf = new Dictionary<Transform, int>(count);
            for (int i = 0; i < count; i++) indexOf[_bones[i]] = i;

            for (int i = 0; i < count; i++)
            {
                Transform parent = _bones[i].parent;
                _parentIndex[i] = parent != null && indexOf.TryGetValue(parent, out int pi) ? pi : -1;
            }

            _baseLocalPos = new Vector3[count];
            _baseLocalRot = new Quaternion[count];
            _baseLocalScale = new Vector3[count];
            _blendPos = new Vector3[count];
            _blendScale = new Vector3[count];
            _blendRot = new Quaternion[count];
            _hasRot = new bool[count];

            for (int i = 0; i < count; i++)
            {
                _baseLocalPos[i] = _bones[i].localPosition;
                _baseLocalRot[i] = _bones[i].localRotation;
                _baseLocalScale[i] = _bones[i].localScale;
            }
        }

        public void SampleSingle(AnimationClip clip, float normalizedTime, bool loop, bool reversed)
        {
            if (_root == null || clip == null) return;
            ResetToBase();
            float time = ResolveClipTime(clip, normalizedTime, loop, reversed);
            clip.SampleAnimation(_root, time);
            PinRoot();
        }

        public void SampleBlend(IReadOnlyList<HonamiBlendTreeMotion> motions, float parameterValue, float normalizedTime, bool loop, bool reversed)
        {
            if (_root == null || motions == null || motions.Count == 0) return;

            int motionCount = motions.Count;
            if (_weights.Length < motionCount)
            {
                _weights = new float[motionCount];
                _sorted = new (float, int)[motionCount];
            }

            var weights = _weights.AsSpan(0, motionCount);
            weights.Clear();
            HonamiBlendWeightUtility.CalculateWeights(motions, parameterValue, weights, _sorted.AsSpan(0, motionCount));

            int boneCount = _bones.Length;
            Array.Clear(_blendPos, 0, boneCount);
            Array.Clear(_blendScale, 0, boneCount);
            Array.Clear(_hasRot, 0, boneCount);

            float avgDur = HonamiBlendTreeEvaluator.GetDurationFromMotions(AsList(motions), parameterValue);
            float total = 0f;

            for (int m = 0; m < motionCount; m++)
            {
                var motion = motions[m];
                float weight = weights[m];
                if (motion.clip == null || weight <= 0.0001f) continue;

                float clipLength = motion.clip.length;
                float speed = avgDur > 0.0001f && clipLength > 0f
                    ? clipLength / avgDur
                    : (motion.speed != 0f ? Mathf.Abs(motion.speed) : 1f);

                float sampleTime = normalizedTime * avgDur * speed;
                if (reversed) sampleTime = clipLength - sampleTime;
                sampleTime = clipLength > 0f
                    ? Mathf.Clamp(sampleTime, 0f, Mathf.Max(0f, clipLength - EndEpsilon))
                    : 0f;

                ResetToBase();
                motion.clip.SampleAnimation(_root, sampleTime);

                float next = total + weight;
                float rotationBlend = next > 0f ? weight / next : 1f;
                for (int i = 0; i < boneCount; i++)
                {
                    Transform t = _bones[i];
                    _blendPos[i] += t.localPosition * weight;
                    _blendScale[i] += t.localScale * weight;
                    _blendRot[i] = _hasRot[i]
                        ? Quaternion.Slerp(_blendRot[i], t.localRotation, rotationBlend)
                        : t.localRotation;
                    _hasRot[i] = true;
                }
                total = next;
            }

            if (total <= 0.0001f)
            {
                ResetToBase();
                PinRoot();
                return;
            }

            float invTotal = 1f / total;
            for (int i = 0; i < boneCount; i++)
            {
                _bones[i].localPosition = _blendPos[i] * invTotal;
                _bones[i].localRotation = _hasRot[i] ? _blendRot[i] : _baseLocalRot[i];
                _bones[i].localScale = _blendScale[i] * invTotal;
            }
            PinRoot();
        }

        public sealed class Pose
        {
            public Vector3[] Pos;
            public Quaternion[] Rot;
            public Vector3[] Scale;
            public int Count => Pos != null ? Pos.Length : 0;
        }

        public Pose CreatePose()
        {
            int n = _bones.Length;
            return new Pose { Pos = new Vector3[n], Rot = new Quaternion[n], Scale = new Vector3[n] };
        }

        public void CaptureInto(Pose pose)
        {
            if (pose == null || pose.Count != _bones.Length) return;
            for (int i = 0; i < _bones.Length; i++)
            {
                pose.Pos[i] = _bones[i].localPosition;
                pose.Rot[i] = _bones[i].localRotation;
                pose.Scale[i] = _bones[i].localScale;
            }
        }

        public void ApplyCrossfade(Pose from, Pose to, float t)
        {
            if (from == null || to == null) return;
            int n = _bones.Length;
            if (from.Count != n || to.Count != n) return;

            t = Mathf.Clamp01(t);
            for (int i = 0; i < n; i++)
            {
                _bones[i].localPosition = Vector3.Lerp(from.Pos[i], to.Pos[i], t);
                _bones[i].localRotation = Quaternion.Slerp(from.Rot[i], to.Rot[i], t);
                _bones[i].localScale = Vector3.Lerp(from.Scale[i], to.Scale[i], t);
            }
            PinRoot();
        }

        private void PinRoot()
        {
            if (_root == null) return;
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
        }

        // Sampling exactly at clip.length wraps back to frame 0, so scrubbing/stopping at the end
        // would snap to the first pose. Looping is driven by the transports normalizing time into
        // [0,1), so here we only clamp just short of the end to hold the final frame.
        private const float EndEpsilon = 0.0001f;

        private static float ResolveClipTime(AnimationClip clip, float normalizedTime, bool loop, bool reversed)
        {
            float length = clip.length;
            if (length <= 0f) return 0f;
            float t = normalizedTime * length;
            if (reversed) t = length - t;
            return Mathf.Clamp(t, 0f, Mathf.Max(0f, length - EndEpsilon));
        }

        private void ResetToBase()
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = _baseLocalPos[i];
                _bones[i].localRotation = _baseLocalRot[i];
                _bones[i].localScale = _baseLocalScale[i];
            }
        }

        public Bounds ComputeBounds()
        {
            if (_bones.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var bounds = new Bounds(_bones[0].position, Vector3.zero);
            for (int i = 1; i < _bones.Length; i++)
                bounds.Encapsulate(_bones[i].position);
            if (bounds.size.sqrMagnitude < 1e-6f)
                bounds.size = Vector3.one;
            return bounds;
        }

        private static int Signature(IReadOnlyList<AnimationClip> clips)
        {
            int hash = 17;
            if (clips == null) return hash;
            for (int i = 0; i < clips.Count; i++)
                hash = hash * 31 + (clips[i] != null ? HonamiObjectHash.Of(clips[i]) : 0);
            return hash;
        }

        private static readonly List<HonamiBlendTreeMotion> MotionListBuffer = new();
        private static List<HonamiBlendTreeMotion> AsList(IReadOnlyList<HonamiBlendTreeMotion> motions)
        {
            if (motions is List<HonamiBlendTreeMotion> concrete) return concrete;
            MotionListBuffer.Clear();
            for (int i = 0; i < motions.Count; i++) MotionListBuffer.Add(motions[i]);
            return MotionListBuffer;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
            _bones = Array.Empty<Transform>();
            _parentIndex = Array.Empty<int>();
            _topologySignature = 0;
        }
    }
}
#endif
