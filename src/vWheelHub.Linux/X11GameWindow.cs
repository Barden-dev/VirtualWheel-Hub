using System;
using System.Runtime.InteropServices;

namespace SimRacingHub.Linux;

/// <summary>Requests a normal exit from a Proton X11 window owned by the exact game process.</summary>
internal static class X11GameWindow
{
    private static readonly object Gate = new();
    private static readonly LinuxProcessDetector Processes = new();
    private static long _focusRefreshAfter;
    private static string[]? _focusNames;
    private static bool? _focusResult;

    internal static bool? MatchesForeground(string[] processNames)
    {
        // Xwayland may retain its last active window while a native Wayland app owns focus.
        if (!OperatingSystem.IsLinux() ||
            string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return null;
        lock (Gate)
        {
            if (ReferenceEquals(processNames, _focusNames) && Environment.TickCount64 < _focusRefreshAfter) return _focusResult;
            _focusNames = processNames;
            _focusRefreshAfter = Environment.TickCount64 + 20;
            return _focusResult = ReadForeground(processNames);
        }
    }

    private static bool? ReadForeground(string[] names)
    {
        IntPtr display = IntPtr.Zero, previous = IntPtr.Zero;
        ErrorHandler? handler = null;
        try
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return null;
            handler = (connection, error) => connection == display ? 0 : ForwardError(previous, connection, error);
            previous = XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(handler));
            var active = ReadProperty(display, XDefaultRootWindow(display), XInternAtom(display, "_NET_ACTIVE_WINDOW", true));
            if (active.Length != 1 || active[0] == 0) return null;
            var owners = ReadProperty(display, active[0], XInternAtom(display, "_NET_WM_PID", true));
            if (owners.Length != 1 || owners[0] == 0 || owners[0] > int.MaxValue) return null;
            int pid = (int)owners[0];
            foreach (string name in names) if (Processes.IsProcess(pid, name)) return true;
            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return null; }
        finally
        {
            if (display != IntPtr.Zero) { XSync(display, false); XCloseDisplay(display); }
            if (handler != null) XSetErrorHandler(previous);
            GC.KeepAlive(handler);
        }
    }

    internal static bool RequestClose(int processId)
    {
        lock (Gate) return RequestCloseCore(processId);
    }

    private static bool RequestCloseCore(int processId)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return false;
        IntPtr display = IntPtr.Zero;
        IntPtr previousHandler = IntPtr.Zero;
        ErrorHandler? handler = null;
        try
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return false;
            // Windows can disappear between enumerating and reading their PID.
            // Ignore errors only on this connection; retain the host's handler for all others.
            handler = (connection, error) => connection == display ? 0 : ForwardError(previousHandler, connection, error);
            previousHandler = XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(handler));
            var windows = ReadProperty(display, XDefaultRootWindow(display), XInternAtom(display, "_NET_CLIENT_LIST", true));
            nuint pidAtom = XInternAtom(display, "_NET_WM_PID", true);
            bool sent = false;
            foreach (nuint window in windows)
            {
                var pid = ReadProperty(display, window, pidAtom);
                if (pid.Length != 1 || pid[0] != (nuint)processId) continue;
                var message = new XEvent();
                message.Client.Type = 33; // ClientMessage
                message.Client.SendEvent = 1;
                message.Client.Display = display;
                message.Client.Window = window;
                message.Client.MessageType = XInternAtom(display, "WM_PROTOCOLS", false);
                message.Client.Format = 32;
                message.Client.Data0 = (nint)XInternAtom(display, "WM_DELETE_WINDOW", false);
                sent |= XSendEvent(display, window, false, 0, ref message) != 0;
            }
            XFlush(display);
            return sent;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return false; }
        finally
        {
            if (display != IntPtr.Zero)
            {
                XSync(display, false);
                XCloseDisplay(display);
            }
            if (handler != null) XSetErrorHandler(previousHandler);
            GC.KeepAlive(handler);
        }
    }

    private static nuint[] ReadProperty(IntPtr display, nuint window, nuint atom)
    {
        if (atom == 0) return Array.Empty<nuint>();
        IntPtr data = IntPtr.Zero;
        try
        {
            int result = XGetWindowProperty(display, window, atom, 0, 4096, false, 0,
                out _, out int format, out nuint count, out _, out data);
            if (result != 0 || format != 32 || count > 4096 || data == IntPtr.Zero) return Array.Empty<nuint>();
            var values = new nuint[(int)count];
            for (int i = 0; i < values.Length; i++) values[i] = (nuint)Marshal.ReadIntPtr(data, i * IntPtr.Size);
            return values;
        }
        finally { if (data != IntPtr.Zero) XFree(data); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClientMessage
    {
        public int Type;
        public nuint Serial;
        public int SendEvent;
        public IntPtr Display;
        public nuint Window, MessageType;
        public int Format;
        public nint Data0, Data1, Data2, Data3, Data4;
    }
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent { [FieldOffset(0)] public ClientMessage Client; }
    private static unsafe int ForwardError(IntPtr handler, IntPtr display, IntPtr error) => handler == IntPtr.Zero
        ? 0 : ((delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>)handler)(display, error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ErrorHandler(IntPtr display, IntPtr error);
    [DllImport("libX11.so.6")] private static extern IntPtr XSetErrorHandler(IntPtr handler);
    [DllImport("libX11.so.6")] private static extern int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern nuint XInternAtom(IntPtr display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);
    [DllImport("libX11.so.6")] private static extern int XGetWindowProperty(IntPtr display, nuint window, nuint property,
        nint offset, nint length, [MarshalAs(UnmanagedType.Bool)] bool delete, nuint type,
        out nuint actualType, out int format, out nuint count, out nuint remaining, out IntPtr data);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(IntPtr display, nuint window,
        [MarshalAs(UnmanagedType.Bool)] bool propagate, nint mask, ref XEvent ev);
    [DllImport("libX11.so.6")] private static extern int XFlush(IntPtr display);
    [DllImport("libX11.so.6")] private static extern int XFree(IntPtr data);
}
