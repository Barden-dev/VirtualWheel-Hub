using SimRacingHub.Models;

namespace SimRacingHub.Core
{
    public sealed class WindowsOutputFeederFactory : IOutputFeederFactory, IOutputModePolicy
    {
        private readonly HubVJoyDeviceStore _selection;
        public WindowsOutputFeederFactory() : this(new HubVJoyDeviceStore()) { }
        internal WindowsOutputFeederFactory(HubVJoyDeviceStore selection) => _selection = selection;
        public int ResolveVJoyDeviceId(ResolvedProfile profile) => new VJoyInventoryService(selection: _selection).ResolveAutomatic(profile);
        // Xbox360 remains readable in old profile files, but Windows output is now vJoy.
        public OutputDeviceMode ResolveOutputMode(OutputDeviceMode savedMode) => savedMode is OutputDeviceMode.VJoy or OutputDeviceMode.Xbox360
            ? OutputDeviceMode.VJoy : throw new System.PlatformNotSupportedException($"Output mode {savedMode} is unavailable on Windows.");
        public IOutputFeeder CreateFeeder(OutputDeviceMode mode) => CreateFeeder(mode, 1);

        public IOutputFeeder CreateFeeder(OutputDeviceMode mode, int vJoyDeviceId)
        {
            return ResolveOutputMode(mode) switch
            {
                OutputDeviceMode.VJoy => new VJoyWrapper((uint)System.Math.Clamp(vJoyDeviceId, 1, 16),
                    () => _selection.ValidateForAcquire(vJoyDeviceId, new WindowsVJoyConfigurationStore())),
                _ => throw new System.PlatformNotSupportedException($"Output mode {mode} is unavailable on Windows.")
            };
        }
    }
}
