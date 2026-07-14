namespace HonamiAnimationSystem.Runtime.Core
{
    /// <summary>
    /// Flattened runtime state for a single Honami animation layer.
    /// </summary>
    public struct HonamiLayerState
    {
        public int CurrentStateIndex;
        public int PreviousStateIndex;
        public int TransientStateIndex;

        public float TransitionWeight;
        public double TransitionDuration;
        public double TransitionTime;
        public float TransitionStartPrevWeight;
        public float TransitionStartLayerWeight;

        public bool IsLayerPaused;
        public int CurrentTransitionPriority;

        public HonamiVictimMode VictimMode;
        public float VictimSpeedMultiplier;
        public bool AcceleratedWeightDrop;
        public bool DestinationFrozen;
        public bool SourceFrozen;
    }

    /// <summary>
    /// Runtime bookkeeping for one playable input port inside a Honami layer mixer.
    /// </summary>
    public struct HonamiPortState
    {
        public int CurrentLoopCount;
        public double LastFrameRawTime;
        public bool StateFinishedFired;
        public bool StateExitedFired;
        public float WeightSnapshot;
    }
}
