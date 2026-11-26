using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public enum HonamiBroadcastTargetMode
    {
        AllLinked,
        ChildrenOnly,
        ByTag
    }

    public enum HonamiBrainLinkMode
    {
        Childs,
        Manual
    }

    [AddComponentMenu("Honami Animation/Honami Linked Animator Brain")]
    public sealed class HonamiLinkedAnimator : MonoBehaviour
    {
        [Header("Link Settings")]
        public HonamiBrainLinkMode linkMode = HonamiBrainLinkMode.Childs;
        public List<HonamiAnimatorBase> manualAnimators = new List<HonamiAnimatorBase>();

        [Header("Blueprint Graph")]
        public HonamiLinkedAnimatorGraph graph;

        private readonly HashSet<HonamiAnimatorBase> _linkedAnimators = new HashSet<HonamiAnimatorBase>();

        // Controller-backed subset, so parameter broadcasts need no per-call type test.
        private readonly List<HonamiAnimator> _fullAnimators = new List<HonamiAnimator>();

        private readonly HonamiLinkedAnimatorExecutor _executor = new();

        public IReadOnlyCollection<HonamiAnimatorBase> LinkedAnimators => _linkedAnimators;
        public IReadOnlyList<HonamiAnimator> FullAnimators => _fullAnimators;
        public HonamiLinkedAnimatorGraph Graph => graph;
        public IReadOnlyList<HonamiLinkedAnimatorNodeBase> ActiveNodes => _executor.ActiveNodes;

        private void Awake()
        {
            _executor.Initialize(this);
            RefreshLinkedAnimators();
        }

        private void Update()
        {
            if (_executor.HasActiveExecutions)
                _executor.Tick(Time.deltaTime);
        }

        private readonly List<HonamiAnimatorBase> _animatorBuffer = new List<HonamiAnimatorBase>();

        private readonly List<HonamiAnimatorBase> _linkedSweepBuffer = new List<HonamiAnimatorBase>();
        private readonly List<HonamiAnimator> _fullSweepBuffer = new List<HonamiAnimator>();
        private int _sweepDepth;

        // Anything that plays a state runs user code: state enter/exit callbacks can Link, Unlink or
        // destroy animators, which mutates the very collection being walked - a HashSet throws outright,
        // and a List silently skips entries. Every fan-out that can reach user code walks a snapshot.
        // Only the outermost sweep owns the reusable buffer; a nested one allocates its own.
        private List<HonamiAnimatorBase> BeginLinkedSweep()
        {
            List<HonamiAnimatorBase> buffer = _sweepDepth == 0
                ? _linkedSweepBuffer
                : new List<HonamiAnimatorBase>(_linkedAnimators.Count);

            buffer.Clear();
            foreach (var animator in _linkedAnimators)
            {
                if (animator != null) buffer.Add(animator);
            }

            _sweepDepth++;
            return buffer;
        }

        private List<HonamiAnimator> BeginFullSweep()
        {
            List<HonamiAnimator> buffer = _sweepDepth == 0
                ? _fullSweepBuffer
                : new List<HonamiAnimator>(_fullAnimators.Count);

            buffer.Clear();
            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (_fullAnimators[i] != null) buffer.Add(_fullAnimators[i]);
            }

            _sweepDepth++;
            return buffer;
        }

        private void EndSweep<T>(List<T> buffer)
        {
            buffer.Clear();
            _sweepDepth--;
        }

        public void RefreshLinkedAnimators()
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null && anim._linkedBrain == this)
                    anim._linkedBrain = null;
            }
            _linkedAnimators.Clear();
            _fullAnimators.Clear();

            if (linkMode == HonamiBrainLinkMode.Childs)
            {
                _animatorBuffer.Clear();
                GetComponentsInChildren<HonamiAnimatorBase>(true, _animatorBuffer);
                foreach (var child in _animatorBuffer)
                {
                    if (child != null && !child.preventLinking)
                    {
                        Track(child);
                    }
                }
            }
            else if (linkMode == HonamiBrainLinkMode.Manual)
            {
                if (manualAnimators != null)
                {
                    foreach (var anim in manualAnimators)
                    {
                        if (anim != null && !anim.preventLinking)
                        {
                            Track(anim);
                        }
                    }
                }
            }
        }

        private void Track(HonamiAnimatorBase animator)
        {
            if (!_linkedAnimators.Add(animator)) return;

            animator._linkedBrain = this;
            if (animator is HonamiAnimator full)
                _fullAnimators.Add(full);
        }

        public void Link(HonamiAnimatorBase animator)
        {
            if (animator == null || animator.preventLinking) return;

            Track(animator);

            if (linkMode == HonamiBrainLinkMode.Manual && !manualAnimators.Contains(animator))
                manualAnimators.Add(animator);

            if (animator is HonamiAnimator full && full.CurrentController != null)
                full.SetController(full.CurrentController);
        }

        public void Unlink(HonamiAnimatorBase animator)
        {
            if (animator == null) return;

            _linkedAnimators.Remove(animator);
            if (animator is HonamiAnimator full)
                _fullAnimators.Remove(full);

            if (animator._linkedBrain == this)
                animator._linkedBrain = null;

            if (linkMode == HonamiBrainLinkMode.Manual)
                manualAnimators.Remove(animator);
        }

        private void OnDestroy()
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null && anim._linkedBrain == this)
                    anim._linkedBrain = null;
            }
        }

        /// <summary>
        /// Swaps the controller on every controller-backed linked animator.
        /// </summary>
        public void SetController(
            HonamiRuntimeController newController,
            float transitionDuration = 0f,
            AnimationCurve transitionCurve = null,
            HonamiControllerTransitionMode mode = HonamiControllerTransitionMode.ContinueEvaluating)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].SetController(newController, transitionDuration, transitionCurve, mode);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        /// <summary>
        /// Applies a controller profile state on every linked animator that carries a <see cref="HonamiControllerProfile"/>.
        /// </summary>
        public void SetProfile(
            string stateName,
            float transitionDuration = -1f,
            AnimationCurve transitionCurve = null,
            HonamiControllerTransitionMode? mode = null)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    HonamiAnimator animator = sweep[i];
                    if (animator != null && animator.TryGetComponent<HonamiControllerProfile>(out var profile))
                        profile.SetProfile(stateName, transitionDuration, transitionCurve, mode);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        private float _globalWeight = 1f;

        public float GlobalWeight
        {
            get => _globalWeight;
            set
            {
                _globalWeight = Mathf.Clamp01(value);
                foreach (var animator in _linkedAnimators)
                {
                    if (animator != null) animator.GlobalWeight = _globalWeight;
                }
            }
        }

        /// <summary>
        /// Fires an action on every linked animator the target accepts. Returns how many reacted.
        /// </summary>
        /// <summary>
        /// Fires an action on every linked animator the target accepts. Returns how many reacted.
        /// </summary>
        public int SetActionID(HonamiActionID actionId, float transitionDuration = 0.25f, in HonamiLinkedTarget target = default)
        {
            if (actionId == null) return 0;

            int reached = 0;
            List<HonamiAnimatorBase> sweep = BeginLinkedSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].ReactToAction(actionId, transitionDuration);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }

            return reached;
        }

        /// <summary>
        /// Plays a state by name - a clip name on Clip Players - on every linked animator the target accepts.
        /// Returns how many were told to play.
        /// </summary>
        public int PlayState(string stateName, float transitionDuration = 0.25f, in HonamiLinkedTarget target = default)
        {
            int reached = 0;
            List<HonamiAnimatorBase> sweep = BeginLinkedSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].Play(stateName, transitionDuration);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }

            return reached;
        }

        /// <summary>
        /// Plays a state by Honami name hash. Only controller-backed animators can resolve a hash.
        /// </summary>
        public int PlayState(int stateHash, float transitionDuration = 0.25f, in HonamiLinkedTarget target = default)
        {
            int reached = 0;
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].PlayState(stateHash, transitionDuration);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }

            return reached;
        }

        public void TrySkipState(int stateHashToSkip, int targetStateHash, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].TrySkipState(stateHashToSkip, targetStateHash, transitionDuration, layer, forceRestart, curve, destinationStartTime);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void TrySkipState(string stateToSkip, string targetState, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
            => TrySkipState(HonamiAnimatorBase.StringToHash(stateToSkip), HonamiAnimatorBase.StringToHash(targetState), transitionDuration, layer, forceRestart, curve, destinationStartTime);

        public void TrySkipStateByGuid(string guidToSkip, string targetGuid, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].TrySkipStateByGuid(guidToSkip, targetGuid, transitionDuration, layer, forceRestart, curve, destinationStartTime);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void TryAutoSkipState(int stateHashToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].TryAutoSkipState(stateHashToSkip, layer, ignoreExitTime, cancelEvents);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void TryAutoSkipState(string stateToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
            => TryAutoSkipState(HonamiAnimatorBase.StringToHash(stateToSkip), layer, ignoreExitTime, cancelEvents);

        public void TryAutoSkipStateByGuid(string guidToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].TryAutoSkipStateByGuid(guidToSkip, layer, ignoreExitTime, cancelEvents);
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void SetFloat(string name, float value, in HonamiLinkedTarget target = default)
            => SetFloat(HonamiAnimatorBase.StringToHash(name), value, target);

        public void SetFloat(int id, float value, in HonamiLinkedTarget target = default)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();
            int reached = 0;

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].SetFloat(id, value);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void SetInteger(string name, int value, in HonamiLinkedTarget target = default)
            => SetInteger(HonamiAnimatorBase.StringToHash(name), value, target);

        public void SetInteger(int id, int value, in HonamiLinkedTarget target = default)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();
            int reached = 0;

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].SetInteger(id, value);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void SetBool(string name, bool value, in HonamiLinkedTarget target = default)
            => SetBool(HonamiAnimatorBase.StringToHash(name), value, target);

        public void SetBool(int id, bool value, in HonamiLinkedTarget target = default)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();
            int reached = 0;

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].SetBool(id, value);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void SetTrigger(string name, in HonamiLinkedTarget target = default)
            => SetTrigger(HonamiAnimatorBase.StringToHash(name), target);

        public void SetTrigger(int id, in HonamiLinkedTarget target = default)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();
            int reached = 0;

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].SetTrigger(id);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void ResetTrigger(string name, in HonamiLinkedTarget target = default)
            => ResetTrigger(HonamiAnimatorBase.StringToHash(name), target);

        public void ResetTrigger(int id, in HonamiLinkedTarget target = default)
        {
            List<HonamiAnimator> sweep = BeginFullSweep();
            int reached = 0;

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (target.IsSatisfied(reached)) break;
                    if (!target.Accepts(sweep[i], transform, reached)) continue;

                    sweep[i].ResetTrigger(id);
                    reached++;
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void SetLayerWeight(int layer, float weight)
        {
            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (_fullAnimators[i] != null) _fullAnimators[i].SetLayerWeight(layer, weight);
            }
        }

        public void PauseAll()
        {
            foreach (var animator in _linkedAnimators)
            {
                if (animator != null) animator.Pause();
            }
        }

        public void ResumeAll()
        {
            foreach (var animator in _linkedAnimators)
            {
                if (animator != null) animator.Resume();
            }
        }

        public void StopAll()
        {
            List<HonamiAnimatorBase> sweep = BeginLinkedSweep();

            try
            {
                for (int i = 0; i < sweep.Count; i++)
                {
                    if (sweep[i] != null) sweep[i].StopAll();
                }
            }
            finally
            {
                EndSweep(sweep);
            }
        }

        public void TriggerEvent(string eventName)
        {
            if (graph == null) return;
            _executor.TriggerEvent(graph, eventName);
        }

        public void TriggerEvent(HonamiLinkedAnimatorEvent brainEvent) => _executor.TriggerEvent(brainEvent);

        public void StopBrainEvents() => _executor.StopAll();

        /// <summary>
        /// Copies every linked animator the target accepts into <paramref name="result"/> and returns how many were written.
        /// </summary>
        public int GetAllLinkedNonAlloc(System.Span<HonamiAnimatorBase> result, in HonamiLinkedTarget target = default)
        {
            int count = 0;
            foreach (var animator in _linkedAnimators)
            {
                if (count >= result.Length || target.IsSatisfied(count)) break;
                if (!target.Accepts(animator, transform, count)) continue;

                result[count++] = animator;
            }

            return count;
        }

        /// <summary>
        /// Same as GetAllLinkedNonAlloc but restricted to controller-backed animators, so Clip Players are skipped.
        /// </summary>
        public int GetLinkedAnimatorsNonAlloc(System.Span<HonamiAnimator> result, in HonamiLinkedTarget target = default)
        {
            int count = 0;
            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (count >= result.Length || target.IsSatisfied(count)) break;
                if (!target.Accepts(_fullAnimators[i], transform, count)) continue;

                result[count++] = _fullAnimators[i];
            }

            return count;
        }

        public bool TryGetAnimatorByTag(HonamiTagID tag, out HonamiAnimator animator)
        {
            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (_fullAnimators[i] != null && _fullAnimators[i].linkingTag == tag)
                {
                    animator = _fullAnimators[i];
                    return true;
                }
            }

            animator = null;
            return false;
        }

        /// <summary>
        /// True when at least one controller-backed linked animator currently has the named state active on the layer.
        /// </summary>
        public bool AnyPlayingState(string stateName, int layer = 0)
            => AnyPlayingState(HonamiAnimatorBase.StringToHash(stateName), layer);

        public bool AnyPlayingState(int stateHash, int layer = 0)
        {
            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (_fullAnimators[i] != null && _fullAnimators[i].IsStateActive(stateHash, layer)) return true;
            }

            return false;
        }

        /// <summary>
        /// How many controller-backed linked animators currently have the named state active on the layer.
        /// </summary>
        public int CountPlayingState(string stateName, int layer = 0)
        {
            int stateHash = HonamiAnimatorBase.StringToHash(stateName);
            int count = 0;

            for (int i = 0; i < _fullAnimators.Count; i++)
            {
                if (_fullAnimators[i] != null && _fullAnimators[i].IsStateActive(stateHash, layer)) count++;
            }

            return count;
        }
    }
}
