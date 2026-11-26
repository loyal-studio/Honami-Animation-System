using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Common;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using HonamiAnimationSystem.Runtime.Riggings;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal static class TimelinePreview
    {
        internal static void Sample(TimelineState s)
        {
            if (!s.PreviewEnabled) return;

            GameObject root = SampleRoot(s);
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

            RepaintViews(s);
        }

        // A timeline drives scene objects through its bindings, so the selection's animator is left alone.
        private static GameObject SampleRoot(TimelineState s) =>
            s.Mode == TimelineMode.HonamiTimeline ? null : GetAnimationRoot(s, Selection.activeGameObject);

        // Camera shots and screen fades only show in the Game view, which does not repaint on its own in edit mode.
        private static void RepaintViews(TimelineState s)
        {
            bool changed = s.IsPlaying || !Mathf.Approximately(s.PlayheadTime, s.LastSampledTime);
            s.LastSampledTime = s.PlayheadTime;
            if (!changed) return;

            if (s.Mode == TimelineMode.HonamiTimeline)
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            else
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
                GameObject root = SampleRoot(states[i]);
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

            for (int i = 0; i < states.Count; i++)
                RepaintViews(states[i]);
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
                SampleModules(s);
            else
            {
                SampleHonamiState(s);
                SampleModules(s);
            }
        }

        private static HonamiAvatarMask ResolveEffectiveMask(TimelineState s)
        {
            if (s.SelectedState?.avatarMask != null)
                return s.SelectedState.avatarMask;

            if (s.Controller == null || s.SelectedLayerIndex < 0
                || s.SelectedLayerIndex >= s.Controller.ActiveLayers.Count)
                return null;

            return s.Controller.ActiveLayers[s.SelectedLayerIndex].avatarMask;
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

            if (s.Mode != TimelineMode.HonamiTimeline) SampleHonamiState(s);
            SampleModules(s);

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

        internal static GameObject ResolvePrefabInstance(GameObject target)
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
            var motions = s.BlendNode?.blendMotions;
            if (motions == null || motions.Count == 0) return;

            int motionsCount = motions.Count;
            EnsureBlendBuffers(s, motionsCount);

            var weightsSpan = s.BlendCache.BlendWeights.AsSpan(0, motionsCount);
            weightsSpan.Clear();
            var sortedSpan = s.BlendCache.BlendSorted.AsSpan(0, motionsCount);
            HonamiBlendWeightUtility.CalculateWeights(motions, s.BlendPreviewValue, weightsSpan, sortedSpan);

            for (int i = 0; i < motionsCount; i++)
            {
                s.BlendCache.MotionClips[i] = motions[i].clip;
                s.BlendCache.MotionSpeeds[i] = motions[i].speed;
                s.BlendCache.MotionMirrors[i] = motions[i].mirror;
            }

            float avgDur = HonamiBlendTreeEvaluator.GetDurationFromMotions(motions, s.BlendPreviewValue);
            SampleWeightedMotions(s, rootTarget, internalT, weightsSpan, motionsCount, avgDur);
        }

        internal static void SampleBlendSpace(TimelineState s, GameObject rootTarget, float internalT)
        {
            if (s.SelectedState?.node is not HonamiBlendSpaceNode node) return;

            var motions = node.blendMotions;
            if (motions == null || motions.Count == 0) return;

            int motionsCount = motions.Count;
            EnsureBlendBuffers(s, motionsCount);

            var weightsSpan = s.BlendCache.BlendWeights.AsSpan(0, motionsCount);
            weightsSpan.Clear();

            int axisCount = node.AxisCount;
            var axes = s.NodePreviewAxes != null && s.NodePreviewAxes.Length >= axisCount
                ? s.NodePreviewAxes.AsSpan(0, axisCount)
                : ReadOnlySpan<float>.Empty;

            HonamiBlendSpaceSolver.CalculateWeights(motions, axisCount, node.spaceType, axes, weightsSpan);

            for (int i = 0; i < motionsCount; i++)
            {
                s.BlendCache.MotionClips[i] = motions[i].clip;
                s.BlendCache.MotionSpeeds[i] = motions[i].speed;
                s.BlendCache.MotionMirrors[i] = motions[i].mirror;
            }

            float avgDur = HonamiBlendSpaceSolver.GetBlendedDuration(motions, weightsSpan);
            SampleWeightedMotions(s, rootTarget, internalT, weightsSpan, motionsCount, avgDur);
        }

        private static void EnsureBlendBuffers(TimelineState s, int motionsCount)
        {
            if (s.BlendCache.BlendWeights != null && s.BlendCache.BlendWeights.Length >= motionsCount) return;

            s.BlendCache.BlendWeights = new float[motionsCount];
            s.BlendCache.BlendSorted = new (float threshold, int index)[motionsCount];
            s.BlendCache.MotionClips = new AnimationClip[motionsCount];
            s.BlendCache.MotionSpeeds = new float[motionsCount];
            s.BlendCache.MotionMirrors = new bool[motionsCount];
        }

        // Shared by the 1D blend tree and the N-dimensional blend space: sample every weighted clip
        // onto the scene target and accumulate the result into one pose.
        private static void SampleWeightedMotions(
            TimelineState s,
            GameObject rootTarget,
            float internalT,
            ReadOnlySpan<float> weights,
            int motionsCount,
            float avgDur)
        {
            var clips = s.BlendCache.MotionClips;
            var speeds = s.BlendCache.MotionSpeeds;
            var mirrors = s.BlendCache.MotionMirrors;

            GameObject sampleTarget = null;
            for (int i = 0; i < motionsCount; i++)
            {
                if (clips[i] == null || weights[i] <= 0.0001f) continue;
                sampleTarget = FindBestTargetForClip(s, rootTarget, clips[i]);
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

            for (int motionIndex = 0; motionIndex < motionsCount; motionIndex++)
            {
                AnimationClip clip = clips[motionIndex];
                float weight = weights[motionIndex];
                if (clip == null || weight <= 0.0001f) continue;

                float motionSpeed = speeds[motionIndex];
                float speed = avgDur > 0.0001f && clip.length > 0f
                    ? clip.length / avgDur
                    : (motionSpeed != 0f ? Mathf.Abs(motionSpeed) : 1f);

                float sampleTime = internalT * speed;
                if (s.SelectedState.isReversed)
                    sampleTime = clip.length - sampleTime;

                bool isLoop = s.SelectedState.loop || s.ForceLoop;
                sampleTime = clip.length > 0f
                    ? (isLoop
                        ? Mathf.Repeat(sampleTime, clip.length)
                        : Mathf.Clamp(sampleTime, 0f, clip.length))
                    : 0f;

                SafeSample(s, sampleTarget, clip, sampleTime);
                if (mirrors[motionIndex]) ApplyManualMirror(sampleTarget);

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

        private static readonly HonamiTimelinePreviewContext PreviewContext = new();

        private static void SampleModules(TimelineState s)
        {
            var container = s.ActiveContainer;
            if (container == null || container.Modules.Count == 0) return;

            var context = PreviewContext;
            context.State = s;
            context.Container = container;
            context.Director = s.Mode == TimelineMode.HonamiTimeline ? s.ResolveDirector() : null;
            context.PreviewRoot = GetAnimationRoot(s, Selection.activeGameObject);
            context.Time = s.PlayheadTime;
            context.IsPlaying = s.IsPlaying;
            context.IsScrubbing = s.IsDraggingPlayhead;
            context.PlaybackSpeed = s.PlaybackSpeed;
            context.FrameRate = s.FrameRate;

            var modules = container.Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null || module.muted || !module.SupportsHost(container.Host) || !s.IsModuleActiveAt(module, s.PlayheadTime)) continue;

                context.Module = module;
                context.TimeOffset = s.GetModuleTimeOffset(module);
                context.Time = s.PlayheadTime - context.TimeOffset;
                context.Binding = HonamiTimelineEditContext.ResolveBinding(container, module, context.Director, context.PreviewRoot);
                try
                {
                    HonamiTimelineModuleEditorRegistry.GetEditor(module).Preview(context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            context.State = null;
            context.Module = null;
            context.Binding = null;
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

        private static readonly HashSet<string> _mirroredPathsBuffer = new();

        internal static void ApplyManualMirror(GameObject root)
        {
            if (root == null) return;
            if (!root.TryGetComponent<HonamiAnimator>(out var anim) || anim.Avatar == null) return;

            var avatar = anim.Avatar;
            var mirroredPaths = _mirroredPathsBuffer;
            mirroredPaths.Clear();
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
