using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using PlayerLoopStages = UnityEngine.PlayerLoop;

namespace HonamiAnimationSystem.Runtime.Core
{
    internal static class HonamiDirectorEvaluation
    {
        private struct CompleteHonamiEvaluation { }

        private static readonly List<HonamiAnimatorBase> _pending = new();
        private static readonly List<HonamiAnimatorBase> _running = new();
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pending.Clear();
            _running.Clear();
            _installed = false;
        }

        public static void Defer(HonamiAnimatorBase animator)
        {
            EnsureInstalled();
            _pending.Add(animator);
        }

        public static void Cancel(HonamiAnimatorBase animator) => _pending.Remove(animator);

        private static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;

            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (TryInsertAfterAnimation(ref loop)) PlayerLoop.SetPlayerLoop(loop);
        }

        private static bool TryInsertAfterAnimation(ref PlayerLoopSystem system)
        {
            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null) return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type != typeof(CompleteHonamiEvaluation)) continue;

                // An entry left over from before a domain reload still points at the unloaded delegate.
                children[i].updateDelegate = CompletePending;
                return true;
            }

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type != typeof(PlayerLoopStages.PreLateUpdate.DirectorUpdateAnimationEnd)) continue;

                var expanded = new PlayerLoopSystem[children.Length + 1];
                System.Array.Copy(children, 0, expanded, 0, i + 1);
                expanded[i + 1] = new PlayerLoopSystem
                {
                    type = typeof(CompleteHonamiEvaluation),
                    updateDelegate = CompletePending
                };
                System.Array.Copy(children, i + 1, expanded, i + 2, children.Length - i - 1);
                system.subSystemList = expanded;
                return true;
            }

            for (int i = 0; i < children.Length; i++)
            {
                if (TryInsertAfterAnimation(ref children[i])) return true;
            }

            return false;
        }

        private static void CompletePending()
        {
            if (_pending.Count == 0) return;

            _running.AddRange(_pending);
            _pending.Clear();

            for (int i = 0; i < _running.Count; i++)
            {
                HonamiAnimatorBase animator = _running[i];
                if (animator != null) animator.CompleteDeferredEvaluation();
            }

            _running.Clear();
        }
    }
}
