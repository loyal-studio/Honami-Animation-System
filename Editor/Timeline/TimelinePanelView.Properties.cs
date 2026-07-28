#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private void BuildProperties()
        {
            _properties.Clear();
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = scroll.style.paddingRight = scroll.style.paddingTop = scroll.style.paddingBottom = 12;
            _properties.Add(scroll);

            if (_state.SelectedTimelineTrack != null)
                BuildTimelineTrackProperties(scroll, _state.SelectedTimelineTrack);
            else if (_state.SelectedTimelineClips.Count == 1)
                BuildTimelineClipProperties(scroll, _state.SelectedTimelineClips[0]);
            else if (_state.SelectedTimelineEvents.Count == 1)
                BuildTimelineEventProperties(scroll, _state.SelectedTimelineEvents[0]);
            else if (_state.SelectedSeqClips.Count == 1)
                BuildSeqClipProperties(scroll, _state.SelectedSeqClips[0]);
            else if (_state.SelectedEvents.Count == 1)
                BuildStateEventProperties(scroll, _state.SelectedEvents[0]);
            else if (_state.Mode == TimelineMode.HonamiTimeline && _state.ActiveTimeline != null)
                BuildTimelineProperties(scroll, _state.ActiveTimeline);
            else if (_state.Mode == TimelineMode.HonamiState && _state.SelectedState != null)
                BuildStateProperties(scroll, _state.SelectedState);
        }

        private void BuildTimelineProperties(VisualElement root, HonamiTimeline timeline)
        {
            root.Add(Title("Timeline"));
            AddText(root, "Name", timeline.timelineName, v =>
                HonamiEditorController.EditTimelineProps(timeline, v, timeline.durationOverride));
            AddFloat(root, "Duration Override", timeline.durationOverride, v =>
                HonamiEditorController.EditTimelineProps(timeline, timeline.timelineName, v));
        }

        private void BuildStateProperties(VisualElement root, HonamiState targetState)
        {
            root.Add(Title("State"));
            AddText(root, "Name", targetState.stateName, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateProps(_state.SelectedState, v, _state.SelectedState.loop, _state.SelectedState.isReversed, _state.SelectedState.speed);
                _state.PostModifyState();
            });
            AddToggle(root, "Loop", targetState.loop, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateProps(_state.SelectedState, _state.SelectedState.stateName, v, _state.SelectedState.isReversed, _state.SelectedState.speed);
                _state.PostModifyState();
            });
            AddToggle(root, "Reversed", targetState.isReversed, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateProps(_state.SelectedState, _state.SelectedState.stateName, _state.SelectedState.loop, v, _state.SelectedState.speed);
                _state.PostModifyState();
            });
            AddFloat(root, "Speed", targetState.speed, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateProps(_state.SelectedState, _state.SelectedState.stateName, _state.SelectedState.loop, _state.SelectedState.isReversed, v);
                _state.PostModifyState();
            });

            if (!_state.IsAnimState || _state.AnimNode == null) return;

            var animNode = _state.AnimNode;
            root.Add(new VisualElement { style = { height = 1, backgroundColor = TimelineTheme.SubtleLine, marginTop = 8, marginBottom = 8 } });
            root.Add(Title("Clip Range"));
            float clipLen = animNode.clip != null ? animNode.clip.length : 0f;
            AddFloat(root, $"Start Time (0–{clipLen:F2}s)", animNode.startTime, v =>
            {
                _state.PreModifyState();
                float end = _state.AnimNode.endTime > 0f ? _state.AnimNode.endTime : clipLen;
                HonamiEditorController.EditAnimNodeClipTime(_state.AnimNode, Mathf.Clamp(v, 0f, end - 0.001f), _state.AnimNode.endTime);
                _state.PostModifyState();
            });
            AddFloat(root, $"End Time (0 = full, max {clipLen:F2}s)", animNode.endTime, v =>
            {
                _state.PreModifyState();
                float clampedEnd = v <= 0f ? 0f : Mathf.Clamp(v, _state.AnimNode.startTime + 0.001f, clipLen);
                HonamiEditorController.EditAnimNodeClipTime(_state.AnimNode, _state.AnimNode.startTime, clampedEnd);
                _state.PostModifyState();
            });
        }

        private void BuildTimelineTrackProperties(VisualElement root, HonamiTimelineTrack track)
        {
            var timeline = _state.ActiveTimeline;
            root.Add(Title($"{track.trackType} Track"));
            AddText(root, "Name", track.trackName, v =>
                HonamiEditorController.EditTimelineTrack(timeline, track, v, track.muted, track.target));
            AddToggle(root, "Muted", track.muted, v =>
                HonamiEditorController.EditTimelineTrack(timeline, track, track.trackName, v, track.target));
            if (track.trackType == HonamiTimelineTrackType.Animation)
                AddObject<GameObject>(root, "Target", track.target, v =>
                    HonamiEditorController.EditTimelineTrack(timeline, track, track.trackName, track.muted, v));
            AddDangerButton(root, "Remove Track", () =>
            {
                HonamiEditorController.DeleteTimelineTrack(timeline, track);
                _state.SelectedTimelineTrack = null;
                _rebuild();
            });
        }

        private void BuildTimelineClipProperties(VisualElement root, HonamiTimelineClip clip)
        {
            var timeline = _state.ActiveTimeline;
            root.Add(Title("Timeline Clip"));
            AddObject<AnimationClip>(root, "Clip", clip.clip, v => EditTimelineClip(timeline, clip, v, clip.startTime, clip.speed, clip.reversed, clip.clipStartOffset, clip.durationOverride));
            AddFloat(root, "Start Time", clip.startTime, v => EditTimelineClip(timeline, clip, clip.clip, v, clip.speed, clip.reversed, clip.clipStartOffset, clip.durationOverride));
            AddFloat(root, "Speed", clip.speed, v => EditTimelineClip(timeline, clip, clip.clip, clip.startTime, v, clip.reversed, clip.clipStartOffset, clip.durationOverride));
            AddToggle(root, "Reversed", clip.reversed, v => EditTimelineClip(timeline, clip, clip.clip, clip.startTime, clip.speed, v, clip.clipStartOffset, clip.durationOverride));
            AddFloat(root, "Start Offset", clip.clipStartOffset, v => EditTimelineClip(timeline, clip, clip.clip, clip.startTime, clip.speed, clip.reversed, v, clip.durationOverride));
            AddFloat(root, "Duration Override", clip.durationOverride, v => EditTimelineClip(timeline, clip, clip.clip, clip.startTime, clip.speed, clip.reversed, clip.clipStartOffset, v));
            AddDangerButton(root, "Remove Clip", () =>
            {
                HonamiEditorController.DeleteTimelineClips(timeline, new List<HonamiTimelineClip> { clip });
                _state.SelectedTimelineClips.Clear();
                _rebuild();
            });
        }

        private void BuildTimelineEventProperties(VisualElement root, HonamiTimelineEvent evt)
        {
            var timeline = _state.ActiveTimeline;
            root.Add(Title("Timeline Event"));
            AddFloat(root, "Time", evt.time, v => HonamiEditorController.EditTimelineEvent(timeline, evt, v, evt.eventId));
            AddText(root, "Event ID", evt.eventId, v => HonamiEditorController.EditTimelineEvent(timeline, evt, evt.time, v));
            AddDangerButton(root, "Remove Event", () =>
            {
                HonamiEditorController.DeleteTimelineEvents(timeline, new List<HonamiTimelineEvent> { evt });
                _state.SelectedTimelineEvents.Clear();
                _rebuild();
            });
        }

        private void BuildSeqClipProperties(VisualElement root, HonamiSequencedAnimationClip clip)
        {
            root.Add(Title("Sequencer Clip"));
            AddObject<AnimationClip>(root, "Clip", clip.clip, v => 
            {
                _state.PreModifyState();
                HonamiEditorController.EditSequencedClip(_state.SeqNode, clip, v, clip.startTime, clip.speed);
                _state.PostModifyState();
            });
            AddFloat(root, "Start Time", clip.startTime, v => 
            {
                _state.PreModifyState();
                HonamiEditorController.EditSequencedClip(_state.SeqNode, clip, clip.clip, v, clip.speed);
                _state.PostModifyState();
            });
            AddFloat(root, "Speed", clip.speed, v => 
            {
                _state.PreModifyState();
                HonamiEditorController.EditSequencedClip(_state.SeqNode, clip, clip.clip, clip.startTime, v);
                _state.PostModifyState();
            });
        }

        private void BuildStateEventProperties(VisualElement root, HonamiEventMarker evt)
        {
            root.Add(Title("State Event"));
            AddFloat(root, "Time", evt.time, v => 
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateEvent(_state.SelectedState, evt, v, evt.eventType, evt.eventName, evt.globalEventId);
                _state.PostModifyState();
            });
            AddEnum(root, "Type", evt.eventType, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateEvent(_state.SelectedState, evt, evt.time, (HonamiEventType)v, evt.eventName, evt.globalEventId);
                _state.PostModifyState();
                _rebuild();
            });

            if (evt.eventType == HonamiEventType.Local)
                AddText(root, "Event Name", evt.eventName, v => 
                {
                    _state.PreModifyState();
                    HonamiEditorController.EditStateEvent(_state.SelectedState, evt, evt.time, evt.eventType, v, evt.globalEventId);
                    _state.PostModifyState();
                });
            else if (evt.eventType == HonamiEventType.Global)
                AddText(root, "Global ID", evt.globalEventId, v => 
                {
                    _state.PreModifyState();
                    HonamiEditorController.EditStateEvent(_state.SelectedState, evt, evt.time, evt.eventType, evt.eventName, v);
                    _state.PostModifyState();
                });

            AddDangerButton(root, "Remove Event", () =>
            {
                _state.PreModifyState();
                HonamiEditorController.DeleteStateEvents(_state.SelectedState, new List<HonamiEventMarker> { evt });
                _state.PostModifyState();
                _state.SelectedEvents.Clear();
                _rebuild();
            });
        }

        private void EditTimelineClip(HonamiTimeline timeline, HonamiTimelineClip clip, AnimationClip newClip, float start, float speed, bool reversed, float offset, float duration)
        {
            HonamiEditorController.EditTimelineClip(timeline, clip, newClip, start, speed, reversed, offset, duration);
            _rebuild();
        }

        private static Label Title(string text) => new(text)
        {
            style =
            {
                color = TimelineTheme.Text,
                fontSize = 15,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 8
            }
        };

        private void AddText(VisualElement root, string label, string value, Action<string> changed)
        {
            var field = new TextField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => { changed(evt.newValue); _rebuild(); });
            root.Add(field);
        }

        private void AddFloat(VisualElement root, string label, float value, Action<float> changed)
        {
            var field = new FloatField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => { changed(evt.newValue); _rebuild(); });
            root.Add(field);
        }

        private void AddToggle(VisualElement root, string label, bool value, Action<bool> changed)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => { changed(evt.newValue); _rebuild(); });
            root.Add(field);
        }

        private void AddEnum(VisualElement root, string label, Enum value, Action<Enum> changed)
        {
            var field = new EnumField(label, value);
            field.RegisterValueChangedCallback(evt => { changed(evt.newValue); _rebuild(); });
            root.Add(field);
        }

        private void AddObject<T>(VisualElement root, string label, T value, Action<T> changed) where T : UnityEngine.Object
        {
            var field = new ObjectField(label) { objectType = typeof(T), value = value };
            field.RegisterValueChangedCallback(evt => { changed(evt.newValue as T); _rebuild(); });
            root.Add(field);
        }

        private static void AddDangerButton(VisualElement root, string label, Action action)
        {
            var button = new Button(action) { text = label };
            button.style.marginTop = 10;
            button.style.height = 26;
            button.style.color = new Color(1f, 0.45f, 0.52f);
            root.Add(button);
        }
    }
}
#endif
