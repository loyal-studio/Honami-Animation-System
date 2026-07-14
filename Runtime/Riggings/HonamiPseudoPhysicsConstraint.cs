using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiPhysicsSimulationSpace
    {
        BoneLocal,
        World
    }

    [Serializable]
    public sealed class HonamiPhysicsBoneData
    {
        public Transform bone;
        [Range(0f, 1f)] public float weightMultiplier = 1f;

        internal Vector3 currentPosOffset;
        internal Vector3 currentPosVelocity;
        internal Vector3 currentRotOffset;
        internal Vector3 currentRotVelocity;

        internal Vector3 lastWorldPos;
        internal Quaternion lastWorldRot;
        internal Vector3 lastVelocity;
        internal Vector3 lastAngularVelocity;

        internal Vector3 appliedPosOffset;
        internal Vector3 appliedRotOffset;
        internal Vector3 lastWrittenLocalPos;
        internal Quaternion lastWrittenLocalRot;
        internal bool hasAppliedOffsets;
        internal bool appliedInWorldSpace;
    }

    [BurstCompile]
    public struct HonamiPseudoPhysicsJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> boneHandles;

        [ReadOnly] public NativeArray<float3> positionOffsets;
        [ReadOnly] public NativeArray<float3> rotationOffsets;
        [ReadOnly] public NativeArray<float> boneWeights;
        [ReadOnly] public NativeArray<float3> posAxisMask;
        [ReadOnly] public NativeArray<float3> rotAxisMask;
        [ReadOnly] public NativeArray<float> parameters;

        private const int ParamGlobalWeight = 0;
        private const int ParamBoneCount = 1;
        private const int ParamWorldSpace = 2;

        public void ProcessAnimation(AnimationStream stream)
        {
            float gw = parameters[ParamGlobalWeight];
            if (gw <= 0.001f) return;

            int count = (int)parameters[ParamBoneCount];
            bool worldSpace = parameters[ParamWorldSpace] > 0.5f;
            float3 posMask = posAxisMask[0];
            float3 rotMask = rotAxisMask[0];

            for (int i = 0; i < count; i++)
            {
                float w = boneWeights[i] * gw;
                if (w <= 0.001f) continue;

                float3 finalPosOffset = positionOffsets[i] * posMask * w;
                float3 finalRotOffset = rotationOffsets[i] * rotMask * w;

                float3 currentPos = (float3)boneHandles[i].GetPosition(stream);
                quaternion currentRot = (quaternion)boneHandles[i].GetRotation(stream);

                if (worldSpace)
                {
                    boneHandles[i].SetPosition(stream, (Vector3)(currentPos + finalPosOffset));
                    boneHandles[i].SetRotation(stream, (Quaternion)math.mul(quaternion.Euler(math.radians(finalRotOffset)), currentRot));
                }
                else
                {
                    boneHandles[i].SetPosition(stream, (Vector3)(currentPos + math.mul(currentRot, finalPosOffset)));
                    boneHandles[i].SetRotation(stream, (Quaternion)math.mul(currentRot, quaternion.Euler(math.radians(finalRotOffset))));
                }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    [AddComponentMenu("Honami Animation/Riggings/Honami Pseudo-Physics Constraint")]
    [ExecuteAlways]
    public sealed class HonamiPseudoPhysicsConstraint : HonamiRig
    {
        [Header("Target Bones")]
        public HonamiPhysicsBoneData[] bones;

        [Header("Simulation Space")]
        [Tooltip("BoneLocal: offsets rotate with the bone — jiggle that sticks to the animation (chest, hair). World: offsets and gravity stay world-aligned — dangling items that sag down and swing (keychains, pouches, pendants).")]
        public HonamiPhysicsSimulationSpace simulationSpace = HonamiPhysicsSimulationSpace.BoneLocal;

        [Header("Axis Setup")]
        [Tooltip("Masks are applied in the selected simulation space.")]
        public Vector3 positionAxisMask = Vector3.one;
        public Vector3 rotationAxisMask = Vector3.one;

        [Header("Position Physics")]
        public Vector3 positionDrag = new Vector3(0.05f, 0.05f, 0.05f);
        public Vector3 positionInertia = new Vector3(0.1f, 0.1f, 0.1f);
        public float positionStiffness = 50f;
        public float positionDamping = 5f;
        public Vector3 maxPositionOffset = new Vector3(0.5f, 0.5f, 0.5f);
        [Tooltip("Constant force in the simulation space. In World space, (0, -y, 0) makes bones sag toward the ground no matter how the parent rotates.")]
        public Vector3 positionGravity = Vector3.zero;

        [Header("Rotation Physics")]
        public Vector3 rotationDrag = new Vector3(2f, 2f, 2f);
        public Vector3 rotationInertia = new Vector3(5f, 5f, 5f);
        public float rotationStiffness = 50f;
        public float rotationDamping = 5f;
        public Vector3 maxRotationOffset = new Vector3(45f, 45f, 45f);

        [Header("Rotation Gravity (Pendulum)")]
        [Tooltip("Torque (deg/s²) pulling Hang Axis toward Gravity Direction, scaled by the sine of the misalignment. 0 disables. It fights rotationStiffness: equilibrium tilt ≈ torque / stiffness, so lower the stiffness or raise the torque until the bone actually hangs.")]
        public float rotationGravity = 0f;
        [Tooltip("Bone-local axis that should point along Gravity Direction — the axis running from the attachment point toward the dangling tip.")]
        public Vector3 rotationHangAxis = Vector3.down;
        [Tooltip("World-space direction the Hang Axis is pulled toward.")]
        public Vector3 rotationGravityDirection = Vector3.down;

        [Header("Cross Effects")]
        public Vector3 movementToRotation = Vector3.zero;

        [Header("Stability")]
        [Tooltip("Animated-pose jump (meters per frame) treated as a teleport: physics state resets instead of receiving a huge impulse. 0 disables.")]
        public float teleportDistanceThreshold = 0.75f;
        [Tooltip("Animated-pose jump (degrees per frame) treated as a teleport: physics state resets instead of receiving a huge impulse. 0 disables.")]
        public float teleportAngleThreshold = 90f;

        [Header("Editor Preview")]
        [Tooltip("Runs the simulation in edit mode so it can be tested without entering Play mode: move the object or bones in the Scene view and watch the response. Turning it off restores the animated pose.")]
        public bool simulateInEditMode;

        private const float MaxSpringSubstep = 1f / 120f;
        private const int MaxSpringSubstepCount = 8;

        private bool _isInitialized;

#if UNITY_EDITOR
        private const float MaxEditorDeltaTime = 1f / 20f;
        private double _lastEditorTime;
#endif

        private AnimationScriptPlayable _playable;
        private NativeArray<TransformStreamHandle> _boneHandles;
        private NativeArray<float3> _nativePosOffsets;
        private NativeArray<float3> _nativeRotOffsets;
        private NativeArray<float> _nativeBoneWeights;
        private NativeArray<float3> _nativePosMask;
        private NativeArray<float3> _nativeRotMask;
        private NativeArray<float> _nativeParams;
        private int _boundBoneCount;

        public override void ResetRig()
        {
            _isInitialized = false;
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            if (bones == null || bones.Length == 0) return Playable.Null;

            int validCount = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].bone != null) validCount++;
            }
            if (validCount == 0) return Playable.Null;

            _boundBoneCount = validCount;

            _boneHandles = new NativeArray<TransformStreamHandle>(validCount, Allocator.Persistent);
            _nativePosOffsets = new NativeArray<float3>(validCount, Allocator.Persistent);
            _nativeRotOffsets = new NativeArray<float3>(validCount, Allocator.Persistent);
            _nativeBoneWeights = new NativeArray<float>(validCount, Allocator.Persistent);
            _nativePosMask = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeRotMask = new NativeArray<float3>(1, Allocator.Persistent);
            _nativeParams = new NativeArray<float>(3, Allocator.Persistent);

            int idx = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || bones[i].bone == null) continue;
                _boneHandles[idx] = animator.BindStreamTransform(bones[i].bone);
                idx++;
            }

            var job = new HonamiPseudoPhysicsJob
            {
                boneHandles = _boneHandles,
                positionOffsets = _nativePosOffsets,
                rotationOffsets = _nativeRotOffsets,
                boneWeights = _nativeBoneWeights,
                posAxisMask = _nativePosMask,
                rotAxisMask = _nativeRotMask,
                parameters = _nativeParams
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            return _playable;
        }

        public override void PrepareJobData(float deltaTime)
        {
            if (!_playable.IsValid() || !Application.isPlaying
                || bones == null || bones.Length == 0 || weight <= 0.001f)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                _isInitialized = false;
                return;
            }

            // keep last frame's offsets applied on paused frames; zeroing the weight pops the bone for one frame
            if (deltaTime <= 0.0001f) return;

            float dt = deltaTime;

            if (!_isInitialized)
            {
                InitializeBones();
                _isInitialized = true;
            }

            _nativePosMask[0] = positionAxisMask;
            _nativeRotMask[0] = rotationAxisMask;

            int idx = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null) continue;

                b.bone.GetPositionAndRotation(out Vector3 currentPos, out Quaternion currentRot);

                Vector3 animPos = currentPos;
                Quaternion animRot = currentRot;
                if (b.hasAppliedOffsets)
                {
                    // the transform still holds last frame's applied offset; recover the pure animated
                    // pose so velocity estimation never feeds on the physics' own output
                    UnapplyOffsets(b, currentPos, currentRot, out animPos, out animRot);
                    // unanimated bones keep the scene pose as the stream default, so restore the pure
                    // pose before evaluation or the job's offset accumulates every frame
                    b.bone.SetPositionAndRotation(animPos, animRot);
                }

                StepBonePhysics(b, animPos, animRot, dt);

                float w = b.weightMultiplier * weight;
                if (w > 0.001f)
                {
                    b.appliedPosOffset = Vector3.Scale(b.currentPosOffset, positionAxisMask) * w;
                    b.appliedRotOffset = Vector3.Scale(b.currentRotOffset, rotationAxisMask) * w;
                    b.hasAppliedOffsets = true;
                    b.appliedInWorldSpace = simulationSpace == HonamiPhysicsSimulationSpace.World;
                }
                else
                {
                    b.appliedPosOffset = Vector3.zero;
                    b.appliedRotOffset = Vector3.zero;
                    b.hasAppliedOffsets = false;
                }

                _nativePosOffsets[idx] = b.currentPosOffset;
                _nativeRotOffsets[idx] = b.currentRotOffset;
                _nativeBoneWeights[idx] = b.weightMultiplier;

                idx++;
            }

            _nativeParams[0] = weight;
            _nativeParams[1] = _boundBoneCount;
            _nativeParams[2] = simulationSpace == HonamiPhysicsSimulationSpace.World ? 1f : 0f;
        }

        public override void DisposeJobData()
        {
            if (_boneHandles.IsCreated) _boneHandles.Dispose();
            if (_nativePosOffsets.IsCreated) _nativePosOffsets.Dispose();
            if (_nativeRotOffsets.IsCreated) _nativeRotOffsets.Dispose();
            if (_nativeBoneWeights.IsCreated) _nativeBoneWeights.Dispose();
            if (_nativePosMask.IsCreated) _nativePosMask.Dispose();
            if (_nativeRotMask.IsCreated) _nativeRotMask.Dispose();
            if (_nativeParams.IsCreated) _nativeParams.Dispose();
        }

        private void InitializeBones()
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null) continue;
                b.bone.GetPositionAndRotation(out b.lastWorldPos, out b.lastWorldRot);
                b.lastVelocity = Vector3.zero;
                b.lastAngularVelocity = Vector3.zero;
                b.currentPosOffset = Vector3.zero;
                b.currentPosVelocity = Vector3.zero;
                b.currentRotOffset = Vector3.zero;
                b.currentRotVelocity = Vector3.zero;
                b.appliedPosOffset = Vector3.zero;
                b.appliedRotOffset = Vector3.zero;
                b.hasAppliedOffsets = false;
                b.appliedInWorldSpace = false;
            }
        }

        private void StepBonePhysics(HonamiPhysicsBoneData b, Vector3 animPos, Quaternion animRot, float dt)
        {
            Vector3 deltaPos = animPos - b.lastWorldPos;
            Quaternion deltaRot = animRot * Quaternion.Inverse(b.lastWorldRot);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            bool teleported =
                (teleportDistanceThreshold > 0f && deltaPos.sqrMagnitude > teleportDistanceThreshold * teleportDistanceThreshold)
                || (teleportAngleThreshold > 0f && Mathf.Abs(angle) > teleportAngleThreshold);
            if (teleported)
            {
                b.lastWorldPos = animPos;
                b.lastWorldRot = animRot;
                b.lastVelocity = Vector3.zero;
                b.lastAngularVelocity = Vector3.zero;
                return;
            }

            Vector3 velocity = deltaPos / dt;
            Vector3 acceleration = (velocity - b.lastVelocity) / dt;

            Vector3 angularVelocity = Vector3.zero;
            if (Mathf.Abs(angle) > 1e-4f && !float.IsInfinity(axis.x) && axis.sqrMagnitude > 1e-6f)
                angularVelocity = axis.normalized * (angle / dt);
            Vector3 angularAcceleration = (angularVelocity - b.lastAngularVelocity) / dt;

            Vector3 simVelocity = velocity;
            Vector3 simAcceleration = acceleration;
            Vector3 simAngVelocity = angularVelocity;
            Vector3 simAngAcceleration = angularAcceleration;
            if (simulationSpace == HonamiPhysicsSimulationSpace.BoneLocal)
            {
                Quaternion invRot = Quaternion.Inverse(animRot);
                simVelocity = invRot * velocity;
                simAcceleration = invRot * acceleration;
                simAngVelocity = invRot * angularVelocity;
                simAngAcceleration = invRot * angularAcceleration;
            }

            b.currentPosOffset -= Vector3.Scale(simVelocity, positionDrag) * dt;
            b.currentPosVelocity -= Vector3.Scale(simAcceleration, positionInertia) * dt;
            b.currentRotOffset -= Vector3.Scale(simAngVelocity, rotationDrag) * dt;
            b.currentRotVelocity -= Vector3.Scale(simAngAcceleration, rotationInertia) * dt;
            b.currentRotOffset -= Vector3.Scale(simVelocity, movementToRotation) * dt;

            IntegrateSpring(ref b.currentPosOffset, ref b.currentPosVelocity,
                positionStiffness, positionDamping, positionGravity, maxPositionOffset, dt);
            IntegrateSpring(ref b.currentRotOffset, ref b.currentRotVelocity,
                rotationStiffness, rotationDamping, ComputeRotationGravityTorque(b, animRot), maxRotationOffset, dt);

            b.lastWorldPos = animPos;
            b.lastWorldRot = animRot;
            b.lastVelocity = velocity;
            b.lastAngularVelocity = angularVelocity;
        }

        private Vector3 ComputeRotationGravityTorque(HonamiPhysicsBoneData b, Quaternion animRot)
        {
            if (rotationGravity == 0f) return Vector3.zero;

            Vector3 hangAxis = rotationHangAxis;
            Vector3 gravityDir = rotationGravityDirection;
            if (hangAxis.sqrMagnitude < 1e-6f || gravityDir.sqrMagnitude < 1e-6f) return Vector3.zero;

            bool worldSpace = simulationSpace == HonamiPhysicsSimulationSpace.World;
            Quaternion offsetRot = Quaternion.Euler(b.currentRotOffset);
            Quaternion effectiveRot = worldSpace ? offsetRot * animRot : animRot * offsetRot;

            Vector3 worldHang = effectiveRot * hangAxis.normalized;
            // cross magnitude is sin(misalignment): real pendulum torque, zero when hanging straight
            Vector3 torque = Vector3.Cross(worldHang, gravityDir.normalized) * rotationGravity;
            return worldSpace ? torque : Quaternion.Inverse(animRot) * torque;
        }

        private static void IntegrateSpring(ref Vector3 offset, ref Vector3 velocity,
            float stiffness, float damping, Vector3 constantForce, Vector3 maxOffset, float dt)
        {
            int substeps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSpringSubstep), 1, MaxSpringSubstepCount);
            float h = dt / substeps;
            float dampingFactor = Mathf.Exp(-Mathf.Max(0f, damping) * h);

            for (int s = 0; s < substeps; s++)
            {
                velocity += (-offset * stiffness + constantForce) * h;
                velocity *= dampingFactor;
                offset += velocity * h;
                ClampOffset(ref offset, ref velocity, maxOffset);
            }
        }

        private static void ClampOffset(ref Vector3 offset, ref Vector3 velocity, Vector3 max)
        {
            ClampAxis(ref offset.x, ref velocity.x, Mathf.Abs(max.x));
            ClampAxis(ref offset.y, ref velocity.y, Mathf.Abs(max.y));
            ClampAxis(ref offset.z, ref velocity.z, Mathf.Abs(max.z));
        }

        private static void ClampAxis(ref float offset, ref float velocity, float limit)
        {
            if (offset > limit)
            {
                offset = limit;
                if (velocity > 0f) velocity = 0f;
            }
            else if (offset < -limit)
            {
                offset = -limit;
                if (velocity < 0f) velocity = 0f;
            }
        }

        private static void UnapplyOffsets(HonamiPhysicsBoneData b, Vector3 currentPos, Quaternion currentRot,
            out Vector3 animPos, out Quaternion animRot)
        {
            if (b.appliedInWorldSpace)
            {
                animRot = Quaternion.Inverse(Quaternion.Euler(b.appliedRotOffset)) * currentRot;
                animPos = currentPos - b.appliedPosOffset;
            }
            else
            {
                animRot = currentRot * Quaternion.Inverse(Quaternion.Euler(b.appliedRotOffset));
                animPos = currentPos - animRot * b.appliedPosOffset;
            }
        }

        public override void ProcessRig(float deltaTime)
        {
            bool isPlaying = Application.isPlaying;

            if ((!isPlaying && !simulateInEditMode) || bones == null || bones.Length == 0 || weight <= 0.001f)
            {
#if UNITY_EDITOR
                if (!isPlaying)
                {
                    RestoreBonesToAnimatedPose();
                    _lastEditorTime = 0d;
                }
#endif
                _isInitialized = false;
                return;
            }

            float dt = deltaTime;
#if UNITY_EDITOR
            // Time.deltaTime is 0 outside Play mode, so the editor preview keeps its own clock
            if (!isPlaying)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                dt = _lastEditorTime > 0d ? (float)(now - _lastEditorTime) : 0f;
                _lastEditorTime = now;
                if (dt > MaxEditorDeltaTime) dt = MaxEditorDeltaTime;
            }
#endif

            if (dt <= 0.0001f) return;

            if (!_isInitialized)
            {
                InitializeBones();
                _isInitialized = true;
            }

            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null) continue;

                b.bone.GetPositionAndRotation(out Vector3 currentPos, out Quaternion currentRot);
                b.bone.GetLocalPositionAndRotation(out Vector3 currentLocalPos, out Quaternion currentLocalRot);

                Vector3 animPos = currentPos;
                Quaternion animRot = currentRot;
                // an unchanged local pose means the animator did not overwrite our last write,
                // so the transform still contains the applied offset and must be un-applied
                if (b.hasAppliedOffsets
                    && (currentLocalPos - b.lastWrittenLocalPos).sqrMagnitude < 1e-10f
                    && Mathf.Abs(Quaternion.Dot(currentLocalRot, b.lastWrittenLocalRot)) > 0.9999999f)
                {
                    UnapplyOffsets(b, currentPos, currentRot, out animPos, out animRot);
                }

                StepBonePhysics(b, animPos, animRot, dt);

                float w = weight * b.weightMultiplier;
                Vector3 finalPosOffset = Vector3.Scale(b.currentPosOffset, positionAxisMask) * w;
                Vector3 finalRotOffset = Vector3.Scale(b.currentRotOffset, rotationAxisMask) * w;

                bool worldSpace = simulationSpace == HonamiPhysicsSimulationSpace.World;
                if (worldSpace)
                    b.bone.SetPositionAndRotation(animPos + finalPosOffset, Quaternion.Euler(finalRotOffset) * animRot);
                else
                    b.bone.SetPositionAndRotation(animPos + animRot * finalPosOffset, animRot * Quaternion.Euler(finalRotOffset));

                b.appliedPosOffset = finalPosOffset;
                b.appliedRotOffset = finalRotOffset;
                b.hasAppliedOffsets = w > 0.001f;
                b.appliedInWorldSpace = worldSpace;
                b.bone.GetLocalPositionAndRotation(out b.lastWrittenLocalPos, out b.lastWrittenLocalRot);
            }
        }

#if UNITY_EDITOR
        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying)
            {
                RestoreBonesToAnimatedPose();
                _lastEditorTime = 0d;
                _isInitialized = false;
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying || !simulateInEditMode) return;
            ProcessRig(0f);
            // without this the editor loop only ticks on repaints and the preview freezes
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        private void RestoreBonesToAnimatedPose()
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null || !b.hasAppliedOffsets) continue;

                b.bone.GetLocalPositionAndRotation(out Vector3 localPos, out Quaternion localRot);
                bool untouchedSinceOurWrite =
                    (localPos - b.lastWrittenLocalPos).sqrMagnitude < 1e-10f
                    && Mathf.Abs(Quaternion.Dot(localRot, b.lastWrittenLocalRot)) > 0.9999999f;
                if (untouchedSinceOurWrite)
                {
                    b.bone.GetPositionAndRotation(out Vector3 currentPos, out Quaternion currentRot);
                    UnapplyOffsets(b, currentPos, currentRot, out Vector3 animPos, out Quaternion animRot);
                    b.bone.SetPositionAndRotation(animPos, animRot);
                }

                b.appliedPosOffset = Vector3.zero;
                b.appliedRotOffset = Vector3.zero;
                b.hasAppliedOffsets = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bones == null) return;
            foreach (var b in bones)
            {
                if (b == null || b.bone == null) continue;
                Gizmos.color = Color.cyan * new Color(1, 1, 1, weight * b.weightMultiplier);
                Gizmos.DrawWireSphere(b.bone.position, 0.02f);
            }
        }
#endif
    }
}
