using System.Collections.Generic;
using UnityEditor;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    /// <summary>
    /// Keeps override controllers in sync when their parent controller is reimported, so inherited fields stay
    /// live even when the override graph was never opened after a parent edit.
    /// </summary>
    public sealed class HonamiOverrideResyncPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var importedControllers = new List<HonamiController>();

            foreach (var path in importedAssets)
            {
                // Checking the main type first avoids loading every imported texture and model.
                // Overrides are a separate type, so their own saves never trigger a resync of the data being edited.
                if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(HonamiController)) continue;

                var controller = AssetDatabase.LoadAssetAtPath<HonamiController>(path);
                if (controller != null) importedControllers.Add(controller);
            }

            if (importedControllers.Count == 0)
            {
                return;
            }

            var toResync = new HashSet<HonamiOverrideController>();
            foreach (var guid in AssetDatabase.FindAssets("t:HonamiOverrideController"))
            {
                var ov = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(AssetDatabase.GUIDToAssetPath(guid));
                if (ov == null || ov.BaseController == null) continue;

                for (int i = 0; i < importedControllers.Count; i++)
                {
                    if (ov.BaseController == importedControllers[i])
                    {
                        toResync.Add(ov);
                        break;
                    }
                }
            }

            if (toResync.Count == 0)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                foreach (var ov in toResync)
                {
                    if (ov != null && ov.parentController != null) HonamiOverrideAuthoring.ResyncFromParent(ov);
                }
            };
        }
    }
}
