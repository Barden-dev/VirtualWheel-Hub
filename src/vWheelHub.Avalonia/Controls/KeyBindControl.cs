using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SimRacingHub.Application.Features.KeyBindings;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Avalonia.Controls
{
    /// <summary>
    /// Interactive Avalonia control for capturing and displaying key and mouse bindings,
    /// with canonical VK name formatting, Esc cancellation, unbind support, and conflict warnings.
    /// </summary>
    public class KeyBindControl : UserControl
    {
        #region Styled Properties

        public static readonly StyledProperty<int> VirtualKeyCodeProperty =
            AvaloniaProperty.Register<KeyBindControl, int>(
                nameof(VirtualKeyCode),
                defaultValue: 0,
                defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<string> BindingNameProperty =
            AvaloniaProperty.Register<KeyBindControl, string>(nameof(BindingName), defaultValue: string.Empty);

        public static readonly StyledProperty<string?> ConflictMessageProperty =
            AvaloniaProperty.Register<KeyBindControl, string?>(nameof(ConflictMessage));

        public static readonly StyledProperty<string?> ValidationMessageProperty =
            AvaloniaProperty.Register<KeyBindControl, string?>(nameof(ValidationMessage));

        public static readonly StyledProperty<bool> HasConflictProperty =
            AvaloniaProperty.Register<KeyBindControl, bool>(nameof(HasConflict), defaultValue: false);

        public static readonly StyledProperty<bool> IsListeningProperty =
            AvaloniaProperty.Register<KeyBindControl, bool>(nameof(IsListening), defaultValue: false);

        public static readonly StyledProperty<string> DisplayTextProperty =
            AvaloniaProperty.Register<KeyBindControl, string>(nameof(DisplayText), defaultValue: "None");

        public static readonly StyledProperty<string> PressAnyKeyTextProperty =
            AvaloniaProperty.Register<KeyBindControl, string>(nameof(PressAnyKeyText), defaultValue: "Press any key...");

        public static readonly StyledProperty<string> NoneTextProperty =
            AvaloniaProperty.Register<KeyBindControl, string>(nameof(NoneText), defaultValue: "None");

        public static readonly StyledProperty<string> ConflictFormatProperty =
            AvaloniaProperty.Register<KeyBindControl, string>(nameof(ConflictFormat), defaultValue: "{0} already used by: {1}");

        public static readonly StyledProperty<KeyBindingSlotViewModel?> SlotViewModelProperty =
            AvaloniaProperty.Register<KeyBindControl, KeyBindingSlotViewModel?>(nameof(SlotViewModel));

        public static readonly StyledProperty<ICommand?> KeyAssignedCommandProperty =
            AvaloniaProperty.Register<KeyBindControl, ICommand?>(nameof(KeyAssignedCommand));

        public static readonly StyledProperty<ICommand?> ConflictOccurredCommandProperty =
            AvaloniaProperty.Register<KeyBindControl, ICommand?>(nameof(ConflictOccurredCommand));

        public static readonly StyledProperty<ICommand?> CaptureCancelledCommandProperty =
            AvaloniaProperty.Register<KeyBindControl, ICommand?>(nameof(CaptureCancelledCommand));

        #endregion

        #region Events

        public event EventHandler<int>? KeyAssigned;
        public event EventHandler? CaptureStarted;
        public event EventHandler? CaptureCancelled;
        public event EventHandler<string>? ConflictOccurred;

        #endregion

        #region CLR Properties

        public int VirtualKeyCode
        {
            get => GetValue(VirtualKeyCodeProperty);
            set => SetValue(VirtualKeyCodeProperty, value);
        }

        public string BindingName
        {
            get => GetValue(BindingNameProperty);
            set => SetValue(BindingNameProperty, value);
        }

        public string? ConflictMessage
        {
            get => GetValue(ConflictMessageProperty);
            set => SetValue(ConflictMessageProperty, value);
        }

        public bool HasConflict
        {
            get => GetValue(HasConflictProperty);
            set => SetValue(HasConflictProperty, value);
        }

        public string? ValidationMessage
        {
            get => GetValue(ValidationMessageProperty);
            set => SetValue(ValidationMessageProperty, value);
        }

        public bool IsListening
        {
            get => GetValue(IsListeningProperty);
            set => SetValue(IsListeningProperty, value);
        }

        public string DisplayText
        {
            get => GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public string PressAnyKeyText
        {
            get => GetValue(PressAnyKeyTextProperty);
            set => SetValue(PressAnyKeyTextProperty, value);
        }

        public string NoneText
        {
            get => GetValue(NoneTextProperty);
            set => SetValue(NoneTextProperty, value);
        }

        public string ConflictFormat
        {
            get => GetValue(ConflictFormatProperty);
            set => SetValue(ConflictFormatProperty, value);
        }

        public KeyBindingSlotViewModel? SlotViewModel
        {
            get => GetValue(SlotViewModelProperty);
            set => SetValue(SlotViewModelProperty, value);
        }

        public ICommand? KeyAssignedCommand
        {
            get => GetValue(KeyAssignedCommandProperty);
            set => SetValue(KeyAssignedCommandProperty, value);
        }

        public ICommand? ConflictOccurredCommand
        {
            get => GetValue(ConflictOccurredCommandProperty);
            set => SetValue(ConflictOccurredCommandProperty, value);
        }

        public ICommand? CaptureCancelledCommand
        {
            get => GetValue(CaptureCancelledCommandProperty);
            set => SetValue(CaptureCancelledCommandProperty, value);
        }

        #endregion

        #region Visual Elements

        private readonly Border _conflictBorder;
        private readonly Button _bindButton;
        private readonly Button _clearButton;
        private readonly TextBlock _conflictTextBlock;

        #endregion

        static KeyBindControl()
        {
            NoneTextProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => { if (ctrl._bindButton != null) ctrl.UpdateDisplayText(); });
            PressAnyKeyTextProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => { if (ctrl._bindButton != null) ctrl.UpdateDisplayText(); });
            VirtualKeyCodeProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => ctrl.OnVirtualKeyCodeChanged());
            IsListeningProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => ctrl.UpdateDisplayText());
            ConflictMessageProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => ctrl.UpdateConflictVisuals());
            HasConflictProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => ctrl.UpdateConflictVisuals());
            ValidationMessageProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, _) => ctrl.UpdateConflictVisuals());
            SlotViewModelProperty.Changed.AddClassHandler<KeyBindControl>((ctrl, e) => ctrl.OnSlotViewModelChanged(e.OldValue as KeyBindingSlotViewModel, e.NewValue as KeyBindingSlotViewModel));
        }

        public KeyBindControl()
        {
            // Visual Tree Construction
            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            _bindButton = new BindingCaptureButton(this)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(12, 6),
                CornerRadius = new CornerRadius(4),
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)), // #1E293B
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))  // #F1F5F9
            };
            _bindButton.Click += OnBindButtonClick;
            _bindButton.PointerPressed += OnBindButtonPointerPressed;
            _bindButton.PointerWheelChanged += OnBindButtonPointerWheelChanged;
            _bindButton.LostFocus += OnBindButtonLostFocus;

            _conflictBorder = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(1),
                Child = _bindButton
            };
            Grid.SetRow(_conflictBorder, 0);
            Grid.SetColumn(_conflictBorder, 0);
            grid.Children.Add(_conflictBorder);

            var clearIcon = new PathIcon { Width = 12, Height = 12 };
            clearIcon.Bind(PathIcon.DataProperty, new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("IconDismissCircle"));
            _clearButton = new Button
            {
                Content = clearIcon,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(8, 6),
                CornerRadius = new CornerRadius(4),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)),
                Foreground = new SolidColorBrush(Color.FromRgb(196, 196, 196))
            };
            ToolTip.SetTip(_clearButton, "Unbind");
            _clearButton.Click += OnClearButtonClick;
            Grid.SetRow(_clearButton, 0);
            Grid.SetColumn(_clearButton, 1);
            grid.Children.Add(_clearButton);

            _conflictTextBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)), // #F87171
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4, 0, 0),
                IsVisible = false
            };
            Grid.SetRow(_conflictTextBlock, 1);
            Grid.SetColumn(_conflictTextBlock, 0);
            Grid.SetColumnSpan(_conflictTextBlock, 2);
            grid.Children.Add(_conflictTextBlock);

            Content = grid;
            UpdateDisplayText();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is KeyBindingSlotViewModel slot)
            {
                SlotViewModel = slot;
            }
        }

        private void OnSlotViewModelChanged(KeyBindingSlotViewModel? oldSlot, KeyBindingSlotViewModel? newSlot)
        {
            if (oldSlot != null)
            {
                oldSlot.PropertyChanged -= OnSlotPropertyChanged;
            }

            if (newSlot != null)
            {
                newSlot.PropertyChanged += OnSlotPropertyChanged;
                VirtualKeyCode = newSlot.VirtualKeyCode;
                IsListening = newSlot.IsListening;
                HasConflict = newSlot.HasConflict;
                ConflictMessage = newSlot.ConflictMessage;
            }

            UpdateDisplayText();
            UpdateConflictVisuals();
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (SlotViewModel == null) return;

            switch (e.PropertyName)
            {
                case nameof(KeyBindingSlotViewModel.VirtualKeyCode):
                    VirtualKeyCode = SlotViewModel.VirtualKeyCode;
                    break;
                case nameof(KeyBindingSlotViewModel.IsListening):
                    IsListening = SlotViewModel.IsListening;
                    break;
                case nameof(KeyBindingSlotViewModel.HasConflict):
                    HasConflict = SlotViewModel.HasConflict;
                    break;
                case nameof(KeyBindingSlotViewModel.ConflictMessage):
                    ConflictMessage = SlotViewModel.ConflictMessage;
                    break;
            }

            UpdateDisplayText();
            UpdateConflictVisuals();
        }

        private void OnVirtualKeyCodeChanged()
        {
            if (SlotViewModel != null && SlotViewModel.VirtualKeyCode != VirtualKeyCode)
            {
                SlotViewModel.VirtualKeyCode = VirtualKeyCode;
            }

            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            if (this.TryFindResource("KeyBind_Unbind_ToolTip", out var unbindTip)) ToolTip.SetTip(_clearButton, unbindTip);
            _clearButton.IsEnabled = VirtualKeyCode != 0 && !IsListening;
            if (IsListening)
            {
                DisplayText = PressAnyKeyText;
                _bindButton.Content = PressAnyKeyText;
                _bindButton.Background = new SolidColorBrush(Color.FromRgb(96, 205, 255));
                _bindButton.Foreground = new SolidColorBrush(Color.FromRgb(0, 48, 71));
                return;
            }

            int vk = VirtualKeyCode;
            if (vk == 0)
            {
                DisplayText = NoneText;
                _bindButton.Content = NoneText;
                _bindButton.Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)); // Slate
                _bindButton.Foreground = new SolidColorBrush(Color.FromRgb(196, 196, 196)); // Muted
            }
            else
            {
                string formattedName = KeyNameFormatter.Format(vk);
                DisplayText = formattedName;
                _bindButton.Content = formattedName;
                _bindButton.Background = new SolidColorBrush(Color.FromRgb(52, 52, 52));
                _bindButton.Foreground = new SolidColorBrush(Color.FromRgb(247, 247, 247));
            }
        }

        private void UpdateConflictVisuals()
        {
            bool showConflict = HasConflict || !string.IsNullOrWhiteSpace(ConflictMessage) || !string.IsNullOrWhiteSpace(ValidationMessage);
            _conflictTextBlock.IsVisible = showConflict;
            _conflictTextBlock.Text = ValidationMessage ?? ConflictMessage ?? string.Empty;
            _conflictTextBlock.FontSize = string.IsNullOrWhiteSpace(ValidationMessage) ? 10.5 : 12;
            _conflictTextBlock.FontWeight = string.IsNullOrWhiteSpace(ValidationMessage) ? FontWeight.Normal : FontWeight.SemiBold;

            _conflictBorder.BorderBrush = showConflict
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // #EF4444 Red
                : Brushes.Transparent;
        }

        #region User Actions

        public void StartCapture()
        {
            ClearConflict();
            IsListening = true;
            if (SlotViewModel != null)
            {
                SlotViewModel.StartCapture();
            }

            CaptureStarted?.Invoke(this, EventArgs.Empty);
            _bindButton.Focus();
        }

        public void CancelCapture()
        {
            if (!IsListening) return;

            IsListening = false;
            if (SlotViewModel != null && SlotViewModel.IsListening)
            {
                SlotViewModel.IsListening = false;
                SlotViewModel.Refresh();
            }

            UpdateDisplayText();
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            CaptureCancelledCommand?.Execute(null);
        }

        public void ClearBinding()
        {
            VirtualKeyCode = 0;
            if (SlotViewModel != null)
            {
                SlotViewModel.Clear();
            }

            ClearConflict();
            IsListening = false;
            UpdateDisplayText();
            KeyAssigned?.Invoke(this, 0);
            KeyAssignedCommand?.Execute(0);
        }

        private void OnBindButtonClick(object? sender, RoutedEventArgs e)
        {
            if (!IsListening)
            {
                StartCapture();
            }
        }

        private void OnClearButtonClick(object? sender, RoutedEventArgs e)
        {
            ClearBinding();
        }

        private void OnBindButtonLostFocus(object? sender, RoutedEventArgs e)
        {
            ((BindingCaptureButton)_bindButton).ResetCapturedKeys();
            if (IsListening)
            {
                CancelCapture();
            }
        }

        private void OnBindButtonKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsListening) return;

            e.Handled = true;

            // Escape cancels capture
            if (e.Key == Key.Escape)
            {
                CancelCapture();
                return;
            }

            // Enum integer values are version-dependent; physical/name identifiers are stable.
            if (CanonicalKeyMapper.TryMapPhysicalKeyToVirtualKey(e.PhysicalKey.ToString(), out int vk) && vk != 0)
            {
                TryAssignKey(vk);
                return;
            }

            if (CanonicalKeyMapper.TryMapKeyNameToVirtualKey(e.Key.ToString(), out vk))
            {
                TryAssignKey(vk);
            }
        }

        private sealed class BindingCaptureButton(KeyBindControl owner) : Button
        {
            private readonly System.Collections.Generic.HashSet<Key> _capturedKeys = new();
            protected override Type StyleKeyOverride => typeof(Button);

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (_capturedKeys.Contains(e.Key)) { e.Handled = true; return; }
                if (owner.IsListening)
                {
                    _capturedKeys.Add(e.Key);
                    owner.OnBindButtonKeyDown(this, e);
                    return;
                }
                base.OnKeyDown(e);
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                if (_capturedKeys.Remove(e.Key)) { e.Handled = true; return; }
                base.OnKeyUp(e);
            }

            public void ResetCapturedKeys() => _capturedKeys.Clear();
        }

        private void OnBindButtonPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsListening) return;

            var point = e.GetCurrentPoint(_bindButton);
            int vk = 0;

            if (point.Properties.IsLeftButtonPressed) vk = CanonicalKeyMapper.MouseLeft;
            else if (point.Properties.IsRightButtonPressed) vk = CanonicalKeyMapper.MouseRight;
            else if (point.Properties.IsMiddleButtonPressed) vk = CanonicalKeyMapper.MouseMiddle;
            else if (point.Properties.IsXButton1Pressed) vk = CanonicalKeyMapper.MouseXButton1;
            else if (point.Properties.IsXButton2Pressed) vk = CanonicalKeyMapper.MouseXButton2;

            if (vk != 0)
            {
                e.Handled = true;
                TryAssignKey(vk);
            }
        }

        private void OnBindButtonPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (!IsListening) return;

            int vk = e.Delta.Y > 0 ? CanonicalKeyMapper.MouseWheelUp : CanonicalKeyMapper.MouseWheelDown;
            e.Handled = true;
            TryAssignKey(vk);
        }

        public void TryAssignKey(int virtualKeyCode)
        {
            if (virtualKeyCode == 0)
            {
                ClearBinding();
                return;
            }

            // Check for conflict via SlotViewModel or parent profile
            if (SlotViewModel != null)
            {
                SlotViewModel.Parent.TryAssignKey(SlotViewModel.ActionId, SlotViewModel.IsSecondary, virtualKeyCode);
                VirtualKeyCode = SlotViewModel.VirtualKeyCode;
                HasConflict = SlotViewModel.HasConflict;
                ConflictMessage = SlotViewModel.ConflictMessage;
                IsListening = false;
                UpdateDisplayText();
                UpdateConflictVisuals();

                if (HasConflict)
                {
                    ConflictOccurred?.Invoke(this, ConflictMessage ?? string.Empty);
                    ConflictOccurredCommand?.Execute(ConflictMessage);
                }
                else
                {
                    KeyAssigned?.Invoke(this, virtualKeyCode);
                    KeyAssignedCommand?.Execute(virtualKeyCode);
                }
                return;
            }

            // Check for conflict if DataContext is ResolvedProfile or contains conflict checker
            if (DataContext is ResolvedProfile resolvedProfile &&
                resolvedProfile.TryGetKeyBindingConflict(virtualKeyCode, BindingName, out string conflicts))
            {
                string msg = string.Format(ConflictFormat, KeyNameFormatter.Format(virtualKeyCode), conflicts);
                ShowConflict(msg);
                IsListening = false;
                UpdateDisplayText();
                ConflictOccurred?.Invoke(this, msg);
                ConflictOccurredCommand?.Execute(msg);
                return;
            }

            // Successfully assign key
            VirtualKeyCode = virtualKeyCode;
            ClearConflict();
            IsListening = false;
            UpdateDisplayText();
            KeyAssigned?.Invoke(this, virtualKeyCode);
            KeyAssignedCommand?.Execute(virtualKeyCode);
        }

        public void ShowConflict(string message)
        {
            ConflictMessage = message;
            HasConflict = true;
            UpdateConflictVisuals();
        }

        public void ClearConflict()
        {
            ConflictMessage = null;
            HasConflict = false;
            UpdateConflictVisuals();
        }

        #endregion
    }
}
