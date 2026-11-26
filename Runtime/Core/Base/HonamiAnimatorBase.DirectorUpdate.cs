using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public abstract partial class HonamiAnimatorBase
    {
        private DirectorUpdateMode _graphTimeMode = DirectorUpdateMode.Manual;
        private bool _deferEvaluation;
        internal bool _evaluationDeferred;

        public bool IsBatchEvaluated => _graphTimeMode != DirectorUpdateMode.Manual;

        public bool SupportsBatching => SupportsBatchedEvaluation;

        public bool BatchedEvaluation
        {
            get => batchedEvaluation;
            set
            {
                batchedEvaluation = value;
                SyncGraphTimeMode();
            }
        }

        // An animator whose Tick evaluates the graph itself, or reads the pose right after EvaluateGraph, would break under batching.
        protected virtual bool SupportsBatchedEvaluation => false;

        // Unity only batches graphs it advances itself, and it advances them by the frame delta,
        // so anything that alters that delta keeps the graph on manual evaluation.
        private DirectorUpdateMode DesiredGraphTimeMode
        {
            get
            {
                if (!SupportsBatchedEvaluation || !batchedEvaluation || fpsCap || _isPaused || EffectiveTimeScale != 1f || IsClipTrackAttached) return DirectorUpdateMode.Manual;
                if (updateMode == HonamiUpdateMode.Normal) return DirectorUpdateMode.GameTime;
                if (updateMode == HonamiUpdateMode.UnscaledTime) return DirectorUpdateMode.UnscaledGameTime;
                return DirectorUpdateMode.Manual;
            }
        }

        protected void SyncGraphTimeMode()
        {
            if (!_playableGraph.IsValid()) return;

            DirectorUpdateMode desired = DesiredGraphTimeMode;
            if (desired == _graphTimeMode) return;

            _graphTimeMode = desired;

            // A playing graph ignores a time mode change until it is restarted.
            bool playing = _playableGraph.IsPlaying();
            if (playing) _playableGraph.Stop();
            _playableGraph.SetTimeUpdateMode(desired);
            if (playing) _playableGraph.Play();
        }

        private void TickFromUpdate(double deltaTime)
        {
            if (_graphTimeMode == DirectorUpdateMode.Manual)
            {
                TickWithFpsCap(deltaTime);
                return;
            }

            _deferEvaluation = true;
            try
            {
                Tick(deltaTime);
            }
            finally
            {
                _deferEvaluation = false;
            }
        }

        internal void CompleteDeferredEvaluation()
        {
            _evaluationDeferred = false;
            if (HasPlayableGraph) CompleteEvaluation();
        }

        private void CancelDeferredEvaluation()
        {
            if (!_evaluationDeferred) return;
            _evaluationDeferred = false;
            HonamiDirectorEvaluation.Cancel(this);
        }
    }
}
