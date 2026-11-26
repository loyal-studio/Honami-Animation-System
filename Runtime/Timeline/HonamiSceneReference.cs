using System;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Timeline
{
    [Serializable]
    public struct HonamiSceneReference : IEquatable<HonamiSceneReference>
    {
        [SerializeField] private string key;

        public HonamiSceneReference(string key) => this.key = key;

        public string Key => key;
        public bool IsAssigned => !string.IsNullOrEmpty(key);

        public static HonamiSceneReference NewKey() => new(Guid.NewGuid().ToString("N"));

        public bool Equals(HonamiSceneReference other) => key == other.key;
        public override bool Equals(object obj) => obj is HonamiSceneReference other && Equals(other);
        public override int GetHashCode() => key != null ? key.GetHashCode() : 0;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HonamiSceneReferenceTypeAttribute : Attribute
    {
        public readonly Type Type;

        public HonamiSceneReferenceTypeAttribute(Type type) => Type = type;
    }

    public interface IHonamiSceneReferenceTable
    {
        UnityEngine.Object GetReference(string key);
        void SetReference(string key, UnityEngine.Object value);
    }
}
