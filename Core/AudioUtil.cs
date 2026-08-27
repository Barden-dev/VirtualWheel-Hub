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
            Play(0x00000010, 600, 300, "error"); // MB_ICONHAND / MB_ICONERROR
        }

        public static void PlaySuccessSound()
        {
            Play(0x00000040, 1200, 200, "success"); // MB_ICONASTERISK / MB_ICONINFORMATION
        }

        private static void Play(uint messageBeepType, int fallbackFreq, int fallbackDur, string kind)
        {
            bool systemSoundPlayed = false;
            try
            {
                systemSoundPlayed = MessageBeep(messageBeepType);
            }
            catch (Exception ex)
            {
                LogSoundFailure(kind, $"MessageBeep failed: {ex.Message}");
            }

            if (systemSoundPlayed) return;

            try
            {
                Console.Beep(fallbackFreq, fallbackDur);
            }
            catch (Exception ex)
            {
                LogSoundFailure(kind, ex.Message);
            }
        }

        private static void LogSoundFailure(string kind, string reason)
        {
            AppLogger.Instance.LogInfo($"Could not play the {kind} sound: {reason}", showInStatusBar: false);
        }
    }
}
