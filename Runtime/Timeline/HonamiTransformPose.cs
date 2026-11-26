using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    public sealed class HonamiTransformPose
    {
        private readonly Transform[] _transforms;
        private readonly Vector3[] _positions;
        private readonly Quaternion[] _rotations;
        private readonly Vector3[] _scales;

        public HonamiTransformPose(Transform root)
        {
            Root = root;
            _transforms = root != null ? root.GetComponentsInChildren<Transform>(true) : System.Array.Empty<Transform>();
            _positions = new Vector3[_transforms.Length];
            _rotations = new Quaternion[_transforms.Length];
            _scales = new Vector3[_transforms.Length];
        }

        public Transform Root { get; }
        public int Count => _transforms.Length;

        public void Capture()
        {
            for (int i = 0; i < _transforms.Length; i++)
            {
                var t = _transforms[i];
                if (t == null) continue;
                t.GetLocalPositionAndRotation(out _positions[i], out _rotations[i]);
                _scales[i] = t.localScale;
            }
        }

        // weight 0 restores the captured pose, weight 1 keeps the current one.
        public void BlendFromCaptured(float weight)
        {
            if (weight >= 1f) return;
            weight = Mathf.Max(0f, weight);
            for (int i = 0; i < _transforms.Length; i++)
            {
                var t = _transforms[i];
                if (t == null) continue;
                t.GetLocalPositionAndRotation(out var position, out var rotation);
                t.SetLocalPositionAndRotation(
                    Vector3.LerpUnclamped(_positions[i], position, weight),
                    Quaternion.SlerpUnclamped(_rotations[i], rotation, weight));
                t.localScale = Vector3.LerpUnclamped(_scales[i], t.localScale, weight);
            }
        }
    }
}
