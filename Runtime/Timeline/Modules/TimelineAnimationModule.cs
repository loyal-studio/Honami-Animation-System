using System;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    public interface IHonamiClipSampler
    {
        void Sample(GameObject target, AnimationClip clip, float time);
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineAnimationModule : HonamiTimelineModule<TimelineAnimationClipItem>
    {
        public int layerIndex;

        public override Type BindingType => typeof(GameObject);
        public override HonamiTimelineModuleHosts SupportedHosts => HonamiTimelineModuleHosts.Timeline;
        public override bool RequiresBinding(HonamiTimelineModuleHost host) => true;

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context) =>
            context.Binding is GameObject target ? new Evaluator(this, target) : null;

        public void Evaluate(GameObject target, float time, IHonamiClipSampler sampler, HonamiTransformPose pose, ref int[] order, bool hold = false)
        {
            if (target == null || sampler == null) return;

            int count = HonamiTimelineBlend.SortByTime(this, ref order);

            // The held pose goes down first, so an item blending in starts from it.
            var held = hold ? FindHeldClip(time) : null;
            if (held != null)
                sampler.Sample(target, held.clip, held.GetSampleTime(held.Duration));

            for (int i = 0; i < count; i++)
            {
                var item = items[order[i]];
                if (item?.clip == null || !HonamiTimelineBlend.IsActive(item, time)) continue;

                float weight = HonamiTimelineBlend.Weight(this, item, time, false, hold);
                if (weight <= 0f) continue;

                float sampleTime = item.GetSampleTime(time - item.time);
                if (weight >= 1f || pose == null)
                {
                    sampler.Sample(target, item.clip, sampleTime);
                    continue;
                }

                pose.Capture();
                sampler.Sample(target, item.clip, sampleTime);
                pose.BlendFromCaptured(weight);
            }
        }

        public void Evaluate(HonamiClipTrack track, float time, ref int[] order, bool hold = false)
        {
            if (track == null) return;

            int count = HonamiTimelineBlend.SortByTime(this, ref order);
            track.SetCount(count);

            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                var item = items[order[i]];
                if (item?.clip != null) totalWeight += HonamiTimelineBlend.Weight(this, item, time, true, hold);
            }

            var held = hold ? FindHeldClip(time) : null;
            float heldWeight = held != null ? Mathf.Max(0f, 1f - totalWeight) : 0f;
            float scale = totalWeight > 1.001f ? 1f / totalWeight : 1f;

            for (int i = 0; i < count; i++)
            {
                var item = items[order[i]];
                var clip = item?.clip;
                if (item == held)
                {
                    track.Set(i, clip, held.GetSampleTime(held.Duration), heldWeight);
                    continue;
                }

                float weight = clip != null ? HonamiTimelineBlend.Weight(this, item, time, true, hold) * scale : 0f;
                track.Set(i, clip, weight > 0f ? item.GetSampleTime(time - item.time) : 0f, weight);
            }
            track.Apply();
        }

        private TimelineAnimationClipItem FindHeldClip(float time) =>
            HonamiTimelineBlend.FindHeldItem(this, time) is TimelineAnimationClipItem held && held.clip != null ? held : null;

        // Plays through the bound Honami animator's graph while the director runs, so rigs stay on top and every
        // curve blends; sampling over the hierarchy covers plain objects, disabled animators and one-off seeks.
        private sealed class Evaluator : HonamiTimelineModuleRuntime, IHonamiClipSampler
        {
            private readonly TimelineAnimationModule _module;
            private readonly GameObject _target;
            private readonly HonamiAnimatorBase _animator;
            private readonly HonamiAnimator _honamiAnimator;
            private HonamiTransformPose _pose;
            private HonamiClipTrack _track;
            private int[] _order;

            private TimelineAnimationClipItem _currentPrimaryItem;
            private TimelineAnimationClipItem _currentIncomingItem;
            private bool _isExternalPlaying;

            private PlayableGraph _standaloneGraph;
            private AnimationMixerPlayable _standaloneMixer;
            private Animator _standaloneAnimator;
            private AnimationClipPlayable[] _playables = Array.Empty<AnimationClipPlayable>();
            private AnimationClip[] _assignedClips = Array.Empty<AnimationClip>();
            private int _slotCount;

            public Evaluator(TimelineAnimationModule module, GameObject target)
            {
                _module = module;
                _target = target;
                if (!target.TryGetComponent(out _animator))
                    _animator = target.GetComponentInChildren<HonamiAnimatorBase>();
                _honamiAnimator = _animator as HonamiAnimator;
                if (_honamiAnimator == null && !target.TryGetComponent(out _honamiAnimator))
                    _honamiAnimator = target.GetComponentInChildren<HonamiAnimator>();
            }

            public override void OnPlay()
            {
                if (_honamiAnimator != null)
                {
                    _honamiAnimator.EnsureGraphBuilt();
                    if (_honamiAnimator.HasPlayableGraph)
                        return;
                }

                if (_animator != null && _animator.SupportsClipTracks && _track == null)
                    _track = _animator.AcquireClipTrack();

                bool useStandalone = _animator == null || (_honamiAnimator != null && !_honamiAnimator.HasPlayableGraph);
                if (useStandalone)
                    EnsureStandaloneGraph();
            }

            public override void OnStop() => Release();

            public override void Dispose() => Release();

            private void Release()
            {
                if (_honamiAnimator != null && _isExternalPlaying)
                {
                    _honamiAnimator.StopExternalClip(_module.layerIndex);
                    _currentPrimaryItem = null;
                    _currentIncomingItem = null;
                    _isExternalPlaying = false;
                }
                ReleaseTrack();
                TeardownStandaloneGraph();
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                bool hold = frame.Hold;

                if (_honamiAnimator != null)
                {
                    _honamiAnimator.EnsureGraphBuilt();
                    if (_honamiAnimator.HasPlayableGraph)
                    {
                        EvaluateHonamiAnimator(frame, hold);
                        return;
                    }
                }

                if (_track != null && _track.IsAttached)
                {
                    _module.Evaluate(_track, frame.Time, ref _order, hold);
                    return;
                }

                if (_target == null) return;

                bool useStandalone = _animator == null || (_honamiAnimator != null && !_honamiAnimator.HasPlayableGraph);
                if (useStandalone)
                {
                    EnsureStandaloneGraph();
                    if (_standaloneGraph.IsValid())
                    {
                        EvaluateStandalone(frame.Time, hold);
                        return;
                    }
                }

                _pose ??= new HonamiTransformPose(_target.transform);
                _module.Evaluate(_target, frame.Time, this, _pose, ref _order, hold);
            }

            private void EvaluateHonamiAnimator(in HonamiTimelineFrame frame, bool hold)
            {
                if (_honamiAnimator == null || _target == null) return;

                int count = HonamiTimelineBlend.SortByTime(_module, ref _order);
                TimelineAnimationClipItem firstActive = null;
                TimelineAnimationClipItem secondActive = null;
                int activeCount = 0;
                TimelineAnimationClipItem held = null;

                for (int i = 0; i < count; i++)
                {
                    var item = _module.items[_order[i]];
                    if (item?.clip == null || !HonamiTimelineBlend.IsActive(item, frame.Time)) continue;

                    if (activeCount == 0)
                    {
                        firstActive = item;
                    }
                    else
                    {
                        firstActive = secondActive ?? firstActive;
                        secondActive = item;
                    }
                    activeCount++;
                }

                int layer = _module.layerIndex;

                if (activeCount == 0)
                {
                    if (hold) held = _module.FindHeldClip(frame.Time);
                    if (held != null)
                    {
                        firstActive = held;
                        activeCount = 1;
                    }
                    else
                    {
                        if (_isExternalPlaying)
                        {
                            float blendOut = _currentPrimaryItem != null ? _currentPrimaryItem.blendOut : 0f;
                            var blendOutCurve = _currentPrimaryItem != null
                                ? HonamiTimelineBlend.GetBlendOutCurve(_currentPrimaryItem).ToAnimationCurve()
                                : null;
                            _honamiAnimator.StopExternalClip(layer, blendOut, blendOutCurve);
                            _currentPrimaryItem = null;
                            _currentIncomingItem = null;
                            _isExternalPlaying = false;
                            _honamiAnimator.EvaluateImmediate(0f);
                        }
                        return;
                    }
                }

                if (activeCount == 1)
                {
                    var itemA = firstActive;
                    bool isHoldingLast = itemA == held;

                    if (_currentIncomingItem != null && _currentIncomingItem == itemA)
                    {
                        _currentPrimaryItem = itemA;
                        _currentIncomingItem = null;
                        _isExternalPlaying = true;
                    }

                    float sampleTimeA = isHoldingLast
                        ? itemA.GetSampleTime(itemA.Duration)
                        : itemA.GetSampleTime(frame.Time - itemA.time);

                    if (_currentPrimaryItem != itemA || !_honamiAnimator.IsExternalClipPlaying(layer))
                    {
                        float blendIn = isHoldingLast ? 0f : HonamiTimelineBlend.GetBlendIn(_module, itemA);
                        _honamiAnimator.CrossFadeExternalClip(
                            itemA.clip,
                            blendIn,
                            layer,
                            sampleTimeA,
                            itemA.EffectiveSpeed,
                            itemA.loop,
                            HonamiTimelineBlend.GetBlendInCurve(itemA).ToAnimationCurve());
                        _currentPrimaryItem = itemA;
                        _currentIncomingItem = null;
                        _isExternalPlaying = true;

                        if (blendIn > 0f && frame.Time - itemA.time < blendIn)
                        {
                            float progress = Mathf.Clamp01((frame.Time - itemA.time) / blendIn);
                            _honamiAnimator.SetTransitionProgress(layer, progress);
                        }
                        _honamiAnimator.SetExternalClipTime(layer, sampleTimeA);
                    }
                    else
                    {
                        _honamiAnimator.SetExternalClipTime(layer, sampleTimeA);

                        if (!isHoldingLast)
                        {
                            float blendIn = HonamiTimelineBlend.GetBlendIn(_module, itemA);
                            if (blendIn > 0f && frame.Time - itemA.time < blendIn)
                            {
                                float progress = Mathf.Clamp01((frame.Time - itemA.time) / blendIn);
                                _honamiAnimator.SetTransitionProgress(layer, progress);
                            }
                        }
                    }

                    _honamiAnimator.EvaluateImmediate(0f);
                    return;
                }

                // activeCount >= 2: Crossfade between firstActive (outgoing) and secondActive (incoming)
                var outgoing = firstActive;
                var incoming = secondActive;

                float blendDuration = HonamiTimelineBlend.GetBlendIn(_module, incoming);
                if (blendDuration <= 0f)
                {
                    blendDuration = Mathf.Max(0.001f, outgoing.EndTime - incoming.time);
                }

                float elapsedInTransition = frame.Time - incoming.time;
                float transitionProgress = blendDuration > 0f ? Mathf.Clamp01(elapsedInTransition / blendDuration) : 1f;

                bool needsSetup = (_currentPrimaryItem != outgoing || _currentIncomingItem != incoming || (transitionProgress < 1f && _honamiAnimator.GetPreviousStateIndex(layer) == -1));
                if (needsSetup)
                {
                    if (_currentPrimaryItem != outgoing)
                    {
                        float sampleTimeOut = outgoing.GetSampleTime(frame.Time - outgoing.time);
                        _honamiAnimator.PlayExternalClip(
                            outgoing.clip,
                            0f,
                            layer,
                            sampleTimeOut,
                            outgoing.EffectiveSpeed,
                            outgoing.loop);
                        _currentPrimaryItem = outgoing;
                    }

                    float sampleTimeIn = incoming.GetSampleTime(frame.Time - incoming.time);
                    _honamiAnimator.CrossFadeExternalClip(
                        incoming.clip,
                        blendDuration,
                        layer,
                        sampleTimeIn,
                        incoming.EffectiveSpeed,
                        incoming.loop,
                        HonamiTimelineBlend.GetBlendInCurve(incoming).ToAnimationCurve());
                    _currentIncomingItem = incoming;
                    _isExternalPlaying = true;
                }

                _honamiAnimator.SetTransitionProgress(layer, transitionProgress);

                float currentSampleTimeIn = incoming.GetSampleTime(frame.Time - incoming.time);
                float currentSampleTimeOut = outgoing.GetSampleTime(frame.Time - outgoing.time);

                int incomingPort = _honamiAnimator.GetActiveStateIndex(layer);
                int outgoingPort = _honamiAnimator.GetPreviousStateIndex(layer);

                _honamiAnimator.SetExternalClipTime(layer, currentSampleTimeIn, incomingPort);
                if (outgoingPort != -1)
                {
                    _honamiAnimator.SetExternalClipTime(layer, currentSampleTimeOut, outgoingPort);
                }

                _honamiAnimator.EvaluateImmediate(0f);
            }

            private void EnsureStandaloneGraph()
            {
                if (_standaloneGraph.IsValid() || _target == null) return;

                _standaloneAnimator = ResolveAnimator(_target);
                if (_standaloneAnimator == null) return;

                _standaloneAnimator.enabled = true;
                _standaloneAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                _standaloneGraph = PlayableGraph.Create("TimelineAnimationModuleGraph");
                _standaloneGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(_standaloneGraph, "Animation", _standaloneAnimator);
                _standaloneMixer = AnimationMixerPlayable.Create(_standaloneGraph, 0);
                output.SetSourcePlayable(_standaloneMixer);
                _slotCount = 0;
            }

            private void TeardownStandaloneGraph()
            {
                if (_standaloneGraph.IsValid())
                {
                    _standaloneGraph.Destroy();
                }
                _playables = Array.Empty<AnimationClipPlayable>();
                _assignedClips = Array.Empty<AnimationClip>();
                _slotCount = 0;
                _standaloneAnimator = null;
            }

            private void EvaluateStandalone(float time, bool hold)
            {
                int count = HonamiTimelineBlend.SortByTime(_module, ref _order);
                if (count != _slotCount)
                {
                    _standaloneMixer.SetInputCount(count);
                    if (_playables.Length < count)
                    {
                        Array.Resize(ref _playables, count);
                        Array.Resize(ref _assignedClips, count);
                    }
                    for (int i = count; i < _slotCount; i++)
                    {
                        if (_playables[i].IsValid())
                        {
                            _standaloneGraph.Disconnect(_standaloneMixer, i);
                            _standaloneGraph.DestroyPlayable(_playables[i]);
                            _playables[i] = default;
                            _assignedClips[i] = null;
                        }
                    }
                    _slotCount = count;
                }

                var held = hold ? _module.FindHeldClip(time) : null;
                float heldWeight = 0f;
                if (held != null)
                {
                    heldWeight = 1f;
                    for (int i = 0; i < count; i++)
                    {
                        var item = _module.items[_order[i]];
                        if (item?.clip != null) heldWeight -= HonamiTimelineBlend.Weight(_module, item, time, true, true);
                    }
                    heldWeight = Mathf.Max(0f, heldWeight);
                }

                float totalWeight = 0f;
                for (int i = 0; i < count; i++)
                {
                    var item = _module.items[_order[i]];
                    var clip = item?.clip;
                    bool isHeld = item == held;
                    float weight = isHeld ? heldWeight
                        : clip != null ? HonamiTimelineBlend.Weight(_module, item, time, true, hold) : 0f;

                    if (clip != _assignedClips[i])
                    {
                        if (_playables[i].IsValid())
                        {
                            _standaloneGraph.Disconnect(_standaloneMixer, i);
                            _standaloneGraph.DestroyPlayable(_playables[i]);
                            _playables[i] = default;
                        }

                        if (clip != null && !clip.legacy)
                        {
                            var clipPlayable = AnimationClipPlayable.Create(_standaloneGraph, clip);
                            clipPlayable.SetApplyFootIK(false);
                            clipPlayable.SetSpeed(0.0);
                            clipPlayable.SetDuration(double.MaxValue);
                            _standaloneGraph.Connect(clipPlayable, 0, _standaloneMixer, i);
                            _playables[i] = clipPlayable;
                        }
                        _assignedClips[i] = clip;
                    }

                    float sampleTime = isHeld
                        ? item.GetSampleTime(item.Duration)
                        : (item != null ? item.GetSampleTime(time - item.time) : 0f);

                    if (clip != null && clip.legacy)
                    {
                        _standaloneMixer.SetInputWeight(i, 0f);
                        if (weight > 0f)
                        {
                            clip.SampleAnimation(_target, sampleTime);
                        }
                        continue;
                    }

                    _standaloneMixer.SetInputWeight(i, weight);
                    totalWeight += weight;
                    if (weight > 0f && _playables[i].IsValid())
                        _playables[i].SetTime(sampleTime);
                }

                if (totalWeight > 1.001f)
                {
                    for (int i = 0; i < count; i++)
                    {
                        float currentW = _standaloneMixer.GetInputWeight(i);
                        if (currentW > 0f)
                            _standaloneMixer.SetInputWeight(i, currentW / totalWeight);
                    }
                }

                if (count > 0)
                    _standaloneGraph.Evaluate(0f);
            }

            private static Animator ResolveAnimator(GameObject target)
            {
                if (target == null) return null;
                if (target.TryGetComponent<Animator>(out var animator)) return animator;

                Transform targetTransform = target.transform;
                int childCount = targetTransform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    if (targetTransform.GetChild(i).TryGetComponent(out animator))
                        return animator;
                }

                animator = target.GetComponentInChildren<Animator>(true);
                return animator != null ? animator : target.AddComponent<Animator>();
            }

            private void ReleaseTrack()
            {
                _track?.Release();
                _track = null;
            }

            public void Sample(GameObject target, AnimationClip clip, float time)
            {
                if (clip == null || target == null) return;
                if (clip.legacy)
                {
                    clip.SampleAnimation(target, time);
                }
            }
        }
    }
}
