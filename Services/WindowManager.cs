using System;
using System.Runtime.InteropServices;

namespace SimRacingHub.Services
{
    public class WindowManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(ref RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private bool _isCursorLocked = false;
        public bool IsCursorLocked => _isCursorLocked;

        public void LockCursorToCenter()
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;
            
            RECT rect = new RECT 
            { 
                Left = centerX, 
                Top = centerY, 
                Right = centerX + 1, 
                Bottom = centerY + 1 
            };
            
            ClipCursor(ref rect);
            _isCursorLocked = true;
        }

        public void ReapplyLock()
        {
            if (_isCursorLocked)
            {
                LockCursorToCenter();
            }
        }

        public void UnlockCursor()
        {
            if (_isCursorLocked)
            {
                ClipCursor(IntPtr.Zero);
                _isCursorLocked = false;
            }
        }
    }
}
