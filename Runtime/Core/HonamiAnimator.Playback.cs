using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Core
{
    public partial class HonamiAnimator
    {
        internal void PlayStateInternal(
            int targetIndex,
            float transitionDuration,
            int layer,
            bool forceRestart,
            AnimationCurve curve,
            float destinationStartTime)
            => HonamiPlaybackEngine.PlayStateInternal(this, targetIndex, transitionDuration, layer, forceRestart, curve, destinationStartTime);

        internal void ClearTransientPort(int layer)
            => HonamiPlaybackEngine.ClearTransientPort(this, layer);

        internal void ResetTime(int portIndex, int layer)
            => HonamiPlaybackEngine.ResetTime(this, portIndex, layer);

        /// <summary>
        /// Plays a state by GUID using an explicit transition priority and optional victim weighting behavior.
        /// </summary>
        public void PlayStateByGuidWithPriority(
            string guid,
            float transitionDuration,
            int layer,
            bool forceRestart,
            AnimationCurve curve,
            float destinationStartTime,
            int priority,
            HonamiVictimMode victimMode,
            float victimSpeedMultiplier,
            bool acceleratedWeightDrop,
            AnimationCurve victimCurve)
        {
            EnsureGraphBuilt();

            if (!TryResolveStateByGuid(guid, out int targetIndex))
            {
                return;
            }

            HonamiPlaybackEngine.PlayStateInternal(
                this,
                targetIndex,
                transitionDuration,
                layer,
                forceRestart,
                curve,
                destinationStartTime,
                priority,
                victimMode,
                victimSpeedMultiplier,
                acceleratedWeightDrop,
                victimCurve);
        }

        private void UpdateStatesRuntimeProperties()
            => HonamiPlaybackEngine.UpdateStatesRuntimeProperties(this);
    }
}
