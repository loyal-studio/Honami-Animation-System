using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    // Peak data per clip: Peaks[2 * i] is the max and Peaks[2 * i + 1] the min of bucket i, all channels merged.
    public sealed class TimelineAudioWaveform
    {
        private const int MaxBuckets = 4096;
        private const int ReadChunk = 65536;
        private static readonly Dictionary<int, TimelineAudioWaveform> Cache = new();
        private static MethodInfo _getMinMaxData;
        private static bool _lookedUpMinMax;

        public readonly float[] Peaks;
        public readonly int BucketCount;
        public readonly float Length;

        private TimelineAudioWaveform(float[] peaks, float length)
        {
            Peaks = peaks;
            BucketCount = peaks.Length / 2;
            Length = length;
        }

        public static TimelineAudioWaveform Get(AudioClip clip)
        {
            if (clip == null) return null;
            int key = clip.GetInstanceID();
            if (Cache.TryGetValue(key, out var cached) && Mathf.Approximately(cached.Length, clip.length)) return cached;

            var waveform = FromImporter(clip) ?? FromSamples(clip);
            Cache[key] = waveform;
            return waveform;
        }

        public static void ClearCache() => Cache.Clear();

        public float PeakBetween(float from, float to, out float min)
        {
            min = 0f;
            if (BucketCount == 0 || Length <= 0f) return 0f;
            int first = Mathf.Clamp(Mathf.FloorToInt(from / Length * BucketCount), 0, BucketCount - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt(to / Length * BucketCount), first + 1, BucketCount);
            float max = float.MinValue;
            min = float.MaxValue;
            for (int i = first; i < last; i++)
            {
                max = Mathf.Max(max, Peaks[2 * i]);
                min = Mathf.Min(min, Peaks[2 * i + 1]);
            }
            return max;
        }

        private static TimelineAudioWaveform FromImporter(AudioClip clip)
        {
            if (!_lookedUpMinMax)
            {
                _lookedUpMinMax = true;
                var audioUtil = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
                _getMinMaxData = audioUtil?.GetMethod("GetMinMaxData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioImporter) }, null);
            }
            if (_getMinMaxData == null) return null;

            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is not AudioImporter importer) return null;

            float[] data;
            try
            {
                data = _getMinMaxData.Invoke(null, new object[] { importer }) as float[];
            }
            catch (Exception)
            {
                return null;
            }

            int channels = Mathf.Max(1, clip.channels);
            if (data == null || data.Length < channels * 2) return null;

            int buckets = data.Length / (channels * 2);
            var peaks = new float[buckets * 2];
            for (int b = 0; b < buckets; b++)
            {
                float max = float.MinValue;
                float min = float.MaxValue;
                for (int c = 0; c < channels; c++)
                {
                    int index = (b * channels + c) * 2;
                    max = Mathf.Max(max, data[index]);
                    min = Mathf.Min(min, data[index + 1]);
                }
                peaks[2 * b] = max;
                peaks[2 * b + 1] = min;
            }
            return new TimelineAudioWaveform(peaks, clip.length);
        }

        private static TimelineAudioWaveform FromSamples(AudioClip clip)
        {
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.samples <= 0) return new TimelineAudioWaveform(Array.Empty<float>(), clip.length);
            if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return new TimelineAudioWaveform(Array.Empty<float>(), clip.length);

            int channels = Mathf.Max(1, clip.channels);
            int frames = clip.samples;
            int buckets = Mathf.Clamp(frames / 256, 1, MaxBuckets);
            var peaks = new float[buckets * 2];
            for (int b = 0; b < buckets; b++)
            {
                peaks[2 * b] = float.MinValue;
                peaks[2 * b + 1] = float.MaxValue;
            }

            var buffer = new float[ReadChunk * channels];
            for (int offset = 0; offset < frames; offset += ReadChunk)
            {
                int count = Mathf.Min(ReadChunk, frames - offset);
                if (count < ReadChunk) buffer = new float[count * channels];
                if (!clip.GetData(buffer, offset)) break;

                for (int f = 0; f < count; f++)
                {
                    int bucket = Mathf.Min(buckets - 1, (int)((long)(offset + f) * buckets / frames));
                    for (int c = 0; c < channels; c++)
                    {
                        float sample = buffer[f * channels + c];
                        if (sample > peaks[2 * bucket]) peaks[2 * bucket] = sample;
                        if (sample < peaks[2 * bucket + 1]) peaks[2 * bucket + 1] = sample;
                    }
                }
            }
            return new TimelineAudioWaveform(peaks, clip.length);
        }
    }
}
