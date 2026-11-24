using SimRacingHub.Application.Runtime;
using SimRacingHub.Windows;

namespace SimRacingHub.Avalonia.Views.Output;

internal static partial class OutputTestSoundFactory
{
    public static IOutputTestSound? Create()
    {
        if (System.OperatingSystem.IsWindows()) return new WindowsAssignmentSound();
        IOutputTestSound? sound = null;
        CreateNonWindows(ref sound);
        return sound;
    }

    static partial void CreateNonWindows(ref IOutputTestSound? sound);
}
