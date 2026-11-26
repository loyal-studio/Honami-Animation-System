using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Editor.Handlers;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor
{
    public sealed partial class HonamiGraphWindow
    {
       public void SetLayer(int index)
        {
            CurrentLayerIndex = index;
            RefreshActiveLayerView();
        }

        private void RefreshActiveLayerView()
        {
            SelectedNode = null;
            SelectedNodeGuid = null;
            SelectedTransition = null;
            SelectedTransitionGuid = null;

            if (_currentHandler is HonamiAnimatorGraphHandler handler && handler.GetMainView() is HonamiGraphView view)
                view.PopulateView(RuntimeController, CurrentLayerIndex);

            _currentHandler?.RebuildLeftPanel();
            BuildRightPanel();
        }

        public void AddParameter(HonamiParameterType type)
        {
            Undo.RecordObject(Controller, "Add Parameter");
            Controller.parameters.Add(new HonamiParameter { name = "New " + type, type = type });
            EditorUtility.SetDirty(Controller);
        }

        public void DuplicateLayer(int srcIdx)
        {
            if (Controller == null) return;

            bool srcInherited = Controller.IsLayerInherited(srcIdx);
            int srcParent = srcInherited ? Controller.layers[srcIdx].parentLayerIndex : -1;

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);
            var srcLayerProp = layersProp.GetArrayElementAtIndex(srcIdx);

            newLayerProp.FindPropertyRelative("name").stringValue = srcLayerProp.FindPropertyRelative("name").stringValue + " Copy";
            newLayerProp.FindPropertyRelative("weight").floatValue = srcLayerProp.FindPropertyRelative("weight").floatValue;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = srcLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue;
            newLayerProp.FindPropertyRelative("mirror").boolValue = srcLayerProp.FindPropertyRelative("mirror").boolValue;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = srcParent;

            if (srcInherited)
            {
                var versionProp = SerializedController.FindProperty("serializedLayerInheritanceVersion");
                if (versionProp != null) versionProp.intValue = 1;
            }

            SerializedController.ApplyModifiedProperties();

            Undo.RecordObject(Controller, "Duplicate Layer");

            var guidMap = new Dictionary<string, string>();
            var newStates = new List<HonamiState>();

            foreach (var s in Controller.states.Where(s => s?.layerIndex == srcIdx))
            {
                var ns = Instantiate(s);
                ns.name = s.name;
                bool isOverrideState = !string.IsNullOrEmpty(s.inheritedFromStateGuid);
                if (srcInherited && isOverrideState)
                {
                    ns.guid = HonamiController.GetInheritedStateGuid(s.inheritedFromStateGuid, newIdx);
                }
                else
                {
                    ns.guid = System.Guid.NewGuid().ToString();
                    ns.inheritedFromStateGuid = null;
                }
                ns.layerIndex = newIdx;

                if (ns.node != null)
                {
                    ns.node = Instantiate(ns.node);
                    ns.node.name = s.node.name;
                    AssetDatabase.AddObjectToAsset(ns.node, Controller);
                    Undo.RegisterCreatedObjectUndo(ns.node, "Dup Layer State Node");
                }

                if (ns.subNodes != null)
                {
                    var copiedSubNodes = new List<HonamiSubNodeBase>();
                    foreach (var sn in ns.subNodes)
                    {
                        if (sn != null)
                        {
                            var nsn = Instantiate(sn);
                            nsn.name = sn.name;
                            AssetDatabase.AddObjectToAsset(nsn, Controller);
                            Undo.RegisterCreatedObjectUndo(nsn, "Dup Layer State SubNode");
                            copiedSubNodes.Add(nsn);
                        }
                    }
                    ns.subNodes = copiedSubNodes;
                }

                guidMap[s.guid] = ns.guid;
                newStates.Add(ns);
                AssetDatabase.AddObjectToAsset(ns, Controller);
                Undo.RegisterCreatedObjectUndo(ns, "Dup Layer State");
            }

            foreach (var ns in newStates)
            {
                Controller.states.Add(ns);
                if (string.IsNullOrEmpty(ns.inheritedFromStateGuid))
                    HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(RuntimeController, ns);
                foreach (var t in ns.transitions)
                {
                    t.id = System.Guid.NewGuid().ToString();
                    if (guidMap.TryGetValue(t.targetStateGuid, out string ng)) t.targetStateGuid = ng;
                    else t.targetStateGuid = RemapInheritedGuidForDuplicate(t.targetStateGuid, srcIdx, newIdx);
                }
                EditorUtility.SetDirty(ns);
            }

            foreach (var g in Controller.groups.Where(g => g.layerIndex == srcIdx).ToList())
                Controller.groups.Add(new HonamiGroupData
                {
                    title = g.title,
                    position = g.position,
                    size = g.size,
                    layerIndex = newIdx,
                    containedNodes = g.containedNodes.Select(id => guidMap.TryGetValue(id, out string ng2) ? ng2 : RemapInheritedGuidForDuplicate(id, srcIdx, newIdx)).ToList()
                });

            foreach (var sn in Controller.stickyNotes.Where(sn => sn.layerIndex == srcIdx).ToList())
                Controller.stickyNotes.Add(new HonamiStickyNoteData
                {
                    title = sn.title,
                    contents = sn.contents,
                    position = sn.position,
                    size = sn.size,
                    layerIndex = newIdx,
                    themeColor = sn.themeColor
                });

            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

            Controller.ClearEffectiveStateCache();

           CurrentLayerIndex = newIdx;
            var srcName = srcLayerProp.FindPropertyRelative("name").stringValue;
            RefreshActiveLayerView();
            HonamiNotificationPanel.ShowGlobal("Layer Duplicated", $"Layer '{srcName}' duplicated.", HonamiNotificationType.Success);
        }

        public void CreateInheritedLayer(int parentIdx)
        {
            if (Controller == null || parentIdx < 0 || parentIdx >= Controller.layers.Count) return;

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);
            var parentLayerProp = layersProp.GetArrayElementAtIndex(parentIdx);
            var versionProp = SerializedController.FindProperty("serializedLayerInheritanceVersion");
            if (versionProp != null) versionProp.intValue = 1;

            newLayerProp.FindPropertyRelative("name").stringValue = parentLayerProp.FindPropertyRelative("name").stringValue + " Child";
            newLayerProp.FindPropertyRelative("weight").floatValue = parentLayerProp.FindPropertyRelative("weight").floatValue;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = parentLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue;
            newLayerProp.FindPropertyRelative("mirror").boolValue = parentLayerProp.FindPropertyRelative("mirror").boolValue;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = parentIdx;

            SerializedController.ApplyModifiedProperties();
            Controller.ClearEffectiveStateCache();
            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

           CurrentLayerIndex = newIdx;
            RefreshActiveLayerView();
            HonamiNotificationPanel.ShowGlobal("Inherited Layer Added", $"Layer now inherits from '{Controller.layers[parentIdx].name}'.", HonamiNotificationType.Success);
        }

        public void CopyLayer(int srcIdx)
        {
            if (Controller == null || srcIdx < 0 || srcIdx >= Controller.layers.Count) return;
            ClipboardController = Controller;
            ClipboardLayerIndex = srcIdx;
            var srcName = Controller.layers[srcIdx].name;
            HonamiNotificationPanel.ShowGlobal("Layer Copied", $"Layer '{srcName}' copied to clipboard.", HonamiNotificationType.Info);
        }

        public void PasteLayer()
        {
            if (Controller == null || ClipboardController == null || ClipboardLayerIndex < 0 || ClipboardLayerIndex >= ClipboardController.layers.Count)
            {
                HonamiNotificationPanel.ShowGlobal("Paste Failed", "Clipboard is empty or invalid.", HonamiNotificationType.Error);
                return;
            }

            SerializedController.Update();
            var layersProp = SerializedController.FindProperty("layers");
            int newIdx = layersProp.arraySize;
            layersProp.InsertArrayElementAtIndex(newIdx);

            var newLayerProp = layersProp.GetArrayElementAtIndex(newIdx);

            var srcLayer = ClipboardController.layers[ClipboardLayerIndex];
            newLayerProp.FindPropertyRelative("name").stringValue = srcLayer.name + " (Pasted)";
            newLayerProp.FindPropertyRelative("weight").floatValue = srcLayer.weight;
            newLayerProp.FindPropertyRelative("avatarMask").objectReferenceValue = srcLayer.avatarMask;
            newLayerProp.FindPropertyRelative("mirror").boolValue = srcLayer.mirror;
            newLayerProp.FindPropertyRelative("parentLayerIndex").intValue = -1;

            SerializedController.ApplyModifiedProperties();

            Undo.RecordObject(Controller, "Paste Layer");

            var guidMap = new Dictionary<string, string>();
            var newStates = new List<HonamiState>();

            foreach (var s in ClipboardController.states.Where(s => s?.layerIndex == ClipboardLayerIndex))
            {
                var ns = Instantiate(s);
                ns.name = s.name;
                ns.guid = System.Guid.NewGuid().ToString();
                ns.layerIndex = newIdx;

                if (ns.node != null)
                {
                    ns.node = Instantiate(ns.node);
                    ns.node.name = s.node.name;
                    AssetDatabase.AddObjectToAsset(ns.node, Controller);
                    Undo.RegisterCreatedObjectUndo(ns.node, "Paste Layer State Node");
                }

                if (ns.subNodes != null)
                {
                    var copiedSubNodes = new List<HonamiSubNodeBase>();
                    foreach (var sn in ns.subNodes)
                    {
                        if (sn != null)
                        {
                            var nsn = Instantiate(sn);
                            nsn.name = sn.name;
                            AssetDatabase.AddObjectToAsset(nsn, Controller);
                            Undo.RegisterCreatedObjectUndo(nsn, "Paste Layer State SubNode");
                            copiedSubNodes.Add(nsn);
                        }
                    }
                    ns.subNodes = copiedSubNodes;
                }

                if (ns.Modules != null)
                {
                    var copiedModules = new List<HonamiTimelineModule>();
                    foreach (var mod in ns.Modules)
                    {
                        if (mod != null)
                        {
                            var nmod = Instantiate(mod);
                            nmod.name = mod.name;
                            nmod.RegenerateId();
                            AssetDatabase.AddObjectToAsset(nmod, Controller);
                            Undo.RegisterCreatedObjectUndo(nmod, "Paste Layer State Module");
                            copiedModules.Add(nmod);
                        }
                    }
                    ns.Modules = copiedModules;
                }

                guidMap[s.guid] = ns.guid;
                newStates.Add(ns);
                AssetDatabase.AddObjectToAsset(ns, Controller);
                Undo.RegisterCreatedObjectUndo(ns, "Paste Layer State");
            }

            foreach (var ns in newStates)
            {
                Controller.states.Add(ns);
                HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(RuntimeController, ns);
                foreach (var t in ns.transitions)
                {
                    t.id = System.Guid.NewGuid().ToString();
                    if (guidMap.TryGetValue(t.targetStateGuid, out string ng)) t.targetStateGuid = ng;
                }

                var usedParameterNames = new HashSet<string>();
                try
                {
                    CollectParameterStrings(ns, usedParameterNames);
                    if (ns.node != null) CollectParameterStrings(ns.node, usedParameterNames);
                    if (ns.subNodes != null)
                    {
                        for (int i = 0; i < ns.subNodes.Count; i++) CollectParameterStrings(ns.subNodes[i], usedParameterNames);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Honami] Failed to collect parameter strings for state {ns.stateName}: {ex.Message}");
                }

                foreach (var paramName in usedParameterNames)
                {
                    bool exists = false;
                    for (int j = 0; j < Controller.parameters.Count; j++)
                    {
                        if (Controller.parameters[j].name == paramName)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        var srcParam = ClipboardController.parameters.FirstOrDefault(p => p.name == paramName);
                        if (srcParam != null)
                        {
                            Controller.parameters.Add(new HonamiParameter
                            {
                                name = srcParam.name,
                                type = srcParam.type,
                                defaultFloat = srcParam.defaultFloat,
                                defaultInt = srcParam.defaultInt,
                                defaultBool = srcParam.defaultBool
                            });
                        }
                    }
                }

                EditorUtility.SetDirty(ns);
            }

            foreach (var g in ClipboardController.groups.Where(g => g.layerIndex == ClipboardLayerIndex).ToList())
                Controller.groups.Add(new HonamiGroupData
                {
                    title = g.title,
                    position = g.position,
                    size = g.size,
                    layerIndex = newIdx,
                    containedNodes = g.containedNodes.Select(id => guidMap.TryGetValue(id, out string ng2) ? ng2 : id).ToList()
                });

            foreach (var sn in ClipboardController.stickyNotes.Where(sn => sn.layerIndex == ClipboardLayerIndex).ToList())
                Controller.stickyNotes.Add(new HonamiStickyNoteData
                {
                    title = sn.title,
                    contents = sn.contents,
                    position = sn.position,
                    size = sn.size,
                    layerIndex = newIdx,
                    themeColor = sn.themeColor
                });

            EditorUtility.SetDirty(Controller);
            HonamiGraphView.DeferredSave();

            Controller.ClearEffectiveStateCache();

           CurrentLayerIndex = newIdx;
            RefreshActiveLayerView();
            HonamiNotificationPanel.ShowGlobal("Layer Pasted", $"Layer '{srcLayer.name}' pasted successfully.", HonamiNotificationType.Success);
        }

        public void DeleteLayer(int idx, string name)
        {
            if (Controller == null || idx <= 0) return;
            if (!EditorUtility.DisplayDialog("Delete Layer",
                    $"Delete '{name}'? This removes all its states and transitions.", "Delete", "Cancel")) return;

            Undo.RecordObject(Controller, "Delete Layer");
            var guids = new HashSet<string>(Controller.states
                .Where(s => s?.layerIndex == idx).Select(s => s.guid));

            foreach (var s in Controller.states)
            {
                if (s?.transitions == null) continue;
                if (s.transitions.RemoveAll(t => guids.Contains(t.targetStateGuid)) > 0)
                    EditorUtility.SetDirty(s);
            }

            foreach (var s in Controller.states.Where(s => s != null && guids.Contains(s.guid)).ToList())
            {
                Controller.states.Remove(s);
                HonamiAnimationSystem.Editor.Core.HonamiEditorController.DestroyStateSubAssets(Controller, s);
                Undo.DestroyObjectImmediate(s);
            }
            Controller.states.RemoveAll(s => s == null);

            foreach (var s in Controller.states)
            {
                if (s?.layerIndex > idx) { s.layerIndex--; EditorUtility.SetDirty(s); }
            }

            foreach (var layer in Controller.layers)
            {
                if (layer == null) continue;
                if (layer.parentLayerIndex == idx) layer.parentLayerIndex = -1;
                else if (layer.parentLayerIndex > idx) layer.parentLayerIndex--;
            }

            SerializedController.Update();
            SerializedController.FindProperty("layers").DeleteArrayElementAtIndex(idx);
            SerializedController.ApplyModifiedProperties();
            Controller.ClearEffectiveStateCache();

            if (CurrentLayerIndex >= Controller.layers.Count)
                CurrentLayerIndex = Controller.layers.Count - 1;

           HonamiGraphView.DeferredSave();
            RefreshActiveLayerView();
            HonamiNotificationPanel.ShowGlobal("Layer Deleted", "Layer and states removed.", HonamiNotificationType.Info);
        }

        public void MoveLayer(int fromIdx, int toIdx)
        {
            if (Controller == null) return;
            int count = Controller.layers.Count;
            if (fromIdx < 0 || fromIdx >= count || toIdx < 0 || toIdx >= count || fromIdx == toIdx) return;

            var map = BuildLayerMoveMap(count, fromIdx, toIdx);

            for (int i = 0; i < count; i++)
            {
                var layer = Controller.layers[i];
                if (layer == null || layer.parentLayerIndex < 0 || layer.parentLayerIndex >= count) continue;
                if (map[layer.parentLayerIndex] >= map[i])
                {
                    HonamiNotificationPanel.ShowGlobal("Move Blocked",
                        $"'{Controller.layers[i].name}' would end up before its parent layer. Detach it from its parent first.",
                        HonamiNotificationType.Warning);
                    return;
                }
            }

            string movedName = Controller.layers[fromIdx].name;

            Undo.RecordObject(Controller, "Move Layer");

            var reordered = new HonamiLayer[count];
            for (int i = 0; i < count; i++) reordered[map[i]] = Controller.layers[i];
            Controller.layers.Clear();
            Controller.layers.AddRange(reordered);

            foreach (var layer in Controller.layers)
            {
                if (layer != null && layer.parentLayerIndex >= 0 && layer.parentLayerIndex < count)
                    layer.parentLayerIndex = map[layer.parentLayerIndex];
            }

            if ((fromIdx == 0 || toIdx == 0) && Controller.layers[0] != null)
                Controller.layers[0].weight = 1f;

            foreach (var s in Controller.states)
                RemapStateLayerData(s, map, count);

            RemapDecorationLayerIndices(Controller.groups, Controller.stickyNotes, map, count);
            RemapDependentOverrideControllers(map, count);
            RemapLayerCameraPrefs(map, count);

            Controller.ClearEffectiveStateCache();
            EditorUtility.SetDirty(Controller);
            SerializedController.Update();
            HonamiGraphView.DeferredSave();

            if (CurrentLayerIndex >= 0 && CurrentLayerIndex < count)
                CurrentLayerIndex = map[CurrentLayerIndex];

           RefreshActiveLayerView();

            if (toIdx == 0)
                HonamiNotificationPanel.ShowGlobal("Base Layer Changed", $"'{movedName}' is now the base layer.", HonamiNotificationType.Success);
            else
                HonamiNotificationPanel.ShowGlobal("Layer Moved", $"'{movedName}' moved to position {toIdx}.", HonamiNotificationType.Success);
        }

        private static int[] BuildLayerMoveMap(int count, int fromIdx, int toIdx)
        {
            var map = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (i == fromIdx)
                {
                    map[i] = toIdx;
                    continue;
                }

                int shifted = i;
                if (shifted > fromIdx) shifted--;
                if (shifted >= toIdx) shifted++;
                map[i] = shifted;
            }
            return map;
        }

        private static void RemapStateLayerData(HonamiState s, int[] map, int count)
        {
            if (s == null) return;

            int newLayer = s.layerIndex >= 0 && s.layerIndex < count ? map[s.layerIndex] : s.layerIndex;
            string newGuid = RemapInheritedLayerGuid(s.guid, map, count);

            bool changed = newLayer != s.layerIndex || !string.Equals(newGuid, s.guid, System.StringComparison.Ordinal);
            if (!changed && s.transitions != null)
            {
                foreach (var t in s.transitions)
                {
                    if (t == null) continue;
                    if (!string.Equals(RemapInheritedLayerGuid(t.targetStateGuid, map, count), t.targetStateGuid, System.StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;

            Undo.RecordObject(s, "Move Layer");
            s.layerIndex = newLayer;
            s.guid = newGuid;
            if (s.transitions != null)
            {
                foreach (var t in s.transitions)
                {
                    if (t != null) t.targetStateGuid = RemapInheritedLayerGuid(t.targetStateGuid, map, count);
                }
            }
            EditorUtility.SetDirty(s);
        }

        private static string RemapInheritedGuidForDuplicate(string guid, int fromLayer, int toLayer)
        {
            if (string.IsNullOrEmpty(guid)) return guid;

            const string marker = HonamiController.InheritedLayerGuidMarker;
            int at = guid.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (at < 0) return guid;
            if (!int.TryParse(guid.Substring(at + marker.Length), out int layer) || layer != fromLayer) return guid;

            return guid.Substring(0, at + marker.Length) + toLayer;
        }

        private static string RemapInheritedLayerGuid(string guid, int[] map, int count)
        {
            if (string.IsNullOrEmpty(guid)) return guid;

            const string marker = HonamiController.InheritedLayerGuidMarker;
            int at = guid.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (at < 0) return guid;
            if (!int.TryParse(guid.Substring(at + marker.Length), out int layer)) return guid;
            if (layer < 0 || layer >= count || map[layer] == layer) return guid;

            return guid.Substring(0, at + marker.Length) + map[layer];
        }

        private static void RemapDecorationLayerIndices(
            List<HonamiGroupData> groups,
            List<HonamiStickyNoteData> stickyNotes,
            int[] map,
            int count)
        {
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g != null && g.layerIndex >= 0 && g.layerIndex < count) g.layerIndex = map[g.layerIndex];
                }
            }

            if (stickyNotes != null)
            {
                foreach (var n in stickyNotes)
                {
                    if (n != null && n.layerIndex >= 0 && n.layerIndex < count) n.layerIndex = map[n.layerIndex];
                }
            }
        }

        private void RemapDependentOverrideControllers(int[] map, int count)
        {
            var assetGuids = AssetDatabase.FindAssets("t:HonamiOverrideController");
            foreach (var assetGuid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetGuid);
                var over = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(path);
                if (over == null || over.BaseController != Controller) continue;

                Undo.RecordObject(over, "Move Layer");

                if (over.additionalLayers != null)
                {
                    foreach (var l in over.additionalLayers)
                    {
                        if (l != null && l.parentLayerIndex >= 0 && l.parentLayerIndex < count)
                            l.parentLayerIndex = map[l.parentLayerIndex];
                    }
                }

                RemapDecorationLayerIndices(over.additionalGroups, over.additionalStickyNotes, map, count);

                if (over.additionalStates != null)
                {
                    foreach (var s in over.additionalStates)
                        RemapStateLayerData(s, map, count);
                }

                if (over.overrides != null)
                {
                    foreach (var entry in over.overrides)
                    {
                        if (entry == null) continue;

                        var remapped = RemapInheritedLayerGuid(entry.parentStateGuid, map, count);
                        if (!string.Equals(remapped, entry.parentStateGuid, System.StringComparison.Ordinal))
                        {
                            entry.parentStateGuid = remapped;
                        }

                        if (entry.effectiveState != null)
                        {
                            RemapStateLayerData(entry.effectiveState, map, count);
                            if (entry.effectiveState.transitions != null)
                            {
                                foreach (var t in entry.effectiveState.transitions)
                                {
                                    if (t != null) t.targetStateGuid = RemapInheritedLayerGuid(t.targetStateGuid, map, count);
                                }
                            }
                        }
                    }
                }

                if (over.nodePositions != null)
                {
                    for (int i = 0; i < over.nodePositions.Count; i++)
                    {
                        var np = over.nodePositions[i];
                        var remapped = RemapInheritedLayerGuid(np.stateGuid, map, count);
                        if (!string.Equals(remapped, np.stateGuid, System.StringComparison.Ordinal))
                        {
                            np.stateGuid = remapped;
                            over.nodePositions[i] = np;
                        }
                    }
                }

                over.ClearCaches();
                EditorUtility.SetDirty(over);
            }
        }

        private void RemapLayerCameraPrefs(int[] map, int count)
        {
            string assetPath = AssetDatabase.GetAssetPath(Controller);
            string controllerGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(controllerGuid)) return;

            var stored = new (bool has, float x, float y, float scale)[count];
            for (int i = 0; i < count; i++)
            {
                string keyBase = $"HonamiGraphView_{controllerGuid}_{i}";
                if (!EditorPrefs.HasKey($"{keyBase}_PosX")) continue;
                stored[i] = (true,
                    EditorPrefs.GetFloat($"{keyBase}_PosX"),
                    EditorPrefs.GetFloat($"{keyBase}_PosY"),
                    EditorPrefs.GetFloat($"{keyBase}_Scale", 1f));
            }

            for (int i = 0; i < count; i++)
            {
                string keyBase = $"HonamiGraphView_{controllerGuid}_{map[i]}";
                if (stored[i].has)
                {
                    EditorPrefs.SetFloat($"{keyBase}_PosX", stored[i].x);
                    EditorPrefs.SetFloat($"{keyBase}_PosY", stored[i].y);
                    EditorPrefs.SetFloat($"{keyBase}_Scale", stored[i].scale);
                }
                else
                {
                    EditorPrefs.DeleteKey($"{keyBase}_PosX");
                    EditorPrefs.DeleteKey($"{keyBase}_PosY");
                    EditorPrefs.DeleteKey($"{keyBase}_Scale");
                }
            }
        }

        private void CollectParameterStrings(ScriptableObject obj, HashSet<string> hashSet)
        {
            if (obj == null) return;
            var so = new SerializedObject(obj);
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    if (!string.IsNullOrEmpty(prop.stringValue))
                        hashSet.Add(prop.stringValue);
                }
            }
        }
    }
}
