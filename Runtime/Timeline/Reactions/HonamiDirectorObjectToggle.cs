using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    public enum DirectorObjectAction
    {
        None,
        Activate,
        Deactivate,
        Restore
    }

    [Serializable]
    public sealed class DirectorObjectRule
    {
        [SerializeField] internal GameObject target;
        [SerializeField] internal DirectorObjectAction onStarted;
        [SerializeField] internal DirectorObjectAction onStopped = DirectorObjectAction.Restore;

        [NonSerialized] internal bool capturedState;
    }

    [AddComponentMenu("Honami Animation/Director Reactions/Director Object Toggle")]
    public sealed class HonamiDirectorObjectToggle : HonamiDirectorReaction
    {
        [SerializeField] private DirectorObjectRule[] rules = Array.Empty<DirectorObjectRule>();

        protected override void OnDirectorStarted(HonamiDirector director)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule.target == null) continue;

                rule.capturedState = rule.target.activeSelf;
                ApplyAction(rule.target, rule.onStarted, rule.capturedState);
            }
        }

        protected override void OnDirectorStopped(HonamiDirector director)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule.target == null) continue;

                ApplyAction(rule.target, rule.onStopped, rule.capturedState);
            }
        }

        private static void ApplyAction(GameObject target, DirectorObjectAction action, bool capturedState)
        {
            switch (action)
            {
                case DirectorObjectAction.Activate:
                    target.SetActive(true);
                    break;
                case DirectorObjectAction.Deactivate:
                    target.SetActive(false);
                    break;
                case DirectorObjectAction.Restore:
                    target.SetActive(capturedState);
                    break;
            }
        }
    }
}
