#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Editor.Preview
{
    internal enum HonamiPreviewRigKind
    {
        None,
        Generic,
        Humanoid,
        Mixed
    }

    /// <summary>
    /// Humanoid half of the preview skeleton. Humanoid clips only hold muscle curves, so they cannot
    /// drive the transform-curve skeleton: instead a copy of a real Humanoid model is posed through a
    /// manual PlayableGraph. Kept apart from the Generic code so that path stays untouched.
    /// </summary>
    internal sealed partial class HonamiPreviewSkeleton
    {
        private Animator _previewAnimator;
        private PlayableGraph _playableGraph;
        private AnimationMixerPlayable _animationMixer;
        private AnimationClipPlayable[] _clipPlayables = Array.Empty<AnimationClipPlayable>();
        private readonly Dictionary<AnimationClip, int> _playableIndexByClip = new();
        private float[] _humanoidWeights = Array.Empty<float>();
        private Transform _pinAnimatorTransform;
        private Vector3 _pinLocalPosition;
        private Quaternion _pinLocalRotation;
        private Vector3 _pinLocalScale;

        public bool IsHumanoid => _root != null && _playableGraph.IsValid() && _animationMixer.IsValid();

        public static HonamiPreviewRigKind Classify(IReadOnlyList<AnimationClip> clips)
        {
            bool generic = false, humanoid = false;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                if (clips[i] == null) continue;
                if (clips[i].isHumanMotion) humanoid = true;
                else generic = true;
            }
            if (humanoid && generic) return HonamiPreviewRigKind.Mixed;
            if (humanoid) return HonamiPreviewRigKind.Humanoid;
            return generic ? HonamiPreviewRigKind.Generic : HonamiPreviewRigKind.None;
        }

        /// <summary>
        /// Builds a HideAndDontSave copy of <paramref name="model"/> (transforms and renderers only, so no
        /// user scripts run and the source asset or scene object is never animated or dirtied) and a
        /// playable graph with one slot per entry of <paramref name="slotClips"/>.
        /// </summary>
        public bool BuildHumanoid(IReadOnlyList<AnimationClip> slotClips, GameObject model, out string error)
        {
            error = null;
            Dispose();

            if (model == null || slotClips == null || slotClips.Count == 0)
            {
                error = "Assign a Preview Model with a Humanoid avatar.";
                return false;
            }

            Animator sourceAnimator = FindHumanoidAnimator(model, out error);
            if (sourceAnimator == null) return false;

            var copies = new Dictionary<Transform, Transform>();
            try
            {
                _root = new GameObject(model.name + " Honami Preview") { hideFlags = HideFlags.HideAndDontSave };
                LiveSkeletons.Add(this);
                CopyTransformTree(model.transform, _root.transform, copies);

                if (!copies.TryGetValue(sourceAnimator.transform, out Transform animatorCopy) ||
                    !CopyRenderers(model.transform, copies, out error))
                {
                    error ??= "The Humanoid Animator is outside the copied model hierarchy.";
                    Dispose();
                    return false;
                }

                // The copy is a plain Animator driven only by the graph below; no controller, no root motion.
                _previewAnimator = animatorCopy.gameObject.AddComponent<Animator>();
                _previewAnimator.avatar = sourceAnimator.avatar;
                _previewAnimator.runtimeAnimatorController = null;
                _previewAnimator.applyRootMotion = false;
                _previewAnimator.fireEvents = false;
                _previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _previewAnimator.Rebind();
                if (!_previewAnimator.isInitialized)
                    throw new InvalidOperationException("The Humanoid preview Animator did not initialize.");

                _pinAnimatorTransform = animatorCopy;
                _pinLocalPosition = animatorCopy.localPosition;
                _pinLocalRotation = animatorCopy.localRotation;
                _pinLocalScale = animatorCopy.localScale;

                CacheCopiedHierarchy();

                _playableGraph = PlayableGraph.Create("Honami Humanoid Preview");
                _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                _animationMixer = AnimationMixerPlayable.Create(_playableGraph, slotClips.Count);
                _clipPlayables = new AnimationClipPlayable[slotClips.Count];
                _humanoidWeights = new float[slotClips.Count];
                for (int i = 0; i < slotClips.Count; i++)
                {
                    AnimationClip clip = slotClips[i];
                    if (clip == null) continue;

                    var playable = AnimationClipPlayable.Create(_playableGraph, clip);
                    playable.SetDuration(double.PositiveInfinity);
                    playable.SetSpeed(0d);
                    playable.SetApplyFootIK(false);
                    playable.SetApplyPlayableIK(false);
                    _playableGraph.Connect(playable, 0, _animationMixer, i);
                    _animationMixer.SetInputWeight(i, 0f);
                    _clipPlayables[i] = playable;
                    if (!_playableIndexByClip.ContainsKey(clip)) _playableIndexByClip.Add(clip, i);
                }

                var output = AnimationPlayableOutput.Create(_playableGraph, "Humanoid Preview", _previewAnimator);
                output.SetSourcePlayable(_animationMixer);
                _playableGraph.Play();

                if (!IsBuilt)
                {
                    error = "The Preview Model has no transforms to animate.";
                    Dispose();
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not build the Humanoid preview: " + exception.Message;
                Dispose();
                return false;
            }
        }

        private static Animator FindHumanoidAnimator(GameObject model, out string error)
        {
            error = null;
            Animator found = null;
            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Avatar avatar = animators[i].avatar;
                if (avatar == null || !avatar.isValid || !avatar.isHuman) continue;
                if (found != null)
                {
                    error = "The Preview Model contains several Humanoid Animators. Choose a model with exactly one.";
                    return null;
                }
                found = animators[i];
            }
            if (found == null) error = "The Preview Model needs an Animator with a valid Humanoid avatar.";
            return found;
        }

        // The copy root keeps an identity transform (like the Generic root) so the framing and the
        // ground grid do not depend on where the source prefab or scene object happens to sit.
        private static void CopyTransformTree(Transform source, Transform target, Dictionary<Transform, Transform> map)
        {
            map.Add(source, target);
            if (target.parent != null)
            {
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
                target.gameObject.SetActive(source.gameObject.activeSelf);
            }

            for (int i = 0; i < source.childCount; i++)
            {
                Transform child = source.GetChild(i);
                var copy = new GameObject(child.name) { hideFlags = HideFlags.HideAndDontSave };
                copy.transform.SetParent(target, false);
                CopyTransformTree(child, copy.transform, map);
            }
        }

        private static bool CopyRenderers(Transform sourceRoot, Dictionary<Transform, Transform> copies, out string error)
        {
            error = null;
            bool any = false;
            foreach (var pair in copies)
            {
                Transform source = pair.Key;
                GameObject target = pair.Value.gameObject;

                var skinned = source.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.sharedMesh != null)
                {
                    Transform[] sourceBones = skinned.bones;
                    var bones = new Transform[sourceBones.Length];
                    for (int i = 0; i < sourceBones.Length; i++)
                    {
                        if (sourceBones[i] == null) continue;
                        if (!copies.TryGetValue(sourceBones[i], out bones[i]))
                        {
                            error = $"Renderer '{skinned.name}' uses a bone outside the Preview Model. Choose its common model root.";
                            return false;
                        }
                    }

                    Transform rootBone = null;
                    if (skinned.rootBone != null && !copies.TryGetValue(skinned.rootBone, out rootBone))
                    {
                        error = $"Renderer '{skinned.name}' has a root bone outside the Preview Model. Choose its common model root.";
                        return false;
                    }

                    var copy = target.AddComponent<SkinnedMeshRenderer>();
                    CopyRendererSettings(skinned, copy);
                    copy.sharedMesh = skinned.sharedMesh;
                    copy.bones = bones;
                    copy.rootBone = rootBone;
                    copy.localBounds = skinned.localBounds;
                    copy.updateWhenOffscreen = true;
                    any |= copy.enabled;
                }

                var filter = source.GetComponent<MeshFilter>();
                var meshRenderer = source.GetComponent<MeshRenderer>();
                if (filter != null && filter.sharedMesh != null && meshRenderer != null)
                {
                    target.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var copy = target.AddComponent<MeshRenderer>();
                    CopyRendererSettings(meshRenderer, copy);
                    any |= copy.enabled;
                }
            }

            if (!any) error = "The Preview Model has no enabled SkinnedMeshRenderer or MeshRenderer with a mesh.";
            return any;
        }

        private static void CopyRendererSettings(Renderer source, Renderer target)
        {
            target.enabled = source.enabled;
            target.sharedMaterials = source.sharedMaterials;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.renderingLayerMask = source.renderingLayerMask;
        }

        private void CacheCopiedHierarchy()
        {
            var list = new List<Transform>();
            CollectDescendants(_root.transform, list);
            _bones = list.ToArray();
            _parentIndex = new int[_bones.Length];

            var indexOf = new Dictionary<Transform, int>(_bones.Length);
            for (int i = 0; i < _bones.Length; i++) indexOf[_bones[i]] = i;
            for (int i = 0; i < _bones.Length; i++)
                _parentIndex[i] = indexOf.TryGetValue(_bones[i].parent, out int parent) ? parent : -1;

            // The copied pose is the base pose shown when no motion has weight (see EvaluateHumanoid).
            _baseLocalPos = new Vector3[_bones.Length];
            _baseLocalRot = new Quaternion[_bones.Length];
            _baseLocalScale = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                _baseLocalPos[i] = _bones[i].localPosition;
                _baseLocalRot[i] = _bones[i].localRotation;
                _baseLocalScale[i] = _bones[i].localScale;
            }
        }

        private static void CollectDescendants(Transform parent, List<Transform> into)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                into.Add(child);
                CollectDescendants(child, into);
            }
        }

        public void SampleHumanoidSingle(AnimationClip clip, float normalizedTime, bool reversed)
        {
            if (!IsHumanoid || clip == null) return;
            Array.Clear(_humanoidWeights, 0, _humanoidWeights.Length);
            if (_playableIndexByClip.TryGetValue(clip, out int index) && _clipPlayables[index].IsValid())
            {
                _clipPlayables[index].SetTime(ResolveClipTime(clip, normalizedTime, true, reversed));
                _humanoidWeights[index] = 1f;
            }
            EvaluateHumanoid();
        }

        /// <summary>
        /// Humanoid counterpart of <see cref="SampleBlend"/>: same weight solver and timing, applied to the graph.
        /// With <paramref name="mapByClip"/> the graph holds one slot per distinct clip (transitions mix several
        /// states); otherwise slot i belongs to motion i so repeated clips can run at their own speeds.
        /// </summary>
        public void SampleHumanoidBlend(IReadOnlyList<HonamiBlendTreeMotion> motions, float parameterValue,
            float normalizedTime, bool reversed, bool mapByClip)
        {
            if (!IsHumanoid || motions == null || motions.Count == 0) return;

            int motionCount = motions.Count;
            if (_weights.Length < motionCount)
            {
                _weights = new float[motionCount];
                _sorted = new (float, int)[motionCount];
            }

            var weights = _weights.AsSpan(0, motionCount);
            weights.Clear();
            HonamiBlendWeightUtility.CalculateWeights(motions, parameterValue, weights, _sorted.AsSpan(0, motionCount));

            float avgDur = HonamiBlendTreeEvaluator.GetDurationFromMotions(AsList(motions), parameterValue);
            SampleHumanoidMotions(motions, weights, normalizedTime, avgDur, reversed, false, mapByClip);
        }

        /// <summary>Humanoid counterpart of <see cref="SampleDirectionalBlend"/> (caller-supplied weights, canonical phase).</summary>
        public void SampleHumanoidDirectional(IReadOnlyList<HonamiBlendTreeMotion> motions,
            ReadOnlySpan<float> weights, float normalizedPhase, bool reversed, bool mapByClip)
        {
            if (!IsHumanoid || motions == null || motions.Count == 0 || weights.Length < motions.Count) return;
            SampleHumanoidMotions(motions, weights, normalizedPhase, 0f, reversed, true, mapByClip);
        }

        private void SampleHumanoidMotions(IReadOnlyList<HonamiBlendTreeMotion> motions,
            ReadOnlySpan<float> weights, float normalizedTime, float avgDur, bool reversed,
            bool useCanonicalChildPhase, bool mapByClip)
        {
            Array.Clear(_humanoidWeights, 0, _humanoidWeights.Length);
            for (int m = 0; m < motions.Count; m++)
            {
                var motion = motions[m];
                float weight = weights[m];
                if (motion?.clip == null || weight <= 0.0001f) continue;

                int slot = m;
                if (mapByClip && !_playableIndexByClip.TryGetValue(motion.clip, out slot)) continue;
                if (slot >= _clipPlayables.Length || !_clipPlayables[slot].IsValid()) continue;

                // Same timing as the Generic SampleWeightedMotions so both rigs show the same phase.
                float clipLength = motion.clip.length;
                float speed = avgDur > 0.0001f && clipLength > 0f
                    ? clipLength / avgDur
                    : (motion.speed != 0f ? Mathf.Abs(motion.speed) : 1f);
                // The canonical phase shares the Generic helper, so cycle offsets cannot drift between rigs.
                float sampleTime = useCanonicalChildPhase
                    ? DirectionalSamplePhase(normalizedTime, HonamiBlendTreeNode.WrapCycleOffset(motion.cycleOffset)) * clipLength
                    : normalizedTime * avgDur * speed;
                if (reversed) sampleTime = clipLength - sampleTime;
                sampleTime = clipLength > 0f
                    ? Mathf.Clamp(sampleTime, 0f, Mathf.Max(0f, clipLength - EndEpsilon))
                    : 0f;

                _clipPlayables[slot].SetTime(sampleTime);
                _humanoidWeights[slot] += weight;
            }
            EvaluateHumanoid();
        }

        private void EvaluateHumanoid()
        {
            // Normalize like the Generic SampleWeightedMotions (same epsilon; below it the base pose is shown)
            // so both rigs blend identically instead of relying on the mixer's own handling of raw weights.
            float total = 0f;
            for (int i = 0; i < _humanoidWeights.Length; i++) total += _humanoidWeights[i];

            if (total <= 0.0001f)
            {
                ResetToBase();
            }
            else
            {
                float invTotal = 1f / total;
                for (int i = 0; i < _humanoidWeights.Length; i++)
                    _animationMixer.SetInputWeight(i, _humanoidWeights[i] * invTotal);
                _playableGraph.Evaluate(0f);
            }

            // Defensive: keep the animated root where the model was copied so the preview never drifts.
            if (_pinAnimatorTransform != null)
            {
                _pinAnimatorTransform.localPosition = _pinLocalPosition;
                _pinAnimatorTransform.localRotation = _pinLocalRotation;
                _pinAnimatorTransform.localScale = _pinLocalScale;
            }
        }

        private void DisposeHumanoid()
        {
            if (_playableGraph.IsValid())
            {
                _playableGraph.Stop();
                _playableGraph.Destroy();
            }
            _playableGraph = default;
            _animationMixer = default;
            _clipPlayables = Array.Empty<AnimationClipPlayable>();
            _playableIndexByClip.Clear();
            _humanoidWeights = Array.Empty<float>();
            _pinAnimatorTransform = null;
            _previewAnimator = null;
        }
    }
}
#endif
