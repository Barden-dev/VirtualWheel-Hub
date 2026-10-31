using System;

namespace SimRacingHub.Core
{
    /// <summary>
    /// Windows PAL implementation of <see cref="IPrecisionTimer"/> delegating to Win32 <see cref="NativeTimer"/>.
    /// </summary>
    public sealed class WindowsPrecisionTimer : IPrecisionTimer
    {
        public void BeginPeriod(uint ms = 1) => NativeTimer.BeginPeriod(ms);

        public void EndPeriod(uint ms = 1) => NativeTimer.EndPeriod(ms);

        public IPrecisionWaiter CreateWaiter() => new WindowsPrecisionWaiter();

        private sealed class WindowsPrecisionWaiter : IPrecisionWaiter
        {
            private readonly NativeTimer.HighResolutionWaiter _waiter;

            public WindowsPrecisionWaiter()
            {
                _waiter = NativeTimer.CreateHighResolutionWaiter();
            }

            public bool Wait(double seconds) => _waiter.Wait(seconds);

            public void Dispose() => _waiter.Dispose();
        }
    }
}
