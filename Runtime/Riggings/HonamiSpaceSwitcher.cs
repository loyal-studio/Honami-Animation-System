using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [BurstCompile]
    public struct HonamiSpaceSwitcherJob : IAnimationJob
    {
        public TransformStreamHandle targetHandle;
        public TransformStreamHandle newSpaceHandle;
        
        public NativeArray<float3> capturedOffsetPos;
        public NativeArray<quaternion> capturedOffsetRot;
        public NativeArray<int> stateData; // 0: is captured
        
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamWeight = 0;

        public void ProcessAnimation(AnimationStream stream)
        {
            float weight = parameters[ParamWeight];
            
            if (weight <= 0.001f)
            {
                stateData[0] = 0; // release capture so it can be recaptured next time
                return;
            }

            float3 currentPos = targetHandle.GetPosition(stream);
            quaternion currentRot = targetHandle.GetRotation(stream);

            float3 spacePos = newSpaceHandle.GetPosition(stream);
            quaternion spaceRot = newSpaceHandle.GetRotation(stream);
            quaternion invSpaceRot = math.inverse(spaceRot);

            if (stateData[0] == 0)
            {
                // Capture the exact offset at the exact moment weight becomes > 0
                capturedOffsetPos[0] = math.mul(invSpaceRot, currentPos - spacePos);
                capturedOffsetRot[0] = math.mul(invSpaceRot, currentRot);
                stateData[0] = 1;
            }

            // Calculate the ideal perfectly stable world transform based on the new space
            float3 targetWorldPos = spacePos + math.mul(spaceRot, capturedOffsetPos[0]);
            quaternion targetWorldRot = math.mul(spaceRot, capturedOffsetRot[0]);

            if (weight >= 0.999f)
            {
                targetHandle.SetPosition(stream, targetWorldPos);
                targetHandle.SetRotation(stream, targetWorldRot);
            }
            else
            {
                targetHandle.SetPosition(stream, math.lerp(currentPos, targetWorldPos, weight));
                targetHandle.SetRotation(stream, math.slerp(currentRot, targetWorldRot, weight));
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Space Switcher")]
    [ExecuteAlways]
    public sealed class HonamiSpaceSwitcher : HonamiRig
    {
        [Header("Settings")]
        [Tooltip("The bone that should switch spaces to stop wobbling (e.g. Socet_R)")]
        public Transform target;
        
        [Tooltip("The stable parent space to temporarily attach to (e.g. hand.l or Root)")]
        public Transform newSpace;

        [Header("Offset Mode")]
        [Tooltip("If unchecked, it dynamically captures the offset exactly when Weight starts increasing. If checked, it uses the fixed offset below.")]
        public bool useFixedOffset = false;
        public Vector3 fixedPositionOffset = Vector3.zero;
        public Vector3 fixedRotationOffset = Vector3.zero;

        private AnimationScriptPlayable _playable;
        private NativeArray<float3> _offsetPos;
        private NativeArray<quaternion> _offsetRot;
        private NativeArray<int> _stateData;
        private NativeArray<float> _parameters;

        public override void ResetRig()
        {
            if (_stateData.IsCreated && !useFixedOffset)
            {
                _stateData[0] = 0;
            }
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (target == null || newSpace == null) return Playable.Null;

            _offsetPos = new NativeArray<float3>(1, Allocator.Persistent);
            _offsetRot = new NativeArray<quaternion>(1, Allocator.Persistent);
            _stateData = new NativeArray<int>(1, Allocator.Persistent);
            _parameters = new NativeArray<float>(1, Allocator.Persistent);

            _stateData[0] = 0;

            if (useFixedOffset)
            {
                _offsetPos[0] = fixedPositionOffset;
                _offsetRot[0] = Quaternion.Euler(fixedRotationOffset);
                _stateData[0] = 1;
            }

            var job = new HonamiSpaceSwitcherJob
            {
                targetHandle = animator.BindStreamTransform(target),
                newSpaceHandle = animator.BindStreamTransform(newSpace),
                capturedOffsetPos = _offsetPos,
                capturedOffsetRot = _offsetRot,
                stateData = _stateData,
                parameters = _parameters
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid()) return;
            
            float effectiveWeight = (enabled && gameObject.activeInHierarchy) ? EffectiveWeight : 0f;
            _parameters[0] = effectiveWeight;

            if (useFixedOffset)
            {
                _offsetPos[0] = fixedPositionOffset;
                _offsetRot[0] = Quaternion.Euler(fixedRotationOffset);
                _stateData[0] = 1;
            }
            else if (effectiveWeight <= 0.001f && _stateData.IsCreated)
            {
                _stateData[0] = 0;
            }
        }

        public override void DisposeJobData()
        {
            if (_offsetPos.IsCreated) _offsetPos.Dispose();
            if (_offsetRot.IsCreated) _offsetRot.Dispose();
            if (_stateData.IsCreated) _stateData.Dispose();
            if (_parameters.IsCreated) _parameters.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (!Application.isPlaying && _stateData.IsCreated && !useFixedOffset)
            {
                _stateData[0] = 0;
            }
        }
    }
}
