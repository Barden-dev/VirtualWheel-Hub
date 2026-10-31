namespace SimRacingHub.Application.Features.Shell
{
    /// <summary>
    /// Stable page identifiers, independent of their placement in navigation sections.
    /// </summary>
    public enum PageId
    {
        Dashboard = 0,
        Profiles = 1,
        Steering = 2,
        Pedals = 3,
        KeyBindings = 5,
        Output = 7,
        Settings = 8,
        About = 11,
        ControlAssignment = 12,
        InputDevices = 13,
        SoundPreview = 14,
        TrailBraking = 15,
        GameDataPreview = 16
    }

    public enum AxisControlTab { Steering, Throttle, Brake }

    public enum NavigationSection
    {
        Dashboard,
        Profiles,
        Controls,
        Application
    }
}
