using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using HonamiAnimationSystem.Editor.Core;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiValidatorWindow : EditorWindow
    {
        private HonamiController _controller;
        private Vector2 _scrollPos;
        private List<string> _logMessages = new List<string>();
        private readonly List<string> _pendingMissingScriptPaths = new();

        [MenuItem("Window/Honami/Honami Controller Validator")]
        public static void ShowWindow()
        {
            var w = GetWindow<HonamiValidatorWindow>("Honami Controller Validator");
            w.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("Honami Controller Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan ALL in Project", GUILayout.Height(30)))
            {
                ProcessAllControllers(true);
            }
            if (GUILayout.Button("Fix ALL in Project", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Fix All Controllers",
                    "Are you sure you want to validate and fix ALL Honami Controllers in the project? This might take a moment.",
                    "Yes, Fix All", "Cancel"))
                {
                    ProcessAllControllers(false);
                }
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            _controller = (HonamiController)EditorGUILayout.ObjectField("Select Controller:", _controller, typeof(HonamiController), false);

            if (_controller == null)
            {
                EditorGUILayout.HelpBox("Select a Honami Controller to scan or fix a specific one.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Scan Selected", GUILayout.Height(30)))
                {
                    ScanSelected();
                }
                if (GUILayout.Button("Fix Selected", GUILayout.Height(30)))
                {
                    ValidateAndFix();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Log:", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, "box");
            if (_logMessages.Count == 0)
            {
                EditorGUILayout.LabelField("No logs yet. Click a 'Scan' or 'Fix' button to run.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                float width = EditorGUIUtility.currentViewWidth - 40f;
                foreach (var msg in _logMessages)
                {
                    float height = EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(msg), width);
                    EditorGUILayout.SelectableLabel(msg, EditorStyles.wordWrappedLabel, GUILayout.Height(height));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Log(string message)
        {
            _logMessages.Add(message);
        }

        private void ScanSelected()
        {
            if (_controller == null) return;
            _logMessages.Clear();
            Log($"Scanning controller: '{_controller.name}'...");
            ValidateController(_controller, true);
        }

        private void ValidateAndFix()
        {
            if (_controller == null) return;

            _logMessages.Clear();
            _pendingMissingScriptPaths.Clear();
            Log($"Starting validation and fix for controller: '{_controller.name}'...");

            bool dirty = ValidateController(_controller, false);

            if (dirty)
            {
                AssetDatabase.SaveAssets();
                FlushMissingScriptCleanup();
                Log("\nDone. Fixes were applied and saved successfully.");
                Debug.Log($"[Honami Validator] Validated controller '{_controller.name}', applied and saved fixes.");
            }
            else
            {
                Log("\nDone. Controller is valid! No fixes were needed.");
            }
        }

        private void ProcessAllControllers(bool dryRun)
        {
            _logMessages.Clear();
            _pendingMissingScriptPaths.Clear();
            Log(dryRun ? "Scanning all HonamiControllers in project..." : "Fixing all HonamiControllers in project...");

            string[] guids = AssetDatabase.FindAssets("t:HonamiController");
            int count = 0;
            int issueCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ctrl = AssetDatabase.LoadAssetAtPath<HonamiController>(path);
                if (ctrl != null)
                {
                    count++;
                    Log($"\n--- Checking: {ctrl.name} ---");
                    bool hasIssues = ValidateController(ctrl, dryRun);
                    if (hasIssues)
                    {
                        issueCount++;
                    }
                }
            }

            issueCount += ProcessOverrideMigrations(dryRun);

            if (!dryRun && issueCount > 0)
            {
                AssetDatabase.SaveAssets();
                FlushMissingScriptCleanup();
            }

            string action = dryRun ? "had issues" : "were fixed";
            if (issueCount > 0)
            {
                Log($"\n[Summary] Checked {count} controllers. {issueCount} controllers {action}.");
            }
            else
            {
                Log($"\n[Summary] Checked {count} controllers. All valid, no issues found.");
            }
        }

        private int ProcessOverrideMigrations(bool dryRun)
        {
            int issues = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HonamiOverrideController"))
            {
                var ov = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(AssetDatabase.GUIDToAssetPath(guid));
                if (ov == null || ov.parentController == null || !ov.NeedsMigration) continue;

                issues++;
                if (dryRun)
                {
                    Log($"\n[{ov.name}] [Issue] Legacy override schema — needs migration to the per-field format.");
                }
                else
                {
                    HonamiOverrideAuthoring.MigrateLegacy(ov);
                    Log($"\n[{ov.name}] [Fix] Migrated legacy overrides to the per-field format.");
                }
            }

            return issues;
        }

        private bool ValidateController(HonamiController ctrl, bool dryRun)
        {
            bool hasIssues = false;

            if (!dryRun)
            {
                Undo.RecordObject(ctrl, "Fix Controller Validation");
            }

            if (ctrl.states == null)
            {
                if (dryRun) Log($"[{ctrl.name}] [Issue] States list is null.");
                else
                {
                    ctrl.states = new List<HonamiState>();
                    Log($"[{ctrl.name}] [Fix] Initialized states list.");
                }
                hasIssues = true;
            }

            // 1. Check for null states
            int nullCount = 0;
            if (ctrl.states != null)
            {
                for (int i = 0; i < ctrl.states.Count; i++)
                {
                    if (ctrl.states[i] == null) nullCount++;
                }
            }

            if (nullCount > 0)
            {
                if (dryRun) Log($"[{ctrl.name}] [Issue] Found {nullCount} null states in controller.");
                else
                {
                    ctrl.states.RemoveAll(s => s == null);
                    Log($"[{ctrl.name}] [Fix] Removed {nullCount} null states.");
                }
                hasIssues = true;
            }

            // 2. Check for Name Hash collisions
            var nameHashes = new HashSet<int>();
            if (ctrl.states != null)
            {
                foreach (var state in ctrl.states)
                {
                    if (state == null) continue;

                    int hash = state.stateName.GetHashCode();
                    if (!nameHashes.Add(hash))
                    {
                        if (dryRun)
                        {
                            Log($"[{ctrl.name}] [Issue] Name Hash collision detected for state '{state.stateName}'.");
                        }
                        else
                        {
                            Undo.RecordObject(state, "Rename State (Hash Collision)");
                            string oldName = state.stateName;
                            int suffix = 1;

                            while (nameHashes.Contains((state.stateName + " " + suffix).GetHashCode()))
                            {
                                suffix++;
                            }

                            state.stateName = state.stateName + " " + suffix;
                            nameHashes.Add(state.stateName.GetHashCode());
                            EditorUtility.SetDirty(state);
                            Log($"[{ctrl.name}] [Fix] Fixed Name Hash collision: Renamed '{oldName}' to '{state.stateName}'.");
                        }
                        hasIssues = true;
                    }
                }
            }

            // 3. Check for empty GUIDs or GUID collisions
            var guids = new HashSet<string>();
            if (ctrl.states != null)
            {
                foreach (var state in ctrl.states)
                {
                    if (state == null) continue;

                    if (string.IsNullOrEmpty(state.guid))
                    {
                        if (dryRun) Log($"[{ctrl.name}] [Issue] State '{state.stateName}' has empty/missing GUID.");
                        else
                        {
                            Undo.RecordObject(state, "Generate missing GUID");
                            state.guid = System.Guid.NewGuid().ToString();
                            guids.Add(state.guid);
                            EditorUtility.SetDirty(state);
                            Log($"[{ctrl.name}] [Fix] Generated missing GUID for state '{state.stateName}'.");
                        }
                        hasIssues = true;
                    }
                    else if (!guids.Add(state.guid))
                    {
                        if (dryRun) Log($"[{ctrl.name}] [Issue] GUID collision detected for state '{state.stateName}'.");
                        else
                        {
                            Undo.RecordObject(state, "Fix GUID Collision");
                            state.guid = System.Guid.NewGuid().ToString();
                            guids.Add(state.guid);
                            EditorUtility.SetDirty(state);
                            Log($"[{ctrl.name}] [Fix] Fixed GUID collision for state '{state.stateName}'.");
                        }
                        hasIssues = true;
                    }
                }
            }

            // 4. Check for invalid layer indices
            int layerCount = ctrl.layers.Count;
            if (layerCount == 0)
            {
                if (dryRun) Log($"[{ctrl.name}] [Issue] Controller has 0 layers.");
                else
                {
                    ctrl.layers.Add(new HonamiLayer() { name = "Base Layer" });
                    layerCount = 1;
                    Log($"[{ctrl.name}] [Fix] Created missing Base Layer.");
                }
                hasIssues = true;
            }

            if (ctrl.states != null)
            {
                foreach (var state in ctrl.states)
                {
                    if (state == null) continue;

                    if (state.layerIndex < 0 || state.layerIndex >= layerCount)
                    {
                        if (dryRun) Log($"[{ctrl.name}] [Issue] Invalid layer index {state.layerIndex} on state '{state.stateName}'.");
                        else
                        {
                            Undo.RecordObject(state, "Fix State Layer Index");
                            Log($"[{ctrl.name}] [Fix] Fixed invalid layer index {state.layerIndex} on state '{state.stateName}'. Resetting to 0.");
                            state.layerIndex = 0;
                            EditorUtility.SetDirty(state);
                        }
                        hasIssues = true;
                    }

                    // 5. Check for null node inside state
                    if (state.node == null)
                    {
                        if (dryRun) Log($"[{ctrl.name}] [Issue] State '{state.stateName}' has no behavior node.");
                        else
                        {
                            Undo.RecordObject(state, "Create missing Node");
                            state.node = ScriptableObject.CreateInstance<HonamiAnimationNode>();
                            state.node.name = state.stateName + "_Node";
                            if (AssetDatabase.Contains(ctrl))
                            {
                                AssetDatabase.AddObjectToAsset(state.node, ctrl);
                            }
                            EditorUtility.SetDirty(state);
                            Log($"[{ctrl.name}] [Fix] Created default HonamiAnimationNode for state '{state.stateName}'.");
                        }
                        hasIssues = true;
                    }

                    // 6. Check for null or dangling transitions
                    if (state.transitions != null)
                    {
                        int nullTrans = 0;
                        foreach (var t in state.transitions) { if (t == null) nullTrans++; }

                        if (nullTrans > 0)
                        {
                            if (dryRun) Log($"[{ctrl.name}] [Issue] State '{state.stateName}' contains {nullTrans} null transitions.");
                            else
                            {
                                Undo.RecordObject(state, "Remove null transitions");
                                state.transitions.RemoveAll(t => t == null);
                                EditorUtility.SetDirty(state);
                                Log($"[{ctrl.name}] [Fix] Removed {nullTrans} null transitions from '{state.stateName}'.");
                            }
                            hasIssues = true;
                        }

                        // Dangling transitions check (targetStateGuid not in guids)
                        for (int i = state.transitions.Count - 1; i >= 0; i--)
                        {
                            var t = state.transitions[i];
                            if (t != null && !string.IsNullOrEmpty(t.targetStateGuid) && !guids.Contains(t.targetStateGuid))
                            {
                                if (dryRun) Log($"[{ctrl.name}] [Issue] State '{state.stateName}' has a dangling transition (target GUID points nowhere).");
                                else
                                {
                                    Undo.RecordObject(state, "Remove dangling transition");
                                    state.transitions.RemoveAt(i);
                                    EditorUtility.SetDirty(state);
                                    Log($"[{ctrl.name}] [Fix] Removed dangling transition from '{state.stateName}'.");
                                }
                                hasIssues = true;
                            }
                        }
                    }

                    // 7. Check for null subNodes
                    if (state.subNodes != null)
                    {
                        int nullSubNodes = 0;
                        foreach (var sn in state.subNodes) { if (sn == null) nullSubNodes++; }

                        if (nullSubNodes > 0)
                        {
                            if (dryRun) Log($"[{ctrl.name}] [Issue] State '{state.stateName}' contains {nullSubNodes} null sub-nodes.");
                            else
                            {
                                Undo.RecordObject(state, "Remove null sub-nodes");
                                state.subNodes.RemoveAll(sn => sn == null);
                                EditorUtility.SetDirty(state);
                                Log($"[{ctrl.name}] [Fix] Removed {nullSubNodes} null sub-nodes from '{state.stateName}'.");
                            }
                            hasIssues = true;
                        }
                    }
                }
            }

            // 8. Check for orphaned sub-assets left behind by deleted states
            hasIssues |= ValidateOrphanedSubAssets(ctrl, dryRun);

            // 9. Check for sub-assets whose node/sub-node script was deleted or renamed
            hasIssues |= ValidateMissingScriptSubAssets(ctrl, dryRun);

            if (!dryRun && hasIssues)
            {
                EditorUtility.SetDirty(ctrl);
            }

            if (dryRun && !hasIssues)
            {
                Log("No issues found in this controller.");
            }

            return hasIssues;
        }

        private bool ValidateOrphanedSubAssets(HonamiController ctrl, bool dryRun)
        {
            string path = AssetDatabase.GetAssetPath(ctrl);
            if (string.IsNullOrEmpty(path)) return false;

            var referenced = new HashSet<Object>();
            AddStateListReferences(ctrl.states, referenced);
            AddProjectOverrideReferences(referenced);

            bool hasIssues = false;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null || asset == ctrl) continue;
                if (asset is not (HonamiState or HonamiNodeBase or HonamiSubNodeBase)) continue;
                if (referenced.Contains(asset)) continue;

                string assetName = asset.name;
                string assetType = asset.GetType().Name;

                if (dryRun)
                {
                    Log($"[{ctrl.name}] [Issue] Orphaned sub-asset '{assetName}' ({assetType}) is not referenced by any state.");
                }
                else
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    Undo.DestroyObjectImmediate(asset);
                    Log($"[{ctrl.name}] [Fix] Removed orphaned sub-asset '{assetName}' ({assetType}).");
                }
                hasIssues = true;
            }

            return hasIssues;
        }

        private bool ValidateMissingScriptSubAssets(HonamiController ctrl, bool dryRun)
        {
            string path = AssetDatabase.GetAssetPath(ctrl);
            if (string.IsNullOrEmpty(path)) return false;

            int missingCount = 0;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null) missingCount++;
            }

            if (missingCount == 0) return false;

            if (dryRun)
            {
                Log($"[{ctrl.name}] [Issue] Found {missingCount} sub-asset(s) with missing scripts (node/sub-node class deleted or renamed).");
            }
            else
            {
                _pendingMissingScriptPaths.Add(path);
                Log($"[{ctrl.name}] [Fix] Scheduled removal of {missingCount} missing-script sub-asset(s).");
            }

            return true;
        }

        // Missing-script sub-assets survive AssetDatabase.SaveAssets, so they are stripped from the
        // saved YAML afterwards and the asset is reimported.
        private void FlushMissingScriptCleanup()
        {
            if (_pendingMissingScriptPaths.Count == 0) return;

            var cleanedPaths = new List<string>();
            foreach (string path in _pendingMissingScriptPaths)
            {
                string text = File.ReadAllText(path);
                string cleaned = HonamiMissingScriptYamlUtility.CleanControllerYaml(text, out List<string> removedNames);
                if (removedNames.Count == 0) continue;

                File.WriteAllText(path, cleaned);
                cleanedPaths.Add(path);
                Log($"[Fix] {Path.GetFileName(path)}: removed {removedNames.Count} missing-script sub-asset(s): {string.Join(", ", removedNames)}");
                Debug.Log($"[Honami Validator] Cleaned '{path}': removed missing-script sub-asset(s): {string.Join(", ", removedNames)}");
            }

            _pendingMissingScriptPaths.Clear();
            if (cleanedPaths.Count == 0) return;

            AssetDatabase.Refresh();
            foreach (string path in cleanedPaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void AddStateListReferences(List<HonamiState> states, HashSet<Object> referenced)
        {
            if (states == null) return;

            foreach (var state in states)
            {
                if (state == null) continue;

                referenced.Add(state);
                if (state.node != null) referenced.Add(state.node);

                if (state.subNodes == null) continue;
                foreach (var subNode in state.subNodes)
                {
                    if (subNode != null) referenced.Add(subNode);
                }
            }
        }

        private static void AddProjectOverrideReferences(HashSet<Object> referenced)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:HonamiOverrideController"))
            {
                var overrideController = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(AssetDatabase.GUIDToAssetPath(guid));
                if (overrideController == null) continue;

                AddStateListReferences(overrideController.additionalStates, referenced);

                if (overrideController.nodeOverrides != null)
                {
                    foreach (var nodeOverride in overrideController.nodeOverrides)
                    {
                        if (nodeOverride.overrideNode != null) referenced.Add(nodeOverride.overrideNode);
                    }
                }

                if (overrideController.overrides == null) continue;
                foreach (var entry in overrideController.overrides)
                {
                    if (entry?.effectiveState == null) continue;
                    referenced.Add(entry.effectiveState);
                    if (entry.effectiveState.node != null) referenced.Add(entry.effectiveState.node);
                    if (entry.effectiveState.subNodes != null)
                    {
                        foreach (var subNode in entry.effectiveState.subNodes)
                        {
                            if (subNode != null) referenced.Add(subNode);
                        }
                    }
                }
            }
        }
    }
}
