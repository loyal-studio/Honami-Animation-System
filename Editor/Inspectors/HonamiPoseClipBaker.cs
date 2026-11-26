using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Riggings
{
    public enum HonamiPosePreviewMode
    {
        None,
        TargetPose,
        ReferencePose
    }

    public static class HonamiPoseClipBaker
    {
        public struct SampledCurveData
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localEuler;
            public bool hasPosition;
            public bool hasRotation;
            public bool hasEuler;
        }

        private static readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> _previewCache = new();
        private static HonamiPoseConstraint _previewingConstraint;
        private static HonamiPosePreviewMode _previewMode = HonamiPosePreviewMode.None;

        public static Transform FindSampleRoot(HonamiPoseConstraint constraint)
        {
            if (constraint == null) return null;
            if (constraint.bakeSampleRoot != null) return constraint.bakeSampleRoot;
            if (constraint.honamiAnimator != null) return constraint.honamiAnimator.transform;

            if (constraint.TryGetComponent<HonamiAnimator>(out var selfHonamiAnim)) return selfHonamiAnim.transform;
            if (constraint.TryGetComponent<Animator>(out var selfUnityAnim)) return selfUnityAnim.transform;

            Transform p = constraint.transform.parent;
            while (p != null)
            {
                if (p.TryGetComponent<HonamiAnimator>(out var anim)) return anim.transform;
                if (p.TryGetComponent<Animator>(out var unityAnim)) return unityAnim.transform;
                p = p.parent;
            }

            return constraint.transform.root;
        }

        public static Dictionary<string, SampledCurveData> ExtractCurves(AnimationClip clip, float time)
        {
            var result = new Dictionary<string, SampledCurveData>(StringComparer.OrdinalIgnoreCase);
            if (clip == null) return result;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings == null || bindings.Length == 0) return result;

            var rawQuat = new Dictionary<string, (float x, float y, float z, float w, bool hx, bool hy, bool hz, bool hw)>(StringComparer.OrdinalIgnoreCase);
            var rawPos = new Dictionary<string, (float x, float y, float z, bool hx, bool hy, bool hz)>(StringComparer.OrdinalIgnoreCase);
            var rawEuler = new Dictionary<string, (float x, float y, float z, bool hx, bool hy, bool hz)>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;

                float val = curve.Evaluate(time);
                string path = b.path;

                switch (b.propertyName)
                {
                    case "m_LocalPosition.x":
                        var px = rawPos.TryGetValue(path, out var pxv) ? pxv : default;
                        px.x = val; px.hx = true;
                        rawPos[path] = px;
                        break;
                    case "m_LocalPosition.y":
                        var py = rawPos.TryGetValue(path, out var pyv) ? pyv : default;
                        py.y = val; py.hy = true;
                        rawPos[path] = py;
                        break;
                    case "m_LocalPosition.z":
                        var pz = rawPos.TryGetValue(path, out var pzv) ? pzv : default;
                        pz.z = val; pz.hz = true;
                        rawPos[path] = pz;
                        break;

                    case "m_LocalRotation.x":
                        var qx = rawQuat.TryGetValue(path, out var qxv) ? qxv : default;
                        qx.x = val; qx.hx = true;
                        rawQuat[path] = qx;
                        break;
                    case "m_LocalRotation.y":
                        var qy = rawQuat.TryGetValue(path, out var qyv) ? qyv : default;
                        qy.y = val; qy.hy = true;
                        rawQuat[path] = qy;
                        break;
                    case "m_LocalRotation.z":
                        var qz = rawQuat.TryGetValue(path, out var qzv) ? qzv : default;
                        qz.z = val; qz.hz = true;
                        rawQuat[path] = qz;
                        break;
                    case "m_LocalRotation.w":
                        var qw = rawQuat.TryGetValue(path, out var qwv) ? qwv : default;
                        qw.w = val; qw.hw = true;
                        rawQuat[path] = qw;
                        break;

                    case "localEulerAnglesRaw.x":
                        var ex = rawEuler.TryGetValue(path, out var exv) ? exv : default;
                        ex.x = val; ex.hx = true;
                        rawEuler[path] = ex;
                        break;
                    case "localEulerAnglesRaw.y":
                        var ey = rawEuler.TryGetValue(path, out var eyv) ? eyv : default;
                        ey.y = val; ey.hy = true;
                        rawEuler[path] = ey;
                        break;
                    case "localEulerAnglesRaw.z":
                        var ez = rawEuler.TryGetValue(path, out var ezv) ? ezv : default;
                        ez.z = val; ez.hz = true;
                        rawEuler[path] = ez;
                        break;
                }
            }

            var allPaths = new HashSet<string>(rawPos.Keys, StringComparer.OrdinalIgnoreCase);
            allPaths.UnionWith(rawQuat.Keys);
            allPaths.UnionWith(rawEuler.Keys);

            foreach (string path in allPaths)
            {
                var data = new SampledCurveData();

                if (rawPos.TryGetValue(path, out var p))
                {
                    data.localPosition = new Vector3(p.x, p.y, p.z);
                    data.hasPosition = p.hx || p.hy || p.hz;
                }

                if (rawQuat.TryGetValue(path, out var q) && (q.hx || q.hy || q.hz || q.hw))
                {
                    var quat = new Quaternion(q.x, q.y, q.z, q.hw ? q.w : 1f);
                    if (quat.x != 0f || quat.y != 0f || quat.z != 0f || quat.w != 0f)
                    {
                        data.localRotation = quat.normalized;
                        data.hasRotation = true;
                    }
                }

                if (rawEuler.TryGetValue(path, out var e) && (e.hx || e.hy || e.hz))
                {
                    data.localEuler = new Vector3(e.x, e.y, e.z);
                    data.hasEuler = true;
                    if (!data.hasRotation)
                    {
                        data.localRotation = Quaternion.Euler(data.localEuler);
                        data.hasRotation = true;
                    }
                }

                result[path] = data;
            }

            return result;
        }

        public static bool TryFindCurveData(Dictionary<string, SampledCurveData> curves, string boneName, string bonePath, out SampledCurveData data)
        {
            data = default;
            if (curves == null || curves.Count == 0) return false;

            if (!string.IsNullOrEmpty(bonePath))
            {
                if (curves.TryGetValue(bonePath, out data)) return true;

                foreach (var pair in curves)
                {
                    string k = pair.Key;
                    if (k.EndsWith("/" + bonePath, StringComparison.OrdinalIgnoreCase) || bonePath.EndsWith("/" + k, StringComparison.OrdinalIgnoreCase))
                    {
                        data = pair.Value;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(boneName))
            {
                if (curves.TryGetValue(boneName, out data)) return true;

                foreach (var pair in curves)
                {
                    string k = pair.Key;
                    if (k.Equals(boneName, StringComparison.OrdinalIgnoreCase) || k.EndsWith("/" + boneName, StringComparison.OrdinalIgnoreCase))
                    {
                        data = pair.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        public static int PopulateTargetsFromClip(HonamiPoseConstraint constraint)
        {
            if (constraint == null || constraint.bakeClip == null) return 0;

            AnimationClip clip = constraint.bakeClip;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings == null || bindings.Length == 0) return 0;

            var uniquePaths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < bindings.Length; i++)
            {
                string p = bindings[i].path;
                if (!string.IsNullOrEmpty(p) && seenPaths.Add(p))
                {
                    uniquePaths.Add(p);
                }
            }

            Undo.RecordObject(constraint, "Populate Bones From Clip");
            int addedCount = 0;

            if (constraint.targetSource == HonamiLookAtTargetSource.Avatar)
            {
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (constraint.avatarTargets != null)
                {
                    for (int i = 0; i < constraint.avatarTargets.Length; i++)
                    {
                        string n = constraint.avatarTargets[i].boneName;
                        string p = constraint.avatarTargets[i].bonePath;
                        if (!string.IsNullOrEmpty(n)) existing.Add(n);
                        if (!string.IsNullOrEmpty(p)) existing.Add(p);
                    }
                }

                var list = constraint.avatarTargets != null ? new List<HonamiAvatarPoseTarget>(constraint.avatarTargets) : new List<HonamiAvatarPoseTarget>();

                for (int i = 0; i < uniquePaths.Count; i++)
                {
                    string curvePath = uniquePaths[i];
                    string boneName = Path.GetFileName(curvePath);
                    string bonePath = curvePath;

                    if (constraint.avatar != null)
                    {
                        var entry = constraint.avatar.FindByPath(curvePath);
                        if (entry != null)
                        {
                            boneName = entry.boneName;
                            bonePath = entry.bonePath;
                        }
                        else
                        {
                            for (int b = 0; b < constraint.avatar.bones.Count; b++)
                            {
                                var cand = constraint.avatar.bones[b];
                                if (cand.bonePath.EndsWith("/" + curvePath, StringComparison.OrdinalIgnoreCase) ||
                                    curvePath.EndsWith("/" + cand.bonePath, StringComparison.OrdinalIgnoreCase) ||
                                    cand.boneName.Equals(boneName, StringComparison.OrdinalIgnoreCase))
                                {
                                    boneName = cand.boneName;
                                    bonePath = cand.bonePath;
                                    break;
                                }
                            }
                        }
                    }

                    if (existing.Contains(boneName) || existing.Contains(bonePath)) continue;

                    list.Add(new HonamiAvatarPoseTarget
                    {
                        enabled = true,
                        boneName = boneName,
                        bonePath = bonePath,
                        positionWeight = constraint.bakePositions ? 1f : 0f,
                        rotationWeight = constraint.bakeRotations ? 1f : 0f
                    });

                    existing.Add(boneName);
                    existing.Add(bonePath);
                    addedCount++;
                }

                constraint.avatarTargets = list.ToArray();
            }
            else
            {
                Transform root = FindSampleRoot(constraint);
                var existingBones = new HashSet<Transform>();
                if (constraint.targets != null)
                {
                    for (int i = 0; i < constraint.targets.Length; i++)
                    {
                        if (constraint.targets[i].bone != null) existingBones.Add(constraint.targets[i].bone);
                    }
                }

                var list = constraint.targets != null ? new List<HonamiPoseBoneTarget>(constraint.targets) : new List<HonamiPoseBoneTarget>();

                for (int i = 0; i < uniquePaths.Count; i++)
                {
                    string curvePath = uniquePaths[i];
                    Transform bone = root != null ? root.Find(curvePath) : null;
                    if (bone == null && root != null)
                    {
                        string boneName = Path.GetFileName(curvePath);
                        var allChildren = root.GetComponentsInChildren<Transform>(true);
                        for (int c = 0; c < allChildren.Length; c++)
                        {
                            if (allChildren[c].name.Equals(boneName, StringComparison.OrdinalIgnoreCase))
                            {
                                bone = allChildren[c];
                                break;
                            }
                        }
                    }

                    if (bone == null || existingBones.Contains(bone)) continue;

                    list.Add(new HonamiPoseBoneTarget
                    {
                        bone = bone,
                        positionWeight = constraint.bakePositions ? 1f : 0f,
                        rotationWeight = constraint.bakeRotations ? 1f : 0f
                    });

                    existingBones.Add(bone);
                    addedCount++;
                }

                constraint.targets = list.ToArray();
            }

            EditorUtility.SetDirty(constraint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
            return addedCount;
        }

        public static bool IsPreviewing(HonamiPoseConstraint constraint, HonamiPosePreviewMode mode)
        {
            return _previewingConstraint == constraint && _previewMode == mode && _previewCache.Count > 0;
        }

        public static void SetPreview(HonamiPoseConstraint constraint, bool enable, HonamiPosePreviewMode mode = HonamiPosePreviewMode.TargetPose)
        {
            if (!enable || constraint == null || mode == HonamiPosePreviewMode.None)
            {
                RestorePreview();
                return;
            }

            AnimationClip clipToPreview = null;
            float timeToPreview = 0f;

            if (mode == HonamiPosePreviewMode.TargetPose)
            {
                clipToPreview = constraint.bakeClip;
                timeToPreview = constraint.bakeTime;
            }
            else if (mode == HonamiPosePreviewMode.ReferencePose)
            {
                if (constraint.bakeAdditiveRef == HonamiPoseAdditiveReference.ReferenceClip)
                {
                    clipToPreview = constraint.bakeReferenceClip != null ? constraint.bakeReferenceClip : constraint.bakeClip;
                    timeToPreview = constraint.bakeRefTime;
                }
                else if (constraint.bakeAdditiveRef == HonamiPoseAdditiveReference.CurrentClipFrame)
                {
                    clipToPreview = constraint.bakeClip;
                    timeToPreview = constraint.bakeRefTime;
                }
                else
                {
                    RestorePreview();
                    return;
                }
            }

            if (clipToPreview == null)
            {
                RestorePreview();
                return;
            }

            if (_previewingConstraint != constraint || _previewMode != mode)
            {
                CachePreview(constraint);
                _previewMode = mode;
            }

            SampleTransforms(constraint, clipToPreview, timeToPreview);
            SceneView.RepaintAll();
        }

        public static void UpdatePreview(HonamiPoseConstraint constraint)
        {
            if (_previewCache.Count == 0 || constraint == null || _previewMode == HonamiPosePreviewMode.None) return;

            AnimationClip clipToPreview = null;
            float timeToPreview = 0f;

            if (_previewMode == HonamiPosePreviewMode.TargetPose)
            {
                clipToPreview = constraint.bakeClip;
                timeToPreview = constraint.bakeTime;
            }
            else if (_previewMode == HonamiPosePreviewMode.ReferencePose)
            {
                if (constraint.bakeAdditiveRef == HonamiPoseAdditiveReference.ReferenceClip)
                {
                    clipToPreview = constraint.bakeReferenceClip != null ? constraint.bakeReferenceClip : constraint.bakeClip;
                    timeToPreview = constraint.bakeRefTime;
                }
                else if (constraint.bakeAdditiveRef == HonamiPoseAdditiveReference.CurrentClipFrame)
                {
                    clipToPreview = constraint.bakeClip;
                    timeToPreview = constraint.bakeRefTime;
                }
            }

            if (clipToPreview != null)
            {
                SampleTransforms(constraint, clipToPreview, timeToPreview);
                SceneView.RepaintAll();
            }
        }

        private static void CachePreview(HonamiPoseConstraint constraint)
        {
            RestorePreview();
            _previewingConstraint = constraint;

            Transform sampleRoot = FindSampleRoot(constraint);
            if (sampleRoot != null)
            {
                var allTransforms = sampleRoot.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform tr = allTransforms[t];
                    if (tr != null && !_previewCache.ContainsKey(tr))
                    {
                        _previewCache[tr] = (tr.localPosition, tr.localRotation);
                    }
                }
            }

            bool isAvatar = constraint.targetSource == HonamiLookAtTargetSource.Avatar;
            int count = isAvatar ? (constraint.avatarTargets?.Length ?? 0) : (constraint.targets?.Length ?? 0);

            for (int i = 0; i < count; i++)
            {
                Transform bone = isAvatar ? constraint.ResolveAvatarBone(constraint.avatarTargets[i]) : constraint.targets[i].bone;
                if (bone != null && !_previewCache.ContainsKey(bone))
                {
                    _previewCache[bone] = (bone.localPosition, bone.localRotation);
                }
            }
        }

        private static void SampleTransforms(HonamiPoseConstraint constraint, AnimationClip clip, float time)
        {
            Transform sampleRoot = FindSampleRoot(constraint);
            if (sampleRoot != null)
            {
                clip.SampleAnimation(sampleRoot.gameObject, time);
            }

            var curves = ExtractCurves(clip, time);
            bool isAvatar = constraint.targetSource == HonamiLookAtTargetSource.Avatar;
            int count = isAvatar ? (constraint.avatarTargets?.Length ?? 0) : (constraint.targets?.Length ?? 0);

            for (int i = 0; i < count; i++)
            {
                Transform bone = isAvatar ? constraint.ResolveAvatarBone(constraint.avatarTargets[i]) : constraint.targets[i].bone;
                if (bone == null) continue;

                string boneName = isAvatar ? constraint.avatarTargets[i].boneName : bone.name;
                string bonePath = isAvatar ? constraint.avatarTargets[i].bonePath : (sampleRoot != null ? AnimationUtility.CalculateTransformPath(bone, sampleRoot) : bone.name);

                if (sampleRoot == null && TryFindCurveData(curves, boneName, bonePath, out var data))
                {
                    if (data.hasPosition) bone.localPosition = data.localPosition;
                    if (data.hasRotation) bone.localRotation = data.localRotation;
                }
            }
        }

        public static void RestorePreview()
        {
            foreach (var pair in _previewCache)
            {
                if (pair.Key != null)
                {
                    pair.Key.localPosition = pair.Value.pos;
                    pair.Key.localRotation = pair.Value.rot;
                }
            }
            _previewCache.Clear();
            _previewingConstraint = null;
            _previewMode = HonamiPosePreviewMode.None;
            SceneView.RepaintAll();
        }

        public static int BakePose(HonamiPoseConstraint constraint, HashSet<int> selectedIndices = null)
        {
            if (constraint == null || constraint.bakeClip == null) return 0;

            RestorePreview();

            AnimationClip clip = constraint.bakeClip;
            float time = constraint.bakeTime;
            bool isAvatar = constraint.targetSource == HonamiLookAtTargetSource.Avatar;
            int targetCount = isAvatar ? (constraint.avatarTargets?.Length ?? 0) : (constraint.targets?.Length ?? 0);

            if (targetCount == 0)
            {
                PopulateTargetsFromClip(constraint);
                targetCount = isAvatar ? (constraint.avatarTargets?.Length ?? 0) : (constraint.targets?.Length ?? 0);
                if (targetCount == 0) return 0;
            }

            Undo.RecordObject(constraint, "Bake Pose From Animation Clip");

            bool isAdditive = constraint.bakeMode == HonamiPoseBakeMode.Auto
                ? constraint.poseType == HonamiPoseType.Additive
                : constraint.bakeMode == HonamiPoseBakeMode.Additive;

            bool isWorld = constraint.poseSpace == HonamiPoseSpace.World;
            bool bakePos = constraint.bakePositions;
            bool bakeRot = constraint.bakeRotations;
            bool selectedOnly = constraint.bakeSelectedOnly && selectedIndices != null && selectedIndices.Count > 0;

            AnimationClip refClip = null;
            float refTime = 0f;

            if (isAdditive)
            {
                switch (constraint.bakeAdditiveRef)
                {
                    case HonamiPoseAdditiveReference.ReferenceClip:
                        refClip = constraint.bakeReferenceClip != null ? constraint.bakeReferenceClip : clip;
                        refTime = constraint.bakeRefTime;
                        break;
                    case HonamiPoseAdditiveReference.CurrentClipFrame:
                        refClip = clip;
                        refTime = constraint.bakeRefTime;
                        break;
                    case HonamiPoseAdditiveReference.CurrentTransform:
                        refClip = null;
                        break;
                }
            }

            Transform sampleRoot = FindSampleRoot(constraint);
            var baseTransforms = new Dictionary<Transform, (Vector3 localPos, Quaternion localRot, Vector3 worldPos, Quaternion worldRot)>();

            if (sampleRoot != null)
            {
                var allTransforms = sampleRoot.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < allTransforms.Length; t++)
                {
                    Transform tr = allTransforms[t];
                    if (tr != null && !baseTransforms.ContainsKey(tr))
                    {
                        baseTransforms[tr] = (tr.localPosition, tr.localRotation, tr.position, tr.rotation);
                    }
                }
            }

            for (int i = 0; i < targetCount; i++)
            {
                Transform bone = isAvatar ? constraint.ResolveAvatarBone(constraint.avatarTargets[i]) : constraint.targets[i].bone;
                if (bone != null && !baseTransforms.ContainsKey(bone))
                {
                    baseTransforms[bone] = (bone.localPosition, bone.localRotation, bone.position, bone.rotation);
                }
            }

            void RestoreAllTransforms()
            {
                foreach (var pair in baseTransforms)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.localPosition = pair.Value.localPos;
                        pair.Key.localRotation = pair.Value.localRot;
                    }
                }
            }

            var refHierarchy = new Dictionary<Transform, (Vector3 localPos, Quaternion localRot, Vector3 worldPos, Quaternion worldRot)>();
            var sampledHierarchy = new Dictionary<Transform, (Vector3 localPos, Quaternion localRot, Vector3 worldPos, Quaternion worldRot)>();

            if (isAdditive && sampleRoot != null && refClip != null)
            {
                refClip.SampleAnimation(sampleRoot.gameObject, refTime);
                foreach (var pair in baseTransforms)
                {
                    Transform b = pair.Key;
                    if (b != null)
                    {
                        refHierarchy[b] = (b.localPosition, b.localRotation, b.position, b.rotation);
                    }
                }
                RestoreAllTransforms();
            }

            if (sampleRoot != null)
            {
                clip.SampleAnimation(sampleRoot.gameObject, time);
                foreach (var pair in baseTransforms)
                {
                    Transform b = pair.Key;
                    if (b != null)
                    {
                        sampledHierarchy[b] = (b.localPosition, b.localRotation, b.position, b.rotation);
                    }
                }
                RestoreAllTransforms();
            }

            var curves = ExtractCurves(clip, time);
            var refCurves = (isAdditive && refClip != null) ? ExtractCurves(refClip, refTime) : null;

            int bakedCount = 0;

            for (int i = 0; i < targetCount; i++)
            {
                if (selectedOnly && !selectedIndices.Contains(i)) continue;

                Transform bone = isAvatar ? constraint.ResolveAvatarBone(constraint.avatarTargets[i]) : constraint.targets[i].bone;
                Transform targetTrans = isAvatar ? constraint.avatarTargets[i].target : constraint.targets[i].target;
                string boneName = isAvatar ? constraint.avatarTargets[i].boneName : (bone != null ? bone.name : $"Bone_{i}");
                string bonePath = isAvatar ? constraint.avatarTargets[i].bonePath : (bone != null && sampleRoot != null ? AnimationUtility.CalculateTransformPath(bone, sampleRoot) : boneName);

                Vector3 sampledLocalPos = Vector3.zero;
                Quaternion sampledLocalRot = Quaternion.identity;
                Vector3 sampledWorldPos = Vector3.zero;
                Quaternion sampledWorldRot = Quaternion.identity;
                bool hasSample = false;

                if (bone != null && sampledHierarchy.TryGetValue(bone, out var sh))
                {
                    sampledLocalPos = sh.localPos;
                    sampledLocalRot = sh.localRot;
                    sampledWorldPos = sh.worldPos;
                    sampledWorldRot = sh.worldRot;
                    hasSample = true;
                }
                else if (TryFindCurveData(curves, boneName, bonePath, out var curveData))
                {
                    sampledLocalPos = curveData.localPosition;
                    sampledLocalRot = curveData.localRotation;
                    if (bone != null && bone.parent != null)
                    {
                        sampledWorldPos = bone.parent.TransformPoint(sampledLocalPos);
                        sampledWorldRot = bone.parent.rotation * sampledLocalRot;
                    }
                    else
                    {
                        sampledWorldPos = sampledLocalPos;
                        sampledWorldRot = sampledLocalRot;
                    }
                    hasSample = true;
                }

                if (!hasSample) continue;

                Vector3 baseLocalPos = Vector3.zero;
                Quaternion baseLocalRot = Quaternion.identity;
                Vector3 baseWorldPos = Vector3.zero;
                Quaternion baseWorldRot = Quaternion.identity;

                if (isAdditive)
                {
                    if (bone != null && refHierarchy.TryGetValue(bone, out var rh))
                    {
                        baseLocalPos = rh.localPos;
                        baseLocalRot = rh.localRot;
                        baseWorldPos = rh.worldPos;
                        baseWorldRot = rh.worldRot;
                    }
                    else if (refCurves != null && TryFindCurveData(refCurves, boneName, bonePath, out var refData))
                    {
                        baseLocalPos = refData.localPosition;
                        baseLocalRot = refData.localRotation;
                        if (bone != null && bone.parent != null)
                        {
                            baseWorldPos = bone.parent.TransformPoint(baseLocalPos);
                            baseWorldRot = bone.parent.rotation * baseLocalRot;
                        }
                        else
                        {
                            baseWorldPos = baseLocalPos;
                            baseWorldRot = baseLocalRot;
                        }
                    }
                    else if (bone != null && baseTransforms.TryGetValue(bone, out var bt))
                    {
                        baseLocalPos = bt.localPos;
                        baseLocalRot = bt.localRot;
                        baseWorldPos = bt.worldPos;
                        baseWorldRot = bt.worldRot;
                    }
                }

                Vector3 posOffset = Vector3.zero;
                Vector3 rotOffset = Vector3.zero;

                if (isWorld)
                {
                    if (isAdditive)
                    {
                        posOffset = targetTrans != null ? sampledWorldPos - baseWorldPos - targetTrans.position : sampledWorldPos - baseWorldPos;
                        Quaternion deltaRot = targetTrans != null
                            ? Quaternion.Inverse(baseWorldRot * targetTrans.rotation) * sampledWorldRot
                            : Quaternion.Inverse(baseWorldRot) * sampledWorldRot;
                        rotOffset = GetMinimalEuler(deltaRot);
                    }
                    else
                    {
                        posOffset = targetTrans != null ? sampledWorldPos - targetTrans.position : sampledWorldPos;
                        Quaternion deltaRot = targetTrans != null
                            ? Quaternion.Inverse(targetTrans.rotation) * sampledWorldRot
                            : sampledWorldRot;
                        rotOffset = GetMinimalEuler(deltaRot);
                    }
                }
                else
                {
                    if (isAdditive)
                    {
                        posOffset = targetTrans != null ? sampledLocalPos - baseLocalPos - targetTrans.localPosition : sampledLocalPos - baseLocalPos;
                        Quaternion deltaRot = targetTrans != null
                            ? Quaternion.Inverse(baseLocalRot * targetTrans.localRotation) * sampledLocalRot
                            : Quaternion.Inverse(baseLocalRot) * sampledLocalRot;
                        rotOffset = GetMinimalEuler(deltaRot);
                    }
                    else
                    {
                        posOffset = targetTrans != null ? sampledLocalPos - targetTrans.localPosition : sampledLocalPos;
                        Quaternion deltaRot = targetTrans != null
                            ? Quaternion.Inverse(targetTrans.localRotation) * sampledLocalRot
                            : sampledLocalRot;
                        rotOffset = GetMinimalEuler(deltaRot);
                    }
                }

                if (isAvatar)
                {
                    if (bakePos) constraint.avatarTargets[i].positionOffset = posOffset;
                    if (bakeRot) constraint.avatarTargets[i].rotationOffset = rotOffset;
                }
                else
                {
                    if (bakePos) constraint.targets[i].positionOffset = posOffset;
                    if (bakeRot) constraint.targets[i].rotationOffset = rotOffset;
                }

                bakedCount++;
            }

            EditorUtility.SetDirty(constraint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(constraint);
            constraint.ProcessRig(0f);
            SceneView.RepaintAll();

            string refClipName = refClip != null ? refClip.name : (constraint.bakeAdditiveRef == HonamiPoseAdditiveReference.CurrentTransform ? "Current Scene Pose" : "None");
            Debug.Log($"HonamiPoseConstraint: Baked {(isAdditive ? "additive" : "absolute")} pose from '{clip.name}' (time {time:F3}s, ref: '{refClipName}') into {bakedCount} targets.", constraint);
            return bakedCount;
        }

        public static Vector3 GetMinimalEuler(Quaternion rotation)
        {
            Vector3 e1 = rotation.eulerAngles;
            e1.x = Mathf.DeltaAngle(0f, e1.x);
            e1.y = Mathf.DeltaAngle(0f, e1.y);
            e1.z = Mathf.DeltaAngle(0f, e1.z);

            Vector3 e2 = new Vector3(
                Mathf.DeltaAngle(0f, 180f - e1.x),
                Mathf.DeltaAngle(0f, e1.y + 180f),
                Mathf.DeltaAngle(0f, e1.z + 180f)
            );

            return e1.sqrMagnitude <= e2.sqrMagnitude ? e1 : e2;
        }

        public static Vector3 GetContinuousEuler(Vector3 oldEuler, Quaternion newRotation)
        {
            Vector3 newEuler = newRotation.eulerAngles;

            Vector3 altEuler = new Vector3(
                180f - newEuler.x,
                newEuler.y + 180f,
                newEuler.z + 180f
            );

            newEuler.x = newEuler.x + Mathf.Round((oldEuler.x - newEuler.x) / 360f) * 360f;
            newEuler.y = newEuler.y + Mathf.Round((oldEuler.y - newEuler.y) / 360f) * 360f;
            newEuler.z = newEuler.z + Mathf.Round((oldEuler.z - newEuler.z) / 360f) * 360f;

            altEuler.x = altEuler.x + Mathf.Round((oldEuler.x - altEuler.x) / 360f) * 360f;
            altEuler.y = altEuler.y + Mathf.Round((oldEuler.y - altEuler.y) / 360f) * 360f;
            altEuler.z = altEuler.z + Mathf.Round((oldEuler.z - altEuler.z) / 360f) * 360f;

            if (Vector3.Distance(oldEuler, altEuler) < Vector3.Distance(oldEuler, newEuler))
            {
                return altEuler;
            }

            return newEuler;
        }
    }
}
