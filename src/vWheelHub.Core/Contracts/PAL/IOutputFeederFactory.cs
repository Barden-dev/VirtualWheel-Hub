using SimRacingHub.Models;

namespace SimRacingHub.Core
{
    public interface IOutputModePolicy
    {
        OutputDeviceMode ResolveOutputMode(OutputDeviceMode savedMode);
    }

    public interface IOutputFeederFactory
    {
        IOutputFeeder CreateFeeder(OutputDeviceMode mode);
        IOutputFeeder CreateFeeder(OutputDeviceMode mode, int vJoyDeviceId) => CreateFeeder(mode);
        int ResolveVJoyDeviceId(ResolvedProfile profile) => profile.VJoyDeviceId;
    }
}
