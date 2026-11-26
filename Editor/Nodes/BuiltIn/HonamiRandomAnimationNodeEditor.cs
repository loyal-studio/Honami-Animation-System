using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiRandomAnimationNode), "Complex/Random Animation", 50)]
    public sealed class HonamiRandomAnimationNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-random";
        public override string TopBarCssClass => "honami-node-top-random";
        public override string AvatarCssClass => "honami-node-avatar-random";
        public override string IconCssClass => "honami-node-icon-random";
        public override string Tooltip => "Plays a random clip from a weighted pool.";

        public override string GetSubtitle(HonamiState state)
        {
            int count = (state.node as HonamiRandomAnimationNode)?.randomClips?.Count ?? 0;
            return $"RANDOM  {count} clips";
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        private AnimationClip[] _lastClips = new AnimationClip[0];

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Random Animation Node"));
            AddNodeField(box, nodeSO, "syncWhenLinked", "Sync When Linked");
            AddNodeField(box, nodeSO, "noRepeat", "No Repeat");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "holdLastFrame", "Freeze Last Frame");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "randomClips", "Random Clips", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                var weight = prop.FindPropertyRelative("weight");
                prop.FindPropertyRelative("id").stringValue = HonamiNodeBase.NewMotionId();
                if (clip.objectReferenceValue == null && speed.floatValue == 0f && weight.floatValue == 0f)
                {
                    speed.floatValue = 1f;
                    weight.floatValue = 1f;
                }
            });

            var node = state.node as HonamiRandomAnimationNode;
            if (node != null && node.randomClips != null)
            {
                _lastClips = new AnimationClip[node.randomClips.Count];
                for (int i = 0; i < node.randomClips.Count; i++) _lastClips[i] = node.randomClips[i].clip;
            }

            box.schedule.Execute(() =>
            {
                if (node == null || node.randomClips == null) return;

                if (_lastClips.Length != node.randomClips.Count)
                {
                    var old = _lastClips;
                    _lastClips = new AnimationClip[node.randomClips.Count];
                    for (int i = 0; i < Mathf.Min(old.Length, _lastClips.Length); i++) _lastClips[i] = old[i];
                }

                bool changed = false;
                for (int i = 0; i < node.randomClips.Count; i++)
                {
                    var rc = node.randomClips[i];
                    if (rc.clip != _lastClips[i])
                    {
                        if (rc.clip != null && _lastClips[i] != rc.clip)
                        {
                            var listProp = nodeSO.FindProperty("randomClips");
                            if (listProp != null && i < listProp.arraySize)
                            {
                                var elem = listProp.GetArrayElementAtIndex(i);
                                elem.FindPropertyRelative("endTime").floatValue = 0f;
                                elem.FindPropertyRelative("startTime").floatValue = 0f;
                                changed = true;
                            }
                        }
                        _lastClips[i] = rc.clip;
                    }
                }

                if (changed) nodeSO.ApplyModifiedProperties();
            }).Every(100);

            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiRandomAnimationNode;
            if (node == null || node.randomClips == null || node.randomClips.Count == 0) return 5f;

            if (state.RandomPreviewIdx >= 0 && state.RandomPreviewIdx < node.randomClips.Count)
            {
                var rc = node.randomClips[state.RandomPreviewIdx];
                if (rc.clip != null)
                    return rc.clip.length / (rc.speed != 0 ? Mathf.Abs(rc.speed) : 1f);
            }
            float max = 0f;
            foreach (var rc in node.randomClips)
            {
                if (rc.clip == null) continue;
                float d = rc.clip.length / (rc.speed != 0 ? Mathf.Abs(rc.speed) : 1f);
                if (d > max) max = d;
            }
            return max > 0f ? max : 5f;
        }

        public override void CollectTimelineRows(TimelineState state, List<HonamiTimelineRow> rows)
        {
            if (state.node is not HonamiRandomAnimationNode node || node.randomClips == null) return;
            for (int i = 0; i < node.randomClips.Count; i++)
            {
                var clip = node.randomClips[i].clip;
                rows.Add(new HonamiTimelineRow(clip != null ? clip.name : $"Empty Slot {i}", HonamiTimelineRow.ClipIcon, TimelineTheme.RandomTrack, i));
            }
        }

        public override bool IsMotionPreviewed(TimelineState state, int motionIndex) => motionIndex == state.RandomPreviewIdx;

        public override void DrawTimelineRow(TimelineState state, ITimelineTrackDrawer drawer, int row, float y)
        {
            var node = state.node as HonamiRandomAnimationNode;
            if (node == null || node.randomClips == null || row >= node.randomClips.Count) return;

            int index = row;
            var rc = node.randomClips[index];
            if (rc.clip == null) return;

            float speed = rc.speed != 0 ? Mathf.Abs(rc.speed) : 1f;
            float stop = rc.endTime > 0 ? rc.endTime : rc.clip.length;
            Color trackColor = index == state.RandomPreviewIdx ? TimelineTheme.PreviewTrack : TimelineTheme.RandomTrack;
            if (rc.muted)
            {
                trackColor.a *= 0.35f;
            }
            drawer.AddClipWithHandles(
                index,
                rc.muted ? rc.clip.name + "  (muted)" : rc.clip.name,
                rc.startTime,
                Mathf.Max(0f, stop - rc.startTime),
                speed,
                y,
                trackColor,
                newStart =>
                {
                    Undo.RecordObject(node, "Trim Random Anim Start");
                    rc.startTime = newStart;
                    node.randomClips[index] = rc;
                    EditorUtility.SetDirty(node);
                },
                newEnd =>
                {
                    Undo.RecordObject(node, "Trim Random Anim End");
                    rc.endTime = newEnd >= rc.clip.length - 0.01f ? 0f : newEnd;
                    node.randomClips[index] = rc;
                    EditorUtility.SetDirty(node);
                },
                () =>
                {
                    state.Mode = TimelineMode.HonamiClipEdit;
                    state.ActiveClip = rc.clip;
                },
                null,
                rc.clip.length
            );
        }

        public override bool SupportsTimelinePreview => true;

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            var node = state.node as HonamiRandomAnimationNode;
            if (node == null || node.randomClips == null) return;
            if (state.RandomPreviewIdx < 0 || state.RandomPreviewIdx >= node.randomClips.Count) return;

            var rc = node.randomClips[state.RandomPreviewIdx];
            if (rc?.clip != null)
            {
                GameObject matchedTarget = TimelinePreview.FindBestTargetForClip(state, rootTarget, rc.clip);
                float cs = rc.speed != 0 ? Mathf.Abs(rc.speed) : 1f;
                float stop = rc.endTime > 0 ? rc.endTime : rc.clip.length;
                float dur = Mathf.Max(0.001f, stop - rc.startTime);
                double rawTime = internalT * cs;
                if (state.SelectedState.loop && dur > 0)
                {
                    double prev = rawTime;
                    rawTime %= dur;
                    if (prev > 0.0 && rawTime < 1e-4) rawTime = dur;
                }
                else rawTime = System.Math.Max(0.0, System.Math.Min(rawTime, (double)dur));
                float sTime = rc.startTime + (float)rawTime;
                if (state.SelectedState.isReversed) sTime = stop - (float)rawTime;
                TimelinePreview.SafeSample(state, matchedTarget, rc.clip, sTime);
                if (rc.mirror) TimelinePreview.ApplyManualMirror(matchedTarget);
            }
        }
    }
}
