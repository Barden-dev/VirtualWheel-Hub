using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Dialogs
{
    /// <summary>
    /// ViewModel for modal notice and confirmation dialogs in Avalonia.
    /// </summary>
    public partial class MessageDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsError))]
        [NotifyPropertyChangedFor(nameof(IsWarning))]
        [NotifyPropertyChangedFor(nameof(IsInfo))]
        [NotifyPropertyChangedFor(nameof(SeverityColor))]
        private DialogSeverity _severity = DialogSeverity.Info;

        [ObservableProperty]
        private bool _isConfirm;

        [ObservableProperty]
        private string _confirmText = string.Empty;

        [ObservableProperty]
        private string _cancelText = string.Empty;

        public DialogResult Result { get; private set; } = DialogResult.Cancelled;

        public event Action<DialogResult>? RequestClose;

        public bool IsError => Severity == DialogSeverity.Error;
        public bool IsWarning => Severity == DialogSeverity.Warning;
        public bool IsInfo => !IsError && !IsWarning;

        public string SeverityColor => Severity switch
        {
            DialogSeverity.Error => "#EF4444",
            DialogSeverity.Warning => "#F59E0B",
            DialogSeverity.Info => "#3B82F6",
            _ => "#3B82F6"
        };

        private readonly SimRacingHub.Application.Localization.ILocalizationManager _localization;
        public MessageDialogViewModel(SimRacingHub.Application.Localization.ILocalizationManager? localization = null)
        {
            _localization = localization ?? App.Services?.LocalizationManager ?? SimRacingHub.Application.Localization.LocalizationManager.Instance;
            Title = _localization.GetString("Common_Notice");
            ConfirmText = _localization.GetString("Common_OK");
            CancelText = _localization.GetString("Common_Cancel");
        }

        public MessageDialogViewModel(DialogNoticeRequest notice) : this()
        {
            ArgumentNullException.ThrowIfNull(notice);
            Title = notice.Title;
            Message = notice.Message;
            Severity = notice.Severity;
            IsConfirm = false;
            ConfirmText = _localization.GetString("Common_OK");
        }

        public MessageDialogViewModel(DialogConfirmRequest confirm) : this()
        {
            ArgumentNullException.ThrowIfNull(confirm);
            Title = confirm.Title;
            Message = confirm.Message;
            Severity = confirm.Severity;
            IsConfirm = true;
            ConfirmText = string.IsNullOrWhiteSpace(confirm.ConfirmText) || confirm.ConfirmText == "OK" ? _localization.GetString("Common_OK") : confirm.ConfirmText;
            CancelText = string.IsNullOrWhiteSpace(confirm.CancelText) || confirm.CancelText == "Cancel" ? _localization.GetString("Common_Cancel") : confirm.CancelText;
        }

        [RelayCommand]
        public void Confirm()
        {
            Result = DialogResult.Confirmed;
            RequestClose?.Invoke(Result);
        }

        [RelayCommand]
        public void Cancel()
        {
            Result = DialogResult.Cancelled;
            RequestClose?.Invoke(Result);
        }
    }
}
