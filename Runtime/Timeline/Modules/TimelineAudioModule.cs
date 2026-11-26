using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Preserve]
    [Serializable]
    public sealed class TimelineAudioClipItem : HonamiTimelineItem, IHonamiTimelineBlendCurveItem
    {
        public AudioClip clip;
        [Min(0f)] public float clipStartOffset;
        [Tooltip("0 = play the clip to its end")]
        [Min(0f)] public float durationOverride;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Repeat the clip when the item is longer than the clip")]
        public bool loop;
        [Tooltip("Fade in, seconds. Overlapping audio items crossfade automatically.")]
        [Min(0f)] public float blendIn;
        [Tooltip("Shape of the fade in: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendInCurve = HonamiBlendCurve.SmoothStep;
        [Tooltip("Fade out, seconds")]
        [Min(0f)] public float blendOut;
        [Tooltip("Shape of the fade out: a preset ease or a custom curve.")]
        public HonamiBlendCurve blendOutCurve = HonamiBlendCurve.SmoothStep;

        public float BlendIn
        {
            get => blendIn;
            set => blendIn = Mathf.Max(0f, value);
        }

        public float BlendOut
        {
            get => blendOut;
            set => blendOut = Mathf.Max(0f, value);
        }

        public HonamiBlendCurve BlendInCurve => blendInCurve;

        public HonamiBlendCurve BlendOutCurve => blendOutCurve;

        public override float Duration
        {
            get
            {
                if (durationOverride > 0f) return durationOverride;
                if (clip == null) return 0f;
                return Mathf.Max(0f, clip.length - clipStartOffset);
            }
        }

        public override bool CanResize => clip != null;

        public override void SetDuration(float duration) => durationOverride = Mathf.Max(MinDuration, duration);

        public override void TrimStart(float newStartTime)
        {
            float end = EndTime;
            float earliest = loop ? float.NegativeInfinity : time - clipStartOffset;
            float start = Mathf.Clamp(newStartTime, Mathf.Max(0f, earliest), end - MinDuration);
            float offset = clipStartOffset + (start - time);
            clipStartOffset = loop && clip != null && clip.length > 0f ? Mathf.Repeat(offset, clip.length) : Mathf.Max(0f, offset);
            time = start;
            durationOverride = end - start;
        }

        // Returns false past the clip's end when not looping, where the item is silent.
        public bool TryGetClipTime(float localTime, out float clipTime)
        {
            clipTime = clipStartOffset + localTime;
            if (clip == null) return false;

            float length = clip.length;
            if (loop && length > 0f)
            {
                clipTime = Mathf.Repeat(clipTime, length);
                return true;
            }
            return clipTime >= 0f && clipTime < length;
        }
    }

    [Preserve]
    [Serializable]
    public sealed class TimelineAudioModule : HonamiTimelineModule<TimelineAudioClipItem>
    {
        private const float ResyncTolerance = 0.12f;

        [Range(0f, 1f)] public float volume = 1f;

        public override Type BindingType => typeof(AudioSource);

        public override HonamiTimelineModuleRuntime CreateRuntime(in HonamiTimelineModuleContext context)
        {
            var template = context.Binding as AudioSource;
            var host = template != null ? template.gameObject : context.Owner;
            return host != null ? new Evaluator(this, host, template) : null;
        }

        public float GetItemVolume(TimelineAudioClipItem item, float time) =>
            volume * item.volume * HonamiTimelineBlend.Weight(this, item, time, true);

        public static void Sync(AudioSource source, TimelineAudioClipItem item, float clipTime, float itemVolume, bool playing)
        {
            if (source.clip != item.clip)
            {
                source.Stop();
                source.clip = item.clip;
            }

            source.volume = itemVolume;
            source.loop = item.loop;

            if (!playing)
            {
                if (source.isPlaying) source.Pause();
                SetTime(source, clipTime);
                return;
            }

            if (!source.isPlaying)
            {
                SetTime(source, clipTime);
                source.Play();
                return;
            }

            if (Mathf.Abs(source.time - clipTime) > ResyncTolerance)
                SetTime(source, clipTime);
        }

        public static void CopySettings(AudioSource template, AudioSource target)
        {
            target.playOnAwake = false;
            if (template == null) return;

            target.outputAudioMixerGroup = template.outputAudioMixerGroup;
            target.spatialBlend = template.spatialBlend;
            target.priority = template.priority;
            target.pitch = template.pitch;
            target.panStereo = template.panStereo;
            target.reverbZoneMix = template.reverbZoneMix;
            target.dopplerLevel = template.dopplerLevel;
            target.spread = template.spread;
            target.rolloffMode = template.rolloffMode;
            target.minDistance = template.minDistance;
            target.maxDistance = template.maxDistance;
            target.bypassEffects = template.bypassEffects;
            target.bypassListenerEffects = template.bypassListenerEffects;
            target.bypassReverbZones = template.bypassReverbZones;
        }

        private static void SetTime(AudioSource source, float clipTime)
        {
            var clip = source.clip;
            if (clip == null) return;
            source.time = Mathf.Clamp(clipTime, 0f, Mathf.Max(0f, clip.length - 0.001f));
        }

        private sealed class Evaluator : HonamiTimelineModuleRuntime
        {
            private readonly TimelineAudioModule _module;
            private readonly GameObject _host;
            private readonly AudioSource _template;
            private GameObject _sourceRoot;
            private AudioSource[] _sources = Array.Empty<AudioSource>();
            private bool _paused;

            public Evaluator(TimelineAudioModule module, GameObject host, AudioSource template)
            {
                _module = module;
                _host = host;
                _template = template;
            }

            public override void Evaluate(in HonamiTimelineFrame frame)
            {
                if (_host == null) return;

                var items = _module.items;
                EnsureSources(items.Count);

                bool playing = !_paused && !frame.IsSeek && frame.Time != frame.PreviousTime;
                for (int i = 0; i < items.Count; i++)
                {
                    var source = _sources[i];
                    var item = items[i];
                    if (source == null) continue;

                    if (item?.clip == null || !HonamiTimelineBlend.IsActive(item, frame.Time)
                        || !item.TryGetClipTime(frame.Time - item.time, out float clipTime))
                    {
                        if (source.isPlaying) source.Stop();
                        continue;
                    }

                    Sync(source, item, clipTime, _module.GetItemVolume(item, frame.Time), playing);
                }
            }

            public override void OnPlay() => _paused = false;

            public override void OnPause()
            {
                _paused = true;
                for (int i = 0; i < _sources.Length; i++)
                {
                    if (_sources[i] != null && _sources[i].isPlaying) _sources[i].Pause();
                }
            }

            public override void OnResume() => _paused = false;

            public override void OnStop()
            {
                for (int i = 0; i < _sources.Length; i++)
                {
                    if (_sources[i] != null) _sources[i].Stop();
                }
            }

            public override void Dispose()
            {
                if (_sourceRoot != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(_sourceRoot);
                    else UnityEngine.Object.DestroyImmediate(_sourceRoot);
                }
                _sourceRoot = null;
                _sources = Array.Empty<AudioSource>();
            }

            private void EnsureSources(int count)
            {
                if (_sources.Length >= count && _sourceRoot != null) return;

                if (_sourceRoot == null)
                {
                    _sourceRoot = new GameObject("Honami Timeline Audio") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
                    _sourceRoot.transform.SetParent(_host.transform, false);
                    _sources = Array.Empty<AudioSource>();
                }

                var sources = new AudioSource[count];
                for (int i = 0; i < count; i++)
                {
                    if (i < _sources.Length && _sources[i] != null)
                    {
                        sources[i] = _sources[i];
                        continue;
                    }
                    var source = _sourceRoot.AddComponent<AudioSource>();
                    CopySettings(_template, source);
                    sources[i] = source;
                }
                _sources = sources;
            }
        }
    }
}
