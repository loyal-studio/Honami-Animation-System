using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using HonamiAnimationSystem.Editor.Timeline;

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
            timeline.Markers.Add(new HonamiTimelineLoopMarker { time = time, endTime = endTime });
            EditorUtility.SetDirty(timeline);
        }

        public static HonamiTimelineLoopMarker AddRuntimeLoopMarker(HonamiTimeline timeline, float time, float endTime)
        {
            Undo.RecordObject(timeline, "Add Runtime Loop Region");
            var marker = new HonamiTimelineLoopMarker
            {
                time = Mathf.Max(0f, time),
                endTime = Mathf.Max(endTime, time + HonamiTimelineItem.MinDuration),
                mode = HonamiTimelineLoopMode.RuntimeLoop,
                id = UniqueMarkerId(timeline, "Loop")
            };
            timeline.Markers.Add(marker);
            EditorUtility.SetDirty(timeline);
            return marker;
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

        public static HonamiTimelineCueMarker AddCueMarker(HonamiTimeline timeline, float time)
        {
            Undo.RecordObject(timeline, "Add Marker");
            var marker = new HonamiTimelineCueMarker { time = Mathf.Max(0f, time), id = UniqueMarkerId(timeline, "Marker") };
            timeline.Markers.Add(marker);
            EditorUtility.SetDirty(timeline);
            return marker;
        }

        public static HonamiTimelineWaitMarker AddWaitMarker(HonamiTimeline timeline, float time)
        {
            Undo.RecordObject(timeline, "Add Wait Marker");
            var marker = new HonamiTimelineWaitMarker { time = Mathf.Max(0f, time), id = UniqueMarkerId(timeline, "Wait") };
            timeline.Markers.Add(marker);
            EditorUtility.SetDirty(timeline);
            return marker;
        }

        public static string UniqueMarkerId(HonamiTimeline timeline, string baseName)
        {
            for (int i = 1; ; i++)
            {
                string candidate = $"{baseName} {i}";
                if (timeline.FindMarker(candidate) == null) return candidate;
            }
        }

        public static void MoveMarkers(HonamiTimeline timeline, IReadOnlyList<HonamiTimelineMarker> markers, float delta)
        {
            Undo.RecordObject(timeline, "Move Markers");
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                float shift = Mathf.Max(delta, -marker.time);
                marker.time += shift;
                if (marker is HonamiTimelineLoopMarker loop) loop.endTime += shift;
            }
            EditorUtility.SetDirty(timeline);
        }

        public static void DeleteMarkers(HonamiTimeline timeline, List<HonamiTimelineMarker> markers)
        {
            Undo.RecordObject(timeline, "Delete Timeline Markers");
            foreach (var m in markers)
            {
                timeline.Markers.Remove(m);
            }
            EditorUtility.SetDirty(timeline);
        }
        #endregion

        #region Timeline Module Operations
        public static HonamiTimelineModule AddModule(IHonamiTimelineModuleContainer container, Type moduleType, string parentId = null)
        {
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(moduleType);
            if (container == null || editor == null || !editor.SupportsHost(container.Host)) return null;

            var module = editor.CreateModule();
            module.parentId = parentId;
            Record(container, "Add Module");

            var hostObject = (UnityEngine.Object)container;
            if (EditorUtility.IsPersistent(hostObject))
            {
                AssetDatabase.AddObjectToAsset(module, hostObject);
                Undo.RegisterCreatedObjectUndo(module, "Add Module");
            }

            container.Modules.Add(module);
            MarkModulesChanged(container);
            return module;
        }

        public static bool SetModuleParent(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, string parentId)
        {
            string previous = module.parentId;
            module.parentId = parentId;
            var node = (container as HonamiState)?.node;
            bool valid = string.IsNullOrEmpty(parentId)
                || HonamiTimelineModuleParent.Resolve(container.Modules, node, module).Kind != HonamiTimelineModuleParentKind.Missing
                && !HonamiTimelineModuleParent.HasCycle(container.Modules, node, module);
            module.parentId = previous;
            if (!valid) return false;

            Record(container, string.IsNullOrEmpty(parentId) ? "Unnest Module" : "Nest Module");
            module.parentId = parentId;
            MarkModulesChanged(container);
            return true;
        }

        public static string EnsureItemId(IHonamiTimelineModuleContainer container, HonamiTimelineItem item)
        {
            if (item == null) return null;
            if (!string.IsNullOrEmpty(item.Id)) return item.Id;

            Record(container, "Assign Item Id");
            string id = item.EnsureId();
            EditorUtility.SetDirty((UnityEngine.Object)container);
            return id;
        }

        // Ids are assigned lazily, so a node saved before motions had ids gets them the first time a module is bound.
        public static string EnsureMotionId(IHonamiTimelineModuleContainer container, int motionIndex)
        {
            var node = (container as HonamiState)?.node;
            if (node == null || motionIndex < 0 || motionIndex >= node.MotionCount) return null;

            Undo.RecordObject(node, "Assign Clip Ids");
            if (node.EnsureMotionIds())
                EditorUtility.SetDirty(node);

            string id = node.GetMotionId(motionIndex);
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public static void DeleteModule(IHonamiTimelineModuleContainer container, HonamiTimelineModule module)
        {
            var modules = container.Modules;
            var node = (container as HonamiState)?.node;
            var removed = new List<HonamiTimelineModule>();
            foreach (var candidate in modules)
            {
                if (candidate != null && HonamiTimelineModuleParent.IsWithin(modules, node, candidate, module))
                    removed.Add(candidate);
            }

            Record(container, "Remove Module");
            foreach (var candidate in removed)
            {
                modules.Remove(candidate);
                if (candidate != null)
                {
                    Undo.DestroyObjectImmediate(candidate);
                }
            }
            MarkModulesChanged(container);
        }

        public static void SetModuleMuted(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, bool muted)
        {
            Record(container, muted ? "Mute Module" : "Unmute Module");
            module.muted = muted;
            MarkModulesChanged(container);
        }

        public static void ToggleModuleMute(IHonamiTimelineModuleContainer container, HonamiTimelineModule module) =>
            SetModuleMuted(container, module, !module.muted);

        public static void MoveModule(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, int newIndex)
        {
            var modules = container.Modules;
            int oldIndex = modules.IndexOf(module);
            newIndex = Mathf.Clamp(newIndex, 0, modules.Count - 1);
            if (oldIndex < 0 || oldIndex == newIndex) return;

            Record(container, "Reorder Modules");
            modules.RemoveAt(oldIndex);
            modules.Insert(newIndex, module);
            MarkModulesChanged(container);
        }

        public static void SetModuleLocked(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, bool locked)
        {
            Record(container, locked ? "Lock Module" : "Unlock Module");
            module.locked = locked;
            MarkModulesChanged(container);
        }

        public static void SetModuleBindingKey(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, string key)
        {
            Record(container, "Set Module Binding");
            module.bindingKey = key ?? string.Empty;
            MarkModulesChanged(container);
        }

        public static void SetTimelineBinding(HonamiDirector director, HonamiTimelineModule module, UnityEngine.Object target)
        {
            if (director == null || module == null) return;
            Undo.RecordObject(director, "Bind Timeline Module");
            director.SetBinding(module, target);
            EditorUtility.SetDirty(director);
        }
        #endregion

        #region Timeline Item Operations
        public static void AddTimelineItem(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, HonamiTimelineItem item)
        {
            if (item == null) return;
            Record(container, "Add Timeline Item");
            if (module.AddItem(item))
                MarkModulesChanged(container);
        }

        public static void MoveTimelineItem(IHonamiTimelineModuleContainer container, HonamiTimelineItem item, float time)
        {
            Record(container, "Move Timeline Item");
            item.time = Mathf.Max(0f, time);
            MarkModulesChanged(container);
        }

        public static void DeleteTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items)
        {
            Record(container, "Delete Timeline Items");
            foreach (var item in items)
            {
                foreach (var module in container.Modules)
                {
                    if (module != null && module.RemoveItem(item))
                        break;
                }
            }
            MarkModulesChanged(container);
        }

        public static int PasteTimelineItems(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, IReadOnlyList<HonamiTimelineItem> items, float playheadTime)
        {
            float earliest = float.MaxValue;
            foreach (var item in items)
            {
                if (item != null && module.ItemType.IsInstanceOfType(item) && item.time < earliest)
                    earliest = item.time;
            }
            if (earliest == float.MaxValue) return 0;

            Record(container, "Paste Timeline Items");
            int pasted = 0;
            float offset = playheadTime - earliest;
            foreach (var item in items)
            {
                if (item == null || !module.ItemType.IsInstanceOfType(item)) continue;
                var clone = CloneTimelineItem(item);
                clone.time = Mathf.Max(0f, item.time + offset);
                if (module.AddItem(clone)) pasted++;
            }
            MarkModulesChanged(container);
            return pasted;
        }

        public static HonamiTimelineItem CloneTimelineItem(HonamiTimelineItem item) => HonamiTimelineModuleEditor.CloneItem(item);

        public static HonamiTimelineModule FindOwner(IHonamiTimelineModuleContainer container, HonamiTimelineItem item)
        {
            foreach (var module in container.Modules)
            {
                if (module != null && module.Contains(item)) return module;
            }
            return null;
        }

        public static List<HonamiTimelineItem> SplitTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items, IReadOnlyList<float> localTimes, HonamiTimelineEditContext context = null)
        {
            var tails = new List<HonamiTimelineItem>();
            Record(container, "Split Timeline Items");
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var module = FindOwner(container, item);
                if (module == null) continue;

                var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
                var tail = editor.SplitItem(context, item, localTimes[i]);
                if (tail != null && module.AddItem(tail))
                    tails.Add(tail);
            }
            MarkModulesChanged(container);
            return tails;
        }

        public static void NudgeTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items, float delta)
        {
            float earliest = float.MaxValue;
            foreach (var item in items)
                earliest = Mathf.Min(earliest, item.time);
            if (earliest == float.MaxValue) return;
            delta = Mathf.Max(delta, -earliest);

            Record(container, "Nudge Timeline Items");
            foreach (var item in items)
                item.time += delta;
            MarkModulesChanged(container);
        }

        public static void MoveItemsToModule(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items, HonamiTimelineModule target)
        {
            if (target == null) return;
            Record(container, "Move Items To Track");
            foreach (var item in items)
            {
                if (item == null || !target.ItemType.IsInstanceOfType(item) || target.Contains(item)) continue;
                var owner = FindOwner(container, item);
                if (owner == null || !owner.RemoveItem(item)) continue;
                target.AddItem(item);
            }
            MarkModulesChanged(container);
        }

        public static List<HonamiTimelineItem> DuplicateTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items)
        {
            float start = float.MaxValue;
            float end = float.MinValue;
            foreach (var item in items)
            {
                start = Mathf.Min(start, item.time);
                end = Mathf.Max(end, item.EndTime);
            }

            var clones = new List<HonamiTimelineItem>();
            if (start == float.MaxValue) return clones;
            float offset = Mathf.Max(end - start, 1f / 30f);

            Record(container, "Duplicate Timeline Items");
            foreach (var item in items)
            {
                var owner = FindOwner(container, item);
                if (owner == null) continue;
                var clone = CloneTimelineItem(item);
                clone.time = item.time + offset;
                if (owner.AddItem(clone)) clones.Add(clone);
            }
            MarkModulesChanged(container);
            return clones;
        }

        // Closes the gap each removed item leaves on its own track.
        public static void RippleDeleteTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items)
        {
            var sorted = new List<HonamiTimelineItem>(items);
            sorted.Sort((a, b) => b.time.CompareTo(a.time));

            Record(container, "Ripple Delete Timeline Items");
            foreach (var item in sorted)
            {
                var owner = FindOwner(container, item);
                if (owner == null) continue;
                float gap = item.Duration;
                float end = item.EndTime;
                owner.RemoveItem(item);
                if (gap <= 0f) continue;

                int count = owner.ItemCount;
                for (int i = 0; i < count; i++)
                {
                    var other = owner.GetItem(i);
                    if (other != null && other.time >= end - 0.0001f)
                        other.time = Mathf.Max(0f, other.time - gap);
                }
            }
            MarkModulesChanged(container);
        }

        // Closes a gap between items: everything starting at the gap edge slides left by the gap length.
        public static void RippleDeleteGap(IHonamiTimelineModuleContainer container, HonamiTimelineModule module, float start, float end)
        {
            if (container == null || module == null) return;
            float duration = end - start;
            if (duration <= 0f) return;

            Record(container, "Ripple Delete Gap");
            int count = module.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var item = module.GetItem(i);
                if (item != null && item.time >= end - 0.0001f)
                    item.time = Mathf.Max(0f, item.time - duration);
            }
            MarkModulesChanged(container);
        }

        public static void TrimTimelineItems(IHonamiTimelineModuleContainer container, IReadOnlyList<HonamiTimelineItem> items, IReadOnlyList<float> localTimes, bool trimStart)
        {
            Record(container, trimStart ? "Trim Start" : "Trim End");
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.IsInstant || !item.CanResize) continue;
                float time = localTimes[i];
                if (time <= item.time || time >= item.EndTime) continue;
                if (trimStart) item.TrimStart(time);
                else item.SetDuration(time - item.time);
            }
            MarkModulesChanged(container);
        }

        public static void EditTimelineProps(HonamiTimeline timeline, string name, float durationOverride)
        {
            Undo.RecordObject(timeline, "Edit Timeline Props");
            timeline.TimelineName = name;
            timeline.DurationOverride = durationOverride;
            MarkModulesChanged(timeline);
        }

        // Modules and their items are sub-asset ScriptableObjects, so the container only holds references
        // to them. Recording the container alone captures none of that state and undo/redo does nothing,
        // so the modules have to be recorded as well.
        public static void RecordUndo(IHonamiTimelineModuleContainer container, string actionName)
        {
            if (container == null) return;

            var target = (UnityEngine.Object)container;
            if (target != null)
                Undo.RecordObject(target, actionName);

            var modules = container.Modules;
            if (modules == null) return;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null)
                    Undo.RecordObject(modules[i], actionName);
            }
        }

        private static void Record(IHonamiTimelineModuleContainer container, string actionName) =>
            RecordUndo(container, actionName);

        public static void MarkModulesChanged(IHonamiTimelineModuleContainer container)
        {
            EditorUtility.SetDirty((UnityEngine.Object)container);
            container.MarkModulesChanged();
        }
        #endregion

        #region Sequence Operations
        public static void AddSequencedClip(HonamiSequencerNode node, float time)
        {
            Undo.RecordObject(node, "Add Seq Clip");
            node.sequencedClips ??= new List<HonamiSequencedAnimationClip>();
            node.sequencedClips.Add(new HonamiSequencedAnimationClip { id = HonamiNodeBase.NewMotionId(), startTime = time });
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

        #region Blend Space Operations
        public static void AddBlendSpaceMotion(HonamiBlendSpaceNode node, AnimationClip clip)
        {
            Undo.RecordObject(node, "Add Blend Space Motion");
            node.blendMotions ??= new List<HonamiBlendSpaceMotion>();

            node.blendMotions.Add(new HonamiBlendSpaceMotion
            {
                clip = clip,
                position = NextFreeBlendSpacePosition(node),
                speed = 1f
            });

            EditorUtility.SetDirty(node);
        }

        private static float[] NextFreeBlendSpacePosition(HonamiBlendSpaceNode node)
        {
            int axisCount = node.AxisCount;
            var position = new float[axisCount];
            if (axisCount == 0 || node.blendMotions.Count == 0) return position;

            // Step around the first two axes so consecutive motions do not stack on one point.
            float angle = node.blendMotions.Count * Mathf.PI * 0.5f;
            position[0] = Mathf.Sin(angle);
            if (axisCount > 1) position[1] = Mathf.Cos(angle);
            return position;
        }

        public static void RemoveBlendSpaceMotion(HonamiBlendSpaceNode node, int index)
        {
            if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count) return;
            Undo.RecordObject(node, "Remove Blend Space Motion");
            node.blendMotions.RemoveAt(index);
            EditorUtility.SetDirty(node);
        }

        public static void DuplicateBlendSpaceMotion(HonamiBlendSpaceNode node, int index)
        {
            if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count) return;
            Undo.RecordObject(node, "Duplicate Blend Space Motion");

            var source = node.blendMotions[index];
            node.blendMotions.Insert(index + 1, new HonamiBlendSpaceMotion
            {
                clip = source.clip,
                position = (float[])source.position.Clone(),
                speed = source.speed,
                mirror = source.mirror
            });

            EditorUtility.SetDirty(node);
        }

        public static void SetBlendSpaceMotionAxis(HonamiBlendSpaceNode node, int index, int axis, float value)
        {
            if (node.blendMotions == null || index < 0 || index >= node.blendMotions.Count) return;
            var motion = node.blendMotions[index];
            if (motion.position == null || axis < 0 || axis >= motion.position.Length) return;

            Undo.RecordObject(node, "Move Blend Space Motion");
            motion.position[axis] = value;
            EditorUtility.SetDirty(node);
        }

        public static void AddBlendSpaceAxis(HonamiBlendSpaceNode node, string parameterName)
        {
            Undo.RecordObject(node, "Add Blend Space Axis");
            node.blendParameters ??= new List<string>();
            node.blendParameters.Add(parameterName ?? string.Empty);
            node.OnValidateNode();
            EditorUtility.SetDirty(node);
        }

        public static void RemoveBlendSpaceAxis(HonamiBlendSpaceNode node, int axis)
        {
            if (node.blendParameters == null || axis < 0 || axis >= node.blendParameters.Count) return;
            Undo.RecordObject(node, "Remove Blend Space Axis");

            node.blendParameters.RemoveAt(axis);
            if (node.blendMotions != null)
            {
                foreach (var motion in node.blendMotions)
                {
                    if (motion?.position == null || axis >= motion.position.Length) continue;
                    var trimmed = new List<float>(motion.position);
                    trimmed.RemoveAt(axis);
                    motion.position = trimmed.ToArray();
                }
            }

            node.OnValidateNode();
            EditorUtility.SetDirty(node);
        }

        public static void SetBlendSpaceAxisParameter(HonamiBlendSpaceNode node, int axis, string parameterName)
        {
            if (node.blendParameters == null || axis < 0 || axis >= node.blendParameters.Count) return;
            Undo.RecordObject(node, "Set Blend Space Parameter");
            node.blendParameters[axis] = parameterName ?? string.Empty;
            node.OnValidateNode();
            EditorUtility.SetDirty(node);
        }

        public static void DistributeBlendSpacePositions(HonamiBlendSpaceNode node, bool centerFirst, int axisX, int axisY)
        {
            if (node.blendMotions == null || node.blendMotions.Count == 0) return;
            Undo.RecordObject(node, "Distribute Blend Space Positions");
            node.OnValidateNode();

            int count = node.blendMotions.Count;
            int first = centerFirst ? 1 : 0;
            if (centerFirst) System.Array.Clear(node.blendMotions[0].position, 0, node.blendMotions[0].position.Length);

            int ringCount = count - first;
            for (int i = first; i < count; i++)
            {
                var position = node.blendMotions[i].position;
                if (position == null || position.Length == 0) continue;

                float angle = ringCount <= 1 ? 0f : (i - first) / (float)ringCount * Mathf.PI * 2f;
                if (axisX >= 0 && axisX < position.Length) position[axisX] = Mathf.Sin(angle);
                if (axisY >= 0 && axisY < position.Length) position[axisY] = Mathf.Cos(angle);
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

        public static void EditStateHoldLastFrame(HonamiState state, bool holdLastFrame)
        {
            Undo.RecordObject(state, "Edit State Props");
            state.holdLastFrame = holdLastFrame;
            EditorUtility.SetDirty(state);
        }

        public static void EditAnimNodeClipTime(HonamiAnimationNode node, float startTime, float endTime)
        {
            Undo.RecordObject(node, "Change Clip Time");
            node.startTime = startTime;
            node.endTime = endTime;
            EditorUtility.SetDirty(node);
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

            if (state.subNodes != null)
            {
                foreach (var subNode in state.subNodes)
                {
                    if (subNode != null && AssetDatabase.GetAssetPath(subNode) == controllerPath)
                        Undo.DestroyObjectImmediate(subNode);
                }
            }

            if (state.Modules != null)
            {
                foreach (var mod in state.Modules)
                {
                    if (mod != null && AssetDatabase.GetAssetPath(mod) == controllerPath)
                        Undo.DestroyObjectImmediate(mod);
                }
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

        #region Saving
        private static readonly HashSet<string> SavedPathsBuffer = new();

        // AssetDatabase.SaveAssets also rewrites every unrelated dirty asset in the project, such as megabyte-sized dynamic font atlases.
        public static void SaveDirtyHonamiAssets()
        {
            var runtimeAssembly = typeof(HonamiRuntimeController).Assembly;
            var editorAssembly = typeof(HonamiEditorController).Assembly;
            var objects = Resources.FindObjectsOfTypeAll<ScriptableObject>();

            SavedPathsBuffer.Clear();
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                var assembly = obj.GetType().Assembly;
                if (assembly != runtimeAssembly && assembly != editorAssembly) continue;
                if (!EditorUtility.IsPersistent(obj)) continue;
                // A main asset's file can hold dirty sub-assets of non-Honami types, which SaveAssetIfDirty also writes.
                if (!EditorUtility.IsDirty(obj) && !AssetDatabase.IsMainAsset(obj)) continue;

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !SavedPathsBuffer.Add(path)) continue;
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
            }
            SavedPathsBuffer.Clear();
        }
        #endregion
    }
}
