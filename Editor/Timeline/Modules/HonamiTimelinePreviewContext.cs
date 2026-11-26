using System;
using System.Globalization;
using HonamiAnimationSystem.Runtime.Timeline;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public sealed class HonamiTimelinePreviewContext
    {
        private static readonly string[] PositionPaths = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        private static readonly string[] RotationPaths = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        private static readonly string[] ScalePaths = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

        internal TimelineState State;

        public IHonamiTimelineModuleContainer Container { get; internal set; }
        public HonamiTimelineModule Module { get; internal set; }
        public HonamiDirector Director { get; internal set; }
        public Object Binding { get; internal set; }
        public GameObject PreviewRoot { get; internal set; }
        public float Time { get; internal set; }
        public float TimeOffset { get; internal set; }
        public bool IsPlaying { get; internal set; }
        public bool IsScrubbing { get; internal set; }
        public float PlaybackSpeed { get; internal set; } = 1f;
        public float FrameRate { get; internal set; }

        public HonamiTimelineModuleHost Host => Container.Host;

        public void SampleClip(GameObject target, AnimationClip clip, float clipTime) =>
            TimelinePreview.SafeSample(State, target, clip, clipTime);

        public GameObject ResolveSceneInstance(GameObject target) => TimelinePreview.ResolvePrefabInstance(target);

        public Object Resolve(HonamiSceneReference reference, Type type) =>
            HonamiTimelineEditContext.ResolveReference(Container, Director, PreviewRoot, reference, type);

        public T Resolve<T>(HonamiSceneReference reference) where T : Object => Resolve(reference, typeof(T)) as T;

        // Must run on every sample: animation mode reverts any driven property a sampling pass did not register again.
        public static void DriveProperty(Object target, string propertyPath)
        {
            if (target == null || !AnimationMode.InAnimationMode()) return;

            using var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyPath);
            if (property == null) return;

            string value = property.propertyType switch
            {
                SerializedPropertyType.Float => property.floatValue.ToString("R", CultureInfo.InvariantCulture),
                SerializedPropertyType.Boolean => property.boolValue ? "1" : "0",
                SerializedPropertyType.Integer or SerializedPropertyType.Enum => property.intValue.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
            if (value == null) return;

            var modification = new PropertyModification { target = target, propertyPath = propertyPath, value = value };
            var binding = EditorCurveBinding.FloatCurve(string.Empty, target.GetType(), propertyPath);
            AnimationMode.AddPropertyModification(binding, modification, true);
        }

        public static void DriveTransform(Transform transform, bool scale = false)
        {
            if (transform == null) return;
            for (int i = 0; i < PositionPaths.Length; i++) DriveProperty(transform, PositionPaths[i]);
            for (int i = 0; i < RotationPaths.Length; i++) DriveProperty(transform, RotationPaths[i]);
            if (!scale) return;
            for (int i = 0; i < ScalePaths.Length; i++) DriveProperty(transform, ScalePaths[i]);
        }

        public static void DriveActive(GameObject gameObject) => DriveProperty(gameObject, "m_IsActive");
    }
}
