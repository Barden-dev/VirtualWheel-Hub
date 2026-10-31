namespace SimRacingHub.Core;

/// <summary>Platform host hook; null means that foreground ownership cannot be verified.</summary>
public static class GameWindowFocus
{
    public static Func<string[], bool?>? PlatformProbe { get; set; }
}
