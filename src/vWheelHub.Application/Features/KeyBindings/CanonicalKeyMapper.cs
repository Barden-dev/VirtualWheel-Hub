using System;
using System.Collections.Generic;
using SimRacingHub.Core;

namespace SimRacingHub.Application.Features.KeyBindings
{
    /// <summary>
    /// Bidirectional mapper between platform key identifiers (Avalonia Key names, PhysicalKey names,
    /// mouse gestures, Linux input events) and canonical Win32 VirtualKey codes (VK).
    /// </summary>
    public static class CanonicalKeyMapper
    {
        #region Canonical Virtual Key Constants

        public const int None = 0;
        public const int MouseLeft = 0x01;
        public const int MouseRight = 0x02;
        public const int MouseMiddle = 0x04;
        public const int MouseXButton1 = 0x05;
        public const int MouseXButton2 = 0x06;
        public const int MouseWheelUp = 0x0A;
        public const int MouseWheelDown = 0x0B;

        public const int Back = 0x08;
        public const int Tab = 0x09;
        public const int Clear = 0x0C;
        public const int Return = 0x0D;
        public const int Shift = 0x10;
        public const int Control = 0x11;
        public const int Alt = 0x12;
        public const int Pause = 0x13;
        public const int Capital = 0x14;
        public const int Escape = 0x1B;
        public const int Space = 0x20;
        public const int PageUp = 0x21;
        public const int PageDown = 0x22;
        public const int End = 0x23;
        public const int Home = 0x24;
        public const int Left = 0x25;
        public const int Up = 0x26;
        public const int Right = 0x27;
        public const int Down = 0x28;
        public const int Select = 0x29;
        public const int Print = 0x2A;
        public const int Execute = 0x2B;
        public const int Snapshot = 0x2C;
        public const int Insert = 0x2D;
        public const int Delete = 0x2E;
        public const int Help = 0x2F;

        public const int LWin = 0x5B;
        public const int RWin = 0x5C;
        public const int Apps = 0x5D;
        public const int Sleep = 0x5F;

        public const int Multiply = 0x6A;
        public const int Add = 0x6B;
        public const int Separator = 0x6C;
        public const int Subtract = 0x6D;
        public const int Decimal = 0x6E;
        public const int Divide = 0x6F;

        public const int NumLock = 0x90;
        public const int Scroll = 0x91;

        public const int LeftShift = 0xA0;
        public const int RightShift = 0xA1;
        public const int LeftCtrl = 0xA2;
        public const int RightCtrl = 0xA3;
        public const int LeftAlt = 0xA4;
        public const int RightAlt = 0xA5;

        #endregion

        #region Mouse Mapping

        public static int ToVirtualKey(AppMouseButton mouseButton)
        {
            return mouseButton switch
            {
                AppMouseButton.Left => MouseLeft,
                AppMouseButton.Right => MouseRight,
                AppMouseButton.Middle => MouseMiddle,
                AppMouseButton.XButton1 => MouseXButton1,
                AppMouseButton.XButton2 => MouseXButton2,
                AppMouseButton.WheelUp => MouseWheelUp,
                AppMouseButton.WheelDown => MouseWheelDown,
                _ => None
            };
        }

        public static bool TryGetMouseButton(int virtualKeyCode, out AppMouseButton mouseButton)
        {
            switch (virtualKeyCode)
            {
                case MouseLeft:
                    mouseButton = AppMouseButton.Left;
                    return true;
                case MouseRight:
                    mouseButton = AppMouseButton.Right;
                    return true;
                case MouseMiddle:
                    mouseButton = AppMouseButton.Middle;
                    return true;
                case MouseXButton1:
                    mouseButton = AppMouseButton.XButton1;
                    return true;
                case MouseXButton2:
                    mouseButton = AppMouseButton.XButton2;
                    return true;
                case MouseWheelUp:
                    mouseButton = AppMouseButton.WheelUp;
                    return true;
                case MouseWheelDown:
                    mouseButton = AppMouseButton.WheelDown;
                    return true;
                default:
                    mouseButton = AppMouseButton.None;
                    return false;
            }
        }

        public static bool IsMouseButton(int virtualKeyCode)
        {
            return virtualKeyCode is MouseLeft or MouseRight or MouseMiddle or MouseXButton1 or MouseXButton2 or MouseWheelUp or MouseWheelDown;
        }

        #endregion

        #region Avalonia Key and PhysicalKey Mapping

        private static readonly Dictionary<string, int> KeyNameToVk = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, string> VkToKeyName = new();

        private static readonly Dictionary<string, int> PhysicalKeyNameToVk = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, string> VkToPhysicalKeyName = new();

        private static readonly Dictionary<int, int> AvaloniaKeyIntToVk = new();
        private static readonly Dictionary<int, int> VkToAvaloniaKeyInt = new();


        static CanonicalKeyMapper()
        {
            RegisterKey("None", None, "None", 0);
            RegisterKey("Back", Back, "Backspace", 2);
            RegisterKey("Backspace", Back, "Backspace", 2);
            RegisterKey("Tab", Tab, "Tab", 3);
            RegisterKey("Return", Return, "Enter", 6);
            RegisterKey("Enter", Return, "Enter", 6);
            RegisterKey("Pause", Pause, "Pause", 7);
            RegisterKey("CapsLock", Capital, "CapsLock", 8);
            RegisterKey("Capital", Capital, "CapsLock", 8);
            RegisterKey("Escape", Escape, "Escape", 9);
            RegisterKey("Space", Space, "Space", 10);
            RegisterKey("PageUp", PageUp, "PageUp", 11);
            RegisterKey("PageDown", PageDown, "PageDown", 12);
            RegisterKey("End", End, "End", 13);
            RegisterKey("Home", Home, "Home", 14);
            RegisterKey("Left", Left, "ArrowLeft", 15);
            RegisterKey("Up", Up, "ArrowUp", 16);
            RegisterKey("Right", Right, "ArrowRight", 17);
            RegisterKey("Down", Down, "ArrowDown", 18);
            RegisterKey("Select", Select, "Select", 19);
            RegisterKey("Print", Print, "Print", 20);
            RegisterKey("Execute", Execute, "Execute", 21);
            RegisterKey("Snapshot", Snapshot, "PrintScreen", 22);
            RegisterKey("Insert", Insert, "Insert", 23);
            RegisterKey("Delete", Delete, "Delete", 24);
            RegisterKey("Help", Help, "Help", 25);

            // Digits D0-D9 (0x30 - 0x39)
            for (int i = 0; i <= 9; i++)
            {
                int vk = 0x30 + i;
                int avaloniaKeyInt = 34 + i;

                RegisterKey($"D{i}", vk, $"Digit{i}", avaloniaKeyInt);
                KeyNameToVk[$"Digit{i}"] = vk;
                KeyNameToVk[i.ToString()] = vk;
            }

            // Letters A-Z (0x41 - 0x5A)
            for (int i = 0; i < 26; i++)
            {
                int vk = 0x41 + i;
                char c = (char)('A' + i);
                int avaloniaKeyInt = 44 + i;

                RegisterKey(c.ToString(), vk, $"Key{c}", avaloniaKeyInt);
                KeyNameToVk[$"Key{c}"] = vk;
            }

            RegisterKey("LWin", LWin, "MetaLeft", 70);
            RegisterKey("RWin", RWin, "MetaRight", 71);
            RegisterKey("Apps", Apps, "ContextMenu", 72);
            RegisterKey("Sleep", Sleep, "Sleep", 73);

            // Numpad 0-9 (0x60 - 0x69)
            for (int i = 0; i <= 9; i++)
            {
                int vk = 0x60 + i;
                int avaloniaKeyInt = 74 + i;

                RegisterKey($"NumPad{i}", vk, $"Numpad{i}", avaloniaKeyInt);
                KeyNameToVk[$"Numpad{i}"] = vk;
            }

            RegisterKey("Multiply", Multiply, "NumpadMultiply", 84);
            RegisterKey("Add", Add, "NumpadAdd", 85);
            RegisterKey("Separator", Separator, "NumpadComma", 86);
            RegisterKey("Subtract", Subtract, "NumpadSubtract", 87);
            RegisterKey("Decimal", Decimal, "NumpadDecimal", 88);
            RegisterKey("Divide", Divide, "NumpadDivide", 89);

            // Function Keys F1-F24 (0x70 - 0x87)
            for (int i = 1; i <= 24; i++)
            {
                int vk = 0x70 + (i - 1);
                int avaloniaKeyInt = 90 + (i - 1);

                RegisterKey($"F{i}", vk, $"F{i}", avaloniaKeyInt);
            }

            RegisterKey("NumLock", NumLock, "NumLock", 114);
            RegisterKey("Scroll", Scroll, "ScrollLock", 115);

            RegisterKey("LeftShift", LeftShift, "ShiftLeft", 116);
            RegisterKey("RightShift", RightShift, "ShiftRight", 117);
            RegisterKey("LeftCtrl", LeftCtrl, "ControlLeft", 118);
            RegisterKey("RightCtrl", RightCtrl, "ControlRight", 119);
            RegisterKey("LeftAlt", LeftAlt, "AltLeft", 120);
            RegisterKey("RightAlt", RightAlt, "AltRight", 121);

            RegisterKey("Shift", Shift, "ShiftLeft", 116);
            RegisterKey("Ctrl", Control, "ControlLeft", 118);
            RegisterKey("Alt", Alt, "AltLeft", 120);

            // Mouse button and wheel names
            KeyNameToVk["LeftMouse"] = MouseLeft;
            KeyNameToVk["MouseLeft"] = MouseLeft;
            KeyNameToVk["PointerLeft"] = MouseLeft;

            KeyNameToVk["RightMouse"] = MouseRight;
            KeyNameToVk["MouseRight"] = MouseRight;
            KeyNameToVk["PointerRight"] = MouseRight;

            KeyNameToVk["MiddleMouse"] = MouseMiddle;
            KeyNameToVk["MouseMiddle"] = MouseMiddle;
            KeyNameToVk["PointerMiddle"] = MouseMiddle;

            KeyNameToVk["Mouse4"] = MouseXButton1;
            KeyNameToVk["XButton1"] = MouseXButton1;
            KeyNameToVk["PointerXButton1"] = MouseXButton1;

            KeyNameToVk["Mouse5"] = MouseXButton2;
            KeyNameToVk["XButton2"] = MouseXButton2;
            KeyNameToVk["PointerXButton2"] = MouseXButton2;

            KeyNameToVk["WheelUp"] = MouseWheelUp;
            KeyNameToVk["MouseWheelUp"] = MouseWheelUp;

            KeyNameToVk["WheelDown"] = MouseWheelDown;
            KeyNameToVk["MouseWheelDown"] = MouseWheelDown;
        }

        private static void RegisterKey(string keyName, int vk, string physicalKeyName, int avaloniaKeyInt)
        {
            if (!KeyNameToVk.ContainsKey(keyName)) KeyNameToVk[keyName] = vk;
            if (!VkToKeyName.ContainsKey(vk)) VkToKeyName[vk] = keyName;

            if (!PhysicalKeyNameToVk.ContainsKey(physicalKeyName)) PhysicalKeyNameToVk[physicalKeyName] = vk;
            if (!VkToPhysicalKeyName.ContainsKey(vk)) VkToPhysicalKeyName[vk] = physicalKeyName;

            if (avaloniaKeyInt > 0)
            {
                if (!AvaloniaKeyIntToVk.ContainsKey(avaloniaKeyInt)) AvaloniaKeyIntToVk[avaloniaKeyInt] = vk;
                if (!VkToAvaloniaKeyInt.ContainsKey(vk)) VkToAvaloniaKeyInt[vk] = avaloniaKeyInt;
            }
        }

        public static bool TryMapKeyNameToVirtualKey(string? keyName, out int virtualKeyCode)
        {
            if (!string.IsNullOrWhiteSpace(keyName) && KeyNameToVk.TryGetValue(keyName.Trim(), out virtualKeyCode))
            {
                return true;
            }

            virtualKeyCode = None;
            return false;
        }

        public static string ToKeyName(int virtualKeyCode)
        {
            if (VkToKeyName.TryGetValue(virtualKeyCode, out string? name))
            {
                return name;
            }

            return KeyNameFormatter.Format(virtualKeyCode);
        }

        public static bool TryMapPhysicalKeyToVirtualKey(string? physicalKeyName, out int virtualKeyCode)
        {
            if (!string.IsNullOrWhiteSpace(physicalKeyName) && PhysicalKeyNameToVk.TryGetValue(physicalKeyName.Trim(), out virtualKeyCode))
            {
                return true;
            }

            virtualKeyCode = None;
            return false;
        }

        public static string? ToPhysicalKeyName(int virtualKeyCode)
        {
            return VkToPhysicalKeyName.TryGetValue(virtualKeyCode, out string? name) ? name : null;
        }

        public static bool TryMapAvaloniaKeyIntToVirtualKey(int avaloniaKeyInt, out int virtualKeyCode)
        {
            return AvaloniaKeyIntToVk.TryGetValue(avaloniaKeyInt, out virtualKeyCode);
        }

        public static bool TryMapVirtualKeyToAvaloniaKeyInt(int virtualKeyCode, out int avaloniaKeyInt)
        {
            return VkToAvaloniaKeyInt.TryGetValue(virtualKeyCode, out avaloniaKeyInt);
        }

        public static string Format(int virtualKeyCode)
        {
            return KeyNameFormatter.Format(virtualKeyCode);
        }

        #endregion
    }
}
