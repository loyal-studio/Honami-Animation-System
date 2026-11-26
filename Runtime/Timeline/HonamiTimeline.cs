using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [CreateAssetMenu(fileName = "New HonamiTimeline", menuName = "Honami Animation/Timeline", order = 10)]
    public sealed class HonamiTimeline : ScriptableObject, IHonamiTimelineModuleContainer
    {
        public const float DefaultFrameRate = 30f;

        // An empty timeline still needs a playable length, so the edit grid never collapses to zero.
        private const float MinDuration = 1f;

        [SerializeField] private string timelineName = "New Timeline";
        [Tooltip("Frames per second of the edit grid: snapping, frame stepping and timecode")]
        [SerializeField, Min(1f)] private float frameRate = DefaultFrameRate;
        [Tooltip("0 = duration of the longest module")]
        [SerializeField, Min(0f)] private float durationOverride;
        [SerializeField] private List<HonamiTimelineModule> modules = new();
        [SerializeReference] private List<HonamiTimelineMarker> markers = new();

        [NonSerialized] private int _revision;

        public string TimelineName
        {
            get => timelineName;
            set => timelineName = value;
        }

        public float FrameRate
        {
            get => frameRate > 0f ? frameRate : DefaultFrameRate;
            set => frameRate = Mathf.Max(1f, value);
        }

        public float DurationOverride
        {
            get => durationOverride;
            set => durationOverride = Mathf.Max(0f, value);
        }

        public HonamiTimelineModuleHost Host => HonamiTimelineModuleHost.Timeline;
        public List<HonamiTimelineModule> Modules => modules;
        public List<HonamiTimelineMarker> Markers => markers;

        public int ModuleRevision => _revision;

        public void MarkModulesChanged() => _revision++;

        public HonamiTimelineModule FindModule(string moduleId)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null && modules[i].Id == moduleId)
                    return modules[i];
            }
            return null;
        }

        public HonamiTimelineMarker FindMarker(string markerId)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null && markers[i].id == markerId)
                    return markers[i];
            }
            return null;
        }

        public bool TryGetMarkerTime(string markerId, out float time)
        {
            var marker = FindMarker(markerId);
            time = marker?.time ?? 0f;
            return marker != null;
        }

        public float GetDuration()
        {
            if (durationOverride > 0f) return durationOverride;

            float max = MinDuration;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] == null) continue;
                max = Mathf.Max(max, modules[i].GetDuration());
            }
            return max;
        }

        public float SnapToFrame(float time) => Mathf.Round(time * FrameRate) / FrameRate;

        private void OnValidate() => MarkModulesChanged();
    }
}
