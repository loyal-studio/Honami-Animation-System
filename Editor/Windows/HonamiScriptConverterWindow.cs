using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Utility window that scans and converts standard Unity Animator API calls 
    /// in C# scripts to their Honami Animation System equivalents.
    /// </summary>
    public sealed class HonamiScriptConverterWindow : EditorWindow
    {
        private sealed class FileEntry
        {
            public string path;
            public bool isSelected = true;
        }

        private string targetDirectory = "Assets";
        private List<FileEntry> filesToConvert = new();
        private bool hasScanned = false;
        private Vector2 scrollPos;

        [MenuItem("Window/Honami/Tools/Honami Script API Converter")]
        public static void ShowWindow()
        {
            GetWindow<HonamiScriptConverterWindow>("API Converter");
        }

        private void OnGUI()
        {
            GUILayout.Label("Honami Script API Converter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("This tool scans C# scripts and converts standard Animator API calls to HonamiAnimator API calls. It's recommended to commit your changes to source control before running this tool.", MessageType.Info);
            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            targetDirectory = EditorGUILayout.TextField("Target Directory", targetDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Target Directory", targetDirectory, "");
                if (!string.IsNullOrEmpty(path))
                {
                    targetDirectory = FileUtil.GetProjectRelativePath(path);
                    if (string.IsNullOrEmpty(targetDirectory)) targetDirectory = path;
                    hasScanned = false;
                }
            }
            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan for Convertible Scripts", GUILayout.Height(30)))
            {
                ScanDirectory();
            }

            if (hasScanned)
            {
                EditorGUILayout.Space(10);

                int selectedCount = filesToConvert.Count(f => f.isSelected);
                GUILayout.Label($"Found {filesToConvert.Count} files ({selectedCount} selected for conversion):", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                    filesToConvert.ForEach(f => f.isSelected = true);
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight, GUILayout.Width(80)))
                    filesToConvert.ForEach(f => f.isSelected = false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUI.skin.box, GUILayout.Height(200));
                if (filesToConvert.Count == 0)
                {
                    GUILayout.Label("No scripts requiring conversion found.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    foreach (var file in filesToConvert)
                    {
                        EditorGUILayout.BeginHorizontal();
                        file.isSelected = EditorGUILayout.Toggle(file.isSelected, GUILayout.Width(20));

                        GUIContent icon = new GUIContent(" " + file.path, EditorGUIUtility.ObjectContent(null, typeof(MonoScript)).image, "Click to open in your code editor");

                        // The file name as a clickable label
                        if (GUILayout.Button(icon, EditorStyles.label, GUILayout.Height(18)))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(file.path);
                            if (asset != null) AssetDatabase.OpenAsset(asset);
                        }

                        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

                        GUILayout.FlexibleSpace();

                        // Explicit Open button
                        if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(file.path);
                            if (asset != null) AssetDatabase.OpenAsset(asset);
                        }

                        // Also open on double click anywhere in the rect
                        var rect = GUILayoutUtility.GetLastRect();
                        if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(file.path);
                            if (asset != null) AssetDatabase.OpenAsset(asset);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);
                GUI.enabled = selectedCount > 0;

                GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
                if (GUILayout.Button("Apply Conversion", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Conversion", $"Are you sure you want to convert {selectedCount} files? This action modifies your source code.", "Convert", "Cancel"))
                    {
                        ConvertFiles();
                    }
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }
        }

        private void ScanDirectory()
        {
            filesToConvert.Clear();

            if (!Directory.Exists(targetDirectory))
            {
                EditorUtility.DisplayDialog("Error", $"Directory not found: {targetDirectory}", "OK");
                return;
            }

            string[] allFiles = Directory.GetFiles(targetDirectory, "*.cs", SearchOption.AllDirectories);

            foreach (string file in allFiles)
            {
                string normalizedPath = file.Replace("\\", "/");
                if (normalizedPath.Contains("/HonamiAnimationSystem/")) continue;

                string content = File.ReadAllText(file);
                if (NeedsConversion(content))
                {
                    filesToConvert.Add(new FileEntry { path = file, isSelected = true });
                }
            }
            hasScanned = true;
        }

        private bool NeedsConversion(string content)
        {
            if (!content.Contains("Animator")) return false;

            return Regex.IsMatch(content, @"\bGetComponent<Animator>\b") ||
                   Regex.IsMatch(content, @"\bTryGetComponent<Animator>\b") ||
                   Regex.IsMatch(content, @"(?<!\bclass\s+)(?<!\benum\s+)(?<!\w)Animator(?=\s+[a-zA-Z_])");
        }

        private void ConvertFiles()
        {
            var selectedFiles = filesToConvert.Where(f => f.isSelected).ToList();
            int convertedCount = 0;
            try
            {
                foreach (var file in selectedFiles)
                {
                    EditorUtility.DisplayProgressBar("Converting", $"Processing {Path.GetFileName(file.path)}...", (float)convertedCount / selectedFiles.Count);

                    string content = File.ReadAllText(file.path);
                    string newContent = ProcessContent(content);

                    if (content != newContent)
                    {
                        File.WriteAllText(file.path, newContent);
                        convertedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Conversion Complete", $"Successfully converted {convertedCount} files.", "OK");

            ScanDirectory();
        }

        private string ProcessContent(string content)
        {
            // 1. Find all variable names that are explicitly Animators
            HashSet<string> animVarNames = new HashSet<string>();

            // Match: Animator myVar
            var explicitMatches = Regex.Matches(content, @"\bAnimator\s+([a-zA-Z_][a-zA-Z0-9_]*)");
            foreach (Match m in explicitMatches)
            {
                animVarNames.Add(m.Groups[1].Value);
            }

            // Match: var myVar = GetComponent<Animator>()
            var implicitMatches = Regex.Matches(content, @"\bvar\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*[^;]*GetComponent<Animator>");
            foreach (Match m in implicitMatches)
            {
                animVarNames.Add(m.Groups[1].Value);
            }

            // 2. Safe method replacements only on those variables
            foreach (var varName in animVarNames)
            {
                string safeVar = Regex.Escape(varName);

                // varName.CrossFade( -> varName.PlayState(
                content = Regex.Replace(content, $@"\b{safeVar}\.CrossFade\s*\(", $"{varName}.PlayState(");
                content = Regex.Replace(content, $@"\b{safeVar}\.CrossFadeInFixedTime\s*\(", $"{varName}.PlayState(");

                // varName.Play("State") -> varName.PlayState("State", 0f)
                content = Regex.Replace(content, $@"\b{safeVar}\.Play\s*\(\s*([^,]+?)\s*\)", $"{varName}.PlayState($1, 0f)");
                // varName.Play("State", layer) -> varName.PlayState("State", 0f, layer)
                content = Regex.Replace(content, $@"\b{safeVar}\.Play\s*\(\s*([^,]+?)\s*,\s*([^,]+?)\s*\)", $"{varName}.PlayState($1, 0f, $2)");
                // varName.Play(...) fallback
                content = Regex.Replace(content, $@"\b{safeVar}\.Play\s*\(", $"{varName}.PlayState(");
            }

            // 3. Finally replace the Animator types
            content = Regex.Replace(content, @"\bGetComponent<Animator>\b", "GetComponent<HonamiAnimator>");
            content = Regex.Replace(content, @"\bTryGetComponent<Animator>\b", "TryGetComponent<HonamiAnimator>");
            content = Regex.Replace(content, @"\bAddComponent<Animator>\b", "AddComponent<HonamiAnimator>");

            content = Regex.Replace(content, @"(?<!\bclass\s+)(?<!\benum\s+)(?<!\w)Animator(?=\s+[a-zA-Z_])", "HonamiAnimator");
            content = Regex.Replace(content, @"(?<!\bclass\s+)(?<!\benum\s+)(?<!\w)typeof\(\s*Animator\s*\)", "typeof(HonamiAnimator)");

            return EnsureHonamiUsing(content);
        }

        private static string EnsureHonamiUsing(string content)
        {
            const string honamiNamespace = "HonamiAnimationSystem.Runtime.Core";

            if (!content.Contains("HonamiAnimator")) return content;
            if (Regex.IsMatch(content, $@"^\s*using\s+{Regex.Escape(honamiNamespace)}\s*;", RegexOptions.Multiline)) return content;

            string newLine = content.Contains("\r\n") ? "\r\n" : "\n";
            string usingDirective = $"using {honamiNamespace};";

            var existingUsings = Regex.Matches(content, @"^[ \t]*using\s+[^;\r\n]+;[ \t]*$", RegexOptions.Multiline);
            if (existingUsings.Count > 0)
            {
                var lastUsing = existingUsings[existingUsings.Count - 1];
                return content.Insert(lastUsing.Index + lastUsing.Length, newLine + usingDirective);
            }

            return usingDirective + newLine + content;
        }
    }
}
