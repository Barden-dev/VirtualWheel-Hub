using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Input;

internal readonly record struct WindowsRawDevice(IntPtr Handle, InputDeviceKind Kind, InputDeviceOption Option);
internal readonly record struct WindowsRawPacket(string Path, InputDeviceKind Kind, int X = 0, int Y = 0,
    int Key = 0, bool Down = false, bool Removed = false, bool Reset = false);

internal static class WindowsRawDevices
{
    // Windows supplies Precision Touchpad mouse packets with hDevice == 0.
    // Keep that logical channel separate from every physical mouse.
    internal const string PrecisionTouchpadPath = "windows:precision-touchpad";

    public static WindowsRawDevice[] Discover()
    {
        uint count = 0;
        uint size = (uint)Marshal.SizeOf<Device>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) throw new Win32Exception();
        // A device can arrive between the count and list calls.
        for (int retry = 0; retry < 3; retry++)
        {
            var list = new Device[count];
            uint read = GetRawInputDeviceList(list, ref count, size);
            if (read == uint.MaxValue) continue;
            return list.Take((int)read).Select(d =>
            {
                if (d.Type == 2)
                {
                    uint infoSize = (uint)Marshal.SizeOf<DeviceInfo>();
                    var info = new DeviceInfo { Size = infoSize };
                    if (GetRawInputDeviceInfo(d.Handle, 0x2000000B, ref info, ref infoSize) == uint.MaxValue)
                        return (WindowsRawDevice?)null;
                    return DescribeHid(d.Handle, info.Type, (ushort)info.Usage, (ushort)(info.Usage >> 16));
                }
                if (d.Type > 1) return null;
                string path = Path(d.Handle);
                var kind = d.Type == 0 ? InputDeviceKind.Mouse : InputDeviceKind.Keyboard;
                return new WindowsRawDevice(d.Handle, kind, new(path, Name(path, kind), path));
            }).Where(d => d.HasValue && d.Value.Option.Path.Length > 0).Select(d => d!.Value).ToArray();
        }
        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate input devices.");
    }

    internal static WindowsRawDevice? DescribeHid(IntPtr handle, uint type, ushort usagePage, ushort usage)
        => type == 2 && usagePage == 0x0D && usage == 0x05
            ? new(handle, InputDeviceKind.Mouse,
                new(PrecisionTouchpadPath, "Windows Precision Touchpad", PrecisionTouchpadPath))
            : null;

    public static string Path(IntPtr device)
    {
        uint size = 0;
        if (GetRawInputDeviceInfo(device, 0x20000007, null, ref size) == uint.MaxValue || size == 0) return "";
        var text = new StringBuilder((int)size + 1);
        return GetRawInputDeviceInfo(device, 0x20000007, text, ref size) == uint.MaxValue ? "" : text.ToString();
    }

    private static string Name(string path, InputDeviceKind kind)
    {
        try
        {
            // The interface path identifies the physical HID instance; the last component is its interface GUID.
            var parts = path.Replace("\\\\?\\", "").Split('#');
            if (parts.Length >= 3)
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + string.Join("\\", parts.Take(3)));
                string? name = key?.GetValue("FriendlyName") as string ?? key?.GetValue("DeviceDesc") as string;
                if (!string.IsNullOrWhiteSpace(name)) return name.Split(';')[^1];
            }
        }
        catch (System.Security.SecurityException) { }
        catch (UnauthorizedAccessException) { }
        return kind == InputDeviceKind.Mouse ? "Mouse" : "Keyboard";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Device { public IntPtr Handle; public uint Type; }
    // RID_DEVICE_INFO has an 8-byte header and a 24-byte union. For HID,
    // vendor/product/version precede the two USHORT usage fields.
    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo
    {
        public uint Size, Type, Vendor, Product, Version, Usage, Reserved1, Reserved2;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList([Out] Device[]? list, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder? data, ref uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, ref DeviceInfo data, ref uint size);
}

/// <summary>One registration per process: extra readers must not steal WM_INPUT from the runtime.</summary>
internal static class WindowsRawInputHub
{
    private static readonly object Gate = new();
    private static RawInputWorker? _worker;
    private static Action<WindowsRawPacket>? _readers;
    public static bool IsRunning => Volatile.Read(ref _worker)?.IsRunning == true;

    public static IDisposable Subscribe(Action<WindowsRawPacket> reader)
    {
        lock (Gate)
        {
            if (_worker?.IsRunning != true)
            {
                _worker?.Dispose();
                _worker = null;
                var worker = new RawInputWorker();
                worker.Packet += Publish;
                if (!worker.Start()) { worker.Dispose(); throw new IOException("Windows Raw Input could not start."); }
                _worker = worker;
            }
            _readers += reader;
            return new Lease(reader);
        }
    }

    private static void Publish(WindowsRawPacket packet) => Volatile.Read(ref _readers)?.Invoke(packet);
    private sealed class Lease(Action<WindowsRawPacket> reader) : IDisposable
    {
        private Action<WindowsRawPacket>? _reader = reader;
        public void Dispose()
        {
            lock (Gate)
            {
                var reader = Interlocked.Exchange(ref _reader, null);
                if (reader == null) return;
                _readers -= reader;
                if (_readers != null) return;
                _worker?.Dispose();
                _worker = null;
            }
        }
    }
}
