using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using PlayerLoopStages = UnityEngine.PlayerLoop;

namespace HonamiAnimationSystem.Runtime.Core
{
    internal static class HonamiClipTrackEvaluation
    {
        private struct FlushHonamiClipTracks { }

        private static readonly List<HonamiAnimatorBase> _queued = new();
        private static readonly List<HonamiAnimatorBase> _flushing = new();
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _queued.Clear();
            _flushing.Clear();
            _installed = false;
        }

        public static void Queue(HonamiAnimatorBase animator)
        {
            EnsureInstalled();
            _queued.Add(animator);
        }

        private static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;

            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (TryInsertAfterLateUpdate(ref loop)) PlayerLoop.SetPlayerLoop(loop);
        }

        private static bool TryInsertAfterLateUpdate(ref PlayerLoopSystem system)
        {
            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null) return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type != typeof(FlushHonamiClipTracks)) continue;

                // An entry left over from before a domain reload still points at the unloaded delegate.
                children[i].updateDelegate = FlushQueued;
                return true;
            }

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type != typeof(PlayerLoopStages.PreLateUpdate.ScriptRunBehaviourLateUpdate)) continue;

                var expanded = new PlayerLoopSystem[children.Length + 1];
                System.Array.Copy(children, 0, expanded, 0, i + 1);
                expanded[i + 1] = new PlayerLoopSystem
                {
                    type = typeof(FlushHonamiClipTracks),
                    updateDelegate = FlushQueued
                };
                System.Array.Copy(children, i + 1, expanded, i + 2, children.Length - i - 1);
                system.subSystemList = expanded;
                return true;
            }

            for (int i = 0; i < children.Length; i++)
            {
                if (TryInsertAfterLateUpdate(ref children[i])) return true;
            }

            return false;
        }

        private static void FlushQueued()
        {
            if (_queued.Count == 0) return;

            _flushing.AddRange(_queued);
            _queued.Clear();

            for (int i = 0; i < _flushing.Count; i++)
            {
                HonamiAnimatorBase animator = _flushing[i];
                if (animator == null) continue;

                animator._clipTrackFlushQueued = false;
                animator.ApplyClipTracks();
            }

            _flushing.Clear();
        }
    }
}
