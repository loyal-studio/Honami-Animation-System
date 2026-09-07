using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public abstract partial class HonamiAnimatorBase
    {
        protected struct HonamiTransformPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        protected HonamiTransformPose[] _initialPose;
        private bool _hasInitialPose;

        private HonamiTransformPose[] _globalWeightPoses;
        private bool _globalWeightPosesDirty = true;
        private int _globalWeightDrivenVersion = -1;
        private object _globalWeightPosesSource;

        /// <summary>
        /// True after Honami captured a runtime initial pose snapshot for this hierarchy.
        /// </summary>
        public bool HasInitialPose => _hasInitialPose;

        /// <summary>
        /// Captures the current local transform pose of this Honami hierarchy.
        /// Call this manually if another setup script changes the rig before Honami should define its rest pose.
        /// </summary>
        public void CaptureInitialPose()
        {
            var transforms = new List<Transform>();
            GetComponentsInChildren<Transform>(true, transforms);
            int skipRoot = includeRootTransformInInitialPose ? 0 : 1;
            int poseCount = Mathf.Max(0, transforms.Count - skipRoot);

            _initialPose = new HonamiTransformPose[poseCount];

            for (int i = 0; i < poseCount; i++)
            {
                Transform target = transforms[i + skipRoot];
                _initialPose[i] = new HonamiTransformPose
                {
                    Transform = target,
                    LocalPosition = target.localPosition,
                    LocalRotation = target.localRotation,
                    LocalScale = target.localScale
                };
            }

            _hasInitialPose = true;
            _globalWeightPosesDirty = true;
        }

        /// <summary>
        /// Restores the last captured initial pose snapshot.
        /// </summary>
        public void RestoreInitialPose()
        {
            if (!_hasInitialPose || _initialPose == null) return;

            for (int i = 0; i < _initialPose.Length; i++)
            {
                HonamiTransformPose pose = _initialPose[i];
                if (pose.Transform == null) continue;

                pose.Transform.localPosition = pose.LocalPosition;
                pose.Transform.localRotation = pose.LocalRotation;
                pose.Transform.localScale = pose.LocalScale;
            }
        }

        protected void RestoreInitialPoseIfIdle()
        {
            if (!restoreInitialPoseWhenIdle || !_hasInitialPose || !IsGraphIdle()) return;
            RestoreInitialPose();
        }

        protected void ApplyGlobalWeightBlend()
        {
            if (globalWeightMode != HonamiGlobalWeightMode.Init) return;
            if (GlobalWeight >= 0.9999f || !_hasInitialPose || _initialPose == null) return;

            HonamiTransformPose[] poses = ResolveGlobalWeightPoses();
            if (poses == null) return;

            float toRest = 1f - GlobalWeight;
            for (int i = 0; i < poses.Length; i++)
            {
                HonamiTransformPose pose = poses[i];
                if (pose.Transform == null) continue;

                pose.Transform.localPosition = Vector3.Lerp(pose.Transform.localPosition, pose.LocalPosition, toRest);
                pose.Transform.localRotation = Quaternion.Slerp(pose.Transform.localRotation, pose.LocalRotation, toRest);
                pose.Transform.localScale = Vector3.Lerp(pose.Transform.localScale, pose.LocalScale, toRest);
            }
        }

        protected virtual IReadOnlyList<Transform> GetDrivenTransforms(out int version)
        {
            version = -1;
            return null;
        }

        // Scope the weight blend to replacer-resolved driven bones, else it drags bones owned by another animator (e.g. FPS arms under the camera) toward rest.
        private HonamiTransformPose[] ResolveGlobalWeightPoses()
        {
            var driven = GetDrivenTransforms(out int drivenVersion);

            if (!_globalWeightPosesDirty && _globalWeightDrivenVersion == drivenVersion && ReferenceEquals(_globalWeightPosesSource, driven))
                return _globalWeightPoses;

            _globalWeightPosesDirty = false;
            _globalWeightDrivenVersion = drivenVersion;
            _globalWeightPosesSource = driven;

            int drivenCount = driven != null ? driven.Count : -1;

            // No avatar system: keep legacy whole-hierarchy blend. With an avatar, scope strictly to its driven bones (empty while rebinding => relax nothing rather than the arms).
            if (driven == null)
            {
                _globalWeightPoses = _initialPose;
                return _globalWeightPoses;
            }

            if (drivenCount == 0)
            {
                _globalWeightPoses = System.Array.Empty<HonamiTransformPose>();
                return _globalWeightPoses;
            }

            var drivenSet = new HashSet<Transform>(driven);
            var scoped = new List<HonamiTransformPose>(drivenCount);
            for (int i = 0; i < _initialPose.Length; i++)
            {
                if (drivenSet.Contains(_initialPose[i].Transform))
                    scoped.Add(_initialPose[i]);
            }

            _globalWeightPoses = scoped.ToArray();
            return _globalWeightPoses;
        }
    }
}
