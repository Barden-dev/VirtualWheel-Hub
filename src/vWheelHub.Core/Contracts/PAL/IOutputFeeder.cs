using System;

namespace SimRacingHub.Core
{
    public interface IOutputFeeder : IDisposable
    {
        string Name { get; }
        int ConsecutiveFailures { get; }
        Exception? LastError { get; }
        bool IsConnected { get; }
        bool Acquire();
        void Release();
        bool TryReacquire();
        bool UpdateAxesAndButtons(int steer, int gas, int brake, bool shiftDown, bool shiftUp);
        bool UpdateReport(OutputReport report) => report.Buttons.High == 0 && (report.Buttons.Low & ~3UL) == 0 &&
            UpdateAxesAndButtons(report.Steer, report.Gas, report.Brake, report.Buttons.IsDown(1), report.Buttons.IsDown(2));
        OutputTestCapabilities TestCapabilities => new(2);
        bool UpdateTestReport(OutputTestReport report) => report.Button <= 2 &&
            UpdateAxesAndButtons(report.Steer, report.Gas, report.Brake, report.Button == 1, report.Button == 2);
    }
}
