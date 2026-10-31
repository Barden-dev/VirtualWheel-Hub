using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Input;

/// <summary>Rebinds port-dependent HID paths only when the connected match is unique.</summary>
internal static class WindowsDevicePathResolver
{
    public static string? Resolve(string? saved, InputDeviceKind kind, IReadOnlyList<WindowsRawDevice> devices)
    {
        if (string.IsNullOrWhiteSpace(saved)) return saved;
        var candidates = devices.Where(d => d.Kind == kind).Select(d => d.Option.Path).ToArray();
        var exact = candidates.FirstOrDefault(p => string.Equals(p, saved, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        string? identity = Identity(saved);
        if (identity == null) return saved;
        var matches = candidates.Where(p => string.Equals(Identity(p), identity, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : saved;
    }

    private static string? Identity(string path)
    {
        var parts = path.Split('#');
        if (parts.Length != 4 || !parts[0].Equals(@"\\?\HID", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Contains("VID_", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Contains("PID_", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[2]) || !Guid.TryParse(parts[3], out _)) return null;
        // Windows-generated instance IDs contain '&' and can change with the USB port.
        // Keep a serial-based instance in the identity so another serial cannot replace it.
        return parts[0] + "#" + parts[1] + "#" + (parts[2].Contains('&') ? "" : parts[2]) + "#" + parts[3];
    }

    public static bool Restore(AppSettings settings, IReadOnlyList<WindowsRawDevice> devices)
    {
        var mouse = Resolve(settings.WindowsSelectedMousePath, InputDeviceKind.Mouse, devices);
        var keyboard = Resolve(settings.WindowsSelectedKeyboardPath, InputDeviceKind.Keyboard, devices);
        bool changed = mouse != settings.WindowsSelectedMousePath || keyboard != settings.WindowsSelectedKeyboardPath;
        settings.WindowsSelectedMousePath = mouse;
        settings.WindowsSelectedKeyboardPath = keyboard;
        return changed;
    }

    public static void EnsureConnected(AppSettings settings, IReadOnlyList<WindowsRawDevice> devices)
    {
        bool Missing(string? path, InputDeviceKind kind) => !string.IsNullOrWhiteSpace(path) &&
            !devices.Any(d => d.Kind == kind && string.Equals(d.Option.Path, path, StringComparison.OrdinalIgnoreCase));
        bool mouse = Missing(settings.WindowsSelectedMousePath, InputDeviceKind.Mouse);
        bool keyboard = Missing(settings.WindowsSelectedKeyboardPath, InputDeviceKind.Keyboard);
        if (mouse || keyboard)
            throw new IOException($"Selected {(mouse && keyboard ? "mouse and keyboard are" : mouse ? "mouse is" : "keyboard is")} disconnected or cannot be identified uniquely. Select {(mouse && keyboard ? "them" : "it")} in Controls > Input devices.");
    }
}
