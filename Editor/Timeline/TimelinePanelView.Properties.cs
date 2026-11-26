using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiEditorController = HonamiAnimationSystem.Editor.Core.HonamiEditorController;

namespace HonamiAnimationSystem.Editor.Timeline
{
    internal sealed partial class TimelinePanelView
    {
        private SerializedObject _modulesObject;
        private readonly Dictionary<string, bool> _bindingFoldouts = new();

        private SerializedObject ModulesObject()
        {
            var container = _state.ActiveContainer as UnityEngine.Object;
            if (container == null) return null;
            if (_modulesObject == null || _modulesObject.targetObject != container)
                _modulesObject = new SerializedObject(container);
            _modulesObject.Update();
            return _modulesObject;
        }

        private bool ShowsModuleProperties =>
            _state.ActiveContainer != null
            && (_state.Mode == TimelineMode.HonamiTimeline || _state.SelectedModule != null || _state.SelectedTimelineItems.Count > 0);

        private object BoundPropertiesKey()
        {
            if (!ShowsModuleProperties) return null;

            var container = _state.ActiveContainer;
            var modules = container.Modules;

            if (_state.SelectedMarkers.Count == 1 && _state.ActiveTimeline != null && _state.SelectedTimelineItems.Count == 0)
            {
                var marker = _state.SelectedMarkers[0];
                return (container, (object)marker, _state.ActiveTimeline.Markers.IndexOf(marker), -2, (object)null, (object)null);
            }

            if (_state.SelectedTimelineItems.Count == 1)
            {
                var item = _state.SelectedTimelineItems[0];
                var owner = _state.FindModuleOf(item);
                if (owner == null) return null;
                return (container, (object)item, modules.IndexOf(owner), owner.IndexOf(item), (object)null, (object)null);
            }

            if (_state.SelectedTimelineItems.Count > 1)
                return (container, (object)"multi-items", _state.SelectedTimelineItems.Count, -3, (object)null, (object)null);

            var module = _state.SelectedModule;
            if (module != null)
            {
                object binding = container.Host == HonamiTimelineModuleHost.Timeline
                    ? (object)(_state.ResolveDirector() is { } director ? director.GetBinding(module) : null)
                    : (module.bindingKey, HonamiTimelineEditContext.ResolveBinding(container, module, null, _state.ResolvePreviewRoot()));
                object bindingSource = container.Host == HonamiTimelineModuleHost.Timeline ? _state.ResolveDirector() : _state.ResolvePreviewRoot();
                return (container, (object)module, modules.IndexOf(module), -1, bindingSource, (binding, module.parentId));
            }

            return (container, (object)null, -1, -1, (object)null, (object)null);
        }

        private void ReleaseStaleProperties()
        {
            if (_properties == null || _propertiesKey == null) return;
            if (_propertiesKey.Equals(BoundPropertiesKey())) return;
            _properties.Clear();
            _propertiesKey = null;
        }

        private void BuildProperties()
        {
            _properties.Clear();
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.Padding(12);
            _properties.Add(scroll);

            if (ShowsModuleProperties)
            {
                BuildModuleModeProperties(scroll);
                return;
            }

            if (_state.SelectedSeqClips.Count == 1)
                BuildSeqClipProperties(scroll, _state.SelectedSeqClips[0]);
            else if (_state.Mode == TimelineMode.HonamiState && _state.SelectedState != null)
                BuildStateProperties(scroll, _state.SelectedState);
        }

        private void BuildModuleModeProperties(VisualElement root)
        {
            var container = _state.ActiveContainer;
            var modulesObject = ModulesObject();
            if (modulesObject == null) return;

            if (_state.SelectedMarkers.Count == 1 && _state.ActiveTimeline != null && _state.SelectedTimelineItems.Count == 0)
            {
                BuildMarkerProperties(root, modulesObject, _state.SelectedMarkers[0]);
                return;
            }

            if (_state.SelectedTimelineItems.Count == 1)
            {
                BuildItemProperties(root, modulesObject, _state.SelectedTimelineItems[0]);
                return;
            }

            if (_state.SelectedTimelineItems.Count > 1)
            {
                root.Add(Title($"{_state.SelectedTimelineItems.Count} Items Selected"));
                root.Add(new HelpBox("Drag any of them to move them together. S splits them at the playhead, Ctrl+D duplicates, Shift+Delete ripple-deletes.", HelpBoxMessageType.None));
                AddDangerButton(root, "Remove Items", () =>
                {
                    var items = new List<HonamiTimelineItem>(_state.SelectedTimelineItems);
                    ModifyModules(target => HonamiEditorController.DeleteTimelineItems(target, items));
                    _state.SelectedTimelineItems.Clear();
                    _rebuild();
                });
                return;
            }

            if (_state.SelectedModule != null)
            {
                BuildModuleProperties(root, modulesObject, _state.SelectedModule);
                return;
            }

            // Timeline-level properties: track modulesObject changes with a dedicated child tracker
            // so the scroll root is never bound to more than one SerializedObject.
            var timelineTracker = new VisualElement { pickingMode = PickingMode.Ignore, style = { display = DisplayStyle.None } };
            root.Add(timelineTracker);
            timelineTracker.TrackSerializedObjectValue(modulesObject, _ =>
            {
                if (IsInteractingWithCanvas()) return;
                container.MarkModulesChanged();
                _state.PostModifyState();
                _rebuild();
            });

            root.Add(Title("Timeline"));
            root.Add(BoundField(modulesObject.FindProperty("timelineName")));
            root.Add(BoundField(modulesObject.FindProperty("frameRate")));
            root.Add(BoundField(modulesObject.FindProperty("durationOverride")));
            if (_state.ActiveTimeline != null)
                BuildDirectorSummary(root, _state.ActiveTimeline);
        }

        private void BuildDirectorSummary(VisualElement root, HonamiTimeline timeline)
        {
            root.Add(new VisualElement { style = { height = 1, backgroundColor = TimelineTheme.SubtleLine, marginTop = 10, marginBottom = 8 } });
            root.Add(Title("Director"));
            var director = _state.ResolveDirector();
            if (director == null)
            {
                root.Add(new HelpBox("No Honami Director in the open scenes plays this timeline. The director stores scene bindings and camera shot anchors, and plays the timeline at runtime.", HelpBoxMessageType.Info));
                root.Add(new Button(() =>
                {
                    var created = HonamiTimelineEditContext.CreateDirector(timeline);
                    if (created != null) Selection.activeGameObject = created.gameObject;
                    _state.ForgetDirector();
                    _propertiesKey = null;
                    _rebuild();
                }) { text = "Create Director In Scene" });
                return;
            }

            var field = new ObjectField("Director") { objectType = typeof(HonamiDirector), value = director, allowSceneObjects = true };
            field.SetEnabled(false);
            root.Add(field);

            var serialized = new SerializedObject(director);
            root.Add(BoundField(serialized.FindProperty("playTrigger")));
            root.Add(BoundField(serialized.FindProperty("pauseAtFirstFrameOnAutoPlay"), "Pause At First Frame"));
            root.Add(BoundField(serialized.FindProperty("wrapMode")));
            root.Add(BoundField(serialized.FindProperty("updateMode")));
            root.Add(BoundField(serialized.FindProperty("speed")));
        }

        private void BuildMarkerProperties(VisualElement root, SerializedObject timelineObject, HonamiTimelineMarker marker)
        {
            var timeline = _state.ActiveTimeline;
            int index = timeline != null ? timeline.Markers.IndexOf(marker) : -1;
            if (index < 0) return;

            var markersProp = timelineObject?.FindProperty("markers");
            if (markersProp == null || index >= markersProp.arraySize) return;

            // Own the tracker for timelineObject on a child element.
            var tracker = new VisualElement { pickingMode = PickingMode.Ignore, style = { display = DisplayStyle.None } };
            root.Add(tracker);
            tracker.TrackSerializedObjectValue(timelineObject, _ =>
            {
                if (IsInteractingWithCanvas()) return;
                _state.PostModifyState();
                _rebuild();
            });

            var markerProperty = markersProp.GetArrayElementAtIndex(index);
            root.Add(Title(marker is HonamiTimelineLoopMarker ? "Loop Region" : marker is HonamiTimelineWaitMarker ? "Wait" : "Marker"));

            var iterator = markerProperty.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.name == "signal" && marker is HonamiTimelineLoopMarker loopMarker && loopMarker.mode != HonamiTimelineLoopMode.RuntimeLoopUntilSignal)
                    continue;

                string label = iterator.name == "mode" ? "Playback"
                    : iterator.name == "signal" ? marker is HonamiTimelineWaitMarker ? "Continue key" : "Release key"
                    : null;
                root.Add(BoundField(iterator.Copy(), label));
            }

            if (marker is HonamiTimelineCueMarker)
                root.Add(new HelpBox($"Jump here at runtime with director.SeekToMarker(\"{marker.id}\").", HelpBoxMessageType.None));
            else if (marker is HonamiTimelineWaitMarker wait)
                root.Add(new HelpBox($"Pauses the Director here. Continue with director.Continue(\"{wait.signal}\").", HelpBoxMessageType.None));
            else if (marker is HonamiTimelineLoopMarker loop && loop.mode == HonamiTimelineLoopMode.RuntimeLoop)
                root.Add(new HelpBox("Repeats this region forever while the Director is active.", HelpBoxMessageType.None));
            else if (marker is HonamiTimelineLoopMarker heldLoop && heldLoop.mode == HonamiTimelineLoopMode.RuntimeLoopUntilSignal)
                root.Add(new HelpBox($"Repeats this region until director.Continue(\"{heldLoop.signal}\").", HelpBoxMessageType.None));

            AddDangerButton(root, "Remove Marker", () =>
            {
                HonamiEditorController.DeleteMarkers(timeline, new List<HonamiTimelineMarker> { marker });
                _state.SelectedMarkers.Clear();
                _rebuild();
            });
        }

        private void BuildModuleProperties(VisualElement root, SerializedObject modulesObject, HonamiTimelineModule module)
        {
            var container = _state.ActiveContainer;
            if (container == null || module == null) return;
            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);

            var moduleSo = new SerializedObject(module);
            var context = CreateEditContext(module);

            root.Add(Title(editor.DisplayName));
            if (!module.SupportsHost(container.Host))
                root.Add(new HelpBox($"This module does not run in a {(container.Host == HonamiTimelineModuleHost.State ? "state" : "timeline")}.", HelpBoxMessageType.Warning));

            var nameProp = moduleSo.FindProperty(nameof(HonamiTimelineModule.moduleName));
            if (nameProp != null) root.Add(BoundField(nameProp));

            var mutedProp = moduleSo.FindProperty(nameof(HonamiTimelineModule.muted));
            if (mutedProp != null) root.Add(BoundField(mutedProp));

            BuildParentField(root, module);

            if (module.BindingType != null)
                root.Add(BuildBindingSection(moduleSo, context, module));

            // `custom` owns the moduleSo tracker - it is a fresh container so it
            // tracks exactly one SerializedObject, satisfying UIElements' constraint.
            var custom = new VisualElement();
            custom.TrackSerializedObjectValue(moduleSo, _ =>
            {
                if (IsInteractingWithCanvas()) return;
                container.MarkModulesChanged();
                _state.PostModifyState();
                _rebuild();
            });
            root.Add(custom);
            editor.BuildModuleInspector(context, moduleSo, custom);

            AddDangerButton(root, "Remove Module", () =>
            {
                ModifyModules(target => HonamiEditorController.DeleteModule(target, module));
                _state.SelectedModule = null;
                _rebuild();
            });
        }

        private void BuildParentField(VisualElement root, HonamiTimelineModule module)
        {
            var choices = CollectParentChoices(module);
            bool missing = _state.ResolveParent(module).IsMissing;
            if (choices.Count <= 1 && !module.HasParent) return;

            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                labels.Add(labels.Contains(choices[i].Label) ? $"{choices[i].Label} ({i})" : choices[i].Label);
                if (!missing && IsCurrentParent(module, choices[i])) selected = i;
            }
            if (missing)
            {
                labels.Add("Missing parent");
                selected = labels.Count - 1;
            }

            var field = new DropdownField("Parent", labels, selected)
            {
                tooltip = "What this module runs inside. A clip or an item: the module runs only while it plays, and its items are timed from its start."
            };
            field.RegisterValueChangedCallback(evt =>
            {
                int choice = labels.IndexOf(evt.newValue);
                if (choice >= 0 && choice < choices.Count) SetModuleParent(module, choices[choice]);
            });
            root.Add(field);

            if (missing)
                root.Add(new HelpBox("The clip or item this module was nested in is gone, so the module never runs. Pick another parent.", HelpBoxMessageType.Warning));
        }

        // Collapsed while the default target is used; opens by itself once a key is set or the binding has a problem.
        private Foldout BuildBindingSection(SerializedObject moduleSo, HonamiTimelineEditContext context, HonamiTimelineModule module)
        {
            bool timelineHost = context.Host == HonamiTimelineModuleHost.Timeline;
            var owner = timelineHost ? (context.Director != null ? context.Director.gameObject : null) : context.PreviewRoot;
            var binding = timelineHost ? (context.Director != null ? context.Director.GetBinding(module) : null) : context.Binding;
            string problem = owner != null ? module.GetBindingProblem(context.Host, binding, owner) : null;

            string summary;
            if (timelineHost)
                summary = context.Director == null ? "no Director" : binding != null ? binding.name : "not bound";
            else
                summary = string.IsNullOrEmpty(module.bindingKey) ? "animator's own GameObject" : $"key '{module.bindingKey}'";

            bool needsAttention = !string.IsNullOrEmpty(problem)
                || (timelineHost ? context.Director == null : !string.IsNullOrEmpty(module.bindingKey));
            var foldout = new Foldout
            {
                text = $"Binding: {summary}",
                value = _bindingFoldouts.TryGetValue(module.Id, out bool open) ? open : needsAttention
            };
            foldout.style.marginTop = 4;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) _bindingFoldouts[module.Id] = evt.newValue;
            });

            if (timelineHost)
                BuildDirectorBindingField(foldout, context, module);
            else
                BuildStateBindingField(foldout, moduleSo, context, module);
            return foldout;
        }

        private void BuildDirectorBindingField(VisualElement root, HonamiTimelineEditContext context, HonamiTimelineModule module)
        {
            var director = context.Director;
            if (director == null)
            {
                root.Add(new HelpBox(
                    "No Honami Director in the open scenes plays this timeline. Add one and assign this timeline to bind scene objects to modules.",
                    HelpBoxMessageType.Info));
                return;
            }

            var field = new ObjectField("Target")
            {
                objectType = module.BindingType,
                allowSceneObjects = true,
                value = director.GetBinding(module),
                tooltip = $"Stored on '{director.name}'"
            };
            field.RegisterValueChangedCallback(evt =>
            {
                HonamiEditorController.SetTimelineBinding(director, module, evt.newValue);
                _state.ClearCache();
                _rebuild();
            });
            root.Add(field);

            string problem = module.GetBindingProblem(HonamiTimelineModuleHost.Timeline, director.GetBinding(module), director.gameObject);
            if (!string.IsNullOrEmpty(problem))
                root.Add(new HelpBox(char.ToUpperInvariant(problem[0]) + problem.Substring(1) + ".", HelpBoxMessageType.Warning));
        }

        private void BuildStateBindingField(VisualElement root, SerializedObject moduleSo, HonamiTimelineEditContext context, HonamiTimelineModule module)
        {
            var previewRoot = context.PreviewRoot;

            var keyRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var keyField = new TextField("Binding Key")
            {
                isDelayed = true,
                tooltip = "Name of the binding on the animator's Honami Timeline Binding Receiver (or any other binding provider). Empty = the animator's own GameObject.",
                style = { flexGrow = 1, flexShrink = 1 }
            };
            var bindingKeyProp = moduleSo.FindProperty(nameof(HonamiTimelineModule.bindingKey));
            if (bindingKeyProp != null) keyField.BindProperty(bindingKeyProp);
            keyRow.Add(keyField);

            var keysButton = new Button(() => ShowBindingKeyMenu(module, previewRoot)) { text = "▾", tooltip = "Keys available on the selected character" };
            keysButton.style.width = 20;
            keysButton.SetEnabled(previewRoot != null);
            keyRow.Add(keysButton);
            root.Add(keyRow);

            if (previewRoot == null)
            {
                root.Add(new HelpBox("Select the character in the Hierarchy to see its binding keys and assign targets.", HelpBoxMessageType.None));
                return;
            }

            if (!string.IsNullOrEmpty(module.bindingKey))
            {
                if (previewRoot.TryGetComponent(out HonamiTimelineBindingReceiver receiver))
                {
                    var targetField = new ObjectField($"Target on '{previewRoot.name}'")
                    {
                        objectType = module.BindingType,
                        allowSceneObjects = true,
                        value = context.Binding,
                        tooltip = $"Stored in the Honami Timeline Binding Receiver on '{previewRoot.name}' under '{module.bindingKey}'"
                    };
                    targetField.RegisterValueChangedCallback(evt =>
                    {
                        Undo.RecordObject(receiver, "Set Timeline Binding");
                        receiver.SetBinding(module.bindingKey, evt.newValue);
                        EditorUtility.SetDirty(receiver);
                        _rebuild();
                    });
                    root.Add(targetField);
                }
                else
                {
                    var addReceiver = new Button(() =>
                    {
                        Undo.AddComponent<HonamiTimelineBindingReceiver>(previewRoot);
                        _rebuild();
                    })
                    { text = $"Add Timeline Binding Receiver to '{previewRoot.name}'" };
                    root.Add(addReceiver);
                }
            }

            string problem = module.GetBindingProblem(HonamiTimelineModuleHost.State, context.Binding, previewRoot);
            if (!string.IsNullOrEmpty(problem))
                root.Add(new HelpBox($"On '{previewRoot.name}': {problem}.", HelpBoxMessageType.Warning));
        }

        private void ShowBindingKeyMenu(HonamiTimelineModule module, GameObject previewRoot)
        {
            var keys = new List<string>();
            HonamiTimelineModuleBinding.CollectKeys(previewRoot, keys);

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(Animator's own GameObject)"), string.IsNullOrEmpty(module.bindingKey), () => SetBindingKey(module, string.Empty));
            if (keys.Count > 0) menu.AddSeparator("");
            foreach (var key in keys)
            {
                string captured = key;
                menu.AddItem(new GUIContent(key), module.bindingKey == key, () => SetBindingKey(module, captured));
            }
            if (keys.Count == 0)
                menu.AddDisabledItem(new GUIContent($"No binding keys on '{previewRoot.name}'"));
            menu.ShowAsContext();
        }

        private void SetBindingKey(HonamiTimelineModule module, string key)
        {
            ModifyModules(target => HonamiEditorController.SetModuleBindingKey(target, module, key));
            _rebuild();
        }

        private void BuildItemProperties(VisualElement root, SerializedObject modulesObject, HonamiTimelineItem item)
        {
            var container = _state.ActiveContainer;
            if (container == null || item == null) return;
            var module = _state.FindModuleOf(item);
            if (module == null) return;

            var editor = HonamiTimelineModuleEditorRegistry.GetEditor(module);
            root.Add(Title(editor.ItemDisplayName));

            int moduleIndex = container.Modules.IndexOf(module);
            int itemIndex = module.IndexOf(item);

            // `custom` owns the moduleSo tracker - fresh container tracks exactly one SO.
            var custom = new VisualElement();
            root.Add(custom);

            if (moduleIndex < 0 || itemIndex < 0)
            {
                custom.Add(new HelpBox($"Item is no longer part of '{editor.DisplayName}'.", HelpBoxMessageType.Warning));
                return;
            }

            var moduleSo = new SerializedObject(module);
            custom.TrackSerializedObjectValue(moduleSo, _ =>
            {
                if (IsInteractingWithCanvas()) return;
                container.MarkModulesChanged();
                _state.PostModifyState();
                _rebuild();
            });

            SerializedProperty itemProperty = null;
            var itemsProp = moduleSo.FindProperty("items");
            if (itemsProp != null && itemIndex < itemsProp.arraySize)
                itemProperty = itemsProp.GetArrayElementAtIndex(itemIndex);

            if (itemProperty == null)
            {
                custom.Add(new HelpBox($"'{module.GetType().Name}' serialized item properties are unavailable.", HelpBoxMessageType.Warning));
            }
            else
            {
                editor.BuildItemInspector(CreateEditContext(module), itemProperty, custom);
            }

            AddDangerButton(root, $"Remove {editor.ItemDisplayName}", () =>
            {
                ModifyModules(target => HonamiEditorController.DeleteTimelineItems(target, new List<HonamiTimelineItem> { item }));
                _state.SelectedTimelineItems.Clear();
                _rebuild();
            });
        }

        private static PropertyField BoundField(SerializedProperty property, string label = null)
        {
            var field = new PropertyField(property, label);
            field.Bind(property.serializedObject);
            return field;
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
            AddToggle(root, "Freeze Last Frame", targetState.holdLastFrame, v =>
            {
                _state.PreModifyState();
                HonamiEditorController.EditStateHoldLastFrame(_state.SelectedState, v);
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
            AddFloat(root, $"Start Time (0-{clipLen:F2}s)", animNode.startTime, v =>
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
