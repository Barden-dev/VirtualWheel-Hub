using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimRacingHub.Core;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Controls
{
    public partial class KeyBindControl : UserControl
    {
        public static readonly DependencyProperty VirtualKeyCodeProperty =
            DependencyProperty.Register("VirtualKeyCode", typeof(int), typeof(KeyBindControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVirtualKeyCodeChanged));

        public static readonly DependencyProperty BindingNameProperty =
            DependencyProperty.Register(nameof(BindingName), typeof(string), typeof(KeyBindControl), new PropertyMetadata(string.Empty));

        public int VirtualKeyCode
        {
            get { return (int)GetValue(VirtualKeyCodeProperty); }
            set { SetValue(VirtualKeyCodeProperty, value); }
        }

        public string BindingName
        {
            get { return (string)GetValue(BindingNameProperty); }
            set { SetValue(BindingNameProperty, value); }
        }

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register("DisplayText", typeof(string), typeof(KeyBindControl), new PropertyMetadata("None"));

        public string DisplayText
        {
            get { return (string)GetValue(DisplayTextProperty); }
            private set { SetValue(DisplayTextProperty, value); }
        }

        private bool _isListening;

        public KeyBindControl()
        {
            InitializeComponent();
            UpdateDisplayText();
        }

        private static void OnVirtualKeyCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KeyBindControl control)
            {
                control.UpdateDisplayText();
            }
        }

        private void UpdateDisplayText()
        {
            if (_isListening)
            {
                DisplayText = "Press any key...";
                BindButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                return;
            }

            DisplayText = KeyNameFormatter.Format(VirtualKeyCode);
            BindButton.Appearance = VirtualKeyCode == 0
                ? Wpf.Ui.Controls.ControlAppearance.Secondary
                : Wpf.Ui.Controls.ControlAppearance.Primary;
        }

        private void BindButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isListening)
            {
                ClearConflict();
                _isListening = true;
                UpdateDisplayText();
                BindButton.Focus();
            }
        }

        private void BindButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isListening) return;

            e.Handled = true;
            int virtualKeyCode = KeyInterop.VirtualKeyFromKey(e.Key);
            if (virtualKeyCode != 0)
            {
                TryAssign(virtualKeyCode);
                _isListening = false;
                UpdateDisplayText();
                Keyboard.ClearFocus();
            }
        }

        private void BindButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isListening) return;

            e.Handled = true;
            int virtualKeyCode = e.ChangedButton switch
            {
                MouseButton.Left => 0x01,
                MouseButton.Right => 0x02,
                MouseButton.Middle => 0x04,
                MouseButton.XButton1 => 0x05,
                MouseButton.XButton2 => 0x06,
                _ => 0
            };

            if (virtualKeyCode != 0)
            {
                TryAssign(virtualKeyCode);
                _isListening = false;
                UpdateDisplayText();
                Keyboard.ClearFocus();
            }
        }

        private void TryAssign(int virtualKeyCode)
        {
            if (DataContext is MainViewModel viewModel &&
                viewModel.ResolvedProfile.TryGetKeyBindingConflict(virtualKeyCode, BindingName, out string conflicts))
            {
                ShowConflict($"{KeyNameFormatter.Format(virtualKeyCode)} already used by: {conflicts}");
                return;
            }
            VirtualKeyCode = virtualKeyCode;
            GetBindingExpression(VirtualKeyCodeProperty)?.UpdateSource();
            ClearConflict();
        }

        private void ShowConflict(string message)
        {
            ConflictTextBlock.Text = message;
            ConflictTextBlock.Visibility = Visibility.Visible;
            ConflictBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }

        private void ClearConflict()
        {
            ConflictTextBlock.Text = string.Empty;
            ConflictTextBlock.Visibility = Visibility.Collapsed;
            ConflictBorder.BorderBrush = Brushes.Transparent;
        }

        private void BindButton_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isListening)
            {
                _isListening = false;
                UpdateDisplayText();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            VirtualKeyCode = 0;
            GetBindingExpression(VirtualKeyCodeProperty)?.UpdateSource();
            _isListening = false;
            ClearConflict();
            UpdateDisplayText();
            Keyboard.ClearFocus();
        }
    }
}