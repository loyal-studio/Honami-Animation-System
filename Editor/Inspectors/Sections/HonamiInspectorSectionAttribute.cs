using System;

namespace HonamiAnimationSystem.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HonamiInspectorSectionAttribute : Attribute
    {
        public int Order { get; }

        public HonamiInspectorSectionAttribute(int order = 100) => Order = order;
    }
}
