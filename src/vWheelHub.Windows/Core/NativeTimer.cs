using System;
using System.Runtime.InteropServices;

namespace SimRacingHub.Core
{
    public static class NativeTimer
    {
        private const uint CreateWaitableTimerHighResolution = 0x00000002;
        private const uint TimerModifyState = 0x0002;
        private const uint Synchronize = 0x00100000;
        private const uint WaitObject0 = 0x00000000;
        private const uint Infinite = 0xFFFFFFFF;

        private static int _activeCount = 0;
        private static readonly object _lock = new object();

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWaitableTimerEx(
            IntPtr timerAttributes,
            string? timerName,
            uint flags,
            uint desiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWaitableTimer(
            IntPtr timer,
            ref long dueTime,
            int period,
            IntPtr completionRoutine,
            IntPtr completionArgument,
            [MarshalAs(UnmanagedType.Bool)] bool resume);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        public sealed class HighResolutionWaiter : IDisposable
        {
            private IntPtr _timer;

            internal HighResolutionWaiter()
            {
                uint access = TimerModifyState | Synchronize;
                _timer = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, access);
                if (_timer == IntPtr.Zero)
                {
                    _timer = CreateWaitableTimerEx(IntPtr.Zero, null, 0, access);
                }
            }

            public bool Wait(double seconds)
            {
                if (_timer == IntPtr.Zero || !double.IsFinite(seconds) || seconds <= 0.0)
                {
                    return false;
                }

                long dueTime = -Math.Max(1L, (long)Math.Ceiling(seconds * 10_000_000.0));
                if (!SetWaitableTimer(_timer, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    return false;
                }

                return WaitForSingleObject(_timer, Infinite) == WaitObject0;
            }

            public void Dispose()
            {
                IntPtr timer = _timer;
                _timer = IntPtr.Zero;
                if (timer != IntPtr.Zero) CloseHandle(timer);
            }
        }

        public static HighResolutionWaiter CreateHighResolutionWaiter() => new HighResolutionWaiter();

        public static void BeginPeriod(uint ms = 1)
        {
            lock (_lock)
            {
                if (_activeCount == 0)
                {
                    try
                    {
                        TimeBeginPeriod(ms);
                        CrashSafety.Register("winmm_timer", () => TimeEndPeriod(ms));
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.LogError("Failed to call timeBeginPeriod", ex);
                    }
                }
                _activeCount++;
            }
        }

        public static void EndPeriod(uint ms = 1)
        {
            lock (_lock)
            {
                if (_activeCount > 0)
                {
                    _activeCount--;
                    if (_activeCount == 0)
                    {
                        try
                        {
                            TimeEndPeriod(ms);
                            CrashSafety.Unregister("winmm_timer");
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Instance.LogError("Failed to call timeEndPeriod", ex);
                        }
                    }
                }
            }
        }
    }
}
