using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SimRacingHub.Core
{
    public class VJoyWrapper : IDisposable
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

        public VJoyWrapper(uint id = 1)
        {
            _id = id;
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
                return VJD_STAT_MISS;
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

        private int _consecutiveFailures;
        private volatile Exception? _lastError;

        public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

        public bool IsHealthy => Volatile.Read(ref _consecutiveFailures) == 0;

        public Exception? LastError => _lastError;

        public bool Acquire()
        {
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

        public bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp)
        {
            bool ok;

            try
            {
                ok = SetAxis(steer, _id, HID_USAGE_X);
                ok &= SetAxis(gas, _id, HID_USAGE_Y);
                ok &= SetAxis(brake, _id, HID_USAGE_Z);
                ok &= SetBtn(shiftDown, _id, 1);
                ok &= SetBtn(shiftUp, _id, 2);
            }
            catch (Exception ex)
            {
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
