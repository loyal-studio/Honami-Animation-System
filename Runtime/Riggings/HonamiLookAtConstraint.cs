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
    public enum HonamiLookAtTargetSource
    {
        Manual,
        Avatar
    }

    public enum HonamiTargetMode
    {
        Single,
        Multiple
    }

    public enum HonamiLookAtType
    {
        Absolute,
        Additive
    }

    public enum HonamiLookAtMode
    {
        Position,
        Rotation,
        LocalRotation,
        DirectPosition
    }

    public enum HonamiUpMode
    {
        World,
        Parent,
        Object,
        Default,
        Target,
        BoneAxis
    }

    [Serializable]
    public struct HonamiLookAtTarget
    {
        public Transform transform;
        public Vector3 aimAxis;
        public Vector3 upAxis;
        public Vector3 rotationOffset;
        public Vector3 positionOffset;
        [Range(0f, 1f)] public float weightX;
        [Range(0f, 1f)] public float weightY;
        [Range(0f, 1f)] public float weightZ;

        public static HonamiLookAtTarget Default() => new HonamiLookAtTarget
        {
            aimAxis = Vector3.forward,
            upAxis = Vector3.up,
            weightX = 1f,
            weightY = 1f,
            weightZ = 1f
        };
    }

    [Serializable]
    public struct HonamiAvatarLookAtTarget
    {
        public bool enabled;
        public string boneName;
        public string bonePath;
        public Vector3 aimAxis;
        public Vector3 upAxis;
        public Vector3 rotationOffset;
        public Vector3 positionOffset;
        [Range(0f, 1f)] public float weightX;
        [Range(0f, 1f)] public float weightY;
        [Range(0f, 1f)] public float weightZ;

        public static HonamiAvatarLookAtTarget Default(string boneName, string bonePath) => new HonamiAvatarLookAtTarget
        {
            enabled = true,
            boneName = boneName,
            bonePath = bonePath,
            aimAxis = Vector3.forward,
            upAxis = Vector3.up,
            weightX = 1f,
            weightY = 1f,
            weightZ = 1f
        };
    }

    [BurstCompile]
    public struct HonamiLookAtJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> boneHandles;
        public NativeArray<TransformStreamHandle> parentHandles;

        [ReadOnly] public NativeArray<float3> targetWorldPos;
        [ReadOnly] public NativeArray<quaternion> targetWorldRot;
        public TransformStreamHandle targetStreamHandle;
        [ReadOnly] public NativeArray<float3> worldUpDir;

        [ReadOnly] public NativeArray<float3> boneAimAxes;
        [ReadOnly] public NativeArray<float3> boneUpAxes;
        [ReadOnly] public NativeArray<float3> boneRotOffsets;
        [ReadOnly] public NativeArray<float3> bonePosOffsets;
        [ReadOnly] public NativeArray<float3> boneAxisWeights;

        [ReadOnly] public NativeArray<quaternion> cachedAxisOffsets;

        public NativeArray<quaternion> smoothedRotations;
        public NativeArray<int> initialized;

        [ReadOnly] public NativeArray<float> parameters;
        [ReadOnly] public NativeArray<float3> boneUpLocalAxisNative;
        [ReadOnly] public NativeArray<float3> eyeCenterOffsetNative;
        public TransformStreamHandle eyeCenterHandle;

        private const int ParamGlobalWeight = 0;
        private const int ParamLerpSpeed = 1;
        private const int ParamDeltaTime = 2;
        private const int ParamLookAtMode = 3;
        private const int ParamLookAtType = 4;
        private const int ParamLimitAngle = 5;
        private const int ParamMaxAngle = 6;
        private const int ParamBoneCount = 7;
        private const int ParamIsPlaying = 8;
        private const int ParamUpMode = 9;
        private const int ParamUseTargetHandle = 10;
        private const int ParamUseEyeTransform = 11;
        private const int ParamMinPitchDistance = 12;

        public void ProcessAnimation(AnimationStream stream)
        {
            float globalWeight = math.clamp(parameters[ParamGlobalWeight], 0f, 1f);
            if (globalWeight <= 0.001f)
            {
                for (int i = 0; i < (int)parameters[ParamBoneCount]; i++)
                    initialized[i] = 0;
                return;
            }

            int boneCount = (int)parameters[ParamBoneCount];
            float lerpSpeed = parameters[ParamLerpSpeed];
            float dt = parameters[ParamDeltaTime];
            int lookAtMode = (int)parameters[ParamLookAtMode];
            int lookAtType = (int)parameters[ParamLookAtType];
            bool doLimit = parameters[ParamLimitAngle] > 0.5f;
            float maxAngle = parameters[ParamMaxAngle];
            bool isPlaying = parameters[ParamIsPlaying] > 0.5f;

            bool useTargetHandle = parameters[ParamUseTargetHandle] > 0.5f;
            float3 tPos = useTargetHandle && targetStreamHandle.IsValid(stream)
                ? (float3)targetStreamHandle.GetPosition(stream)
                : targetWorldPos[0];
            quaternion tRot = useTargetHandle && targetStreamHandle.IsValid(stream)
                ? (quaternion)targetStreamHandle.GetRotation(stream)
                : targetWorldRot[0];

            for (int i = 0; i < boneCount; i++)
            {
                var boneH = boneHandles[i];
                if (!boneH.IsValid(stream)) continue;
                var parentH = parentHandles[i];

                quaternion boneRot = (quaternion)boneH.GetRotation(stream);
                float3 bonePos = (float3)boneH.GetPosition(stream);
                quaternion parentRot = parentH.IsValid(stream) ? (quaternion)parentH.GetRotation(stream) : quaternion.identity;

                quaternion worldTargetRotation;

                if (lookAtMode == 2)
                {
                    quaternion localPose = math.mul(math.mul(tRot, cachedAxisOffsets[i]), quaternion.Euler(math.radians(boneRotOffsets[i])));
                    quaternion targetLocal = lookAtType == 1
                        ? math.mul((quaternion)boneH.GetLocalRotation(stream), localPose)
                        : localPose;
                    worldTargetRotation = math.mul(parentRot, targetLocal);
                }
                else
                {
                    float3 lookDirection;
                    float3 eyeOrigin;
                    bool useEyeTransform = parameters[ParamUseEyeTransform] > 0.5f;

                    if (lookAtMode == 3)
                    {
                        if (useEyeTransform && eyeCenterHandle.IsValid(stream))
                            eyeOrigin = (float3)eyeCenterHandle.GetPosition(stream) + eyeCenterOffsetNative[0];
                        else
                            eyeOrigin = bonePos + eyeCenterOffsetNative[0];
                    }
                    else
                    {
                        if (useEyeTransform && eyeCenterHandle.IsValid(stream))
                            eyeOrigin = (float3)eyeCenterHandle.GetPosition(stream) + math.mul(boneRot, eyeCenterOffsetNative[0]);
                        else
                            eyeOrigin = bonePos + math.mul(boneRot, eyeCenterOffsetNative[0]);
                    }

                    if (lookAtMode == 0 || lookAtMode == 3)
                        lookDirection = tPos - eyeOrigin;
                    else
                        lookDirection = math.mul(tRot, new float3(0, 0, 1));

                    float lookMag = math.length(lookDirection);
                    if (lookMag < 0.0001f) continue;
                    lookDirection /= lookMag;

                    quaternion baseLook;

                    if (lookAtMode == 3)
                    {
                        float3 rawDir = lookDirection * lookMag;
                        float minPitchDist = parameters[ParamMinPitchDistance];
                        float3 adjustedDir = rawDir;

                        if (minPitchDist > 0.0001f)
                        {
                            float3 horizRaw = new float3(rawDir.x, 0f, rawDir.z);
                            float rawHorizLen = math.length(horizRaw);
                            if (rawHorizLen > 0.0001f && rawHorizLen < minPitchDist)
                            {
                                float scale = minPitchDist / rawHorizLen;
                                adjustedDir = new float3(rawDir.x * scale, rawDir.y, rawDir.z * scale);
                            }
                        }

                        float3 normDir = math.normalizesafe(adjustedDir, new float3(0, 0, 1));
                        float3 dpUp = new float3(0, 1, 0);
                        if (math.lengthsq(math.cross(normDir, dpUp)) < 0.0001f)
                            dpUp = new float3(0, 0, 1);
                        baseLook = quaternion.LookRotation(normDir, dpUp);
                    }
                    else
                    {
                        float3 upDir;
                        int currentUpMode = (int)parameters[ParamUpMode];
                        if (currentUpMode == 4)
                            upDir = math.mul(tRot, new float3(0, 1, 0));
                        else if (currentUpMode == 5)
                            upDir = math.mul(boneRot, boneUpLocalAxisNative[0]);
                        else
                            upDir = worldUpDir[0];
                        if (math.lengthsq(upDir) < 0.0001f) upDir = new float3(0, 1, 0);
                        if (math.lengthsq(math.cross(lookDirection, upDir)) < 0.0001f)
                            upDir = math.abs(lookDirection.y) < 0.9f ? new float3(0, 1, 0) : new float3(0, 0, 1);

                        baseLook = quaternion.LookRotation(lookDirection, upDir);
                    }

                    worldTargetRotation = math.mul(math.mul(baseLook, cachedAxisOffsets[i]), quaternion.Euler(math.radians(boneRotOffsets[i])));

                    if (lookAtType == 1)
                    {
                        float3 normAim = math.normalizesafe(boneAimAxes[i], new float3(0, 0, 1));
                        float3 normUp = math.normalizesafe(boneUpAxes[i], new float3(0, 1, 0));
                        if (math.lengthsq(math.cross(normAim, normUp)) < 0.0001f)
                            normUp = math.abs(normAim.y) < 0.9f ? new float3(0, 1, 0) : new float3(0, 0, 1);

                        quaternion identityLook = math.mul(math.mul(parentRot, quaternion.LookRotation(normAim, normUp)), cachedAxisOffsets[i]);
                        quaternion delta = math.mul(worldTargetRotation, math.inverse(identityLook));
                        worldTargetRotation = math.mul(delta, boneRot);
                    }
                }

                if (doLimit)
                {
                    float angle = MathAngle(boneRot, worldTargetRotation);
                    if (angle > maxAngle)
                        worldTargetRotation = RotateTowards(boneRot, worldTargetRotation, maxAngle);
                }

                quaternion finalRotation;
                if (lerpSpeed > 0f && isPlaying)
                {
                    if (initialized[i] == 0)
                    {
                        smoothedRotations[i] = boneRot;
                        initialized[i] = 1;
                    }
                    smoothedRotations[i] = math.slerp(smoothedRotations[i], worldTargetRotation, dt * lerpSpeed);
                    finalRotation = smoothedRotations[i];
                }
                else
                {
                    initialized[i] = 0;
                    finalRotation = worldTargetRotation;
                }

                float3 axisWeights = boneAxisWeights[i];
                if (axisWeights.x < 0.999f || axisWeights.y < 0.999f || axisWeights.z < 0.999f)
                {
                    quaternion invParent = math.inverse(parentRot);
                    float3 currentEuler = ToEulerDeg(math.mul(invParent, boneRot));
                    float3 targetEuler = ToEulerDeg(math.mul(invParent, finalRotation));

                    float x = LerpAngleDeg(currentEuler.x, targetEuler.x, axisWeights.x);
                    float y = LerpAngleDeg(currentEuler.y, targetEuler.y, axisWeights.y);
                    float z = LerpAngleDeg(currentEuler.z, targetEuler.z, axisWeights.z);

                    finalRotation = math.mul(parentRot, quaternion.Euler(math.radians(new float3(x, y, z))));
                }

                boneH.SetRotation(stream, (Quaternion)math.slerp(boneRot, finalRotation, globalWeight));

                float3 posOff = bonePosOffsets[i];
                if (math.lengthsq(posOff) > 0.0001f)
                {
                    float3 localPos = (float3)boneH.GetLocalPosition(stream);
                    boneH.SetLocalPosition(stream, (Vector3)(localPos + posOff * globalWeight));
                }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        private static float MathAngle(quaternion a, quaternion b)
        {
            float dot = math.abs(math.dot(a, b));
            return math.degrees(2f * math.acos(math.min(dot, 1f)));
        }

        private static quaternion RotateTowards(quaternion from, quaternion to, float maxDeg)
        {
            float angle = MathAngle(from, to);
            if (angle <= 0.001f) return to;
            float t = math.min(1f, maxDeg / angle);
            return math.slerp(from, to, t);
        }

        private static float3 ToEulerDeg(quaternion q)
        {
            return math.degrees(math.Euler(q));
        }

        private static float LerpAngleDeg(float a, float b, float t)
        {
            float delta = RepeatMod(b - a + 180f, 360f) - 180f;
            return a + delta * t;
        }

        private static float RepeatMod(float t, float length)
        {
            return t - math.floor(t / length) * length;
        }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami LookAt Constraint")]
    [ExecuteAlways]
    public sealed class HonamiLookAtConstraint : HonamiRig
    {
        [Header("Settings")]
        public HonamiLookAtTargetSource targetSource = HonamiLookAtTargetSource.Manual;
        public HonamiTargetMode targetMode = HonamiTargetMode.Single;
        public HonamiLookAtType lookAtType = HonamiLookAtType.Absolute;
        public HonamiLookAtMode lookAtMode = HonamiLookAtMode.Position;
        public HonamiUpMode upMode = HonamiUpMode.World;

        [Header("Targets")]
        public Transform target;
        public Transform worldUpObject;
        public Vector3 worldUpAxis = Vector3.up;
        public Vector3 boneUpLocalAxis = Vector3.forward;

        [Header("Objects")]
        public HonamiLookAtTarget[] targets;

        [Header("Avatar Objects")]
        public HonamiAnimator honamiAnimator;
        public HonamiAvatar avatar;
        public HonamiAvatarLookAtTarget[] avatarTargets;

        public Transform constrainedObject
        {
            get => (targets != null && targets.Length > 0) ? targets[0].transform : null;
            set
            {
                if (targets == null || targets.Length == 0) targets = new[] { HonamiLookAtTarget.Default() };
                targets[0].transform = value;
            }
        }

        [Header("Tracking Settings")]
        public float lerpSpeed = 0f;
        public Transform eyeTransform;
        public Vector3 eyeCenterOffset;
        public float minPitchDistance = 2f;

        [Header("Rotation Limits")]
        public bool limitAngle = false;
        [Range(0f, 180f)]
        public float maxAngle = 90f;

        private AnimationScriptPlayable _playable;
        private NativeArray<TransformStreamHandle> _boneHandles;
        private NativeArray<TransformStreamHandle> _parentHandles;
        private NativeArray<float3> _nativeTargetPos;
        private NativeArray<quaternion> _nativeTargetRot;
        private NativeArray<float3> _nativeUpDir;
        private NativeArray<float3> _nativeAimAxes;
        private NativeArray<float3> _nativeUpAxes;
        private NativeArray<float3> _nativeRotOffsets;
        private NativeArray<float3> _nativePosOffsets;
        private NativeArray<float3> _nativeAxisWeights;
        private NativeArray<quaternion> _nativeCachedOffsets;
        private NativeArray<quaternion> _nativeSmoothed;
        private NativeArray<int> _nativeInitialized;
        private NativeArray<float> _nativeParams;
        private NativeArray<float3> _nativeBoneUpLocalAxis;
        private NativeArray<float3> _nativeEyeCenterOffset;

        private int _boundBoneCount;
        private bool _targetInHierarchy;
        private HonamiLookAtTarget[] _resolvedAvatarTargets;
        private Animator _cachedAnimator;
        private Transform _boundTarget;
        private HonamiAnimator _cachedHonamiAnimator;
        private HonamiBoneReplacer _cachedBoneReplacer;
        private bool _avatarComponentsCached;
        private Transform[] _resolvedAvatarBones;
        private bool _avatarBonesResolved;

        private Quaternion[] _smoothedRotations;
        private bool[] _isInitialized;
        private Quaternion[] _cachedAxisOffsets;
        private Vector3[] _lastAimAxes;
        private Vector3[] _lastUpAxes;

        public override void ResetRig()
        {
            _avatarComponentsCached = false;
            _avatarBonesResolved = false;
            _cachedHonamiAnimator = null;
            _cachedBoneReplacer = null;
            InvalidateAxisOffsetCache();

            if (_nativeSmoothed.IsCreated)
            {
                for (int i = 0; i < _nativeSmoothed.Length; i++)
                    _nativeSmoothed[i] = quaternion.identity;
            }
            if (_nativeInitialized.IsCreated)
            {
                for (int i = 0; i < _nativeInitialized.Length; i++)
                    _nativeInitialized[i] = 0;
            }
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();

            HonamiLookAtTarget[] activeTargets = ResolveActiveTargets(out int count);
            if (count == 0 || target == null) return Playable.Null;

            _boundBoneCount = count;

            _boneHandles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _parentHandles = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _nativeTargetPos = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeTargetRot = new NativeArray<quaternion>(1, Allocator.Persistent);
            _nativeUpDir = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeAimAxes = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeUpAxes = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeRotOffsets = new NativeArray<float3>(count, Allocator.Persistent);
            _nativePosOffsets = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeAxisWeights = new NativeArray<float3>(count, Allocator.Persistent);
            _nativeCachedOffsets = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativeSmoothed = new NativeArray<quaternion>(count, Allocator.Persistent);
            _nativeInitialized = new NativeArray<int>(count, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(13, Allocator.Persistent);
            _nativeBoneUpLocalAxis = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeBoneUpLocalAxis[0] = (float3)boneUpLocalAxis;
            _nativeEyeCenterOffset = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeEyeCenterOffset[0] = (float3)eyeCenterOffset;

            // the native offsets are fresh; the managed axis cache must not report them as up to date
            EnsureArrays(count);
            InvalidateAxisOffsetCache();

            for (int i = 0; i < count; i++)
            {
                ref readonly var data = ref activeTargets[i];
                if (data.transform == null)
                {
                    Debug.LogWarning($"HonamiLookAtConstraint: Objects entry {i} on '{name}' has no bone assigned. It is skipped until one is set.", this);
                    continue;
                }

                if (!data.transform.IsChildOf(animator.transform))
                {
                    Debug.LogWarning($"HonamiLookAtConstraint: bone '{data.transform.name}' on '{name}' is outside the Animator '{animator.name}' hierarchy and cannot be driven. It is skipped.", this);
                    continue;
                }

                _boneHandles[i] = animator.BindStreamTransform(data.transform);
                _parentHandles[i] = data.transform.parent != null
                    ? animator.BindStreamTransform(data.transform.parent)
                    : default;

                _nativeAimAxes[i] = data.aimAxis;
                _nativeUpAxes[i] = data.upAxis;
                _nativeRotOffsets[i] = data.rotationOffset;
                _nativePosOffsets[i] = data.positionOffset;
                _nativeAxisWeights[i] = new float3(data.weightX, data.weightY, data.weightZ);

                Vector3 aim = data.aimAxis.sqrMagnitude > 0.0001f ? data.aimAxis.normalized : Vector3.forward;
                Vector3 up = data.upAxis.sqrMagnitude > 0.0001f ? data.upAxis.normalized : Vector3.up;
                if (Vector3.SqrMagnitude(Vector3.Cross(aim, up)) < 0.0001f)
                    up = Mathf.Abs(aim.y) < 0.9f ? Vector3.up : Vector3.forward;
                _nativeCachedOffsets[i] = Quaternion.Inverse(Quaternion.LookRotation(aim, up));
            }

            var job = new HonamiLookAtJob
            {
                boneHandles = _boneHandles,
                parentHandles = _parentHandles,
                targetWorldPos = _nativeTargetPos,
                targetWorldRot = _nativeTargetRot,
                worldUpDir = _nativeUpDir,
                boneAimAxes = _nativeAimAxes,
                boneUpAxes = _nativeUpAxes,
                boneRotOffsets = _nativeRotOffsets,
                bonePosOffsets = _nativePosOffsets,
                boneAxisWeights = _nativeAxisWeights,
                cachedAxisOffsets = _nativeCachedOffsets,
                smoothedRotations = _nativeSmoothed,
                initialized = _nativeInitialized,
                parameters = _nativeParams,
                boneUpLocalAxisNative = _nativeBoneUpLocalAxis,
                eyeCenterOffsetNative = _nativeEyeCenterOffset
            };

            _cachedAnimator = animator;
            _boundTarget = target;
            _targetInHierarchy = target != null && target.IsChildOf(animator.transform);
            if (_targetInHierarchy)
                job.targetStreamHandle = animator.BindStreamTransform(target);

            if (eyeTransform != null && eyeTransform.IsChildOf(animator.transform))
                job.eyeCenterHandle = animator.BindStreamTransform(eyeTransform);

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid()) return;

            float effectiveWeight = (enabled && gameObject.activeInHierarchy && target != null && _boundBoneCount > 0)
                ? Mathf.Clamp01(EffectiveWeight)
                : 0f;

            if (target != null)
            {
                _nativeTargetPos[0] = target.position;
                _nativeTargetRot[0] = target.rotation;
            }

            Vector3 upDir;
            switch (upMode)
            {
                case HonamiUpMode.Parent:
                    upDir = constrainedObject != null && constrainedObject.parent != null
                        ? constrainedObject.parent.up : Vector3.up;
                    break;
                case HonamiUpMode.Object:
                    upDir = worldUpObject != null ? worldUpObject.up : Vector3.up;
                    break;
                case HonamiUpMode.Default:
                    upDir = constrainedObject != null ? constrainedObject.up : Vector3.up;
                    break;
                case HonamiUpMode.Target:
                    upDir = target != null ? target.up : Vector3.up;
                    break;
                case HonamiUpMode.BoneAxis:
                    upDir = constrainedObject != null
                        ? constrainedObject.rotation * boneUpLocalAxis : Vector3.up;
                    break;
                case HonamiUpMode.World:
                default:
                    upDir = worldUpAxis;
                    break;
            }
            _nativeUpDir[0] = upDir;

            HonamiLookAtTarget[] activeTargets = ResolveActiveTargets(out int count);
            int actualCount = math.min(count, _boundBoneCount);
            EnsureArrays(actualCount);

            for (int i = 0; i < actualCount; i++)
            {
                ref readonly var data = ref activeTargets[i];
                _nativeAimAxes[i] = data.aimAxis;
                _nativeUpAxes[i] = data.upAxis;
                _nativeRotOffsets[i] = data.rotationOffset;
                _nativePosOffsets[i] = data.positionOffset;
                _nativeAxisWeights[i] = new float3(data.weightX, data.weightY, data.weightZ);

                if (UpdateCachedOffsets(i, data.aimAxis, data.upAxis))
                    _nativeCachedOffsets[i] = _cachedAxisOffsets[i];
            }

            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = lerpSpeed;
            _nativeParams[2] = deltaTime;
            _nativeParams[3] = (float)lookAtMode;
            _nativeParams[4] = (float)lookAtType;
            _nativeParams[5] = limitAngle ? 1f : 0f;
            _nativeParams[6] = maxAngle;
            _nativeParams[7] = actualCount;
            _nativeParams[8] = Application.isPlaying ? 1f : 0f;
            _nativeParams[9] = (float)upMode;
            _targetInHierarchy = target != null && target == _boundTarget && _cachedAnimator != null && target.IsChildOf(_cachedAnimator.transform);
            _nativeParams[10] = _targetInHierarchy ? 1f : 0f;
            _nativeParams[11] = eyeTransform != null ? 1f : 0f;
            _nativeParams[12] = minPitchDistance;
            _nativeBoneUpLocalAxis[0] = (float3)boneUpLocalAxis;
            _nativeEyeCenterOffset[0] = (float3)eyeCenterOffset;
        }

        public override void DisposeJobData()
        {
            if (_boneHandles.IsCreated) _boneHandles.Dispose();
            if (_parentHandles.IsCreated) _parentHandles.Dispose();
            if (_nativeTargetPos.IsCreated) _nativeTargetPos.Dispose();
            if (_nativeTargetRot.IsCreated) _nativeTargetRot.Dispose();
            if (_nativeUpDir.IsCreated) _nativeUpDir.Dispose();
            if (_nativeAimAxes.IsCreated) _nativeAimAxes.Dispose();
            if (_nativeUpAxes.IsCreated) _nativeUpAxes.Dispose();
            if (_nativeRotOffsets.IsCreated) _nativeRotOffsets.Dispose();
            if (_nativePosOffsets.IsCreated) _nativePosOffsets.Dispose();
            if (_nativeAxisWeights.IsCreated) _nativeAxisWeights.Dispose();
            if (_nativeCachedOffsets.IsCreated) _nativeCachedOffsets.Dispose();
            if (_nativeSmoothed.IsCreated) _nativeSmoothed.Dispose();
            if (_nativeInitialized.IsCreated) _nativeInitialized.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
            if (_nativeBoneUpLocalAxis.IsCreated) _nativeBoneUpLocalAxis.Dispose();
            if (_nativeEyeCenterOffset.IsCreated) _nativeEyeCenterOffset.Dispose();
        }

        private HonamiLookAtTarget[] ResolveActiveTargets(out int count)
        {
            if (targetSource == HonamiLookAtTargetSource.Avatar)
            {
                count = BuildAvatarTargets();
                return _resolvedAvatarTargets;
            }

            count = targets != null && targets.Length > 0
                ? targetMode == HonamiTargetMode.Single ? 1 : targets.Length
                : 0;
            return targets;
        }

        private int BuildAvatarTargets()
        {
            if (avatarTargets == null || avatarTargets.Length == 0) return 0;

            if (_resolvedAvatarTargets == null || _resolvedAvatarTargets.Length < avatarTargets.Length)
                System.Array.Resize(ref _resolvedAvatarTargets, avatarTargets.Length);

            EnsureAvatarBoneCache();

            int count = 0;
            var span = new ReadOnlySpan<HonamiAvatarLookAtTarget>(avatarTargets);
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var entry = ref span[i];
                if (!entry.enabled) continue;

                Transform bone = _resolvedAvatarBones[i];
                if (bone == null) continue;

                _resolvedAvatarTargets[count++] = new HonamiLookAtTarget
                {
                    transform = bone,
                    aimAxis = entry.aimAxis,
                    upAxis = entry.upAxis,
                    rotationOffset = entry.rotationOffset,
                    positionOffset = entry.positionOffset,
                    weightX = entry.weightX,
                    weightY = entry.weightY,
                    weightZ = entry.weightZ
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

        private Transform ResolveAvatarBone(in HonamiAvatarLookAtTarget entry)
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

        public override void ProcessRig(float deltaTime)
        {
            HonamiLookAtTarget[] activeTargets = targets;
            int count = targets != null && targets.Length > 0
                ? targetMode == HonamiTargetMode.Single ? 1 : targets.Length
                : 0;

            if (targetSource == HonamiLookAtTargetSource.Avatar)
            {
                count = BuildAvatarTargets();
                activeTargets = _resolvedAvatarTargets;
            }

            float safeWeight = Mathf.Clamp01(EffectiveWeight);
            if (target == null || safeWeight <= 0.001f || activeTargets == null || count == 0)
            {
                if (_isInitialized != null)
                {
                    for (int i = 0; i < _isInitialized.Length; i++) _isInitialized[i] = false;
                }
                return;
            }

            EnsureArrays(count);

            var span = new ReadOnlySpan<HonamiLookAtTarget>(activeTargets, 0, count);
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var data = ref span[i];
                if (data.transform == null) continue;

                UpdateCachedOffsets(i, data.aimAxis, data.upAxis);
                ProcessObject(i, in data, deltaTime);
            }
        }

        private void EnsureArrays(int count)
        {
            if (_isInitialized == null || _isInitialized.Length < count)
            {
                int newSize = _isInitialized == null ? count : math.max(count, _isInitialized.Length * 2);
                System.Array.Resize(ref _isInitialized, newSize);
                System.Array.Resize(ref _smoothedRotations, newSize);
                System.Array.Resize(ref _cachedAxisOffsets, newSize);
                System.Array.Resize(ref _lastAimAxes, newSize);
                System.Array.Resize(ref _lastUpAxes, newSize);
            }
        }

        // zero is unreachable as a stored axis, so it marks every entry as needing a recompute
        private void InvalidateAxisOffsetCache()
        {
            if (_lastAimAxes != null) System.Array.Clear(_lastAimAxes, 0, _lastAimAxes.Length);
            if (_lastUpAxes != null) System.Array.Clear(_lastUpAxes, 0, _lastUpAxes.Length);
        }

        private bool UpdateCachedOffsets(int index, Vector3 aim, Vector3 up)
        {
            if (aim.sqrMagnitude < 0.0001f) aim = Vector3.forward;
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;

            if (aim == _lastAimAxes[index] && up == _lastUpAxes[index]) return false;

            _lastAimAxes[index] = aim;
            _lastUpAxes[index] = up;
            Vector3 normAim = aim.normalized;
            Vector3 safeUp = up.normalized;
            if (Vector3.SqrMagnitude(Vector3.Cross(normAim, safeUp)) < 0.0001f)
                safeUp = (Mathf.Abs(normAim.y) < 0.9f) ? Vector3.up : Vector3.forward;
            _cachedAxisOffsets[index] = Quaternion.Inverse(Quaternion.LookRotation(normAim, safeUp));
            return true;
        }

        private void ProcessObject(int index, in HonamiLookAtTarget data, float deltaTime)
        {
            Transform obj = data.transform;
            Quaternion parentRot = obj.parent != null ? obj.parent.rotation : Quaternion.identity;
            Quaternion worldTargetRotation;

            if (lookAtMode == HonamiLookAtMode.LocalRotation)
            {
                Quaternion localPoseRotation = target.localRotation * _cachedAxisOffsets[index] * Quaternion.Euler(data.rotationOffset);
                Quaternion targetLocalRotation = lookAtType == HonamiLookAtType.Additive
                    ? obj.localRotation * localPoseRotation
                    : localPoseRotation;

                worldTargetRotation = parentRot * targetLocalRotation;
            }
            else
            {
                if (!TryGetLookRotation(obj, index, out Quaternion baseLookRotation)) return;
                worldTargetRotation = baseLookRotation * _cachedAxisOffsets[index] * Quaternion.Euler(data.rotationOffset);

                if (lookAtType == HonamiLookAtType.Additive)
                {
                    Vector3 normAim = data.aimAxis.normalized;
                    Vector3 normUp = data.upAxis.normalized;

                    if (Vector3.SqrMagnitude(Vector3.Cross(normAim, normUp)) < 0.0001f)
                        normUp = (Mathf.Abs(normAim.y) < 0.9f) ? Vector3.up : Vector3.forward;

                    Quaternion identityLook = parentRot * Quaternion.LookRotation(normAim, normUp) * _cachedAxisOffsets[index];
                    Quaternion delta = worldTargetRotation * Quaternion.Inverse(identityLook);
                    worldTargetRotation = delta * obj.rotation;
                }
            }

            if (limitAngle)
            {
                float angle = Quaternion.Angle(obj.rotation, worldTargetRotation);
                if (angle > maxAngle)
                    worldTargetRotation = Quaternion.RotateTowards(obj.rotation, worldTargetRotation, maxAngle);
            }

            Quaternion finalRotation;
            if (lerpSpeed > 0f && Application.isPlaying)
            {
                if (!_isInitialized[index])
                {
                    _smoothedRotations[index] = obj.rotation;
                    _isInitialized[index] = true;
                }
                _smoothedRotations[index] = Quaternion.Slerp(_smoothedRotations[index], worldTargetRotation, deltaTime * lerpSpeed);
                finalRotation = _smoothedRotations[index];
            }
            else
            {
                _isInitialized[index] = false;
                finalRotation = worldTargetRotation;
            }

            if (data.weightX < 0.999f || data.weightY < 0.999f || data.weightZ < 0.999f)
            {
                Quaternion invParent = Quaternion.Inverse(parentRot);

                Vector3 currentLocalEuler = (invParent * obj.rotation).eulerAngles;
                Vector3 targetLocalEuler = (invParent * finalRotation).eulerAngles;

                float x = Mathf.LerpAngle(currentLocalEuler.x, targetLocalEuler.x, data.weightX);
                float y = Mathf.LerpAngle(currentLocalEuler.y, targetLocalEuler.y, data.weightY);
                float z = Mathf.LerpAngle(currentLocalEuler.z, targetLocalEuler.z, data.weightZ);

                finalRotation = parentRot * Quaternion.Euler(x, y, z);
            }

            float safeWeight = Mathf.Clamp01(EffectiveWeight);
            obj.rotation = Quaternion.Slerp(obj.rotation, finalRotation, safeWeight);

            if (data.positionOffset.sqrMagnitude > 0.0001f)
                obj.localPosition += data.positionOffset * safeWeight;
        }

        private bool TryGetLookRotation(Transform obj, int index, out Quaternion rotation)
        {
            Vector3 eyePos;
            if (lookAtMode == HonamiLookAtMode.DirectPosition)
            {
                eyePos = eyeTransform != null
                    ? eyeTransform.position + eyeCenterOffset
                    : obj.position + eyeCenterOffset;
            }
            else
            {
                eyePos = eyeTransform != null
                    ? eyeTransform.position + obj.rotation * eyeCenterOffset
                    : obj.position + obj.rotation * eyeCenterOffset;
            }

            Vector3 lookDirection = (lookAtMode == HonamiLookAtMode.Position || lookAtMode == HonamiLookAtMode.DirectPosition)
                ? target.position - eyePos
                : target.forward;

            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            lookDirection.Normalize();

            if (lookAtMode == HonamiLookAtMode.DirectPosition)
            {
                Vector3 rawDir = target.position - eyePos;
                Vector3 adjustedDir = rawDir;

                if (minPitchDistance > 0.0001f)
                {
                    Vector3 horizRaw = new Vector3(rawDir.x, 0f, rawDir.z);
                    float rawHorizLen = horizRaw.magnitude;
                    if (rawHorizLen > 0.0001f && rawHorizLen < minPitchDistance)
                    {
                        float scale = minPitchDistance / rawHorizLen;
                        adjustedDir = new Vector3(rawDir.x * scale, rawDir.y, rawDir.z * scale);
                    }
                }

                adjustedDir.Normalize();
                Vector3 dpUp = Vector3.up;
                if (Vector3.SqrMagnitude(Vector3.Cross(adjustedDir, dpUp)) < 0.0001f)
                    dpUp = Vector3.forward;
                rotation = Quaternion.LookRotation(adjustedDir, dpUp);
                return true;
            }

            Vector3 upDir;
            switch (upMode)
            {
                case HonamiUpMode.Parent:
                    upDir = obj.parent != null ? obj.parent.up : Vector3.up;
                    break;
                case HonamiUpMode.Object:
                    upDir = worldUpObject != null ? worldUpObject.up : Vector3.up;
                    break;
                case HonamiUpMode.Default:
                    upDir = obj.up;
                    break;
                case HonamiUpMode.Target:
                    upDir = target.up;
                    break;
                case HonamiUpMode.BoneAxis:
                    upDir = obj.rotation * boneUpLocalAxis;
                    break;
                case HonamiUpMode.World:
                default:
                    upDir = worldUpAxis;
                    break;
            }

            if (upDir.sqrMagnitude < 0.0001f) upDir = Vector3.up;
            if (Vector3.SqrMagnitude(Vector3.Cross(lookDirection, upDir)) < 0.0001f)
                upDir = (Mathf.Abs(lookDirection.y) < 0.9f) ? Vector3.up : Vector3.forward;

            rotation = Quaternion.LookRotation(lookDirection, upDir);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;

            HonamiLookAtTarget[] targetData = targets;
            int count = targets != null && targets.Length > 0
                ? targetMode == HonamiTargetMode.Single ? 1 : targets.Length
                : 0;

            if (targetSource == HonamiLookAtTargetSource.Avatar)
            {
                count = BuildAvatarTargets();
                targetData = _resolvedAvatarTargets;
            }

            if (targetData == null || count == 0) return;

            for (int i = 0; i < count; i++)
                DrawObjectGizmos(targetData[i]);

            Gizmos.color = Color.yellow * new Color(1, 1, 1, weight);
            Gizmos.DrawWireSphere(target.position, 0.05f);
        }

        private void DrawObjectGizmos(in HonamiLookAtTarget data)
        {
            if (data.transform == null) return;
            Vector3 bonePos = data.transform.position;
            Vector3 eyePos = eyeTransform != null
                ? eyeTransform.position + data.transform.rotation * eyeCenterOffset
                : bonePos + data.transform.rotation * eyeCenterOffset;

            Gizmos.color = Color.yellow * new Color(1, 1, 1, weight);
            Gizmos.DrawLine(eyePos, target.position);

            if (eyeTransform != null || eyeCenterOffset.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(eyePos, 0.02f);
                Gizmos.DrawLine(bonePos, eyePos);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(bonePos, data.transform.TransformDirection(data.aimAxis.normalized) * 0.4f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(bonePos, data.transform.TransformDirection(data.upAxis.normalized) * 0.2f);
        }
#endif
    }
}
