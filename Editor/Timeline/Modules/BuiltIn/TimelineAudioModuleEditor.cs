using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Timeline
{
    [HonamiTimelineModuleEditor(typeof(TimelineAudioModule), "Audio", 3)]
    public sealed class TimelineAudioModuleEditor : HonamiTimelineModuleEditor
    {
        private const int MaxWaveformColumns = 2048;
        private const double ScrubSnippet = 0.09;
        private static readonly Color AudioColor = new(0.16f, 0.52f, 0.56f, 0.96f);
        private static readonly Color WaveformColor = new(1f, 1f, 1f, 0.42f);

        private static readonly Dictionary<TimelineAudioClipItem, AudioSource> Sources = new();
        private static readonly Dictionary<HonamiTimelineModule, float> LastTimes = new();
        private static GameObject _sourceRoot;
        private static double _scrubUntil;

        public override Color ModuleColor => AudioColor;
        public override Texture Icon => EditorGUIUtility.IconContent("AudioClip Icon").image;
        public override string ItemDisplayName => "Audio Clip";
        public override bool FadesOutUnderOverlap => true;

        [InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DestroySources;
            EditorApplication.playModeStateChanged += _ => DestroySources();
        }

        public override string GetItemLabel(HonamiTimelineItem item)
        {
            var audio = (TimelineAudioClipItem)item;
            return audio.clip != null ? audio.clip.name : "(No Audio Clip)";
        }

        public override bool AcceptsDrop(Object dropped) => dropped is AudioClip;

        public override HonamiTimelineItem CreateItemFromDrop(HonamiTimelineEditContext context, Object dropped, float time) =>
            dropped is AudioClip clip ? new TimelineAudioClipItem { clip = clip, time = time } : null;

        public override void DecorateItem(HonamiTimelineItemView view)
        {
            var item = (TimelineAudioClipItem)view.Item;
            if (item.clip != null)
                AddWaveform(view, item);

            base.DecorateItem(view);

            if (item.loop && item.clip != null && item.clip.length > 0f)
                view.AddCycleMarkers(item.clip.length - Mathf.Repeat(item.clipStartOffset, item.clip.length), item.clip.length);
        }

        public override void Preview(HonamiTimelinePreviewContext context)
        {
            var module = (TimelineAudioModule)context.Module;
            var template = context.Binding as AudioSource;

            bool timeChanged = !LastTimes.TryGetValue(module, out float lastTime) || !Mathf.Approximately(lastTime, context.Time);
            LastTimes[module] = context.Time;

            double now = EditorApplication.timeSinceStartup;
            if (!context.IsPlaying && timeChanged && context.IsScrubbing)
                _scrubUntil = now + ScrubSnippet;
            bool audible = context.IsPlaying ? Mathf.Approximately(context.PlaybackSpeed, 1f) : now < _scrubUntil;

            var items = module.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                bool active = item.clip != null && HonamiTimelineBlend.IsActive(item, context.Time)
                    && item.TryGetClipTime(context.Time - item.time, out _);
                if (!active)
                {
                    if (Sources.TryGetValue(item, out var idle) && idle != null && idle.isPlaying) idle.Stop();
                    continue;
                }

                item.TryGetClipTime(context.Time - item.time, out float clipTime);
                var source = GetSource(item, template);
                bool wasPlaying = source.isPlaying;
                TimelineAudioModule.Sync(source, item, clipTime, module.GetItemVolume(item, context.Time), audible);
                if (audible && !context.IsPlaying && wasPlaying && timeChanged)
                    source.time = Mathf.Clamp(clipTime, 0f, Mathf.Max(0f, item.clip.length - 0.001f));
            }
        }

        public override void StopPreview() => DestroySources();

        private static AudioSource GetSource(TimelineAudioClipItem item, AudioSource template)
        {
            if (Sources.TryGetValue(item, out var source) && source != null) return source;

            if (_sourceRoot == null)
                _sourceRoot = new GameObject("Honami Timeline Audio Preview") { hideFlags = HideFlags.HideAndDontSave };

            source = _sourceRoot.AddComponent<AudioSource>();
            source.hideFlags = HideFlags.HideAndDontSave;
            TimelineAudioModule.CopySettings(template, source);
            source.spatialBlend = 0f;
            Sources[item] = source;
            return source;
        }

        private static void DestroySources()
        {
            if (_sourceRoot != null) Object.DestroyImmediate(_sourceRoot);
            _sourceRoot = null;
            Sources.Clear();
            LastTimes.Clear();
        }

        private static void AddWaveform(HonamiTimelineItemView view, TimelineAudioClipItem item)
        {
            var waveform = TimelineAudioWaveform.Get(item.clip);
            if (waveform == null || waveform.BucketCount == 0) return;

            var overlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 0, right = 0, top = 3, bottom = 3 }
            };
            overlay.generateVisualContent += mesh => DrawWaveform(mesh, overlay.contentRect, waveform, item, view.PixelsPerSecond);
            view.Element.Add(overlay);
        }

        private static void DrawWaveform(MeshGenerationContext mesh, Rect rect, TimelineAudioWaveform waveform, TimelineAudioClipItem item, float pixelsPerSecond)
        {
            if (rect.width < 2f || rect.height < 2f || pixelsPerSecond <= 0f) return;

            int columns = Mathf.Clamp((int)(rect.width / 2f), 1, MaxWaveformColumns);
            float columnWidth = rect.width / columns;
            float mid = rect.center.y;
            float half = rect.height * 0.5f;
            float gain = Mathf.Clamp01(item.volume);

            var tops = new Vector2[columns + 1];
            var bottoms = new Vector2[columns + 1];
            for (int c = 0; c <= columns; c++)
            {
                float x = rect.xMin + c * columnWidth;
                float from = c * columnWidth / pixelsPerSecond;
                float to = (c + 1) * columnWidth / pixelsPerSecond;
                float max = 0f;
                float min = 0f;
                if (item.TryGetClipTime(from, out float clipFrom) && item.TryGetClipTime(Mathf.Min(to, item.Duration), out float clipTo))
                {
                    if (clipTo < clipFrom) clipTo = waveform.Length;
                    max = waveform.PeakBetween(clipFrom, Mathf.Max(clipTo, clipFrom + 0.0001f), out min);
                }
                tops[c] = new Vector2(x, mid - Mathf.Clamp(max * gain, -1f, 1f) * half);
                bottoms[c] = new Vector2(x, mid - Mathf.Clamp(min * gain, -1f, 1f) * half);
            }

            var painter = mesh.painter2D;
            painter.fillColor = WaveformColor;
            painter.BeginPath();
            painter.MoveTo(tops[0]);
            for (int c = 1; c <= columns; c++) painter.LineTo(tops[c]);
            for (int c = columns; c >= 0; c--) painter.LineTo(bottoms[c]);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
