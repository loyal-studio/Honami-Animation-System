using System.Collections.Generic;
using UnityEditor;

namespace HonamiAnimationSystem.Editor
{
    public static class HonamiPreferences
    {
        private const string RootPath = "Preferences/Honami";

        private static readonly SortedDictionary<string, HonamiPreferenceCategory> Categories = new();

        public static HonamiPreferenceCategory Category(string name)
        {
            if (!Categories.TryGetValue(name, out var category))
            {
                category = new HonamiPreferenceCategory(name);
                Categories.Add(name, category);
                SettingsService.NotifySettingsProviderChanged();
            }

            return category;
        }

        public static void Open(string categoryName)
        {
            SettingsService.OpenUserPreferences($"{RootPath}/{categoryName}");
        }

        [SettingsProviderGroup]
        private static SettingsProvider[] CreateProviders()
        {
            var providers = new SettingsProvider[Categories.Count];
            int index = 0;
            foreach (var category in Categories.Values)
            {
                providers[index++] = category.CreateProvider(RootPath);
            }

            return providers;
        }
    }
}
