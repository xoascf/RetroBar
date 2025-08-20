using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using RetroBar.Extensions;

namespace RetroBar.Utilities
{
    public class DictionaryManager : IDisposable
    {
        private const string SYSTEM_NAME = "System";
        private const string DICT_EXT = "xaml";

        private const string LANG_FALLBACK = "English";
        private const string LANG_FOLDER = "Languages";

        private const string THEME_FOLDER = "Themes";
        public const string THEME_DEFAULT = SYSTEM_NAME;

        // Keep track of currently loaded dictionaries for proper cleanup
        private readonly HashSet<ResourceDictionary> _loadedThemes = [];
        private readonly HashSet<ResourceDictionary> _loadedLanguages = [];

        public DictionaryManager()
        {
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;

            // Load initial resources
            SetLanguageFromSettings();
            SetThemeFromSettings();
        }

        public void SetThemeFromSettings()
        {
            string theme = Settings.Instance.Theme;

            // Clear existing theme dictionaries
            ClearDictionaries(_loadedThemes);

            // Always load base system theme first
            LoadDictionary(SYSTEM_NAME, THEME_FOLDER, _loadedThemes);

            // Load specific theme if different from system
            if (!string.Equals(theme, SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                LoadDictionary(theme, THEME_FOLDER, _loadedThemes);
            }

            // Handle system theme parameters
            if (theme.StartsWith(SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.Resources["GlobalFontFamily"] = SystemFonts.CaptionFontFamily;
            }
            else
            {
                Application.Current.Resources.Remove("GlobalFontFamily");
            }
        }

        public void SetLanguageFromSettings()
        {
            string language = Settings.Instance.Language;

            // Clear existing language dictionaries
            ClearDictionaries(_loadedLanguages);

            // Always load fallback English first
            LoadDictionary(LANG_FALLBACK, LANG_FOLDER, _loadedLanguages);

            if (language == SYSTEM_NAME)
            {
                // Load system language(s)
                var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
                string systemLanguageParent = currentUICulture.Parent.NativeName;
                string systemLanguage = currentUICulture.NativeName;

                ManagedShell.Common.Logging.ShellLogger.Info($"Loading system language: {systemLanguageParent}, {systemLanguage}");

                // Load parent language first, then specific
                LoadDictionary(systemLanguageParent, LANG_FOLDER, _loadedLanguages);
                LoadDictionary(systemLanguage, LANG_FOLDER, _loadedLanguages);
            }
            else if (!string.Equals(language, LANG_FALLBACK, StringComparison.OrdinalIgnoreCase))
            {
                // Load specific language
                LoadDictionary(language, LANG_FOLDER, _loadedLanguages);
            }
        }

        private void ClearDictionaries(HashSet<ResourceDictionary> trackedDictionaries)
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            // Remove tracked dictionaries from the application resources
            foreach (var dict in trackedDictionaries.ToList())
            {
                mergedDictionaries.Remove(dict);

                // Properly dispose of resources if the dictionary implements IDisposable
                if (dict is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            trackedDictionaries.Clear();

            // Force garbage collection to help with memory cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private bool LoadDictionary(string name, string folder, HashSet<ResourceDictionary> tracker)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string dictionaryPath = FindDictionaryFile(name, folder);
            if (string.IsNullOrEmpty(dictionaryPath))
                return false;

            try
            {
                var resourceDictionary = new ResourceDictionary
                {
                    Source = new Uri(dictionaryPath, UriKind.RelativeOrAbsolute)
                };

                Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
                tracker.Add(resourceDictionary);

                return true;
            }
            catch (Exception e)
            {
                ManagedShell.Common.Logging.ShellLogger.Error($"Error loading dictionary '{dictionaryPath}': {e.Message}");
                return false;
            }
        }

        private string FindDictionaryFile(string name, string folder)
        {
            string filename = Path.ChangeExtension(name, DICT_EXT);

            // Search paths in order of priority
            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder),
                folder.InLocalAppData(),
                Path.Combine(ExePath.GetExecutableBasePath(), folder)
            };

            foreach (var path in searchPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                string fullPath = Path.Combine(path, filename);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        public string GetThemeInstallDir()
        {
            return THEME_FOLDER.InLocalAppData();
        }

        public List<string> GetThemes()
        {
            return GetAvailableDictionaries(THEME_FOLDER, SYSTEM_NAME);
        }

        public List<string> GetLanguages()
        {
            var languages = new List<string> { SYSTEM_NAME };
            languages.AddRange(GetAvailableDictionaries(LANG_FOLDER, LANG_FALLBACK));
            return [.. languages.Distinct()];
        }

        /// <summary>
        /// Gets display names for themes with localized "System" string
        /// </summary>
        /// <returns>List of theme display names</returns>
        public List<string> GetThemeDisplayNames()
        {
            var themes = GetThemes();
            return themes.Select(GetThemeDisplayName).ToList();
        }

        /// <summary>
        /// Gets display names for languages with localized "System" string
        /// </summary>
        /// <returns>List of language display names</returns>
        public List<string> GetLanguageDisplayNames()
        {
            var languages = GetLanguages();
            return languages.Select(GetLanguageDisplayName).ToList();
        }

        /// <summary>
        /// Gets the localized display name for a theme
        /// </summary>
        /// <param name="themeSettingValue">The theme setting value</param>
        /// <returns>The localized display name</returns>
        public string GetThemeDisplayName(string themeSettingValue)
        {
            if (string.Equals(themeSettingValue, SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return GetLocalizedString("theme_system") ?? SYSTEM_NAME;
            }
            return themeSettingValue;
        }

        /// <summary>
        /// Gets the localized display name for a language
        /// </summary>
        /// <param name="languageSettingValue">The language setting value</param>
        /// <returns>The localized display name</returns>
        public string GetLanguageDisplayName(string languageSettingValue)
        {
            if (string.Equals(languageSettingValue, SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return GetLocalizedString("language_system") ?? SYSTEM_NAME;
            }
            return languageSettingValue;
        }

        /// <summary>
        /// Converts a theme display name back to the setting value
        /// </summary>
        /// <param name="displayName">The display name</param>
        /// <returns>The setting value</returns>
        public string GetThemeSettingValue(string displayName)
        {
            // Check if this is the localized "System" string
            string localizedSystem = GetLocalizedString("theme_system");
            if (localizedSystem != null && string.Equals(displayName, localizedSystem, StringComparison.OrdinalIgnoreCase))
            {
                return SYSTEM_NAME;
            }

            // Check if this is the English "System" string
            if (string.Equals(displayName, SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return SYSTEM_NAME;
            }

            return displayName;
        }

        /// <summary>
        /// Converts a language display name back to the setting value
        /// </summary>
        /// <param name="displayName">The display name</param>
        /// <returns>The setting value</returns>
        public string GetLanguageSettingValue(string displayName)
        {
            // Check if this is the localized "System" string
            string localizedSystem = GetLocalizedString("language_system");
            if (localizedSystem != null && string.Equals(displayName, localizedSystem, StringComparison.OrdinalIgnoreCase))
            {
                return SYSTEM_NAME;
            }

            // Check if this is the English "System" string
            if (string.Equals(displayName, SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return SYSTEM_NAME;
            }

            return displayName;
        }

        /// <summary>
        /// Gets a localized string from the current application resources
        /// </summary>
        /// <param name="key">The resource key</param>
        /// <returns>The localized string or null if not found</returns>
        private string GetLocalizedString(string key)
        {
            try
            {
                var resource = Application.Current.FindResource(key);
                return resource as string;
            }
            catch
            {
                // If the key is not found in the current language, try to get it from the English fallback
                try
                {
                    // First try to find in merged dictionaries that might contain English
                    foreach (var dict in Application.Current.Resources.MergedDictionaries.Reverse())
                    {
                        if (dict.Contains(key))
                        {
                            return dict[key] as string;
                        }
                    }
                }
                catch
                {
                    // Ignore and return null
                }
                return null;
            }
        }

        private List<string> GetAvailableDictionaries(string folder, string defaultName)
        {
            var dictionaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { defaultName };

            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder),
                folder.InLocalAppData(),
                Path.Combine(ExePath.GetExecutableBasePath(), folder)
            };

            foreach (var path in searchPaths.Where(Directory.Exists))
            {
                var files = Directory.GetFiles(path, $"*.{DICT_EXT}");
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    dictionaries.Add(name);
                }
            }

            return [.. dictionaries];
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.Language):
                    SetLanguageFromSettings();
                    break;
                case nameof(Settings.Theme):
                    SetThemeFromSettings();
                    break;
            }
        }

        public void Dispose()
        {
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;

            // Clean up all tracked dictionaries
            ClearDictionaries(_loadedThemes);
            ClearDictionaries(_loadedLanguages);
        }
    }
}