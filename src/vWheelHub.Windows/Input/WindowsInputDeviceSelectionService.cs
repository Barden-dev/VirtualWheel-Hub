using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Input;

public sealed class WindowsInputDeviceSelectionService : IInputDeviceSelectionService
{
    private readonly string _settingsPath;
    private readonly Func<WindowsRawDevice[]> _discover;
    public WindowsInputDeviceSelectionService(string? settingsPath = null) : this(settingsPath, WindowsRawDevices.Discover) { }
    internal WindowsInputDeviceSelectionService(string? settingsPath, Func<WindowsRawDevice[]> discover)
    {
        _settingsPath = settingsPath ?? StoragePaths.AppSettingsPath;
        _discover = discover;
    }

    public InputDeviceSelectionSnapshot Discover()
    {
        var devices = _discover();
        var settings = AppSettings.Load(_settingsPath);
        if (WindowsDevicePathResolver.Restore(settings, devices)) settings.Save(_settingsPath);
        return new(devices.Where(d => d.Kind == InputDeviceKind.Mouse).Select(d => d.Option)
                .DistinctBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            devices.Where(d => d.Kind == InputDeviceKind.Keyboard).Select(d => d.Option).ToArray(),
            settings.WindowsSelectedMousePath, settings.WindowsSelectedKeyboardPath);
    }

    public IInputDeviceActivityProbe Watch(InputDeviceKind kind, InputDeviceOption device) => new Probe(kind, device);

    public void Save(InputDeviceOption mouse, InputDeviceOption keyboard)
    {
        var devices = Discover();
        if (!devices.Mice.Contains(mouse) || !devices.Keyboards.Contains(keyboard))
            throw new IOException("Input devices changed. Refresh and select them again.");
        var settings = AppSettings.Load(_settingsPath);
        settings.WindowsSelectedMousePath = mouse.Path;
        settings.WindowsSelectedKeyboardPath = keyboard.Path;
        settings.Save(_settingsPath);
        var saved = AppSettings.Load(_settingsPath);
        if (saved.WindowsSelectedMousePath != mouse.Path || saved.WindowsSelectedKeyboardPath != keyboard.Path)
            throw new IOException("Could not save input device selection.");
    }

    private sealed class Probe : IInputDeviceActivityProbe
    {
        private readonly string _path;
        private readonly InputDeviceKind _kind;
        private readonly IDisposable _lease;
        private long _movement, _presses;
        private volatile bool _connected = true;
        public Probe(InputDeviceKind kind, InputDeviceOption device)
        {
            _path = device.Path; _kind = kind;
            _lease = WindowsRawInputHub.Subscribe(Accept);
            try
            {
                if (!WindowsRawDevices.Discover().Any(d => d.Kind == kind && string.Equals(d.Option.Path, _path, StringComparison.OrdinalIgnoreCase)))
                { _connected = false; _lease.Dispose(); }
            }
            catch { _lease.Dispose(); throw; }
        }
        private void Accept(WindowsRawPacket packet)
        {
            if (packet.Reset) { _connected = false; return; }
            if (!string.Equals(packet.Path, _path, StringComparison.OrdinalIgnoreCase)) return;
            if (packet.Removed) { _connected = false; return; }
            if (!_connected || packet.Kind != _kind) return;
            if (packet.X != 0 || packet.Y != 0) Interlocked.Increment(ref _movement);
            if (packet.Key != 0 && packet.Down) Interlocked.Increment(ref _presses);
        }
        public InputDeviceActivity Read() => new(_connected && WindowsRawInputHub.IsRunning, Interlocked.Read(ref _movement), Interlocked.Read(ref _presses));
        public void Dispose() { _connected = false; _lease.Dispose(); }
    }
}
