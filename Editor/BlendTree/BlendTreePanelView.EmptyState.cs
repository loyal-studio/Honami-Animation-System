using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.BlendTree
{
    internal sealed partial class BlendTreePanelView
    {
        private void BuildEmptyState()
        {
            Clear();
            _mainArea = null;
            _canvas = null;
            _edges = null;

            var container = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            Add(container);

            if (_state.Controller == null)
            {
                var box = HonamiEmptyStatePanel.Box(HonamiEditorIcons.BlendTree, "Honami Animation System", "No Controller Selected");
                box.Add(HonamiEmptyStatePanel.CTAButton("Select Honami Controller", ShowControllerMenu));
                box.Add(HonamiEmptyStatePanel.HintLabel("You can also drop a Honami Controller into the toolbar field above."));
                container.Add(box);
                return;
            }

            var stateBox = HonamiEmptyStatePanel.Box(HonamiEditorIcons.BlendTree, "Honami Animation System", "No Blend Tree State Selected");
            container.Add(stateBox);

            bool anyBlendState = false;
            if (_state.Controller.states != null)
            {
                var buttonScroll = new ScrollView(ScrollViewMode.Vertical);
                buttonScroll.style.maxHeight = 260;
                buttonScroll.contentContainer.style.alignItems = Align.Center;

                foreach (var st in _state.Controller.states)
                {
                    if (st == null || st.node is not HonamiBlendTreeNode) continue;
                    anyBlendState = true;
                    string guid = st.guid;
                    buttonScroll.Add(HonamiEmptyStatePanel.CTAButton(st.stateName, () =>
                    {
                        _state.StateGuid = guid;
                        _state.ResetForNewTarget();
                        _rebuild();
                    }));
                }

                if (anyBlendState) stateBox.Add(buttonScroll);
            }

            stateBox.Add(HonamiEmptyStatePanel.HintLabel(anyBlendState
                ? "Pick a Blend Tree state to edit, or double-click one in the Honami Graph window."
                : "This controller has no Blend Tree states yet. Create one in the Honami Graph window."));
        }

        private void ShowControllerMenu()
        {
            var menu = new GenericMenu();
            string[] assetGuids = AssetDatabase.FindAssets("t:HonamiController");

            if (assetGuids.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Honami Controllers in project"));
            }
            else
            {
                foreach (var assetGuid in assetGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                    var controller = AssetDatabase.LoadAssetAtPath<HonamiController>(path);
                    if (controller == null) continue;
                    menu.AddItem(new GUIContent(controller.name), false, () =>
                    {
                        _state.Controller = controller;
                        _state.StateGuid = FirstBlendStateGuid(controller);
                        _state.ResetForNewTarget();
                        _rebuild();
                    });
                }
            }

            menu.ShowAsContext();
        }

        private static string FirstBlendStateGuid(HonamiController controller)
        {
            if (controller == null || controller.states == null) return string.Empty;
            foreach (var st in controller.states)
            {
                if (st != null && st.node is HonamiBlendTreeNode)
                    return st.guid;
            }
            return string.Empty;
        }
    }
}
