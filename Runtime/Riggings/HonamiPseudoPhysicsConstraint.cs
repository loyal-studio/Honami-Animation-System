using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Collections.Generic;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    public enum HonamiPhysicsMode
    {
        PseudoPhysics,
        Simulation
    }

    public enum HonamiPhysicsSimulationSpace
    {
        BoneLocal,
        World
    }

    public enum HonamiPhysicsJoint
    {
        Free,
        Hinge,
        Fixed
    }

    [Serializable]
    public sealed class HonamiPhysicsBoneData
    {
        public Transform bone;
        [Range(0f, 1f)] public float weightMultiplier = 1f;

        [Tooltip("Simulation mode: THIS bone hangs on that renderer's mesh surface instead of swinging on its bone length — a mesh collider purely for the physics. It slides across the surface with momentum and settles on the low side under gravity: assign the keyring's renderer on the charm bone and a single entry is a complete keychain. A MeshRenderer surface follows its own transform; a SkinnedMeshRenderer is anchored to the bone it is skinned to. Triangles are baked into this component in the editor, so Read/Write can stay off; keep the mesh a simple proxy. Replaces Joint and Angle Limit — model the shape itself to limit the travel.")]
        public Renderer shapeRenderer;
        [SerializeField, HideInInspector] internal Mesh bakedShapeSource;
        [SerializeField, HideInInspector] internal Vector3[] bakedShapeVertices;
        [SerializeField, HideInInspector] internal int[] bakedShapeTriangles;
        [SerializeField, HideInInspector] internal bool bakedShapeInBoneSpace;
        [SerializeField, HideInInspector] internal Transform bakedShapeAnchorBone;
        [SerializeField, HideInInspector] internal int bakedShapeVersion;

        [Tooltip("Simulation mode: how this bone is allowed to rotate. Free = swings in any direction. Hinge = rotates only about Hinge Axis, like a ring on a peg. Fixed = does not rotate at all, but still carries the bones below it.")]
        public HonamiPhysicsJoint joint = HonamiPhysicsJoint.Free;
        [Tooltip("Bone-local axis this bone rotates about when joint is Hinge — the peg the ring turns on. Any direction works: perpendicular to the bone it swings like a door, along the bone it spins in place like a ring on its peg, driven by the parent's twist and by gravity on Up Axis.")]
        public Vector3 hingeAxis = Vector3.forward;
        [Tooltip("Maximum angle (degrees) this bone may turn away from Limit Center. 0 = unlimited.")]
        public float angleLimit = 0f;
        [Tooltip("Bone-local direction the angle limit is measured from, rotating with the parent. Zero = the bone's own animated direction. Set it when the bone is modelled pointing away from where it should rest.")]
        public Vector3 limitCenter = Vector3.zero;
        [Tooltip("Free joint: bone-local axis kept facing the animated up direction — the roll control, deciding which way the bone faces while physics decides where it points. Hinge along the bone: marks the ring's heavy side, the direction gravity pulls down; zero = a balanced ring that only spins from the parent's motion. Zero on Free = derived automatically.")]
        public Vector3 upAxis = Vector3.zero;
        [Tooltip("Last bone of a chain only: bone-local direction of the virtual tip it swings. Zero = continue the bone's own offset from its parent.")]
        public Vector3 tipDirection = Vector3.zero;
        [Tooltip("Last bone of a chain only: length of the virtual tip in the bone's local units. 0 = the bone's own length.")]
        public float tipLength = 0f;
        [Range(0f, 2f)]
        [Tooltip("How hard this bone's mass is thrown around by the chain's own movement and turning. 0 = it rides along rigidly and never swings from handling, 1 = whatever the chain's Inertia allows, 2 = twice the whip. Gravity still applies at any value, so a 0 bone keeps hanging — it just ignores the motion.")]
        public float motionResponse = 1f;
        [Tooltip("Multiplies the chain's Gravity for this bone's mass. Lower it for a light link, raise it for a heavy charm.")]
        public float gravityScale = 1f;
        [Tooltip("Multiplies the chain's Damping for this bone's mass.")]
        public float dampingScale = 1f;
        [Tooltip("Multiplies the chain's Stiffness for this bone's mass.")]
        public float stiffnessScale = 1f;

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
        [Header("Mode")]
        [Tooltip("PseudoPhysics: offsets reacting to the animated bone's own motion, always springing back to the animated pose. Simulation: a real hanging chain — the listed bones become particles with mass, gravity and a rigid bone length, so they swing, wrap around and settle on their own. Use Simulation for keychains, pendants and straps.")]
        public HonamiPhysicsMode mode = HonamiPhysicsMode.PseudoPhysics;

        [Header("Target Bones")]
        [Tooltip("In Simulation mode, listed bones that are direct parent/child of each other are linked into one chain; the rest start their own chain.")]
        public HonamiPhysicsBoneData[] bones;

        [Header("Simulation")]
        [Tooltip("World-space acceleration applied to every particle (m/s²).")]
        public Vector3 simulationGravity = new Vector3(0f, -9.81f, 0f);
        [Range(0f, 1f)]
        [Tooltip("Air drag on the chain's free swing. 0 lets it swing forever, 1 settles it almost at once. Independent of Stiffness: raising one never forces you to retune the other.")]
        public float simulationDamping = 0.3f;
        [Range(0f, 1f)]
        [Tooltip("How hard the chain is pulled back toward its animated pose. 0 leaves it hanging fully free, and the chain then starts already settled along Gravity instead of at the bind pose — which is what a keychain modelled sticking up needs. 1 makes it follow the animation almost rigidly. The pull is critically damped, so raising it never makes the chain ring or overshoot.")]
        public float simulationStiffness = 0f;
        [Range(0f, 1f)]
        [Tooltip("Fraction of the root bone's movement and turning the chain inherits instantly. 0 = full lag and whip, 1 = the chain rides along rigidly. Raise it on fast weapons and first-person items so handling does not whip the chain.")]
        public float simulationInertia = 0f;

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

        private const float MaxSimStep = 1f / 120f;
        private const int MaxSimSubstepCount = 8;
        // the substep count is capped, so the frame delta must be too: without this a hitch hands the
        // integrator a step far larger than MaxSimStep and the chain is flung instead of simulated
        private const float MaxSimDeltaTime = MaxSimStep * MaxSimSubstepCount;

        // bumped whenever the bake math changes, so stale serialized bakes rebake themselves
        private const int ShapeBakeVersion = 3;

        private const float MinRetimeScale = 0.25f;
        private const float MaxRetimeScale = 4f;

        private const float MaxStiffnessRate = 40f;
        private const float MaxDragRate = 20f;
        // a rigid safety valve in angular terms: bone length is the amplifier turning a millimetre of
        // animation noise into a huge spin on a short bone, so the ceiling has to scale with it
        private const float MaxAngularSpeed = 30f;
        // a hinge axis this close to the bone direction has almost no cone left to swing — and the sliver
        // that remains is so thin the projection flips on frame noise — so it becomes a roll instead
        private const float RollHingeDotThreshold = 0.98f;
        // how fast a free bone's roll reference eases back to the animated up after swinging past a pole
        private const float UpFollowRate = 6f;
        // nominal pendulum arm for a rolling ring's heavy side; Gravity Scale is the per-bone tune
        private const float RollLeverLength = 0.05f;
        // ~0.16°: wide enough that a world-rotation write surviving the round trip back through the
        // parent matrices still counts as ours, or the rest pose ratchets onto the physics output
        private const float WrittenPoseTolerance = 0.999999f;
        // a micrometre in local units: a position write surviving the frame still counts as ours
        private const float WrittenPositionToleranceSq = 1e-12f;

        private sealed class SimParticle
        {
            public Transform transform;
            public HonamiPhysicsBoneData data;
            public Vector3 tipLocalOffset;

            public Quaternion restLocalRot;
            public Quaternion writtenLocalRot;
            public bool hasWritten;

            public Vector3 restWorldPos;
            public Quaternion restWorldRot;
            public float boneLength;
            public float rootDistance;

            public Vector3 localAimDir;
            public Vector3 localUpDir;
            public bool hasAim;
            public Vector3 lastWorldUp;
            public bool upInitialized;

            public HonamiPhysicsJoint joint;
            public Vector3 worldHingeAxis;
            public float hingeAlong;
            public Vector3 worldLimitDir;
            public float limitRad;
            public bool hingeWarned;

            public bool isRollHinge;
            public Vector3 localHingeAxis;
            public Vector3 localHeavyDir;
            public bool hasHeavy;
            public float rollAngle;
            public float rollPrevAngle;
            public Quaternion lastRestWorldRot;
            public bool rollInitialized;

            public HonamiPhysicsMeshShape meshShape;
            public bool hasShape;
            public bool shapeWarned;
            public Vector3 shapeRestProj;
            public Quaternion invRestWorldRot;

            public Vector3 restLocalPos;
            public Vector3 writtenLocalPos;
            public bool hasWrittenPos;

            public Vector3 gravity;
            public float dampFactor;
            public float stiffBlend;
            public float maxSpeed;

            public float carry;
            public Vector3 carryStepDelta;
            public Quaternion carryStepRot;
            public Vector3 swingReference;

            public Vector3 simPos;
            public Vector3 prevPos;
            public Vector3 nextPos;
        }

        private sealed class SimChain
        {
            public SimParticle[] particles;
            public Vector3 lastRootPos;
            public Quaternion lastRootRot;
            public bool initialized;
            public bool lengthsCaptured;
            public float lastStep;
        }

        private SimChain[] _chains;
        private bool _chainsDirty = true;
        private int _chainSignature;

        private bool _isInitialized;

#if UNITY_EDITOR
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
            // a graph rebuild must not pop a settled chain, so only relist when the chain layout really changed
            if (ComputeChainSignature() != _chainSignature) _chainsDirty = true;
        }

        private int ComputeChainSignature()
        {
            if (bones == null) return 0;
            int hash = 17;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                hash = hash * 31 + (b == null || b.bone == null ? 0 : b.bone.GetInstanceID());
                if (b == null) continue;
                hash = hash * 31 + b.tipDirection.GetHashCode();
                hash = hash * 31 + b.tipLength.GetHashCode();
                hash = hash * 31 + (b.shapeRenderer == null ? 0 : b.shapeRenderer.GetInstanceID());
                hash = hash * 31 + (b.bakedShapeSource == null ? 0 : b.bakedShapeSource.GetInstanceID());
                hash = hash * 31 + (b.bakedShapeAnchorBone == null ? 0 : b.bakedShapeAnchorBone.GetInstanceID());
            }
            return hash;
        }

        public override Playable CreatePlayable(Animator animator, PlayableGraph graph)
        {
            DisposeJobData();
            // Simulation writes whole bone rotations after the graph evaluated, where the animated pose is readable
            if (mode == HonamiPhysicsMode.Simulation) return Playable.Null;
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
            if (!_playable.IsValid() || !Application.isPlaying || mode == HonamiPhysicsMode.Simulation
                || bones == null || bones.Length == 0 || weight <= 0.001f)
            {
                if (_nativeParams.IsCreated) _nativeParams[0] = 0f;
                _isInitialized = false;
                return;
            }

            // keep last frame's offsets applied on paused frames; zeroing the weight pops the bone for one frame
            if (deltaTime <= 0.0001f) return;

            float dt = deltaTime > MaxSimDeltaTime ? MaxSimDeltaTime : deltaTime;

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
            int substeps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSimStep), 1, MaxSimSubstepCount);
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

        private void BuildChains()
        {
            _chains = Array.Empty<SimChain>();
            _chainSignature = ComputeChainSignature();
            if (bones == null || bones.Length == 0) return;

            var entries = new List<HonamiPhysicsBoneData>();
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null) continue;
                if (FindEntry(entries, b.bone) >= 0) continue;
                entries.Add(b);
            }
            if (entries.Count == 0) return;

            var used = new bool[entries.Count];
            var chains = new List<SimChain>();
            var buffer = new List<HonamiPhysicsBoneData>();

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (used[i]) continue;
                    // first pass only starts chains at bones whose parent is not listed, so links are not cut
                    // in half; a shaped bone hangs on its surface instead of its parent, so it always starts one
                    if (pass == 0 && entries[i].shapeRenderer == null && FindEntry(entries, entries[i].bone.parent) >= 0) continue;

                    buffer.Clear();
                    int current = i;
                    while (current >= 0 && !used[current])
                    {
                        used[current] = true;
                        buffer.Add(entries[current]);
                        current = FindChildEntry(entries, used, entries[current].bone);
                    }
                    chains.Add(CreateChain(buffer));
                }
            }

            _chains = chains.ToArray();
        }

        private static int FindEntry(List<HonamiPhysicsBoneData> entries, Transform bone)
        {
            if (bone == null) return -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].bone == bone) return i;
            }
            return -1;
        }

        private static int FindChildEntry(List<HonamiPhysicsBoneData> entries, bool[] used, Transform parent)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!used[i] && entries[i].shapeRenderer == null && entries[i].bone.parent == parent) return i;
            }
            return -1;
        }

        private SimChain CreateChain(List<HonamiPhysicsBoneData> entries)
        {
            // a shaped bone hangs on its surface, so the surface's anchor becomes the chain's root:
            // the chain then inherits the anchor's motion exactly like a bone-rooted chain would
            Transform anchor = null;
            if (entries[0].shapeRenderer != null)
            {
                anchor = ResolveShapeAnchor(entries[0]);
                if (anchor == null || anchor == entries[0].bone || anchor.IsChildOf(entries[0].bone))
                {
                    Debug.LogWarning($"HonamiPseudoPhysicsConstraint: Shape Renderer on '{entries[0].bone.name}' could not resolve a usable surface anchor — the surface must belong to another transform, not the sliding bone itself. Treating it as a plain chain.", this);
                    anchor = null;
                }
            }

            int head = anchor != null ? 1 : 0;
            Vector3 tipOffset = ResolveTipOffset(entries[entries.Count - 1]);
            bool hasTip = tipOffset.sqrMagnitude > 1e-8f;

            var particles = new SimParticle[head + entries.Count + (hasTip ? 1 : 0)];
            if (anchor != null) particles[0] = new SimParticle { transform = anchor };
            for (int i = 0; i < entries.Count; i++)
                particles[head + i] = new SimParticle { transform = entries[i].bone, data = entries[i] };

            // the tip carries the last bone's mass, so it reads that bone's per-mass scales too
            if (hasTip)
                particles[particles.Length - 1] = new SimParticle
                {
                    tipLocalOffset = tipOffset,
                    data = entries[entries.Count - 1]
                };
            else if (particles.Length < 2)
                Debug.LogWarning($"HonamiPseudoPhysicsConstraint: chain '{entries[0].bone.name}' has nothing to swing. Give it a child bone, or set Tip Direction and Tip Length on that bone.", this);

            return new SimChain { particles = particles };
        }

        private static Transform ResolveShapeAnchor(HonamiPhysicsBoneData d)
        {
            if (d.shapeRenderer == null) return null;
            if (d.shapeRenderer is SkinnedMeshRenderer smr)
            {
                if (d.bakedShapeAnchorBone != null) return d.bakedShapeAnchorBone;
                Mesh source = smr.sharedMesh;
                if (source != null && source.isReadable)
                {
                    Transform dominant = FindDominantBone(smr);
                    if (dominant != null) return dominant;
                }
                return smr.rootBone != null ? smr.rootBone : smr.transform;
            }
            return d.shapeRenderer.transform;
        }

        private static Transform FindDominantBone(SkinnedMeshRenderer smr)
        {
            Mesh source = smr.sharedMesh;
            Transform[] skinBones = smr.bones;
            if (source == null || skinBones == null || skinBones.Length == 0) return null;
            BoneWeight[] weights = source.boneWeights;
            if (weights == null || weights.Length == 0) return null;

            var sums = new float[skinBones.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                if (w.boneIndex0 < sums.Length) sums[w.boneIndex0] += w.weight0;
                if (w.boneIndex1 < sums.Length) sums[w.boneIndex1] += w.weight1;
                if (w.boneIndex2 < sums.Length) sums[w.boneIndex2] += w.weight2;
                if (w.boneIndex3 < sums.Length) sums[w.boneIndex3] += w.weight3;
            }

            int best = -1;
            float bestSum = 0f;
            for (int i = 0; i < sums.Length; i++)
            {
                if (sums[i] > bestSum && skinBones[i] != null)
                {
                    bestSum = sums[i];
                    best = i;
                }
            }
            return best >= 0 ? skinBones[best] : null;
        }

        private static Vector3 ResolveTipOffset(HonamiPhysicsBoneData data)
        {
            Transform bone = data.bone;
            bool canAutoDerive = bone.parent != null && bone.localPosition.sqrMagnitude > 1e-8f;

            Vector3 dir = data.tipDirection;
            if (dir.sqrMagnitude < 1e-8f)
            {
                if (!canAutoDerive) return Vector3.zero;
                // continue the bone's own offset from its parent, re-expressed in the bone's own space
                dir = Quaternion.Inverse(bone.localRotation) * bone.localPosition;
            }

            float length = data.tipLength;
            if (length <= 0f)
            {
                if (!canAutoDerive) return Vector3.zero;
                length = bone.localPosition.magnitude;
            }
            if (length <= 1e-4f) return Vector3.zero;

            return dir.normalized * length;
        }

        private static Vector3 StablePerpendicular(Vector3 previous, Vector3 aimDir)
        {
            Vector3 up = previous - aimDir * Vector3.Dot(previous, aimDir);
            return up.sqrMagnitude > 1e-6f ? up.normalized : AnyPerpendicular(aimDir);
        }

        private static Vector3 AnyPerpendicular(Vector3 dir)
        {
            Vector3 axis = Mathf.Abs(dir.x) < Mathf.Abs(dir.y)
                ? (Mathf.Abs(dir.x) < Mathf.Abs(dir.z) ? Vector3.right : Vector3.forward)
                : (Mathf.Abs(dir.y) < Mathf.Abs(dir.z) ? Vector3.up : Vector3.forward);
            return Vector3.Cross(dir, axis).normalized;
        }

        private void RestoreChains()
        {
            if (_chains == null) return;
            for (int c = 0; c < _chains.Length; c++)
            {
                var ps = _chains[c].particles;
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    if (p.transform == null) continue;
                    if (p.hasWritten)
                    {
                        if (Mathf.Abs(Quaternion.Dot(p.transform.localRotation, p.writtenLocalRot)) > WrittenPoseTolerance)
                            p.transform.localRotation = p.restLocalRot;
                        p.hasWritten = false;
                    }
                    if (p.hasWrittenPos)
                    {
                        if ((p.transform.localPosition - p.writtenLocalPos).sqrMagnitude < WrittenPositionToleranceSq)
                            p.transform.localPosition = p.restLocalPos;
                        p.hasWrittenPos = false;
                    }
                }
                _chains[c].initialized = false;
                _chains[c].lengthsCaptured = false;
                _chains[c].lastStep = 0f;
            }
        }

        private void SimulateChains(float dt)
        {
            if (_chains == null) BuildChains();
            if (_chains == null) return;

            for (int c = 0; c < _chains.Length; c++)
                SimulateChain(_chains[c], dt);
        }

        private void SimulateChain(SimChain chain, float dt)
        {
            var ps = chain.particles;
            if (ps.Length < 2) return;

            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p.transform == null) continue;

                p.transform.GetLocalPositionAndRotation(out Vector3 localPos, out Quaternion localRot);
                // an unchanged local pose means the animator did not overwrite our last write,
                // so put the animated pose back before anything reads a world position from it
                if (p.hasWritten && Mathf.Abs(Quaternion.Dot(localRot, p.writtenLocalRot)) > WrittenPoseTolerance)
                    p.transform.localRotation = p.restLocalRot;
                else
                    p.restLocalRot = localRot;

                if (p.hasWrittenPos && (localPos - p.writtenLocalPos).sqrMagnitude < WrittenPositionToleranceSq)
                    p.transform.localPosition = p.restLocalPos;
                else
                    p.restLocalPos = localPos;
            }

            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p.transform != null)
                    p.transform.GetPositionAndRotation(out p.restWorldPos, out p.restWorldRot);
                else
                {
                    var last = ps[i - 1];
                    p.restWorldPos = last.transform.TransformPoint(p.tipLocalOffset);
                    p.restWorldRot = last.restWorldRot;
                }
            }

            // a bone is rigid, so its length is measured once. re-measuring it per frame lets sub-millimetre
            // animation noise breathe the length, and the constraint then reports that as real velocity
            if (!chain.lengthsCaptured)
            {
                for (int i = 1; i < ps.Length; i++)
                {
                    ps[i].boneLength = Vector3.Distance(ps[i].restWorldPos, ps[i - 1].restWorldPos);
                    ps[i].rootDistance = ps[i - 1].rootDistance + ps[i].boneLength;
                }
                for (int i = 0; i < ps.Length - 1; i++)
                    CaptureShape(ps[i], ps[i + 1]);
                chain.lengthsCaptured = true;
            }

            CaptureAimAxes(ps);

            var root = ps[0];
            Vector3 rootTo = root.restWorldPos;
            Quaternion rootRotTo = root.restWorldRot;
            bool firstRun = !chain.initialized;
            Vector3 rootFrom = firstRun ? rootTo : chain.lastRootPos;
            Quaternion rootRotFrom = firstRun ? rootRotTo : chain.lastRootRot;
            Vector3 rootDelta = rootTo - rootFrom;
            Quaternion rootRotDelta = rootRotTo * Quaternion.Inverse(rootRotFrom);

            bool teleported = !firstRun
                && ((teleportDistanceThreshold > 0f && rootDelta.sqrMagnitude > teleportDistanceThreshold * teleportDistanceThreshold)
                || (teleportAngleThreshold > 0f && Quaternion.Angle(rootRotFrom, rootRotTo) > teleportAngleThreshold));

            chain.lastRootPos = rootTo;
            chain.lastRootRot = rootRotTo;
            chain.initialized = true;

            if (firstRun)
            {
                SettleChain(ps);
            }
            else if (teleported)
            {
                // carry the whole chain rigidly so it keeps its shape: resettling or letting the length
                // constraints absorb the jump both read as a visible pop on respawns and snap turns
                TransportChain(ps, rootFrom, rootDelta, rootRotDelta);
            }

            if (firstRun || teleported)
            {
                rootFrom = rootTo;
                rootDelta = Vector3.zero;
                rootRotDelta = Quaternion.identity;
            }

            int substeps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxSimStep), 1, MaxSimSubstepCount);
            float h = dt / substeps;

            // Verlet keeps velocity as the displacement over one step, so a step that changed length since
            // the history was written silently rescales that velocity. dt lands right on a substep boundary
            // at common refresh rates, so the count flips every frame and the chain buzzes. rescale the
            // history to the new step instead, which is what makes the sliders readable at all
            RetimeHistory(chain, ps, h);
            chain.lastStep = h;

            bool anyCarry = PrepareDynamics(ps, h, rootDelta, rootRotDelta, dt, substeps);
            bool transportPerStep = anyCarry
                && (rootDelta.sqrMagnitude > 1e-14f || Quaternion.Angle(Quaternion.identity, rootRotDelta) > 1e-3f);

            for (int s = 0; s < substeps; s++)
            {
                // the attachment has to travel with the substeps: dropping its whole frame delta into the
                // first one makes the length constraint recover a velocity scaled by the substep count
                Vector3 rootPos = Vector3.Lerp(rootFrom, rootTo, (s + 1f) / substeps);
                // the inherited motion must travel with it too, or the pre-moved chain sits ahead of the
                // still-interpolating attachment and the length constraint drags it back and forth
                if (transportPerStep)
                    CarryChain(ps, Vector3.Lerp(rootFrom, rootTo, (float)s / substeps));
                StepChain(ps, h, rootPos);
            }

            StepRollHinges(ps, firstRun || teleported, substeps, h);
            ApplyChain(ps, dt);
        }

        // moving both position and history by the same rigid transform adds no implicit velocity:
        // the chain keeps its shape and its swing continues in the moved frame
        private static void TransportChain(SimParticle[] ps, Vector3 pivot, Vector3 delta, Quaternion rotation)
        {
            for (int i = 1; i < ps.Length; i++)
            {
                var p = ps[i];
                p.simPos = pivot + delta + rotation * (p.simPos - pivot);
                p.prevPos = pivot + delta + rotation * (p.prevPos - pivot);
            }
        }

        // rewrite the history so the same velocity is expressed over the new step: prev must land where it
        // would have, had the last step always been h
        private static void RetimeHistory(SimChain chain, SimParticle[] ps, float h)
        {
            float last = chain.lastStep;
            if (last <= 1e-6f) return;
            // correcting a substep flip needs ~[0.5, 2]; a wilder ratio means the last step was a hitch that
            // sampled the velocity badly, and restoring that faithfully would pop the chain
            float scale = Mathf.Clamp(h / last, MinRetimeScale, MaxRetimeScale);
            if (Mathf.Abs(scale - 1f) < 1e-4f) return;

            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (i > 0) p.prevPos = p.simPos - (p.simPos - p.prevPos) * scale;
                p.rollPrevAngle = p.rollAngle - (p.rollAngle - p.rollPrevAngle) * scale;
            }
        }

        // same rigid move as TransportChain, but each bone carries its own fraction of it: the part it does
        // not carry is exactly the lag that reads as swing, which is what Motion Response tunes
        private static void CarryChain(SimParticle[] ps, Vector3 pivot)
        {
            for (int i = 1; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p.carry == 0f) continue;
                p.simPos = pivot + p.carryStepDelta + p.carryStepRot * (p.simPos - pivot);
                p.prevPos = pivot + p.carryStepDelta + p.carryStepRot * (p.prevPos - pivot);
            }
        }

        // a part rigidly skinned to one bone keeps a constant surface in that bone's space, so a single
        // snapshot of the current pose, re-expressed relative to the bone, is valid in every pose.
        // the vertices are skinned manually with the same matrices Unity skins with — BakeMesh scale
        // semantics differ across setups and misplace rigs with import scale
        private static void SnapshotSkinnedToBoneLocal(SkinnedMeshRenderer smr, Transform bone,
            out Vector3[] vertices, out int[] triangles)
        {
            Mesh source = smr.sharedMesh;
            vertices = source.vertices;
            triangles = source.triangles;

            BoneWeight[] weights = source.boneWeights;
            Matrix4x4[] bindposes = source.bindposes;
            Transform[] skinBones = smr.bones;

            bone.GetPositionAndRotation(out Vector3 bonePos, out Quaternion boneRot);
            Quaternion invBone = Quaternion.Inverse(boneRot);
            Matrix4x4 rendererToWorld = smr.transform.localToWorldMatrix;

            bool isSkinned = weights.Length == vertices.Length && bindposes.Length > 0 && skinBones.Length > 0;
            if (isSkinned)
            {
                int matrixCount = Mathf.Min(bindposes.Length, skinBones.Length);
                var skin = new Matrix4x4[matrixCount];
                for (int i = 0; i < matrixCount; i++)
                    skin[i] = skinBones[i] != null ? skinBones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    BoneWeight w = weights[i];
                    Vector3 world = Vector3.zero;
                    float total = 0f;
                    if (w.weight0 > 0f && w.boneIndex0 < matrixCount)
                    {
                        world += (Vector3)skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
                        total += w.weight0;
                    }
                    if (w.weight1 > 0f && w.boneIndex1 < matrixCount)
                    {
                        world += (Vector3)skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
                        total += w.weight1;
                    }
                    if (w.weight2 > 0f && w.boneIndex2 < matrixCount)
                    {
                        world += (Vector3)skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
                        total += w.weight2;
                    }
                    if (w.weight3 > 0f && w.boneIndex3 < matrixCount)
                    {
                        world += (Vector3)skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                        total += w.weight3;
                    }
                    if (total > 1e-4f) world /= total;
                    else world = rendererToWorld.MultiplyPoint3x4(v);

                    vertices[i] = invBone * (world - bonePos);
                }
            }
            else
            {
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = invBone * ((Vector3)rendererToWorld.MultiplyPoint3x4(vertices[i]) - bonePos);
            }
        }

        // the shape lives on the SLIDING bone's entry (child), while the constraint state lives on the
        // particle it hangs from (p) — the anchor particle that CreateChain put above it
        private void CaptureShape(SimParticle p, SimParticle child)
        {
            var d = child.data;
            Renderer renderer = d != null && child.transform != null ? d.shapeRenderer : null;
            if (renderer == null || p.transform == null || p.data != null)
            {
                p.hasShape = false;
                p.meshShape = null;
                return;
            }

            // a renderer or mesh swap changes the chain signature and relists the particles, so a
            // structure built once stays valid for this particle's whole lifetime — no rebuild on re-enable
            if (p.meshShape != null)
            {
                p.hasShape = true;
                return;
            }

            if (renderer is SkinnedMeshRenderer smr)
            {
                Mesh source = smr.sharedMesh;
                if (source == null)
                {
                    WarnShapeOnce(p, $"Shape Renderer on '{child.transform.name}' has no mesh");
                    return;
                }

                bool bakeValid = d.bakedShapeInBoneSpace && d.bakedShapeSource == source
                    && d.bakedShapeVersion == ShapeBakeVersion
                    && d.bakedShapeAnchorBone == p.transform
                    && d.bakedShapeVertices != null && d.bakedShapeVertices.Length == source.vertexCount
                    && d.bakedShapeTriangles != null && d.bakedShapeTriangles.Length > 0;

                if (bakeValid)
                {
                    p.meshShape = HonamiPhysicsMeshShape.Build(d.bakedShapeVertices, d.bakedShapeTriangles, Vector3.one);
                }
                else if (source.isReadable)
                {
                    SnapshotSkinnedToBoneLocal(smr, p.transform, out Vector3[] vertices, out int[] triangles);
                    p.meshShape = HonamiPhysicsMeshShape.Build(vertices, triangles, Vector3.one);
                }
                else
                {
                    WarnShapeOnce(p, $"mesh '{source.name}' on '{child.transform.name}' has no baked data — reassign the renderer in the inspector so it bakes");
                    return;
                }
            }
            else if (renderer is MeshRenderer)
            {
                Mesh mesh = renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    WarnShapeOnce(p, $"Shape Renderer on '{child.transform.name}' has no MeshFilter with a mesh");
                    return;
                }

                Vector3 scale = renderer.transform.lossyScale;
                bool bakeValid = !d.bakedShapeInBoneSpace && d.bakedShapeSource == mesh
                    && d.bakedShapeVersion == ShapeBakeVersion
                    && d.bakedShapeVertices != null && d.bakedShapeVertices.Length == mesh.vertexCount
                    && d.bakedShapeTriangles != null && d.bakedShapeTriangles.Length > 0;

                if (bakeValid)
                {
                    p.meshShape = HonamiPhysicsMeshShape.Build(d.bakedShapeVertices, d.bakedShapeTriangles, scale);
                }
                else if (mesh.isReadable)
                {
                    // runtime-created meshes are readable and never pass through the editor bake
                    p.meshShape = HonamiPhysicsMeshShape.Build(mesh, scale);
                }
                else
                {
                    WarnShapeOnce(p, $"mesh '{mesh.name}' on '{child.transform.name}' has no baked data — reassign the renderer in the inspector so it bakes");
                    return;
                }
            }
            else
            {
                WarnShapeOnce(p, $"Shape Renderer on '{child.transform.name}' is not a MeshRenderer or SkinnedMeshRenderer");
                return;
            }

            if (p.meshShape == null)
            {
                WarnShapeOnce(p, $"the shape mesh on '{child.transform.name}' has no usable triangles");
                return;
            }

            p.hasShape = true;
        }

        private void WarnShapeOnce(SimParticle p, string reason)
        {
            p.hasShape = false;
            if (p.shapeWarned) return;
            Debug.LogWarning($"HonamiPseudoPhysicsConstraint: {reason}. Falling back to a plain chain link.", this);
            p.shapeWarned = true;
        }

        private void UpdateShapeFrame(SimParticle p, SimParticle child)
        {
            // the anchor particle IS the surface's frame: its transform is the bone the skinned mesh
            // is bound to (or the MeshRenderer's own transform), read post-restore like any rest pose
            p.invRestWorldRot = Quaternion.Inverse(p.restWorldRot);
            p.shapeRestProj = p.meshShape.ClosestPoint(p.invRestWorldRot * (child.restWorldPos - p.restWorldPos));

            // the mesh is the whole constraint for this link, so the joint machinery is disarmed
            p.joint = HonamiPhysicsJoint.Free;
            p.isRollHinge = false;
            p.worldHingeAxis = Vector3.zero;
            p.limitRad = 0f;

            Vector3 worldAim = child.restWorldPos - p.restWorldPos;
            if (worldAim.sqrMagnitude < 1e-10f)
            {
                p.hasAim = false;
                return;
            }
            p.localAimDir = (p.invRestWorldRot * worldAim).normalized;
            p.localUpDir = StablePerpendicular(p.localUpDir, p.localAimDir);
            p.hasAim = true;
        }

        private void CaptureAimAxes(SimParticle[] ps)
        {
            for (int i = 0; i < ps.Length - 1; i++)
            {
                var p = ps[i];
                if (p.hasShape)
                {
                    UpdateShapeFrame(p, ps[i + 1]);
                    continue;
                }

                Vector3 worldAim = ps[i + 1].restWorldPos - p.restWorldPos;
                if (worldAim.sqrMagnitude < 1e-10f)
                {
                    p.hasAim = false;
                    continue;
                }

                p.localAimDir = (Quaternion.Inverse(p.restWorldRot) * worldAim).normalized;

                var d = p.data;
                p.joint = d != null ? d.joint : HonamiPhysicsJoint.Free;
                p.isRollHinge = false;

                Vector3 hinge = d != null ? d.hingeAxis : Vector3.zero;
                if (p.joint == HonamiPhysicsJoint.Hinge && hinge.sqrMagnitude < 1e-8f)
                {
                    if (!p.hingeWarned)
                    {
                        Debug.LogWarning($"HonamiPseudoPhysicsConstraint: Hinge Axis on '{p.transform.name}' is zero. Falling back to Free.", this);
                        p.hingeWarned = true;
                    }
                    p.joint = HonamiPhysicsJoint.Free;
                }

                if (p.joint == HonamiPhysicsJoint.Hinge)
                {
                    hinge.Normalize();
                    p.worldHingeAxis = (p.restWorldRot * hinge).normalized;
                    p.hingeAlong = Vector3.Dot(hinge, p.localAimDir);
                    // an axis along the bone leaves the child rigid but frees the spin about itself —
                    // a ring on its peg — so it becomes an angular hinge instead of being rejected
                    p.isRollHinge = Mathf.Abs(p.hingeAlong) > RollHingeDotThreshold;
                    if (p.isRollHinge)
                    {
                        p.localHingeAxis = hinge;
                        p.localUpDir = StablePerpendicular(p.localUpDir, p.localAimDir);
                        Vector3 heavy = d != null ? d.upAxis - hinge * Vector3.Dot(hinge, d.upAxis) : Vector3.zero;
                        p.hasHeavy = heavy.sqrMagnitude > 1e-8f;
                        p.localHeavyDir = p.hasHeavy ? heavy.normalized : Vector3.zero;
                    }
                    else
                    {
                        // the hinge axis must stay put in world space, which fixes the roll on its own
                        p.localUpDir = hinge;
                    }
                }
                else
                {
                    p.worldHingeAxis = Vector3.zero;

                    Vector3 up = d != null ? d.upAxis : Vector3.zero;
                    if (up.sqrMagnitude > 1e-8f) up -= p.localAimDir * Vector3.Dot(up, p.localAimDir);
                    // axes are re-derived every frame, so an auto up must stay continuous: reprojecting
                    // last frame's choice keeps it put, while a fresh arbitrary perpendicular flips its
                    // branch on sub-millimetre aim noise and snaps the bone's roll
                    p.localUpDir = up.sqrMagnitude > 1e-8f
                        ? up.normalized
                        : StablePerpendicular(p.localUpDir, p.localAimDir);
                }

                p.limitRad = d != null && d.angleLimit > 0f ? d.angleLimit * Mathf.Deg2Rad : 0f;

                Vector3 center = d != null ? d.limitCenter : Vector3.zero;
                if (center.sqrMagnitude < 1e-8f) center = p.localAimDir;
                p.worldLimitDir = (p.restWorldRot * center.normalized).normalized;

                p.hasAim = true;
            }
        }

        private bool PrepareDynamics(SimParticle[] ps, float h, Vector3 rootDelta, Quaternion rootRotDelta,
            float dt, int substeps)
        {
            float stiffness01 = Mathf.Clamp01(simulationStiffness);
            float damping01 = Mathf.Clamp01(simulationDamping);
            float lag = 1f - Mathf.Clamp01(simulationInertia);
            float invSubsteps = 1f / substeps;
            bool anyCarry = false;

            for (int i = 1; i < ps.Length; i++)
            {
                var p = ps[i];
                var d = p.data;
                float gravityScale = d != null ? d.gravityScale : 1f;
                float dampingScale = d != null ? d.dampingScale : 1f;
                float stiffnessScale = d != null ? d.stiffnessScale : 1f;

                // squared so the low end of the slider, where hanging accessories live, keeps its resolution
                float s = Mathf.Clamp01(stiffness01 * stiffnessScale);
                float dm = Mathf.Clamp01(damping01 * dampingScale);

                p.gravity = simulationGravity * gravityScale;
                p.dampFactor = Mathf.Exp(-MaxDragRate * dm * dm * h);
                p.stiffBlend = s > 0f ? 1f - Mathf.Exp(-MaxStiffnessRate * s * s * h) : 0f;
                // the swing is measured against the attachment, so the radius is the reach along the chain
                p.maxSpeed = p.rootDistance * MaxAngularSpeed;

                // response above 1 overshoots the lag into a negative carry, which drags the bone against the
                // root and reads as an exaggerated whip; the length constraint still pins it, so it is bounded
                p.carry = 1f - lag * ResolveMotionResponse(d);
                p.swingReference = rootDelta * ((1f - p.carry) / dt);
                p.carryStepDelta = rootDelta * (p.carry * invSubsteps);
                p.carryStepRot = Quaternion.SlerpUnclamped(Quaternion.identity, rootRotDelta, p.carry * invSubsteps);
                if (p.carry != 0f) anyCarry = true;
            }

            return anyCarry;
        }

        private static float ResolveMotionResponse(HonamiPhysicsBoneData d)
            => d != null ? Mathf.Max(0f, d.motionResponse) : 1f;

        // the anchor is kinematic — the bone the ring is bound to, never a simulated link — so its
        // animated frame is exact and the surface never needs to chase a swinging owner
        private static Vector3 ConstrainToShape(SimParticle parent, Vector3 next, float stiffBlend)
        {
            Vector3 local = parent.invRestWorldRot * (next - parent.nextPos);
            Vector3 target = parent.meshShape.ClosestPoint(local);
            // stiffness pulls along the surface, so re-project the blend instead of springing off it
            if (stiffBlend > 0f)
                target = parent.meshShape.ClosestPoint(Vector3.Lerp(target, parent.shapeRestProj, stiffBlend));
            return parent.nextPos + parent.restWorldRot * target;
        }

        private static Vector3 ConstrainDirection(SimParticle parent, Vector3 dir)
        {
            if (parent.joint == HonamiPhysicsJoint.Hinge)
            {
                // a hinge preserves the bone's angle to the axis — the bone sweeps a cone, not a flat disc.
                // flattening into the plane is only correct for a perpendicular axis, and rejecting every
                // slanted axis is what forced artists into one specific hinge direction per rig
                Vector3 n = parent.worldHingeAxis;
                float along = parent.hingeAlong;
                Vector3 planar = dir - n * Vector3.Dot(n, dir);
                if (planar.sqrMagnitude < 1e-10f) return Vector3.zero;
                float planarMag = Mathf.Sqrt(Mathf.Max(0f, 1f - along * along));
                dir = n * along + planar.normalized * planarMag;
            }

            // a roll hinge clamps its own roll angle instead; the aim direction is already rigid
            if (parent.limitRad > 0f && !parent.isRollHinge)
            {
                Vector3 center = parent.worldLimitDir;
                if (parent.joint == HonamiPhysicsJoint.Hinge)
                {
                    // keep the clamp's centre on the same cone or it would pull the bone off the hinge
                    Vector3 n = parent.worldHingeAxis;
                    Vector3 planarC = center - n * Vector3.Dot(n, center);
                    if (planarC.sqrMagnitude < 1e-10f) return dir;
                    float along = parent.hingeAlong;
                    center = n * along + planarC.normalized * Mathf.Sqrt(Mathf.Max(0f, 1f - along * along));
                }
                dir = Vector3.RotateTowards(center.normalized, dir, parent.limitRad, 0f);
            }

            return dir;
        }

        // with no spring to the animated pose the only equilibrium is along gravity, so start there:
        // a chain modelled sticking straight up otherwise balances on an unstable inverted pendulum
        private void SettleChain(SimParticle[] ps)
        {
            Vector3 hang = simulationGravity;
            bool useGravity = simulationStiffness <= 0f && hang.sqrMagnitude > 1e-8f;
            if (useGravity) hang.Normalize();

            ps[0].simPos = ps[0].restWorldPos;
            ps[0].prevPos = ps[0].simPos;

            for (int i = 1; i < ps.Length; i++)
            {
                var p = ps[i];
                var parent = ps[i - 1];

                if (parent.hasShape)
                {
                    Vector3 local = parent.shapeRestProj;
                    if (useGravity)
                    {
                        // aim well past the shape along gravity: the closest surface point to that
                        // probe is the low side of the mesh, which is where the mass would settle
                        Vector3 g = parent.invRestWorldRot * hang;
                        local = parent.meshShape.ClosestPoint(parent.shapeRestProj + g * parent.meshShape.BoundsSize);
                    }
                    p.simPos = parent.simPos + parent.restWorldRot * local;
                }
                else if (!useGravity || !parent.hasAim)
                {
                    p.simPos = p.restWorldPos;
                }
                else
                {
                    Vector3 restDir = parent.restWorldRot * parent.localAimDir;
                    Vector3 dir = parent.joint == HonamiPhysicsJoint.Fixed || parent.isRollHinge
                        ? restDir
                        : ConstrainDirection(parent, hang);
                    if (dir.sqrMagnitude < 1e-10f) dir = ConstrainDirection(parent, restDir);
                    if (dir.sqrMagnitude < 1e-10f) dir = restDir;

                    p.simPos = parent.simPos + dir * p.boneLength;
                }

                p.prevPos = p.simPos;
            }

            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                p.rollAngle = 0f;
                p.rollPrevAngle = 0f;
                p.rollInitialized = false;
                p.upInitialized = false;
            }
        }

        private void StepChain(SimParticle[] ps, float h, Vector3 rootPos)
        {
            ps[0].nextPos = rootPos;

            for (int i = 1; i < ps.Length; i++)
            {
                var p = ps[i];
                var parent = ps[i - 1];

                if (!parent.hasShape && p.boneLength <= 1e-5f)
                {
                    p.prevPos = p.simPos;
                    p.simPos = parent.nextPos;
                    p.nextPos = parent.nextPos;
                    continue;
                }

                // position Verlet: velocity lives in the position history, so a constraint projection is
                // absorbed by the next step instead of being read back amplified as an explicit velocity
                Vector3 velocity = (p.simPos - p.prevPos) / h;
                // the ceiling is on the swing relative to the attachment, never on the transport it inherits,
                // or a chain on a running character would be clamped into permanently lagging behind
                Vector3 swing = velocity - p.swingReference;
                float max = p.maxSpeed;
                if (max > 0f && swing.sqrMagnitude > max * max)
                    velocity = p.swingReference + swing * (max / swing.magnitude);

                Vector3 next = p.simPos + velocity * (p.dampFactor * h) + p.gravity * (h * h);

                if (parent.hasShape)
                {
                    p.nextPos = ConstrainToShape(parent, next, p.stiffBlend);
                    p.prevPos = p.simPos;
                    p.simPos = p.nextPos;
                    continue;
                }

                Vector3 restDir = parent.restWorldRot * parent.localAimDir;
                Vector3 dir;
                // a roll hinge is positionally rigid: its whole motion is the roll angle, and the residual
                // near-axis cone would only renormalize noise-sized vectors into a random wobble
                if (parent.joint == HonamiPhysicsJoint.Fixed || parent.isRollHinge)
                {
                    dir = restDir;
                }
                else
                {
                    dir = next - parent.nextPos;
                    dir = dir.sqrMagnitude < 1e-10f ? restDir : dir.normalized;
                    // stiffness steers the direction toward the animated pose instead of springing the world
                    // position: the pull no longer scales with how far the character moved this frame, so a
                    // fast run or a camera turn cannot overshoot the chain or make it ring
                    if (p.stiffBlend > 0f) dir = Vector3.Slerp(dir, restDir, p.stiffBlend);
                    dir = ConstrainDirection(parent, dir);
                    if (dir.sqrMagnitude < 1e-10f) dir = ConstrainDirection(parent, restDir);
                    if (dir.sqrMagnitude < 1e-10f) dir = restDir;
                }

                p.nextPos = parent.nextPos + dir * p.boneLength;
                p.prevPos = p.simPos;
                p.simPos = p.nextPos;
            }

            ps[0].prevPos = ps[0].simPos;
            ps[0].simPos = rootPos;
        }

        private void StepRollHinges(SimParticle[] ps, bool reanchor, int substeps, float h)
        {
            float stiffness01 = Mathf.Clamp01(simulationStiffness);
            float damping01 = Mathf.Clamp01(simulationDamping);
            float lag = 1f - Mathf.Clamp01(simulationInertia);

            for (int i = 0; i < ps.Length - 1; i++)
            {
                var p = ps[i];
                if (!p.hasAim || !p.isRollHinge) continue;

                var d = p.data;
                bool skipKick = reanchor || !p.rollInitialized;
                Vector3 worldAxis = p.restWorldRot * p.localHingeAxis;
                Quaternion frameDelta = p.restWorldRot * Quaternion.Inverse(p.lastRestWorldRot);
                p.lastRestWorldRot = p.restWorldRot;
                p.rollInitialized = true;

                float rollLag = lag * ResolveMotionResponse(d);
                if (!skipKick && rollLag > 0f)
                {
                    // the ring tends to keep its world orientation while the parent twists under it, so
                    // the parent's twist about the axis arrives as an opposite kick on the relative angle
                    float twistProj = frameDelta.x * worldAxis.x + frameDelta.y * worldAxis.y + frameDelta.z * worldAxis.z;
                    float twist = 2f * Mathf.Atan2(twistProj, frameDelta.w);
                    if (twist > Mathf.PI) twist -= 2f * Mathf.PI;
                    else if (twist < -Mathf.PI) twist += 2f * Mathf.PI;

                    float kick = twist * rollLag;
                    p.rollAngle -= kick;
                    // the kick is a whole-frame delta, but Verlet reads history at substep scale: leaving
                    // the history untouched replays the kick as a velocity multiplied by the substep
                    // count, and alternating idle sway then pumps the ring into spinning on its own.
                    // shift the history too, keeping only the true per-second rate as velocity
                    p.rollPrevAngle -= kick * (1f - 1f / substeps);
                }

                float dm = Mathf.Clamp01(damping01 * (d != null ? d.dampingScale : 1f));
                float st = Mathf.Clamp01(stiffness01 * (d != null ? d.stiffnessScale : 1f));
                float gravityScale = d != null ? d.gravityScale : 1f;
                float dampFactor = Mathf.Exp(-MaxDragRate * dm * dm * h);
                float stiffBlend = st > 0f ? 1f - Mathf.Exp(-MaxStiffnessRate * st * st * h) : 0f;

                for (int s = 0; s < substeps; s++)
                {
                    float angularVelocity = Mathf.Clamp((p.rollAngle - p.rollPrevAngle) / h, -MaxAngularSpeed, MaxAngularSpeed);

                    float accel = 0f;
                    if (p.hasHeavy)
                    {
                        Vector3 lever = p.restWorldRot * (Quaternion.AngleAxis(p.rollAngle * Mathf.Rad2Deg, p.localHingeAxis) * p.localHeavyDir);
                        // pendulum about the axis: the cross projected on it is torque with sin() built in
                        accel = Vector3.Dot(Vector3.Cross(lever, simulationGravity * gravityScale), worldAxis) / RollLeverLength;
                    }

                    float next = p.rollAngle + angularVelocity * dampFactor * h + accel * (h * h);
                    if (stiffBlend > 0f) next *= 1f - stiffBlend;
                    if (p.limitRad > 0f) next = Mathf.Clamp(next, -p.limitRad, p.limitRad);

                    p.rollPrevAngle = p.rollAngle;
                    p.rollAngle = next;
                }
            }
        }

        private void ApplyChain(SimParticle[] ps, float dt)
        {
            float upBlend = 1f - Mathf.Exp(-UpFollowRate * dt);

            for (int i = 0; i < ps.Length - 1; i++)
            {
                var p = ps[i];
                // a shape link moves its mass across a surface, which no rotation of this bone can
                // express, so the child is translated instead and this bone keeps its own pose
                if (p.hasShape && ps[i + 1].transform != null)
                {
                    ApplyShapeChild(ps, i);
                    continue;
                }

                Vector3 aimDir = ps[i + 1].simPos - p.simPos;
                if (!p.hasAim || aimDir.sqrMagnitude < 1e-10f) continue;
                aimDir.Normalize();

                float w = weight * (p.data != null ? p.data.weightMultiplier : 1f);
                Quaternion simRot = BuildAimRotation(p, aimDir, upBlend);
                if (p.isRollHinge && p.rollAngle != 0f)
                    simRot *= Quaternion.AngleAxis(p.rollAngle * Mathf.Rad2Deg, p.localHingeAxis);
                Quaternion finalRot = w >= 0.999f ? simRot : Quaternion.Slerp(p.restWorldRot, simRot, w);

                p.transform.rotation = finalRot;
                p.writtenLocalRot = p.transform.localRotation;
                p.hasWritten = true;
            }
        }

        private void ApplyShapeChild(SimParticle[] ps, int ownerIndex)
        {
            var p = ps[ownerIndex];
            var child = ps[ownerIndex + 1];

            // the anchor is kinematic, so its rest pose is its current pose and this world point is exact
            Vector3 target = p.meshShape.ClosestPoint(p.invRestWorldRot * (child.simPos - p.simPos));
            Vector3 world = p.simPos + p.restWorldRot * target;

            Transform sceneParent = child.transform.parent;
            Vector3 local = sceneParent != null ? sceneParent.InverseTransformPoint(world) : world;

            float w = weight * (child.data != null ? child.data.weightMultiplier : 1f);
            Vector3 final = w >= 0.999f ? local : Vector3.Lerp(child.restLocalPos, local, w);

            child.transform.localPosition = final;
            child.writtenLocalPos = final;
            child.hasWrittenPos = true;
        }

        // a minimal-arc rotation off the bind direction goes singular once the chain hangs ~180° away from it,
        // which is exactly where a keychain modelled sticking up lives, so aim and roll are resolved separately
        private static Quaternion BuildAimRotation(SimParticle p, Vector3 aimDir, float upBlend)
        {
            Vector3 animatedUp = p.restWorldRot * p.localUpDir;
            animatedUp -= aimDir * Vector3.Dot(animatedUp, aimDir);
            bool animatedUpValid = animatedUp.sqrMagnitude > 1e-6f;
            if (animatedUpValid) animatedUp.Normalize();

            Vector3 upRef;
            // a free bone swinging past the animated up direction flips the projected up's sign, which
            // reads as the charm mirroring 180° every time an idle sway crosses that pose. carrying last
            // frame's up across the pole keeps the roll continuous, and it then eases back to the
            // animated up so an intended roll in the animation still arrives; a hinge up is locked to
            // its axis and never crosses the pole, so it keeps the direct derivation
            if (p.joint == HonamiPhysicsJoint.Free && p.upInitialized)
            {
                upRef = p.lastWorldUp - aimDir * Vector3.Dot(p.lastWorldUp, aimDir);
                if (upRef.sqrMagnitude < 1e-6f)
                {
                    upRef = animatedUpValid ? animatedUp : AnyPerpendicular(aimDir);
                }
                else
                {
                    upRef.Normalize();
                    // pull only while the animated up is on the same side: after a pole crossing the
                    // two are opposed, and easing across that arc reads as the charm slowly spinning
                    if (animatedUpValid && upBlend > 0f && Vector3.Dot(upRef, animatedUp) > 0f)
                    {
                        upRef = Vector3.Slerp(upRef, animatedUp, upBlend);
                        upRef -= aimDir * Vector3.Dot(upRef, aimDir);
                        upRef = upRef.sqrMagnitude > 1e-6f ? upRef.normalized : AnyPerpendicular(aimDir);
                    }
                }
            }
            else
            {
                upRef = animatedUpValid ? animatedUp : AnyPerpendicular(aimDir);
            }

            p.lastWorldUp = upRef;
            p.upInitialized = true;

            return Quaternion.LookRotation(aimDir, upRef)
                * Quaternion.Inverse(Quaternion.LookRotation(p.localAimDir, p.localUpDir));
        }

        public override void ProcessRig(float deltaTime)
        {
            bool isPlaying = Application.isPlaying;

            if (_chainsDirty)
            {
                RestoreChains();
                _chains = null;
                _chainsDirty = false;
            }

            if ((!isPlaying && !simulateInEditMode) || bones == null || bones.Length == 0 || weight <= 0.001f)
            {
                RestoreChains();
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
            }
#endif

            if (dt <= 0.0001f) return;
            if (dt > MaxSimDeltaTime) dt = MaxSimDeltaTime;

            if (mode == HonamiPhysicsMode.Simulation)
            {
                SimulateChains(dt);
                return;
            }

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

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreChains();
            _chainsDirty = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RestoreBonesToAnimatedPose();
                _lastEditorTime = 0d;
                _isInitialized = false;
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BakeShapeMeshes();
            // tuning a slider must not resettle a running chain, so only a real layout edit relists it
            if (ComputeChainSignature() != _chainSignature) _chainsDirty = true;
        }

        // the editor can read any mesh regardless of its Read/Write flag, so the triangles are copied
        // into the component here and the player never needs the CPU-side mesh copy
        private void BakeShapeMeshes()
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;

                var skinned = b.shapeRenderer as SkinnedMeshRenderer;
                Mesh mesh = null;
                if (skinned != null) mesh = skinned.sharedMesh;
                else if (b.shapeRenderer != null && b.shapeRenderer.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;

                bool wantBoneSpace = skinned != null;
                Transform anchor = null;
                if (wantBoneSpace && mesh != null)
                {
                    // the surface belongs to whatever bone the mesh is bound to, not to the sliding bone
                    anchor = FindDominantBone(skinned);
                    if (anchor == null) anchor = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                }

                if (mesh == null || (wantBoneSpace && anchor == null))
                {
                    if (b.bakedShapeSource == null && b.bakedShapeVertices == null) continue;
                    b.bakedShapeSource = null;
                    b.bakedShapeVertices = null;
                    b.bakedShapeTriangles = null;
                    b.bakedShapeInBoneSpace = false;
                    b.bakedShapeAnchorBone = null;
                    if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
                    continue;
                }

                bool upToDate = b.bakedShapeSource == mesh
                    && b.bakedShapeInBoneSpace == wantBoneSpace
                    && b.bakedShapeVersion == ShapeBakeVersion
                    && (!wantBoneSpace || b.bakedShapeAnchorBone == anchor)
                    && b.bakedShapeVertices != null && b.bakedShapeVertices.Length == mesh.vertexCount
                    && b.bakedShapeTriangles != null;
                if (upToDate) continue;

                if (wantBoneSpace)
                {
                    SnapshotSkinnedToBoneLocal(skinned, anchor, out b.bakedShapeVertices, out b.bakedShapeTriangles);
                    b.bakedShapeAnchorBone = anchor;
                }
                else
                {
                    b.bakedShapeVertices = mesh.vertices;
                    b.bakedShapeTriangles = mesh.triangles;
                    b.bakedShapeAnchorBone = null;
                }
                b.bakedShapeSource = mesh;
                b.bakedShapeInBoneSpace = wantBoneSpace;
                b.bakedShapeVersion = ShapeBakeVersion;
                // OnValidate edits made outside SerializedProperty are not tracked automatically
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
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

            foreach (var b in bones)
            {
                if (b == null || b.shapeRenderer == null) continue;
                Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);

                if (b.shapeRenderer is SkinnedMeshRenderer)
                {
                    // the constraint uses the anchor-space snapshot, so that is what must be drawn —
                    // the live skinned mesh would diverge from it the moment the pose changes
                    if (b.bakedShapeAnchorBone == null || !b.bakedShapeInBoneSpace) continue;
                    var v = b.bakedShapeVertices;
                    var tri = b.bakedShapeTriangles;
                    if (v == null || tri == null) continue;

                    b.bakedShapeAnchorBone.GetPositionAndRotation(out Vector3 bonePos, out Quaternion boneRot);
                    for (int k = 0; k + 2 < tri.Length; k += 3)
                    {
                        Vector3 v0 = bonePos + boneRot * v[tri[k]];
                        Vector3 v1 = bonePos + boneRot * v[tri[k + 1]];
                        Vector3 v2 = bonePos + boneRot * v[tri[k + 2]];
                        Gizmos.DrawLine(v0, v1);
                        Gizmos.DrawLine(v1, v2);
                        Gizmos.DrawLine(v2, v0);
                    }
                    continue;
                }

                Mesh mesh = b.shapeRenderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;

                Transform t = b.shapeRenderer.transform;
                Gizmos.DrawWireMesh(mesh, t.position, t.rotation, t.lossyScale);
            }

            if (mode != HonamiPhysicsMode.Simulation) return;

            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.bone == null || b.joint != HonamiPhysicsJoint.Hinge) continue;
                if (b.hingeAxis.sqrMagnitude < 1e-8f) continue;

                // the peg the bone turns on: if this line does not run through the ring, the axis is wrong
                Vector3 axis = b.bone.rotation * b.hingeAxis.normalized;
                float len = b.bone.localPosition.magnitude;
                if (len <= 1e-4f) len = 0.05f;

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(b.bone.position - axis * len, b.bone.position + axis * len);
            }

            if (_chains == null) return;
            Gizmos.color = Color.yellow;
            for (int c = 0; c < _chains.Length; c++)
            {
                var ps = _chains[c].particles;
                for (int i = 1; i < ps.Length; i++)
                {
                    Gizmos.DrawLine(ps[i - 1].simPos, ps[i].simPos);
                    Gizmos.DrawWireSphere(ps[i].simPos, 0.005f);
                }
            }
        }
#endif
    }
}
