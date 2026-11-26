using UnityEditor;
using UnityEngine;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    public sealed class HonamiProfileGraphWindow : EditorWindow
    {
        [MenuItem("Window/Honami/Honami Profile")]
        public static void OpenWindow()
        {
            var w = GetWindow<HonamiGraphWindow>();
            w.titleContent = HonamiEditorIcons.IconContent("HonamiGraphWhite", "Honami Profile");
            w.minSize = new Vector2(800, 600);

            // Try to load last opened profile graph if none selected
            string last = EditorPrefs.GetString("Honami_LastOpenedProfileGraphPath", "");
            if (!string.IsNullOrEmpty(last))
            {
                var pg = AssetDatabase.LoadAssetAtPath<HonamiControllerProfileGraph>(last);
                if (pg != null) w.SetProfileGraph(pg);
            }
        }
    }
}
