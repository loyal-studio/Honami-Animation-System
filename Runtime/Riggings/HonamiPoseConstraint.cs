using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using HonamiAnimationSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiPoseSpace
    {
        Local,
        World
    }

    public enum HonamiPoseType
    {
        Absolute,
        Additive
    }

    public enum HonamiPoseBakeMode
    {
        Auto,
        Absolute,
        Additive
    }

    public enum HonamiPoseAdditiveReference
    {
        ReferenceClip = 0,
        CurrentClipFrame = 1,
        CurrentTransform = 2,
        ClipFrameZero = 1

    }

    [Serializable]
    public struct HonamiPoseBoneTarget
    {
        public Transform bone;
        public Transform target;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        [Range(0f, 1f)] public float positionWeight;
        [Range(0f, 1f)] public float rotationWeight;

        public static HonamiPoseBoneTarget Default() => new HonamiPoseBoneTarget
        {
            positionWeight = 1f,
            rotationWeight = 1f
        };
    }

    [Serializable]
    public struct HonamiAvatarPoseTarget
    {
        public bool enabled;
        public string boneName;
        public string bonePath;
        public Transform target;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        [Range(0f, 1f)] public float positionWeight;
        [Range(0f, 1f)] public float rotationWeight;

        public static HonamiAvatarPoseTarget Default(string boneName, string bonePath) => new HonamiAvatarPoseTarget
        {
            enabled = true,
            boneName = boneName,
            bonePath = bonePath,
            positionWeight = 1f,
            rotationWeight = 1f
        };
    }

    [BurstCompile]
    public struct HonamiPoseJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> boneHandles;
        [ReadOnly] public NativeArray<int> hasTarget;
        [ReadOnly] public NativeArray<float3> targetWorldPos;
        [ReadOnly] public NativeArray<quaternion> targetWorldRot;
        [ReadOnly] public NativeArray<float3> targetLocalPos;
        [ReadOnly] public NativeArray<quaternion> targetLocalRot;
        [ReadOnly] public NativeArray<float3> positionOffsets;
        [ReadOnly] public NativeArray<quaternion> rotationOffsets;
        [ReadOnly] public NativeArray<float> positionWeights;
        [ReadOnly] public NativeArray<float> rotationWeights;
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamGlobalWeight = 0;
        private const int ParamBoneCount = 1;
        private const int ParamPoseSpace = 2;
        private const int ParamPoseType = 3;

        public void ProcessAnimation(AnimationStream stream)
        {
            float globalWeight = parameters[ParamGlobalWeight];
            if (globalWeight <= 0.001f) return;

            int boneCount = (int)parameters[ParamBoneCount];
            int poseSpace = (int)parameters[ParamPoseSpace];
            int poseType = (int)parameters[ParamPoseType];

            for (int i = 0; i < boneCount; i++)
            {
                var boneH = boneHandles[i];
                if (!boneH.IsValid(stream)) continue;
                float pWeight = positionWeights[i] * globalWeight;
                float rWeight = rotationWeights[i] * globalWeight;
                bool isAdditive = poseType == 1;

                if (hasTarget[i] == 1)
                {
                    if (pWeight > 0.001f)
                    {
                        if (poseSpace == 1)
                        {
                            float3 currentPos = (float3)boneH.GetPosition(stream);
                            float3 targetPos = isAdditive ? currentPos + targetWorldPos[i] + positionOffsets[i] : targetWorldPos[i] + positionOffsets[i];
                            boneH.SetPosition(stream, math.lerp(currentPos, targetPos, pWeight));
                        }
                        else
                        {
                            float3 currentPos = (float3)boneH.GetLocalPosition(stream);
                            float3 targetPos = isAdditive ? currentPos + targetLocalPos[i] + positionOffsets[i] : targetLocalPos[i] + positionOffsets[i];
                            boneH.SetLocalPosition(stream, math.lerp(currentPos, targetPos, pWeight));
                        }
                    }

                    if (rWeight > 0.001f)
                    {
                        if (poseSpace == 1)
                        {
                            quaternion currentRot = (quaternion)boneH.GetRotation(stream);
                            quaternion targetRot = isAdditive ? math.mul(currentRot, math.mul(targetWorldRot[i], rotationOffsets[i])) : math.mul(targetWorldRot[i], rotationOffsets[i]);
                            boneH.SetRotation(stream, math.slerp(currentRot, targetRot, rWeight));
                        }
                        else
                        {
                            quaternion currentRot = (quaternion)boneH.GetLocalRotation(stream);
                            quaternion targetRot = isAdditive ? math.mul(currentRot, math.mul(targetLocalRot[i], rotationOffsets[i])) : math.mul(targetLocalRot[i], rotationOffsets[i]);
                            boneH.SetLocalRotation(stream, math.slerp(currentRot, targetRot, rWeight));
                        }
                    }
                }
                else
                {
                    if (pWeight > 0.001f)
                    {
                        if (poseSpace == 1)
                        {
                            float3 currentPos = (float3)boneH.GetPosition(stream);
                            float3 targetPos = isAdditive ? currentPos + positionOffsets[i] : positionOffsets[i];
                            boneH.SetPosition(stream, math.lerp(currentPos, targetPos, pWeight));
                        }
                        else
                        {
                            float3 currentPos = (float3)boneH.GetLocalPosition(stream);
                            float3 targetPos = isAdditive ? currentPos + positionOffsets[i] : positionOffsets[i];
                            boneH.SetLocalPosition(stream, math.lerp(currentPos, targetPos, pWeight));
                        }
                    }

                    if (rWeight > 0.001f)
                    {
                        if (poseSpace == 1)
                        {
                            quaternion currentRot = (quaternion)boneH.GetRotation(stream);
                            quaternion targetRot = isAdditive ? math.mul(currentRot, rotationOffsets[i]) : rotationOffsets[i];
                            boneH.SetRotation(stream, math.slerp(currentRot, targetRot, rWeight));
                        }
                        else
                        {
                            quaternion currentRot = (quaternion)boneH.GetLocalRotation(stream);
                            quaternion targetRot = isAdditive ? math.mul(currentRot, rotationOffsets[i]) : rotationOffsets[i];
                            boneH.SetLocalRotation(stream, math.slerp(currentRot, targetRot, rWeight));
                        }
                    }
                }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Pose Constraint")]
    [ExecuteAlways]
    public sealed class HonamiPoseConstraint : HonamiRig
    {
        public HonamiLookAtTargetSource targetSource = HonamiLookAtTargetSource.Manual;
        public HonamiPoseType poseType = HonamiPoseType.Absolute;
        public HonamiPoseSpace poseSpace = HonamiPoseSpace.Local;
        
        [Header("Objects")]
        public HonamiPoseBoneTarget[] targets;

        [Header("Avatar Objects")]
        public HonamiAnimator honamiAnimator;
        public HonamiAvatar avatar;
        public HonamiAvatarPoseTarget[] avatarTargets;

        [Header("Bake From Clip")]
        public AnimationClip bakeClip;
        public int bakeFrame;
        public float bakeTime;
        public HonamiPoseBakeMode bakeMode = HonamiPoseBakeMode.Auto;
        public HonamiPoseAdditiveReference bakeAdditiveRef = HonamiPoseAdditiveReference.ReferenceClip;
        public AnimationClip bakeReferenceClip;
        public int bakeRefFrame;
        public float bakeRefTime;
        public Transform bakeSampleRoot;
        public bool bakePositions = true;
        public bool bakeRotations = true;
        public bool bakeSelectedOnly;

        private AnimationScriptPlayable _playable;
        private NativeArray<TransformStreamHandle> _boneHandles;
        private NativeArray<int> _nativeHasTarget;
        private NativeArray<float3> _nativeTargetWorldPos;
        private NativeArray<quaternion> _nativeTargetWorldRot;
        private NativeArray<float3> _nativeTargetLocalPos;
        private NativeArray<quaternion> _nativeTargetLocalRot;
        private NativeArray<float3> _nativePosOffsets;
        private NativeArray<quaternion> _nativeRotOffsets;
        private NativeArray<float> _nativePosWeights;
        private NativeArray<float> _nativeRotWeights;
        private NativeArray<float> _nativeParams;
        
        private int _boundBoneCount;
        private HonamiPoseBoneTarget[] _resolvedAvatarTargets;
        private HonamiAnimator _cachedHonamiAnimator;
        private HonamiBoneReplacer _cachedBoneReplacer;
        private bool _avatarComponentsCached;
        private Transform[] _resolvedAvatarBones;
        private bool _avatarBonesResolved;

        public override void ResetRig()
        {
            _avatarComponentsCached = false;
            _avatarBonesResolved = false;
            _cachedHonamiAnimator = null;
            _cachedBoneReplacer = null;
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();

            HonamiPoseBoneTarget[] activeTargets = ResolveActiveTargets(out int count);
            if (count == 0) return Playable.Null;

            _boundBoneCount = count;

            _boneHandles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _nativeHasTarget = new NativeArray<int>(count, Allocator.Persistent);
            _nativeTargetWorldPos = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeTargetWorldRot = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativeTargetLocalPos = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeTargetLocalRot = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativePosOffsets = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeRotOffsets = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativePosWeights = new NativeArray<float>(count, Allocator.Persistent);
            _nativeRotWeights = new NativeArray<float>(count, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(4, Allocator.Persistent);

            for (int i = 0; i < count; i++)
            {
                ref readonly var data = ref activeTargets[i];
                if (data.bone == null) continue;

                _boneHandles[i] = animator.BindStreamTransform(data.bone);
                _nativePosOffsets[i] = data.positionOffset;
                _nativeRotOffsets[i] = Quaternion.Euler(data.rotationOffset);
                _nativePosWeights[i] = data.positionWeight;
                _nativeRotWeights[i] = data.rotationWeight;
            }

            var job = new HonamiPoseJob
            {
                boneHandles = _boneHandles,
                hasTarget = _nativeHasTarget,
                targetWorldPos = _nativeTargetWorldPos,
                targetWorldRot = _nativeTargetWorldRot,
                targetLocalPos = _nativeTargetLocalPos,
                targetLocalRot = _nativeTargetLocalRot,
                positionOffsets = _nativePosOffsets,
                rotationOffsets = _nativeRotOffsets,
                positionWeights = _nativePosWeights,
                rotationWeights = _nativeRotWeights,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid()) return;

            float effectiveWeight = (enabled && gameObject.activeInHierarchy && _boundBoneCount > 0) ? EffectiveWeight : 0f;

            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = _boundBoneCount;
            _nativeParams[2] = (float)poseSpace;
            _nativeParams[3] = (float)poseType;

            HonamiPoseBoneTarget[] activeTargets = ResolveActiveTargets(out int count);
            int actualCount = math.min(count, _boundBoneCount);

            for (int i = 0; i < actualCount; i++)
            {
                ref readonly var data = ref activeTargets[i];

                if (data.target != null)
                {
                    _nativeHasTarget[i] = 1;
                    _nativeTargetWorldPos[i] = data.target.position;
                    _nativeTargetWorldRot[i] = data.target.rotation;
                    _nativeTargetLocalPos[i] = data.target.localPosition;
                    _nativeTargetLocalRot[i] = data.target.localRotation;
                }
                else
                {
                    _nativeHasTarget[i] = 0;
                }

                _nativePosOffsets[i] = data.positionOffset;
                _nativeRotOffsets[i] = Quaternion.Euler(data.rotationOffset);
                _nativePosWeights[i] = data.positionWeight;
                _nativeRotWeights[i] = data.rotationWeight;
            }
        }

        public override void DisposeJobData()
        {
            if (_boneHandles.IsCreated) _boneHandles.Dispose();
            if (_nativeHasTarget.IsCreated) _nativeHasTarget.Dispose();
            if (_nativeTargetWorldPos.IsCreated) _nativeTargetWorldPos.Dispose();
            if (_nativeTargetWorldRot.IsCreated) _nativeTargetWorldRot.Dispose();
            if (_nativeTargetLocalPos.IsCreated) _nativeTargetLocalPos.Dispose();
            if (_nativeTargetLocalRot.IsCreated) _nativeTargetLocalRot.Dispose();
            if (_nativePosOffsets.IsCreated) _nativePosOffsets.Dispose();
            if (_nativeRotOffsets.IsCreated) _nativeRotOffsets.Dispose();
            if (_nativePosWeights.IsCreated) _nativePosWeights.Dispose();
            if (_nativeRotWeights.IsCreated) _nativeRotWeights.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (_playable.IsValid())
            {
                PrepareJobData(deltaTime);
                return;
            }

            HonamiPoseBoneTarget[] activeTargets = ResolveActiveTargets(out int count);

            if (EffectiveWeight <= 0.001f || activeTargets == null || count == 0) return;

            bool isAdditive = poseType == HonamiPoseType.Additive;

            if (isAdditive && !Application.isPlaying) return;

            var span = new ReadOnlySpan<HonamiPoseBoneTarget>(activeTargets, 0, count);
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var data = ref span[i];
                if (data.bone == null) continue;

                float pWeight = data.positionWeight * EffectiveWeight;
                float rWeight = data.rotationWeight * EffectiveWeight;

                if (data.target != null)
                {
                    if (pWeight > 0.001f)
                    {
                        if (poseSpace == HonamiPoseSpace.World)
                        {
                            Vector3 targetPos = isAdditive ? data.bone.position + data.target.position + data.positionOffset : data.target.position + data.positionOffset;
                            data.bone.position = Vector3.Lerp(data.bone.position, targetPos, pWeight);
                        }
                        else
                        {
                            Vector3 targetPos = isAdditive ? data.bone.localPosition + data.target.localPosition + data.positionOffset : data.target.localPosition + data.positionOffset;
                            data.bone.localPosition = Vector3.Lerp(data.bone.localPosition, targetPos, pWeight);
                        }
                    }

                    if (rWeight > 0.001f)
                    {
                        if (poseSpace == HonamiPoseSpace.World)
                        {
                            Quaternion targetRot = isAdditive ? data.bone.rotation * data.target.rotation * Quaternion.Euler(data.rotationOffset) : data.target.rotation * Quaternion.Euler(data.rotationOffset);
                            data.bone.rotation = Quaternion.Slerp(data.bone.rotation, targetRot, rWeight);
                        }
                        else
                        {
                            Quaternion targetRot = isAdditive ? data.bone.localRotation * data.target.localRotation * Quaternion.Euler(data.rotationOffset) : data.target.localRotation * Quaternion.Euler(data.rotationOffset);
                            data.bone.localRotation = Quaternion.Slerp(data.bone.localRotation, targetRot, rWeight);
                        }
                    }
                }
                else
                {
                    if (pWeight > 0.001f)
                    {
                        if (poseSpace == HonamiPoseSpace.World)
                        {
                            Vector3 targetPos = isAdditive ? data.bone.position + data.positionOffset : data.positionOffset;
                            data.bone.position = Vector3.Lerp(data.bone.position, targetPos, pWeight);
                        }
                        else
                        {
                            Vector3 targetPos = isAdditive ? data.bone.localPosition + data.positionOffset : data.positionOffset;
                            data.bone.localPosition = Vector3.Lerp(data.bone.localPosition, targetPos, pWeight);
                        }
                    }

                    if (rWeight > 0.001f)
                    {
                        if (poseSpace == HonamiPoseSpace.World)
                        {
                            Quaternion targetRot = isAdditive ? data.bone.rotation * Quaternion.Euler(data.rotationOffset) : Quaternion.Euler(data.rotationOffset);
                            data.bone.rotation = Quaternion.Slerp(data.bone.rotation, targetRot, rWeight);
                        }
                        else
                        {
                            Quaternion targetRot = isAdditive ? data.bone.localRotation * Quaternion.Euler(data.rotationOffset) : Quaternion.Euler(data.rotationOffset);
                            data.bone.localRotation = Quaternion.Slerp(data.bone.localRotation, targetRot, rWeight);
                        }
                    }
                }
            }
        }

        private HonamiPoseBoneTarget[] ResolveActiveTargets(out int count)
        {
            if (targetSource == HonamiLookAtTargetSource.Avatar)
            {
                count = BuildAvatarTargets();
                return _resolvedAvatarTargets;
            }

            count = targets != null ? targets.Length : 0;
            return targets;
        }

        private int BuildAvatarTargets()
        {
            if (avatarTargets == null || avatarTargets.Length == 0) return 0;

            if (_resolvedAvatarTargets == null || _resolvedAvatarTargets.Length < avatarTargets.Length)
                System.Array.Resize(ref _resolvedAvatarTargets, avatarTargets.Length);

            EnsureAvatarBoneCache();

            int count = 0;
            var span = new ReadOnlySpan<HonamiAvatarPoseTarget>(avatarTargets);
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var entry = ref span[i];
                if (!entry.enabled) continue;

                Transform bone = _resolvedAvatarBones[i];
                if (bone == null) continue;

                Transform resolvedTarget = entry.target;

                _resolvedAvatarTargets[count++] = new HonamiPoseBoneTarget
                {
                    bone = bone,
                    target = resolvedTarget,
                    positionOffset = entry.positionOffset,
                    rotationOffset = entry.rotationOffset,
                    positionWeight = entry.positionWeight,
                    rotationWeight = entry.rotationWeight
                };
            }

            return count;
        }

        // Resolving a bone walks the hierarchy by path and scans the replacer list by name, so it is
        // done once per binding instead of per frame; ResetRig drops the cache when either can change.
        private void EnsureAvatarBoneCache()
        {
            bool rebuild = !_avatarBonesResolved
                           || _resolvedAvatarBones == null
                           || _resolvedAvatarBones.Length != avatarTargets.Length;

            if (rebuild)
            {
                if (_resolvedAvatarBones == null || _resolvedAvatarBones.Length != avatarTargets.Length)
                    _resolvedAvatarBones = new Transform[avatarTargets.Length];

                _avatarBonesResolved = true;
            }

            for (int i = 0; i < avatarTargets.Length; i++)
            {
                if (!avatarTargets[i].enabled)
                {
                    _resolvedAvatarBones[i] = null;
                    continue;
                }

                // an entry that stayed unresolved keeps retrying, so a bone spawned later still binds
                if (rebuild || _resolvedAvatarBones[i] == null)
                    _resolvedAvatarBones[i] = ResolveAvatarBone(in avatarTargets[i]);
            }
        }

        public Transform ResolveAvatarBone(in HonamiAvatarPoseTarget entry)
        {
            if (!_avatarComponentsCached)
            {
                _cachedHonamiAnimator = honamiAnimator;
                if (_cachedHonamiAnimator == null)
                {
                    Transform p = transform.parent;
                    while (p != null)
                    {
                        if (p.TryGetComponent(out _cachedHonamiAnimator)) break;
                        p = p.parent;
                    }
                }
                
                if (_cachedHonamiAnimator != null)
                {
                    _cachedHonamiAnimator.TryGetComponent(out _cachedBoneReplacer);
                }
                
                _avatarComponentsCached = true;
            }

            if (_cachedHonamiAnimator == null) return null;

            Transform root = _cachedHonamiAnimator.transform;
            Transform source = string.IsNullOrEmpty(entry.bonePath) ? root : root.Find(entry.bonePath);

            if (_cachedBoneReplacer != null)
            {
                Transform replacement = _cachedBoneReplacer.GetReplacement(entry.boneName);
                if (replacement != null) return replacement;
                if (_cachedBoneReplacer.disableUnreplacedBones) return null;
            }

            return source;
        }
    }
}
