using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace HonamiAnimationSystem.Editor.Core
{
    public static class HonamiMissingScriptYamlUtility
    {
        private static readonly Regex DocumentStartRegex = new(@"^--- !u!(\d+) &(\d+)", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex ScriptRefRegex = new(@"m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"(?m)^  m_Name: (.*)$", RegexOptions.Compiled);
        private static readonly Regex ClassIdentifierRegex = new(@"(?m)^  m_EditorClassIdentifier: .*?([A-Za-z0-9_]+)\s*$", RegexOptions.Compiled);

        public static string CleanControllerYaml(string text, out List<string> removedNames)
        {
            removedNames = new List<string>();

            var matches = DocumentStartRegex.Matches(text);
            if (matches.Count == 0) return text;

            var removedFileIds = new List<string>();
            var result = new StringBuilder(text.Length);
            result.Append(text, 0, matches[0].Index);

            for (int i = 0; i < matches.Count; i++)
            {
                int start = matches[i].Index;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                string document = text.Substring(start, end - start);

                string classId = matches[i].Groups[1].Value;
                string fileId = matches[i].Groups[2].Value;

                if (classId == "114" && IsScriptMissing(document))
                {
                    removedFileIds.Add(fileId);
                    removedNames.Add(GetDocumentDisplayName(document, fileId));
                    continue;
                }

                result.Append(document);
            }

            if (removedFileIds.Count == 0) return text;

            string cleaned = result.ToString();
            foreach (string fileId in removedFileIds)
            {
                cleaned = Regex.Replace(cleaned, $@"(?m)^\s*- \{{fileID: {fileId}\}}\s*\r?\n", "");
                cleaned = cleaned.Replace($"{{fileID: {fileId}}}", "{fileID: 0}");
            }

            return cleaned;
        }

        private static bool IsScriptMissing(string document)
        {
            var scriptMatch = ScriptRefRegex.Match(document);
            // No m_Script reference at all means the script field itself is already broken.
            if (!scriptMatch.Success) return true;

            string guid = scriptMatch.Groups[1].Value;
            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(scriptPath)) return true;

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            return monoScript == null || monoScript.GetClass() == null;
        }

        private static string GetDocumentDisplayName(string document, string fileId)
        {
            var classMatch = ClassIdentifierRegex.Match(document);
            if (classMatch.Success) return classMatch.Groups[1].Value;

            var nameMatch = NameRegex.Match(document);
            if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value)) return nameMatch.Groups[1].Value.Trim();

            return $"fileID {fileId}";
        }
    }
}
