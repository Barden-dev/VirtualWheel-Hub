using System;
using System.Runtime.InteropServices;

namespace SimRacingHub.Core
{
    public static class AudioUtil
    {
        [DllImport("user32.dll")]
        private static extern bool MessageBeep(uint uType);

        public static void PlayErrorSound()
        {
            try
            {
                if (!MessageBeep(0x00000010)) // MB_ICONHAND / MB_ICONERROR
                {
                    Console.Beep(600, 300);
                }
            }
            catch
            {
                try { Console.Beep(600, 300); } catch { }
            }
        }

        public static void PlaySuccessSound()
        {
            try
            {
                if (!MessageBeep(0x00000040)) // MB_ICONASTERISK / MB_ICONINFORMATION
                {
                    Console.Beep(1200, 200);
                }
            }
            catch
            {
                try { Console.Beep(1200, 200); } catch { }
            }
        }
    }
}
