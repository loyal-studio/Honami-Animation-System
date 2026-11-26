using System;
using UnityEditor;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiImportSettings
    {
        private const string ModelImporterSectionKey = "Honami_ModelImporterSection";

        public static event Action Changed;

        public static bool ShowModelImporterSection
        {
            get => EditorPrefs.GetBool(ModelImporterSectionKey, true);
            set
            {
                if (EditorPrefs.GetBool(ModelImporterSectionKey, true) == value) return;
                EditorPrefs.SetBool(ModelImporterSectionKey, value);
                Changed?.Invoke();
            }
        }

        [InitializeOnLoadMethod]
        private static void RegisterPreferences()
        {
            HonamiPreferences.Category("Import")
                .WithDescription("Honami additions to Unity's asset import inspectors. Right-click a field to reset it.")
                .AddToggle("Model Importer Section", () => ShowModelImporterSection, value => ShowModelImporterSection = value,
                    "Show the Honami Avatar section in the inspector of imported models (FBX, OBJ, glTF and friends)",
                    () => ShowModelImporterSection = true);
        }
    }
}
