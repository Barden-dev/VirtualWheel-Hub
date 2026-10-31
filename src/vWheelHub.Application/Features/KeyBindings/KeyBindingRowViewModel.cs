using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Application.Features.KeyBindings
{
    /// <summary>
    /// ViewModel representing a single control action row with its primary and secondary key binding slots.
    /// </summary>
    public class KeyBindingRowViewModel : ObservableObject
    {
        private readonly KeyBindingsViewModel _parent;
        private readonly KeyActionId _actionId;
        private readonly string _actionName;
        private readonly string _resourceKey;
        private readonly string _toolTipResourceKey;

        public KeyBindingsViewModel Parent => _parent;

        public KeyActionId ActionId => _actionId;

        public string ActionName => _actionName;

        public string ResourceKey => _resourceKey;

        public string ToolTipResourceKey => _toolTipResourceKey;

        public string DisplayName => _parent.GetLocalizedString(_resourceKey, _actionName);

        public string ToolTip => _parent.GetLocalizedString(_toolTipResourceKey, string.Empty);

        public bool IsEnabled => _actionId switch
        {
            KeyActionId.SteerLeft or KeyActionId.SteerRight => _parent.IsKeyboardSteeringEnabled,
            _ => true
        };

        public KeyBindingSlotViewModel Primary { get; }

        public KeyBindingSlotViewModel Secondary { get; }

        public bool HasConflict => Primary.HasConflict || Secondary.HasConflict;

        public string? ConflictMessage => Primary.ConflictMessage ?? Secondary.ConflictMessage;

        public int PrimaryVirtualKeyCode
        {
            get => Primary.VirtualKeyCode;
            set => Primary.VirtualKeyCode = value;
        }

        public int SecondaryVirtualKeyCode
        {
            get => Secondary.VirtualKeyCode;
            set => Secondary.VirtualKeyCode = value;
        }

        public KeyBindingRowViewModel(
            KeyBindingsViewModel parent,
            KeyActionId actionId,
            string actionName,
            string resourceKey,
            string toolTipResourceKey)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _actionId = actionId;
            _actionName = actionName ?? throw new ArgumentNullException(nameof(actionName));
            _resourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
            _toolTipResourceKey = toolTipResourceKey ?? string.Empty;

            Primary = new KeyBindingSlotViewModel(parent, this, isSecondary: false);
            Secondary = new KeyBindingSlotViewModel(parent, this, isSecondary: true);
        }

        internal int GetSlotKey(bool isSecondary)
        {
            return _parent.GetActionKey(_actionId, isSecondary);
        }

        public void ClearConflict()
        {
            Primary.ClearConflict();
            Secondary.ClearConflict();
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(ConflictMessage));
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ToolTip));
            OnPropertyChanged(nameof(IsEnabled));
            Primary.Refresh();
            Secondary.Refresh();
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(ConflictMessage));
            OnPropertyChanged(nameof(PrimaryVirtualKeyCode));
            OnPropertyChanged(nameof(SecondaryVirtualKeyCode));
        }
    }
}
