using System;
using System.Collections.Generic;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public static class LinuxKeyCodeMapper
    {
        // Linux input-event-codes
        public const ushort KEY_RESERVED = 0;
        public const ushort KEY_ESC = 1;
        public const ushort KEY_1 = 2;
        public const ushort KEY_2 = 3;
        public const ushort KEY_3 = 4;
        public const ushort KEY_4 = 5;
        public const ushort KEY_5 = 6;
        public const ushort KEY_6 = 7;
        public const ushort KEY_7 = 8;
        public const ushort KEY_8 = 9;
        public const ushort KEY_9 = 10;
        public const ushort KEY_0 = 11;
        public const ushort KEY_BACKSPACE = 14;
        public const ushort KEY_TAB = 15;
        public const ushort KEY_Q = 16;
        public const ushort KEY_W = 17;
        public const ushort KEY_E = 18;
        public const ushort KEY_R = 19;
        public const ushort KEY_T = 20;
        public const ushort KEY_Y = 21;
        public const ushort KEY_U = 22;
        public const ushort KEY_I = 23;
        public const ushort KEY_O = 24;
        public const ushort KEY_P = 25;
        public const ushort KEY_ENTER = 28;
        public const ushort KEY_LEFTCTRL = 29;
        public const ushort KEY_A = 30;
        public const ushort KEY_S = 31;
        public const ushort KEY_D = 32;
        public const ushort KEY_F = 33;
        public const ushort KEY_G = 34;
        public const ushort KEY_H = 35;
        public const ushort KEY_J = 36;
        public const ushort KEY_K = 37;
        public const ushort KEY_L = 38;
        public const ushort KEY_LEFTSHIFT = 42;
        public const ushort KEY_Z = 44;
        public const ushort KEY_X = 45;
        public const ushort KEY_C = 46;
        public const ushort KEY_V = 47;
        public const ushort KEY_B = 48;
        public const ushort KEY_N = 49;
        public const ushort KEY_M = 50;
        public const ushort KEY_RIGHTSHIFT = 54;
        public const ushort KEY_LEFTALT = 56;
        public const ushort KEY_SPACE = 57;
        public const ushort KEY_F1 = 59;
        public const ushort KEY_F2 = 60;
        public const ushort KEY_F3 = 61;
        public const ushort KEY_F4 = 62;
        public const ushort KEY_F5 = 63;
        public const ushort KEY_F6 = 64;
        public const ushort KEY_F7 = 65;
        public const ushort KEY_F8 = 66;
        public const ushort KEY_F9 = 67;
        public const ushort KEY_F10 = 68;
        public const ushort KEY_F11 = 87;
        public const ushort KEY_F12 = 88;
        public const ushort KEY_RIGHTCTRL = 97;
        public const ushort KEY_RIGHTALT = 100;
        public const ushort KEY_HOME = 102;
        public const ushort KEY_UP = 103;
        public const ushort KEY_PAGEUP = 104;
        public const ushort KEY_LEFT = 105;
        public const ushort KEY_RIGHT = 106;
        public const ushort KEY_END = 107;
        public const ushort KEY_DOWN = 108;
        public const ushort KEY_PAGEDOWN = 109;
        public const ushort KEY_INSERT = 110;
        public const ushort KEY_DELETE = 111;

        private static readonly Dictionary<int, ushort> VkToLinux = new Dictionary<int, ushort>
        {
            // Mouse buttons
            { 0x01, LinuxNative.BTN_LEFT },
            { 0x02, LinuxNative.BTN_RIGHT },
            { 0x04, LinuxNative.BTN_MIDDLE },
            { 0x05, LinuxNative.BTN_SIDE },
            { 0x06, LinuxNative.BTN_EXTRA },

            // Standard keys
            { 0x08, KEY_BACKSPACE },
            { 0x09, KEY_TAB },
            { 0x0D, KEY_ENTER },
            { 0x10, KEY_LEFTSHIFT },
            { 0x11, KEY_LEFTCTRL },
            { 0x12, KEY_LEFTALT },
            { 0x1B, KEY_ESC },
            { 0x20, KEY_SPACE },
            { 0x21, KEY_PAGEUP },
            { 0x22, KEY_PAGEDOWN },
            { 0x23, KEY_END },
            { 0x24, KEY_HOME },
            { 0x25, KEY_LEFT },
            { 0x26, KEY_UP },
            { 0x27, KEY_RIGHT },
            { 0x28, KEY_DOWN },
            { 0x2D, KEY_INSERT },
            { 0x2E, KEY_DELETE },

            // Digits 0-9
            { 0x30, KEY_0 }, { 0x31, KEY_1 }, { 0x32, KEY_2 }, { 0x33, KEY_3 }, { 0x34, KEY_4 },
            { 0x35, KEY_5 }, { 0x36, KEY_6 }, { 0x37, KEY_7 }, { 0x38, KEY_8 }, { 0x39, KEY_9 },

            // Letters A-Z
            { 0x41, KEY_A }, { 0x42, KEY_B }, { 0x43, KEY_C }, { 0x44, KEY_D }, { 0x45, KEY_E },
            { 0x46, KEY_F }, { 0x47, KEY_G }, { 0x48, KEY_H }, { 0x49, KEY_I }, { 0x4A, KEY_J },
            { 0x4B, KEY_K }, { 0x4C, KEY_L }, { 0x4D, KEY_M }, { 0x4E, KEY_N }, { 0x4F, KEY_O },
            { 0x50, KEY_P }, { 0x51, KEY_Q }, { 0x52, KEY_R }, { 0x53, KEY_S }, { 0x54, KEY_T },
            { 0x55, KEY_U }, { 0x56, KEY_V }, { 0x57, KEY_W }, { 0x58, KEY_X }, { 0x59, KEY_Y },
            { 0x5A, KEY_Z },

            // Function keys
            { 0x70, KEY_F1 }, { 0x71, KEY_F2 }, { 0x72, KEY_F3 }, { 0x73, KEY_F4 },
            { 0x74, KEY_F5 }, { 0x75, KEY_F6 }, { 0x76, KEY_F7 }, { 0x77, KEY_F8 },
            { 0x78, KEY_F9 }, { 0x79, KEY_F10 }, { 0x7A, KEY_F11 }, { 0x7B, KEY_F12 },

            // Explicit Modifiers
            { 0xA0, KEY_LEFTSHIFT },
            { 0xA1, KEY_RIGHTSHIFT },
            { 0xA2, KEY_LEFTCTRL },
            { 0xA3, KEY_RIGHTCTRL },
            { 0xA4, KEY_LEFTALT },
            { 0xA5, KEY_RIGHTALT }
        };

        private static readonly Dictionary<ushort, int> LinuxToVk = new Dictionary<ushort, int>();

        static LinuxKeyCodeMapper()
        {
            foreach (var kvp in VkToLinux)
            {
                if (!LinuxToVk.ContainsKey(kvp.Value))
                {
                    LinuxToVk[kvp.Value] = kvp.Key;
                }
            }
        }

        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;

        public static bool IsGenericModifier(int win32Vk, out ushort primary, out ushort secondary)
        {
            switch (win32Vk)
            {
                case VK_SHIFT:
                    primary = KEY_LEFTSHIFT;
                    secondary = KEY_RIGHTSHIFT;
                    return true;
                case VK_CONTROL:
                    primary = KEY_LEFTCTRL;
                    secondary = KEY_RIGHTCTRL;
                    return true;
                case VK_MENU:
                    primary = KEY_LEFTALT;
                    secondary = KEY_RIGHTALT;
                    return true;
                default:
                    primary = 0;
                    secondary = 0;
                    return false;
            }
        }

        public static ushort ToLinuxKey(int win32Vk)
        {
            return VkToLinux.TryGetValue(win32Vk, out ushort linuxCode) ? linuxCode : (ushort)0;
        }

        public static int ToWin32Vk(ushort linuxKey)
        {
            return LinuxToVk.TryGetValue(linuxKey, out int win32Vk) ? win32Vk : 0;
        }
    }
}
