using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.KeyBindings
{
    /// <summary>
    /// ViewModel for an individual key binding slot (Primary or Secondary) of a control action.
    /// </summary>
    public partial class KeyBindingSlotViewModel : ObservableObject
    {
        private readonly KeyBindingsViewModel _parent;
        private readonly KeyBindingRowViewModel _row;
        private readonly bool _isSecondary;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private bool _isListening;

        [ObservableProperty]
        private bool _hasConflict;

        [ObservableProperty]
        private string? _conflictMessage;

        public KeyBindingsViewModel Parent => _parent;

        public KeyBindingRowViewModel Row => _row;

        public KeyActionId ActionId => _row.ActionId;

        public string ActionName => _row.ActionName;

        public bool IsSecondary => _isSecondary;

        public bool IsEnabled => _row.IsEnabled;

        public int VirtualKeyCode
        {
            get => _row.GetSlotKey(_isSecondary);
            set => _parent.TryAssignKey(ActionId, _isSecondary, value);
        }

        public string DisplayText
        {
            get
            {
                if (IsListening)
                {
                    return _parent.PressAnyKeyText;
                }

                int code = VirtualKeyCode;
                return code == 0 ? _parent.NoneText : KeyNameFormatter.Format(code);
            }
        }

        public IRelayCommand StartCaptureCommand { get; }

        public IRelayCommand ClearCommand { get; }

        public KeyBindingSlotViewModel(
            KeyBindingsViewModel parent,
            KeyBindingRowViewModel row,
            bool isSecondary)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _isSecondary = isSecondary;

            StartCaptureCommand = new RelayCommand(StartCapture);
            ClearCommand = new RelayCommand(Clear);
        }

        public void StartCapture()
        {
            if (!IsEnabled) return;
            _parent.StartCapture(this);
        }

        public void Clear()
        {
            _parent.ClearBinding(this);
        }

        public void SetConflict(string message)
        {
            ConflictMessage = message;
            HasConflict = true;
        }

        public void ClearConflict()
        {
            ConflictMessage = null;
            HasConflict = false;
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(VirtualKeyCode));
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(ConflictMessage));
        }
    }
}
