using System;
using System.Collections.Generic;
using System.IO;
using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public struct HonamiAvatarBuildOptions
    {
        public bool skinnedBonesOnly;
        public bool includeInactive;
        public bool autoBindMirrors;

        public static HonamiAvatarBuildOptions Default => new()
        {
            skinnedBonesOnly = false,
            includeInactive = true,
            autoBindMirrors = true
        };

        public static HonamiAvatarBuildOptions From(HonamiAvatar.ModelSource source)
        {
            if (source == null) return Default;

            return new HonamiAvatarBuildOptions
            {
                skinnedBonesOnly = source.skinnedBonesOnly,
                includeInactive = source.includeInactive,
                autoBindMirrors = source.autoBindMirrors
            };
        }
    }

    public struct HonamiAvatarBuildResult
    {
        public int addedBones;
        public int removedBones;
        public int totalBones;
        public int addedMirrorPairs;
    }

    public static class HonamiAvatarBuilder
    {
        public const string AvatarAssetSuffix = "_HonamiAvatar";

        private static readonly List<Transform> BoneBuffer = new();
        private static Dictionary<string, string> _modelGuidToAvatarPath;

        public static string GetRelativePath(Transform root, Transform bone)
        {
            if (bone == null || bone == root) return string.Empty;

            string path = bone.name;
            Transform parent = bone.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        public static void CollectBoneTransforms(GameObject modelRoot, HonamiAvatarBuildOptions options, List<Transform> results)
        {
            results.Clear();
            if (modelRoot == null) return;

            Transform root = modelRoot.transform;
            var all = modelRoot.GetComponentsInChildren<Transform>(true);

            if (options.skinnedBonesOnly)
            {
                var skinned = new HashSet<Transform>();
                foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    AddChain(renderer.rootBone, root, skinned);
                    var bones = renderer.bones;
                    if (bones == null) continue;

                    foreach (var bone in bones) AddChain(bone, root, skinned);
                }

                if (skinned.Count > 0)
                {
                    foreach (var transform in all)
                    {
                        if (transform != root && skinned.Contains(transform)) results.Add(transform);
                    }

                    return;
                }
            }

            foreach (var transform in all)
            {
                if (transform == root) continue;
                if (!options.includeInactive && !transform.gameObject.activeInHierarchy) continue;
                results.Add(transform);
            }
        }

        public static int AddBones(HonamiAvatar avatar, GameObject modelRoot, HonamiAvatarBuildOptions options)
        {
            if (avatar == null || modelRoot == null) return 0;

            CollectBoneTransforms(modelRoot, options, BoneBuffer);
            Transform root = modelRoot.transform;

            var known = new HashSet<string>();
            for (int i = 0; i < avatar.bones.Count; i++)
            {
                if (avatar.bones[i] != null) known.Add(avatar.bones[i].bonePath);
            }

            int added = 0;
            foreach (var bone in BoneBuffer)
            {
                string path = GetRelativePath(root, bone);
                if (string.IsNullOrEmpty(path) || !known.Add(path)) continue;

                avatar.bones.Add(new HonamiAvatar.BoneEntry
                {
                    boneName = bone.name,
                    bonePath = path,
                    enabled = true
                });
                added++;
            }

            return added;
        }

        public static HonamiAvatarBuildResult Rebuild(HonamiAvatar avatar, GameObject modelRoot, HonamiAvatarBuildOptions options)
        {
            var result = default(HonamiAvatarBuildResult);
            if (avatar == null || modelRoot == null) return result;

            CollectBoneTransforms(modelRoot, options, BoneBuffer);
            Transform root = modelRoot.transform;

            var previous = new Dictionary<string, HonamiAvatar.BoneEntry>(avatar.bones.Count);
            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var entry = avatar.bones[i];
                if (entry == null || string.IsNullOrEmpty(entry.bonePath)) continue;
                previous[entry.bonePath] = entry;
            }

            var rebuilt = new List<HonamiAvatar.BoneEntry>(BoneBuffer.Count);
            var livePaths = new HashSet<string>();
            foreach (var bone in BoneBuffer)
            {
                string path = GetRelativePath(root, bone);
                if (string.IsNullOrEmpty(path) || !livePaths.Add(path)) continue;

                if (previous.TryGetValue(path, out var entry))
                {
                    entry.boneName = bone.name;
                    rebuilt.Add(entry);
                    continue;
                }

                rebuilt.Add(new HonamiAvatar.BoneEntry
                {
                    boneName = bone.name,
                    bonePath = path,
                    enabled = true
                });
                result.addedBones++;
            }

            result.removedBones = previous.Count - (rebuilt.Count - result.addedBones);
            result.totalBones = rebuilt.Count;
            avatar.bones = rebuilt;

            for (int i = avatar.mirrorBones.Count - 1; i >= 0; i--)
            {
                var pair = avatar.mirrorBones[i];
                if (pair == null)
                {
                    avatar.mirrorBones.RemoveAt(i);
                    continue;
                }

                bool lostA = !string.IsNullOrEmpty(pair.boneA) && !livePaths.Contains(pair.boneA);
                bool lostB = !string.IsNullOrEmpty(pair.boneB) && !livePaths.Contains(pair.boneB);
                if (lostA || lostB) avatar.mirrorBones.RemoveAt(i);
            }

            if (options.autoBindMirrors) result.addedMirrorPairs = BindMirrors(avatar);

            return result;
        }

        public static bool RebuildFromLinkedModel(HonamiAvatar avatar, out HonamiAvatarBuildResult result)
        {
            result = default;
            var modelRoot = LoadLinkedModel(avatar);
            if (modelRoot == null) return false;

            result = Rebuild(avatar, modelRoot, HonamiAvatarBuildOptions.From(avatar.modelSource));
            EditorUtility.SetDirty(avatar);
            return true;
        }

        public static int BindMirrors(HonamiAvatar avatar)
        {
            if (avatar == null) return 0;

            var taken = new HashSet<string>();
            foreach (var pair in avatar.mirrorBones)
            {
                if (pair == null) continue;
                if (!string.IsNullOrEmpty(pair.boneA)) taken.Add(pair.boneA);
                if (!string.IsNullOrEmpty(pair.boneB)) taken.Add(pair.boneB);
            }

            int added = 0;
            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var bone = avatar.bones[i];
                if (bone == null || string.IsNullOrEmpty(bone.bonePath) || taken.Contains(bone.bonePath)) continue;

                string mirror = FindMirrorPath(avatar, bone.boneName);
                if (string.IsNullOrEmpty(mirror) || mirror == bone.bonePath || taken.Contains(mirror)) continue;

                avatar.mirrorBones.Add(new HonamiAvatar.MirrorEntry { boneA = bone.bonePath, boneB = mirror });
                taken.Add(bone.bonePath);
                taken.Add(mirror);
                added++;
            }

            return added;
        }

        public static string FindMirrorPath(HonamiAvatar avatar, string boneName)
        {
            if (avatar == null || string.IsNullOrEmpty(boneName)) return string.Empty;

            string lower = boneName.ToLowerInvariant();
            string mirrorName = null;

            if (lower.EndsWith("_l")) mirrorName = boneName.Substring(0, boneName.Length - 2) + "_r";
            else if (lower.EndsWith("_r")) mirrorName = boneName.Substring(0, boneName.Length - 2) + "_l";
            else if (lower.EndsWith(".l")) mirrorName = boneName.Substring(0, boneName.Length - 2) + ".r";
            else if (lower.EndsWith(".r")) mirrorName = boneName.Substring(0, boneName.Length - 2) + ".l";
            else if (lower.StartsWith("left")) mirrorName = "Right" + boneName.Substring(4);
            else if (lower.StartsWith("right")) mirrorName = "Left" + boneName.Substring(5);
            else if (lower.StartsWith("l_")) mirrorName = "R_" + boneName.Substring(2);
            else if (lower.StartsWith("r_")) mirrorName = "L_" + boneName.Substring(2);

            if (mirrorName == null) return string.Empty;

            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var candidate = avatar.bones[i];
                if (candidate != null && string.Equals(candidate.boneName, mirrorName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.bonePath;
                }
            }

            return string.Empty;
        }

        public static void CountModelDelta(HonamiAvatar avatar, GameObject modelRoot, out int missing, out int stale)
        {
            missing = 0;
            stale = 0;
            if (avatar == null || modelRoot == null) return;

            CollectBoneTransforms(modelRoot, HonamiAvatarBuildOptions.From(avatar.modelSource), BoneBuffer);
            Transform root = modelRoot.transform;

            var modelPaths = new HashSet<string>();
            foreach (var bone in BoneBuffer)
            {
                string path = GetRelativePath(root, bone);
                if (!string.IsNullOrEmpty(path)) modelPaths.Add(path);
            }

            var avatarPaths = new HashSet<string>();
            for (int i = 0; i < avatar.bones.Count; i++)
            {
                var entry = avatar.bones[i];
                if (entry == null || string.IsNullOrEmpty(entry.bonePath)) continue;
                avatarPaths.Add(entry.bonePath);
            }

            foreach (string path in modelPaths)
            {
                if (!avatarPaths.Contains(path)) missing++;
            }

            foreach (string path in avatarPaths)
            {
                if (!modelPaths.Contains(path)) stale++;
            }
        }

        public static HonamiAvatar CreateForModel(string modelAssetPath, HonamiAvatarBuildOptions options)
        {
            var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (modelRoot == null) return null;

            string directory = Path.GetDirectoryName(modelAssetPath);
            directory = string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(modelAssetPath);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + fileName + AvatarAssetSuffix + ".asset");

            var avatar = ScriptableObject.CreateInstance<HonamiAvatar>();
            avatar.modelSource = new HonamiAvatar.ModelSource
            {
                modelGuid = AssetDatabase.AssetPathToGUID(modelAssetPath),
                refreshOnImport = true,
                skinnedBonesOnly = options.skinnedBonesOnly,
                includeInactive = options.includeInactive,
                autoBindMirrors = options.autoBindMirrors
            };

            Rebuild(avatar, modelRoot, options);
            AssetDatabase.CreateAsset(avatar, assetPath);
            AssetDatabase.SaveAssetIfDirty(avatar);
            InvalidateLinkCache();
            return avatar;
        }

        public static HonamiAvatar FindForModel(string modelAssetPath)
        {
            if (string.IsNullOrEmpty(modelAssetPath)) return null;

            string modelGuid = AssetDatabase.AssetPathToGUID(modelAssetPath);
            if (string.IsNullOrEmpty(modelGuid)) return null;

            EnsureLinkCache();
            return _modelGuidToAvatarPath.TryGetValue(modelGuid, out string avatarPath)
                ? AssetDatabase.LoadAssetAtPath<HonamiAvatar>(avatarPath)
                : null;
        }

        public static string GetLinkedModelPath(HonamiAvatar avatar)
        {
            if (avatar == null || avatar.modelSource == null || string.IsNullOrEmpty(avatar.modelSource.modelGuid))
            {
                return string.Empty;
            }

            return AssetDatabase.GUIDToAssetPath(avatar.modelSource.modelGuid);
        }

        public static GameObject LoadLinkedModel(HonamiAvatar avatar)
        {
            string path = GetLinkedModelPath(avatar);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static void LinkToModel(HonamiAvatar avatar, string modelAssetPath)
        {
            if (avatar == null) return;

            avatar.modelSource ??= new HonamiAvatar.ModelSource();
            avatar.modelSource.modelGuid = string.IsNullOrEmpty(modelAssetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(modelAssetPath);

            EditorUtility.SetDirty(avatar);
            InvalidateLinkCache();
        }

        public static bool IsModelAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && AssetImporter.GetAtPath(assetPath) is ModelImporter;
        }

        public static void InvalidateLinkCache() => _modelGuidToAvatarPath = null;

        private static void EnsureLinkCache()
        {
            if (_modelGuidToAvatarPath != null) return;

            _modelGuidToAvatarPath = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:HonamiAvatar"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var avatar = AssetDatabase.LoadAssetAtPath<HonamiAvatar>(path);
                if (avatar == null || avatar.modelSource == null) continue;

                string modelGuid = avatar.modelSource.modelGuid;
                if (string.IsNullOrEmpty(modelGuid) || _modelGuidToAvatarPath.ContainsKey(modelGuid)) continue;

                _modelGuidToAvatarPath[modelGuid] = path;
            }
        }

        private static void AddChain(Transform bone, Transform root, HashSet<Transform> results)
        {
            Transform current = bone;
            while (current != null && current != root)
            {
                if (!results.Add(current)) break;
                current = current.parent;
            }
        }
    }
}
