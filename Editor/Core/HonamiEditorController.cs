using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor.Core
{
    /// <summary>
    /// Centralized point for all Editor data mutations. 
    /// Isolates Undo/Redo logic and SetDirty calls from UI Views/Event Handlers.
    /// </summary>
    public static class HonamiEditorController
    {
        #region Marker Operations
        public static void AddLoopMarker(HonamiTimeline timeline, float time, float endTime)
        {
            Undo.RecordObject(timeline, "Add Loop Marker");
            timeline.markers ??= new List<HonamiTimelineMarker>();
            timeline.markers.Add(new HonamiTimelineLoopMarker { time = time, endTime = endTime });
            EditorUtility.SetDirty(timeline);
        }

        public static void MoveMarker(HonamiTimeline timeline, HonamiTimelineMarker marker, float time)
        {
            Undo.RecordObject(timeline, "Move Marker Start");
            marker.time = time;
            EditorUtility.SetDirty(timeline);
        }

        public static void MoveLoopMarkerEnd(HonamiTimeline timeline, HonamiTimelineLoopMarker marker, float endTime)
        {
            Undo.RecordObject(timeline, "Move Marker End");
            marker.endTime = endTime;
            EditorUtility.SetDirty(timeline);
        }

        public static void MoveLoopMarkerBody(HonamiTimeline timeline, HonamiTimelineLoopMarker marker, float time, float duration)
        {
            Undo.RecordObject(timeline, "Move Marker Body");
            marker.time = time;
            marker.endTime = time + duration;
            EditorUtility.SetDirty(timeline);
        }

        public static void EditMarker(HonamiTimeline timeline, HonamiTimelineMarker marker, string id, float time)
        {
            Undo.RecordObject(timeline, "Edit Marker");
            marker.id = id;
            marker.time = time;
            EditorUtility.SetDirty(timeline);
        }

        public static void EditLoopMarker(HonamiTimeline timeline, HonamiTimelineLoopMarker marker, string id, float time, float endTime)
        {
            Undo.RecordObject(timeline, "Edit Loop Marker");
            marker.id = id;
            marker.time = time;
            marker.endTime = endTime;
            EditorUtility.SetDirty(timeline);
        }

        public static void DeleteMarkers(HonamiTimeline timeline, List<HonamiTimelineMarker> markers)
        {
            Undo.RecordObject(timeline, "Delete Timeline Markers");
            foreach (var m in markers)
            {
                timeline.markers.Remove(m);
            }
            EditorUtility.SetDirty(timeline);
        }
        #endregion

        #region Event Operations (State)
        public static void AddStateEvent(HonamiState state, float time, HonamiEventType type, string eventName)
        {
            Undo.RecordObject(state, "Add Event");
            state.events ??= new List<HonamiEventMarker>();
            state.events.Add(new HonamiEventMarker { time = time, eventType = type, eventName = eventName });
            EditorUtility.SetDirty(state);
        }

        public static void EditStateEvent(HonamiState state, HonamiEventMarker ev, float time, HonamiEventType type, string eventName, string globalId)
        {
            Undo.RecordObject(state, "Edit Event");
            ev.time = time;
            ev.eventType = type;
            ev.eventName = eventName;
            ev.globalEventId = globalId;
            EditorUtility.SetDirty(state);
        }

        public static void MoveStateEvents(HonamiState state, List<HonamiEventMarker> events, Dictionary<HonamiEventMarker, float> newTimes)
        {
            Undo.RecordObject(state, "Move Event");
            foreach (var ev in events)
            {
                if (newTimes.TryGetValue(ev, out float t))
                    ev.time = t;
            }
            EditorUtility.SetDirty(state);
        }

        public static void DeleteStateEvents(HonamiState state, List<HonamiEventMarker> events)
        {
            Undo.RecordObject(state, "Delete Event");
            foreach (var ev in events)
            {
                state.events.Remove(ev);
            }
            EditorUtility.SetDirty(state);
        }

        public static void PasteStateEvents(HonamiState state, List<HonamiEventMarker> copiedEvents, float playheadTime)
        {
            Undo.RecordObject(state, "Paste Events");
            state.events ??= new List<HonamiEventMarker>();
            float min = 0;
            if (copiedEvents.Count > 0)
            {
                min = copiedEvents[0].time;
                foreach (var ev in copiedEvents)
                {
                    if (ev.time < min) min = ev.time;
                }
            }

            float off = playheadTime - min;
            foreach (var ev in copiedEvents)
            {
                var n = new HonamiEventMarker
                {
                    time = Mathf.Max(0f, ev.time + off),
                    eventType = ev.eventType,
                    eventName = ev.eventName,
                    globalEventId = ev.globalEventId
                };
                state.events.Add(n);
            }
            EditorUtility.SetDirty(state);
        }
        #endregion

        #region Timeline Track Operations
        public static void AddTimelineTrack(HonamiTimeline timeline, HonamiTimelineTrackType trackType)
        {
            Undo.RecordObject(timeline, "Add Timeline Track");
            timeline.tracks ??= new List<HonamiTimelineTrack>();
            timeline.tracks.Add(new HonamiTimelineTrack() { trackType = trackType, trackName = "New " + trackType.ToString() + " Track" });
            EditorUtility.SetDirty(timeline);
        }

        public static void EditTimelineTrack(HonamiTimeline timeline, HonamiTimelineTrack track, string name, bool muted, GameObject target)
        {
            Undo.RecordObject(timeline, "Edit Timeline Track");
            track.trackName = name;
            track.muted = muted;
            track.target = target;
            EditorUtility.SetDirty(timeline);
        }

        public static void DeleteTimelineTrack(HonamiTimeline timeline, HonamiTimelineTrack track)
        {
            Undo.RecordObject(timeline, "Remove Track");
            timeline.tracks.Remove(track);
            EditorUtility.SetDirty(timeline);
        }

        public static void ToggleTrackMute(HonamiTimeline timeline, HonamiTimelineTrack track)
        {
            Undo.RecordObject(timeline, "Toggle Track Mute");
            track.muted = !track.muted;
            EditorUtility.SetDirty(timeline);
        }
        #endregion

        #region Timeline Element Operations (Clips & Events)
        public static void AddTimelineClip(HonamiTimeline timeline, HonamiTimelineTrack track, float time, AnimationClip clip = null)
        {
            Undo.RecordObject(timeline, "Add Clip");
            track.clips.Add(new HonamiTimelineClip { startTime = time, clip = clip });
            EditorUtility.SetDirty(timeline);
        }

        public static void EditTimelineClip(HonamiTimeline timeline, HonamiTimelineClip sc, AnimationClip clip, float startTime, float speed, bool reversed, float offset, float durationOverride)
        {
            Undo.RecordObject(timeline, "Edit Timeline Clip");
            sc.clip = clip;
            sc.startTime = startTime;
            sc.speed = speed;
            sc.reversed = reversed;
            sc.clipStartOffset = offset;
            sc.durationOverride = durationOverride;
            EditorUtility.SetDirty(timeline);
        }

        public static void MoveTimelineClips(HonamiTimeline timeline, List<HonamiTimelineClip> clips, Dictionary<HonamiTimelineClip, float> newTimes)
        {
            Undo.RecordObject(timeline, "Move Timeline Clip");
            foreach (var sc in clips)
            {
                if (newTimes.TryGetValue(sc, out float t))
                    sc.startTime = t;
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void DeleteTimelineClips(HonamiTimeline timeline, List<HonamiTimelineClip> clips)
        {
            Undo.RecordObject(timeline, "Delete Timeline Clips");
            foreach (var sc in clips)
            {
                foreach (var track in timeline.tracks)
                    track.clips.Remove(sc);
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void AddTimelineEvent(HonamiTimeline timeline, HonamiTimelineTrack track, float time, string eventId)
        {
            Undo.RecordObject(timeline, "Add Event");
            track.events.Add(new HonamiTimelineEvent { time = time, eventId = eventId });
            EditorUtility.SetDirty(timeline);
        }

        public static void EditTimelineEvent(HonamiTimeline timeline, HonamiTimelineEvent ev, float time, string eventId)
        {
            Undo.RecordObject(timeline, "Edit Timeline Event");
            ev.time = time;
            ev.eventId = eventId;
            EditorUtility.SetDirty(timeline);
        }

        public static void MoveTimelineEvents(HonamiTimeline timeline, List<HonamiTimelineEvent> events, Dictionary<HonamiTimelineEvent, float> newTimes)
        {
            Undo.RecordObject(timeline, "Move Timeline Event");
            foreach (var ev in events)
            {
                if (newTimes.TryGetValue(ev, out float t))
                    ev.time = t;
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void DeleteTimelineEvents(HonamiTimeline timeline, List<HonamiTimelineEvent> events)
        {
            Undo.RecordObject(timeline, "Delete Timeline Events");
            foreach (var ev in events)
            {
                foreach (var track in timeline.tracks)
                    track.events.Remove(ev);
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void PasteTimelineElements(HonamiTimeline timeline, HonamiTimelineTrack track, List<HonamiTimelineClip> copiedClips, List<HonamiTimelineEvent> copiedEvents, float playheadTime)
        {
            Undo.RecordObject(timeline, "Paste Timeline Elements");

            if (track.trackType == HonamiTimelineTrackType.Animation && copiedClips != null && copiedClips.Count > 0)
            {
                float min = copiedClips[0].startTime;
                foreach (var c in copiedClips) if (c.startTime < min) min = c.startTime;

                float off = playheadTime - min;
                foreach (var c in copiedClips)
                {
                    var nc = new HonamiTimelineClip
                    {
                        startTime = Mathf.Max(0f, c.startTime + off),
                        clip = c.clip,
                        durationOverride = c.durationOverride,
                        speed = c.speed,
                        reversed = c.reversed
                    };
                    track.clips.Add(nc);
                }
            }
            else if (track.trackType == HonamiTimelineTrackType.Event && copiedEvents != null && copiedEvents.Count > 0)
            {
                float min = copiedEvents[0].time;
                foreach (var ev in copiedEvents) if (ev.time < min) min = ev.time;

                float off = playheadTime - min;
                foreach (var ev in copiedEvents)
                {
                    var ne = new HonamiTimelineEvent { time = Mathf.Max(0f, ev.time + off), eventId = ev.eventId };
                    track.events.Add(ne);
                }
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void SplitTimelineClips(HonamiTimeline timeline, List<HonamiTimelineClip> clipsToSplit, float playheadTime)
        {
            Undo.RecordObject(timeline, "Split Timeline Clips");
            foreach (var sc in clipsToSplit)
            {
                if (playheadTime > sc.startTime && playheadTime < sc.startTime + sc.GetDuration())
                {
                    foreach (var track in timeline.tracks)
                    {
                        if (track.clips.Contains(sc))
                        {
                            float splitLocalTime = playheadTime - sc.startTime;
                            float originalDuration = sc.GetDuration();

                            var newClip = new HonamiTimelineClip
                            {
                                clip = sc.clip,
                                startTime = playheadTime,
                                speed = sc.speed,
                                reversed = sc.reversed,
                                clipStartOffset = sc.clipStartOffset + splitLocalTime * (sc.speed != 0 ? Mathf.Abs(sc.speed) : 1f),
                                durationOverride = originalDuration - splitLocalTime
                            };

                            sc.durationOverride = splitLocalTime;
                            track.clips.Add(newClip);
                            break;
                        }
                    }
                }
            }
            EditorUtility.SetDirty(timeline);
        }
        #endregion

        #region Sequence Operations
        public static void AddSequencedClip(HonamiSequencerNode node, float time)
        {
            Undo.RecordObject(node, "Add Seq Clip");
            node.sequencedClips ??= new List<HonamiSequencedAnimationClip>();
            node.sequencedClips.Add(new HonamiSequencedAnimationClip { startTime = time });
            EditorUtility.SetDirty(node);
        }

        public static void EditSequencedClip(HonamiSequencerNode node, HonamiSequencedAnimationClip sc, AnimationClip clip, float startTime, float speed)
        {
            Undo.RecordObject(node, "Edit Seq Clip");
            sc.clip = clip;
            sc.startTime = startTime;
            sc.speed = speed;
            EditorUtility.SetDirty(node);
        }

        public static void MoveSequencedClips(HonamiSequencerNode node, List<HonamiSequencedAnimationClip> clips, Dictionary<HonamiSequencedAnimationClip, float> newTimes)
        {
            Undo.RecordObject(node, "Move Seq Clip");
            foreach (var sc in clips)
            {
                if (newTimes.TryGetValue(sc, out float t))
                    sc.startTime = t;
            }
            EditorUtility.SetDirty(node);
        }

        public static void DeleteSequencedClip(HonamiSequencerNode node, HonamiSequencedAnimationClip sc)
        {
            Undo.RecordObject(node, "Remove Seq Clip");
            node.sequencedClips.Remove(sc);
            EditorUtility.SetDirty(node);
        }
        #endregion

        #region Blend Tree Operations
        public static void AddBlendMotion(HonamiBlendTreeNode node, AnimationClip clip)
        {
            Undo.RecordObject(node, "Add Blend Motion");
            node.blendMotions ??= new List<HonamiBlendTreeMotion>();

            float threshold = 0f;
            if (node.blendMotions.Count > 0)
            {
                float max = float.MinValue;
                foreach (var motion in node.blendMotions)
                    if (motion.threshold > max) max = motion.threshold;
                threshold = max + 1f;
            }

            node.blendMotions.Add(new HonamiBlendTreeMotion { clip = clip, threshold = threshold, speed = 1f });
            EditorUtility.SetDirty(node);
        }

        public static void RemoveBlendMotion(HonamiBlendTreeNode node, int index)
        {
            if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count) return;
            Undo.RecordObject(node, "Remove Blend Motion");
            node.blendMotions.RemoveAt(index);
            EditorUtility.SetDirty(node);
        }

        public static void DuplicateBlendMotion(HonamiBlendTreeNode node, int index)
        {
            if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count) return;
            Undo.RecordObject(node, "Duplicate Blend Motion");
            var source = node.blendMotions[index];
            node.blendMotions.Insert(index + 1, new HonamiBlendTreeMotion
            {
                clip = source.clip,
                threshold = source.threshold,
                speed = source.speed,
                mirror = source.mirror
            });
            EditorUtility.SetDirty(node);
        }

        public static void DistributeBlendThresholds(HonamiBlendTreeNode node)
        {
            if (node.blendMotions == null || node.blendMotions.Count == 0) return;
            Undo.RecordObject(node, "Distribute Blend Thresholds");

            int count = node.blendMotions.Count;
            if (count == 1)
            {
                node.blendMotions[0].threshold = 0f;
            }
            else
            {
                for (int i = 0; i < count; i++)
                    node.blendMotions[i].threshold = i / (float)(count - 1);
            }

            EditorUtility.SetDirty(node);
        }
        #endregion

        #region State Edit Operations
        public static void EditStateProps(HonamiState state, string name, bool loop, bool reversed, float speed)
        {
            Undo.RecordObject(state, "Edit State Props");
            state.stateName = name;
            string shortGuid = !string.IsNullOrEmpty(state.guid) && state.guid.Length >= 8 ? state.guid.Substring(0, 8) : System.Guid.NewGuid().ToString().Substring(0, 8);
            state.name = name + "_" + shortGuid;
            if (state.node != null)
            {
                Undo.RecordObject(state.node, "Edit State Props");
                state.node.name = name + "_" + state.node.GetType().Name + "_" + shortGuid;
            }
            state.loop = loop;
            state.isReversed = reversed;
            state.speed = speed;
            EditorUtility.SetDirty(state);
            if (state.node != null) EditorUtility.SetDirty(state.node);
        }

        public static void EditAnimNodeClipTime(HonamiAnimationNode node, float startTime, float endTime)
        {
            Undo.RecordObject(node, "Change Clip Time");
            node.startTime = startTime;
            node.endTime = endTime;
            EditorUtility.SetDirty(node);
        }

        public static void EditTimelineProps(HonamiTimeline timeline, string name, float durationOverride)
        {
            Undo.RecordObject(timeline, "Edit Timeline Props");
            timeline.timelineName = name;
            timeline.durationOverride = durationOverride;
            EditorUtility.SetDirty(timeline);
        }
        #endregion

        #region Graph Operations
        public static void DeleteStateNode(HonamiRuntimeController controller, HonamiState state)
        {
            var owner = FindStateOwner(controller, state);
            if (owner == null) return;

            Undo.RecordObject(owner, "Remove State Node");
            if (owner is HonamiOverrideController overrideController) overrideController.additionalStates.Remove(state);
            else if (owner is HonamiController baseController) baseController.states.Remove(state);

            DestroyStateSubAssets(owner, state);
            Undo.DestroyObjectImmediate(state);
            EditorUtility.SetDirty(owner);

            var cacheOwner = controller.BaseController;
            if (cacheOwner != null) cacheOwner.ClearEffectiveStateCache();
        }

        private static HonamiRuntimeController FindStateOwner(HonamiRuntimeController controller, HonamiState state)
        {
            if (controller is HonamiOverrideController overrideController &&
                overrideController.additionalStates != null &&
                overrideController.additionalStates.Contains(state))
            {
                return overrideController;
            }

            return controller.BaseController;
        }

        public static void DestroyStateSubAssets(HonamiRuntimeController controller, HonamiState state)
        {
            string controllerPath = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(controllerPath)) return;

            if (state.node != null &&
                AssetDatabase.GetAssetPath(state.node) == controllerPath &&
                !IsNodeReferencedByOtherState(controller, state, state.node))
            {
                Undo.DestroyObjectImmediate(state.node);
            }

            if (state.subNodes == null) return;
            foreach (var subNode in state.subNodes)
            {
                if (subNode != null && AssetDatabase.GetAssetPath(subNode) == controllerPath)
                    Undo.DestroyObjectImmediate(subNode);
            }
        }

        private static bool IsNodeReferencedByOtherState(HonamiRuntimeController controller, HonamiState excludedState, HonamiNodeBase node)
        {
            var baseController = controller as HonamiController ?? controller.BaseController;
            if (baseController != null)
            {
                foreach (var s in baseController.states)
                {
                    if (s != null && s != excludedState && s.node == node) return true;
                }
            }

            if (controller is HonamiOverrideController overrideController)
            {
                foreach (var s in overrideController.additionalStates)
                {
                    if (s != null && s != excludedState && s.node == node) return true;
                }

                if (overrideController.overrides != null)
                {
                    foreach (var entry in overrideController.overrides)
                    {
                        if (entry?.effectiveState != null && entry.effectiveState != excludedState && entry.effectiveState.node == node) return true;
                    }
                }
            }

            return false;
        }

        public static void AddStateToController(HonamiController baseController, ScriptableObject targetCtrl, HonamiState newState, HonamiNodeBase newNode)
        {
            Undo.RegisterCreatedObjectUndo(newState, "Create State");
            if (newNode != null)
                Undo.RegisterCreatedObjectUndo(newNode, "Create Node");

            Undo.RecordObject(targetCtrl, "Add State To Controller");
            if (targetCtrl is HonamiOverrideController ov) ov.additionalStates.Add(newState);
            else baseController.states.Add(newState);

            EnsureUniqueStateName(targetCtrl as HonamiRuntimeController, newState);
            EditorUtility.SetDirty(targetCtrl);
        }

        public static void MoveStateNode(HonamiState state, Vector2 newPos)
        {
            Undo.RecordObject(state, "Move Node");
            state.editorPosition = newPos;
            EditorUtility.SetDirty(state);
        }

        public static void AddTransition(ScriptableObject targetCtrl, HonamiState sourceState, HonamiTransition transition)
        {
            Undo.RecordObject(targetCtrl, "Add Transition");
            if (sourceState != null)
                Undo.RecordObject(sourceState, "Add Transition");

            sourceState?.transitions.Add(transition);
            EditorUtility.SetDirty(targetCtrl);
            if (sourceState != null) EditorUtility.SetDirty(sourceState);
        }

        public static void RemoveTransition(HonamiState sourceState, HonamiTransition transition)
        {
            Undo.RecordObject(sourceState, "Remove Transition");
            sourceState.transitions.Remove(transition);
            EditorUtility.SetDirty(sourceState);
        }

        public static void SetDefaultState(HonamiState newDefault, HonamiState oldDefault)
        {
            if (oldDefault != null)
            {
                Undo.RecordObject(oldDefault, "Unset Default State");
                oldDefault.isDefaultState = false;
                EditorUtility.SetDirty(oldDefault);
            }
            if (newDefault != null)
            {
                Undo.RecordObject(newDefault, "Set Default State");
                newDefault.isDefaultState = true;
                EditorUtility.SetDirty(newDefault);
            }
        }

        public static void RenameGroup(HonamiController controller, HonamiGroupData group, string newTitle)
        {
            Undo.RecordObject(controller, "Rename Group");
            group.title = newTitle;
            EditorUtility.SetDirty(controller);
        }

        public static void EditGroupNodes(HonamiController controller, HonamiGroupData group, List<string> toAdd = null, List<string> toRemove = null)
        {
            bool adding = toAdd != null && toAdd.Count > 0;
            bool removing = toRemove != null && toRemove.Count > 0;
            if (adding && removing) Undo.RecordObject(controller, "Edit Group Nodes");
            else if (adding) Undo.RecordObject(controller, "Add To Group");
            else if (removing) Undo.RecordObject(controller, "Remove From Group");

            if (adding)
            {
                foreach (var id in toAdd) if (!group.containedNodes.Contains(id)) group.containedNodes.Add(id);
            }
            if (removing)
            {
                foreach (var id in toRemove) group.containedNodes.Remove(id);
            }
            if (adding || removing) EditorUtility.SetDirty(controller);
        }

        public static void AddSubNode(HonamiController controller, HonamiState state, HonamiSubNodeBase instance)
        {
            Undo.RecordObject(state, "Add Sub-Node");
            AssetDatabase.AddObjectToAsset(instance, controller);
            Undo.RegisterCreatedObjectUndo(instance, "Add Sub-Node");

            state.subNodes ??= new List<HonamiSubNodeBase>();
            state.subNodes.Add(instance);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
        }
        public static void EnsureUniqueStateName(HonamiRuntimeController controller, HonamiState state)
        {
            if (controller == null || state == null || controller.ActiveStates == null) return;

            var existingNames = new HashSet<string>();
            foreach (var s in controller.ActiveStates)
            {
                if (s != null && s != state && !string.IsNullOrEmpty(s.stateName))
                {
                    existingNames.Add(s.stateName);
                }
            }

            string baseName = state.stateName;
            int suffix = 1;
            while (existingNames.Contains(state.stateName))
            {
                state.stateName = baseName + " " + suffix;
                suffix++;
            }

            string shortGuid = !string.IsNullOrEmpty(state.guid) && state.guid.Length >= 8 ? state.guid.Substring(0, 8) : System.Guid.NewGuid().ToString().Substring(0, 8);
            state.name = state.stateName + "_" + shortGuid;
            if (state.node != null)
            {
                state.node.name = state.stateName + "_" + state.node.GetType().Name + "_" + shortGuid;
            }
        }
        #endregion
    }
}
