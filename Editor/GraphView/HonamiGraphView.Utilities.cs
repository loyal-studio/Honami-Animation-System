using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;

namespace HonamiAnimationSystem.Editor
{
    public partial class HonamiGraphView
    {
        public static Rect GetNodeWorldRect(HonamiNode node)
        {
            Rect r = node.GetPosition();

            if (float.IsNaN(r.x) || float.IsInfinity(r.x) || float.IsNaN(r.y) || float.IsInfinity(r.y))
            {
                r.x = 0f;
                r.y = 0f;
            }

            if (r.width < 1f || float.IsNaN(r.width) || float.IsInfinity(r.width)) r.width = 160f;
            if (r.height < 1f || float.IsNaN(r.height) || float.IsInfinity(r.height)) r.height = 60f;
            return r;
        }

        public static Vector2 FixNaN(Vector2 v)
        {
            if (float.IsNaN(v.x) || float.IsInfinity(v.x)) v.x = 0f;
            if (float.IsNaN(v.y) || float.IsInfinity(v.y)) v.y = 0f;
            return v;
        }

        public static Rect FixRectNaN(Rect r)
        {
            if (float.IsNaN(r.x) || float.IsInfinity(r.x)) r.x = 0;
            if (float.IsNaN(r.y) || float.IsInfinity(r.y)) r.y = 0;
            if (float.IsNaN(r.width) || float.IsInfinity(r.width)) r.width = 100f;
            if (float.IsNaN(r.height) || float.IsInfinity(r.height)) r.height = 100f;
            return r;
        }

        public static Vector2 RaycastRect(Rect rect, Vector2 center, Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return center;

            float tBest = float.MaxValue;
            bool hit = false;

            TryPlane(rect.xMin, center.x, dir.x, ref tBest, ref hit);
            TryPlane(rect.xMax, center.x, dir.x, ref tBest, ref hit);
            TryPlane(rect.yMin, center.y, dir.y, ref tBest, ref hit);
            TryPlane(rect.yMax, center.y, dir.y, ref tBest, ref hit);

            if (!hit || tBest <= 0f || float.IsNaN(tBest) || float.IsInfinity(tBest))
                return center;

            return center + dir * tBest;
        }

        public static void TryPlane(float planeVal, float originComp, float dirComp,
                                     ref float tBest, ref bool hit)
        {
            if (Mathf.Abs(dirComp) < 0.0001f) return;
            float t = (planeVal - originComp) / dirComp;
            if (t > 0f && t < tBest) { tBest = t; hit = true; }
        }

        private static List<HonamiState> _clipboard = new List<HonamiState>();
        private static List<HonamiParameter> _clipboardParameters = new List<HonamiParameter>();
        private static HashSet<string> _tempStringHash = new HashSet<string>();

        private string GetTransitionTooltip(HonamiTransition trans)
        {
            if (trans.conditions == null || trans.conditions.Count == 0)
                return trans.hasExitTime ? $"Auto exit at {trans.exitTime * 100}%" : "Immediate transition";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Conditions:");
            foreach (var cond in trans.conditions)
            {
                string modeStr = cond.mode switch
                {
                    HonamiConditionMode.Greater => ">",
                    HonamiConditionMode.Less => "<",
                    HonamiConditionMode.Equals => "==",
                    HonamiConditionMode.NotEqual => "!=",
                    HonamiConditionMode.If => "is True",
                    HonamiConditionMode.IfNot => "is False",
                    _ => "?"
                };

                bool isBool = cond.mode == HonamiConditionMode.If || cond.mode == HonamiConditionMode.IfNot;
                string line = isBool ? $"- {cond.parameter} {modeStr}" : $"- {cond.parameter} {modeStr} {cond.threshold}";
                sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }

        private void CopySelection()
        {
            var selectedNodes = selection.OfType<HonamiNode>().ToList();
            if (selectedNodes.Count == 0) return;

            _clipboard.Clear();
            _clipboardParameters.Clear();

            if (_controller != null && _controller.parameters != null)
            {
                for (int i = 0; i < _controller.parameters.Count; i++)
                {
                    var p = _controller.parameters[i];
                    _clipboardParameters.Add(new HonamiParameter
                    {
                        name = p.name,
                        type = p.type,
                        defaultFloat = p.defaultFloat,
                        defaultInt = p.defaultInt,
                        defaultBool = p.defaultBool
                    });
                }
            }

            foreach (var node in selectedNodes)
            {
                if (node.State != null)
                {
                    var clone = ScriptableObject.Instantiate(node.State);
                    clone.name = node.State.name;
                    _clipboard.Add(clone);
                }
            }
        }

        private void PasteSelection(Vector2 position)
        {
            if (_clipboard.Count == 0 || _controller == null) return;

            Undo.RecordObject(_controller, "Paste Nodes");

            Vector2 avgPos = Vector2.zero;
            foreach (var s in _clipboard) avgPos += s.editorPosition;
            avgPos /= _clipboard.Count;

            Vector2 offset = position - avgPos;
            var newNodes = new List<HonamiNode>();

            var guidMap = new Dictionary<string, string>();
            var newStates = new List<HonamiState>();

            foreach (var s in _clipboard)
            {
                var newState = ScriptableObject.Instantiate(s);
                newState.guid = Guid.NewGuid().ToString();
                newState.editorPosition += offset;
                newState.layerIndex = currentLayerIndex;
                newState.name = s.name;

                if (newState.node != null)
                {
                    newState.node = ScriptableObject.Instantiate(newState.node);
                    newState.node.name = s.node.name;
                }

                if (newState.subNodes != null)
                {
                    var copiedSubNodes = new List<HonamiSubNodeBase>();
                    foreach (var sn in newState.subNodes)
                    {
                        if (sn != null)
                        {
                            var nsn = ScriptableObject.Instantiate(sn);
                            nsn.name = sn.name;
                            AssetDatabase.AddObjectToAsset(nsn, _controller);
                            Undo.RegisterCreatedObjectUndo(nsn, "Paste State SubNode");
                            copiedSubNodes.Add(nsn);
                        }
                    }
                    newState.subNodes = copiedSubNodes;
                }

                if (newState.Modules != null)
                {
                    var copiedModules = new List<HonamiTimelineModule>();
                    foreach (var mod in newState.Modules)
                    {
                        if (mod != null)
                        {
                            var nmod = ScriptableObject.Instantiate(mod);
                            nmod.name = mod.name;
                            nmod.RegenerateId();
                            AssetDatabase.AddObjectToAsset(nmod, _controller);
                            Undo.RegisterCreatedObjectUndo(nmod, "Paste State Module");
                            copiedModules.Add(nmod);
                        }
                    }
                    newState.Modules = copiedModules;
                }

                guidMap[s.guid] = newState.guid;
                newStates.Add(newState);
                _controller.states.Add(newState);
            }

            foreach (var newState in newStates)
            {
                if (newState.transitions != null)
                {
                    foreach (var t in newState.transitions)
                    {
                        t.id = Guid.NewGuid().ToString();
                        if (guidMap.TryGetValue(t.targetStateGuid, out string newTargetGuid))
                        {
                            t.targetStateGuid = newTargetGuid;
                        }
                    }
                }

                HonamiAnimationSystem.Editor.Core.HonamiEditorController.EnsureUniqueStateName(_runtimeController, newState);

                if (newState.node != null)
                {
                    AssetDatabase.AddObjectToAsset(newState.node, _controller);
                    Undo.RegisterCreatedObjectUndo(newState.node, "Paste State Node");
                }

                AssetDatabase.AddObjectToAsset(newState, _controller);
                Undo.RegisterCreatedObjectUndo(newState, "Paste State");
            }

            _controller.ClearEffectiveStateCache();

            // Duplicate incoming transitions from global (AnyState) nodes
            foreach (var state in _controller.states)
            {
                if (state.layerIndex == currentLayerIndex && state.node != null && state.node.IsGlobal)
                {
                    if (state.transitions == null || state.transitions.Count == 0) continue;

                    var newTransitions = new List<HonamiTransition>();
                    foreach (var t in state.transitions)
                    {
                        if (guidMap.TryGetValue(t.targetStateGuid, out string newTargetGuid))
                        {
                            var newTransition = JsonUtility.FromJson<HonamiTransition>(JsonUtility.ToJson(t));
                            newTransition.id = Guid.NewGuid().ToString();
                            newTransition.targetStateGuid = newTargetGuid;
                            newTransitions.Add(newTransition);
                        }
                    }

                    if (newTransitions.Count > 0)
                    {
                        Undo.RecordObject(state, "Paste Global Transitions");
                        state.transitions.AddRange(newTransitions);
                        EditorUtility.SetDirty(state);
                    }
                }
            }

            _tempStringHash.Clear();
            for (int i = 0; i < _clipboard.Count; i++)
            {
                var s = _clipboard[i];
                try
                {
                    CollectParameterStrings(s);
                    if (s.node != null) CollectParameterStrings(s.node);
                    if (s.subNodes != null)
                    {
                        for (int j = 0; j < s.subNodes.Count; j++)
                        {
                            CollectParameterStrings(s.subNodes[j]);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Honami] Failed to collect parameter strings for state {s.stateName}: {ex.Message}");
                }
            }

            int addedParams = 0;
            if (_controller.parameters != null)
            {
                for (int i = 0; i < _clipboardParameters.Count; i++)
                {
                    var cp = _clipboardParameters[i];
                    if (_tempStringHash.Contains(cp.name))
                    {
                        bool exists = false;
                        for (int j = 0; j < _controller.parameters.Count; j++)
                        {
                            if (_controller.parameters[j].name == cp.name)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            _controller.parameters.Add(new HonamiParameter
                            {
                                name = cp.name,
                                type = cp.type,
                                defaultFloat = cp.defaultFloat,
                                defaultInt = cp.defaultInt,
                                defaultBool = cp.defaultBool
                            });
                            addedParams++;
                        }
                    }
                }
            }

            EditorUtility.SetDirty(_controller);
            _controller.ClearEffectiveStateCache();
            PopulateView(_controller, currentLayerIndex);

            DeferredSave();

            string notifMsg = $"Pasted {_clipboard.Count} states.";
            if (addedParams > 0) notifMsg += $" Added {addedParams} missing parameters.";
            HonamiNotificationPanel.ShowGlobal("Pasted", notifMsg, HonamiNotificationType.Info);
        }

        private void CollectParameterStrings(ScriptableObject obj)
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
                        _tempStringHash.Add(prop.stringValue);
                }
            }
        }

        private void AlignNodesHorizontal()
        {
            var nodes = selection.OfType<HonamiNode>().ToList();
            if (nodes.Count < 2) return;

            Undo.RecordObject(_controller, "Align Nodes Horizontal");
            float avgY = nodes.Average(n => n.GetPosition().y);
            foreach (var n in nodes)
            {
                var rect = n.GetPosition();
                rect.y = avgY;
                n.SetPosition(rect);
                if (n.State != null)
                {
                    Undo.RecordObject(n.State, "Align Node");
                    n.State.editorPosition.y = avgY;
                    EditorUtility.SetDirty(n.State);
                }
            }
        }

        private void AlignNodesVertical()
        {
            var nodes = selection.OfType<HonamiNode>().ToList();
            if (nodes.Count < 2) return;

            Undo.RecordObject(_controller, "Align Nodes Vertical");
            float avgX = nodes.Average(n => n.GetPosition().x);
            foreach (var n in nodes)
            {
                var rect = n.GetPosition();
                rect.x = avgX;
                n.SetPosition(rect);
                if (n.State != null)
                {
                    Undo.RecordObject(n.State, "Align Node");
                    n.State.editorPosition.x = avgX;
                    EditorUtility.SetDirty(n.State);
                }
            }
        }
    }
}
