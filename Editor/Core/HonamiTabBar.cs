using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Reusable horizontal tab strip driven entirely by callbacks, so it can be shared by
    /// the Timeline and Blend Tree windows without owning any tab data itself.
    /// </summary>
    internal sealed class HonamiTabBar : VisualElement
    {
        private const float BarHeight = 24f;

        private readonly Func<int> _getCount;
        private readonly Func<int> _getActive;
        private readonly Action<int> _onSelect;
        private readonly Action<int> _onClose;
        private readonly Action _onAdd;
        private readonly Func<int, string> _getTitle;
        private readonly Func<int, Texture> _getIcon;
        private readonly Func<int, bool> _getShowDot;
        private readonly string _dotTooltip;
        private readonly ScrollView _strip;

        public Color ActiveTabColor { get; set; } = HonamiEditorTheme.PanelBg;

        public HonamiTabBar(
            Func<int> getCount,
            Func<int> getActive,
            Action<int> onSelect,
            Action<int> onClose,
            Action onAdd,
            Func<int, string> getTitle,
            Func<int, Texture> getIcon,
            Func<int, bool> getShowDot = null,
            string dotTooltip = null)
        {
            _getCount = getCount;
            _getActive = getActive;
            _onSelect = onSelect;
            _onClose = onClose;
            _onAdd = onAdd;
            _getTitle = getTitle;
            _getIcon = getIcon;
            _getShowDot = getShowDot;
            _dotTooltip = dotTooltip;

            name = "honami-tabbar";
            style.height = BarHeight;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.backgroundColor = HonamiEditorTheme.ToolbarBg;
            style.borderBottomWidth = 1;
            style.borderBottomColor = HonamiEditorTheme.Divider;

            _strip = new ScrollView(ScrollViewMode.Horizontal);
            _strip.style.flexGrow = 1;
            _strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _strip.contentContainer.style.flexDirection = FlexDirection.Row;
            _strip.contentContainer.style.alignItems = Align.Stretch;
            _strip.contentContainer.style.height = Length.Percent(100);
            Add(_strip);

            Refresh();
        }

        public void Refresh()
        {
            _strip.Clear();
            int count = _getCount();
            int active = _getActive();

            for (int i = 0; i < count; i++)
                _strip.Add(TabElement(i, i == active));

            _strip.Add(AddButton());
        }

        private VisualElement TabElement(int index, bool isActive)
        {
            var tab = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0,
                    height = Length.Percent(100),
                    paddingLeft = 10,
                    paddingRight = 4,
                    minWidth = 90,
                    maxWidth = 220,
                    backgroundColor = isActive ? ActiveTabColor : Color.clear,
                    borderTopWidth = 2,
                    borderTopColor = isActive ? HonamiEditorTheme.Accent : Color.clear,
                    borderRightWidth = 1,
                    borderRightColor = HonamiEditorTheme.Divider
                }
            };

            tab.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 2)
                {
                    _onClose(index);
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0) return;
                _onSelect(index);
                evt.StopPropagation();
            });

            if (!isActive)
            {
                tab.RegisterCallback<PointerEnterEvent>(_ => tab.style.backgroundColor = HonamiEditorTheme.ToolbarButtonHot);
                tab.RegisterCallback<PointerLeaveEvent>(_ => tab.style.backgroundColor = Color.clear);
            }

            var icon = new Image
            {
                image = _getIcon(index),
                pickingMode = PickingMode.Ignore,
                tintColor = isActive ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f),
                style =
                {
                    width = 14,
                    height = 14,
                    marginRight = 5,
                    flexShrink = 0
                }
            };
            tab.Add(icon);

            if (_getShowDot != null && _getShowDot(index))
            {
                tab.Add(new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        width = 7,
                        height = 7,
                        marginRight = 6,
                        flexShrink = 0,
                        backgroundColor = HonamiEditorTheme.Accent,
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4
                    },
                    tooltip = _dotTooltip
                });
            }

            var label = new Label(_getTitle(index))
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = isActive ? HonamiEditorTheme.Text : HonamiEditorTheme.MutedText,
                    fontSize = 11,
                    unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    overflow = Overflow.Hidden,
                    flexShrink = 1,
                    marginRight = 4
                }
            };
            tab.Add(label);

            tab.Add(CloseButton(index));
            return tab;
        }

        private VisualElement CloseButton(int index)
        {
            var close = new Label("×")
            {
                tooltip = "Close tab (middle click also works)",
                style =
                {
                    width = 16,
                    height = 16,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    color = HonamiEditorTheme.MutedText,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            close.RegisterCallback<PointerEnterEvent>(_ =>
            {
                close.style.color = Color.white;
                close.style.backgroundColor = new Color(0.75f, 0.22f, 0.28f, 0.9f);
            });
            close.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                close.style.color = HonamiEditorTheme.MutedText;
                close.style.backgroundColor = Color.clear;
            });
            close.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _onClose(index);
                evt.StopPropagation();
            });
            return close;
        }

        private VisualElement AddButton()
        {
            var add = new Label("+")
            {
                tooltip = "New tab",
                style =
                {
                    width = 26,
                    height = Length.Percent(100),
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    color = HonamiEditorTheme.MutedText
                }
            };
            add.RegisterCallback<PointerEnterEvent>(_ =>
            {
                add.style.color = HonamiEditorTheme.Text;
                add.style.backgroundColor = HonamiEditorTheme.ToolbarButtonHot;
            });
            add.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                add.style.color = HonamiEditorTheme.MutedText;
                add.style.backgroundColor = Color.clear;
            });
            add.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _onAdd();
                evt.StopPropagation();
            });
            return add;
        }
    }
}
