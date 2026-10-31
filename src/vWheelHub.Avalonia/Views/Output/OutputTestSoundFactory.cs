using SimRacingHub.Application.Runtime;
using SimRacingHub.Windows;

namespace SimRacingHub.Avalonia.Views.Output;

internal static class OutputTestSoundFactory
{
    public static IOutputTestSound? Create() => System.OperatingSystem.IsWindows() ? new WindowsAssignmentSound() : null;
}
