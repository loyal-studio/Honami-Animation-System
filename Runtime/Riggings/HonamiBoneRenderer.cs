using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [AddComponentMenu("Honami Animation/Honami Bone Renderer")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class HonamiBoneRenderer : MonoBehaviour
    {
        public enum BoneShape { Line, Pyramid, Octahedron }

        [SerializeField] private BoneShape shape = BoneShape.Octahedron;
        [SerializeField, Range(0.1f, 3f)] private float boneSize = 1f;
        [SerializeField] private bool drawJoints = true;
        [SerializeField, Range(0.1f, 3f)] private float jointSize = 1f;
        [SerializeField] private bool xRay = true;
        [SerializeField] private Color boneColor = new Color(0.18f, 0.76f, 0.9f, 0.6f);
        [SerializeField] private Color hoverColor = new Color(0.55f, 0.92f, 1f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f, 0.95f);
        [SerializeField] private List<Transform> bones = new();

        public BoneShape Shape => shape;
        public float BoneSize => boneSize;
        public bool DrawJoints => drawJoints;
        public float JointSize => jointSize;
        public bool XRay => xRay;
        public Color BoneColor => boneColor;
        public Color HoverColor => hoverColor;
        public Color SelectedColor => selectedColor;
        public IReadOnlyList<Transform> Bones => bones;

#if UNITY_EDITOR
        private static readonly List<HonamiBoneRenderer> Instances = new();

        public static IReadOnlyList<HonamiBoneRenderer> EditorInstances => Instances;

        public int EditorVersion { get; private set; }

        public void EditorSetBones(IReadOnlyList<Transform> newBones)
        {
            bones.Clear();
            for (int i = 0; i < newBones.Count; i++)
            {
                bones.Add(newBones[i]);
            }
            EditorVersion++;
        }

        public List<Transform> EditorCollectSkinnedMeshBones()
        {
            var result = new List<Transform>();
            var seen = new HashSet<Transform>();
            foreach (var skinnedMesh in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in skinnedMesh.bones)
                {
                    if (bone != null && seen.Add(bone)) result.Add(bone);
                }
            }
            return result;
        }

        private void OnEnable()
        {
            Instances.Add(this);
            UnityEditor.SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            UnityEditor.SceneView.RepaintAll();
        }

        private void OnValidate() => EditorVersion++;

        private void Reset() => EditorSetBones(EditorCollectSkinnedMeshBones());
#endif
    }
}
