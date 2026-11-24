using SimRacingHub.Application.Contracts;
using SimRacingHub.Core;

namespace SimRacingHub.Input;

public sealed class WindowsKeyCaptureSource(string? settingsPath = null) : IKeyCaptureSource
{
    public IDisposable? Listen(Action<int> keyPressed)
    {
        var settings = AppSettings.Load(settingsPath);
        if (settings.AutomaticInputEnabled) return null;
        WindowsInputSource.ConfigureSelection(settings);
        var devices = WindowsRawDevices.Discover();
        if (WindowsDevicePathResolver.Restore(settings, devices)) settings.Save(settingsPath);
        WindowsDevicePathResolver.EnsureConnected(settings, devices);
        var capture = new WindowsSelectedKeyCapture(settings, keyPressed, Enumerable.Range(1, 254).Where(GlobalKeyboard.IsKeyDown));
        return WindowsRawInputHub.Subscribe(capture.Accept);
    }
}

internal sealed class WindowsSelectedKeyCapture(AppSettings settings, Action<int> keyPressed, IEnumerable<int>? initiallyHeld = null)
{
    private readonly WindowsInputState _state = new(settings.WindowsSelectedMousePath, settings.WindowsSelectedKeyboardPath,
        initiallyHeld);

    internal void Accept(WindowsRawPacket packet)
    {
        bool wasDown = packet.Key != 0 && _state.IsKeyDown(packet.Key);
        _state.Accept(packet);
        if (packet.Down && packet.Key != 0 && !wasDown && _state.IsKeyDown(packet.Key)) keyPressed(packet.Key);
    }
}
