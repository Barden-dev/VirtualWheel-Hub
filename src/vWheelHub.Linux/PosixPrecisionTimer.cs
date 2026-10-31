using System;
using System.Runtime.InteropServices;
using SimRacingHub.Core;
using SimRacingHub.Linux.Native;

namespace SimRacingHub.Linux
{
    public sealed class PosixPrecisionTimer : IPrecisionTimer
    {
        private int _periodRefCount;
        private readonly object _lock = new object();

        public void BeginPeriod(uint ms = 1)
        {
            lock (_lock)
            {
                _periodRefCount++;
            }
        }

        public void EndPeriod(uint ms = 1)
        {
            lock (_lock)
            {
                if (_periodRefCount > 0)
                {
                    _periodRefCount--;
                }
            }
        }

        public IPrecisionWaiter CreateWaiter()
        {
            return new PosixPrecisionWaiter();
        }

        private sealed class PosixPrecisionWaiter : IPrecisionWaiter
        {
            private int _timerFd = -1;
            private bool _disposed;

            public PosixPrecisionWaiter()
            {
                try
                {
                    _timerFd = LinuxNative.TimerFdCreate(LinuxNative.CLOCK_MONOTONIC, 0);
                }
                catch
                {
                    _timerFd = -1;
                }
            }

            public bool Wait(double seconds)
            {
                if (seconds <= 0.0) return false;

                long totalNanoseconds = (long)(seconds * 1_000_000_000.0);
                if (totalNanoseconds <= 0) return false;

                if (_timerFd >= 0)
                {
                    var spec = new ITimerSpec
                    {
                        Interval = new TimeSpec { TvSec = 0, TvNsec = 0 },
                        Value = new TimeSpec
                        {
                            TvSec = totalNanoseconds / 1_000_000_000L,
                            TvNsec = totalNanoseconds % 1_000_000_000L
                        }
                    };

                    int setRet = LinuxNative.TimerFdSetTime(_timerFd, 0, ref spec, IntPtr.Zero);
                    if (setRet == 0)
                    {
                        ulong expirations = 0;
                        unsafe
                        {
                            nint readBytes = LinuxNative.Read(_timerFd, (IntPtr)(&expirations), 8);
                            if (readBytes == 8) return true;
                        }
                    }
                }

                // Fallback: clock_nanosleep
                try
                {
                    var req = new TimeSpec
                    {
                        TvSec = totalNanoseconds / 1_000_000_000L,
                        TvNsec = totalNanoseconds % 1_000_000_000L
                    };
                    int sleepRet = LinuxNative.ClockNanosleep(LinuxNative.CLOCK_MONOTONIC, 0, ref req, IntPtr.Zero);
                    return sleepRet == 0;
                }
                catch
                {
                    return false;
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_timerFd >= 0)
                {
                    LinuxNative.Close(_timerFd);
                    _timerFd = -1;
                }
            }
        }
    }
}
