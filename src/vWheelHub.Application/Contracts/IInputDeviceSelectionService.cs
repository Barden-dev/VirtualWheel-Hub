namespace SimRacingHub.Application.Contracts;

public enum InputDeviceKind { Mouse, Keyboard }
public sealed record InputDeviceOption(string Path, string Name, string? Identity = null);
public sealed record InputDeviceSelectionSnapshot(IReadOnlyList<InputDeviceOption> Mice,
    IReadOnlyList<InputDeviceOption> Keyboards, string? SelectedMouse, string? SelectedKeyboard);
public readonly record struct InputDeviceActivity(bool IsConnected, long Movement, long Presses);

public interface IInputDeviceActivityProbe : IDisposable
{
    InputDeviceActivity Read();
}

public interface IInputDeviceSelectionService
{
    InputDeviceSelectionSnapshot Discover();
    IInputDeviceActivityProbe Watch(InputDeviceKind kind, InputDeviceOption device);
    void Save(InputDeviceOption mouse, InputDeviceOption keyboard);
}
