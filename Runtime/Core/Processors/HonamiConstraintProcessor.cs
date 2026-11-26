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
    [BurstCompile]
    public struct HonamiConstraintJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> bones;
        [ReadOnly] public NativeArray<float3>     baselinesPos;
        [ReadOnly] public NativeArray<quaternion> baselinesRot;
        [ReadOnly] public NativeArray<float3>     baselinesScl;

        public float lockPosX; public float lockPosY; public float lockPosZ;
        public float lockRotX; public float lockRotY; public float lockRotZ;
        public float lockSclX; public float lockSclY; public float lockSclZ;

        public void ProcessAnimation(AnimationStream stream)
        {
            float maxRot = math.max(lockRotX, math.max(lockRotY, lockRotZ));
            bool  doPos  = lockPosX > 0.001f || lockPosY > 0.001f || lockPosZ > 0.001f;
            bool  doRot  = maxRot > 0.001f;
            bool  doScl  = lockSclX > 0.001f || lockSclY > 0.001f || lockSclZ > 0.001f;

            if (!doPos && !doRot && !doScl) return;

            for (int i = 0; i < bones.Length; i++)
            {
                var h = bones[i];

                if (!h.IsValid(stream)) continue;

                if (doPos)
                {
                    float3 p = h.GetLocalPosition(stream);
                    float3 b = baselinesPos[i];
                    if (lockPosX > 0.001f) p.x = math.lerp(p.x, b.x, lockPosX);
                    if (lockPosY > 0.001f) p.y = math.lerp(p.y, b.y, lockPosY);
                    if (lockPosZ > 0.001f) p.z = math.lerp(p.z, b.z, lockPosZ);
                    h.SetLocalPosition(stream, p);
                }

                if (doRot)
                {
                    quaternion r = h.GetLocalRotation(stream);
                    quaternion b = baselinesRot[i];
                    h.SetLocalRotation(stream, math.slerp(r, b, maxRot));
                }

                if (doScl)
                {
                    float3 s = h.GetLocalScale(stream);
                    float3 b = baselinesScl[i];
                    if (lockSclX > 0.001f) s.x = math.lerp(s.x, b.x, lockSclX);
                    if (lockSclY > 0.001f) s.y = math.lerp(s.y, b.y, lockSclY);
                    if (lockSclZ > 0.001f) s.z = math.lerp(s.z, b.z, lockSclZ);
                    h.SetLocalScale(stream, s);
                }
            }
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }

    public sealed class HonamiConstraintProcessor : IDisposable
    {
        private NativeArray<TransformStreamHandle> _bones;
        private NativeArray<float3>               _baselinesPos;
        private NativeArray<quaternion>            _baselinesRot;
        private NativeArray<float3>               _baselinesScl;
        private Transform[]                        _transforms;

        private AnimationScriptPlayable            _constraintPlayable;
        private bool                               _initialized;

        private float _lockPosX, _lockPosY, _lockPosZ;
        private float _lockRotX, _lockRotY, _lockRotZ;
        private float _lockSclX, _lockSclY, _lockSclZ;

        private float _pushedLockPosX = float.NaN, _pushedLockPosY, _pushedLockPosZ;
        private float _pushedLockRotX, _pushedLockRotY, _pushedLockRotZ;
        private float _pushedLockSclX, _pushedLockSclY, _pushedLockSclZ;

        public bool AnyLockActive =>
            _lockPosX > 0.001f || _lockPosY > 0.001f || _lockPosZ > 0.001f ||
            _lockRotX > 0.001f || _lockRotY > 0.001f || _lockRotZ > 0.001f ||
            _lockSclX > 0.001f || _lockSclY > 0.001f || _lockSclZ > 0.001f;

        public void Initialize(Animator animator, PlayableGraph graph, Playable rootMixer, HonamiAvatar avatar = null)
        {
            Dispose();
            _initialized = false;
            _pushedLockPosX = float.NaN;
            if (animator == null || !graph.IsValid()) return;

            if (avatar != null)
            {
                var validList = new List<Transform>();
                foreach (var entry in avatar.bones)
                {
                    if (!entry.enabled) continue;
                    Transform t = string.IsNullOrEmpty(entry.bonePath) ? animator.transform : animator.transform.Find(entry.bonePath);
                    if (t != null) validList.Add(t);
                }
                _transforms = validList.ToArray();
            }
            else
            {
                var transformList = new List<Transform>();
                animator.GetComponentsInChildren<Transform>(true, transformList);
                _transforms = transformList.ToArray();
            }

            int count = _transforms.Length;
            _bones        = new NativeArray<TransformStreamHandle>(count, Allocator.Persistent);
            _baselinesPos = new NativeArray<float3>(count, Allocator.Persistent);
            _baselinesRot = new NativeArray<quaternion>(count, Allocator.Persistent);
            _baselinesScl = new NativeArray<float3>(count, Allocator.Persistent);

            for (int i = 0; i < count; i++)
            {
                var t = _transforms[i];
                _bones[i]        = animator.BindStreamTransform(t);
                _baselinesPos[i] = t.localPosition;
                _baselinesRot[i] = t.localRotation;
                _baselinesScl[i] = t.localScale;
            }

            var job = new HonamiConstraintJob
            {
                bones        = _bones,
                baselinesPos = _baselinesPos,
                baselinesRot = _baselinesRot,
                baselinesScl = _baselinesScl
            };

            _constraintPlayable = AnimationScriptPlayable.Create(graph, job, 1);

            var output = graph.GetOutput(0);
            if (output.IsPlayableOutputOfType<AnimationPlayableOutput>())
            {
                _constraintPlayable.AddInput(rootMixer, 0, 1f);
                output.SetSourcePlayable(_constraintPlayable);
            }

            _initialized = true;
        }

        // The job already holds these arrays and reads the same native memory, so writing the baselines
        // is all this needs - re-pushing the job struct would only marshal the identical handles back.
        public void UpdateBaselines()
        {
            if (!_initialized) return;

            for (int i = 0; i < _transforms.Length; i++)
            {
                var t = _transforms[i];
                if (t == null) continue;
                t.GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localRotation);
                _baselinesPos[i] = localPosition;
                _baselinesRot[i] = localRotation;
                _baselinesScl[i] = t.localScale;
            }
        }

        public void Apply(
            HonamiState[]                 states,
            int                           statesCount,
            AnimationLayerMixerPlayable   layerMixer,
            List<AnimationMixerPlayable>  layerMixers,
            HonamiLayerState[]            layerStates,
            int                           transientPortIndex)
        {
            if (!_initialized || states == null) return;

            _lockPosX = _lockPosY = _lockPosZ = 0f;
            _lockRotX = _lockRotY = _lockRotZ = 0f;
            _lockSclX = _lockSclY = _lockSclZ = 0f;

            int layerCount = layerMixers.Count;
            for (int i = 0; i < layerCount; i++)
            {
                float layerWeight = layerMixer.GetInputWeight(i);
                if (layerWeight < 0.001f) continue;

                float px = 0, py = 0, pz = 0;
                float rx = 0, ry = 0, rz = 0;
                float sx = 0, sy = 0, sz = 0;

                var mixer = layerMixers[i];
                AccumulateStateConstraint(states, statesCount, mixer, i, layerStates[i].CurrentStateIndex,  layerStates[i].TransientStateIndex, transientPortIndex, ref px, ref py, ref pz, ref rx, ref ry, ref rz, ref sx, ref sy, ref sz);
                AccumulateStateConstraint(states, statesCount, mixer, i, layerStates[i].PreviousStateIndex, layerStates[i].TransientStateIndex, transientPortIndex, ref px, ref py, ref pz, ref rx, ref ry, ref rz, ref sx, ref sy, ref sz);

                _lockPosX += layerWeight * px;
                _lockPosY += layerWeight * py;
                _lockPosZ += layerWeight * pz;
                _lockRotX += layerWeight * rx;
                _lockRotY += layerWeight * ry;
                _lockRotZ += layerWeight * rz;
                _lockSclX += layerWeight * sx;
                _lockSclY += layerWeight * sy;
                _lockSclZ += layerWeight * sz;
            }

            _lockPosX = math.min(_lockPosX, 1f);
            _lockPosY = math.min(_lockPosY, 1f);
            _lockPosZ = math.min(_lockPosZ, 1f);
            _lockRotX = math.min(_lockRotX, 1f);
            _lockRotY = math.min(_lockRotY, 1f);
            _lockRotZ = math.min(_lockRotZ, 1f);
            _lockSclX = math.min(_lockSclX, 1f);
            _lockSclY = math.min(_lockSclY, 1f);
            _lockSclZ = math.min(_lockSclZ, 1f);

            if (_lockPosX == _pushedLockPosX && _lockPosY == _pushedLockPosY && _lockPosZ == _pushedLockPosZ &&
                _lockRotX == _pushedLockRotX && _lockRotY == _pushedLockRotY && _lockRotZ == _pushedLockRotZ &&
                _lockSclX == _pushedLockSclX && _lockSclY == _pushedLockSclY && _lockSclZ == _pushedLockSclZ)
                return;

            _pushedLockPosX = _lockPosX; _pushedLockPosY = _lockPosY; _pushedLockPosZ = _lockPosZ;
            _pushedLockRotX = _lockRotX; _pushedLockRotY = _lockRotY; _pushedLockRotZ = _lockRotZ;
            _pushedLockSclX = _lockSclX; _pushedLockSclY = _lockSclY; _pushedLockSclZ = _lockSclZ;

            var job = _constraintPlayable.GetJobData<HonamiConstraintJob>();
            job.lockPosX = _lockPosX; job.lockPosY = _lockPosY; job.lockPosZ = _lockPosZ;
            job.lockRotX = _lockRotX; job.lockRotY = _lockRotY; job.lockRotZ = _lockRotZ;
            job.lockSclX = _lockSclX; job.lockSclY = _lockSclY; job.lockSclZ = _lockSclZ;
            _constraintPlayable.SetJobData(job);
        }

        private static void AccumulateStateConstraint(
            HonamiState[]                states,
            int                          statesCount,
            AnimationMixerPlayable       mixer,
            int                          layerIdx,
            int                          statePortIdx,
            int                          transientStateIndex,
            int                          transientPortIndex,
            ref float px, ref float py, ref float pz,
            ref float rx, ref float ry, ref float rz,
            ref float sx, ref float sy, ref float sz)
        {
            if (statePortIdx < 0) return;
            float weight = mixer.GetInputWeight(statePortIdx);
            if (weight < 0.001f) return;

            int realIdx = (statePortIdx == transientPortIndex) ? transientStateIndex : statePortIdx;
            if (realIdx < 0 || realIdx >= statesCount) return;

            var sc = states[realIdx].constraints;
            if (sc.lockPositionX) px += weight;
            if (sc.lockPositionY) py += weight;
            if (sc.lockPositionZ) pz += weight;
            if (sc.lockRotationX) rx += weight;
            if (sc.lockRotationY) ry += weight;
            if (sc.lockRotationZ) rz += weight;
            if (sc.lockScaleX)    sx += weight;
            if (sc.lockScaleY)    sy += weight;
            if (sc.lockScaleZ)    sz += weight;
        }

        public void Dispose()
        {
            if (_bones.IsCreated)        _bones.Dispose();
            if (_baselinesPos.IsCreated) _baselinesPos.Dispose();
            if (_baselinesRot.IsCreated) _baselinesRot.Dispose();
            if (_baselinesScl.IsCreated) _baselinesScl.Dispose();
        }
    }
}

