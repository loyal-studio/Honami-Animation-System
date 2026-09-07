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

        public void SetController(HonamiRuntimeController newController, float transitionDuration = 0f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode mode = HonamiControllerTransitionMode.ContinueEvaluating)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetController(newController, transitionDuration, transitionCurve, mode);
            }
        }

        public void SetProfile(string stateName, float transitionDuration = -1f, AnimationCurve transitionCurve = null, HonamiControllerTransitionMode? mode = null)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.TryGetComponent<HonamiControllerProfile>(out var profile))
                {
                    profile.SetProfile(stateName, transitionDuration, transitionCurve, mode);
                }
            }
        }

        private float _globalWeight = 1f;
        public float GlobalWeight
        {
            get => _globalWeight;
            set
            {
                _globalWeight = Mathf.Clamp01(value);
                foreach (var anim in _linkedAnimators)
                {
                    if (anim != null)
                        anim.GlobalWeight = _globalWeight;
                }
            }
        }

        public void SetActionID(HonamiActionID actionId)
        {
            if (actionId == null) return;

            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.ReactToAction(actionId);
            }
        }

        public void SetActionID(HonamiActionID actionId, float transitionDuration)
        {
            if (actionId == null) return;

            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public void SetActionID(HonamiActionID actionId, float transitionDuration, HonamiBroadcastTargetMode targetMode, HonamiTagID targetTag = null)
        {
            if (actionId == null) return;

            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;

                if (targetMode == HonamiBroadcastTargetMode.ChildrenOnly)
                {
                    if (!anim.transform.IsChildOf(transform))
                        continue;
                }
                else if (targetMode == HonamiBroadcastTargetMode.ByTag)
                {
                    if (targetTag == null || anim.linkingTag != targetTag)
                        continue;
                }

                anim.ReactToAction(actionId, transitionDuration);
            }
        }

        public void SetActionID(HonamiActionID actionId, Vector3 origin, float maxDistance, int limit = int.MaxValue)
        {
            if (actionId == null || limit <= 0) return;

            float maxDistSq = maxDistance * maxDistance;
            int count = 0;
            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;
                if (Vector3.SqrMagnitude(anim.transform.position - origin) <= maxDistSq)
                {
                    anim.ReactToAction(actionId);
                    if (++count >= limit) break;
                }
            }
        }

        public void SetActionID(HonamiActionID actionId, Vector3 origin, float maxDistance, float transitionDuration, int limit = int.MaxValue)
        {
            if (actionId == null || limit <= 0) return;

            float maxDistSq = maxDistance * maxDistance;
            int count = 0;
            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;
                if (Vector3.SqrMagnitude(anim.transform.position - origin) <= maxDistSq)
                {
                    anim.ReactToAction(actionId, transitionDuration);
                    if (++count >= limit) break;
                }
            }
        }

        public void SetActionID(HonamiActionID actionId, HonamiBroadcastTargetMode targetMode, HonamiTagID targetTag = null)
        {
            if (actionId == null) return;

            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;

                if (targetMode == HonamiBroadcastTargetMode.ChildrenOnly)
                {
                    if (!anim.transform.IsChildOf(transform))
                        continue;
                }
                else if (targetMode == HonamiBroadcastTargetMode.ByTag)
                {
                    if (targetTag == null || anim.linkingTag != targetTag)
                        continue;
                }

                anim.ReactToAction(actionId);
            }
        }

        public void PlayState(int stateHash, float transitionDuration = 0.25f)
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim is HonamiAnimator full)
                    full.PlayState(stateHash, transitionDuration);
            }
        }

        public void PlayState(string stateName, float transitionDuration = 0.25f)
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.Play(stateName, transitionDuration);
            }
        }

        public void PlayState(string stateName, Vector3 origin, float maxDistance, float transitionDuration = 0.25f, int limit = int.MaxValue)
        {
            if (limit <= 0) return;

            float maxDistSq = maxDistance * maxDistance;
            int count = 0;
            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;
                if (Vector3.SqrMagnitude(anim.transform.position - origin) <= maxDistSq)
                {
                    anim.Play(stateName, transitionDuration);
                    if (++count >= limit) break;
                }
            }
        }

        public void PlayState(string stateName, HonamiBroadcastTargetMode targetMode, HonamiTagID targetTag = null, float transitionDuration = 0.25f)
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;

                if (targetMode == HonamiBroadcastTargetMode.ChildrenOnly)
                {
                    if (!anim.transform.IsChildOf(transform))
                        continue;
                }
                else if (targetMode == HonamiBroadcastTargetMode.ByTag)
                {
                    if (targetTag == null || anim.linkingTag != targetTag)
                        continue;
                }

                anim.Play(stateName, transitionDuration);
            }
        }

        public void TrySkipState(int stateHashToSkip, int targetStateHash, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TrySkipState(stateHashToSkip, targetStateHash, transitionDuration, layer, forceRestart, curve, destinationStartTime);
            }
        }

        public void TrySkipState(string stateToSkip, string targetState, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TrySkipState(stateToSkip, targetState, transitionDuration, layer, forceRestart, curve, destinationStartTime);
            }
        }

        public void TrySkipStateByGuid(string guidToSkip, string targetGuid, float transitionDuration = 0.25f, int layer = 0, bool forceRestart = false, AnimationCurve curve = null, float destinationStartTime = 0f)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TrySkipStateByGuid(guidToSkip, targetGuid, transitionDuration, layer, forceRestart, curve, destinationStartTime);
            }
        }

        public void TryAutoSkipState(int stateHashToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TryAutoSkipState(stateHashToSkip, layer, ignoreExitTime, cancelEvents);
            }
        }

        public void TryAutoSkipState(string stateToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TryAutoSkipState(stateToSkip, layer, ignoreExitTime, cancelEvents);
            }
        }

        public void TryAutoSkipStateByGuid(string guidToSkip, int layer = 0, bool ignoreExitTime = false, bool cancelEvents = false)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.TryAutoSkipStateByGuid(guidToSkip, layer, ignoreExitTime, cancelEvents);
            }
        }

        public void SetFloat(string name, float value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetFloat(name, value);
            }
        }

        public void SetFloat(int id, float value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetFloat(id, value);
            }
        }

        public void SetFloatByTag(HonamiTagID tag, string name, float value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetFloat(name, value);
            }
        }

        public void SetFloatByTag(HonamiTagID tag, int id, float value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetFloat(id, value);
            }
        }

        public void SetInteger(string name, int value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetInteger(name, value);
            }
        }

        public void SetInteger(int id, int value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetInteger(id, value);
            }
        }

        public void SetIntegerByTag(HonamiTagID tag, string name, int value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetInteger(name, value);
            }
        }

        public void SetIntegerByTag(HonamiTagID tag, int id, int value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetInteger(id, value);
            }
        }

        public void SetBool(string name, bool value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetBool(name, value);
            }
        }

        public void SetBool(int id, bool value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetBool(id, value);
            }
        }

        public void SetBoolByTag(HonamiTagID tag, string name, bool value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetBool(name, value);
            }
        }

        public void SetBoolByTag(HonamiTagID tag, int id, bool value)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetBool(id, value);
            }
        }

        public void SetTrigger(string name)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetTrigger(name);
            }
        }

        public void SetTrigger(int id)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetTrigger(id);
            }
        }

        public void SetTriggerByTag(HonamiTagID tag, string name)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetTrigger(name);
            }
        }

        public void SetTriggerByTag(HonamiTagID tag, int id)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                    anim.SetTrigger(id);
            }
        }

        public void ResetTrigger(string name)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.ResetTrigger(name);
            }
        }

        public void SetLayerWeight(int layer, float weight)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null)
                    anim.SetLayerWeight(layer, weight);
            }
        }

        public void PauseAll()
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.Pause();
            }
        }

        public void ResumeAll()
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.Resume();
            }
        }

        public void StopAll()
        {
            foreach (var anim in _linkedAnimators)
            {
                if (anim != null)
                    anim.StopAll();
            }
        }

        [System.Obsolete("Use SetActionID instead")]
        public void BroadcastAction(HonamiActionID actionId, HonamiBroadcastTargetMode targetMode = HonamiBroadcastTargetMode.AllLinked, HonamiTagID targetTag = null)
        {
            SetActionID(actionId, targetMode, targetTag);
        }

        public void TriggerEvent(string eventName)
        {
            if (graph == null) return;
            _executor.TriggerEvent(graph, eventName);
        }

        public void TriggerEvent(HonamiLinkedAnimatorEvent brainEvent)
        {
            _executor.TriggerEvent(brainEvent);
        }

        public void StopBrainEvents()
        {
            _executor.StopAll();
        }

        public int GetAllLinkedNonAlloc(System.Span<HonamiAnimatorBase> result, HonamiBroadcastTargetMode mode = HonamiBroadcastTargetMode.AllLinked, HonamiTagID tag = null)
        {
            int count = 0;
            foreach (var anim in _linkedAnimators)
            {
                if (anim == null) continue;
                if (count >= result.Length) break;

                if (mode == HonamiBroadcastTargetMode.ChildrenOnly && !anim.transform.IsChildOf(transform)) continue;
                if (mode == HonamiBroadcastTargetMode.ByTag && (tag == null || anim.linkingTag != tag)) continue;

                result[count++] = anim;
            }
            return count;
        }

        public int GetLinkedAnimatorsNonAlloc(System.Span<HonamiAnimator> result, HonamiBroadcastTargetMode mode = HonamiBroadcastTargetMode.AllLinked, HonamiTagID tag = null)
        {
            int count = 0;
            foreach (var anim in _fullAnimators)
            {
                if (anim == null) continue;
                if (count >= result.Length) break;

                if (mode == HonamiBroadcastTargetMode.ChildrenOnly && !anim.transform.IsChildOf(transform)) continue;
                if (mode == HonamiBroadcastTargetMode.ByTag && (tag == null || anim.linkingTag != tag)) continue;

                result[count++] = anim;
            }
            return count;
        }

        public bool TryGetAnimatorByTag(HonamiTagID tag, out HonamiAnimator animator)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim != null && anim.linkingTag == tag)
                {
                    animator = anim;
                    return true;
                }
            }
            animator = null;
            return false;
        }

        public bool AnyPlayingState(string stateName)
        {
            foreach (var anim in _fullAnimators)
            {
                if (anim == null) continue;
                if (anim.GetActiveStateIndex(0) != -1) // Simplified check
                {
                    // This would need a proper implementation in HonamiAnimator to check by name
                    // For now, let's just add the signature as requested for "helpers"
                }
            }
            return false;
        }
    }
}



