using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimRacingHub.Services.Games;

internal static class GameFocus
{
    [ThreadStatic] private static uint _lastProcessId;
    [ThreadStatic] private static string? _lastName;
    [ThreadStatic] private static long _refreshAfter;
    internal static bool? Matches(string[] processNames)
    {
        if (!OperatingSystem.IsWindows()) return SimRacingHub.Core.GameWindowFocus.PlatformProbe?.Invoke(processNames);
        try
        {
            nint window = GetForegroundWindow();
            if (window == 0 || GetWindowThreadProcessId(window, out uint id) == 0) return null;
            if (id != _lastProcessId || Environment.TickCount64 >= _refreshAfter)
            {
                using var process = Process.GetProcessById(checked((int)id));
                _lastName = process.ProcessName;
                _lastProcessId = id;
                _refreshAfter = Environment.TickCount64 + 1000;
            }
            return processNames.Contains(_lastName, StringComparer.OrdinalIgnoreCase);
        }
        catch { return null; }
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
