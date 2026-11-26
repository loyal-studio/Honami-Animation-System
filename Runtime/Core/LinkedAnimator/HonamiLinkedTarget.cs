using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Describes which linked animators a <see cref="HonamiLinkedAnimator"/> command reaches.
    /// Combine a mode, an optional tag, an optional sphere and an optional cap; the default value
    /// (<see cref="All"/>) accepts everything.
    /// </summary>
    public readonly struct HonamiLinkedTarget
    {
        public readonly HonamiBroadcastTargetMode Mode;
        public readonly HonamiTagID Tag;
        public readonly Vector3 Origin;

        /// <summary>Sphere radius around <see cref="Origin"/>. Zero or less means no distance filter.</summary>
        public readonly float MaxDistance;

        /// <summary>Maximum animators to reach. Zero or less means no cap.</summary>
        public readonly int Limit;

        public HonamiLinkedTarget(
            HonamiBroadcastTargetMode mode,
            HonamiTagID tag = null,
            Vector3 origin = default,
            float maxDistance = 0f,
            int limit = 0)
        {
            Mode = mode;
            Tag = tag;
            Origin = origin;
            MaxDistance = maxDistance;
            Limit = limit;
        }

        /// <summary>Every linked animator.</summary>
        public static HonamiLinkedTarget All => default;

        /// <summary>Only animators parented under the brain.</summary>
        public static HonamiLinkedTarget Children => new(HonamiBroadcastTargetMode.ChildrenOnly);

        /// <summary>Only animators carrying a given linking tag.</summary>
        public static HonamiLinkedTarget ByTag(HonamiTagID tag) => new(HonamiBroadcastTargetMode.ByTag, tag);

        /// <summary>Only animators inside a sphere, optionally capped at <paramref name="limit"/> animators.</summary>
        public static HonamiLinkedTarget Within(Vector3 origin, float radius, int limit = 0)
            => new(HonamiBroadcastTargetMode.AllLinked, null, origin, radius, limit);

        /// <summary>Narrows an existing target to a sphere.</summary>
        public HonamiLinkedTarget WithinSphere(Vector3 origin, float radius, int limit = 0)
            => new(Mode, Tag, origin, radius, limit);

        /// <summary>Narrows an existing target to at most <paramref name="limit"/> animators.</summary>
        public HonamiLinkedTarget WithLimit(int limit) => new(Mode, Tag, Origin, MaxDistance, limit);

        /// <summary>
        /// True when <paramref name="animator"/> passes every filter and the cap has not been reached yet.
        /// </summary>
        public bool Accepts(HonamiAnimatorBase animator, Transform brainRoot, int alreadyReached)
        {
            if (animator == null) return false;
            if (Limit > 0 && alreadyReached >= Limit) return false;

            switch (Mode)
            {
                case HonamiBroadcastTargetMode.ChildrenOnly:
                    if (brainRoot == null || !animator.transform.IsChildOf(brainRoot)) return false;
                    break;

                case HonamiBroadcastTargetMode.ByTag:
                    if (Tag == null || animator.linkingTag != Tag) return false;
                    break;
            }

            if (MaxDistance > 0f
                && Vector3.SqrMagnitude(animator.transform.position - Origin) > MaxDistance * MaxDistance)
            {
                return false;
            }

            return true;
        }

        /// <summary>True once <paramref name="reached"/> animators satisfy the cap, so iteration can stop early.</summary>
        public bool IsSatisfied(int reached) => Limit > 0 && reached >= Limit;
    }
}
