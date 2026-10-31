using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SimRacingHub.Core
{
    public class VJoyWrapper : IOutputFeeder
    {
        private uint _id;

        [DllImport("vJoyInterface.dll", EntryPoint = "vJoyEnabled", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool vJoyEnabledNative();

        [DllImport("vJoyInterface.dll", EntryPoint = "GetvJoyVersion", CallingConvention = CallingConvention.Cdecl)]
        private static extern short GetvJoyVersionNative();

        [DllImport("vJoyInterface.dll", EntryPoint = "AcquireVJD", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool AcquireVJD(uint rID);

        [DllImport("vJoyInterface.dll", EntryPoint = "RelinquishVJD", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RelinquishVJD(uint rID);

        [DllImport("vJoyInterface.dll", EntryPoint = "SetAxis", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetAxis(int Value, uint rID, uint Axis);

        [DllImport("vJoyInterface.dll", EntryPoint = "SetBtn", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetBtn(bool Value, uint rID, byte vBtn);

        [DllImport("vJoyInterface.dll", EntryPoint = "GetVJDStatus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDStatus(uint rID);

        [DllImport("vJoyInterface.dll", EntryPoint = "GetVJDButtonNumber", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDButtonNumber(uint rID);

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDDiscPovNumber(uint rID);
        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVJDContPovNumber(uint rID);
        public static int GetDiscretePovCount(uint id)
        {
            try { return GetVJDDiscPovNumber(id); } catch { return -1; }
        }
        public static int GetContinuousPovCount(uint id)
        {
            try { return GetVJDContPovNumber(id); } catch { return -1; }
        }

        [DllImport("vJoyInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsDeviceFfbEffect(uint rID, uint effect);

        public static bool HasWheelCapabilities(uint id)
        {
            try
            {
                return MeetsWheelCapabilities(GetVJDDiscPovNumber(id), effect => IsDeviceFfbEffect(id, effect));
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return false;
            }
        }

        internal static bool MeetsWheelCapabilities(int discretePovCount, Func<uint, bool> hasEffect)
        {
            uint[] effects = { 0x26, 0x27, 0x30, 0x31, 0x32, 0x33, 0x34, 0x40, 0x41, 0x42, 0x43 };
            return discretePovCount >= 1 && Array.TrueForAll(effects, effect => hasEffect(effect));
        }

        [DllImport("vJoyInterface.dll", EntryPoint = "GetVJDAxisExist", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetVJDAxisExist(uint rID, uint Axis);

        // VJD_STAT
        public const int VJD_STAT_OWN = 0;   // Owned by this application
        public const int VJD_STAT_FREE = 1;  // Free to acquire
        public const int VJD_STAT_BUSY = 2;  // Owned by another application
        public const int VJD_STAT_MISS = 3;  // Device is missing / unconfigured
        public const int VJD_STAT_UNKN = 4;  // Unknown status

        // Standard HID Usages for axes
        public const uint HID_USAGE_X = 0x30;   // Steering
        public const uint HID_USAGE_Y = 0x31;   // Gas / Throttle
        public const uint HID_USAGE_Z = 0x32;   // Brake
        public const uint HID_USAGE_RX = 0x33;  // Rx
        public const uint HID_USAGE_RY = 0x34;  // Ry
        public const uint HID_USAGE_RZ = 0x35;  // Rz / Clutch
        public const uint HID_USAGE_SL0 = 0x36; // Slider 0 / Handbrake
        public const uint HID_USAGE_SL1 = 0x37; // Slider 1

        private readonly Func<string?>? _validateConfiguration;
        public VJoyWrapper(uint id = 1, Func<string?>? validateConfiguration = null)
        {
            _id = id;
            _validateConfiguration = validateConfiguration;
        }

        public static bool IsDriverEnabled()
        {
            try
            {
                return vJoyEnabledNative();
            }
            catch
            {
                return false;
            }
        }

        public static short GetDriverVersion()
        {
            try
            {
                return GetvJoyVersionNative();
            }
            catch
            {
                return 0;
            }
        }

        public static int GetDeviceStatus(uint rID)
        {
            try
            {
                return GetVJDStatus(rID);
            }
            catch
            {
                return VJD_STAT_UNKN;
            }
        }

        public static bool CheckAxisExists(uint rID, uint axis)
        {
            try
            {
                return GetVJDAxisExist(rID, axis);
            }
            catch
            {
                return false;
            }
        }

        public static int GetButtonCount(uint rID)
        {
            try
            {
                return GetVJDButtonNumber(rID);
            }
            catch
            {
                return 0;
            }
        }

        private string CleanupKey => $"vjoy-device-{_id}";

        public OutputTestCapabilities TestCapabilities => new(Math.Clamp(GetButtonCount(_id), 0, 128));

        public bool UpdateTestReport(OutputTestReport report)
        {
            try
            {
                bool ok = SetAxis(report.Steer, _id, HID_USAGE_X);
                ok &= SetAxis(report.Gas, _id, HID_USAGE_Y);
                ok &= SetAxis(report.Brake, _id, HID_USAGE_Z);
                int buttons = Math.Clamp(GetButtonCount(_id), 0, 128);
                for (int i = 1; i <= buttons; i++) ok &= SetBtn(report.Button == i, _id, (byte)i);
                if (ok) { _lastButtons = new OutputButtons().With(report.Button); Volatile.Write(ref _consecutiveFailures, 0); }
                else Interlocked.Increment(ref _consecutiveFailures);
                return ok;
            }
            catch (Exception ex)
            {
                var attempted = new OutputButtons().With(report.Button);
                _lastButtons = new(_lastButtons.Low | attempted.Low, _lastButtons.High | attempted.High);
                _lastError = ex; Interlocked.Increment(ref _consecutiveFailures); return false;
            }
        }

        public string Name => $"vJoy Device #{_id}";

        private int _consecutiveFailures;
        private volatile Exception? _lastError;

        public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

        public bool IsHealthy => Volatile.Read(ref _consecutiveFailures) == 0;

        public Exception? LastError => _lastError;

        public bool IsConnected
        {
            get
            {
                int status = GetDeviceStatus(_id);
                return status == VJD_STAT_OWN;
            }
        }

        public bool Acquire()
        {
            if (_validateConfiguration?.Invoke() is { } reason)
            {
                _lastError = new InvalidOperationException(reason);
                return false;
            }
            int status = GetDeviceStatus(_id);
            if (status == VJD_STAT_FREE || status == VJD_STAT_OWN)
            {
                if (!AcquireVJD(_id)) return false;

                CrashSafety.Register(CleanupKey, Release);
                Volatile.Write(ref _consecutiveFailures, 0);
                _lastError = null;
                return true;
            }
            return false;
        }

        public void Release()
        {
            CrashSafety.Unregister(CleanupKey);

            try
            {
                if (GetDeviceStatus(_id) == VJD_STAT_OWN) UpdateTestReport(OutputTestReport.Neutral);
                RelinquishVJD(_id);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Could not release vJoy device {_id}", ex);
            }
        }

        public bool TryReacquire()
        {
            Release();

            if (Acquire()) return true;

            AppLogger.Instance.LogWarning($"Could not re-acquire vJoy device {_id} (status: {DescribeStatus(GetDeviceStatus(_id))})");
            return false;
        }

        private OutputButtons _lastButtons;

        public bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp) =>
            UpdateReport(new(steer, gas, brake, OutputButtons.Gears(shiftDown, shiftUp)));

        public bool UpdateReport(OutputReport report)
        {
            bool ok;

            try
            {
                ok = SetAxis(report.Steer, _id, HID_USAGE_X);
                ok &= SetAxis(report.Gas, _id, HID_USAGE_Y);
                ok &= SetAxis(report.Brake, _id, HID_USAGE_Z);
                ok &= SetBtn(report.Buttons.IsDown(1), _id, 1);
                ok &= SetBtn(report.Buttons.IsDown(2), _id, 2);
                for (int button = 3; button <= 128; button++)
                    if (_lastButtons.IsDown(button) != report.Buttons.IsDown(button))
                        ok &= SetBtn(report.Buttons.IsDown(button), _id, (byte)button);
                _lastButtons = ok ? report.Buttons : new(_lastButtons.Low | report.Buttons.Low, _lastButtons.High | report.Buttons.High);
            }
            catch (Exception ex)
            {
                _lastButtons = new(_lastButtons.Low | report.Buttons.Low, _lastButtons.High | report.Buttons.High);
                _lastError = ex;
                ok = false;
            }

            if (ok) Volatile.Write(ref _consecutiveFailures, 0);
            else Interlocked.Increment(ref _consecutiveFailures);

            return ok;
        }

        public static string DescribeStatus(int status) => status switch
        {
            VJD_STAT_OWN => "owned by this application",
            VJD_STAT_FREE => "free",
            VJD_STAT_BUSY => "owned by another application",
            VJD_STAT_MISS => "missing or not configured",
            _ => "unknown"
        };

        public void Dispose()
        {
            Release();
        }
    }
}
