using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimRacingHub.Avalonia.Dialogs
{
    /// <summary>
    /// ViewModel for confirming unsaved profile changes before context change or exit.
    /// </summary>
    public partial class UnsavedChangesDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "Unsaved Changes";

        [ObservableProperty]
        private string _message = "You have unsaved changes in the current profile. What would you like to do?";

        [ObservableProperty]
        private string? _profileName;

        public UnsavedChangesDialogResult Result { get; private set; } = UnsavedChangesDialogResult.Cancel;

        public event Action<UnsavedChangesDialogResult>? RequestClose;

        public UnsavedChangesDialogViewModel(string? profileName = null, string? title = null, string? message = null,
            SimRacingHub.Application.Localization.ILocalizationManager? localizationManager = null)
        {
            _profileName = profileName;
            var strings = localizationManager ?? new SimRacingHub.Application.Localization.LocalizationManager();
            _title = !string.IsNullOrWhiteSpace(title) ? title : strings.GetString("Title_UnsavedChanges", "Unsaved Changes");
            _message = !string.IsNullOrWhiteSpace(message) ? message : strings.GetString("Ui_UnsavedMessage", "Save your profile changes before continuing?");
        }

        [RelayCommand]
        public void Save()
        {
            Result = UnsavedChangesDialogResult.Save;
            RequestClose?.Invoke(Result);
        }

        [RelayCommand]
        public void Discard()
        {
            Result = UnsavedChangesDialogResult.Discard;
            RequestClose?.Invoke(Result);
        }

        [RelayCommand]
        public void Cancel()
        {
            Result = UnsavedChangesDialogResult.Cancel;
            RequestClose?.Invoke(Result);
        }
    }
}
