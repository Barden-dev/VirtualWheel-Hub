using System;
using System.IO;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux;

/// <summary>Preserves desktop clicks and scrolling while evdev captures physical motion.</summary>
internal sealed class UInputPointerRelay : IDisposable
{
    internal const string DeviceName = "vWheelHub cursor buttons";
    private int _fd = -1;
    private readonly bool[] _buttons = new bool[8];

    public unsafe UInputPointerRelay()
    {
        _fd = LinuxNative.Open("/dev/uinput", LinuxNative.O_WRONLY | LinuxNative.O_NONBLOCK);
        if (_fd < 0) throw new IOException("Cannot open /dev/uinput for cursor buttons.");
        try
        {
            Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_KEY));
            Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_REL));
            for (int code = 272; code < 280; code++)
                Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_SET_KEYBIT, code));
            // X/Y capabilities make this a desktop pointer; movement is never forwarded.
            foreach (int code in new[] { 0, 1, 6, 8, 11, 12 })
                Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_SET_RELBIT, code));
            var setup = new UInputSetup { Id = new InputId { Bustype = LinuxNative.BUS_VIRTUAL, Vendor = 0x1209, Product = 0x0003, Version = 1 } };
            setup.SetName(DeviceName);
            Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_DEV_SETUP, (IntPtr)(&setup)));
            Check(LinuxNative.Ioctl(_fd, LinuxNative.UI_DEV_CREATE, IntPtr.Zero));
        }
        catch { Dispose(); throw; }
    }

    internal static bool ShouldForward(InputEvent ev) =>
        (ev.Type == LinuxNative.EV_KEY && ev.Code >= 272 && ev.Code < 280 && ev.Value is 0 or 1) ||
        (ev.Type == LinuxNative.EV_REL && ev.Code is 6 or 8 or 11 or 12);

    public void Forward(InputEvent ev)
    {
        if (ev.Type == LinuxNative.EV_SYN && ev.Code == LinuxNative.SYN_DROPPED)
        {
            ReleaseButtons();
            return;
        }
        if (!ShouldForward(ev)) return;
        Send(ev);
        if (ev.Type == LinuxNative.EV_KEY) _buttons[ev.Code - 272] = ev.Value != 0;
    }

    private unsafe void Send(InputEvent ev)
    {
        InputEvent* report = stackalloc InputEvent[2];
        report[0] = ev;
        report[1] = new InputEvent { Type = LinuxNative.EV_SYN, Code = LinuxNative.SYN_REPORT };
        if (LinuxNative.Write(_fd, (IntPtr)report, (nuint)(2 * sizeof(InputEvent))) != 2 * sizeof(InputEvent))
            throw new IOException("Cannot forward cursor buttons.");
    }

    public void ReleaseButtons()
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (!_buttons[i]) continue;
            Send(new InputEvent { Type = LinuxNative.EV_KEY, Code = (ushort)(272 + i), Value = 0 });
            _buttons[i] = false;
        }
    }

    public void Dispose()
    {
        if (_fd < 0) return;
        // Destroying the device also releases held desktop buttons on every failure path.
        LinuxNative.Ioctl(_fd, LinuxNative.UI_DEV_DESTROY, IntPtr.Zero);
        LinuxNative.Close(_fd);
        _fd = -1;
    }

    private static void Check(int result)
    {
        if (result < 0) throw new IOException("Cannot create cursor button relay.");
    }
}
