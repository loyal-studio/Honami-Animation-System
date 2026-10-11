using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>Where the inertializer is in the life of one Inertial transition.</summary>
    public enum HonamiInertializerPhase : byte
    {
        /// <summary>No residual. The job only records the visible pose history.</summary>
        Idle,
        /// <summary>The transition was requested: the next advancing frame stores the target pose and outputs the extrapolated source.</summary>
        CaptureOffset,
        /// <summary>The next advancing frame measures the target velocity and builds the residual.</summary>
        CaptureVelocity,
        /// <summary>The residual is added on top of the live target until the duration ends.</summary>
        Active
    }

    /// <summary>Persistent job state shared between the main thread and <see cref="HonamiInertializerJob"/>.</summary>
    public struct HonamiInertializerState
    {
        public HonamiInertializerPhase Phase;
        /// <summary>Visible frames recorded so far, capped at 2. Zero means there is no usable history.</summary>
        public byte HistoryDepth;
        /// <summary>Seconds between the previous and the current history pose.</summary>
        public float HistoryDelta;
        public float Elapsed;
        public float Duration;
        /// <summary>1 when this inertialization re-started while a capture was still pending: hold the visible pose instead of extrapolating again.</summary>
        public byte HoldInsteadOfExtrapolate;
    }

    /// <summary>
    /// Whole-output inertializer. Records the last two visible poses of the bound bones and, while a transition is
    /// active, adds a decaying residual (source offset and velocity) on top of the live target pose.
    /// </summary>
    [BurstCompile]
    public struct HonamiInertializerJob : IAnimationJob
    {
        [ReadOnly] public NativeArray<TransformStreamHandle> bones;
        public NativeArray<HonamiInertialPose> historyCurrent;
        public NativeArray<HonamiInertialPose> historyPrevious;
        public NativeArray<HonamiInertialPose> targetAtCapture;
        public NativeArray<HonamiInertialPoseResidual> residuals;
        public NativeArray<HonamiInertializerState> state;

        public void ProcessRootMotion(AnimationStream stream) { }

        private static bool PoseChanged(HonamiInertialPose a, HonamiInertialPose b)
        {
            return math.lengthsq(a.Position - b.Position) > 1e-10f ||
                   math.lengthsq(a.Scale - b.Scale) > 1e-10f ||
                   math.abs(math.dot(a.Rotation.value, b.Rotation.value)) < 0.9999999f;
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!state.IsCreated || state.Length == 0) return;

            HonamiInertializerState st = state[0];
            float dt = stream.deltaTime;
            bool advancing = math.isfinite(dt) && dt > HonamiInertialPoseMath.MinimumUsableDeltaSeconds;
            bool active = st.Phase != HonamiInertializerPhase.Idle;
            int depth = st.HistoryDepth;
            float historyDelta = st.HistoryDelta;
            float elapsed = st.Elapsed;

            for (int i = 0; i < bones.Length; i++)
            {
                TransformStreamHandle h = bones[i];
                if (!h.IsValid(stream)) continue;

                var target = new HonamiInertialPose(h.GetLocalPosition(stream), h.GetLocalRotation(stream), h.GetLocalScale(stream));
                HonamiInertialPose output = target;
                bool write = active;

                switch (st.Phase)
                {
                    case HonamiInertializerPhase.CaptureOffset:
                        if (!advancing)
                        {
                            // Hold the last visible pose until a frame that advances the target arrives.
                            if (depth > 0) output = historyCurrent[i];
                        }
                        else
                        {
                            if (depth >= 1 && st.HoldInsteadOfExtrapolate != 0)
                                output = historyCurrent[i];
                            else if (depth >= 2)
                                output = HonamiInertialPoseMath.Extrapolate(historyCurrent[i], historyPrevious[i], historyDelta, dt);
                            else if (depth == 1)
                                output = historyCurrent[i];
                            targetAtCapture[i] = target;
                        }
                        break;

                    case HonamiInertializerPhase.CaptureVelocity:
                        if (!advancing)
                        {
                            output = historyCurrent[i];
                        }
                        else
                        {
                            HonamiInertialPoseResidual residual = HonamiInertialPoseMath.CaptureResidual(
                                historyCurrent[i], historyPrevious[i], historyDelta,
                                targetAtCapture[i], target, dt);
                            residuals[i] = residual;
                            output = HonamiInertialPoseMath.ApplyResidual(target, residual, dt, st.Duration);
                        }
                        break;

                    case HonamiInertializerPhase.Active:
                        output = HonamiInertialPoseMath.ApplyResidual(target, residuals[i], elapsed + (advancing ? dt : 0f), st.Duration);
                        break;
                }

                if (write)
                {
                    h.SetLocalPosition(stream, output.Position);
                    h.SetLocalRotation(stream, output.Rotation);
                    h.SetLocalScale(stream, output.Scale);
                }

                if (advancing)
                {
                    historyPrevious[i] = depth == 0 ? output : historyCurrent[i];
                    historyCurrent[i] = output;
                }
                else
                {
                    // Time did not advance. If the pose is unchanged (paused) the recorded velocity stays valid;
                    // if it jumped (a cut followed by Tick(0)) the jump must not be read as velocity, so the
                    // velocity is dropped: the new animation's real velocity is unknown until it advances.
                    HonamiInertialPose old = historyCurrent[i];
                    historyCurrent[i] = output;
                    if (depth == 0 || PoseChanged(old, output)) historyPrevious[i] = output;
                }
            }

            if (advancing)
            {
                st.HistoryDelta = dt;
                st.HistoryDepth = (byte)math.min(depth + 1, 2);

                switch (st.Phase)
                {
                    case HonamiInertializerPhase.CaptureOffset:
                        st.Phase = HonamiInertializerPhase.CaptureVelocity;
                        st.Elapsed = 0f;
                        break;
                    case HonamiInertializerPhase.CaptureVelocity:
                        st.Phase = HonamiInertializerPhase.Active;
                        st.Elapsed = dt;
                        break;
                    case HonamiInertializerPhase.Active:
                        st.Elapsed = elapsed + dt;
                        break;
                }

                if (st.Phase == HonamiInertializerPhase.Active && st.Elapsed >= st.Duration)
                    st.Phase = HonamiInertializerPhase.Idle;
            }

            state[0] = st;
        }
    }

    /// <summary>
    /// Main-thread owner of the inertializer script playable. Created only for controllers that contain
    /// Inertial transitions, so every other controller keeps the exact graph it had before.
    /// </summary>
    public sealed class HonamiInertializationProcessor : IDisposable
    {
        private NativeArray<TransformStreamHandle> _bones;
        private NativeArray<HonamiInertialPose> _historyCurrent;
        private NativeArray<HonamiInertialPose> _historyPrevious;
        private NativeArray<HonamiInertialPose> _targetAtCapture;
        private NativeArray<HonamiInertialPoseResidual> _residuals;
        private NativeArray<HonamiInertializerState> _state;

        private AnimationScriptPlayable _playable;
        private bool _initialized;

        public bool IsCreated => _initialized && _state.IsCreated;
        public bool IsActive => IsCreated && _state[0].Phase != HonamiInertializerPhase.Idle;
        internal HonamiInertializerState State => _state.IsCreated ? _state[0] : default;
        internal int BoneCount => _bones.IsCreated ? _bones.Length : 0;

        /// <summary>True when any state of the controller has an Inertial transition with a positive duration.</summary>
        public static bool ControllerUsesInertial(HonamiRuntimeController controller, HonamiState[] states, int count)
        {
            if (controller == null || states == null) return false;

            for (int i = 0; i < count && i < states.Length; i++)
            {
                var state = states[i];
                if (state == null) continue;

                var transitions = controller.GetTransitions(state);
                if (transitions == null) continue;

                for (int t = 0; t < transitions.Count; t++)
                {
                    var tr = transitions[t];
                    if (tr != null && tr.type == HonamiTransitionType.Inertial && tr.duration > 0f)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Binds the bones and inserts the inertializer between the current output source and the output.
        /// <paramref name="avatarBones"/> restricts the bones to the avatar's driven set when provided.
        /// </summary>
        public void Initialize(Animator animator, PlayableGraph graph, IReadOnlyList<Transform> avatarBones = null)
        {
            Dispose();
            _initialized = false;
            if (animator == null || !graph.IsValid()) return;

            var output = graph.GetOutput(0);
            if (!output.IsPlayableOutputOfType<AnimationPlayableOutput>()) return;

            var transforms = CollectBones(animator, avatarBones);
            int count = transforms.Count;

            _bones = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _historyCurrent = new NativeArray<HonamiInertialPose>(count, Allocator.Persistent);
            _historyPrevious = new NativeArray<HonamiInertialPose>(count, Allocator.Persistent);
            _targetAtCapture = new NativeArray<HonamiInertialPose>(count, Allocator.Persistent);
            _residuals = new NativeArray<HonamiInertialPoseResidual>(count, Allocator.Persistent);
            _state = new NativeArray<HonamiInertializerState>(1, Allocator.Persistent);

            for (int i = 0; i < count; i++)
                _bones[i] = animator.BindStreamTransform(transforms[i]);

            var job = new HonamiInertializerJob
            {
                bones = _bones,
                historyCurrent = _historyCurrent,
                historyPrevious = _historyPrevious,
                targetAtCapture = _targetAtCapture,
                residuals = _residuals,
                state = _state
            };

            _playable = AnimationScriptPlayable.Create(graph, job, 1);
            Playable source = output.GetSourcePlayable();
            if (source.IsValid())
            {
                // The script playable was created with exactly one input slot; fill that one.
                graph.Connect(source, 0, _playable, 0);
                _playable.SetInputWeight(0, 1f);
            }
            output.SetSourcePlayable(_playable);

            _initialized = true;
        }

        /// <summary>Starts (or rebases) an inertialization lasting <paramref name="duration"/> seconds.</summary>
        public void Begin(float duration)
        {
            if (!IsCreated || !(duration > 0f)) return;

            var st = _state[0];
            // Nothing visible has been recorded yet, so there is no source pose to decay from.
            if (st.HistoryDepth == 0)
            {
                st.Phase = HonamiInertializerPhase.Idle;
                _state[0] = st;
                return;
            }

            // Re-triggered before the previous capture finished (a held condition firing every tick): extrapolating
            // again each time would slide the pose away linearly, so hold the visible pose instead.
            st.HoldInsteadOfExtrapolate = (byte)(st.Phase == HonamiInertializerPhase.CaptureOffset ||
                                                 st.Phase == HonamiInertializerPhase.CaptureVelocity ? 1 : 0);
            st.Phase = HonamiInertializerPhase.CaptureOffset;
            st.Elapsed = 0f;
            st.Duration = duration;
            _state[0] = st;
        }

        /// <summary>Drops any running residual, producing a hard cut to the target pose.</summary>
        public void Cancel()
        {
            if (!IsCreated) return;

            var st = _state[0];
            if (st.Phase == HonamiInertializerPhase.Idle) return;
            st.Phase = HonamiInertializerPhase.Idle;
            st.Elapsed = 0f;
            _state[0] = st;
        }

        /// <summary>Forgets the recorded pose history, e.g. after evaluations that are not part of the visible timeline.</summary>
        public void InvalidateHistory()
        {
            if (!IsCreated) return;

            var st = _state[0];
            st.HistoryDepth = 0;
            st.HistoryDelta = 0f;
            _state[0] = st;
        }

        private static List<Transform> CollectBones(Animator animator, IReadOnlyList<Transform> avatarBones)
        {
            Transform root = animator.transform;
            var result = new List<Transform>();
            var seen = new HashSet<Transform>();

            void Add(Transform t)
            {
                if (t == null || t == root || !t.IsChildOf(root)) return;
                if (seen.Add(t)) result.Add(t);
            }

            if (avatarBones != null && avatarBones.Count > 0)
            {
                for (int i = 0; i < avatarBones.Count; i++) Add(avatarBones[i]);
                return result;
            }

            if (animator.isHuman)
            {
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    Add(animator.GetBoneTransform((HumanBodyBones)i));
                if (result.Count > 0) return result;
            }

            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var smr = renderers[r];
                AddWithAncestors(smr.rootBone);
                var skinBones = smr.bones;
                for (int b = 0; b < skinBones.Length; b++) AddWithAncestors(skinBones[b]);
            }

            if (result.Count > 0) return result;

            // No skinned meshes (props, simple rigs): every descendant.
            var all = new List<Transform>();
            root.GetComponentsInChildren(true, all);
            for (int i = 0; i < all.Count; i++) Add(all[i]);
            return result;

            void AddWithAncestors(Transform t)
            {
                if (t == null) return;
                // Parents first so the bone order follows the hierarchy.
                if (t != root && t.parent != null && t.parent != root) AddWithAncestors(t.parent);
                Add(t);
            }
        }

        public void Dispose()
        {
            if (_bones.IsCreated) _bones.Dispose();
            if (_historyCurrent.IsCreated) _historyCurrent.Dispose();
            if (_historyPrevious.IsCreated) _historyPrevious.Dispose();
            if (_targetAtCapture.IsCreated) _targetAtCapture.Dispose();
            if (_residuals.IsCreated) _residuals.Dispose();
            if (_state.IsCreated) _state.Dispose();
            _initialized = false;
        }
    }
}
