using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SimRacingHub.Input
{
    public class RawInputWorker : IDisposable
    {
        private Thread _workerThread;
        private bool _isRunning;
        private IntPtr _hwnd;
        
        public event Action<int, int> OnMouseDelta; 

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _workerThread = new Thread(WorkerLoop)
            {
                Name = "RawInput_HighPriority_Thread",
                IsBackground = true,
                Priority = ThreadPriority.Highest 
            };
            
            _workerThread.SetApartmentState(ApartmentState.STA);
            _workerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            if (_hwnd != IntPtr.Zero)
            {
                PostMessage(_hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void WorkerLoop()
        {
            _hwnd = CreateWindowEx(0, "Message", null, 0, 0, 0, 0, 0, (IntPtr)HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = RIDEV_INPUTSINK; 
            rid[0].hwndTarget = _hwnd;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Exception("Failed to register RawInput device.");
            }

            MSG msg;
            while (_isRunning && GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_INPUT)
                {
                    ProcessRawInput(msg.lParam);
                }
                
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            
            if (dwSize == 0) return;

            unsafe
            {
                byte* buffer = stackalloc byte[(int)dwSize];
                if (GetRawInputData(lParam, RID_INPUT, (IntPtr)buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                {
                    RAWINPUT* raw = (RAWINPUT*)buffer;
                    if (raw->header.dwType == RIM_TYPEMOUSE)
                    {
                        int lLastX = raw->mouse.lLastX;
                        int lLastY = raw->mouse.lLastY;
                        
                        if (lLastX != 0 || lLastY != 0)
                        {
                            OnMouseDelta?.Invoke(lLastX, lLastY);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        // ================= WIN32 API =================
        private const int HWND_MESSAGE = -3;
        private const uint WM_INPUT = 0x00FF;
        private const uint WM_QUIT = 0x0012;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEMOUSE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(24)]
            public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
           uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
           int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
           IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);
    }
}
