using System.Collections.Generic;

namespace HonamiAnimationSystem.Runtime.Core
{
    public readonly struct HonamiLinkedAnimatorContext
    {
        public readonly HonamiLinkedAnimator Brain;
        public readonly IReadOnlyCollection<HonamiAnimatorBase> LinkedAnimators;

        /// <summary>
        /// Controller-backed subset of <see cref="LinkedAnimators"/>. Only these expose parameters and states.
        /// </summary>
        public readonly IReadOnlyList<HonamiAnimator> FullAnimators;
        public readonly float DeltaTime;
        public readonly float EventTime;

        public HonamiLinkedAnimatorContext(
            HonamiLinkedAnimator brain,
            IReadOnlyCollection<HonamiAnimatorBase> linkedAnimators,
            float deltaTime,
            float eventTime)
        {
            Brain = brain;
            LinkedAnimators = linkedAnimators;
            FullAnimators = brain != null ? brain.FullAnimators : null;
            DeltaTime = deltaTime;
            EventTime = eventTime;
        }
    }
}

