using System.Windows.Input;

namespace SimRacingHub.Core
{
    public static class KeyNameFormatter
    {
        public static string Format(int virtualKeyCode)
        {
            return virtualKeyCode switch
            {
                0 => "None",
                0x01 => "Left Mouse",
                0x02 => "Right Mouse",
                0x04 => "Middle Mouse",
                0x05 => "Mouse 4",
                0x06 => "Mouse 5",
                0x20 => "Space",
                0x10 => "Shift",
                0x11 => "Ctrl",
                0x12 => "Alt",
                _ => FormatKeyboardKey(virtualKeyCode)
            };
        }

        private static string FormatKeyboardKey(int virtualKeyCode)
        {
            try
            {
                Key key = KeyInterop.KeyFromVirtualKey(virtualKeyCode);
                return key == Key.None ? $"Key {virtualKeyCode}" : key.ToString();
            }
            catch
            {
                return $"Key {virtualKeyCode}";
            }
        }
    }
}