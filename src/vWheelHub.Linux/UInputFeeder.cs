using System;
using System.IO;
using System.Runtime.InteropServices;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public sealed class UInputFeeder : IOutputFeeder
    {
        private const string UInputPath = "/dev/uinput";
        private readonly OutputDeviceMode _mode;
        private readonly object _lock = new object();
        private int _fd = -1;
        private bool _disposed;
        private OutputButtons _lastCustomButtons;

        public UInputFeeder(OutputDeviceMode mode = OutputDeviceMode.VJoy)
        {
            _mode = mode;
        }

        public string Name => _mode == OutputDeviceMode.Xbox360
            ? "Linux uinput Feeder (Xbox 360)"
            : "Linux uinput Feeder (Virtual Wheel)";

        public int ConsecutiveFailures { get; private set; }
        public Exception? LastError { get; private set; }
        public bool IsConnected => _fd >= 0;

        public bool Acquire()
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(UInputFeeder));
                if (_fd >= 0) return true;

                try
                {
                    _fd = LinuxNative.Open(UInputPath, LinuxNative.O_WRONLY | LinuxNative.O_NONBLOCK);
                    if (_fd < 0)
                    {
                        int errno = Marshal.GetLastWin32Error();
                        throw new IOException($"Failed to open {UInputPath}: errno {errno}. Ensure udev rule is installed.");
                    }

                    if (_mode == OutputDeviceMode.Xbox360)
                    {
                        ConfigureXbox360Device(_fd);
                    }
                    else
                    {
                        ConfigureWheelDevice(_fd);
                    }

                    int createRet = LinuxNative.Ioctl(_fd, LinuxNative.UI_DEV_CREATE, IntPtr.Zero);
                    if (createRet < 0)
                    {
                        int errno = Marshal.GetLastWin32Error();
                        throw new IOException($"UI_DEV_CREATE failed: errno {errno}");
                    }

                    ConsecutiveFailures = 0;
                    LastError = null;
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex;
                    ConsecutiveFailures++;
                    if (_fd >= 0)
                    {
                        LinuxNative.Close(_fd);
                        _fd = -1;
                    }
                    return false;
                }
            }
        }

        public void Release()
        {
            lock (_lock)
            {
                if (_fd >= 0)
                {
                    LinuxNative.Ioctl(_fd, LinuxNative.UI_DEV_DESTROY, IntPtr.Zero);
                    LinuxNative.Close(_fd);
                    _fd = -1;
                }
            }
        }

        public bool TryReacquire()
        {
            Release();
            return Acquire();
        }

        public OutputTestCapabilities TestCapabilities => new(_mode == OutputDeviceMode.Xbox360 ? 8 : 42);
        private static readonly ushort[] XboxTestButtons = { LinuxNative.BTN_TL, LinuxNative.BTN_TR,
            LinuxNative.BTN_SOUTH, LinuxNative.BTN_EAST, LinuxNative.BTN_NORTH, LinuxNative.BTN_WEST,
            LinuxNative.BTN_SELECT, LinuxNative.BTN_START };

        public bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp)
            => SendReport(steer, gas, brake, shiftDown, shiftUp, 0, false);

        public bool UpdateTestReport(OutputTestReport report)
            => SendReport(report.Steer, report.Gas, report.Brake, report.Button == 1, report.Button == 2,
                report.Button, true);

        public bool UpdateReport(OutputReport report) => SendReport(report.Steer, report.Gas, report.Brake,
            report.Buttons.IsDown(1), report.Buttons.IsDown(2), 0, false, report.Buttons);

        private unsafe bool SendReport(int steer, int gas, int brake, bool shiftDown, bool shiftUp, int button, bool test, OutputButtons buttons = default)
        {
            int fd = _fd;
            if (fd < 0) return false;

            Span<InputEvent> packetBuffer = stackalloc InputEvent[48];
            int count = 0;

            if (_mode == OutputDeviceMode.Xbox360)
            {
                // Xbox 360: steer [0..32768] -> [-32768..32767], gas/brake [0..32768] -> [0..255]
                int mappedSteer = Math.Clamp((steer - 16384) * 2, -32768, 32767);
                int mappedGas = (int)Math.Clamp(Math.Round(Math.Max(0, gas) / 32768.0 * 255.0), 0, 255);
                int mappedBrake = (int)Math.Clamp(Math.Round(Math.Max(0, brake) / 32768.0 * 255.0), 0, 255);

                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_X, Value = mappedSteer };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_RZ, Value = mappedGas };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_Z, Value = mappedBrake };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY, Code = LinuxNative.BTN_TL, Value = shiftDown ? 1 : 0 };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY, Code = LinuxNative.BTN_TR, Value = shiftUp ? 1 : 0 };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_SYN, Code = LinuxNative.SYN_REPORT, Value = 0 };
            }
            else
            {
                // Virtual Wheel: steer, gas, brake [0..32768]
                int mappedSteer = Math.Clamp(steer, 0, 32768);
                int mappedGas = Math.Clamp(Math.Max(0, gas), 0, 32768);
                int mappedBrake = Math.Clamp(Math.Max(0, brake), 0, 32768);

                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_X, Value = mappedSteer };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_Y, Value = mappedGas };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_ABS, Code = LinuxNative.ABS_RZ, Value = mappedBrake };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY, Code = LinuxNative.BTN_GEAR_DOWN, Value = shiftDown ? 1 : 0 };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY, Code = LinuxNative.BTN_GEAR_UP, Value = shiftUp ? 1 : 0 };
                packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_SYN, Code = LinuxNative.SYN_REPORT, Value = 0 };
            }

            // Extend the same atomic report before its final SYN_REPORT.
            count--;
            if (_mode == OutputDeviceMode.Xbox360)
            {
                for (int i = 0; i < XboxTestButtons.Length; i++)
                    if (i >= 2 || test) packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY,
                        Code = XboxTestButtons[i], Value = (test ? button == i + 1 : buttons.IsDown(i + 1)) ? 1 : 0 };
            }
            else
            {
                for (int i = 3; i <= 42; i++)
                {
                    bool pressed = test ? button == i : buttons.IsDown(i);
                    if (pressed != _lastCustomButtons.IsDown(i)) packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_KEY,
                        Code = (ushort)(0x2c0 + i - 3), Value = pressed ? 1 : 0 };
                }
            }
            packetBuffer[count++] = new InputEvent { Type = LinuxNative.EV_SYN, Code = LinuxNative.SYN_REPORT };

            nuint totalBytes = (nuint)(count * sizeof(InputEvent));
            var nextButtons = test ? new OutputButtons().With(button) : buttons;
            _lastCustomButtons = new(_lastCustomButtons.Low | nextButtons.Low, _lastCustomButtons.High | nextButtons.High);
            fixed (InputEvent* ptr = packetBuffer)
            {
                nint written = LinuxNative.Write(fd, (IntPtr)ptr, totalBytes);
                if (written != (nint)totalBytes)
                {
                    int errno = Marshal.GetLastWin32Error();
                    ConsecutiveFailures++;
                    LastError = new IOException($"Write to uinput failed: written={written}, expected={totalBytes}, errno={errno}");
                    return false;
                }
            }

            _lastCustomButtons = nextButtons;
            ConsecutiveFailures = 0;
            return true;
        }

        private static void ConfigureWheelDevice(int fd)
        {
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_KEY);
            for (int i = 0; i < 40; i++) LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, (ushort)(0x2c0 + i));
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_GEAR_DOWN);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_GEAR_UP);

            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_ABS);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_X);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_Y);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_RZ);

            SetupAbsAxis(fd, LinuxNative.ABS_X, 0, 32768, 0, 0);
            SetupAbsAxis(fd, LinuxNative.ABS_Y, 0, 32768, 0, 0);
            SetupAbsAxis(fd, LinuxNative.ABS_RZ, 0, 32768, 0, 0);

            var setup = new UInputSetup
            {
                Id = new InputId
                {
                    Bustype = LinuxNative.BUS_USB,
                    Vendor = 0x046D,  // Logitech
                    Product = 0xC29B, // G27 Compatible
                    Version = 1
                }
            };
            setup.SetName("vWheelHub Virtual Wheel");
            LinuxNative.Ioctl(fd, LinuxNative.UI_DEV_SETUP, ref setup);
        }

        private static void ConfigureXbox360Device(int fd)
        {
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_KEY);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_SOUTH);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_EAST);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_NORTH);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_WEST);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_TL);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_TR);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_SELECT);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_KEYBIT, LinuxNative.BTN_START);

            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_EVBIT, LinuxNative.EV_ABS);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_X);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_RZ);
            LinuxNative.Ioctl(fd, LinuxNative.UI_SET_ABSBIT, LinuxNative.ABS_Z);

            SetupAbsAxis(fd, LinuxNative.ABS_X, -32768, 32767, 16, 128);
            SetupAbsAxis(fd, LinuxNative.ABS_RZ, 0, 255, 0, 0);
            SetupAbsAxis(fd, LinuxNative.ABS_Z, 0, 255, 0, 0);

            var setup = new UInputSetup
            {
                Id = new InputId
                {
                    Bustype = LinuxNative.BUS_USB,
                    Vendor = 0x045E,  // Microsoft
                    Product = 0x028E, // Xbox 360 Controller
                    Version = 0x0110
                }
            };
            setup.SetName("Microsoft X-Box 360 pad");
            LinuxNative.Ioctl(fd, LinuxNative.UI_DEV_SETUP, ref setup);
        }

        private static void SetupAbsAxis(int fd, ushort code, int min, int max, int fuzz, int flat)
        {
            var absSetup = new UInputAbsSetup
            {
                Code = code,
                AbsInfo = new InputAbsInfo
                {
                    Minimum = min,
                    Maximum = max,
                    Fuzz = fuzz,
                    Flat = flat,
                    Resolution = 0,
                    Value = (min + max) / 2
                }
            };
            LinuxNative.Ioctl(fd, LinuxNative.UI_ABS_SETUP, ref absSetup);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            Release();
        }
    }
}
