using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiAntiJitterMode
    {
        LockToRestPose,
        SmoothFilter,
        ThresholdFilter
    }

    public enum HonamiAntiJitterTarget
    {
        Position,
        Rotation,
        Both
    }

    [BurstCompile]
    public struct HonamiAntiJitterJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> boneHandles;
        
        [ReadOnly] public NativeArray<float3> initialLocalPositions;
        [ReadOnly] public NativeArray<quaternion> initialLocalRotations;
        
        public NativeArray<float3> previousLocalPositions;
        public NativeArray<quaternion> previousLocalRotations;
        
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamWeight = 0;
        private const int ParamMode = 1;
        private const int ParamTarget = 2;
        private const int ParamSpeed = 3;
        private const int ParamThreshold = 4;
        private const int ParamDt = 5;
        private const int ParamIsFirstFrame = 6;

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = parameters[ParamWeight];
            if (w <= 0.001f) return;

            int mode = (int)parameters[ParamMode];
            int target = (int)parameters[ParamTarget];
            
            bool processPos = target == 0 || target == 2;
            bool processRot = target == 1 || target == 2;
            bool isFirstFrame = parameters[ParamIsFirstFrame] > 0.5f;

            float dt = parameters[ParamDt];
            float speed = parameters[ParamSpeed];
            float threshold = parameters[ParamThreshold];

            for (int i = 0; i < boneHandles.Length; i++)
            {
                var handle = boneHandles[i];
                if (!handle.IsValid(stream)) continue;

                if (processPos)
                {
                    float3 currentRawPos = handle.GetLocalPosition(stream);
                    float3 finalPos = currentRawPos;

                    if (mode == 0)
                    {
                        finalPos = initialLocalPositions[i];
                    }
                    else if (mode == 1)
                    {
                        if (!isFirstFrame)
                        {
                            float t = math.saturate(dt * speed);
                            finalPos = math.lerp(previousLocalPositions[i], currentRawPos, t);
                        }
                    }
                    else if (mode == 2)
                    {
                        if (!isFirstFrame)
                        {
                            float dist = math.distance(previousLocalPositions[i], currentRawPos);
                            if (dist < threshold)
                            {
                                finalPos = previousLocalPositions[i];
                            }
                        }
                    }

                    finalPos = math.lerp(currentRawPos, finalPos, w);
                    previousLocalPositions[i] = finalPos;
                    handle.SetLocalPosition(stream, finalPos);
                }

                if (processRot)
                {
                    quaternion currentRawRot = handle.GetLocalRotation(stream);
                    quaternion finalRot = currentRawRot;

                    if (mode == 0)
                    {
                        finalRot = initialLocalRotations[i];
                    }
                    else if (mode == 1)
                    {
                        if (!isFirstFrame)
                        {
                            float t = math.saturate(dt * speed);
                            finalRot = math.slerp(previousLocalRotations[i], currentRawRot, t);
                        }
                    }
                    else if (mode == 2)
                    {
                        if (!isFirstFrame)
                        {
                            float angle = math.abs(math.acos(math.dot(previousLocalRotations[i], currentRawRot)) * 2f);
                            if (angle < threshold)
                            {
                                finalRot = previousLocalRotations[i];
                            }
                        }
                    }

                    finalRot = math.slerp(currentRawRot, finalRot, w);
                    previousLocalRotations[i] = finalRot;
                    handle.SetLocalRotation(stream, finalRot);
                }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Anti Jitter")]
    [ExecuteAlways]
    public sealed class HonamiAntiJitter : HonamiRig
    {
        [Header("Target Bones")]
        public Transform[] bones;

        [Header("Settings")]
        public HonamiAntiJitterMode mode = HonamiAntiJitterMode.SmoothFilter;
        public HonamiAntiJitterTarget target = HonamiAntiJitterTarget.Position;
        
        [Header("Smooth Filter Settings")]
        [Min(0f)] public float smoothSpeed = 15f;
        
        [Header("Threshold Filter Settings")]
        [Min(0f)] public float changeThreshold = 0.001f;

        private AnimationScriptPlayable _playable;
        private NativeArray<TransformStreamHandle> _nativeBoneHandles;
        private NativeArray<float3> _nativeInitialPos;
        private NativeArray<quaternion> _nativeInitialRot;
        private NativeArray<float3> _nativePrevPos;
        private NativeArray<quaternion> _nativePrevRot;
        private NativeArray<float> _nativeParams;

        private Vector3[] _restPositions;
        private Quaternion[] _restRotations;
        
        private Vector3[] _mainThreadPrevPos;
        private Quaternion[] _mainThreadPrevRot;

        private bool _restCaptured;
        private bool _isFirstFrame;

        private void Awake() => CaptureRestPose();

        protected override void OnEnable()
        {
            base.OnEnable();
            CaptureRestPose();
            _isFirstFrame = true;
        }

        public void CaptureRestPose()
        {
            if (bones == null || bones.Length == 0) return;

            if (_restPositions == null || _restPositions.Length < bones.Length)
            {
                System.Array.Resize(ref _restPositions, bones.Length);
                System.Array.Resize(ref _restRotations, bones.Length);
                System.Array.Resize(ref _mainThreadPrevPos, bones.Length);
                System.Array.Resize(ref _mainThreadPrevRot, bones.Length);
            }

            Span<Transform> bonesSpan = bones;
            Span<Vector3> posSpan = _restPositions;
            Span<Quaternion> rotSpan = _restRotations;

            for (int i = 0; i < bonesSpan.Length; i++)
            {
                Transform t = bonesSpan[i];
                if (t != null)
                {
                    posSpan[i] = t.localPosition;
                    rotSpan[i] = t.localRotation;
                }
            }
            _restCaptured = true;
        }

        public override void ResetRig()
        {
            if (bones == null || !_restCaptured) return;
            _isFirstFrame = true;
            
            ReadOnlySpan<Vector3> posSpan = _restPositions;
            ReadOnlySpan<Quaternion> rotSpan = _restRotations;
            
            Span<Vector3> mainPosSpan = _mainThreadPrevPos;
            Span<Quaternion> mainRotSpan = _mainThreadPrevRot;
            
            for (int i = 0; i < bones.Length; i++)
            {
                mainPosSpan[i] = posSpan[i];
                mainRotSpan[i] = rotSpan[i];
            }
            
            if (_nativeInitialPos.IsCreated && _nativeInitialRot.IsCreated)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (i < _nativeInitialPos.Length) _nativeInitialPos[i] = posSpan[i];
                    if (i < _nativeInitialRot.Length) _nativeInitialRot[i] = rotSpan[i];
                    if (i < _nativePrevPos.Length) _nativePrevPos[i] = posSpan[i];
                    if (i < _nativePrevRot.Length) _nativePrevRot[i] = rotSpan[i];
                }
            }
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (bones == null || bones.Length == 0) return Playable.Null;

            int count = bones.Length;
            _nativeBoneHandles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _nativeInitialPos = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeInitialRot = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativePrevPos = new NativeArray<float3>(count, Allocator.Persistent);
            _nativePrevRot = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(7, Allocator.Persistent);

            if (!_restCaptured) CaptureRestPose();

            Span<Transform> bonesSpan = bones;
            ReadOnlySpan<Vector3> posSpan = _restPositions;
            ReadOnlySpan<Quaternion> rotSpan = _restRotations;

            for (int i = 0; i < count; i++)
            {
                if (bonesSpan[i] != null)
                {
                    try
                    {
                        _nativeBoneHandles[i] = animator.BindStreamTransform(bonesSpan[i]);
                    }
                    catch
                    {
                        _nativeBoneHandles[i] = default;
                    }
                    _nativeInitialPos[i] = posSpan[i];
                    _nativeInitialRot[i] = rotSpan[i];
                    _nativePrevPos[i] = posSpan[i];
                    _nativePrevRot[i] = rotSpan[i];
                }
            }

            _isFirstFrame = true;

            var job = new HonamiAntiJitterJob
            {
                boneHandles = _nativeBoneHandles,
                initialLocalPositions = _nativeInitialPos,
                initialLocalRotations = _nativeInitialRot,
                previousLocalPositions = _nativePrevPos,
                previousLocalRotations = _nativePrevRot,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || bones == null || bones.Length == 0)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                return;
            }

            float effectiveWeight = (enabled && gameObject.activeInHierarchy) ? EffectiveWeight : 0f;
            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = (float)mode;
            _nativeParams[2] = (float)target;
            _nativeParams[3] = smoothSpeed;
            _nativeParams[4] = changeThreshold;
            _nativeParams[5] = deltaTime;
            _nativeParams[6] = _isFirstFrame ? 1f : 0f;
            
            _isFirstFrame = false;
        }

        public override void DisposeJobData()
        {
            if (_nativeBoneHandles.IsCreated) _nativeBoneHandles.Dispose();
            if (_nativeInitialPos.IsCreated) _nativeInitialPos.Dispose();
            if (_nativeInitialRot.IsCreated) _nativeInitialRot.Dispose();
            if (_nativePrevPos.IsCreated) _nativePrevPos.Dispose();
            if (_nativePrevRot.IsCreated) _nativePrevRot.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (bones == null || EffectiveWeight <= 0.001f) return;
            if (!_restCaptured) CaptureRestPose();

            bool processPos = target == HonamiAntiJitterTarget.Position || target == HonamiAntiJitterTarget.Both;
            bool processRot = target == HonamiAntiJitterTarget.Rotation || target == HonamiAntiJitterTarget.Both;

            Span<Transform> bonesSpan = bones;
            ReadOnlySpan<Vector3> initialPosSpan = _restPositions;
            ReadOnlySpan<Quaternion> initialRotSpan = _restRotations;
            Span<Vector3> prevPosSpan = _mainThreadPrevPos;
            Span<Quaternion> prevRotSpan = _mainThreadPrevRot;

            for (int i = 0; i < bonesSpan.Length; i++)
            {
                Transform t = bonesSpan[i];
                if (t == null) continue;

                if (processPos)
                {
                    Vector3 currentPos = t.localPosition;
                    Vector3 finalPos = currentPos;

                    if (mode == HonamiAntiJitterMode.LockToRestPose)
                    {
                        finalPos = initialPosSpan[i];
                    }
                    else if (mode == HonamiAntiJitterMode.SmoothFilter && Application.isPlaying)
                    {
                        if (!_isFirstFrame)
                        {
                            float tLerp = math.saturate(deltaTime * smoothSpeed);
                            finalPos = Vector3.Lerp(prevPosSpan[i], currentPos, tLerp);
                        }
                    }
                    else if (mode == HonamiAntiJitterMode.ThresholdFilter && Application.isPlaying)
                    {
                        if (!_isFirstFrame)
                        {
                            float dist = Vector3.Distance(prevPosSpan[i], currentPos);
                            if (dist < changeThreshold)
                            {
                                finalPos = prevPosSpan[i];
                            }
                        }
                    }

                    finalPos = Vector3.Lerp(currentPos, finalPos, EffectiveWeight);
                    prevPosSpan[i] = finalPos;
                    t.localPosition = finalPos;
                }

                if (processRot)
                {
                    Quaternion currentRot = t.localRotation;
                    Quaternion finalRot = currentRot;

                    if (mode == HonamiAntiJitterMode.LockToRestPose)
                    {
                        finalRot = initialRotSpan[i];
                    }
                    else if (mode == HonamiAntiJitterMode.SmoothFilter && Application.isPlaying)
                    {
                        if (!_isFirstFrame)
                        {
                            float tLerp = math.saturate(deltaTime * smoothSpeed);
                            finalRot = Quaternion.Slerp(prevRotSpan[i], currentRot, tLerp);
                        }
                    }
                    else if (mode == HonamiAntiJitterMode.ThresholdFilter && Application.isPlaying)
                    {
                        if (!_isFirstFrame)
                        {
                            float angle = Quaternion.Angle(prevRotSpan[i], currentRot);
                            if (angle < changeThreshold)
                            {
                                finalRot = prevRotSpan[i];
                            }
                        }
                    }

                    finalRot = Quaternion.Slerp(currentRot, finalRot, EffectiveWeight);
                    prevRotSpan[i] = finalRot;
                    t.localRotation = finalRot;
                }
            }
            
            _isFirstFrame = false;
        }
    }
}
