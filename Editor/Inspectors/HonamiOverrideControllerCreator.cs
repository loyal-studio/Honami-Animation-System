using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;
using System.IO;

namespace HonamiAnimationSystem.Editor.Inspectors
{
    public static class HonamiOverrideControllerCreator
    {
        [MenuItem("Assets/Create/Honami Animation/Override Controller", false, 15)]
        public static void CreateOverrideController()
        {
            var overrideController = ScriptableObject.CreateInstance<HonamiOverrideController>();
            overrideController.OverrideSchemaVersion = HonamiOverrideController.CurrentOverrideSchemaVersion;
            var selected = Selection.activeObject as HonamiRuntimeController;

            string path = "Assets";
            string defaultName = "NewHonamiOverrideController.asset";

            if (selected != null)
            {
                overrideController.parentController = selected;
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                string directory = Path.GetDirectoryName(selectedPath);
                string baseName = Path.GetFileNameWithoutExtension(selectedPath);

                path = $"{directory}/{baseName} Override.asset";
            }
            else if (Selection.activeObject != null)
            {
                string activePath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(activePath))
                {
                    if (Directory.Exists(activePath))
                        path = $"{activePath}/{defaultName}";
                    else
                        path = $"{Path.GetDirectoryName(activePath)}/{defaultName}";
                }
            }

            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            ProjectWindowUtil.CreateAsset(overrideController, newPath);
        }
    }
}
