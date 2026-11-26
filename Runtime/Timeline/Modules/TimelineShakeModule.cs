using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineShakeItem : HonamiTimelineItem, IHonamiTimelineBlendCurveItem
    {
        [Min(0.001f)] public float length = 1f;
        [Min(0f)] public float blendIn = 0.1f;
        [Tooltip("Shape of the shake fade in: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendInCurve = HonamiBlendCurve.SmoothStep;
        [Min(0f)] public float blendOut = 0.5f;
        [Tooltip("Shape of the shake fade out: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendOutCurve = HonamiBlendCurve.SmoothStep;

        [Header("Shake Settings")]
        public Vector3 positionAmplitude = new(0.1f, 0.1f, 0.1f);
        public Vector3 rotationAmplitude = new(2f, 2f, 2f);
        [Min(0.1f)] public float frequency = 15f;

        [Tooltip("Seed for the noise. 0 derives one from the item's start time.")]
        public int seed;

        public override float Duration => length;
        public override bool CanResize => true;
        public override void SetDuration(float duration) => length = Mathf.Max(MinDuration, duration);

        public float BlendIn
        {
            get => blendIn;
            set => blendIn = Mathf.Max(0f, value);
        }

        public float BlendOut
        {
            get => blendOut;
            set => blendOut = Mathf.Max(0f, value);
        }

        public HonamiBlendCurve BlendInCurve => blendInCurve;

        public HonamiBlendCurve BlendOutCurve => blendOutCurve;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineShakeModule : HonamiTimelineModule<TimelineShakeItem>
    {
        public override Type BindingType => typeof(Transform);
        public override HonamiTimelineModuleHosts SupportedHosts => HonamiTimelineModuleHosts.Timeline | HonamiTimelineModuleHosts.State;

        public override HonamiTimelineItem CreateItem(float time) => new TimelineShakeItem { time = time };

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            var transform = HonamiTimelineModuleBinding.Convert(context.Binding, typeof(Transform)) as Transform;
            if (transform == null && context.Owner != null)
                transform = context.Owner.transform;
            return transform != null ? new Evaluator(this, transform) : null;
        }

        public bool EvaluateShake(float time, out Vector3 posOffset, out Quaternion rotOffset)
        {
            posOffset = Vector3.zero;
            rotOffset = Quaternion.identity;
            bool any = false;

            for (int i = 0; i < items.Count; i++)
            {
                var shot = items[i];
                if (shot == null || !HonamiTimelineBlend.IsActive(shot, time)) continue;

                float weight = HonamiTimelineBlend.Weight(this, shot, time, false);
                if (weight <= 0f) continue;

                int s = shot.seed != 0 ? shot.seed : (int)(shot.time * 10000f) + i * 1337;
                float t = time * shot.frequency + s;

                var position = new Vector3(Noise(t), Noise(t + 100f), Noise(t + 200f));
                var euler = new Vector3(Noise(t + 300f), Noise(t + 400f), Noise(t + 500f));

                posOffset += Vector3.Scale(position, shot.positionAmplitude) * weight;
                rotOffset *= Quaternion.Euler(Vector3.Scale(euler, shot.rotationAmplitude) * weight);
                any = true;
            }

            return any;
        }

        private static float Noise(float x) => (Mathf.PerlinNoise(x, 0f) - 0.5f) * 2f;

        // The offset is layered on whatever pose the transform holds this frame. If nothing rewrote it since the
        // last shake (a static prop), the stored base is reused, so the shake never accumulates into drift.
        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineShakeModule _module;
            private readonly Transform _transform;

            private bool _applied;
            private Vector3 _basePosition;
            private Quaternion _baseRotation = Quaternion.identity;
            private Vector3 _writtenPosition;
            private Quaternion _writtenRotation = Quaternion.identity;

            public Evaluator(TimelineShakeModule module, Transform transform)
            {
                _module = module;
                _transform = transform;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_transform == null) return;

                bool shaking = _module.EvaluateShake(frame.Time, out var posOffset, out var rotOffset);
                if (!_applied || IsOverwritten())
                {
                    if (!shaking)
                    {
                        _applied = false;
                        return;
                    }
                    _transform.GetLocalPositionAndRotation(out _basePosition, out _baseRotation);
                }

                if (!shaking)
                {
                    Restore();
                    return;
                }

                _writtenPosition = _basePosition + posOffset;
                _writtenRotation = _baseRotation * rotOffset;
                _transform.SetLocalPositionAndRotation(_writtenPosition, _writtenRotation);
                _applied = true;
            }

            public override void OnStop()
            {
                if (_transform != null && _applied && !IsOverwritten())
                    Restore();
                _applied = false;
            }

            private bool IsOverwritten()
            {
                _transform.GetLocalPositionAndRotation(out var position, out var rotation);
                return position != _writtenPosition || rotation != _writtenRotation;
            }

            private void Restore()
            {
                _transform.SetLocalPositionAndRotation(_basePosition, _baseRotation);
                _applied = false;
            }
        }
    }
}
