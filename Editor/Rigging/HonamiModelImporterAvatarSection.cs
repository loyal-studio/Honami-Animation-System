using HonamiAnimationSystem.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    [InitializeOnLoad]
    public static class HonamiModelImporterAvatarSection
    {
        private const string ExpandedKey = "Honami_ModelImporterSection_Expanded";
        private const float ButtonWidth = 62f;

        private static readonly GUIContent AvatarLabel = new("Honami Avatar",
            "Bone list and mirror pairs Honami uses to animate this model. Unlike a Humanoid Avatar it accepts any skeleton.");

        private static readonly GUIContent CreateLabel = new("Create",
            "Create <Model>_HonamiAvatar.asset next to this model, filled with every bone path and auto-bound left/right mirror pairs.");

        private static readonly GUIContent RefreshLabel = new("Refresh On Reimport",
            "Rebuild the bone list automatically every time this model is imported");

        private static readonly GUIContent SkinnedLabel = new("Skinned Bones Only",
            "Keep only the bones referenced by the model's SkinnedMeshRenderers and their parents");

        private static readonly GUIContent InactiveLabel = new("Include Inactive",
            "Include transforms whose GameObject is inactive in the imported model");

        private static readonly GUIContent MirrorsLabel = new("Auto Bind Mirrors",
            "Pair bones by the usual left/right naming conventions after every rebuild");

        private static string _cachedModelPath;
        private static HonamiAvatar _cachedAvatar;
        private static int _missingBones;
        private static int _staleBones;
        private static bool _modelMissing;

        static HonamiModelImporterAvatarSection()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= Draw;
            UnityEditor.Editor.finishedDefaultHeaderGUI += Draw;
        }

        public static void InvalidateCache() => _cachedModelPath = null;

        private static void Draw(UnityEditor.Editor editor)
        {
            if (!HonamiImportSettings.ShowModelImporterSection) return;
            if (editor == null || editor.target == null) return;

            var targets = editor.targets;
            if (targets == null || targets.Length == 0 || targets[0] is not ModelImporter first) return;

            if (targets.Length > 1)
            {
                DrawMultiSelection(targets);
                return;
            }

            DrawSingle(first);
        }

        private static void DrawSingle(ModelImporter importer)
        {
            string modelPath = importer.assetPath;
            if (Event.current.type == EventType.Layout) RefreshCache(modelPath);

            bool hasAvatar = _cachedAvatar != null;
            float labelWidth = EditorGUIUtility.labelWidth;

            Rect line = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(line.x, line.y, labelWidth, line.height);

            float fieldWidth = line.width - labelWidth;
            if (!hasAvatar) fieldWidth -= ButtonWidth + 4f;
            var fieldRect = new Rect(line.x + labelWidth, line.y, Mathf.Max(60f, fieldWidth), line.height);

            bool expanded = EditorPrefs.GetBool(ExpandedKey, true);
            if (hasAvatar)
            {
                bool next = EditorGUI.Foldout(labelRect, expanded, AvatarLabel, true);
                if (next != expanded)
                {
                    EditorPrefs.SetBool(ExpandedKey, next);
                    expanded = next;
                }
            }
            else
            {
                EditorGUI.LabelField(labelRect, AvatarLabel);
            }

            EditorGUI.BeginChangeCheck();
            var picked = (HonamiAvatar)EditorGUI.ObjectField(fieldRect, _cachedAvatar, typeof(HonamiAvatar), false);
            if (EditorGUI.EndChangeCheck() && picked != _cachedAvatar) Relink(picked, modelPath);

            if (!hasAvatar)
            {
                var createRect = new Rect(line.xMax - ButtonWidth, line.y, ButtonWidth, line.height);
                if (GUI.Button(createRect, CreateLabel, EditorStyles.miniButton)) Create(modelPath);
                return;
            }

            if (expanded) DrawBody();
        }

        private static void DrawBody()
        {
            _cachedAvatar.modelSource ??= new HonamiAvatar.ModelSource();
            var source = _cachedAvatar.modelSource;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField($"{_cachedAvatar.bones.Count} bones, {_cachedAvatar.mirrorBones.Count} mirror pairs", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            bool refreshOnImport = EditorGUILayout.Toggle(RefreshLabel, source.refreshOnImport);
            bool skinnedBonesOnly = EditorGUILayout.Toggle(SkinnedLabel, source.skinnedBonesOnly);
            bool includeInactive = EditorGUILayout.Toggle(InactiveLabel, source.includeInactive);
            bool autoBindMirrors = EditorGUILayout.Toggle(MirrorsLabel, source.autoBindMirrors);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_cachedAvatar, "Honami Avatar Import Settings");
                source.refreshOnImport = refreshOnImport;
                source.skinnedBonesOnly = skinnedBonesOnly;
                source.includeInactive = includeInactive;
                source.autoBindMirrors = autoBindMirrors;
                EditorUtility.SetDirty(_cachedAvatar);
                InvalidateCache();
            }

            Rect buttons = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            float half = (buttons.width - 4f) * 0.5f;
            if (GUI.Button(new Rect(buttons.x, buttons.y, half, buttons.height), "Rebuild From Model", EditorStyles.miniButton))
            {
                Rebuild(_cachedAvatar);
            }
            if (GUI.Button(new Rect(buttons.xMax - half, buttons.y, half, buttons.height), "Open Avatar Editor", EditorStyles.miniButton))
            {
                HonamiAvatarEditorWindow.OpenWithAsset(_cachedAvatar);
            }

            if (_missingBones > 0 || _staleBones > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Model and avatar drifted apart: {_missingBones} new bone(s), {_staleBones} bone(s) the model no longer has.",
                    MessageType.Warning);
            }

            if (_modelMissing)
            {
                EditorGUILayout.HelpBox("This avatar is linked to a model that no longer exists. Assign it here again to relink it.", MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawMultiSelection(Object[] targets)
        {
            int withoutAvatar = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not ModelImporter importer) continue;
                if (HonamiAvatarBuilder.FindForModel(importer.assetPath) == null) withoutAvatar++;
            }

            Rect line = EditorGUILayout.GetControlRect();
            var labelRect = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, line.height);
            var statusRect = new Rect(line.x + EditorGUIUtility.labelWidth, line.y, line.width - EditorGUIUtility.labelWidth - ButtonWidth - 4f, line.height);
            var buttonRect = new Rect(line.xMax - ButtonWidth, line.y, ButtonWidth, line.height);

            EditorGUI.LabelField(labelRect, AvatarLabel);
            EditorGUI.LabelField(statusRect, $"{targets.Length - withoutAvatar} of {targets.Length} models linked", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(withoutAvatar == 0))
            {
                if (GUI.Button(buttonRect, new GUIContent($"Create {withoutAvatar}", CreateLabel.tooltip), EditorStyles.miniButton))
                {
                    CreateForAll(targets);
                }
            }
        }

        private static void RefreshCache(string modelPath)
        {
            if (_cachedModelPath == modelPath && _cachedAvatar != null) return;

            _cachedModelPath = modelPath;
            _cachedAvatar = HonamiAvatarBuilder.FindForModel(modelPath);
            _missingBones = 0;
            _staleBones = 0;
            _modelMissing = false;

            if (_cachedAvatar == null) return;

            var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            _modelMissing = HonamiAvatarBuilder.LoadLinkedModel(_cachedAvatar) == null;
            HonamiAvatarBuilder.CountModelDelta(_cachedAvatar, modelRoot, out _missingBones, out _staleBones);
        }

        private static void Create(string modelPath)
        {
            var avatar = HonamiAvatarBuilder.CreateForModel(modelPath, HonamiAvatarBuildOptions.Default);
            if (avatar == null)
            {
                Debug.LogWarning("[Honami] could not load a model at " + modelPath);
                return;
            }

            InvalidateCache();
            EditorGUIUtility.PingObject(avatar);
            Debug.Log($"[Honami] created {avatar.name} with {avatar.bones.Count} bones and {avatar.mirrorBones.Count} mirror pairs.", avatar);
        }

        private static void CreateForAll(Object[] targets)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not ModelImporter importer) continue;
                if (HonamiAvatarBuilder.FindForModel(importer.assetPath) != null) continue;

                HonamiAvatarBuilder.CreateForModel(importer.assetPath, HonamiAvatarBuildOptions.Default);
            }

            InvalidateCache();
        }

        private static void Relink(HonamiAvatar picked, string modelPath)
        {
            if (picked != null)
            {
                string current = HonamiAvatarBuilder.GetLinkedModelPath(picked);
                if (!string.IsNullOrEmpty(current) && current != modelPath)
                {
                    bool proceed = EditorUtility.DisplayDialog("Relink Honami Avatar",
                        $"{picked.name} is already linked to {current}.\n\nLink it to {modelPath} instead?",
                        "Relink", "Cancel");
                    if (!proceed) return;
                }
            }

            if (_cachedAvatar != null && _cachedAvatar != picked)
            {
                Undo.RecordObject(_cachedAvatar, "Unlink Honami Avatar");
                HonamiAvatarBuilder.LinkToModel(_cachedAvatar, null);
            }

            if (picked != null)
            {
                Undo.RecordObject(picked, "Link Honami Avatar");
                HonamiAvatarBuilder.LinkToModel(picked, modelPath);
            }

            if (_cachedAvatar != null) AssetDatabase.SaveAssetIfDirty(_cachedAvatar);
            if (picked != null) AssetDatabase.SaveAssetIfDirty(picked);
            InvalidateCache();
        }

        private static void Rebuild(HonamiAvatar avatar)
        {
            Undo.RecordObject(avatar, "Rebuild Honami Avatar");
            if (!HonamiAvatarBuilder.RebuildFromLinkedModel(avatar, out var result))
            {
                Debug.LogWarning("[Honami] the model linked to " + avatar.name + " is missing.", avatar);
                return;
            }

            AssetDatabase.SaveAssetIfDirty(avatar);
            InvalidateCache();
            Debug.Log($"[Honami] rebuilt {avatar.name}: {result.totalBones} bones (+{result.addedBones} / -{result.removedBones}), {avatar.mirrorBones.Count} mirror pairs.", avatar);
        }
    }
}
