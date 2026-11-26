using UnityEngine;
using UnityEngine.Animations;

namespace HonamiAnimationSystem.Runtime.Core
{
    public sealed class HonamiClipTrack
    {
        internal struct Slot
        {
            public AnimationClip Clip;
            public AnimationClipPlayable Playable;
            public double Time;
            public float Weight;
        }

        internal Slot[] _slots = System.Array.Empty<Slot>();
        internal int _count;
        private HonamiAnimatorBase _animator;

        internal HonamiClipTrack(HonamiAnimatorBase animator) => _animator = animator;

        public HonamiAnimatorBase Animator => _animator;
        public int Count => _count;
        public bool IsAttached => _animator != null && _animator.IsClipTrackAttached;

        // Changing the slot count rebinds the animator, so callers should size the track once up front.
        public void SetCount(int count)
        {
            if (_animator == null) return;

            count = Mathf.Max(0, count);
            if (count == _count) return;

            if (count > _slots.Length)
                System.Array.Resize(ref _slots, Mathf.Max(count, _slots.Length * 2));

            for (int i = count; i < _count; i++)
            {
                _animator.DestroyClipTrackPlayable(ref _slots[i]);
                _slots[i] = default;
            }

            _count = count;
            _animator.OnClipTrackLayoutChanged();
        }

        public void Set(int index, AnimationClip clip, double time, float weight)
        {
            if (_animator == null || (uint)index >= (uint)_count) return;

            ref Slot slot = ref _slots[index];
            if (slot.Clip != clip)
            {
                _animator.DestroyClipTrackPlayable(ref slot);
                slot.Clip = clip;
                _animator.OnClipTrackLayoutChanged();
            }

            slot.Time = time;
            slot.Weight = clip != null ? Mathf.Clamp01(weight) : 0f;
            _animator.OnClipTrackValuesChanged();
        }

        public void Apply()
        {
            if (_animator != null) _animator.ApplyClipTracks();
        }

        public void Release()
        {
            if (ReferenceEquals(_animator, null)) return;

            HonamiAnimatorBase animator = _animator;
            _animator = null;
            if (animator != null) animator.ReleaseClipTrack(this);
        }
    }
}
