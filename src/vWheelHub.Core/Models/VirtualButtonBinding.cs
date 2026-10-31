using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Models;

public enum VirtualButtonMode { Hold, Pulse }

public sealed record VirtualButtonBinding(string Name, int Key, int Button,
    VirtualButtonMode Mode = VirtualButtonMode.Hold, int PulseMs = 100, bool AllowInMenu = true);

public partial class Profile
{
    [ObservableProperty] private VirtualButtonBinding[]? _virtualButtons;
}

public partial class ResolvedProfile
{
    [ObservableProperty] private VirtualButtonBinding[] _virtualButtons = [];
}
