using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiStyleExtensions
    {
        public static T BorderWidth<T>(this T element, float width) where T : VisualElement
        {
            IStyle style = element.style;
            style.borderTopWidth = style.borderRightWidth = style.borderBottomWidth = style.borderLeftWidth = width;
            return element;
        }

       public static T BorderColor<T>(this T element, StyleColor color) where T : VisualElement
        {
            IStyle style = element.style;
            style.borderTopColor = style.borderRightColor = style.borderBottomColor = style.borderLeftColor = color;
            return element;
        }

        public static T Border<T>(this T element, float width, StyleColor color) where T : VisualElement
            => element.BorderWidth(width).BorderColor(color);

        public static T Radius<T>(this T element, float radius) where T : VisualElement
        {
            IStyle style = element.style;
            style.borderTopLeftRadius = style.borderTopRightRadius =
                style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
            return element;
        }

        public static T Padding<T>(this T element, float all) where T : VisualElement
            => element.Padding(all, all);

        public static T Padding<T>(this T element, float vertical, float horizontal) where T : VisualElement
        {
            IStyle style = element.style;
            style.paddingTop = style.paddingBottom = vertical;
            style.paddingLeft = style.paddingRight = horizontal;
            return element;
        }

        public static T Margin<T>(this T element, float all) where T : VisualElement
            => element.Margin(all, all);

        public static T Margin<T>(this T element, float vertical, float horizontal) where T : VisualElement
        {
            IStyle style = element.style;
            style.marginTop = style.marginBottom = vertical;
            style.marginLeft = style.marginRight = horizontal;
            return element;
        }

        public static T Size<T>(this T element, float width, float height) where T : VisualElement
        {
            element.style.width = width;
            element.style.height = height;
            return element;
        }
    }
}
