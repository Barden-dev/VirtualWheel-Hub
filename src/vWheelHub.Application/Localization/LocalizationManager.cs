using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Localization
{
    public class LocalizationManager : ILocalizationManager
    {
        public static LocalizationManager Instance { get; } = new LocalizationManager();

        public const string DefaultCulture = "en-US";
        public const string RussianCulture = "ru-RU";

        public static readonly IReadOnlyList<LanguageInfo> SupportedLanguages = new[]
        {
            new LanguageInfo(DefaultCulture, "EN", "English"),
            new LanguageInfo(RussianCulture, "RU", "Русский")
        };

        public static string? CustomLocalizationDirectory { get; set; }

        private readonly object _syncLock = new();
        private readonly string? _customDirectory;
        private string _currentCultureCode = DefaultCulture;
        private Dictionary<string, string> _activeStrings = new(StringComparer.Ordinal);
        private Dictionary<string, string> _fallbackStrings = new(StringComparer.Ordinal);
        private bool _isInitialized;

        public event Action<string>? LanguageChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string CurrentCultureCode => _currentCultureCode;

        public string CurrentLanguageCode => CurrentLanguage.DisplayCode;

        public LanguageInfo CurrentLanguage
        {
            get
            {
                var available = AvailableLanguages;
                return available.FirstOrDefault(l => string.Equals(l.CultureCode, _currentCultureCode, StringComparison.OrdinalIgnoreCase))
                    ?? available.FirstOrDefault(l => l.CultureCode.StartsWith(_currentCultureCode, StringComparison.OrdinalIgnoreCase))
                    ?? available[0];
            }
        }

        public IReadOnlyList<LanguageInfo> AvailableLanguages => GetAvailableLanguages(_customDirectory);

        public string? CustomDirectory => _customDirectory;

        public string this[string key] => GetString(key);

        public LocalizationManager(string? customDirectory = null)
        {
            _customDirectory = customDirectory;

            // Pre-load fallback strings and default active strings
            var fallback = new Dictionary<string, string>(StringComparer.Ordinal);
            LoadStringsIntoMap(DefaultCulture, fallback);
            _fallbackStrings = fallback;

            var active = new Dictionary<string, string>(StringComparer.Ordinal);
            LoadStringsIntoMap(_currentCultureCode, active);
            _activeStrings = active;
        }

        public void Initialize(string? initialCultureCode = null)
        {
            if (_isInitialized && initialCultureCode == null) return;

            string targetCulture = initialCultureCode ?? AppSettings.Load().ResolvedLanguage;
            SetLanguage(targetCulture, saveToSettings: false);
            _isInitialized = true;
        }

        public void SetLanguage(string cultureCode, bool saveToSettings = true)
        {
            string normalized = NormalizeCulture(cultureCode, _customDirectory);
            _currentCultureCode = normalized;

            // Ensure fallback strings are loaded
            lock (_syncLock)
            {
                if (_fallbackStrings.Count == 0)
                {
                    var fallback = new Dictionary<string, string>(StringComparer.Ordinal);
                    LoadStringsIntoMap(DefaultCulture, fallback);
                    _fallbackStrings = fallback;
                }
            }

            // Load active strings atomically
            var active = new Dictionary<string, string>(StringComparer.Ordinal);
            LoadStringsIntoMap(normalized, active);

            if (active.Count == 0 && !string.Equals(normalized, DefaultCulture, StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Instance.LogWarning($"Localization dictionary for '{normalized}' contains no valid string entries; falling back to '{DefaultCulture}'.");
                normalized = DefaultCulture;
                _currentCultureCode = normalized;
                lock (_syncLock)
                {
                    active = new Dictionary<string, string>(_fallbackStrings, StringComparer.Ordinal);
                }
            }

            lock (_syncLock)
            {
                _activeStrings = active;
            }

            // Use the final culture, including recovery from an invalid dictionary.
            try
            {
                var ci = new System.Globalization.CultureInfo(normalized);
                System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
            catch { }

            // Persist setting if requested
            if (saveToSettings)
            {
                try
                {
                    var settings = AppSettings.Load();
                    settings.Language = normalized;
                    settings.Save();
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Failed to save language setting '{normalized}'", ex);
                }
            }

            // Notify UI
            OnPropertyChanged(nameof(CurrentCultureCode));
            OnPropertyChanged(nameof(CurrentLanguageCode));
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged("Item[]");
            OnPropertyChanged("Item");

            LanguageChanged?.Invoke(normalized);
        }

        public void ToggleLanguage(bool saveToSettings = true)
        {
            var languages = AvailableLanguages;
            if (languages.Count <= 1) return;

            int currentIndex = -1;
            for (int i = 0; i < languages.Count; i++)
            {
                if (string.Equals(languages[i].CultureCode, _currentCultureCode, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + 1) % languages.Count;
            SetLanguage(languages[nextIndex].CultureCode, saveToSettings);
        }

        public bool TryGetActiveString(string key, out string? value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;

            Dictionary<string, string> active;
            lock (_syncLock)
            {
                active = _activeStrings;
            }

            if (active.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                value = val;
                return true;
            }

            return false;
        }

        public bool TryGetFallbackString(string key, out string? value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;

            Dictionary<string, string> fallback;
            lock (_syncLock)
            {
                fallback = _fallbackStrings;
            }

            if (fallback.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                value = val;
                return true;
            }

            return false;
        }

        public bool TryGetString(string key, out string? value)
        {
            if (TryGetActiveString(key, out value))
            {
                return true;
            }

            if (TryGetFallbackString(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public string GetString(string key, string? defaultValue = null)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue ?? string.Empty;

            if (TryGetString(key, out var val))
            {
                return val!;
            }

            return defaultValue ?? key;
        }

        public IReadOnlyDictionary<string, string> GetAllKeys(string cultureCode)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            LoadStringsIntoMap(DefaultCulture, dict);
            string culture = NormalizeCulture(cultureCode, _customDirectory);
            if (!string.Equals(culture, DefaultCulture, StringComparison.OrdinalIgnoreCase))
            {
                var translated = new Dictionary<string, string>(StringComparer.Ordinal);
                LoadStringsIntoMap(culture, translated);
                foreach (var pair in translated)
                    if (!string.IsNullOrWhiteSpace(pair.Value)) dict[pair.Key] = pair.Value;
            }
            return dict;
        }

        public void Reload()
        {
            lock (_syncLock)
            {
                var fallback = new Dictionary<string, string>(StringComparer.Ordinal);
                LoadStringsIntoMap(DefaultCulture, fallback);
                _fallbackStrings = fallback;
            }

            OnPropertyChanged(nameof(AvailableLanguages));
            SetLanguage(_currentCultureCode, saveToSettings: false);
        }

        private void LoadStringsIntoMap(string cultureCode, Dictionary<string, string> targetMap)
        {
            string? filePath = FindLocalizationFile(cultureCode, _customDirectory);
            if (filePath != null && File.Exists(filePath))
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var doc = XDocument.Load(stream);
                    foreach (var element in doc.Descendants())
                    {
                        var keyAttr = element.Attributes().FirstOrDefault(a =>
                            string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase));
                        if (keyAttr != null && !string.IsNullOrEmpty(keyAttr.Value))
                        {
                            targetMap[keyAttr.Value] = element.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Instance.LogError($"Failed to parse XAML dictionary from disk: {filePath}", ex);
                }
            }
        }

        public static string? GetLocalizationDirectory(string? customDir = null)
        {
            if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
            {
                return Path.GetFullPath(customDir);
            }

            if (!string.IsNullOrWhiteSpace(CustomLocalizationDirectory) && Directory.Exists(CustomLocalizationDirectory))
            {
                return Path.GetFullPath(CustomLocalizationDirectory);
            }

            string? firstExisting = null;

            foreach (var dir in GetCandidateDirectories(customDir))
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        if (Directory.GetFiles(dir, "Strings.*.xaml").Length > 0)
                        {
                            return dir;
                        }
                        firstExisting ??= dir;
                    }
                }
                catch { }
            }

            return firstExisting;
        }

        public static IReadOnlyList<LanguageInfo> GetAvailableLanguages(string? customDir = null)
        {
            var list = new List<LanguageInfo>(SupportedLanguages);
            try
            {
                string? locDir = GetLocalizationDirectory(customDir);
                if (locDir != null && Directory.Exists(locDir))
                {
                    var files = Directory.GetFiles(locDir, "Strings.*.xaml");
                    foreach (var file in files)
                    {
                        string fn = Path.GetFileName(file);
                        if (fn.StartsWith("Strings.", StringComparison.OrdinalIgnoreCase) &&
                            fn.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        {
                            string culture = fn.Substring("Strings.".Length, fn.Length - "Strings.".Length - ".xaml".Length);
                            if (string.IsNullOrWhiteSpace(culture)) continue;
                            if (!list.Any(l => string.Equals(l.CultureCode, culture, StringComparison.OrdinalIgnoreCase)))
                            {
                                string displayCode = culture.Length >= 2 ? culture.Substring(0, 2).ToUpperInvariant() : culture.ToUpperInvariant();
                                string displayName = culture;
                                try
                                {
                                    var ci = System.Globalization.CultureInfo.GetCultureInfo(culture);
                                    if (!string.IsNullOrWhiteSpace(ci.TwoLetterISOLanguageName))
                                    {
                                        displayCode = ci.TwoLetterISOLanguageName.ToUpperInvariant();
                                    }
                                    if (!string.IsNullOrWhiteSpace(ci.NativeName))
                                    {
                                        displayName = char.ToUpperInvariant(ci.NativeName[0]) + ci.NativeName.Substring(1);
                                    }
                                }
                                catch { }
                                list.Add(new LanguageInfo(culture, displayCode, displayName));
                            }
                        }
                    }
                }
            }
            catch { }
            return list.OrderBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static string? FindLocalizationFile(string? cultureCode, string? customDir = null)
        {
            if (string.IsNullOrWhiteSpace(cultureCode)) return null;

            string code = cultureCode.Trim();
            var candidateFileNames = new List<string> { $"Strings.{code}.xaml" };

            if (code.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            {
                candidateFileNames.Add("Strings.ru-RU.xaml");
                candidateFileNames.Add("Strings.ru.xaml");
            }
            else if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                candidateFileNames.Add("Strings.en-US.xaml");
                candidateFileNames.Add("Strings.en.xaml");
            }

            string? locDir = GetLocalizationDirectory(customDir);
            if (locDir != null && Directory.Exists(locDir))
            {
                foreach (var fn in candidateFileNames)
                {
                    string direct = Path.Combine(locDir, fn);
                    if (File.Exists(direct)) return Path.GetFullPath(direct);
                }

                // Fuzzy match prefix e.g. "de" -> "Strings.de-DE.xaml"
                try
                {
                    var matches = Directory.GetFiles(locDir, $"Strings.{code}*.xaml");
                    if (matches.Length > 0) return Path.GetFullPath(matches[0]);
                }
                catch { }
            }

            foreach (var dir in GetCandidateDirectories(customDir))
            {
                foreach (var fn in candidateFileNames)
                {
                    string direct = Path.Combine(dir, fn);
                    if (File.Exists(direct)) return Path.GetFullPath(direct);
                }

                if (Directory.Exists(dir))
                {
                    try
                    {
                        var matches = Directory.GetFiles(dir, $"Strings.{code}*.xaml");
                        if (matches.Length > 0) return Path.GetFullPath(matches[0]);
                    }
                    catch { }
                }
            }

            return null;
        }

        public static string NormalizeCulture(string? cultureCode, string? customDir = null)
        {
            if (string.IsNullOrWhiteSpace(cultureCode)) return DefaultCulture;

            cultureCode = cultureCode.Trim();

            var available = GetAvailableLanguages(customDir);
            var exact = available.FirstOrDefault(l => string.Equals(l.CultureCode, cultureCode, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.CultureCode;
            if (cultureCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return RussianCulture;
            if (cultureCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return DefaultCulture;
            var matched = available.FirstOrDefault(l =>
                string.Equals(l.CultureCode, cultureCode, StringComparison.OrdinalIgnoreCase) ||
                l.CultureCode.StartsWith(cultureCode + "-", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.DisplayCode, cultureCode, StringComparison.OrdinalIgnoreCase));

            if (matched != null) return matched.CultureCode;

            if (FindLocalizationFile(cultureCode, customDir) != null) return cultureCode;

            return DefaultCulture;
        }

        private static IEnumerable<string> GetCandidateDirectories(string? customDir = null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            void Add(string? path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        string full = Path.GetFullPath(path);
                        if (seen.Add(full))
                        {
                            result.Add(full);
                        }
                    }
                    catch { }
                }
            }

            if (!string.IsNullOrWhiteSpace(customDir)) Add(customDir);
            if (!string.IsNullOrWhiteSpace(CustomLocalizationDirectory)) Add(CustomLocalizationDirectory);

            string baseDir = AppContext.BaseDirectory;
            string currentDir = Directory.GetCurrentDirectory();

            Add(Path.Combine(baseDir, "Resources", "Localization"));
            Add(Path.Combine(baseDir, "Localization"));
            Add(baseDir);

            Add(Path.Combine(currentDir, "Resources", "Localization"));
            Add(Path.Combine(currentDir, "Localization"));
            Add(currentDir);

            // Walk up from baseDir
            string? walkBase = baseDir;
            for (int i = 0; i < 6 && walkBase != null; i++)
            {
                Add(Path.Combine(walkBase, "Resources", "Localization"));
                Add(Path.Combine(walkBase, "Localization"));
                walkBase = Path.GetDirectoryName(walkBase);
            }

            // Walk up from currentDir
            string? walkCurr = currentDir;
            for (int i = 0; i < 6 && walkCurr != null; i++)
            {
                Add(Path.Combine(walkCurr, "Resources", "Localization"));
                Add(Path.Combine(walkCurr, "Localization"));
                walkCurr = Path.GetDirectoryName(walkCurr);
            }

            return result;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
