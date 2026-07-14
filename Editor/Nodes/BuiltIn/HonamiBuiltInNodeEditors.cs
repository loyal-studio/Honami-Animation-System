#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Editor.Timeline;

namespace HonamiAnimationSystem.Editor
{
    [HonamiNodeEditor(typeof(HonamiAnimationNode), "Basic/Animation", 10)]
    public sealed class HonamiAnimationNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-animation";
        public override string TopBarCssClass => "honami-node-top-animation";
        public override string AvatarCssClass => "honami-node-avatar-animation";
        public override string IconCssClass => "honami-node-icon-animation";
        public override string Tooltip => "Plays a single animation clip.";
        public override string GetSubtitle(HonamiState _) => "ANIMATION";

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        private AnimationClip _lastClip;

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Animation Node"));
            AddNodeField(box, nodeSO, "clip", "Motion");
            AddNodeField(box, nodeSO, "startTime", "Clip Start");
            AddNodeField(box, nodeSO, "endTime", "Clip End");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");

            var node = state.node as HonamiAnimationNode;
            if (node != null) _lastClip = node.clip;

            box.schedule.Execute(() =>
            {
                if (node == null) return;
                if (node.clip != _lastClip)
                {
                    if (node.clip != null && _lastClip != node.clip)
                    {
                        var endTimeProp = nodeSO.FindProperty("endTime");
                        if (endTimeProp != null) endTimeProp.floatValue = 0f;
                        var startTimeProp = nodeSO.FindProperty("startTime");
                        if (startTimeProp != null) startTimeProp.floatValue = 0f;
                        nodeSO.ApplyModifiedProperties();
                    }
                    _lastClip = node.clip;
                }
            }).Every(100);

            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null) return node.clip.length;
            return 5f;
        }

        public override void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null)
            {
                float stop = node.endTime > 0 ? node.endTime : node.clip.length;
                drawer.AddClipWithHandles(
                    0,
                    node.clip.name,
                    node.startTime,
                    Mathf.Max(0, stop - node.startTime),
                    1f,
                    y,
                    TimelineTheme.AnimationTrack,
                    newStart =>
                    {
                        Undo.RecordObject(node, "Trim Anim Start");
                        node.startTime = newStart;
                        EditorUtility.SetDirty(node);
                    },
                    newEnd =>
                    {
                        Undo.RecordObject(node, "Trim Anim End");
                        node.endTime = newEnd >= node.clip.length - 0.01f ? 0f : newEnd;
                        EditorUtility.SetDirty(node);
                    },
                    () =>
                    {
                        state.Mode = TimelineMode.HonamiClipEdit;
                        state.ActiveClip = node.clip;
                    },
                    null,
                    node.clip.length
                );
                y += TimelineTheme.RowHeight;
            }
        }

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            var node = state.node as HonamiAnimationNode;
            if (node != null && node.clip != null)
            {
                GameObject matchedTarget = TimelinePreview.FindBestTargetForClip(state, rootTarget, node.clip);
                float sTime = state.PlayheadTime;
                if (state.SelectedState.isReversed) sTime = node.clip.length - sTime;
                TimelinePreview.SafeSample(state, matchedTarget, node.clip, sTime);
            }
        }
    }

    [HonamiNodeEditor(typeof(HonamiExitNode), "Basic/Exit", 30)]
    public sealed class HonamiExitNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-exit";
        public override string TopBarCssClass => "honami-node-top-exit";
        public override string AvatarCssClass => "honami-node-avatar-exit";
        public override string IconCssClass => "honami-node-icon-exit";
        public override string Tooltip => "Exits the current layer - fades it out.";
        public override bool HasOutput => false;
        public override string GetSubtitle(HonamiState _) => "EXIT";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Exit Node"));
            box.Add(new HelpBox("Transition to this node to smoothly deactivate the layer.", HelpBoxMessageType.Info));
            return box;
        }
    }

    [HonamiNodeEditor(typeof(HonamiBlendTreeNode), "Complex/Blend Tree", 40)]
    public sealed class HonamiBlendTreeNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-blend";
        public override string TopBarCssClass => "honami-node-top-blend";
        public override string AvatarCssClass => "honami-node-avatar-blend";
        public override string IconCssClass => "honami-node-icon-blend";
        public override string Tooltip => "Blends multiple clips based on a float parameter.";

        public override string GetSubtitle(HonamiState state)
        {
            HonamiBlendTreeNode n = state.node as HonamiBlendTreeNode;
            return "BLEND  " + (string.IsNullOrEmpty(n?.blendParameter) ? "-" : n.blendParameter);
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiBlendTreeWindow.OpenWindow();
            HonamiBlendTreeWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
        {
            root.Add(BlendTreeButton(state, ctrl));
            root.Add(TimelineButton(state, ctrl));
        }

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Blend Tree Node"));
            AddNodeField(box, nodeSO, "blendType", "Blend Mode");
            AddParameterPopup(box, nodeSO, controller, "blendParameter", "Blend Parameter");
            AddNodeField(box, nodeSO, "blendParameterDampTime", "Damp Time");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "blendMotions", "Blend Motions", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                if (clip.objectReferenceValue == null && speed.floatValue == 0f)
                    speed.floatValue = 1f;
            });
            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiBlendTreeNode;
            if (node == null || node.blendMotions == null) return 5f;
            return HonamiAnimationSystem.Runtime.Core.HonamiBlendTreeEvaluator.GetDurationFromMotions(node.blendMotions, state.BlendPreviewValue);
        }

        public override void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y)
        {
            var node = state.node as HonamiBlendTreeNode;
            if (node == null || node.blendMotions == null) return;

            int mCount = node.blendMotions.Count;
            Span<float> weights = mCount <= 32 ? stackalloc float[mCount] : new float[mCount];
            Span<(float threshold, int index)> sorted = mCount <= 32 ? stackalloc (float, int)[mCount] : new (float, int)[mCount];
            HonamiBlendWeightUtility.CalculateWeights(node.blendMotions, state.BlendPreviewValue, weights, sorted);

            for (int i = 0; i < mCount; i++)
            {
                var motion = node.blendMotions[i];
                if (motion.clip != null)
                {
                    float speed = motion.speed != 0 ? Mathf.Abs(motion.speed) : 1f;
                    string label = $"{motion.clip.name}  {weights[i] * 100f:F0}%";
                    drawer.AddReadonlyClip(motion.clip, label, 0f, motion.clip.length / speed, y, weights[i] > 0.001f ? TimelineTheme.PreviewTrack : TimelineTheme.RandomTrack);
                }
                y += TimelineTheme.RowHeight;
            }
        }

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            TimelinePreview.SampleBlendTree(state, rootTarget, internalT);
        }
    }

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
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "randomClips", "Random Clips", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                var weight = prop.FindPropertyRelative("weight");
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

        public override void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y)
        {
            var node = state.node as HonamiRandomAnimationNode;
            if (node == null || node.randomClips == null) return;

            for (int i = 0; i < node.randomClips.Count; i++)
            {
                int index = i;
                var rc = node.randomClips[index];
                if (rc.clip != null)
                {
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
                y += TimelineTheme.RowHeight;
            }
        }

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

    [HonamiNodeEditor(typeof(HonamiSequencerNode), "Complex/Sequencer", 60)]
    public sealed class HonamiSequencerNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-sequencer";
        public override string TopBarCssClass => "honami-node-top-sequencer";
        public override string AvatarCssClass => "honami-node-avatar-sequencer";
        public override string IconCssClass => "honami-node-icon-sequencer";
        public override string Tooltip => "Plays a sequence of clips with defined start times.";

        public override string GetSubtitle(HonamiState state)
        {
            int count = (state.node as HonamiSequencerNode)?.sequencedClips?.Count ?? 0;
            return $"SEQUENCER  {count} clips";
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Sequencer Node"));
            AddStateField(box, state, "loop", "Loop Sequence");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            AddNodeListField(box, nodeSO, "sequencedClips", "Sequenced Clips", prop =>
            {
                var clip = prop.FindPropertyRelative("clip");
                var speed = prop.FindPropertyRelative("speed");
                var startTime = prop.FindPropertyRelative("startTime");
                if (clip.objectReferenceValue == null && speed.floatValue == 0f && startTime.floatValue == 0f)
                {
                    speed.floatValue = 1f;
                }
            });
            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null) return 5f;
            float max = 1f;
            foreach (var sc in node.sequencedClips)
            {
                if (sc.clip == null) continue;
                float eff = sc.speed != 0 ? Mathf.Abs(sc.speed) : 1f;
                float d = sc.startTime + sc.clip.length / eff;
                if (d > max) max = d;
            }
            return max;
        }

        public override void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null) return;
            foreach (var clip in node.sequencedClips)
            {
                if (clip?.clip != null)
                {
                    float speed = clip.speed != 0 ? Mathf.Abs(clip.speed) : 1f;
                    drawer.AddClipElement(clip, clip.clip.name, clip.startTime, clip.clip.length / speed, y, state.SelectedSeqClips.Contains(clip), TimelineTheme.SequencerTrack);
                }
                y += TimelineTheme.RowHeight;
            }
        }

        public override void SampleTimelinePreview(TimelineState state, GameObject rootTarget, float internalT)
        {
            var node = state.node as HonamiSequencerNode;
            if (node == null || node.sequencedClips == null) return;
            foreach (var sc in node.sequencedClips)
            {
                if (sc.clip == null) continue;
                float eff = sc.speed != 0 ? Mathf.Abs(sc.speed) : 1f;
                float start = sc.startTime;
                float end = start + sc.clip.length / eff;
                if (internalT >= start && internalT < end)
                {
                    GameObject matchedTarget = TimelinePreview.FindBestTargetForClip(state, rootTarget, sc.clip);
                    float sTime = (internalT - start) * eff;
                    TimelinePreview.SafeSample(state, matchedTarget, sc.clip, sTime);
                }
            }
        }
    }

    [HonamiNodeEditor(typeof(HonamiAnyStateNode), "Logic/Any State", 20)]
    public sealed class HonamiAnyStateNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-any";
        public override string TopBarCssClass => "honami-node-top-any";
        public override string AvatarCssClass => "honami-node-avatar-any";
        public override string IconCssClass => "honami-node-icon-any";
        public override string Tooltip => "Global transition source - fires from any active state.";
        public override bool HasInput => false;
        public override string GetSubtitle(HonamiState _) => "ANY STATE";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Any State Node"));
            box.Add(new HelpBox("Transitions from this node can fire regardless of which state is active.", HelpBoxMessageType.Info));
            return box;
        }
    }

    [HonamiNodeEditor(typeof(HonamiEventNode), "Logic/Event", 25)]
    public sealed class HonamiEventNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-event";
        public override string TopBarCssClass => "honami-node-top-event";
        public override string AvatarCssClass => "honami-node-avatar-event";
        public override string IconCssClass => "honami-node-icon-event";
        public override string Tooltip => "Plays no animation - a pure logic state that fires events and Sub-Nodes for a set duration.";

        public override string GetSubtitle(HonamiState state)
        {
            HonamiEventNode n = state.node as HonamiEventNode;
            return n != null ? $"EVENT  {n.duration:F1}s" : "EVENT";
        }

        public override void OnDoubleClick(HonamiState state, HonamiController ctrl)
        {
            HonamiTimelineWindow.OpenWindow();
            HonamiTimelineWindow.InspectState(ctrl, state.guid);
        }

        public override void AppendActionButtons(VisualElement root, HonamiState state, HonamiController ctrl)
            => root.Add(TimelineButton(state, ctrl));

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Event Node"));
            AddNodeField(box, nodeSO, "duration", "Duration (s)");
            AddStateField(box, state, "loop", "Loop");
            AddStateField(box, state, "speed", "Speed");
            AddStateField(box, state, "isReversed", "Reverse");
            box.Add(new HelpBox("This state plays no animation. Place Local/Global event markers on its timeline, stack Sub-Nodes and assign parameters on enter/exit to run logic without a clip.", HelpBoxMessageType.Info));
            return box;
        }

        public override float GetTimelineDuration(TimelineState state)
        {
            var node = state.node as HonamiEventNode;
            return node != null && node.duration > 0f ? node.duration : 1f;
        }

        public override void DrawTimelineTracks(TimelineState state, ITimelineTrackDrawer drawer, ref float y)
        {
            var node = state.node as HonamiEventNode;
            if (node == null) return;

            drawer.AddClipWithHandles(
                0,
                "Logic Window",
                0f,
                Mathf.Max(0.01f, node.duration),
                1f,
                y,
                TimelineTheme.EventTrack,
                null,
                newEnd =>
                {
                    Undo.RecordObject(node, "Resize Event Duration");
                    node.duration = Mathf.Max(0.01f, newEnd);
                    EditorUtility.SetDirty(node);
                },
                null,
                null);
            y += TimelineTheme.RowHeight;
        }
    }

    [HonamiNodeEditor(typeof(HonamiRepeaterNode), "Logic/Repeater", 70)]
    public sealed class HonamiRepeaterNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-repeater";
        public override string TopBarCssClass => "honami-node-top-repeater";
        public override string AvatarCssClass => "honami-node-avatar-repeater";
        public override string IconCssClass => "honami-node-icon-repeater";
        public override string Tooltip => "Fires transitions periodically from any active state.";
        public override bool HasInput => false;

        public override string GetSubtitle(HonamiState state)
        {
            HonamiRepeaterNode n = state.node as HonamiRepeaterNode;
            if (n == null) return "REPEATER";
            return n.comboMode ? $"COMBO  {n.repeatCooldown:F1}s" : $"REPEATER  {n.repeatCooldown:F1}s";
        }

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Repeater Node"));
            AddNodeField(box, nodeSO, "repeatCooldown", "Cooldown (s)");
            AddNodeField(box, nodeSO, "maxRepeats", "Max Repeats");
            box.Add(new HelpBox("0 max repeats = unlimited.", HelpBoxMessageType.Info));

            AddNodeField(box, nodeSO, "comboMode", "Combo Mode");

            VisualElement comboGroup = new VisualElement();
            AddNodeField(comboGroup, nodeSO, "cancelWindow", "Cancel Window");
            AddNodeField(comboGroup, nodeSO, "comboResetTime", "Combo Reset (s)");
            AddNodeField(comboGroup, nodeSO, "loopCombo", "Loop Combo");
            comboGroup.Add(new HelpBox("Combo mode fires outgoing transitions in order, one per trigger. A trigger pressed before the current attack reaches Cancel Window is buffered and fires when the window opens.", HelpBoxMessageType.Info));
            box.Add(comboGroup);

            SerializedProperty comboModeProp = nodeSO.FindProperty("comboMode");
            comboGroup.style.display = comboModeProp != null && comboModeProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (comboModeProp != null)
            {
                comboGroup.TrackPropertyValue(comboModeProp, p =>
                    comboGroup.style.display = p.boolValue ? DisplayStyle.Flex : DisplayStyle.None);
            }

            return box;
        }
    }

    [HonamiNodeEditor(typeof(HonamiPortalEntranceNode), "Logic/Portal Entrance", 80)]
    public sealed class HonamiPortalEntranceNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-portal-entrance";
        public override string TopBarCssClass => "honami-node-top-portal-entrance";
        public override string AvatarCssClass => "honami-node-avatar-portal-entrance";
        public override string IconCssClass => "honami-node-icon-portal-entrance";
        public override string Tooltip => "Entrance of a named portal pair - routes to the matching Exit.";
        public override bool HasOutput => false;

        public override string GetTitle(HonamiState state)
        {
            HonamiPortalEntranceNode n = state.node as HonamiPortalEntranceNode;
            return string.IsNullOrEmpty(n?.portalName) ? "UNNAMED" : n.portalName;
        }

        public override string GetSubtitle(HonamiState _) => "PORTAL IN";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Portal Entrance"));
            AddNodeField(box, nodeSO, "portalName", "Portal Name");
            box.Add(new HelpBox("Transitions INTO this node redirect to the matching Portal Exit.", HelpBoxMessageType.Info));
            return box;
        }
    }

    [HonamiNodeEditor(typeof(HonamiPortalExitNode), "Logic/Portal Exit", 90)]
    public sealed class HonamiPortalExitNodeEditor : HonamiNodeEditorBase
    {
        public override string NodeCssClass => "honami-node-portal-exit";
        public override string TopBarCssClass => "honami-node-top-portal-exit";
        public override string AvatarCssClass => "honami-node-avatar-portal-exit";
        public override string IconCssClass => "honami-node-icon-portal-exit";
        public override string Tooltip => "Exit of a named portal pair - transitions FROM here.";
        public override bool HasInput => false;

        public override string GetTitle(HonamiState state)
        {
            HonamiPortalExitNode n = state.node as HonamiPortalExitNode;
            return string.IsNullOrEmpty(n?.portalName) ? "UNNAMED" : n.portalName;
        }

        public override string GetSubtitle(HonamiState _) => "PORTAL OUT";

        public override VisualElement BuildInspectorUI(HonamiState state, SerializedObject nodeSO, HonamiController controller)
        {
            VisualElement box = HonamiGraphStyles.Box();
            box.Add(HonamiGraphStyles.SubTitle("Portal Exit"));
            AddNodeField(box, nodeSO, "portalName", "Portal Name");
            box.Add(new HelpBox("Transitions FROM this node determine where the paired Entrance leads.", HelpBoxMessageType.Info));
            return box;
        }
    }
}
#endif

