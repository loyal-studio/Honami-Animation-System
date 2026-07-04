using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Common;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal static class TimelinePreview
    {
        internal static void Sample(TimelineState s)
        {
            if (!s.PreviewEnabled) return;

            GameObject root = GetAnimationRoot(s, Selection.activeGameObject);
            if (s.Mode == TimelineMode.HonamiState && root == null)
                return;

            ResolveComponents(root, out var animator, out var replacer);

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            CreateProxies(s, root, animator, replacer);

            AnimationMode.BeginSampling();

            SampleState(s, root);

            if (root != null) ApplyAndCleanup(root, animator, replacer);

            AnimationMode.EndSampling();

            if (s.Mode == TimelineMode.HonamiState && root != null)
                ApplyConstraints(s, root);

            SceneView.RepaintAll();
        }

        private static readonly List<GameObject> RootBuffer = new();
        private static readonly List<HonamiAnimator> AnimatorBuffer = new();
        private static readonly List<HonamiBoneReplacer> ReplacerBuffer = new();

        internal static void SampleMany(List<TimelineState> states)
        {
            if (states == null || states.Count == 0) return;
            if (states.Count == 1)
            {
                Sample(states[0]);
                return;
            }

            SortBySampleOrder(states);

            RootBuffer.Clear();
            AnimatorBuffer.Clear();
            ReplacerBuffer.Clear();

            bool anyRoot = false;
            bool anyStateMode = false;
            for (int i = 0; i < states.Count; i++)
            {
                GameObject root = GetAnimationRoot(states[i], Selection.activeGameObject);
                RootBuffer.Add(root);
                if (root != null) anyRoot = true;
                if (states[i].Mode == TimelineMode.HonamiState) anyStateMode = true;

                ResolveComponents(root, out var animator, out var replacer);
                AnimatorBuffer.Add(animator);
                ReplacerBuffer.Add(replacer);
            }

            if (!anyRoot && anyStateMode) return;

            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            for (int i = 0; i < states.Count; i++)
                CreateProxies(states[i], RootBuffer[i], AnimatorBuffer[i], ReplacerBuffer[i]);

            AnimationMode.BeginSampling();

            for (int i = 0; i < states.Count; i++)
                SampleState(states[i], RootBuffer[i]);

            for (int i = 0; i < states.Count; i++)
            {
                GameObject root = RootBuffer[i];
                if (root == null) continue;

                bool alreadyProcessed = false;
                for (int j = 0; j < i; j++)
                {
                    if (RootBuffer[j] == root)
                    {
                        alreadyProcessed = true;
                        break;
                    }
                }
                if (!alreadyProcessed)
                    ApplyAndCleanup(root, AnimatorBuffer[i], ReplacerBuffer[i]);
            }

            AnimationMode.EndSampling();

            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s.PreviewEnabled && s.Mode == TimelineMode.HonamiState && RootBuffer[i] != null)
                    ApplyConstraints(s, RootBuffer[i]);
            }

            SceneView.RepaintAll();
        }

        // Lower layers sample first so higher layers and masked states override them; insertion sort keeps equal keys stable
        private static void SortBySampleOrder(List<TimelineState> states)
        {
            for (int i = 1; i < states.Count; i++)
            {
                var current = states[i];
                int key = SampleOrder(current);
                int j = i - 1;
                while (j >= 0 && SampleOrder(states[j]) > key)
                {
                    states[j + 1] = states[j];
                    j--;
                }
                states[j + 1] = current;
            }
        }

        private static int SampleOrder(TimelineState s)
        {
            int layer = s.SelectedLayerIndex >= 0 ? s.SelectedLayerIndex : 0;
            return layer * 2 + (ResolveEffectiveMask(s) != null ? 1 : 0);
        }

        private static void ResolveComponents(GameObject root, out HonamiAnimator animator, out HonamiBoneReplacer replacer)
        {
            animator = null;
            replacer = null;
            if (root == null) return;
            root.TryGetComponent(out animator);
            root.TryGetComponent(out replacer);
        }

        private static void SampleState(TimelineState s, GameObject root)
        {
            if (!s.PreviewEnabled) return;
            if (s.Mode == TimelineMode.HonamiState && s.SelectedState == null) return;

            HonamiAvatarMask mask = ResolveEffectiveMask(s);
            if (mask != null && root != null)
                SampleHonamiStateWithMask(s, root, mask);
            else if (s.Mode == TimelineMode.HonamiTimeline)
                SampleTimeline(s);
            else
                SampleHonamiState(s);
        }

        private static HonamiAvatarMask ResolveEffectiveMask(TimelineState s)
        {
            if (s.SelectedState?.avatarMask != null)
                return s.SelectedState.avatarMask;

            if (s.Controller == null || s.SelectedLayerIndex < 0
                || s.SelectedLayerIndex >= s.Controller.layers.Count)
                return null;

            return s.Controller.layers[s.SelectedLayerIndex].avatarMask;
        }

        private static void SampleHonamiStateWithMask(TimelineState s, GameObject root, HonamiAvatarMask mask)
        {
            if (root == null || mask == null) return;

            if (s.MaskCache.Root != root || s.MaskCache.Mask != mask)
            {
                s.MaskCache.Root = root;
                s.MaskCache.Mask = mask;
                s.MaskCache.WeightedTransforms ??= new List<(Transform t, float weight)>();
                s.MaskCache.WeightedTransforms.Clear();

                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    var t = allTransforms[i];
                    string path = GetRelativePath(root.transform, t);
                    float w = mask.GetWeight(path.GetHashCode());
                    if (w < 1.0f)
                        s.MaskCache.WeightedTransforms.Add((t, w));
                }
            }

            var weighted = s.MaskCache.WeightedTransforms;
            int count = weighted.Count;

            if (s.MaskCache.SavedPos == null || s.MaskCache.SavedPos.Length < count)
            {
                s.MaskCache.SavedPos = new Vector3[count];
                s.MaskCache.SavedRot = new Quaternion[count];
            }

            var savedPos = s.MaskCache.SavedPos;
            var savedRot = s.MaskCache.SavedRot;

            for (int i = 0; i < count; i++)
            {
                var item = weighted[i];
                savedPos[i] = item.t.localPosition;
                savedRot[i] = item.t.localRotation;
            }

            if (s.Mode == TimelineMode.HonamiTimeline) SampleTimeline(s);
            else SampleHonamiState(s);

            for (int i = 0; i < count; i++)
            {
                var (t, w) = weighted[i];
                if (w <= 0.001f)
                {
                    t.localPosition = savedPos[i];
                    t.localRotation = savedRot[i];
                }
                else
                {
                    t.localPosition = Vector3.Lerp(savedPos[i], t.localPosition, w);
                    t.localRotation = Quaternion.Slerp(savedRot[i], t.localRotation, w);
                }
            }
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;
            System.Text.StringBuilder sb = new();
            Transform cur = target;
            while (cur != null && cur != root)
            {
                if (sb.Length > 0) sb.Insert(0, '/');
                sb.Insert(0, cur.name);
                cur = cur.parent;
            }
            return sb.ToString();
        }

        private static void SampleHonamiState(TimelineState s)
        {
            GameObject rootTarget = GetAnimationRoot(s, Selection.activeGameObject);
            if (rootTarget == null) return;

            if (s.Mode != TimelineMode.HonamiClipEdit && s.SelectedState == null) return;

            rootTarget = ResolvePrefabInstance(rootTarget);

            float internalT = s.PlayheadTime;

            if (s.Mode == TimelineMode.HonamiClipEdit)
            {
                if (s.ActiveClip != null)
                {
                    GameObject matchedTarget = FindBestTargetForClip(s, rootTarget, s.ActiveClip);
                    SafeSample(s, matchedTarget, s.ActiveClip, internalT);
                }
                return;
            }

            if (s.SelectedState != null && s.SelectedState.node != null)
            {
                var editor = HonamiNodeEditorRegistry.GetEditor(s.SelectedState.node);
                if (editor != null) editor.SampleTimelinePreview(s, rootTarget, internalT);
            }
        }

        private static GameObject ResolvePrefabInstance(GameObject target)
        {
            if (target == null || !PrefabUtility.IsPartOfPrefabAsset(target)) return target;

            var instances = UnityEngine.Object.FindObjectsByType<HonamiAnimator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var inst in instances)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(inst.gameObject) == target)
                    return inst.gameObject;
            }
            return target;
        }

        internal static void SampleBlendTree(TimelineState s, GameObject rootTarget, float internalT)
        {
            if (s.BlendNode?.blendMotions == null || s.BlendNode.blendMotions.Count == 0)
                return;

            int motionsCount = s.BlendNode.blendMotions.Count;
            if (s.BlendCache.BlendWeights == null || s.BlendCache.BlendWeights.Length < motionsCount)
            {
                s.BlendCache.BlendWeights = new float[motionsCount];
                s.BlendCache.BlendSorted = new (float threshold, int index)[motionsCount];
            }

            var weightsSpan = s.BlendCache.BlendWeights.AsSpan(0, motionsCount);
            weightsSpan.Clear();
            var sortedSpan = s.BlendCache.BlendSorted.AsSpan(0, motionsCount);

            HonamiBlendWeightUtility.CalculateWeights(s.BlendNode.blendMotions, s.BlendPreviewValue, weightsSpan, sortedSpan);

            var motions = s.BlendNode.blendMotions;
            GameObject sampleTarget = null;
            for (int i = 0; i < motionsCount; i++)
            {
                var motion = motions[i];
                if (motion.clip == null || weightsSpan[i] <= 0.0001f) continue;
                sampleTarget = FindBestTargetForClip(s, rootTarget, motion.clip);
                break;
            }

            if (sampleTarget == null) return;

            if (s.BlendCache.Target != sampleTarget || s.BlendCache.Transforms == null)
            {
                s.BlendCache.Target = sampleTarget;
                s.BlendCache.Transforms = sampleTarget.GetComponentsInChildren<Transform>(true);
                int len = s.BlendCache.Transforms.Length;
                s.BlendCache.BasePositions = new Vector3[len];
                s.BlendCache.BaseRotations = new Quaternion[len];
                s.BlendCache.BaseScales = new Vector3[len];
                s.BlendCache.BlendedPositions = new Vector3[len];
                s.BlendCache.BlendedScales = new Vector3[len];
                s.BlendCache.BlendedRotations = new Quaternion[len];
                s.BlendCache.HasRotation = new bool[len];
            }

            var transforms = s.BlendCache.Transforms;
            int transformCount = transforms.Length;
            if (transformCount == 0) return;

            var basePositions = s.BlendCache.BasePositions;
            var baseRotations = s.BlendCache.BaseRotations;
            var baseScales = s.BlendCache.BaseScales;

            for (int i = 0; i < transformCount; i++)
            {
                Transform t = transforms[i];
                basePositions[i] = t.localPosition;
                baseRotations[i] = t.localRotation;
                baseScales[i] = t.localScale;
            }

            var blendedPositions = s.BlendCache.BlendedPositions;
            var blendedScales = s.BlendCache.BlendedScales;
            var blendedRotations = s.BlendCache.BlendedRotations;
            var hasRotation = s.BlendCache.HasRotation;

            Array.Clear(blendedPositions, 0, transformCount);
            Array.Clear(blendedScales, 0, transformCount);
            Array.Clear(hasRotation, 0, transformCount);

            float totalWeight = 0f;
            float avgDur = HonamiBlendTreeEvaluator.GetDurationFromMotions(motions, s.BlendPreviewValue);

            for (int motionIndex = 0; motionIndex < motionsCount; motionIndex++)
            {
                var motion = motions[motionIndex];
                float weight = weightsSpan[motionIndex];
                if (motion.clip == null || weight <= 0.0001f) continue;

                float speed;
                if (avgDur > 0.0001f && motion.clip.length > 0f)
                    speed = motion.clip.length / avgDur;
                else
                    speed = motion.speed != 0f ? Mathf.Abs(motion.speed) : 1f;

                float sampleTime = internalT * speed;
                if (s.SelectedState.isReversed)
                    sampleTime = motion.clip.length - sampleTime;

                bool isLoop = s.SelectedState.loop || s.ForceLoop;
                sampleTime = motion.clip.length > 0f
                    ? (isLoop
                        ? Mathf.Repeat(sampleTime, motion.clip.length)
                        : Mathf.Clamp(sampleTime, 0f, motion.clip.length))
                    : 0f;

                SafeSample(s, sampleTarget, motion.clip, sampleTime);
                if (motion.mirror) ApplyManualMirror(sampleTarget);

                float nextTotal = totalWeight + weight;
                float rotationBlend = nextTotal > 0f ? weight / nextTotal : 1f;
                for (int i = 0; i < transformCount; i++)
                {
                    Transform t = transforms[i];
                    blendedPositions[i] += t.localPosition * weight;
                    blendedScales[i] += t.localScale * weight;
                    blendedRotations[i] = hasRotation[i]
                        ? Quaternion.Slerp(blendedRotations[i], t.localRotation, rotationBlend)
                        : t.localRotation;
                    hasRotation[i] = true;
                }

                totalWeight = nextTotal;

                for (int i = 0; i < transformCount; i++)
                {
                    transforms[i].localPosition = basePositions[i];
                    transforms[i].localRotation = baseRotations[i];
                    transforms[i].localScale = baseScales[i];
                }
            }

            if (totalWeight > 0.0001f)
            {
                float invTotal = 1f / totalWeight;
                for (int i = 0; i < transformCount; i++)
                {
                    transforms[i].localPosition = blendedPositions[i] * invTotal;
                    transforms[i].localRotation = hasRotation[i] ? blendedRotations[i] : baseRotations[i];
                    transforms[i].localScale = blendedScales[i] * invTotal;
                }
            }
        }

        internal static void SafeSample(TimelineState s, GameObject target, AnimationClip clip, float time)
        {
            if (target == null || clip == null) return;

            if (time >= clip.length)
                time = clip.length - 0.001f;

            if (!s.AnimatorCache.TryGetValue(target, out var anim) || anim == null)
            {
                if (!target.TryGetComponent(out anim) && !PrefabUtility.IsPartOfPrefabAsset(target))
                {
                    anim = target.AddComponent<Animator>();
                    if (anim != null)
                        anim.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
                }
                s.AnimatorCache[target] = anim;
            }

            AnimationMode.SampleAnimationClip(target, clip, time);
        }

        internal static GameObject FindBestTargetForClip(TimelineState s, GameObject root, AnimationClip clip)
        {
            if (root == null || clip == null) return root;

            int key = HonamiObjectHash.Of(root) ^ HonamiObjectHash.Of(clip);
            if (s.ClipTargetCache.TryGetValue(key, out var cached))
                return cached;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            string firstPath = null;
            foreach (var b in bindings)
            {
                if (!string.IsNullOrEmpty(b.path))
                {
                    firstPath = b.path;
                    break;
                }
            }

            GameObject result = root;
            if (!string.IsNullOrEmpty(firstPath) && root.transform.Find(firstPath) == null)
            {
                var allTransforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    Transform t = allTransforms[i];
                    if (t != root.transform && t.Find(firstPath) != null)
                    {
                        result = t.gameObject;
                        break;
                    }
                }
            }

            s.ClipTargetCache[key] = result;
            return result;
        }

        private static void SampleTimeline(TimelineState s)
        {
            if (s.ActiveTimeline == null) return;
            float time = s.PlayheadTime;

            var tracks = s.ActiveTimeline.tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.muted || track.trackType != HonamiTimelineTrackType.Animation) continue;

                GameObject target = track.target;
                if (target == null)
                    target = GetAnimationRoot(s, Selection.activeGameObject);

                target = ResolvePrefabInstance(target);
                if (target == null) continue;

                var clips = track.clips;
                for (int clipIdx = 0; clipIdx < clips.Count; clipIdx++)
                {
                    var clip = clips[clipIdx];
                    if (clip.clip == null) continue;
                    float local = time - clip.startTime;
                    if (local < 0f || local > clip.GetDuration()) continue;
                    SafeSample(s, target, clip.clip, clip.GetSampleTime(local));
                }
            }
        }

        private static void ApplyConstraints(TimelineState s, GameObject target)
        {
            if (s.SelectedState == null) return;
            var c = s.SelectedState.constraints;
            if (!c.AnyLocked()) return;

            if (s.CachedConstraintsTarget != target || s.CachedTransforms == null)
            {
                s.CachedConstraintsTarget = target;
                s.CachedTransforms = target.GetComponentsInChildren<Transform>(true);
            }

            var transforms = s.CachedTransforms;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null) continue;
                if (c.lockPositionX || c.lockPositionY || c.lockPositionZ)
                {
                    Vector3 p = t.localPosition;
                    if (c.lockPositionX) p.x = 0;
                    if (c.lockPositionY) p.y = 0;
                    if (c.lockPositionZ) p.z = 0;
                    t.localPosition = p;
                }
                if (c.lockRotationX || c.lockRotationY || c.lockRotationZ)
                {
                    Vector3 r = t.localEulerAngles;
                    if (c.lockRotationX) r.x = 0;
                    if (c.lockRotationY) r.y = 0;
                    if (c.lockRotationZ) r.z = 0;
                    t.localEulerAngles = r;
                }
                if (c.lockScaleX || c.lockScaleY || c.lockScaleZ)
                {
                    Vector3 sc = t.localScale;
                    if (c.lockScaleX) sc.x = 1;
                    if (c.lockScaleY) sc.y = 1;
                    if (c.lockScaleZ) sc.z = 1;
                    t.localScale = sc;
                }
            }
        }

        private static string FindBonePath(HonamiAvatar avatar, string boneName)
        {
            var bones = avatar.bones;
            for (int i = 0; i < bones.Count; i++)
            {
                if (string.Equals(bones[i].boneName, boneName, StringComparison.OrdinalIgnoreCase))
                    return bones[i].bonePath;
            }
            return null;
        }

        private static void CreateProxies(TimelineState s, GameObject root, HonamiAnimator animator, HonamiBoneReplacer replacer)
        {
            if (root == null || animator == null || replacer == null || animator.Avatar == null) return;

            var replacements = replacer.replacements;

            for (int i = 0; i < replacements.Count; i++)
            {
                var r = replacements[i];
                if (r.targetTransform == null || string.IsNullOrEmpty(r.boneName)) continue;

                string path = FindBonePath(animator.Avatar, r.boneName);
                if (string.IsNullOrEmpty(path)) continue;

                if (root.transform.Find(path) != null) continue;

                var parts = path.Split('/');
                Transform current = root.transform;
                for (int pi = 0; pi < parts.Length; pi++)
                {
                    Transform next = current.Find(parts[pi]);
                    if (next == null)
                    {
                        var go = new GameObject(parts[pi]) { hideFlags = HideFlags.HideAndDontSave };
                        next = go.transform;
                        next.SetParent(current, false);
                        next.localPosition = Vector3.zero;
                        next.localRotation = Quaternion.identity;
                        s.Proxies.Add(go);
                    }
                    current = next;
                }
            }
        }

        private static void ApplyAndCleanup(GameObject root, HonamiAnimator animator, HonamiBoneReplacer replacer)
        {
            if (root == null) return;

            Vector3 rootPos = root.transform.localPosition;
            Quaternion rootRot = root.transform.localRotation;

            if (animator != null && replacer != null && animator.Avatar != null)
            {
                var replacements = replacer.replacements;

                for (int i = 0; i < replacements.Count; i++)
                {
                    var r = replacements[i];
                    if (r.targetTransform == null || string.IsNullOrEmpty(r.boneName)) continue;

                    string path = FindBonePath(animator.Avatar, r.boneName);
                    if (string.IsNullOrEmpty(path))
                    {
                        r.targetTransform.localPosition = root.transform.localPosition;
                        r.targetTransform.localRotation = root.transform.localRotation;
                        continue;
                    }

                    Transform original = root.transform.Find(path);
                    if (original == null || original == r.targetTransform) continue;

                    r.targetTransform.localPosition = original.localPosition;
                    r.targetTransform.localRotation = original.localRotation;

                    if ((original.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0 && original != root.transform)
                    {
                        original.localPosition = Vector3.zero;
                        original.localRotation = Quaternion.identity;
                    }
                }
            }

            root.transform.localPosition = rootPos;
            root.transform.localRotation = rootRot;
        }

        internal static GameObject GetAnimationRoot(TimelineState s, GameObject selected)
        {
            if (selected != null)
            {
                Transform current = selected.transform;
                while (current != null)
                {
                    if (current.TryGetComponent<HonamiAnimator>(out _) || current.TryGetComponent<Animator>(out _))
                    {
                        s.PreviewRoot = current.gameObject;
                        return s.PreviewRoot;
                    }
                    current = current.parent;
                }
            }

            if (s.PreviewRoot != null) return s.PreviewRoot;

            return selected;
        }

        internal static void ApplyManualMirror(GameObject root)
        {
            if (root == null) return;
            if (!root.TryGetComponent<HonamiAnimator>(out var anim) || anim.Avatar == null) return;

            var avatar = anim.Avatar;
            var mirroredPaths = new HashSet<string>();
            root.TryGetComponent<HonamiBoneReplacer>(out var replacer);

            for (int i = 0; i < avatar.mirrorBones.Count; i++)
            {
                var pair = avatar.mirrorBones[i];
                Transform boneA = FindSourceOrReplacement(root, replacer, pair.boneA, avatar);
                Transform boneB = FindSourceOrReplacement(root, replacer, pair.boneB, avatar);

                if (boneA != null && boneB != null)
                {
                    Vector3 posA = boneA.localPosition;
                    Quaternion rotA = boneA.localRotation;
                    Vector3 posB = boneB.localPosition;
                    Quaternion rotB = boneB.localRotation;

                    boneA.localPosition = new Vector3(-posB.x, posB.y, posB.z);
                    boneA.localRotation = new Quaternion(rotB.x, -rotB.y, -rotB.z, rotB.w);

                    boneB.localPosition = new Vector3(-posA.x, posA.y, posA.z);
                    boneB.localRotation = new Quaternion(rotA.x, -rotA.y, -rotA.z, rotA.w);
                }
                mirroredPaths.Add(pair.boneA);
                mirroredPaths.Add(pair.boneB);
            }

            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var bone = avatar.bones[i];
                if (!bone.enabled || mirroredPaths.Contains(bone.bonePath)) continue;
                Transform t = FindSourceOrReplacement(root, replacer, bone.bonePath, avatar, bone.boneName);
                if (t != null)
                {
                    Vector3 p = t.localPosition;
                    Quaternion r = t.localRotation;
                    t.localPosition = new Vector3(-p.x, p.y, p.z);
                    t.localRotation = new Quaternion(r.x, -r.y, -r.z, r.w);
                }
            }
        }

        private static Transform FindSourceOrReplacement(GameObject root, HonamiBoneReplacer replacer, string path, HonamiAvatar avatar, string fallbackName = null)
        {
            Transform t = root.transform.Find(path);
            if (replacer != null)
            {
                string boneName = fallbackName;
                if (boneName == null)
                {
                    var entry = avatar.FindByPath(path);
                    if (entry != null) boneName = entry.boneName;
                }
                if (boneName != null)
                {
                    Transform target = replacer.GetReplacement(boneName);
                    if (target != null) return target;
                }
                if (replacer.disableUnreplacedBones) return null;
            }
            return t;
        }
    }
}
