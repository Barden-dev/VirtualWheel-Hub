using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Linux
{
    /// <summary>
    /// Factory for creating Linux uinput virtual output feeders.
    /// Supports both Virtual Wheel (VJoy mode) and Xbox 360 controller emulation.
    /// </summary>
    public class UInputFeederFactory : IOutputFeederFactory
    {
        public virtual IOutputFeeder CreateFeeder(OutputDeviceMode mode)
        {
            return new UInputFeeder(mode);
        }
    }
}
