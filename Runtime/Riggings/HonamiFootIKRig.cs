using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [Serializable]
    public sealed class HonamiFootIKLeg
    {
        public Transform thigh;
        public Transform calf;
        public Transform foot;
        public Transform toes;

        [Space]
        public Vector3 kneeLocalAxis = Vector3.forward;

        [Space]
        [Range(0f, 1f)] public float weightMultiplier = 1f;

        public Transform poleTarget;
        [Range(0f, 1f)] public float poleWeight = 0f;

        [HideInInspector] public bool _hasContact;
        [HideInInspector] public Vector3 _hitPoint;
        [HideInInspector] public Vector3 _hitNormal;

        [HideInInspector] public bool _hasToesContact;
        [HideInInspector] public Vector3 _toesHitPoint;
        [HideInInspector] public Vector3 _toesHitNormal;

        [HideInInspector] public float _currentIKWeight;
        [HideInInspector] public Quaternion _smoothedAnkleRot;
        [HideInInspector] public bool _ankleInitialized;

        [HideInInspector] public Quaternion _smoothedToesRot;
        [HideInInspector] public bool _toesInitialized;

        [HideInInspector] public bool _isPinned;
        [HideInInspector] public Vector3 _pinnedPosition;
    }

    [BurstCompile]
    public struct HonamiFootIKJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> thighHandles;
        public NativeArray<TransformStreamHandle> calfHandles;
        public NativeArray<TransformStreamHandle> footHandles;
        public NativeArray<TransformStreamHandle> toesHandles;
        public NativeArray<int> hasToesHandle;
        public TransformStreamHandle rootBoneHandle;
        public int hasRootBone;

        [ReadOnly] public NativeArray<float3> solvedFootTargets;
        [ReadOnly] public NativeArray<float> legIKWeights;
        [ReadOnly] public NativeArray<float3> poleWorldDirs;
        [ReadOnly] public NativeArray<float3> kneeLocalAxes;
        [ReadOnly] public NativeArray<float> poleWeights;
        [ReadOnly] public NativeArray<quaternion> ankleTargetTilts;
        [ReadOnly] public NativeArray<quaternion> toesTargetDeltas;
        [ReadOnly] public NativeArray<float> ankleEffWeights;
        [ReadOnly] public NativeArray<float> toesEffWeights;

        public NativeArray<quaternion> smoothedAnkleRots;
        public NativeArray<int> ankleInitFlags;
        public NativeArray<quaternion> smoothedToesRots;
        public NativeArray<int> toesInitFlags;

        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamGlobalWeight = 0;
        private const int ParamDeltaTime = 1;
        private const int ParamAnkleRotSpeed = 2;
        private const int ParamIsPlaying = 3;
        private const int ParamLegCount = 4;
        private const int ParamRootBoneOffset = 5;
        private const int ParamRootBoneWeight = 6;

        public void ProcessAnimation(AnimationStream stream)
        {
            float gw = parameters[ParamGlobalWeight];
            if (gw <= 0.001f) return;

            int legCount = (int)parameters[ParamLegCount];
            float dt = parameters[ParamDeltaTime];
            float ankleSpeed = parameters[ParamAnkleRotSpeed];
            bool isPlaying = parameters[ParamIsPlaying] > 0.5f;

            if (hasRootBone > 0)
            {
                float rbOffset = parameters[ParamRootBoneOffset];
                float rbWeight = parameters[ParamRootBoneWeight];
                if (math.abs(rbOffset) > 0.0001f && rbWeight > 0.001f)
                {
                    float3 rootPos = (float3)rootBoneHandle.GetPosition(stream);
                    rootPos.y += rbOffset;
                    rootBoneHandle.SetPosition(stream, (Vector3)rootPos);
                }
            }

            for (int i = 0; i < legCount; i++)
            {
                float ikWeight = legIKWeights[i];
                if (ikWeight <= 0.001f) continue;

                float effectiveWeight = ikWeight * gw;

                quaternion footRot = (quaternion)footHandles[i].GetRotation(stream);
                quaternion toesRot = (hasToesHandle[i] > 0) ? (quaternion)toesHandles[i].GetRotation(stream) : quaternion.identity;

                SolveTwoBoneIK(stream, i, effectiveWeight);

                footHandles[i].SetRotation(stream, (Quaternion)footRot);
                if (hasToesHandle[i] > 0)
                    toesHandles[i].SetRotation(stream, (Quaternion)toesRot);

                RotateAnkle(stream, i, effectiveWeight, dt, ankleSpeed, isPlaying);
                if (hasToesHandle[i] > 0)
                    RotateToes(stream, i, effectiveWeight, dt, ankleSpeed, isPlaying);
            }
        }

        private void SolveTwoBoneIK(AnimationStream stream, int legIdx, float effectiveWeight)
        {
            float3 A = (float3)thighHandles[legIdx].GetPosition(stream);
            float3 B = (float3)calfHandles[legIdx].GetPosition(stream);
            float3 C = (float3)footHandles[legIdx].GetPosition(stream);

            float3 targetWorld = math.lerp(C, solvedFootTargets[legIdx], effectiveWeight);

            float upperLen = math.distance(A, B);
            float lowerLen = math.distance(B, C);
            float totalLen = upperLen + lowerLen;

            float3 rawToTarget = targetWorld - A;
            float rawDist = math.length(rawToTarget);
            if (rawDist < 0.0001f) return;

            float clampedDist = math.clamp(rawDist, math.abs(upperLen - lowerLen) + 0.001f, totalLen - 0.001f);
            float3 targetDir = rawToTarget / rawDist;

            float3 lineDir = math.normalizesafe(targetWorld - A);
            float3 calfHint = B - (A + lineDir * math.dot(B - A, lineDir));

            float3 poleWorld = poleWeights[legIdx] > 0.001f
                ? math.normalizesafe(math.lerp(
                    math.lengthsq(calfHint) > 0.0001f ? math.normalize(calfHint) : new float3(0, 0, 1),
                    poleWorldDirs[legIdx],
                    poleWeights[legIdx]))
                : (math.lengthsq(calfHint) > 0.0001f ? math.normalize(calfHint) : new float3(0, 0, 1));

            float3 bendAxis = math.cross(poleWorld, targetDir);
            if (math.lengthsq(bendAxis) < 0.0001f) bendAxis = math.cross(new float3(0, 1, 0), targetDir);
            if (math.lengthsq(bendAxis) < 0.0001f) bendAxis = math.cross(new float3(1, 0, 0), targetDir);
            bendAxis = math.normalize(bendAxis);

            float cosA = math.clamp(
                (upperLen * upperLen + clampedDist * clampedDist - lowerLen * lowerLen) /
                (2f * upperLen * clampedDist), -1f, 1f);
            float angleA = math.degrees(math.acos(cosA));

            float3 desiredThighDir = math.mul(quaternion.AxisAngle(bendAxis, math.radians(-angleA)), targetDir);
            float3 currentThighDir = math.normalizesafe(B - A);

            quaternion thighDelta = FromToRotation(currentThighDir, desiredThighDir);
            thighHandles[legIdx].SetRotation(stream, (Quaternion)math.mul(thighDelta, (quaternion)thighHandles[legIdx].GetRotation(stream)));

            float3 kneeAxis = kneeLocalAxes[legIdx];
            float3 currentKneeDir = math.mul((quaternion)thighHandles[legIdx].GetRotation(stream), kneeAxis);
            float3 desiredKneeDir = math.normalizesafe(ProjectOnPlane(poleWorld, desiredThighDir));

            if (poleWeights[legIdx] > 0.001f && math.lengthsq(desiredKneeDir) > 0.001f)
            {
                float3 currentKneeInPlane = math.normalizesafe(ProjectOnPlane(currentKneeDir, desiredThighDir));
                if (math.lengthsq(currentKneeInPlane) > 0.001f)
                {
                    float rollAngle = SignedAngle(currentKneeInPlane, desiredKneeDir, desiredThighDir);
                    thighHandles[legIdx].SetRotation(stream,
                        (Quaternion)math.mul(quaternion.AxisAngle(desiredThighDir, math.radians(rollAngle)),
                                 (quaternion)thighHandles[legIdx].GetRotation(stream)));
                }
            }

            float3 newCalfPos = (float3)calfHandles[legIdx].GetPosition(stream);
            float3 currentCalfDir = math.normalizesafe((float3)footHandles[legIdx].GetPosition(stream) - newCalfPos);
            float3 desiredCalfDir = math.normalizesafe(targetWorld - newCalfPos);

            quaternion calfDelta = FromToRotation(currentCalfDir, desiredCalfDir);
            calfHandles[legIdx].SetRotation(stream, (Quaternion)math.mul(calfDelta, (quaternion)calfHandles[legIdx].GetRotation(stream)));
        }

        private void RotateAnkle(AnimationStream stream, int legIdx, float effectiveWeight, float dt, float ankleSpeed, bool isPlaying)
        {
            if (effectiveWeight <= 0.001f) return;

            quaternion targetTilt = ankleTargetTilts[legIdx];
            quaternion footRot = (quaternion)footHandles[legIdx].GetRotation(stream);

            if (ankleSpeed > 0.001f && isPlaying)
            {
                if (ankleInitFlags[legIdx] == 0)
                {
                    smoothedAnkleRots[legIdx] = targetTilt;
                    ankleInitFlags[legIdx] = 1;
                }
                float t = 1f - math.exp(-ankleSpeed * dt);
                smoothedAnkleRots[legIdx] = math.normalize(math.slerp(smoothedAnkleRots[legIdx], targetTilt, t));
                footHandles[legIdx].SetRotation(stream, (Quaternion)math.normalize(math.slerp(footRot, math.mul(smoothedAnkleRots[legIdx], footRot), effectiveWeight)));
            }
            else
            {
                ankleInitFlags[legIdx] = 0;
                footHandles[legIdx].SetRotation(stream, (Quaternion)math.normalize(math.slerp(footRot, math.mul(targetTilt, footRot), effectiveWeight)));
            }
        }

        private void RotateToes(AnimationStream stream, int legIdx, float effectiveWeight, float dt, float ankleSpeed, bool isPlaying)
        {
            if (effectiveWeight <= 0.001f) return;

            quaternion targetDelta = toesTargetDeltas[legIdx];
            quaternion toesRot = (quaternion)toesHandles[legIdx].GetRotation(stream);

            if (ankleSpeed > 0.001f && isPlaying)
            {
                if (toesInitFlags[legIdx] == 0)
                {
                    smoothedToesRots[legIdx] = targetDelta;
                    toesInitFlags[legIdx] = 1;
                }
                float t = 1f - math.exp(-ankleSpeed * dt);
                smoothedToesRots[legIdx] = math.normalize(math.slerp(smoothedToesRots[legIdx], targetDelta, t));
                toesHandles[legIdx].SetRotation(stream, (Quaternion)math.normalize(math.slerp(toesRot, math.mul(smoothedToesRots[legIdx], toesRot), effectiveWeight)));
            }
            else
            {
                toesInitFlags[legIdx] = 0;
                toesHandles[legIdx].SetRotation(stream, (Quaternion)math.normalize(math.slerp(toesRot, math.mul(targetDelta, toesRot), effectiveWeight)));
            }
        }

        private static quaternion FromToRotation(float3 from, float3 to)
        {
            float3 cross = math.cross(from, to);
            float dot = math.dot(from, to);
            if (math.lengthsq(cross) < 1e-8f)
                return dot >= 0 ? quaternion.identity : quaternion.AxisAngle(math.abs(from.x) < 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0), math.PI);
            return math.normalize(new quaternion(cross.x, cross.y, cross.z, 1f + dot));
        }

        private static float3 ProjectOnPlane(float3 vector, float3 planeNormal)
        {
            return vector - planeNormal * math.dot(vector, planeNormal);
        }

        private static float SignedAngle(float3 from, float3 to, float3 axis)
        {
            float angle = math.degrees(math.acos(math.clamp(math.dot(math.normalize(from), math.normalize(to)), -1f, 1f)));
            float sign = math.sign(math.dot(axis, math.cross(from, to)));
            return angle * sign;
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Foot IK")]
    [ExecuteAlways]
    public sealed class HonamiFootIKRig : HonamiRig
    {
        [Header("Legs")]
        public HonamiFootIKLeg[] legs;

        [Header("Root Bone (Optional)")]
        public Transform rootBone;
        [Range(0f, 1f)] public float rootBoneWeight = 1f;
        public float rootBoneAdjustSpeed = 6f;

        [Header("Raycast")]
        public LayerMask groundLayers = ~0;
        public float raycastRadius = 0.05f;
        public float raycastUpOffset = 0.5f;
        public float raycastDistance = 1.2f;
        public float footSurfaceOffset = 0.04f;

        [Header("Blending")]
        public float ikBlendSpeed = 10f;
        public float ankleRotationSpeed = 12f;

        [Header("Sticking")]
        [Range(0f, 1f)] public float horizontalSticking = 0.5f;
        public float footHeightThreshold = 0.15f;
        public float maxStickDistance = 0.15f;
        public float stickReleaseSpeed = 10f;

        private float _rootBoneOffset;
        private float _rootBoneOffsetVelocity;

        private AnimationScriptPlayable _playable;
        private NativeArray<TransformStreamHandle> _thighHandles;
        private NativeArray<TransformStreamHandle> _calfHandles;
        private NativeArray<TransformStreamHandle> _footHandles;
        private NativeArray<TransformStreamHandle> _toesHandles;
        private NativeArray<int> _hasToesHandle;

        private NativeArray<float3> _solvedFootTargets;
        private NativeArray<float> _legIKWeights;
        private NativeArray<float3> _poleWorldDirs;
        private NativeArray<float3> _kneeLocalAxes;
        private NativeArray<float> _poleWeights;
        private NativeArray<quaternion> _ankleTargetTilts;
        private NativeArray<quaternion> _toesTargetDeltas;
        private NativeArray<float> _ankleEffWeights;
        private NativeArray<float> _toesEffWeights;
        private NativeArray<quaternion> _smoothedAnkleRots;
        private NativeArray<int> _ankleInitFlags;
        private NativeArray<quaternion> _smoothedToesRots;
        private NativeArray<int> _toesInitFlags;
        private NativeArray<float> _nativeParams;

        private int _boundLegCount;

        public override void ResetRig()
        {
            if (legs == null) return;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                legs[i]._ankleInitialized = false;
                legs[i]._toesInitialized = false;
                legs[i]._isPinned = false;
                legs[i]._currentIKWeight = 0f;
            }
            _rootBoneOffset = 0f;
            _rootBoneOffsetVelocity = 0f;

            if (_ankleInitFlags.IsCreated)
                for (int i = 0; i < _ankleInitFlags.Length; i++) _ankleInitFlags[i] = 0;
            if (_toesInitFlags.IsCreated)
                for (int i = 0; i < _toesInitFlags.Length; i++) _toesInitFlags[i] = 0;
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (legs == null || legs.Length == 0) return Playable.Null;

            int validCount = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] != null && legs[i].thigh != null && legs[i].calf != null && legs[i].foot != null)
                    validCount++;
            }
            if (validCount == 0) return Playable.Null;

            _boundLegCount = validCount;

            _thighHandles = new NativeArray<TransformStreamHandle>(validCount, Allocator.Persistent);
            _calfHandles = new NativeArray<TransformStreamHandle>(validCount, Allocator.Persistent);
            _footHandles = new NativeArray<TransformStreamHandle>(validCount, Allocator.Persistent);
            _toesHandles = new NativeArray<TransformStreamHandle>(validCount, Allocator.Persistent);
            _hasToesHandle = new NativeArray<int>(validCount, Allocator.Persistent);
            _solvedFootTargets = new NativeArray<float3>(validCount, Allocator.Persistent);
            _legIKWeights = new NativeArray<float>(validCount, Allocator.Persistent);
            _poleWorldDirs = new NativeArray<float3>(validCount, Allocator.Persistent);
            _kneeLocalAxes = new NativeArray<float3>(validCount, Allocator.Persistent);
            _poleWeights = new NativeArray<float>(validCount, Allocator.Persistent);
            _ankleTargetTilts = new NativeArray<quaternion>(validCount, Allocator.Persistent);
            _toesTargetDeltas = new NativeArray<quaternion>(validCount, Allocator.Persistent);
            _ankleEffWeights = new NativeArray<float>(validCount, Allocator.Persistent);
            _toesEffWeights = new NativeArray<float>(validCount, Allocator.Persistent);
            _smoothedAnkleRots = new NativeArray<quaternion>(validCount, Allocator.Persistent);
            _ankleInitFlags = new NativeArray<int>(validCount, Allocator.Persistent);
            _smoothedToesRots = new NativeArray<quaternion>(validCount, Allocator.Persistent);
            _toesInitFlags = new NativeArray<int>(validCount, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(7, Allocator.Persistent);

            int idx = 0;
            TransformStreamHandle rootHandle = default;
            int hasRoot = 0;

            if (rootBone != null)
            {
                rootHandle = animator.BindStreamTransform(rootBone);
                hasRoot = 1;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (leg == null || leg.thigh == null || leg.calf == null || leg.foot == null) continue;

                _thighHandles[idx] = animator.BindStreamTransform(leg.thigh);
                _calfHandles[idx] = animator.BindStreamTransform(leg.calf);
                _footHandles[idx] = animator.BindStreamTransform(leg.foot);

                if (leg.toes != null)
                {
                    _toesHandles[idx] = animator.BindStreamTransform(leg.toes);
                    _hasToesHandle[idx] = 1;
                }
                else
                {
                    _hasToesHandle[idx] = 0;
                }

                _kneeLocalAxes[idx] = leg.kneeLocalAxis;
                idx++;
            }

            var job = new HonamiFootIKJob
            {
                thighHandles = _thighHandles,
                calfHandles = _calfHandles,
                footHandles = _footHandles,
                toesHandles = _toesHandles,
                hasToesHandle = _hasToesHandle,
                rootBoneHandle = rootHandle,
                hasRootBone = hasRoot,
                solvedFootTargets = _solvedFootTargets,
                legIKWeights = _legIKWeights,
                poleWorldDirs = _poleWorldDirs,
                kneeLocalAxes = _kneeLocalAxes,
                poleWeights = _poleWeights,
                ankleTargetTilts = _ankleTargetTilts,
                toesTargetDeltas = _toesTargetDeltas,
                ankleEffWeights = _ankleEffWeights,
                toesEffWeights = _toesEffWeights,
                smoothedAnkleRots = _smoothedAnkleRots,
                ankleInitFlags = _ankleInitFlags,
                smoothedToesRots = _smoothedToesRots,
                toesInitFlags = _toesInitFlags,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || legs == null || legs.Length == 0 || EffectiveWeight <= 0.001f)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                return;
            }
            if (deltaTime <= 0.0001f)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                return;
            }

            int idx = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                if (!IsLegValid(legs[i])) continue;
                CastLeg(legs[i]);
                idx++;
            }

            float lowestAnkleDrop = 0f;
            bool anyLegContact = false;

            idx = 0;
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (!IsLegValid(leg)) continue;

                BlendLegWeight(leg, deltaTime);

                if (leg._currentIKWeight > 0.001f && leg._hasContact)
                {
                    float h = leg._hitPoint.y - leg.foot.position.y;
                    if (!anyLegContact || h < lowestAnkleDrop) lowestAnkleDrop = h;
                    anyLegContact = true;
                }

                float verticalShift = leg._hasContact ? (leg._hitPoint.y - leg.foot.position.y) : 0f;
                Vector3 C = leg.foot.position;
                Vector3 solvedTarget = C + Vector3.up * verticalShift;

                if (horizontalSticking > 0.001f && leg._hasContact && leg._currentIKWeight > 0.1f)
                {
                    float animatedHeight = leg.foot.position.y - (leg._hitPoint.y - footSurfaceOffset);
                    float horizontalDist = leg._isPinned ? Vector2.Distance(new Vector2(leg.foot.position.x, leg.foot.position.z), new Vector2(leg._pinnedPosition.x, leg._pinnedPosition.z)) : 0f;
                    bool shouldStick = animatedHeight < footHeightThreshold && horizontalDist < maxStickDistance;

                    if (shouldStick)
                    {
                        if (!leg._isPinned) { leg._pinnedPosition = solvedTarget; leg._isPinned = true; }
                        else leg._pinnedPosition.y = solvedTarget.y;

                        float edgeFade = 1f - Mathf.Clamp01(horizontalDist / maxStickDistance);
                        float stickBlend = horizontalSticking * edgeFade * leg._currentIKWeight;
                        solvedTarget = Vector3.Lerp(solvedTarget, leg._pinnedPosition, stickBlend);
                    }
                    else if (leg._isPinned)
                    {
                        float releaseT = 1f - Mathf.Exp(-stickReleaseSpeed * 1.5f * deltaTime);
                        leg._pinnedPosition = Vector3.Lerp(leg._pinnedPosition, solvedTarget, releaseT);
                        leg._pinnedPosition.y = solvedTarget.y;
                        solvedTarget = Vector3.Lerp(solvedTarget, leg._pinnedPosition, horizontalSticking * leg._currentIKWeight * 0.1f);
                        if (Vector3.Distance(leg._pinnedPosition, solvedTarget) < 0.02f) leg._isPinned = false;
                    }
                }
                else if (leg._isPinned)
                {
                    float releaseT = 1f - Mathf.Exp(-stickReleaseSpeed * 1.5f * deltaTime);
                    leg._pinnedPosition = Vector3.Lerp(leg._pinnedPosition, solvedTarget, releaseT);
                    leg._pinnedPosition.y = solvedTarget.y;
                    solvedTarget = Vector3.Lerp(solvedTarget, leg._pinnedPosition, horizontalSticking * leg._currentIKWeight * 0.1f);
                    if (Vector3.Distance(leg._pinnedPosition, solvedTarget) < 0.02f) leg._isPinned = false;
                }

                _solvedFootTargets[idx] = solvedTarget;
                _legIKWeights[idx] = leg._currentIKWeight;

                if (leg.poleTarget != null && leg.poleWeight > 0.001f)
                    _poleWorldDirs[idx] = (leg.poleTarget.position - leg.thigh.position).normalized;
                else
                    _poleWorldDirs[idx] = float3.zero;

                _kneeLocalAxes[idx] = leg.kneeLocalAxis;
                _poleWeights[idx] = leg.poleWeight;

                Vector3 footNormal = leg._hasContact ? leg._hitNormal : (leg._hasToesContact ? leg._toesHitNormal : Vector3.up);
                footNormal.Normalize();
                _ankleTargetTilts[idx] = Quaternion.FromToRotation(Vector3.up, footNormal);

                if (leg.toes != null)
                {
                    Vector3 heelN = leg._hasContact ? leg._hitNormal : Vector3.up;
                    Vector3 toeN = leg._hasToesContact ? leg._toesHitNormal : heelN;
                    heelN.Normalize();
                    toeN.Normalize();
                    _toesTargetDeltas[idx] = Quaternion.FromToRotation(heelN, toeN);
                }

                idx++;
            }

            if (rootBone != null && rootBoneWeight > 0.001f)
            {
                float targetDrop = Mathf.Min(anyLegContact ? lowestAnkleDrop : 0f, 0f) * EffectiveWeight * rootBoneWeight;
                float smoothTime = rootBoneAdjustSpeed > 0.001f ? 1f / rootBoneAdjustSpeed : 0.001f;
                _rootBoneOffset = Mathf.SmoothDamp(_rootBoneOffset, targetDrop, ref _rootBoneOffsetVelocity, smoothTime, float.MaxValue, deltaTime);
            }
            else
            {
                _rootBoneOffset = 0f;
            }

            _nativeParams[0] = EffectiveWeight;
            _nativeParams[1] = deltaTime;
            _nativeParams[2] = ankleRotationSpeed;
            _nativeParams[3] = Application.isPlaying ? 1f : 0f;
            _nativeParams[4] = _boundLegCount;
            _nativeParams[5] = _rootBoneOffset;
            _nativeParams[6] = rootBoneWeight;
        }

        public override void DisposeJobData()
        {
            if (_thighHandles.IsCreated) _thighHandles.Dispose();
            if (_calfHandles.IsCreated) _calfHandles.Dispose();
            if (_footHandles.IsCreated) _footHandles.Dispose();
            if (_toesHandles.IsCreated) _toesHandles.Dispose();
            if (_hasToesHandle.IsCreated) _hasToesHandle.Dispose();
            if (_solvedFootTargets.IsCreated) _solvedFootTargets.Dispose();
            if (_legIKWeights.IsCreated) _legIKWeights.Dispose();
            if (_poleWorldDirs.IsCreated) _poleWorldDirs.Dispose();
            if (_kneeLocalAxes.IsCreated) _kneeLocalAxes.Dispose();
            if (_poleWeights.IsCreated) _poleWeights.Dispose();
            if (_ankleTargetTilts.IsCreated) _ankleTargetTilts.Dispose();
            if (_toesTargetDeltas.IsCreated) _toesTargetDeltas.Dispose();
            if (_ankleEffWeights.IsCreated) _ankleEffWeights.Dispose();
            if (_toesEffWeights.IsCreated) _toesEffWeights.Dispose();
            if (_smoothedAnkleRots.IsCreated) _smoothedAnkleRots.Dispose();
            if (_ankleInitFlags.IsCreated) _ankleInitFlags.Dispose();
            if (_smoothedToesRots.IsCreated) _smoothedToesRots.Dispose();
            if (_toesInitFlags.IsCreated) _toesInitFlags.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        private static bool IsLegValid(HonamiFootIKLeg leg)
            => leg != null && leg.thigh != null && leg.calf != null && leg.foot != null;

        private void CastLeg(HonamiFootIKLeg leg)
        {
            if (!IsLegValid(leg)) return;

            Vector3 origin = leg.foot.position + Vector3.up * raycastUpOffset;
            float totalLength = raycastUpOffset + raycastDistance;

            if (raycastRadius > 0.001f)
            {
                leg._hasContact = Physics.SphereCast(
                    origin, raycastRadius, Vector3.down, out RaycastHit hit,
                    totalLength, groundLayers, QueryTriggerInteraction.Ignore);
                if (leg._hasContact) { leg._hitPoint = hit.point + Vector3.up * footSurfaceOffset; leg._hitNormal = hit.normal; }
            }
            else
            {
                leg._hasContact = Physics.Raycast(
                    origin, Vector3.down, out RaycastHit hit,
                    totalLength, groundLayers, QueryTriggerInteraction.Ignore);
                if (leg._hasContact) { leg._hitPoint = hit.point + Vector3.up * footSurfaceOffset; leg._hitNormal = hit.normal; }
            }

            if (!leg._hasContact) { leg._hitPoint = leg.foot.position; leg._hitNormal = Vector3.up; }

            if (leg.toes != null)
            {
                Vector3 toesOrigin = leg.toes.position + Vector3.up * raycastUpOffset;
                if (raycastRadius > 0.001f)
                {
                    leg._hasToesContact = Physics.SphereCast(
                        toesOrigin, raycastRadius, Vector3.down, out RaycastHit toesHit,
                        totalLength, groundLayers, QueryTriggerInteraction.Ignore);
                    if (leg._hasToesContact) { leg._toesHitPoint = toesHit.point + Vector3.up * footSurfaceOffset; leg._toesHitNormal = toesHit.normal; }
                }
                else
                {
                    leg._hasToesContact = Physics.Raycast(
                        toesOrigin, Vector3.down, out RaycastHit toesHit,
                        totalLength, groundLayers, QueryTriggerInteraction.Ignore);
                    if (leg._hasToesContact) { leg._toesHitPoint = toesHit.point + Vector3.up * footSurfaceOffset; leg._toesHitNormal = toesHit.normal; }
                }
                if (!leg._hasToesContact) { leg._toesHitPoint = leg.toes.position; leg._toesHitNormal = Vector3.up; }
            }
        }

        private void BlendLegWeight(HonamiFootIKLeg leg, float deltaTime)
        {
            float t = leg._hasContact ? leg.weightMultiplier : 0f;
            leg._currentIKWeight = ikBlendSpeed > 0.001f
                ? Mathf.MoveTowards(leg._currentIKWeight, t, ikBlendSpeed * deltaTime)
                : t;
        }

        public override void ProcessRig(float deltaTime)
        {
            if (!this || legs == null || legs.Length == 0 || EffectiveWeight <= 0.001f) return;
            if (deltaTime <= 0.0001f) return;

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                CastLeg(legs[i]);
            }

            float lowestAnkleDrop = 0f;
            bool anyLegContact = false;

            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (!IsLegValid(leg)) continue;

                BlendLegWeight(leg, deltaTime);

                if (leg._currentIKWeight > 0.001f && leg._hasContact)
                {
                    float h = leg._hitPoint.y - leg.foot.position.y;
                    if (!anyLegContact || h < lowestAnkleDrop) lowestAnkleDrop = h;
                    anyLegContact = true;
                }
            }

            if (rootBone != null && rootBoneWeight > 0.001f)
            {
                float targetDrop = Mathf.Min(anyLegContact ? lowestAnkleDrop : 0f, 0f) * EffectiveWeight * rootBoneWeight;
                float smoothTime = rootBoneAdjustSpeed > 0.001f ? 1f / rootBoneAdjustSpeed : 0.001f;
                _rootBoneOffset = Mathf.SmoothDamp(_rootBoneOffset, targetDrop, ref _rootBoneOffsetVelocity, smoothTime, float.MaxValue, deltaTime);
                rootBone.position += Vector3.up * _rootBoneOffset;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (!IsLegValid(leg) || leg._currentIKWeight <= 0.001f) continue;

                Quaternion footRot = leg.foot.rotation;
                Quaternion toesRot = leg.toes != null ? leg.toes.rotation : Quaternion.identity;

                SolveTwoBoneIKOnTransforms(leg, deltaTime);

                leg.foot.rotation = footRot;
                if (leg.toes != null) leg.toes.rotation = toesRot;

                RotateAnkleOnTransforms(leg, deltaTime);
                RotateToesOnTransforms(leg, deltaTime);
            }
        }

        private void SolveTwoBoneIKOnTransforms(HonamiFootIKLeg leg, float deltaTime)
        {
            float effectiveWeight = leg._currentIKWeight * EffectiveWeight;
            Vector3 A = leg.thigh.position, B = leg.calf.position, C = leg.foot.position;
            float verticalShift = leg._hasContact ? (leg._hitPoint.y - leg.foot.position.y) : 0f;
            Vector3 solvedTarget = C + Vector3.up * verticalShift;
            Vector3 targetWorld = Vector3.Lerp(C, solvedTarget, effectiveWeight);

            float upperLen = Vector3.Distance(A, B);
            float lowerLen = Vector3.Distance(B, C);
            float totalLen = upperLen + lowerLen;
            Vector3 rawToTarget = targetWorld - A;
            float rawDist = rawToTarget.magnitude;
            if (rawDist < 0.0001f) return;

            float clampedDist = Mathf.Clamp(rawDist, Mathf.Abs(upperLen - lowerLen) + 0.001f, totalLen - 0.001f);
            Vector3 targetDir = rawToTarget / rawDist;
            Vector3 lineDir = (targetWorld - A).normalized;
            Vector3 calfHint = B - (A + lineDir * Vector3.Dot(B - A, lineDir));
            Vector3 characterForward = transform.forward;
            Vector3 poleWorld;

            if (leg.poleTarget != null && leg.poleWeight > 0.001f)
            {
                Vector3 toPole = (leg.poleTarget.position - A).normalized;
                poleWorld = Vector3.Lerp(calfHint.sqrMagnitude > 0.0001f ? calfHint.normalized : characterForward, toPole, leg.poleWeight).normalized;
            }
            else
                poleWorld = calfHint.sqrMagnitude > 0.0001f ? calfHint.normalized : characterForward;

            Vector3 bendAxis = Vector3.Cross(poleWorld, targetDir);
            if (bendAxis.sqrMagnitude < 0.0001f) bendAxis = Vector3.Cross(Vector3.up, targetDir);
            if (bendAxis.sqrMagnitude < 0.0001f) bendAxis = Vector3.Cross(Vector3.right, targetDir);
            bendAxis.Normalize();

            float cosA = Mathf.Clamp((upperLen * upperLen + clampedDist * clampedDist - lowerLen * lowerLen) / (2f * upperLen * clampedDist), -1f, 1f);
            float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;

            Vector3 desiredThighDir = Quaternion.AngleAxis(-angleA, bendAxis) * targetDir;
            Vector3 currentThighDir = (B - A).normalized;
            Quaternion thighDelta = Quaternion.FromToRotation(currentThighDir, desiredThighDir);
            leg.thigh.rotation = thighDelta * leg.thigh.rotation;

            Vector3 currentKneeDir = leg.thigh.TransformDirection(leg.kneeLocalAxis);
            Vector3 desiredKneeDir = Vector3.ProjectOnPlane(poleWorld, desiredThighDir).normalized;

            if (leg.poleWeight > 0.001f && desiredKneeDir.sqrMagnitude > 0.001f)
            {
                Vector3 currentKneeInPlane = Vector3.ProjectOnPlane(currentKneeDir, desiredThighDir).normalized;
                if (currentKneeInPlane.sqrMagnitude > 0.001f)
                {
                    float rollAngle = Vector3.SignedAngle(currentKneeInPlane, desiredKneeDir, desiredThighDir);
                    leg.thigh.rotation = Quaternion.AngleAxis(rollAngle, desiredThighDir) * leg.thigh.rotation;
                }
            }

            Vector3 newCalfPos = leg.calf.position;
            Vector3 currentCalfDir = (leg.foot.position - newCalfPos).normalized;
            Vector3 desiredCalfDir = (targetWorld - newCalfPos).normalized;
            Quaternion calfDelta = Quaternion.FromToRotation(currentCalfDir, desiredCalfDir);
            leg.calf.rotation = calfDelta * leg.calf.rotation;
        }

        private void RotateAnkleOnTransforms(HonamiFootIKLeg leg, float deltaTime)
        {
            float effectiveWeight = leg._currentIKWeight * EffectiveWeight;
            if (effectiveWeight <= 0.001f) return;
            Vector3 normal = leg._hasContact ? leg._hitNormal : (leg._hasToesContact ? leg._toesHitNormal : Vector3.up);
            normal.Normalize();
            Quaternion targetTilt = Quaternion.FromToRotation(Vector3.up, normal);
            if (ankleRotationSpeed > 0.001f && Application.isPlaying)
            {
                if (!leg._ankleInitialized) { leg._smoothedAnkleRot = targetTilt; leg._ankleInitialized = true; }
                float t = 1f - Mathf.Exp(-ankleRotationSpeed * deltaTime);
                leg._smoothedAnkleRot = Quaternion.Normalize(Quaternion.Slerp(leg._smoothedAnkleRot, targetTilt, t));
                leg.foot.rotation = Quaternion.Normalize(Quaternion.Slerp(leg.foot.rotation, leg._smoothedAnkleRot * leg.foot.rotation, effectiveWeight));
            }
            else
            {
                leg._ankleInitialized = false;
                leg.foot.rotation = Quaternion.Normalize(Quaternion.Slerp(leg.foot.rotation, targetTilt * leg.foot.rotation, effectiveWeight));
            }
        }

        private void RotateToesOnTransforms(HonamiFootIKLeg leg, float deltaTime)
        {
            if (leg.toes == null) return;
            float effectiveWeight = leg._currentIKWeight * EffectiveWeight;
            if (effectiveWeight <= 0.001f) return;
            Vector3 heelNormal = leg._hasContact ? leg._hitNormal : Vector3.up;
            Vector3 toeNormal = leg._hasToesContact ? leg._toesHitNormal : heelNormal;
            heelNormal.Normalize();
            toeNormal.Normalize();
            Quaternion targetDelta = Quaternion.FromToRotation(heelNormal, toeNormal);
            if (ankleRotationSpeed > 0.001f && Application.isPlaying)
            {
                if (!leg._toesInitialized) { leg._smoothedToesRot = targetDelta; leg._toesInitialized = true; }
                float t = 1f - Mathf.Exp(-ankleRotationSpeed * deltaTime);
                leg._smoothedToesRot = Quaternion.Normalize(Quaternion.Slerp(leg._smoothedToesRot, targetDelta, t));
                leg.toes.rotation = Quaternion.Normalize(Quaternion.Slerp(leg.toes.rotation, leg._smoothedToesRot * leg.toes.rotation, effectiveWeight));
            }
            else
            {
                leg._toesInitialized = false;
                leg.toes.rotation = Quaternion.Normalize(Quaternion.Slerp(leg.toes.rotation, targetDelta * leg.toes.rotation, effectiveWeight));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!this || legs == null) return;

            foreach (var leg in legs)
            {
                if (leg == null || leg.foot == null) continue;

                Vector3 origin = leg.foot.position + Vector3.up * raycastUpOffset;
                Gizmos.color = leg._hasContact ? Color.green : Color.red;
                Gizmos.DrawLine(origin, origin + Vector3.down * (raycastUpOffset + raycastDistance));
                if (raycastRadius > 0.001f)
                    Gizmos.DrawWireSphere(origin + Vector3.down * (raycastUpOffset + raycastDistance), raycastRadius);

                if (leg.toes != null)
                {
                    Vector3 toesOrigin = leg.toes.position + Vector3.up * raycastUpOffset;
                    Gizmos.color = leg._hasToesContact ? Color.green : Color.red;
                    Gizmos.DrawLine(toesOrigin, toesOrigin + Vector3.down * (raycastUpOffset + raycastDistance));
                    if (raycastRadius > 0.001f)
                        Gizmos.DrawWireSphere(toesOrigin + Vector3.down * (raycastUpOffset + raycastDistance), raycastRadius);
                }

                if (leg._hasContact)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.85f);
                    Gizmos.DrawWireSphere(leg._hitPoint, 0.04f);
                    Gizmos.DrawRay(leg._hitPoint, leg._hitNormal * 0.15f);
                }

                if (leg.thigh != null && leg.calf != null)
                {
                    Gizmos.color = new Color(0f, 1f, 1f, weight);
                    Gizmos.DrawLine(leg.thigh.position, leg.calf.position);
                    Gizmos.DrawLine(leg.calf.position, leg.foot.position);
                    Gizmos.DrawWireSphere(leg.thigh.position, 0.025f);
                    Gizmos.DrawWireSphere(leg.calf.position, 0.025f);
                    Gizmos.DrawWireSphere(leg.foot.position, 0.025f);
                }
            }

            if (rootBone != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(rootBone.position, 0.05f);
            }
        }
#endif
    }
}
