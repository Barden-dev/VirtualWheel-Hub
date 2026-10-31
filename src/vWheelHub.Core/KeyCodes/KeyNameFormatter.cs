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
                0x0A => "Wheel Up",
                0x0B => "Wheel Down",
                0x08 => "Back",
                0x09 => "Tab",
                0x0C => "Clear",
                0x0D => "Return",
                0x10 => "Shift",
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x13 => "Pause",
                0x14 => "Capital",
                0x15 => "KanaMode",
                0x17 => "JunjaMode",
                0x18 => "FinalMode",
                0x19 => "HanjaMode",
                0x1B => "Escape",
                0x1C => "ImeConvert",
                0x1D => "ImeNonConvert",
                0x1E => "ImeAccept",
                0x1F => "ImeModeChange",
                0x20 => "Space",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x23 => "End",
                0x24 => "Home",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0x29 => "Select",
                0x2A => "Print",
                0x2B => "Execute",
                0x2C => "Snapshot",
                0x2D => "Insert",
                0x2E => "Delete",
                0x2F => "Help",

                // 0-9
                >= 0x30 and <= 0x39 => $"D{(char)virtualKeyCode}",

                // A-Z
                >= 0x41 and <= 0x5A => ((char)virtualKeyCode).ToString(),

                0x5B => "LWin",
                0x5C => "RWin",
                0x5D => "Apps",
                0x5F => "Sleep",

                // Numpad 0-9
                >= 0x60 and <= 0x69 => $"NumPad{virtualKeyCode - 0x60}",
                0x6A => "Multiply",
                0x6B => "Add",
                0x6C => "Separator",
                0x6D => "Subtract",
                0x6E => "Decimal",
                0x6F => "Divide",

                // F1-F24
                >= 0x70 and <= 0x87 => $"F{virtualKeyCode - 0x70 + 1}",

                0x90 => "NumLock",
                0x91 => "Scroll",

                0xA0 => "LeftShift",
                0xA1 => "RightShift",
                0xA2 => "LeftCtrl",
                0xA3 => "RightCtrl",
                0xA4 => "LeftAlt",
                0xA5 => "RightAlt",

                0xA6 => "BrowserBack",
                0xA7 => "BrowserForward",
                0xA8 => "BrowserRefresh",
                0xA9 => "BrowserStop",
                0xAA => "BrowserSearch",
                0xAB => "BrowserFavorites",
                0xAC => "BrowserHome",
                0xAD => "VolumeMute",
                0xAE => "VolumeDown",
                0xAF => "VolumeUp",
                0xB0 => "MediaNextTrack",
                0xB1 => "MediaPreviousTrack",
                0xB2 => "MediaStop",
                0xB3 => "MediaPlayPause",
                0xB4 => "LaunchMail",
                0xB5 => "SelectMedia",
                0xB6 => "LaunchApplication1",
                0xB7 => "LaunchApplication2",

                0xBA => "Oem1",
                0xBB => "OemPlus",
                0xBC => "OemComma",
                0xBD => "OemMinus",
                0xBE => "OemPeriod",
                0xBF => "Oem2",
                0xC0 => "Oem3",
                0xDB => "Oem4",
                0xDC => "Oem5",
                0xDD => "Oem6",
                0xDE => "Oem7",
                0xDF => "Oem8",
                0xE2 => "Oem102",

                _ => $"Key {virtualKeyCode}"
            };
        }
    }
}
