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
            AnimationCurve victimCurve,
            bool inertial = false)
        {
            EnsureGraph();

            if (!_stateGuidToIndex.TryGetValue(guid.GetHashCode(), out int targetIndex))
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
                victimCurve,
                inertial);
        }

        /// <summary>
        /// Plays a state with an inertial transition: the target starts immediately while the source pose and
        /// velocity decay over <paramref name="duration"/>. Requires the controller to contain at least one Inertial
        /// transition (that is what builds the inertializer); otherwise it falls back to a normal crossfade.
        /// </summary>
        public void PlayStateInertial(string stateName, float duration, int layer = 0, bool forceRestart = false, float destinationStartTime = 0f)
        {
            EnsureGraph();
            if (!_stateNameToIndex.TryGetValue(StringToHash(stateName), out int targetIndex)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (duration > 0f && (_inertializer == null || !_inertializer.IsCreated) && !_inertialFallbackWarned)
            {
                _inertialFallbackWarned = true;
                Debug.LogWarning("[Honami] PlayStateInertial needs at least one Inertial transition (duration > 0) in the controller; playing a normal cross-fade instead.", this);
            }
#endif
            HonamiPlaybackEngine.PlayStateInternal(this, targetIndex, duration, layer, forceRestart, null, destinationStartTime,
                inertial: true);
        }

        private bool _inertialFallbackWarned;

        /// <summary>True while an inertial residual is still decaying on this layer.</summary>
        public bool IsInertializing(int layer)
            => _layerStates != null && layer >= 0 && layer < _layerStates.Length && _layerStates[layer].InertialDuration > 0.0;

        /// <summary>
        /// Starts the transition <paramref name="tr"/> towards <paramref name="targetGuid"/>, mapping its type to the
        /// matching playback arguments (Victim settings, Inertial switch) and applying its freeze mode.
        /// Portals pass <paramref name="allowVictim"/> = false because they never carried victim settings.
        /// </summary>
        internal void PlayTransition(HonamiTransition tr, string targetGuid, float duration, int layer, bool forceRestart,
            float destinationStartTime, bool allowVictim = true)
        {
            bool inertial = tr.type == HonamiTransitionType.Inertial;
            bool victim = allowVictim && tr.type == HonamiTransitionType.Victim;

            PlayStateByGuidWithPriority(targetGuid, duration, layer, forceRestart,
                tr.ResolveBlendCurve(),
                destinationStartTime,
                victim || inertial ? tr.priority : 0,
                victim ? tr.victimMode : HonamiVictimMode.None,
                victim ? tr.sacrificeSpeedMultiplier : 1f,
                victim && tr.acceleratedWeightDrop,
                victim && tr.useCustomVictimCurve ? tr.victimWeightCurve : null,
                inertial);

            // Nothing to freeze for Inertial: the layer already switched with a zero duration.
            HonamiPlaybackEngine.ApplyTransitionFreeze(this, layer, tr.freezeMode);
        }

        private void UpdateStatesRuntimeProperties()
            => HonamiPlaybackEngine.UpdateStatesRuntimeProperties(this);
    }
}
