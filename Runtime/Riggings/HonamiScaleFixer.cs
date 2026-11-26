using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiScaleFixMode
    {
        SnapToTarget,
        Smooth
    }

    [BurstCompile]
    public struct HonamiScaleFixerJob : IAnimationJob
    {
        public TransformStreamHandle boneHandle;

        [ReadOnly] public NativeArray<float3> targetScale;
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamWeight = 0;
        private const int ParamFixX   = 1;
        private const int ParamFixY   = 2;
        private const int ParamFixZ   = 3;
        private const int ParamMode   = 4;
        private const int ParamSpeed  = 5;
        private const int ParamDt     = 6;

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = parameters[ParamWeight];
            if (w <= 0.001f) return;

            float3 current = boneHandle.GetLocalScale(stream);
            float3 desired  = targetScale[0];

            bool fixX = parameters[ParamFixX] > 0.5f;
            bool fixY = parameters[ParamFixY] > 0.5f;
            bool fixZ = parameters[ParamFixZ] > 0.5f;

            float3 locked = new float3(
                fixX ? desired.x : current.x,
                fixY ? desired.y : current.y,
                fixZ ? desired.z : current.z
            );

            float3 result;
            if (parameters[ParamMode] > 0.5f)
            {
                float speed = parameters[ParamSpeed];
                float dt    = parameters[ParamDt];
                float t     = math.saturate(dt * speed * w);
                result      = math.lerp(current, locked, t);
            }
            else
            {
                result = math.lerp(current, locked, w);
            }

            if (!IsValidScale(result)) return;
            boneHandle.SetLocalScale(stream, result);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private static bool IsValidScale(float3 v) =>
            !math.any(math.isnan(v)) && !math.any(math.isinf(v)) &&
            v.x > 0.0001f && v.y > 0.0001f && v.z > 0.0001f;
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Scale Fixer")]
    [ExecuteAlways]
    public sealed class HonamiScaleFixer : HonamiRig
    {
        [Header("Target Bone")]
        public Transform bone;

        [Header("Target Scale")]
        public bool useCustomScale = false;
        public Vector3 customTargetScale = Vector3.one;

        [Header("Axes Mask")]
        public bool fixX = true;
        public bool fixY = true;
        public bool fixZ = true;

        [Header("Fix Mode")]
        public HonamiScaleFixMode fixMode = HonamiScaleFixMode.SnapToTarget;

        [Header("Smooth Mode Settings")]
        [Min(0f)]
        public float smoothSpeed = 10f;

        private AnimationScriptPlayable _playable;
        private NativeArray<float3> _nativeTargetScale;
        private NativeArray<float>  _nativeParams;

        private Vector3 _restScale;
        private bool    _restCaptured;

        private void Awake() => CaptureRestScale();

        protected override void OnEnable()
        {
            base.OnEnable();
            CaptureRestScale();
        }

        public void CaptureRestScale()
        {
            if (bone == null) return;
            _restScale    = bone.localScale;
            _restCaptured = true;
        }

        public override void ResetRig()
        {
            if (bone == null || !_restCaptured) return;
            if (_nativeTargetScale.IsCreated)
                _nativeTargetScale[0] = GetDesiredScale();
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (bone == null) return Playable.Null;

            _nativeTargetScale = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeParams      = new NativeArray<float>(7, Allocator.Persistent);

            if (!_restCaptured) CaptureRestScale();

            var job = new HonamiScaleFixerJob
            {
                boneHandle   = animator.BindStreamTransform(bone),
                targetScale  = _nativeTargetScale,
                parameters   = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || bone == null)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                return;
            }

            float effectiveWeight = (enabled && gameObject.activeInHierarchy) ? EffectiveWeight : 0f;

            _nativeTargetScale[0] = GetDesiredScale();

            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = fixX ? 1f : 0f;
            _nativeParams[2] = fixY ? 1f : 0f;
            _nativeParams[3] = fixZ ? 1f : 0f;
            _nativeParams[4] = fixMode == HonamiScaleFixMode.Smooth ? 1f : 0f;
            _nativeParams[5] = smoothSpeed;
            _nativeParams[6] = deltaTime;
        }

        public override void DisposeJobData()
        {
            if (_nativeTargetScale.IsCreated) _nativeTargetScale.Dispose();
            if (_nativeParams.IsCreated)      _nativeParams.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (bone == null || EffectiveWeight <= 0.001f) return;

            if (!_restCaptured) CaptureRestScale();

            Vector3 desired = GetDesiredScale();
            Vector3 current = bone.localScale;

            float3 desired3 = desired;
            float3 current3 = current;

            bool isValid = !float.IsNaN(desired3.x) && !float.IsNaN(desired3.y) && !float.IsNaN(desired3.z) &&
                           desired3.x > 0.0001f && desired3.y > 0.0001f && desired3.z > 0.0001f;
            if (!isValid) return;

            float3 locked = new float3(
                fixX ? desired3.x : current3.x,
                fixY ? desired3.y : current3.y,
                fixZ ? desired3.z : current3.z
            );

            float3 result;
            if (fixMode == HonamiScaleFixMode.Smooth && Application.isPlaying)
            {
                float t = math.saturate(deltaTime * smoothSpeed * EffectiveWeight);
                result  = math.lerp(current3, locked, t);
            }
            else
            {
                result = math.lerp(current3, locked, EffectiveWeight);
            }

            bone.localScale = result;
        }

        private float3 GetDesiredScale()
        {
            if (useCustomScale) return customTargetScale;
            if (_restCaptured)  return _restScale;
            return Vector3.one;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (bone == null) return;
            Vector3 p = bone.position;
            float   s = bone.localScale.magnitude * 0.25f;
            Gizmos.color = new Color(0.2f, 1f, 0.6f, weight);
            Gizmos.DrawWireCube(p, Vector3.one * s);
        }
#endif
    }
}
