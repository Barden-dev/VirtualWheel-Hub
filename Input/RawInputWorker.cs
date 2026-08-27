using System;
using System.Runtime.InteropServices;
using System.Threading;
using SimRacingHub.Core;

namespace SimRacingHub.Input
{
    public class RawInputWorker : IDisposable
    {
        private const int StartupTimeoutMs = 2000;
        private const int StopHandshakeTimeoutMs = 2000;
        private const int ThreadJoinTimeoutMs = 1000;

        private readonly object _lifecycleLock = new object();

        private readonly ManualResetEventSlim _startupComplete = new ManualResetEventSlim(false);
        private Thread? _workerThread;
        private volatile bool _isRunning;
        private volatile bool _startupSucceeded;
        private volatile IntPtr _hwnd;
        private bool _disposed;

        private bool _hasLastAbsolute;
        private int _lastAbsoluteX;
        private int _lastAbsoluteY;

        // Raw Input is a producer for the real-time steering loop.  Do not run
        // steering math (or take a lock) on this message-pump thread: a burst of
        // WM_INPUT messages must be reduced to one cheap atomic operation.
        private long _pendingDeltaX;

        public bool IsRunning => _isRunning;
        public int ConsumeDeltaX()
        {
            long value = Interlocked.Exchange(ref _pendingDeltaX, 0);
            return value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
        }

        public bool Start()
        {
            lock (_lifecycleLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(RawInputWorker));
                if (_isRunning) return _startupSucceeded;

                _startupComplete.Reset();
                _startupSucceeded = false;
                _isRunning = true;

                _workerThread = new Thread(WorkerLoop)
                {
                    Name = "RawInput_HighPriority_Thread",
                    IsBackground = true,
                    // Keep the message pump responsive without starving the
                    // dedicated steering/output loops.
                    Priority = ThreadPriority.Normal
                };

                _workerThread.SetApartmentState(ApartmentState.STA);
                _workerThread.Start();
            }

            try
            {
                if (!_startupComplete.Wait(StartupTimeoutMs))
                {
                    AppLogger.Instance.LogError($"Raw input worker did not initialise within {StartupTimeoutMs} ms");
                    return false;
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            return _startupSucceeded;
        }

        public void Stop()
        {
            Thread? thread;

            lock (_lifecycleLock)
            {
                thread = _workerThread;
                _isRunning = false;
                if (thread == null) return;
            }

            try { _startupComplete.Wait(StopHandshakeTimeoutMs); }
            catch (ObjectDisposedException) { }

            IntPtr hwnd = _hwnd;
            if (hwnd != IntPtr.Zero && !PostMessage(hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero))
            {
                AppLogger.Instance.LogWarning($"Could not post WM_QUIT to the raw input window (Win32 error {Marshal.GetLastWin32Error()})");
            }

            if (!thread.Join(ThreadJoinTimeoutMs))
            {
                AppLogger.Instance.LogWarning($"Raw input worker thread did not exit within {ThreadJoinTimeoutMs} ms");
            }

            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_workerThread, thread)) _workerThread = null;
            }
        }

        private void WorkerLoop()
        {
            try
            {
                IntPtr hwnd = CreateWindowEx(0, "Message", null, 0, 0, 0, 0, 0, (IntPtr)HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                {
                    AppLogger.Instance.LogError($"Could not create the raw input message window (Win32 error {Marshal.GetLastWin32Error()}). Mouse steering is unavailable.");
                    return;
                }

                _hwnd = hwnd;

                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                rid[0].usUsagePage = 0x01; // Generic Desktop Controls
                rid[0].usUsage = 0x02;     // Mouse
                rid[0].dwFlags = RIDEV_INPUTSINK;
                rid[0].hwndTarget = hwnd;

                if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                {
                    AppLogger.Instance.LogError($"Could not register the mouse for raw input (Win32 error {Marshal.GetLastWin32Error()}). Mouse steering is unavailable.");
                    return;
                }

                _hasLastAbsolute = false;
                Interlocked.Exchange(ref _pendingDeltaX, 0);
                _startupSucceeded = true;
                _startupComplete.Set();

                PumpMessages();
            }
            catch (Exception ex)
            {
                _startupSucceeded = false;
                AppLogger.Instance.LogError("Raw input worker thread failed", ex);
            }
            finally
            {
                _isRunning = false;

                IntPtr hwnd = _hwnd;
                _hwnd = IntPtr.Zero;
                if (hwnd != IntPtr.Zero && !DestroyWindow(hwnd))
                {
                    AppLogger.Instance.LogWarning($"Could not destroy the raw input window (Win32 error {Marshal.GetLastWin32Error()})");
                }

                try { _startupComplete.Set(); } catch (ObjectDisposedException) { }
            }
        }

        private void PumpMessages()
        {
            while (_isRunning)
            {
                int result = GetMessage(out MSG msg, IntPtr.Zero, 0, 0);

                if (result == 0) break;
                if (result == -1)
                {
                    AppLogger.Instance.LogError($"GetMessage failed in the raw input loop (Win32 error {Marshal.GetLastWin32Error()})");
                    break;
                }

                if (msg.message == WM_INPUT)
                {
                    ProcessRawInput(msg.lParam);
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
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
                        int deltaX = raw->mouse.lLastX;
                        int deltaY = raw->mouse.lLastY;

                        if ((raw->mouse.usFlags & MOUSE_MOVE_ABSOLUTE) != 0)
                        {
                            if (!_hasLastAbsolute)
                            {
                                _lastAbsoluteX = deltaX;
                                _lastAbsoluteY = deltaY;
                                _hasLastAbsolute = true;
                                return;
                            }

                            int dx = deltaX - _lastAbsoluteX;
                            int dy = deltaY - _lastAbsoluteY;
                            _lastAbsoluteX = deltaX;
                            _lastAbsoluteY = deltaY;
                            deltaX = dx;
                            deltaY = dy;
                        }
                        else if (_hasLastAbsolute)
                        {
                            _hasLastAbsolute = false;
                        }

                        if (deltaX != 0)
                        {
                            Interlocked.Add(ref _pendingDeltaX, deltaX);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            Stop();
            _startupComplete.Dispose();
        }

        // ================= WIN32 API =================
        private const int HWND_MESSAGE = -3;
        private const uint WM_INPUT = 0x00FF;
        private const uint WM_QUIT = 0x0012;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEMOUSE = 0;
        private const ushort MOUSE_MOVE_ABSOLUTE = 0x0001;

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
           uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
           int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
           IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
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
