using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiPivotFixMode
    {
        PositionAndRotation,
        PositionOnly,
        RotationOnly
    }

    public enum HonamiBlockMode
    {
        None,
        Static,
        SnapToPivot,
        KeepOffset
    }

    [BurstCompile]
    public struct HonamiPivotFixerJob : IAnimationJob
    {
        public TransformStreamHandle targetHandle;
        public TransformStreamHandle parentHandle;
        public TransformStreamHandle pivotHandle;
        public int hasParent;
        public int hasPivot;

        [ReadOnly] public NativeArray<float3> computedLocalPos;
        [ReadOnly] public NativeArray<quaternion> computedLocalRot;
        
        [ReadOnly] public NativeArray<float3> mainThreadPivotPos;
        [ReadOnly] public NativeArray<quaternion> mainThreadPivotRot;

        [ReadOnly] public NativeArray<float3> positionOffset;
        [ReadOnly] public NativeArray<float3> rotationOffset;

        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamWeight = 0;
        private const int ParamFixMode = 1;
        private const int ParamFixPosX = 2;
        private const int ParamFixPosY = 3;
        private const int ParamFixPosZ = 4;
        private const int ParamFixRotX = 5;
        private const int ParamFixRotY = 6;
        private const int ParamFixRotZ = 7;
        private const int ParamAxisSpace = 8;
        private const int ParamOffsetMult = 9;
        private const int ParamBlockPos = 10;
        private const int ParamBlockRot = 11;

        private static bool IsValid(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        private static bool IsValidRot(Quaternion q) => !float.IsNaN(q.x) && !float.IsNaN(q.y) && !float.IsNaN(q.z) && !float.IsNaN(q.w) && (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f);

        public void ProcessAnimation(AnimationStream stream)
        {
            float w = parameters[ParamWeight];
            if (w <= 0.001f) return;

            if (parameters[ParamBlockPos] > 0.5f)
            {
                Vector3 cp = computedLocalPos[0];
                if (IsValid(cp)) targetHandle.SetLocalPosition(stream, cp);
            }
            
            if (parameters[ParamBlockRot] > 0.5f)
            {
                Quaternion cr = computedLocalRot[0];
                if (IsValidRot(cr)) targetHandle.SetLocalRotation(stream, cr);
            }

            Vector3 wPos = targetHandle.GetPosition(stream);
            Quaternion wRot = targetHandle.GetRotation(stream);

            if (!IsValid(wPos) || !IsValidRot(wRot)) return;

            Vector3 pivotPos;
            Quaternion pivotRot;
            if (hasPivot > 0 && pivotHandle.IsValid(stream))
            {
                pivotPos = pivotHandle.GetPosition(stream);
                pivotRot = pivotHandle.GetRotation(stream);
            }
            else
            {
                pivotPos = mainThreadPivotPos[0];
                pivotRot = mainThreadPivotRot[0];
            }

            Vector3 lPos = Quaternion.Inverse(wRot) * (pivotPos - wPos);
            Quaternion lRot = Quaternion.Inverse(wRot) * pivotRot;

            float mult = parameters[ParamOffsetMult];
            lPos *= mult;
            if (Mathf.Abs(mult - 1f) > 0.001f)
            {
                lRot = Quaternion.SlerpUnclamped(Quaternion.identity, lRot, mult);
            }

            Quaternion pivotFixedRot = wRot * lRot;
            
            int fixMode = (int)parameters[ParamFixMode];
            Quaternion maskedRot = wRot;
            if (fixMode == 0 || fixMode == 2)
            {
                bool fixRotX = parameters[ParamFixRotX] > 0.5f;
                bool fixRotY = parameters[ParamFixRotY] > 0.5f;
                bool fixRotZ = parameters[ParamFixRotZ] > 0.5f;
                
                if (fixRotX && fixRotY && fixRotZ)
                {
                    maskedRot = pivotFixedRot;
                }
                else
                {
                    if (parameters[ParamAxisSpace] == 0f)
                    {
                        Vector3 eulerW = wRot.eulerAngles;
                        Vector3 eulerN = pivotFixedRot.eulerAngles;
                        eulerW.x = fixRotX ? eulerN.x : eulerW.x;
                        eulerW.y = fixRotY ? eulerN.y : eulerW.y;
                        eulerW.z = fixRotZ ? eulerN.z : eulerW.z;
                        maskedRot = Quaternion.Euler(eulerW);
                    }
                    else
                    {
                        Quaternion pRotInv = (hasParent > 0 && parentHandle.IsValid(stream))
                            ? Quaternion.Inverse(parentHandle.GetRotation(stream))
                            : Quaternion.identity;
                        
                        Vector3 localEulerW = (pRotInv * wRot).eulerAngles;
                        Vector3 localEulerN = (pRotInv * pivotFixedRot).eulerAngles;
                        localEulerW.x = fixRotX ? localEulerN.x : localEulerW.x;
                        localEulerW.y = fixRotY ? localEulerN.y : localEulerW.y;
                        localEulerW.z = fixRotZ ? localEulerN.z : localEulerW.z;
                        
                        Quaternion pRot = (hasParent > 0 && parentHandle.IsValid(stream))
                            ? parentHandle.GetRotation(stream)
                            : Quaternion.identity;
                            
                        maskedRot = pRot * Quaternion.Euler(localEulerW);
                    }
                }
            }

            Vector3 maskedPos = wPos;
            if (fixMode == 0 || fixMode == 1)
            {
                Vector3 currentPivotFixedPos = wPos + maskedRot * lPos;
                bool fixPosX = parameters[ParamFixPosX] > 0.5f;
                bool fixPosY = parameters[ParamFixPosY] > 0.5f;
                bool fixPosZ = parameters[ParamFixPosZ] > 0.5f;

                if (fixPosX && fixPosY && fixPosZ)
                {
                    maskedPos = currentPivotFixedPos;
                }
                else
                {
                    if (parameters[ParamAxisSpace] == 0f)
                    {
                        maskedPos.x = fixPosX ? currentPivotFixedPos.x : wPos.x;
                        maskedPos.y = fixPosY ? currentPivotFixedPos.y : wPos.y;
                        maskedPos.z = fixPosZ ? currentPivotFixedPos.z : wPos.z;
                    }
                    else
                    {
                        Vector3 localW = wPos;
                        Vector3 localN = currentPivotFixedPos;
                        if (hasParent > 0 && parentHandle.IsValid(stream))
                        {
                            Vector3 pPos = parentHandle.GetPosition(stream);
                            Quaternion pRotInv = Quaternion.Inverse(parentHandle.GetRotation(stream));
                            localW = pRotInv * (wPos - pPos);
                            localN = pRotInv * (currentPivotFixedPos - pPos);
                        }
                        
                        localW.x = fixPosX ? localN.x : localW.x;
                        localW.y = fixPosY ? localN.y : localW.y;
                        localW.z = fixPosZ ? localN.z : localW.z;
                        
                        if (hasParent > 0 && parentHandle.IsValid(stream))
                        {
                            Vector3 pPos = parentHandle.GetPosition(stream);
                            Quaternion pRot = parentHandle.GetRotation(stream);
                            maskedPos = pPos + pRot * localW;
                        }
                        else
                        {
                            maskedPos = localW;
                        }
                    }
                }
            }

            Quaternion finalRot = maskedRot;
            Vector3 rotOff = rotationOffset[0];
            if (rotOff.sqrMagnitude > 0f)
            {
                Quaternion qOff = Quaternion.Euler(rotOff);
                if (parameters[ParamAxisSpace] == 0f)
                    finalRot = qOff * finalRot;
                else
                    finalRot = finalRot * qOff;
            }

            Vector3 finalPos = maskedPos;
            Vector3 posOff = positionOffset[0];
            if (posOff.sqrMagnitude > 0f)
            {
                if (parameters[ParamAxisSpace] == 0f)
                    finalPos += posOff;
                else
                {
                    Quaternion pRot = (hasParent > 0 && parentHandle.IsValid(stream)) 
                        ? parentHandle.GetRotation(stream) 
                        : Quaternion.identity;
                    finalPos += pRot * posOff;
                }
            }

            if (!IsValid(finalPos) || !IsValidRot(finalRot))
            {
                finalPos = wPos;
                finalRot = wRot;
                if (!IsValid(finalPos) || !IsValidRot(finalRot)) return;
            }

            if (w >= 1f)
            {
                targetHandle.SetPosition(stream, finalPos);
                targetHandle.SetRotation(stream, finalRot);
            }
            else
            {
                targetHandle.SetPosition(stream, Vector3.Lerp(wPos, finalPos, w));
                targetHandle.SetRotation(stream, Quaternion.Slerp(wRot, finalRot, w));
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Honami Pivot Fixer")]
    public sealed class HonamiPivotFixer : HonamiRig, IEarlyInitRig
    {
        [Header("References")]
        public Transform target;
        public Transform pivot;

        [Header("Behavior Settings")]
        public HonamiPivotFixMode fixMode = HonamiPivotFixMode.PositionAndRotation;
        public Space axisSpace = Space.Self;
        public bool resetOnBecomeVisible = true;
        public HonamiBlockMode blockPosition = HonamiBlockMode.None;
        public HonamiBlockMode blockRotation = HonamiBlockMode.None;
        public bool applyAnimationDelta = false;

        [Header("Position Axes Mask")]
        public bool fixPositionX = true;
        public bool fixPositionY = true;
        public bool fixPositionZ = true;

        [Header("Rotation Axes Mask")]
        public bool fixRotationX = true;
        public bool fixRotationY = true;
        public bool fixRotationZ = true;

        [Header("Custom Lock Settings")]
        public bool useCustomLockPosition = false;
        public Vector3 customLockPosition = Vector3.zero;
        public bool useCustomLockRotation = false;
        public Vector3 customLockRotation = Vector3.zero;

        [Header("Manual Offsets")]
        public float offsetMultiplier = 1f;
        public bool invertOffset = false;

        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;

        private Vector3 _animLocalPos;
        private Quaternion _animLocalRot;
        private Vector3 _lastSetLocalPos;
        private Quaternion _lastSetLocalRot;

        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private Vector3 _restPivotOffsetPos;
        private Quaternion _restPivotOffsetRot;

        private bool _restCaptured;
        private bool _initialized;
        private bool _forceReset;
        private bool _needsRebind;

        private AnimationScriptPlayable _playable;
        private NativeArray<float3> _nativeComputedLocalPos;
        private NativeArray<quaternion> _nativeComputedLocalRot;
        private NativeArray<float3> _nativeMainThreadPivotPos;
        private NativeArray<quaternion> _nativeMainThreadPivotRot;
        private NativeArray<float3> _nativePositionOffset;
        private NativeArray<float3> _nativeRotationOffset;
        private NativeArray<float> _nativeParams;

        private static Vector3 Sanitize(Vector3 v, Vector3 fallback)
        {
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                return fallback;
            return v;
        }

        private static Quaternion Sanitize(Quaternion q)
        {
            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) ||
                (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f))
                return Quaternion.identity;
            
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 0.0001f) return Quaternion.identity;
            
            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        private void Awake() => CaptureRestPose();

        private void OnValidate()
        {
            FlushOffsets();
        }

        public void SetOffsets(Vector3 newPositionOffset, Vector3 newRotationOffset)
        {
            positionOffset = newPositionOffset;
            rotationOffset = newRotationOffset;
            FlushOffsets();
        }

        private void FlushOffsets()
        {
            if (_nativePositionOffset.IsCreated)
                _nativePositionOffset[0] = positionOffset;
            if (_nativeRotationOffset.IsCreated)
                _nativeRotationOffset[0] = rotationOffset;

            if (_nativeParams.IsCreated)
            {
                float effectiveWeight = (enabled && gameObject.activeInHierarchy) ? EffectiveWeight : 0f;
                _nativeParams[0] = effectiveWeight;
                _nativeParams[1] = (float)fixMode;
                _nativeParams[2] = fixPositionX ? 1f : 0f;
                _nativeParams[3] = fixPositionY ? 1f : 0f;
                _nativeParams[4] = fixPositionZ ? 1f : 0f;
                _nativeParams[5] = fixRotationX ? 1f : 0f;
                _nativeParams[6] = fixRotationY ? 1f : 0f;
                _nativeParams[7] = fixRotationZ ? 1f : 0f;
                _nativeParams[8] = axisSpace == Space.World ? 0f : 1f;
                _nativeParams[9] = invertOffset ? -offsetMultiplier : offsetMultiplier;
                _nativeParams[10] = blockPosition != HonamiBlockMode.None ? 1f : 0f;
                _nativeParams[11] = blockRotation != HonamiBlockMode.None ? 1f : 0f;
            }

            if (_playable.IsValid())
            {
                var job = _playable.GetJobData<HonamiPivotFixerJob>();
                job.positionOffset = _nativePositionOffset;
                job.rotationOffset = _nativeRotationOffset;
                job.parameters = _nativeParams;
                _playable.SetJobData(job);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _needsRebind = true;
            if (resetOnBecomeVisible) ResetState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetState();
        }

        public void CaptureRestPose()
        {
            if (target == null || pivot == null) return;
            _restLocalPos = Sanitize(target.localPosition, Vector3.zero);
            _restLocalRot = Sanitize(target.localRotation);
            _restPivotOffsetPos = Sanitize(pivot.InverseTransformPoint(target.position), Vector3.zero);
            _restPivotOffsetRot = Sanitize(Quaternion.Inverse(pivot.rotation) * target.rotation);
            _restCaptured = true;
        }

        public void ResetState()
        {
            _initialized = false;
            _forceReset = true;

            if (target != null)
            {
                _lastSetLocalPos = target.localPosition;
                _lastSetLocalRot = target.localRotation;
            }
        }

        public override void ResetRig() => ResetState();

        public void EarlyInit()
        {
            if (target == null || pivot == null) return;

            if (!_restCaptured) CaptureRestPose();

            Transform parent = target.parent;
            Quaternion parentRotInv = parent != null ? Quaternion.Inverse(parent.rotation) : Quaternion.identity;

            Vector3 lockPos = useCustomLockPosition ? customLockPosition : _restLocalPos;
            Quaternion lockRot = useCustomLockRotation ? Quaternion.Euler(customLockRotation) : _restLocalRot;

            if (blockPosition == HonamiBlockMode.None)
            {
                _animLocalPos = Sanitize(target.localPosition, lockPos);
            }
            else if (blockPosition == HonamiBlockMode.Static)
            {
                _animLocalPos = lockPos;
                if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                _animLocalPos = Sanitize(_animLocalPos, lockPos);
            }
            else if (blockPosition == HonamiBlockMode.SnapToPivot)
            {
                _animLocalPos = parent != null ? parent.InverseTransformPoint(pivot.position) : pivot.position;
                if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                _animLocalPos = Sanitize(_animLocalPos, lockPos);
            }
            else if (blockPosition == HonamiBlockMode.KeepOffset)
            {
                Vector3 worldPos = pivot.TransformPoint(_restPivotOffsetPos);
                _animLocalPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
                if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                _animLocalPos = Sanitize(_animLocalPos, lockPos);
            }

            if (blockRotation == HonamiBlockMode.None)
            {
                _animLocalRot = Sanitize(target.localRotation);
            }
            else if (blockRotation == HonamiBlockMode.Static)
            {
                _animLocalRot = lockRot;
                if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                _animLocalRot = Sanitize(_animLocalRot);
            }
            else if (blockRotation == HonamiBlockMode.SnapToPivot)
            {
                _animLocalRot = parentRotInv * pivot.rotation;
                if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                _animLocalRot = Sanitize(_animLocalRot);
            }
            else if (blockRotation == HonamiBlockMode.KeepOffset)
            {
                _animLocalRot = parentRotInv * (pivot.rotation * _restPivotOffsetRot);
                if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                _animLocalRot = Sanitize(_animLocalRot);
            }

            _lastSetLocalPos = _animLocalPos;
            _lastSetLocalRot = _animLocalRot;
            _initialized = true;
            _forceReset = false;
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (target == null || pivot == null) return Playable.Null;

            _nativeComputedLocalPos = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeComputedLocalRot = new NativeArray<quaternion>(1, Allocator.Persistent);
            _nativeMainThreadPivotPos = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeMainThreadPivotRot = new NativeArray<quaternion>(1, Allocator.Persistent);
            _nativePositionOffset = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeRotationOffset = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(12, Allocator.Persistent);

            TransformStreamHandle parentH = default;
            int hasParent = 0;
            if (target.parent != null)
            {
                parentH = animator.BindStreamTransform(target.parent);
                hasParent = 1;
            }

            TransformStreamHandle pivotH = default;
            int hasPivot = 0;
            try 
            {
                pivotH = animator.BindStreamTransform(pivot);
                hasPivot = 1;
            }
            catch 
            {
                // Pivot is likely not in the Animator hierarchy, handle gracefully
                hasPivot = 0;
            }

            var job = new HonamiPivotFixerJob
            {
                targetHandle = animator.BindStreamTransform(target),
                parentHandle = parentH,
                pivotHandle = pivotH,
                hasParent = hasParent,
                hasPivot = hasPivot,
                computedLocalPos = _nativeComputedLocalPos,
                computedLocalRot = _nativeComputedLocalRot,
                mainThreadPivotPos = _nativeMainThreadPivotPos,
                mainThreadPivotRot = _nativeMainThreadPivotRot,
                positionOffset = _nativePositionOffset,
                rotationOffset = _nativeRotationOffset,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || target == null || pivot == null)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                return;
            }

            float effectiveWeight = (enabled && gameObject.activeInHierarchy) ? EffectiveWeight : 0f;
            if (effectiveWeight <= 0f)
            {
                _nativeParams[0] = 0f;
                return;
            }

            if (_needsRebind)
            {
                Animator animator = null;
                Transform p = transform;
                while (p != null)
                {
                    if (p.TryGetComponent(out animator)) break;
                    p = p.parent;
                }
                
                if (animator != null && animator.isInitialized)
                {
                    var job = _playable.GetJobData<HonamiPivotFixerJob>();
                    job.targetHandle = animator.BindStreamTransform(target);
                    if (target.parent != null)
                    {
                        job.parentHandle = animator.BindStreamTransform(target.parent);
                        job.hasParent = 1;
                    }
                    try 
                    {
                        job.pivotHandle = animator.BindStreamTransform(pivot);
                        job.hasPivot = 1;
                    }
                    catch 
                    {
                        job.hasPivot = 0;
                    }
                    _playable.SetJobData(job);
                    _needsRebind = false;
                }
            }

            Transform parent = target.parent;
            Quaternion parentRotInv = parent != null ? Quaternion.Inverse(parent.rotation) : Quaternion.identity;

            if (!_restCaptured) CaptureRestPose();

            Vector3 lockPos = useCustomLockPosition ? customLockPosition : _restLocalPos;
            Quaternion lockRot = useCustomLockRotation ? Quaternion.Euler(customLockRotation) : _restLocalRot;

            if (!_initialized || _forceReset)
            {
                if (blockPosition == HonamiBlockMode.None)
                {
                    _animLocalPos = target.localPosition;
                }
                else if (blockPosition == HonamiBlockMode.Static)
                {
                    _animLocalPos = lockPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(pivot.position) : pivot.position;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.KeepOffset)
                {
                    Vector3 worldPos = pivot.TransformPoint(_restPivotOffsetPos);
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }

                if (blockRotation == HonamiBlockMode.None)
                {
                    _animLocalRot = target.localRotation;
                }
                else if (blockRotation == HonamiBlockMode.Static)
                {
                    _animLocalRot = lockRot;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }
                else if (blockRotation == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalRot = parentRotInv * pivot.rotation;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }
                else if (blockRotation == HonamiBlockMode.KeepOffset)
                {
                    _animLocalRot = parentRotInv * (pivot.rotation * _restPivotOffsetRot);
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }

                _initialized = true;
                _forceReset = false;
            }
            else
            {
                if (blockPosition == HonamiBlockMode.None)
                {
                    float posDiff = (target.localPosition - _lastSetLocalPos).sqrMagnitude;
                    if (posDiff > 1e-6f)
                        _animLocalPos = target.localPosition;
                }
                else if (blockPosition == HonamiBlockMode.Static)
                {
                    _animLocalPos = lockPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(pivot.position) : pivot.position;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.KeepOffset)
                {
                    Vector3 worldPos = pivot.TransformPoint(_restPivotOffsetPos);
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                }

                if (blockRotation == HonamiBlockMode.None)
                {
                    float rotDiff = Quaternion.Angle(target.localRotation, _lastSetLocalRot);
                    if (rotDiff > 0.01f)
                        _animLocalRot = target.localRotation;
                }
                else if (blockRotation == HonamiBlockMode.Static)
                {
                    _animLocalRot = lockRot;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }
                else if (blockRotation == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalRot = parentRotInv * pivot.rotation;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }
                else if (blockRotation == HonamiBlockMode.KeepOffset)
                {
                    _animLocalRot = parentRotInv * (pivot.rotation * _restPivotOffsetRot);
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                }
            }

            _nativeComputedLocalPos[0] = _animLocalPos;
            _nativeComputedLocalRot[0] = _animLocalRot;

            _nativeMainThreadPivotPos[0] = pivot.position;
            _nativeMainThreadPivotRot[0] = pivot.rotation;

            _nativePositionOffset[0] = positionOffset;
            _nativeRotationOffset[0] = rotationOffset;

            _nativeParams[0] = effectiveWeight;
            _nativeParams[1] = (float)fixMode;
            _nativeParams[2] = fixPositionX ? 1f : 0f;
            _nativeParams[3] = fixPositionY ? 1f : 0f;
            _nativeParams[4] = fixPositionZ ? 1f : 0f;
            _nativeParams[5] = fixRotationX ? 1f : 0f;
            _nativeParams[6] = fixRotationY ? 1f : 0f;
            _nativeParams[7] = fixRotationZ ? 1f : 0f;
            _nativeParams[8] = axisSpace == Space.World ? 0f : 1f;
            _nativeParams[9] = invertOffset ? -offsetMultiplier : offsetMultiplier;
            _nativeParams[10] = blockPosition != HonamiBlockMode.None ? 1f : 0f;
            _nativeParams[11] = blockRotation != HonamiBlockMode.None ? 1f : 0f;

            _lastSetLocalPos = _animLocalPos;
            _lastSetLocalRot = _animLocalRot;
        }

        public override void DisposeJobData()
        {
            if (_nativeComputedLocalPos.IsCreated) _nativeComputedLocalPos.Dispose();
            if (_nativeComputedLocalRot.IsCreated) _nativeComputedLocalRot.Dispose();
            if (_nativeMainThreadPivotPos.IsCreated) _nativeMainThreadPivotPos.Dispose();
            if (_nativeMainThreadPivotRot.IsCreated) _nativeMainThreadPivotRot.Dispose();
            if (_nativePositionOffset.IsCreated) _nativePositionOffset.Dispose();
            if (_nativeRotationOffset.IsCreated) _nativeRotationOffset.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        public override void ProcessRig(float deltaTime)
        {
            if (target == null || pivot == null || EffectiveWeight <= 0f) return;

            Transform parent = target.parent;
            Quaternion parentRotInv = parent != null ? Quaternion.Inverse(parent.rotation) : Quaternion.identity;

            if (!_restCaptured) CaptureRestPose();

            Vector3 lockPos = useCustomLockPosition ? Sanitize(customLockPosition, Vector3.zero) : _restLocalPos;
            Quaternion lockRot = useCustomLockRotation ? Sanitize(Quaternion.Euler(customLockRotation)) : _restLocalRot;

            if (!_initialized || _forceReset)
            {
                if (blockPosition == HonamiBlockMode.None)
                {
                    _animLocalPos = Sanitize(target.localPosition, lockPos);
                }
                else if (blockPosition == HonamiBlockMode.Static)
                {
                    _animLocalPos = lockPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, lockPos);
                }
                else if (blockPosition == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(pivot.position) : pivot.position;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, lockPos);
                }
                else if (blockPosition == HonamiBlockMode.KeepOffset)
                {
                    Vector3 worldPos = pivot.TransformPoint(_restPivotOffsetPos);
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, lockPos);
                }

                if (blockRotation == HonamiBlockMode.None)
                {
                    _animLocalRot = Sanitize(target.localRotation);
                }
                else if (blockRotation == HonamiBlockMode.Static)
                {
                    _animLocalRot = lockRot;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }
                else if (blockRotation == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalRot = parentRotInv * pivot.rotation;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }
                else if (blockRotation == HonamiBlockMode.KeepOffset)
                {
                    _animLocalRot = parentRotInv * (pivot.rotation * _restPivotOffsetRot);
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }

                _initialized = true;
                _forceReset = false;
            }
            else
            {
                if (blockPosition == HonamiBlockMode.None)
                {
                    float posDiff = (target.localPosition - _lastSetLocalPos).sqrMagnitude;
                    if (posDiff > 1e-6f)
                        _animLocalPos = Sanitize(target.localPosition, _animLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.Static)
                {
                    _animLocalPos = lockPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, lockPos);
                }
                else if (blockPosition == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(pivot.position) : pivot.position;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, _lastSetLocalPos);
                }
                else if (blockPosition == HonamiBlockMode.KeepOffset)
                {
                    Vector3 worldPos = pivot.TransformPoint(_restPivotOffsetPos);
                    _animLocalPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
                    if (applyAnimationDelta) _animLocalPos += (target.localPosition - _restLocalPos);
                    _animLocalPos = Sanitize(_animLocalPos, _lastSetLocalPos);
                }

                if (blockRotation == HonamiBlockMode.None)
                {
                    float rotDiff = Quaternion.Angle(target.localRotation, _lastSetLocalRot);
                    if (rotDiff > 0.01f)
                        _animLocalRot = Sanitize(target.localRotation);
                }
                else if (blockRotation == HonamiBlockMode.Static)
                {
                    _animLocalRot = lockRot;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }
                else if (blockRotation == HonamiBlockMode.SnapToPivot)
                {
                    _animLocalRot = parentRotInv * pivot.rotation;
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }
                else if (blockRotation == HonamiBlockMode.KeepOffset)
                {
                    _animLocalRot = parentRotInv * (pivot.rotation * _restPivotOffsetRot);
                    if (applyAnimationDelta) _animLocalRot = _animLocalRot * (target.localRotation * Quaternion.Inverse(_restLocalRot));
                    _animLocalRot = Sanitize(_animLocalRot);
                }
            }

            target.localPosition = _animLocalPos;
            target.localRotation = _animLocalRot;

            Vector3 wPos = target.position;
            Quaternion wRot = target.rotation;

            Vector3 lPos = target.InverseTransformPoint(pivot.position);
            Quaternion lRot = Quaternion.Inverse(target.rotation) * pivot.rotation;

            float mult = invertOffset ? -offsetMultiplier : offsetMultiplier;
            lPos *= mult;

            if (Mathf.Abs(mult - 1f) > 0.001f)
                lRot = Quaternion.SlerpUnclamped(Quaternion.identity, lRot, mult);

            Quaternion pivotFixedRot = wRot * lRot;

            Quaternion maskedRot = wRot;
            if (fixMode == HonamiPivotFixMode.PositionAndRotation || fixMode == HonamiPivotFixMode.RotationOnly)
            {
                if (fixRotationX && fixRotationY && fixRotationZ)
                {
                    maskedRot = pivotFixedRot;
                }
                else
                {
                    if (axisSpace == Space.World)
                    {
                        Vector3 eulerW = wRot.eulerAngles;
                        Vector3 eulerN = pivotFixedRot.eulerAngles;
                        eulerW.x = fixRotationX ? eulerN.x : eulerW.x;
                        eulerW.y = fixRotationY ? eulerN.y : eulerW.y;
                        eulerW.z = fixRotationZ ? eulerN.z : eulerW.z;
                        maskedRot = Quaternion.Euler(eulerW);
                    }
                    else
                    {
                        Vector3 localEulerW = (parentRotInv * wRot).eulerAngles;
                        Vector3 localEulerN = (parentRotInv * pivotFixedRot).eulerAngles;
                        localEulerW.x = fixRotationX ? localEulerN.x : localEulerW.x;
                        localEulerW.y = fixRotationY ? localEulerN.y : localEulerW.y;
                        localEulerW.z = fixRotationZ ? localEulerN.z : localEulerW.z;
                        maskedRot = (parent != null ? parent.rotation : Quaternion.identity) * Quaternion.Euler(localEulerW);
                    }
                }
            }

            Vector3 maskedPos = wPos;
            if (fixMode == HonamiPivotFixMode.PositionAndRotation || fixMode == HonamiPivotFixMode.PositionOnly)
            {
                Vector3 currentPivotFixedPos = wPos + maskedRot * lPos;

                if (fixPositionX && fixPositionY && fixPositionZ)
                {
                    maskedPos = currentPivotFixedPos;
                }
                else
                {
                    if (axisSpace == Space.World)
                    {
                        maskedPos.x = fixPositionX ? currentPivotFixedPos.x : wPos.x;
                        maskedPos.y = fixPositionY ? currentPivotFixedPos.y : wPos.y;
                        maskedPos.z = fixPositionZ ? currentPivotFixedPos.z : wPos.z;
                    }
                    else
                    {
                        Vector3 localW = parent != null ? parent.InverseTransformPoint(wPos) : wPos;
                        Vector3 localN = parent != null ? parent.InverseTransformPoint(currentPivotFixedPos) : currentPivotFixedPos;
                        localW.x = fixPositionX ? localN.x : localW.x;
                        localW.y = fixPositionY ? localN.y : localW.y;
                        localW.z = fixPositionZ ? localN.z : localW.z;
                        maskedPos = parent != null ? parent.TransformPoint(localW) : localW;
                    }
                }
            }

            Quaternion finalRot = maskedRot;
            if (rotationOffset.sqrMagnitude > 0f)
            {
                Quaternion rotOff = Quaternion.Euler(rotationOffset);
                if (axisSpace == Space.World) finalRot = rotOff * finalRot;
                else finalRot = finalRot * rotOff;
            }

            Vector3 finalPos = maskedPos;
            if (positionOffset.sqrMagnitude > 0f)
            {
                if (axisSpace == Space.World) finalPos += positionOffset;
                else finalPos += (parent != null ? parent.rotation : Quaternion.identity) * positionOffset;
            }

            if (EffectiveWeight >= 1f)
            {
                target.position = finalPos;
                target.rotation = finalRot;
            }
            else
            {
                target.position = Vector3.Lerp(wPos, finalPos, EffectiveWeight);
                target.rotation = Quaternion.Slerp(wRot, finalRot, EffectiveWeight);
            }

            _lastSetLocalPos = target.localPosition;
            _lastSetLocalRot = target.localRotation;
        }
    }
}
