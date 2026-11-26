using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiAvatarModelPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx", ".obj", ".dae", ".3ds", ".dxf", ".blend", ".max", ".ma", ".mb",
            ".c4d", ".lxo", ".jas", ".skp", ".gltf", ".glb", ".usd", ".usda", ".usdc", ".usdz"
        };

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (deletedAssets.Length > 0 || movedAssets.Length > 0)
            {
                HonamiAvatarBuilder.InvalidateLinkCache();
                HonamiModelImporterAvatarSection.InvalidateCache();
            }

            List<string> models = null;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    HonamiAvatarBuilder.InvalidateLinkCache();
                    HonamiModelImporterAvatarSection.InvalidateCache();
                    continue;
                }

                if (!ModelExtensions.Contains(Path.GetExtension(path))) continue;
                if (!HonamiAvatarBuilder.IsModelAsset(path)) continue;

                models ??= new List<string>();
                models.Add(path);
            }

            if (models == null) return;

            EditorApplication.delayCall += () => RefreshAvatars(models);
        }

        private static void RefreshAvatars(List<string> modelPaths)
        {
            List<HonamiAvatar> changed = null;

            for (int i = 0; i < modelPaths.Count; i++)
            {
                var avatar = HonamiAvatarBuilder.FindForModel(modelPaths[i]);
                if (avatar == null || avatar.modelSource == null || !avatar.modelSource.refreshOnImport) continue;

                if (!HonamiAvatarBuilder.RebuildFromLinkedModel(avatar, out var result)) continue;

                changed ??= new List<HonamiAvatar>();
                changed.Add(avatar);
                if (result.addedBones > 0 || result.removedBones > 0)
                {
                    Debug.Log($"[Honami] refreshed {avatar.name} after importing {modelPaths[i]}: {result.totalBones} bones (+{result.addedBones} / -{result.removedBones}).", avatar);
                }
            }

            if (changed == null) return;

            for (int i = 0; i < changed.Count; i++)
                AssetDatabase.SaveAssetIfDirty(changed[i]);
            HonamiModelImporterAvatarSection.InvalidateCache();
        }
    }
}
