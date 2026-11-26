using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiGraphAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Saves initiated by the graph editor itself must not rebuild the view mid-edit.
            if (HonamiGraphView.IsInternalSave) return;
            if (!HonamiGraphWindow.HasOpenInstances<HonamiGraphWindow>()) return;

            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<HonamiGraphWindow>();
                foreach (var window in windows)
                {
                    if (window == null) continue;

                    bool shouldReload = false;
                    string ctrlPath = window.RuntimeController != null ? AssetDatabase.GetAssetPath(window.RuntimeController) : null;
                    string baseCtrlPath = window.Controller != null ? AssetDatabase.GetAssetPath(window.Controller) : null;
                    string linkedPath = window.LinkedGraph != null ? AssetDatabase.GetAssetPath(window.LinkedGraph) : null;
                    string profilePath = window.ProfileGraph != null ? AssetDatabase.GetAssetPath(window.ProfileGraph) : null;

                    foreach (var path in importedAssets)
                    {
                        if ((!string.IsNullOrEmpty(ctrlPath) && path == ctrlPath) ||
                            (!string.IsNullOrEmpty(baseCtrlPath) && path == baseCtrlPath) ||
                            (!string.IsNullOrEmpty(linkedPath) && path == linkedPath) ||
                            (!string.IsNullOrEmpty(profilePath) && path == profilePath))
                        {
                            shouldReload = true;
                            break;
                        }
                    }

                    if (shouldReload)
                    {
                        window.ReloadCurrentGraph();
                    }
                }
            };
        }
    }
}
