using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [AddComponentMenu("Honami Animation/Director Reactions/Director Behaviour Toggle")]
    public sealed class HonamiDirectorBehaviourToggle : HonamiDirectorReaction
    {
        [Tooltip("Disabled while the cutscene runs, then set back to how they were. Input handlers, gameplay camera scripts, AI.")]
        [SerializeField] private Behaviour[] disable = Array.Empty<Behaviour>();
        [Tooltip("Enabled while the cutscene runs, then set back to how they were.")]
        [SerializeField] private Behaviour[] enable = Array.Empty<Behaviour>();

        private bool[] _disableStates = Array.Empty<bool>();
        private bool[] _enableStates = Array.Empty<bool>();

        protected override void OnDirectorStarted(HonamiDirector director)
        {
            _enableStates = Capture(enable);
            _disableStates = Capture(disable);
            Apply(enable, true);
            Apply(disable, false);
        }

        protected override void OnDirectorStopped(HonamiDirector director)
        {
            Restore(disable, _disableStates);
            Restore(enable, _enableStates);
        }

        private static bool[] Capture(Behaviour[] targets)
        {
            var states = new bool[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                states[i] = targets[i] != null && targets[i].enabled;
            return states;
        }

        private static void Apply(Behaviour[] targets, bool enabled)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    targets[i].enabled = enabled;
            }
        }

        private static void Restore(Behaviour[] targets, bool[] states)
        {
            int count = Mathf.Min(targets.Length, states.Length);
            for (int i = 0; i < count; i++)
            {
                if (targets[i] != null)
                    targets[i].enabled = states[i];
            }
        }
    }
}
