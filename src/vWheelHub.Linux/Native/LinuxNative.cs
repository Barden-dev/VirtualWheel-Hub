using System;
using System.Runtime.InteropServices;

namespace SimRacingHub.Linux.Native
{
    /// <summary>
    /// Linux input event packet as defined in &lt;linux/input.h&gt;.
    /// Exact size on Linux x86_64 ABI: 24 bytes (timeval 16B, type 2B, code 2B, value 4B).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InputEvent
    {
        public long TimeSec;   // 8 bytes: timeval.tv_sec
        public long TimeUsec;  // 8 bytes: timeval.tv_usec
        public ushort Type;    // 2 bytes: EV_SYN, EV_KEY, EV_REL, EV_ABS
        public ushort Code;    // 2 bytes: ABS_X, BTN_A, etc.
        public int Value;      // 4 bytes: axis position or button state (0/1)
    }

    /// <summary>
    /// Device identification struct as defined in &lt;linux/input.h&gt;.
    /// Exact size: 8 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InputId
    {
        public ushort Bustype; // e.g. BUS_USB = 0x0003, BUS_VIRTUAL = 0x0006
        public ushort Vendor;  // e.g. 0x045E, 0x046D
        public ushort Product; // e.g. 0x028E, 0xC29B
        public ushort Version; // e.g. 0x0110
    }

    /// <summary>
    /// Absolute axis parameters as defined in &lt;linux/input.h&gt;.
    /// Exact size: 24 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct InputAbsInfo
    {
        public int Value;      // Current axis value
        public int Minimum;    // Minimum axis value (e.g. 0 or -32768)
        public int Maximum;    // Maximum axis value (e.g. 32768 or 32767)
        public int Fuzz;       // Noise filtering deadband
        public int Flat;       // Center resting deadzone
        public int Resolution; // Units per mm or radians
    }

    /// <summary>
    /// Axis setup payload for UI_ABS_SETUP ioctl as defined in &lt;linux/uinput.h&gt;.
    /// Exact size: 28 bytes (2 bytes code + 2 bytes padding + 24 bytes absinfo).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UInputAbsSetup
    {
        public ushort Code;          // ABS_X, ABS_Y, ABS_RZ, etc.
        private ushort _padding;     // 2 bytes kernel alignment padding
        public InputAbsInfo AbsInfo; // 24 bytes
    }

    /// <summary>
    /// Device setup payload for UI_DEV_SETUP ioctl as defined in &lt;linux/uinput.h&gt;.
    /// Exact size: 92 bytes (8 bytes id + 80 bytes name + 4 bytes ff_effects_max).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UInputSetup
    {
        public InputId Id;
        public fixed byte Name[80]; // UINPUT_MAX_NAME_SIZE = 80
        public uint FfEffectsMax;

        public void SetName(string deviceName)
        {
            fixed (byte* ptr = Name)
            {
                Span<byte> span = new Span<byte>(ptr, 80);
                span.Clear();
                if (!string.IsNullOrEmpty(deviceName))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(deviceName);
                    int length = Math.Min(bytes.Length, 79);
                    bytes.AsSpan(0, length).CopyTo(span);
                }
            }
        }
    }

    /// <summary>
    /// POSIX high-resolution timestamp struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeSpec
    {
        public long TvSec;
        public long TvNsec;
    }

    /// <summary>
    /// POSIX interval timer specification for timerfd.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ITimerSpec
    {
        public TimeSpec Interval;
        public TimeSpec Value;
    }

    /// <summary>
    /// File descriptor polling structure for poll(2).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    /// <summary>
    /// Kernel constants and P/Invoke bindings.
    /// </summary>
    public static class LinuxNative
    {
        // Event types
        public const ushort EV_SYN = 0x00;
        public const ushort EV_KEY = 0x01;
        public const ushort EV_REL = 0x02;
        public const ushort EV_ABS = 0x03;

        // Synchronization codes
        public const ushort SYN_REPORT = 0x00;
        public const ushort SYN_DROPPED = 0x03;

        // Relative axes
        public const ushort REL_X = 0x00;
        public const ushort REL_Y = 0x01;

        // Absolute axes
        public const ushort ABS_X = 0x00;
        public const ushort ABS_Y = 0x01;
        public const ushort ABS_Z = 0x02;
        public const ushort ABS_RX = 0x03;
        public const ushort ABS_RY = 0x04;
        public const ushort ABS_RZ = 0x05;

        // Bus types
        public const ushort BUS_USB = 0x03;
        public const ushort BUS_VIRTUAL = 0x06;

        // Gamepad buttons
        public const ushort BTN_SOUTH = 0x130; // BTN_A
        public const ushort BTN_EAST = 0x131;  // BTN_B
        public const ushort BTN_NORTH = 0x133; // BTN_X
        public const ushort BTN_WEST = 0x134;  // BTN_Y
        public const ushort BTN_TL = 0x136;    // Left Bumper (LB)
        public const ushort BTN_TR = 0x137;    // Right Bumper (RB)
        public const ushort BTN_SELECT = 0x13A;// Back
        public const ushort BTN_START = 0x13B; // Start
        public const ushort BTN_MODE = 0x13C;  // Guide
        public const ushort BTN_THUMBL = 0x13D;// Left Stick Click
        public const ushort BTN_THUMBR = 0x13E;// Right Stick Click

        // Shifter & wheel buttons
        public const ushort BTN_GEAR_DOWN = 0x150;
        public const ushort BTN_GEAR_UP = 0x151;

        // Mouse buttons
        public const ushort BTN_LEFT = 0x110;
        public const ushort BTN_RIGHT = 0x111;
        public const ushort BTN_MIDDLE = 0x112;
        public const ushort BTN_SIDE = 0x113;
        public const ushort BTN_EXTRA = 0x114;

        // uinput ioctl codes (Linux x86_64)
        public const uint UI_DEV_CREATE  = 0x00005501;
        public const uint UI_DEV_DESTROY = 0x00005502;
        public const uint UI_DEV_SETUP   = 0x405C5503;
        public const uint UI_ABS_SETUP   = 0x401C5504;
        public const uint UI_SET_EVBIT   = 0x40045564;
        public const uint UI_SET_KEYBIT  = 0x40045565;
        public const uint UI_SET_RELBIT  = 0x40045566;
        public const uint UI_SET_ABSBIT  = 0x40045567;
        public const uint UI_SET_MSCBIT  = 0x40045568;

        // evdev ioctl codes
        public const uint EVIOCGRAB        = 0x40044590;
        public const uint EVIOCGKEY_64     = 0x80404518;
        public const uint EVIOCGID         = 0x80084502;
        public const uint EVIOCGNAME_256   = 0x81004506;
        public const uint EVIOCGBIT_EV_8   = 0x80084520;
        public const uint EVIOCGBIT_KEY_96 = 0x80604521;
        public const uint EVIOCGBIT_REL_4  = 0x80044522;
        public const uint EVIOCGBIT_ABS_8  = 0x80084523;

        // File flags
        public const int O_RDONLY = 0x0000;
        public const int O_WRONLY = 0x0001;
        public const int O_RDWR = 0x0002;
        public const int O_NONBLOCK = 0x0800;
        public const int O_CLOEXEC = 0x80000;

        // Poll flags
        public const short POLLIN = 0x0001;
        public const short POLLPRI = 0x0002;
        public const short POLLOUT = 0x0004;
        public const short POLLERR = 0x0008;
        public const short POLLHUP = 0x0010;

        // Clock IDs
        public const int CLOCK_MONOTONIC = 1;
        public const int TIMER_ABSTIME = 1;

        // libc P/Invoke
        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        public static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags, int mode = 0);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        public static extern int Close(int fd);

        [DllImport("libc", EntryPoint = "read", SetLastError = true)]
        public static extern nint Read(int fd, IntPtr buf, nuint count);

        [DllImport("libc", EntryPoint = "write", SetLastError = true)]
        public static extern nint Write(int fd, IntPtr buf, nuint count);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, IntPtr arg);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, int arg);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, ref UInputSetup setup);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, ref UInputAbsSetup absSetup);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, byte[] buf);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        public static extern int Ioctl(int fd, uint request, ref InputId id);

        [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
        public static extern int Poll([In, Out] PollFd[] fds, nuint nfds, int timeout);

        [DllImport("libc", EntryPoint = "clock_nanosleep", SetLastError = true)]
        public static extern int ClockNanosleep(int clockId, int flags, ref TimeSpec req, IntPtr rem);

        [DllImport("libc", EntryPoint = "timerfd_create", SetLastError = true)]
        public static extern int TimerFdCreate(int clockId, int flags);

        [DllImport("libc", EntryPoint = "timerfd_settime", SetLastError = true)]
        public static extern int TimerFdSetTime(int fd, int flags, ref ITimerSpec newValue, IntPtr oldValue);
    }
}
