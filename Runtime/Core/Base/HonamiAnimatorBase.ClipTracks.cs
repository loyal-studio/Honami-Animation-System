using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HonamiAnimationSystem.Runtime.Core
{
    public abstract partial class HonamiAnimatorBase
    {
        private readonly List<HonamiClipTrack> _clipTracks = new();
        private AnimationLayerMixerPlayable _clipTrackMixer;
        private Playable _clipTrackRoot;
        private bool _clipTrackLayoutDirty;
        private bool _clipTrackValuesDirty;
        private bool _clipTracksWeighted;
        private bool _clipTrackTickPending;
        private double _clipTrackPendingDelta;
        internal bool _clipTrackFlushQueued;

        public bool SupportsClipTracks => SupportsClipTrackLayering;

        // While a clip track is attached the animator hands its evaluation to the track owner, so an animator
        // whose Tick evaluates the graph itself or reads the pose right after EvaluateGraph must not opt in.
        protected virtual bool SupportsClipTrackLayering => false;

        public int ClipTrackCount => _clipTracks.Count;

        internal bool IsClipTrackAttached => _clipTrackMixer.IsValid();

        public HonamiClipTrack AcquireClipTrack()
        {
            if (!SupportsClipTrackLayering) return null;

            var track = new HonamiClipTrack(this);
            _clipTracks.Add(track);

            if (!IsClipTrackAttached && HasPlayableGraph) SpliceClipTrackMixer();
            _clipTrackLayoutDirty = true;
            return track;
        }

        public void ApplyClipTracks()
        {
            if (!_clipTrackTickPending && !_clipTrackValuesDirty && !_clipTrackLayoutDirty) return;
            EvaluateWithClipTracks();
        }

        internal void ReleaseClipTrack(HonamiClipTrack track)
        {
            int index = _clipTracks.IndexOf(track);
            if (index < 0) return;

            for (int i = 0; i < track._count; i++)
                DestroyClipTrackPlayable(ref track._slots[i]);
            _clipTracks.RemoveAt(index);

            if (_clipTracks.Count > 0)
            {
                OnClipTrackLayoutChanged();
                return;
            }

            DetachClipTrackMixer();
        }

        internal void OnClipTrackLayoutChanged()
        {
            _clipTrackLayoutDirty = true;
            QueueClipTrackFlush();
        }

        internal void OnClipTrackValuesChanged()
        {
            _clipTrackValuesDirty = true;
            QueueClipTrackFlush();
        }

        internal void DestroyClipTrackPlayable(ref HonamiClipTrack.Slot slot)
        {
            if (slot.Playable.IsValid()) _playableGraph.DestroyPlayable(slot.Playable);
            slot.Playable = default;
        }

        private void DeferToClipTracks(float deltaTime)
        {
            _clipTrackPendingDelta += deltaTime;
            _clipTrackTickPending = true;
            QueueClipTrackFlush();
        }

        private void QueueClipTrackFlush()
        {
            if (_clipTrackFlushQueued || !IsClipTrackAttached) return;
            _clipTrackFlushQueued = true;
            HonamiClipTrackEvaluation.Queue(this);
        }

        private void EvaluateWithClipTracks()
        {
            double deltaTime = _clipTrackPendingDelta;
            _clipTrackPendingDelta = 0.0;
            _clipTrackTickPending = false;

            if (!HasPlayableGraph || !IsClipTrackAttached) return;

            if (_clipTrackLayoutDirty) RewireClipTracks();
            PushClipTrackValues();

            _cachedDeltaTime = deltaTime;
            PrepareRigs();
            _playableGraph.Evaluate((float)deltaTime);
            CompleteEvaluation();
        }

        // Called while the graph is built, before the rigs go on top, so clip tracks survive a rebuild.
        private void AttachClipTrackMixer()
        {
            _clipTrackMixer = default;
            _clipTrackRoot = _playableOutput.IsOutputValid() ? _playableOutput.GetSourcePlayable() : Playable.Null;

            // Earlier slot playables died with the old graph, or stay behind in an outgoing controller cross-fade.
            for (int t = 0; t < _clipTracks.Count; t++)
            {
                var track = _clipTracks[t];
                for (int i = 0; i < track._count; i++)
                    track._slots[i].Playable = default;
            }

            if (_clipTracks.Count == 0 || !_clipTrackRoot.IsValid()) return;

            _clipTrackMixer = CreateClipTrackMixer(_clipTrackRoot);
            _playableOutput.SetSourcePlayable(_clipTrackMixer);
            _clipTrackLayoutDirty = true;
            _clipTrackValuesDirty = true;
        }

        private void SpliceClipTrackMixer()
        {
            Playable root = _clipTrackRoot;
            if (!root.IsValid() || !_playableOutput.IsOutputValid()) return;

            Playable consumer = root.GetOutputCount() > 0 ? root.GetOutput(0) : Playable.Null;
            if (consumer.IsValid())
            {
                int port = FindInputPort(consumer, root);
                if (port < 0) return;

                float weight = consumer.GetInputWeight(port);
                _playableGraph.Disconnect(consumer, port);
                _clipTrackMixer = CreateClipTrackMixer(root);
                _playableGraph.Connect(_clipTrackMixer, 0, consumer, port);
                consumer.SetInputWeight(port, weight);
            }
            else if (_playableOutput.GetSourcePlayable().Equals(root))
            {
                _clipTrackMixer = CreateClipTrackMixer(root);
                _playableOutput.SetSourcePlayable(_clipTrackMixer);
            }
            else
            {
                return;
            }

            SyncGraphTimeMode();
            InvalidateFpsCapInterpolation();
        }

        private void DetachClipTrackMixer()
        {
            AnimationLayerMixerPlayable mixer = _clipTrackMixer;
            _clipTrackMixer = default;
            _clipTracksWeighted = false;
            _clipTrackLayoutDirty = false;
            _clipTrackValuesDirty = false;

            if (!mixer.IsValid())
            {
                _clipTrackPendingDelta = 0.0;
                _clipTrackTickPending = false;
                return;
            }

            Playable root = mixer.GetInput(0);
            Playable consumer = mixer.GetOutputCount() > 0 ? mixer.GetOutput(0) : Playable.Null;
            int port = consumer.IsValid() ? FindInputPort(consumer, mixer) : -1;

            if (root.IsValid()) _playableGraph.Disconnect(mixer, 0);

            if (port >= 0)
            {
                float weight = consumer.GetInputWeight(port);
                _playableGraph.Disconnect(consumer, port);
                if (root.IsValid())
                {
                    _playableGraph.Connect(root, 0, consumer, port);
                    consumer.SetInputWeight(port, weight);
                }
            }
            else if (_playableOutput.IsOutputValid() && _playableOutput.GetSourcePlayable().Equals(mixer))
            {
                _playableOutput.SetSourcePlayable(root);
            }

            _playableGraph.DestroySubgraph(mixer);

            SyncGraphTimeMode();
            InvalidateFpsCapInterpolation();

            double deltaTime = _clipTrackPendingDelta;
            _clipTrackPendingDelta = 0.0;
            _clipTrackTickPending = false;

            // The animator's own pose comes back on the frame the last track goes, not a frame later.
            if (!HasPlayableGraph || !isActiveAndEnabled) return;
            _cachedDeltaTime = deltaTime;
            PrepareRigs();
            _playableGraph.Evaluate((float)deltaTime);
            CompleteEvaluation();
        }

        private AnimationLayerMixerPlayable CreateClipTrackMixer(Playable root)
        {
            var mixer = AnimationLayerMixerPlayable.Create(_playableGraph, 1);
            _playableGraph.Connect(root, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            return mixer;
        }

        private void RewireClipTracks()
        {
            _clipTrackLayoutDirty = false;
            _clipTrackValuesDirty = true;

            int portCount = 1;
            for (int t = 0; t < _clipTracks.Count; t++)
                portCount += _clipTracks[t]._count;

            int current = _clipTrackMixer.GetInputCount();
            for (int port = 1; port < current; port++)
            {
                if (_clipTrackMixer.GetInput(port).IsValid())
                    _playableGraph.Disconnect(_clipTrackMixer, port);
            }

            _clipTrackMixer.SetInputCount(portCount);

            int next = 1;
            for (int t = 0; t < _clipTracks.Count; t++)
            {
                var track = _clipTracks[t];
                for (int i = 0; i < track._count; i++, next++)
                {
                    ref HonamiClipTrack.Slot slot = ref track._slots[i];
                    if (slot.Clip == null) continue;

                    if (!slot.Playable.IsValid())
                    {
                        slot.Playable = AnimationClipPlayable.Create(_playableGraph, slot.Clip);
                        slot.Playable.SetApplyFootIK(false);
                        slot.Playable.SetSpeed(0.0);
                        slot.Playable.SetDuration(double.MaxValue);
                    }

                    _playableGraph.Connect(slot.Playable, 0, _clipTrackMixer, next);
                }
            }
        }

        private void PushClipTrackValues()
        {
            _clipTrackValuesDirty = false;

            bool weighted = false;
            int port = 1;
            for (int t = 0; t < _clipTracks.Count; t++)
            {
                var track = _clipTracks[t];
                for (int i = 0; i < track._count; i++, port++)
                {
                    ref HonamiClipTrack.Slot slot = ref track._slots[i];
                    float weight = slot.Playable.IsValid() ? slot.Weight : 0f;
                    _clipTrackMixer.SetInputWeight(port, weight);
                    if (weight <= 0f) continue;

                    slot.Playable.SetTime(slot.Time);
                    weighted = true;
                }
            }

            _clipTracksWeighted = weighted;
        }

        private static int FindInputPort(Playable consumer, Playable input)
        {
            int count = consumer.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                if (consumer.GetInput(i).Equals(input)) return i;
            }
            return -1;
        }
    }
}
