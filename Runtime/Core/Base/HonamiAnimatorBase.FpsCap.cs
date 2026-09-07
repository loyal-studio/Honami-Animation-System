using System;
using System.Collections.Generic;
using UnityEngine;
namespace HonamiAnimationSystem.Runtime.Core
{
    public abstract partial class HonamiAnimatorBase
    {
        private struct FpsCapPose
        {
            public Transform Target;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        private FpsCapPose[] _fpsPoseA;
        private FpsCapPose[] _fpsPoseB;
        private bool _fpsPoseReady;
        private bool _fpsTransformsDirty = true;
        private readonly List<Transform> _fpsTransforms = new List<Transform>();

        /// <summary>
        /// Call after adding/removing child transforms at runtime (e.g. equipping items) on a
        /// character using fpsCap + fpsCapInterpolate, so the interpolation buffers pick them up.
        /// </summary>
        public void RefreshFpsCapHierarchy() => _fpsTransformsDirty = true;

        private void EnsureFpsCapPoseBuffers()
        {
            if (!_fpsTransformsDirty) return;
            _fpsTransformsDirty = false;

            GetComponentsInChildren<Transform>(true, _fpsTransforms);
            int count = _fpsTransforms.Count;

            if (_fpsPoseA == null || _fpsPoseA.Length != count)
                _fpsPoseA = new FpsCapPose[count];

            if (_fpsPoseB == null || _fpsPoseB.Length != count)
                _fpsPoseB = new FpsCapPose[count];

            for (int i = 0; i < count; i++)
            {
                _fpsPoseA[i].Target = _fpsTransforms[i];
                _fpsPoseB[i].Target = _fpsTransforms[i];
            }
        }

        private void SnapshotPose(FpsCapPose[] buffer)
        {
            if (buffer == null) return;

            var span = new Span<FpsCapPose>(buffer);
            for (int i = 0; i < span.Length; i++)
            {
                Transform t = span[i].Target;
                if (t == null) continue;
                span[i].LocalPosition = t.localPosition;
                span[i].LocalRotation = t.localRotation;
                span[i].LocalScale    = t.localScale;
            }
        }

        private void ApplyInterpolatedPose(float alpha)
        {
            if (_fpsPoseA == null || _fpsPoseB == null) return;

            int count = Mathf.Min(_fpsPoseA.Length, _fpsPoseB.Length);
            var spanA = new ReadOnlySpan<FpsCapPose>(_fpsPoseA, 0, count);
            var spanB = new ReadOnlySpan<FpsCapPose>(_fpsPoseB, 0, count);

            for (int i = 0; i < count; i++)
            {
                Transform t = spanB[i].Target;
                if (t == null) continue;

                t.localPosition = Vector3.LerpUnclamped(spanA[i].LocalPosition, spanB[i].LocalPosition, alpha);
                t.localRotation = Quaternion.SlerpUnclamped(spanA[i].LocalRotation, spanB[i].LocalRotation, alpha);
                t.localScale    = Vector3.LerpUnclamped(spanA[i].LocalScale,    spanB[i].LocalScale,    alpha);
            }
        }

        protected void TickWithFpsCap(double rawDeltaTime)
        {
            if (!fpsCap)
            {
                Tick(rawDeltaTime);
                return;
            }

            double frameInterval = 1.0 / targetFPS;
            _fpsAccumulator += rawDeltaTime;

            if (_fpsAccumulator >= frameInterval)
            {
                if (fpsCapInterpolate)
                {
                    EnsureFpsCapPoseBuffers();

                    if (_fpsPoseReady)
                        SnapshotPose(_fpsPoseA);

                    double tickDelta = _fpsAccumulator;
                    _fpsAccumulator = 0.0;
                    Tick(tickDelta);

                    SnapshotPose(_fpsPoseB);

                    if (!_fpsPoseReady)
                    {
                        SnapshotPose(_fpsPoseA);
                        _fpsPoseReady = true;
                    }
                }
                else
                {
                    double tickDelta = _fpsAccumulator;
                    _fpsAccumulator = 0.0;
                    Tick(tickDelta);
                }
            }
            else if (fpsCapInterpolate && _fpsPoseReady)
            {
                float alpha = (float)(_fpsAccumulator / frameInterval);
                ApplyInterpolatedPose(alpha);
            }
        }

        protected void InvalidateFpsCapInterpolation() => _fpsPoseReady = false;

        protected void ResetFpsCapState()
        {
            _fpsAccumulator = 0.0;
            _fpsPoseReady = false;
            _fpsPoseA = null;
            _fpsPoseB = null;
            _fpsTransformsDirty = true;
        }
    }
}
