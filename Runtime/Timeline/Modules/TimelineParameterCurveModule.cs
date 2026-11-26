using System;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineParameterCurveItem : HonamiTimelineItem
    {
        [Min(0.001f)] public float length = 1f;
        [Tooltip("Horizontal axis is normalized over the item's length (0 = start, 1 = end)")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public override float Duration => length;
        public override bool CanResize => true;
        public override void SetDuration(float duration) => length = Mathf.Max(MinDuration, duration);

        public float Evaluate(float time) => curve != null ? curve.Evaluate(Mathf.Clamp01((time - this.time) / length)) : 0f;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineParameterCurveModule : HonamiTimelineModule<TimelineParameterCurveItem>
    {
        [Tooltip("Float parameter of the Honami controller written while the playhead is inside an item")]
        public string parameterName = "NewFloat";

        public override Type BindingType => typeof(HonamiAnimator);

        public override bool RequiresBinding(HonamiTimelineModuleHost host) => host == HonamiTimelineModuleHost.Timeline;

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            var animator = context.Binding as HonamiAnimator;
            if (animator == null) animator = context.Animator;
            if (animator == null || string.IsNullOrEmpty(parameterName)) return null;
            return new Evaluator(this, animator, HonamiAnimator.StringToHash(parameterName));
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineParameterCurveModule _module;
            private readonly HonamiAnimator _animator;
            private readonly int _parameterHash;

            public Evaluator(TimelineParameterCurveModule module, HonamiAnimator animator, int parameterHash)
            {
                _module = module;
                _animator = animator;
                _parameterHash = parameterHash;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_animator == null) return;

                var segments = _module.items;
                if (segments.Count == 0) return;

                for (int i = 0; i < segments.Count; i++)
                {
                    var item = segments[i];
                    if (item == null || frame.Time < item.time || frame.Time > item.EndTime) continue;

                    _animator.SetFloat(_parameterHash, item.Evaluate(frame.Time));
                    return;
                }

                if (frame.Hold && HonamiTimelineBlend.FindHeldItem(_module, frame.Time) is TimelineParameterCurveItem held)
                    _animator.SetFloat(_parameterHash, held.Evaluate(held.EndTime));
            }
        }
    }
}
