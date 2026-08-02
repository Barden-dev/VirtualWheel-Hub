using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimRacingHub.Controls
{
    public partial class KeyBindControl : UserControl
    {
        public static readonly DependencyProperty VirtualKeyCodeProperty =
            DependencyProperty.Register("VirtualKeyCode", typeof(int), typeof(KeyBindControl), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVirtualKeyCodeChanged));

        public int VirtualKeyCode
        {
            get { return (int)GetValue(VirtualKeyCodeProperty); }
            set { SetValue(VirtualKeyCodeProperty, value); }
        }

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register("DisplayText", typeof(string), typeof(KeyBindControl), new PropertyMetadata("None"));

        public string DisplayText
        {
            get { return (string)GetValue(DisplayTextProperty); }
            private set { SetValue(DisplayTextProperty, value); }
        }

        private bool _isListening = false;

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

            if (VirtualKeyCode == 0)
            {
                DisplayText = "None";
                BindButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                return;
            }
            
            BindButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;

            switch (VirtualKeyCode)
            {
                case 0x01: DisplayText = "Left Mouse"; return;
                case 0x02: DisplayText = "Right Mouse"; return;
                case 0x04: DisplayText = "Middle Mouse"; return;
                case 0x05: DisplayText = "Mouse 4"; return;
                case 0x06: DisplayText = "Mouse 5"; return;
                case 0x20: DisplayText = "Space"; return;
                case 0x10: DisplayText = "Shift"; return;
                case 0x11: DisplayText = "Ctrl"; return;
                case 0x12: DisplayText = "Alt"; return;
            }

            try
            {
                Key key = KeyInterop.KeyFromVirtualKey(VirtualKeyCode);
                DisplayText = key.ToString();
            }
            catch
            {
                DisplayText = $"VK: {VirtualKeyCode}";
            }
        }

        private void BindButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isListening)
            {
                _isListening = true;
                UpdateDisplayText();
                BindButton.Focus();
            }
        }

        private void BindButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isListening)
            {
                e.Handled = true;
                int vk = KeyInterop.VirtualKeyFromKey(e.Key);
                if (vk != 0)
                {
                    VirtualKeyCode = vk;
                    _isListening = false;
                    UpdateDisplayText();
                    
                    // Move focus away to avoid catching accidental secondary key presses
                    Keyboard.ClearFocus();
                }
            }
        }

        private void BindButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isListening)
            {
                e.Handled = true;
                int vk = 0;
                if (e.ChangedButton == MouseButton.Left) vk = 0x01;
                else if (e.ChangedButton == MouseButton.Right) vk = 0x02;
                else if (e.ChangedButton == MouseButton.Middle) vk = 0x04;
                else if (e.ChangedButton == MouseButton.XButton1) vk = 0x05;
                else if (e.ChangedButton == MouseButton.XButton2) vk = 0x06;

                if (vk != 0)
                {
                    VirtualKeyCode = vk;
                    _isListening = false;
                    UpdateDisplayText();
                    Keyboard.ClearFocus();
                }
            }
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
            _isListening = false;
            UpdateDisplayText();
            Keyboard.ClearFocus();
        }
    }
}
