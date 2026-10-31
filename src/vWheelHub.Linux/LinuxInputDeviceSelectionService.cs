using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Linux;

/// <summary>Explicit, machine-wide device selection shared by preparation and runtime.</summary>
public sealed class LinuxInputDeviceSelectionService : IInputDeviceSelectionService
{
    private readonly string _settingsPath;
    private readonly Func<AppSettings, DeviceResolutionResult> _discover;
    private readonly Func<InputDeviceKind, InputDeviceOption, IInputDeviceActivityProbe> _watch;

    public LinuxInputDeviceSelectionService(string? settingsPath = null,
        Func<AppSettings, DeviceResolutionResult>? discover = null,
        Func<InputDeviceKind, InputDeviceOption, IInputDeviceActivityProbe>? watch = null)
    {
        _settingsPath = settingsPath ?? StoragePaths.AppSettingsPath;
        _discover = discover ?? (settings => LinuxInputDeviceResolver.ResolveDevices(settings));
        _watch = watch ?? ((kind, device) => new Probe(kind, device));
    }

    public InputDeviceSelectionSnapshot Discover()
    {
        var settings = AppSettings.Load(_settingsPath);
        var devices = _discover(settings);
        InputDeviceOption Option(DiscoveredDevice d) => new(d.Path, d.Name, d.Identity);
        return new(devices.AvailableMice.Select(Option).ToArray(), devices.AvailableKeyboards.Select(Option).ToArray(),
            settings.LinuxSelectedMousePath ?? devices.MousePath, settings.LinuxSelectedKeyboardPath ?? devices.KeyboardPath);
    }

    public IInputDeviceActivityProbe Watch(InputDeviceKind kind, InputDeviceOption device) => _watch(kind, device);

    public void Save(InputDeviceOption mouse, InputDeviceOption keyboard)
    {
        // Recheck identities: an event number may have been reused while the page was open.
        var fresh = Discover();
        if (!fresh.Mice.Contains(mouse) || !fresh.Keyboards.Contains(keyboard))
            throw new IOException("Input device selection changed or disconnected. Refresh devices and select them again.");
        using var mouseProbe = Watch(InputDeviceKind.Mouse, mouse);
        using var keyboardProbe = Watch(InputDeviceKind.Keyboard, keyboard);
        if (!mouseProbe.Read().IsConnected || !keyboardProbe.Read().IsConnected)
            throw new IOException("Selected input devices are unavailable.");
        var settings = AppSettings.Load(_settingsPath);
        settings.LinuxSelectedMousePath = mouse.Path;
        settings.LinuxSelectedKeyboardPath = keyboard.Path;
        settings.LinuxSelectedMouseIdentity = mouse.Identity;
        settings.LinuxSelectedKeyboardIdentity = keyboard.Identity;
        settings.Save(_settingsPath);
        var saved = AppSettings.Load(_settingsPath);
        if (saved.LinuxSelectedMousePath != mouse.Path || saved.LinuxSelectedKeyboardPath != keyboard.Path ||
            saved.LinuxSelectedMouseIdentity != mouse.Identity || saved.LinuxSelectedKeyboardIdentity != keyboard.Identity)
            throw new IOException("Could not save the selected input devices.");
    }

    private sealed class Probe : IInputDeviceActivityProbe
    {
        private readonly EvdevInputSource _source;
        private readonly InputDeviceKind _kind;
        public Probe(InputDeviceKind kind, InputDeviceOption device)
        {
            _kind = kind;
            _source = kind == InputDeviceKind.Mouse
                ? new EvdevInputSource(device.Path, "")
                : new EvdevInputSource("", device.Path, keyboardOnly: true);
            try { _source.Start(); }
            catch { _source.Dispose(); throw; }
        }
        public InputDeviceActivity Read()
        {
            _source.ConsumeDeltaX(); // This reader observes activity, never steering movement.
            return new(_source.IsRunning, _source.MovementVersion,
                _kind == InputDeviceKind.Keyboard ? _source.KeyboardPressVersion : _source.PressVersion);
        }
        public void Dispose() => _source.Dispose();
    }
}
