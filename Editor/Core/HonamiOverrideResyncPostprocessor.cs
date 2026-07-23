using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor.Core
{
    /// <summary>
    /// Keeps override controllers in sync when their parent controller (or the override itself) is reimported, so
    /// inherited fields stay live even when the override graph was never opened after a parent edit.
    /// </summary>
    public sealed class HonamiOverrideResyncPostprocessor : AssetPostprocessor
    {
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += () =>
            {
                bool migratedAny = false;
                foreach (var guid in AssetDatabase.FindAssets("t:HonamiOverrideController"))
                {
                    var ov = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(AssetDatabase.GUIDToAssetPath(guid));
                    if (ov == null || ov.parentController == null || !ov.NeedsMigration) continue;

                    HonamiOverrideAuthoring.MigrateLegacy(ov);
                    migratedAny = true;
                }

                if (migratedAny) AssetDatabase.SaveAssets();
            };
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var importedControllers = new List<HonamiController>();
            var toResync = new HashSet<HonamiOverrideController>();

            foreach (var path in importedAssets)
            {
                var ov = AssetDatabase.LoadAssetAtPath<HonamiOverrideController>(path);
                if (ov != null)
                {
                    if (ov.parentController != null) toResync.Add(ov);
                    continue;
                }

                var controller = AssetDatabase.LoadAssetAtPath<HonamiController>(path);
                if (controller != null) importedControllers.Add(controller);
            }

            if (importedControllers.Count > 0)
            {
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
            }

            if (toResync.Count == 0)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                foreach (var ov in toResync)
                {
                    if (ov != null && ov.parentController != null)
                    {
                        HonamiOverrideAuthoring.ResyncFromParent(ov);
                    }
                }
            };
        }
    }
}
