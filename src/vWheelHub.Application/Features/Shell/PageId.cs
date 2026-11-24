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

    public enum AxisControlTab { Steering, Throttle, Brake, Clutch }

    public enum NavigationSection
    {
        Dashboard,
        Profiles,
        Controls,
        Application
    }

    internal static class PageIdSections
    {
        public static bool IsControlSection(PageId page) => page is PageId.Steering or PageId.Pedals or PageId.TrailBraking
            or PageId.KeyBindings or PageId.Output or PageId.ControlAssignment or PageId.InputDevices or PageId.SoundPreview;

        public static bool IsApplicationSection(PageId page) => page is PageId.Settings or PageId.About or PageId.GameDataPreview;

        public static bool IsGameConnectionPage(PageId page) => false;
    }
}
