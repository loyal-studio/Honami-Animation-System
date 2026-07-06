using System;
using System.Collections.Generic;
using UnityEngine;

namespace HonamiAnimationSystem.Runtime.Riggings
{
    [AddComponentMenu("Honami Animation/Honami Bone Replacer")]
    public sealed class HonamiBoneReplacer : MonoBehaviour
    {
        [Serializable]
        public struct BoneReplacement
        {
            public string boneName;
            public Transform targetTransform;
        }

        public bool disableUnreplacedBones = true;
        public List<BoneReplacement> replacements = new();

        public Transform GetReplacement(string boneName)
        {
            for (int i = 0; i < replacements.Count; i++)
            {
                if (string.Equals(replacements[i].boneName, boneName, StringComparison.OrdinalIgnoreCase))
                    return replacements[i].targetTransform;
            }
            return null;
        }

        public void AddReplacement(string name, Transform target)
        {
            for (int i = 0; i < replacements.Count; i++)
            {
                if (replacements[i].boneName == name)
                {
                    var r = replacements[i];
                    r.targetTransform = target;
                    replacements[i] = r;
                    return;
                }
            }
            replacements.Add(new BoneReplacement { boneName = name, targetTransform = target });
        }

        public void RemoveReplacement(string name)
        {
            for (int i = 0; i < replacements.Count; i++)
            {
                if (replacements[i].boneName == name)
                {
                    replacements.RemoveAt(i);
                    return;
                }
            }
        }

        public void ClearGarbage()
        {
            for (int i = replacements.Count - 1; i >= 0; i--)
            {
                if (replacements[i].targetTransform == null)
                {
                    replacements.RemoveAt(i);
                }
            }
        }
    }
}

