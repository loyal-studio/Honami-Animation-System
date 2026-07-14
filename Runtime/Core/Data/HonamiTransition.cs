using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Defines the transition blending strategy used between Honami states.
    /// </summary>
    public enum HonamiTransitionType
    {
        Standard,
        Victim,
        Smart,
        Inertial
    }

    /// <summary>
    /// Defines which side of a transition is treated as the victim for special blending rules.
    /// </summary>
    public enum HonamiVictimMode
    {
        None,
        Source,
        Destination
    }

    /// <summary>
    /// Defines which side of a transition holds its pose while the blend runs.
    /// </summary>
    public enum HonamiTransitionFreezeMode
    {
        None,
        Destination,
        Source
    }

    /// <summary>
    /// Defines the easing applied to the transition blend weight when no custom curve is used.
    /// </summary>
    public enum HonamiTransitionEase
    {
        Linear,
        InSine, OutSine, InOutSine,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        InQuart, OutQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, OutExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InBack, OutBack, InOutBack,
        InElastic, OutElastic, InOutElastic,
        InBounce, OutBounce, InOutBounce
    }

    /// <summary>
    /// Defines how a transition condition compares a parameter value.
    /// </summary>
    public enum HonamiConditionMode
    {
        If,
        IfNot,
        Greater,
        Less,
        Equals,
        NotEqual
    }

    /// <summary>
    /// Serialized condition used to decide whether a transition can fire.
    /// </summary>
    [Serializable]
    public sealed class HonamiCondition
    {
        public string parameter;
        public HonamiConditionMode mode;
        public float threshold;
    }

    /// <summary>
    /// Serialized transition definition connecting one Honami state to another.
    /// </summary>
    [Serializable]
    public sealed class HonamiTransition
    {
        public string id;
        public string targetStateGuid;
        public HonamiTransitionType type = HonamiTransitionType.Standard;

        [Tooltip("Transition duration in seconds.")]
        public float duration = 0.25f;

        [Header("Victim Settings")]
        public HonamiVictimMode victimMode = HonamiVictimMode.None;

        [Tooltip("Playback speed multiplier for the 'victim' state during transition.")]
        public float sacrificeSpeedMultiplier = 1f;

        [Tooltip("If true, uses a custom curve to define how the victim state's weight drops.")]
        public bool useCustomVictimCurve = false;
        public AnimationCurve victimWeightCurve = AnimationCurve.Linear(0, 1, 1, 0);

        [Tooltip("If true, the victim's weight will drop significantly faster (Quadratic) if no custom curve is used.")]
        public bool acceleratedWeightDrop = false;

        [Tooltip("Higher priority transitions can interrupt lower priority ones even if 'canInterrupt' is false.")]
        public int priority = 0;

        [Tooltip("If true, this transition will 'sacrifice' (instantly kill) any other active transitions on this layer.")]
        public bool sacrificeExisting = false;

        [Header("Smart Settings")]
        [Tooltip("If true, the transition duration will scale based on the remaining time of the current state.")]
        public bool adaptiveDuration = false;

        [Range(0.1f, 2f)]
        public float smartScaleFactor = 1f;

        [Header("Blending")]
        [Tooltip("Easing preset applied to the blend weight. Ignored when Use Curve is enabled.")]
        public HonamiTransitionEase ease = HonamiTransitionEase.Linear;

        [Tooltip("If true, uses the custom curve below for the transition instead of the easing preset.")]
        public bool useCurve = false;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Has Exit Time")]
        public bool hasExitTime = false;
        public float exitTime = 0.75f;

        [Tooltip("If true, allows transitioning to itself (e.g. from AnyState) to restart the state.")]
        public bool canTransitionToSelf = false;

        [Tooltip("Offset in seconds to start the target animation from.")]
        public float destinationStartTime = 0f;

        [Tooltip("Destination: the target holds its first frame during the blend and starts playing once the transition completes — keeps fast attacks crisp. Source: the state being left freezes on its current pose and fades out static.")]
        [UnityEngine.Serialization.FormerlySerializedAs("freezeDestination")]
        public HonamiTransitionFreezeMode freezeMode = HonamiTransitionFreezeMode.None;

        [Tooltip("If true, this transition can fire and interrupt an already in-progress blend.")]
        public bool canInterruptTransition = false;

        public List<HonamiCondition> conditions = new();
        public List<HonamiParameterAssignment> parameterAssignments = new();

        [Tooltip("If set, this portal exit transition will only be considered if the entrance transition came from the state with this GUID.")]
        public string portalSourceFilterGuid = "";

        [NonSerialized]
        public int targetStateGuidHash;

        [NonSerialized]
        public bool hasTargetGuid;

        public AnimationCurve ResolveBlendCurve()
        {
            return useCurve ? curve : HonamiTransitionEasing.GetCurve(ease);
        }

        /// <summary>
        /// Caches frequently-used transition GUID values for runtime evaluation.
        /// </summary>
        public void BakeHashes()
        {
            hasTargetGuid = !string.IsNullOrEmpty(targetStateGuid);
            targetStateGuidHash = hasTargetGuid ? targetStateGuid.GetHashCode() : 0;
        }

        public HonamiTransition()
        {
            id = Guid.NewGuid().ToString();
            conditions = new List<HonamiCondition>();
            curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
    }
}
