using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    public enum TimelineActivationPostPlayback
    {
        Revert,
        Active,
        Inactive,
        LeaveAsIs
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineActivationItem : HonamiTimelineItem
    {
        [Min(0.001f)] public float length = 0.5f;

        public override float Duration => length;
        public override bool CanResize => true;
        public override void SetDuration(float duration) => length = Mathf.Max(MinDuration, duration);
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineActivationModule : HonamiTimelineModule<TimelineActivationItem>
    {
        [Tooltip("State of the object once playback leaves this module: the state stops being current, or the timeline stops.")]
        public TimelineActivationPostPlayback postPlayback = TimelineActivationPostPlayback.Revert;

        public override Type BindingType => typeof(GameObject);
        public override bool RequiresBinding(HonamiTimelineModuleHost host) => true;

        // Deactivating the animator's own GameObject would disable the animator that is evaluating this module.
        public override string GetBindingProblem(HonamiTimelineModuleHost host, UnityEngine.Object binding, GameObject owner)
        {
            if (host == HonamiTimelineModuleHost.State && binding != null && binding == owner)
                return "it targets the animator's own GameObject; set the binding path to a child";
            return base.GetBindingProblem(host, binding, owner);
        }

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            if (context.Binding is not GameObject target) return null;
            if (context.Host == HonamiTimelineModuleHost.State && target == context.Owner) return null;
            return new Evaluator(this, target);
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineActivationModule _module;
            private readonly GameObject _target;
            private bool _initialActive;
            private bool _settledAfterEnd;

            public Evaluator(TimelineActivationModule module, GameObject target)
            {
                _module = module;
                _target = target;
            }

            public override void OnPlay()
            {
                if (_target != null)
                    _initialActive = _target.activeSelf;
                _settledAfterEnd = false;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_target == null) return;

                var items = _module.items;
                bool any = false;
                bool inside = false;
                float maxEndTime = float.NegativeInfinity;

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null) continue;

                    any = true;
                    if (item.EndTime > maxEndTime) maxEndTime = item.EndTime;
                    if (frame.Time >= item.time && frame.Time <= item.EndTime) inside = true;
                }

                if (!any) return;

                if (frame.Time <= maxEndTime)
                {
                    _settledAfterEnd = false;
                    SetActive(inside);
                    return;
                }

                // Hold and Leave As Is apply the last item's state once, even when a skip jumped over it, then let go of the object.
                if (frame.Hold || _module.postPlayback == TimelineActivationPostPlayback.LeaveAsIs)
                {
                    if (!_settledAfterEnd)
                    {
                        _settledAfterEnd = true;
                        SetActive(true);
                    }
                    return;
                }

                ApplyPostPlayback();
            }

            public override void OnStop()
            {
                if (_target != null) ApplyPostPlayback();
            }

            private void ApplyPostPlayback()
            {
                switch (_module.postPlayback)
                {
                    case TimelineActivationPostPlayback.Revert:
                        SetActive(_initialActive);
                        break;
                    case TimelineActivationPostPlayback.Active:
                        SetActive(true);
                        break;
                    case TimelineActivationPostPlayback.Inactive:
                        SetActive(false);
                        break;
                }
            }

            private void SetActive(bool active)
            {
                if (_target.activeSelf != active)
                    _target.SetActive(active);
            }
        }
    }
}
