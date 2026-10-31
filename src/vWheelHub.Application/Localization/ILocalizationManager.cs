using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SimRacingHub.Application.Localization
{
    public interface ILocalizationManager : INotifyPropertyChanged
    {
        string CurrentCultureCode { get; }

        string CurrentLanguageCode { get; }

        LanguageInfo CurrentLanguage { get; }

        IReadOnlyList<LanguageInfo> AvailableLanguages { get; }

        event Action<string>? LanguageChanged;

        void Initialize(string? initialCultureCode = null);

        void SetLanguage(string cultureCode, bool saveToSettings = true);

        void ToggleLanguage(bool saveToSettings = true);

        string GetString(string key, string? defaultValue = null);

        bool TryGetString(string key, out string? value);

        bool TryGetActiveString(string key, out string? value);

        bool TryGetFallbackString(string key, out string? value);

        string this[string key] { get; }

        IReadOnlyDictionary<string, string> GetAllKeys(string cultureCode);

        void Reload();
    }
}
