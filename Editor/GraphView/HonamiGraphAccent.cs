using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    internal static class HonamiGraphAccent
    {
        private sealed class SubNodeBadgeState
        {
            public bool Hover;
            public bool Pressed;
        }

        private static Color Accent => HonamiGraphStyles.Accent;

        private static Color AccentWithAlpha(float alpha)
        {
            var color = Accent;
            color.a = alpha;
            return color;
        }

        public static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = element.style.borderBottomColor =
            element.style.borderLeftColor = element.style.borderRightColor = color;
        }

        public static void ClearBorderColor(VisualElement element)
        {
            element.style.borderTopColor = element.style.borderBottomColor =
            element.style.borderLeftColor = element.style.borderRightColor = StyleKeyword.Null;
        }

        public static void SetSelectionBorder(VisualElement element, bool selected)
        {
            if (selected) SetBorderColor(element, Accent);
            else ClearBorderColor(element);
        }

        public static void SetGroupSelected(VisualElement group, bool selected)
        {
            if (selected)
            {
                SetBorderColor(group, Accent);
                group.style.backgroundColor = AccentWithAlpha(0.06f);
            }
            else
            {
                ClearBorderColor(group);
                group.style.backgroundColor = StyleKeyword.Null;
            }
        }

        public static void RefreshGraphView(GraphView graphView)
        {
            var miniMap = graphView.Q<MiniMap>();
            if (miniMap != null) ApplyMinimapViewport(miniMap);

            graphView.graphElements.ForEach(element =>
            {
                switch (element)
                {
                    case HonamiNode node: node.RefreshAccent(); break;
                    case HonamiGroup group: SetGroupSelected(group, group.selected); break;
                    case HonamiTransitionEdge edge: edge.RefreshAccent(); break;
                }
                element.MarkDirtyRepaint();
            });
        }

        public static void ApplyMinimapViewport(VisualElement miniMap)
        {
            var viewport = miniMap.Q("viewport");
            if (viewport == null) return;
            viewport.style.backgroundColor = AccentWithAlpha(0.16f);
            SetBorderColor(viewport, Accent);
        }

        public static void AttachSubNodeBadge(Label badge)
        {
            var state = new SubNodeBadgeState();
            badge.userData = state;
            badge.RegisterCallback<PointerOverEvent>(_ => { state.Hover = true; StyleSubNodeBadge(badge, state); });
            badge.RegisterCallback<PointerOutEvent>(_ => { state.Hover = false; state.Pressed = false; StyleSubNodeBadge(badge, state); });
            badge.RegisterCallback<PointerDownEvent>(_ => { state.Pressed = true; StyleSubNodeBadge(badge, state); });
            badge.RegisterCallback<PointerUpEvent>(_ => { state.Pressed = false; StyleSubNodeBadge(badge, state); });
            StyleSubNodeBadge(badge, state);
        }

        public static void RefreshSubNodeBadge(Label badge)
        {
            if (badge.userData is SubNodeBadgeState state) StyleSubNodeBadge(badge, state);
        }

        private static void StyleSubNodeBadge(Label badge, SubNodeBadgeState state)
        {
            bool selected = badge.ClassListContains("honami-node-subnode-badge-selected");

            if (state.Pressed)
            {
                badge.style.backgroundColor = AccentWithAlpha(0.40f);
                SetBorderColor(badge, Accent);
                badge.style.color = Color.white;
            }
            else if (selected)
            {
                badge.style.backgroundColor = Accent;
                SetBorderColor(badge, Accent);
                badge.style.color = Color.white;
            }
            else if (state.Hover)
            {
                badge.style.backgroundColor = AccentWithAlpha(0.20f);
                SetBorderColor(badge, AccentWithAlpha(0.62f));
                badge.style.color = Color.white;
            }
            else
            {
                badge.style.backgroundColor = AccentWithAlpha(0.10f);
                SetBorderColor(badge, AccentWithAlpha(0.42f));
                badge.style.color = Accent;
            }
        }
    }
}
