using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HonamiAnimationSystem.Editor
{
    internal static class HonamiPackageInfo
    {
        private const string PackageName = "com.loyalstudio.honami-animation-system";

        [Serializable]
        private sealed class Manifest
        {
            public string name;
            public string version;
            public string displayName;
            public RepositoryInfo repository;
        }

        [Serializable]
        private sealed class RepositoryInfo
        {
            public string url;
        }

        private static string _version;
        private static string _repositoryUrl;
        private static string _displayName;
        private static bool _loaded;

        public static string Version { get { EnsureLoaded(); return _version; } }
        public static string ShortVersion { get { EnsureLoaded(); return "v" + SemVerBase(_version); } }
        public static string RepositoryUrl { get { EnsureLoaded(); return _repositoryUrl; } }
        public static string DisplayName { get { EnsureLoaded(); return _displayName; } }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        private static void Load()
        {
            _loaded = true;
            _version = "0.0.0";
            _repositoryUrl = string.Empty;
            _displayName = "Honami";

            var manifest = ReadManifest();
            if (manifest == null) return;

            if (!string.IsNullOrEmpty(manifest.version)) _version = manifest.version;
            if (!string.IsNullOrEmpty(manifest.displayName)) _displayName = manifest.displayName;
            if (manifest.repository != null && !string.IsNullOrEmpty(manifest.repository.url)) _repositoryUrl = manifest.repository.url;
        }

        private static Manifest ReadManifest()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HonamiPackageInfo).Assembly);
            if (packageInfo != null)
            {
                var packageJson = Path.Combine(packageInfo.resolvedPath, "package.json");
                if (File.Exists(packageJson)) return JsonUtility.FromJson<Manifest>(File.ReadAllText(packageJson));
            }

            // Embedded in Assets/ rather than installed as a package: find our own package.json.
            foreach (var guid in AssetDatabase.FindAssets("package"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)) continue;

                var absolutePath = Path.GetFullPath(path);
                if (!File.Exists(absolutePath)) continue;

                var json = File.ReadAllText(absolutePath);
                if (!json.Contains(PackageName)) continue;

                var manifest = JsonUtility.FromJson<Manifest>(json);
                if (manifest.name == PackageName) return manifest;
            }

            return null;
        }

        private static string SemVerBase(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;
            var dash = version.IndexOf('-');
            return dash > 0 ? version.Substring(0, dash) : version;
        }
    }
}
