using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiAvatarMenuItems
    {
        private const string CreateFromModelPath = "Assets/Create Honami Avatar From Model";

        [MenuItem(CreateFromModelPath, true)]
        private static bool ValidateCreateFromModel()
        {
            foreach (var selected in Selection.objects)
            {
                if (HonamiAvatarBuilder.IsModelAsset(AssetDatabase.GetAssetPath(selected))) return true;
            }

            return false;
        }

        [MenuItem(CreateFromModelPath)]
        private static void CreateFromModel()
        {
            HonamiAvatar last = null;
            int created = 0;

            foreach (var selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (!HonamiAvatarBuilder.IsModelAsset(path)) continue;

                var existing = HonamiAvatarBuilder.FindForModel(path);
                if (existing != null)
                {
                    Debug.Log($"[Honami] {path} already has {existing.name}.", existing);
                    last = existing;
                    continue;
                }

                var avatar = HonamiAvatarBuilder.CreateForModel(path, HonamiAvatarBuildOptions.Default);
                if (avatar == null) continue;

                last = avatar;
                created++;
                Debug.Log($"[Honami] created {avatar.name} with {avatar.bones.Count} bones and {avatar.mirrorBones.Count} mirror pairs.", avatar);
            }

            HonamiModelImporterAvatarSection.InvalidateCache();

            if (created == 1 && last != null) HonamiAvatarEditorWindow.OpenWithAsset(last);
            else if (last != null) EditorGUIUtility.PingObject(last);
        }
    }
}
