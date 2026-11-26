using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [BurstCompile]
    public struct HonamiPointConstraintJob : IAnimationJob
    {
        public TransformStreamHandle boneHandle;

        [ReadOnly] public NativeArray<float3> targetLocalPos;
        [ReadOnly] public NativeArray<quaternion> targetLocalRot;
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamWeight = 0;
        private const int ParamLockPos = 1;
        private const int ParamLockRot = 2;
        private const int ParamPosSpeed = 3;
        private const int ParamRotSpeed = 4;
        private const int ParamDeltaTime = 5;

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = parameters[ParamWeight];
            if (w <= 0.001f || !boneHandle.IsValid(stream)) return;

            if (parameters[ParamLockPos] > 0.5f)
            {
                float3 current = (float3)boneHandle.GetLocalPosition(stream);
                float3 target = targetLocalPos[0];
                float posSpeed = parameters[ParamPosSpeed];

                float3 result = posSpeed > 0f
                    ? math.lerp(current, target, parameters[ParamDeltaTime] * posSpeed * w)
                    : math.lerp(current, target, w);

                boneHandle.SetLocalPosition(stream, (Vector3)result);
            }

            if (parameters[ParamLockRot] > 0.5f)
            {
                quaternion current = (quaternion)boneHandle.GetLocalRotation(stream);
                quaternion target = targetLocalRot[0];
                float rotSpeed = parameters[ParamRotSpeed];

                quaternion result = rotSpeed > 0f
                    ? math.slerp(current, target, parameters[ParamDeltaTime] * rotSpeed * w)
                    : math.slerp(current, target, w);

                boneHandle.SetLocalRotation(stream, (Quaternion)result);
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Point Constraint")]
    [ExecuteAlways]
    public sealed class HonamiPointConstraint : HonamiRig
    {
        [Header("Constraint Settings")]
        public Transform point;
        public Transform target;

        [Header("Lock Settings")]
        public bool lockPosition = true;
        public bool lockRotation = true;

        [Header("Offsets")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        [Header("Interpolation (0 = Instant)")]
        public float positionLerpSpeed = 0f;
        public float rotationLerpSpeed = 0f;

        private AnimationScriptPlayable _playable;
        private NativeArray<float3> _nativeTargetPos;
        private NativeArray<quaternion> _nativeTargetRot;
        private NativeArray<float> _nativeParams;

        public override void ResetRig() { }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (point == null) return Playable.Null;

            _nativeTargetPos = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeTargetRot = new NativeArray<quaternion>(1, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(6, Allocator.Persistent);

            var job = new HonamiPointConstraintJob
            {
                boneHandle = animator.BindStreamTransform(point),
                targetLocalPos = _nativeTargetPos,
                targetLocalRot = _nativeTargetRot,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || point == null) return;

            float effectiveWeight = (enabled && gameObject.activeInHierarchy && target != null)
                ? EffectiveWeight
                : 0f;

            if (target != null)
            {
                Vector3 worldTargetPos = target.TransformPoint(positionOffset);
                Quaternion worldTargetRot = target.rotation * Quaternion.Euler(rotationOffset);

                Transform parent = point.parent;
                if (parent != null)
                {
                    _nativeTargetPos[0] = parent.InverseTransformPoint(worldTargetPos);
                    _nativeTargetRot[0] = Quaternion.Inverse(parent.rotation) * worldTargetRot;
                }
                else
                {
                    _nativeTargetPos[0] = worldTargetPos;
                    _nativeTargetRot[0] = worldTargetRot;
                }
            }

            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = lockPosition ? 1f : 0f;
            _nativeParams[2] = lockRotation ? 1f : 0f;
            _nativeParams[3] = positionLerpSpeed;
            _nativeParams[4] = rotationLerpSpeed;
            _nativeParams[5] = deltaTime;
        }

        public override void DisposeJobData()
        {
            if (_nativeTargetPos.IsCreated) _nativeTargetPos.Dispose();
            if (_nativeTargetRot.IsCreated) _nativeTargetRot.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (point == null || target == null || EffectiveWeight <= 0.001f) return;

            Vector3 targetPos = target.TransformPoint(positionOffset);
            Quaternion targetRot = target.rotation * Quaternion.Euler(rotationOffset);

            float currentWeight = EffectiveWeight;

            if (lockPosition)
            {
                if (positionLerpSpeed > 0f && Application.isPlaying)
                    point.position = Vector3.Lerp(point.position, targetPos, deltaTime * positionLerpSpeed * currentWeight);
                else
                    point.position = Vector3.Lerp(point.position, targetPos, currentWeight);
            }

            if (lockRotation)
            {
                if (rotationLerpSpeed > 0f && Application.isPlaying)
                    point.rotation = Quaternion.Slerp(point.rotation, targetRot, deltaTime * rotationLerpSpeed * currentWeight);
                else
                    point.rotation = Quaternion.Slerp(point.rotation, targetRot, currentWeight);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (point == null || target == null) return;

            Gizmos.color = Color.cyan * new Color(1, 1, 1, weight);
            Gizmos.DrawLine(point.position, target.TransformPoint(positionOffset));
            Gizmos.DrawWireSphere(target.TransformPoint(positionOffset), 0.05f);
        }
#endif
    }
}
