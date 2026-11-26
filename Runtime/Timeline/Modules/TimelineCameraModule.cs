using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class HandheldCameraSettings
    {
        [Tooltip("Enable handheld/operator camera sway for this shot.")]
        public bool enabled;

        [Header("Position Sway")]
        [Tooltip("Max position offset in world units applied by the sway noise.")]
        public Vector3 positionAmplitude = new(0.03f, 0.02f, 0.01f);

        [Header("Rotation Sway")]
        [Tooltip("Max rotation offset in degrees applied by the sway noise.")]
        public Vector3 rotationAmplitude = new(0.5f, 0.3f, 0.15f);

        [Header("Noise")]
        [Tooltip("Base scroll speed of the Perlin noise (slow drift).")]
        [Min(0.01f)] public float driftFrequency = 0.4f;

        [Tooltip("Secondary high-frequency jitter layered on top of the drift.")]
        [Min(0f)] public float jitterFrequency = 3f;

        [Tooltip("How much of the jitter layer is mixed in (0 = drift only, 1 = equal mix).")]
        [Range(0f, 1f)] public float jitterAmount = 0.25f;

        [Tooltip("Deterministic noise offset. 0 picks a seed from the shot start time.")]
        public int seed;
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineCameraShotItem : HonamiTimelineItem, IHonamiTimelineBlendCurveItem
    {
        [Tooltip("Scene object the camera is placed on for this shot. A Camera component on it (can be disabled) supplies the field of view.")]
        [HonamiSceneReferenceType(typeof(Transform))]
        public HonamiSceneReference anchor;
        [Min(0.001f)] public float length = 3f;
        [Tooltip("Seconds to move in from the gameplay camera. Overlapping shots crossfade automatically.")]
        [Min(0f)] public float blendIn;
        [Tooltip("Shape of the move in: a preset ease or a custom curve over the blend.")]
        public HonamiBlendCurve blendInCurve = HonamiBlendCurve.SmoothStep;
        [Tooltip("Seconds to move back to the gameplay camera")]
        [Min(0f)] public float blendOut;
        [Tooltip("Shape of the move back: a preset ease or a custom curve over the blend.")]
        public HonamiBlendCurve blendOutCurve = HonamiBlendCurve.SmoothStep;
        public bool overrideFieldOfView;
        [Range(1f, 179f)] public float fieldOfView = 50f;

        [Header("Handheld Effect")]
        public HandheldCameraSettings handheld = new();

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
    public struct HonamiCameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;

        public static HonamiCameraPose Of(Camera camera)
        {
            var transform = camera.transform;
            return new HonamiCameraPose { Position = transform.position, Rotation = transform.rotation, FieldOfView = camera.fieldOfView };
        }

        public void ApplyTo(Camera camera)
        {
            camera.transform.SetPositionAndRotation(Position, Rotation);
            camera.fieldOfView = FieldOfView;
        }

        public static HonamiCameraPose Lerp(in HonamiCameraPose from, in HonamiCameraPose to, float t) => new()
        {
            Position = Vector3.LerpUnclamped(from.Position, to.Position, t),
            Rotation = Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, t),
            FieldOfView = Mathf.LerpUnclamped(from.FieldOfView, to.FieldOfView, t)
        };
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineCameraModule : HonamiTimelineModule<TimelineCameraShotItem>
    {
        public override Type BindingType => typeof(Camera);
        public override HonamiTimelineModuleHosts SupportedHosts => HonamiTimelineModuleHosts.Timeline;

        public override HonamiTimelineItem CreateItem(float time) => new TimelineCameraShotItem { time = time };

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            var camera = context.Binding as Camera;
            if (camera == null) camera = Camera.main;
            return camera != null ? new Evaluator(this, camera, context.Director) : null;
        }

        public HonamiCameraPose GetShotPose(TimelineCameraShotItem shot, Transform anchor, in HonamiCameraPose fallback, float time)
        {
            if (anchor == null) return fallback;

            float fieldOfView = fallback.FieldOfView;
            if (shot.overrideFieldOfView) fieldOfView = shot.fieldOfView;
            else if (anchor.TryGetComponent(out Camera shotCamera)) fieldOfView = shotCamera.fieldOfView;

            var pose = new HonamiCameraPose { Position = anchor.position, Rotation = anchor.rotation, FieldOfView = fieldOfView };

            var h = shot.handheld;
            if (!h.enabled) return pose;

            // Compute a deterministic per-shot seed offset so each shot has unique noise.
            float seedOffset = h.seed != 0 ? h.seed : shot.time * 10000f;

            // Sample two octaves: slow drift + fast jitter.
            float drift = h.driftFrequency * time + seedOffset;
            float jitter = h.jitterFrequency * time + seedOffset;

            // Position noise - each axis gets independent Perlin samples.
            var driftPos = new Vector3(
                (Mathf.PerlinNoise(drift + 0f,   0.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(drift + 31.7f, 0.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(drift + 63.4f, 0.3f) - 0.5f) * 2f
            );
            var jitterPos = new Vector3(
                (Mathf.PerlinNoise(jitter + 10f,  0.7f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(jitter + 41.7f, 0.7f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(jitter + 83.4f, 0.7f) - 0.5f) * 2f
            );

            Vector3 posNoise = driftPos + jitterPos * h.jitterAmount;
            Vector3 posOffset = new Vector3(
                posNoise.x * h.positionAmplitude.x,
                posNoise.y * h.positionAmplitude.y,
                posNoise.z * h.positionAmplitude.z
            );

            // Rotation noise - applied in anchor-local space so the sway is relative to shot orientation.
            var driftRot = new Vector3(
                (Mathf.PerlinNoise(drift + 100f,  0.5f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(drift + 131.7f, 0.5f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(drift + 163.4f, 0.5f) - 0.5f) * 2f
            );
            var jitterRot = new Vector3(
                (Mathf.PerlinNoise(jitter + 110f,  0.9f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(jitter + 141.7f, 0.9f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(jitter + 173.4f, 0.9f) - 0.5f) * 2f
            );

            Vector3 rotNoise = driftRot + jitterRot * h.jitterAmount;
            Vector3 rotEuler = new Vector3(
                rotNoise.x * h.rotationAmplitude.x,
                rotNoise.y * h.rotationAmplitude.y,
                rotNoise.z * h.rotationAmplitude.z
            );

            // Add world-space position offset and local-rotation sway.
            pose.Position += anchor.TransformDirection(posOffset);
            pose.Rotation = pose.Rotation * Quaternion.Euler(rotEuler);

            return pose;
        }

        public bool Evaluate(float time, in HonamiCameraPose gameplayPose, Func<HonamiSceneReference, Transform> resolve, ref int[] order, out HonamiCameraPose pose, bool hold = false)
        {
            pose = gameplayPose;
            bool any = false;
            int count = HonamiTimelineBlend.SortByTime(this, ref order);

            if (hold && HonamiTimelineBlend.FindHeldItem(this, time) is TimelineCameraShotItem held)
            {
                var anchor = resolve(held.anchor);
                if (anchor != null)
                {
                    pose = GetShotPose(held, anchor, gameplayPose, time);
                    any = true;
                }
            }

            for (int i = 0; i < count; i++)
            {
                var shot = items[order[i]];
                if (shot == null || !HonamiTimelineBlend.IsActive(shot, time)) continue;

                var anchor = resolve(shot.anchor);
                if (anchor == null) continue;

                float weight = HonamiTimelineBlend.Weight(this, shot, time, false, hold);
                if (weight <= 0f) continue;

                pose = HonamiCameraPose.Lerp(pose, GetShotPose(shot, anchor, gameplayPose, time), weight);
                any = true;
            }

            return any;
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineCameraModule _module;
            private readonly Camera _camera;
            private readonly HonamiDirector _director;
            private readonly Func<HonamiSceneReference, Transform> _resolve;
            private HonamiCameraPose _gameplayPose;
            private bool _driving;
            private bool _applied;
            private int[] _order;

            public Evaluator(TimelineCameraModule module, Camera camera, HonamiDirector director)
            {
                _module = module;
                _camera = camera;
                _director = director;
                _resolve = Resolve;
            }

            private Transform Resolve(HonamiSceneReference reference) =>
                _director != null ? _director.Resolve(reference, typeof(Transform)) as Transform : null;

            public override void OnPlay()
            {
                if (_camera == null) return;
                _gameplayPose = HonamiCameraPose.Of(_camera);
                _driving = true;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_camera == null || !_driving) return;

                if (_module.Evaluate(frame.Time, _gameplayPose, _resolve, ref _order, out var pose, frame.Hold))
                {
                    pose.ApplyTo(_camera);
                    _applied = true;
                }
                else if (_applied)
                {
                    _gameplayPose.ApplyTo(_camera);
                    _applied = false;
                }
            }

            public override void OnStop()
            {
                if (_camera == null || !_driving) return;
                _driving = false;
                if (!_applied) return;
                _applied = false;
                _gameplayPose.ApplyTo(_camera);
            }
        }
    }
}
