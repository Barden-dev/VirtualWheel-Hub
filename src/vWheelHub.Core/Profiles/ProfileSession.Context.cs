using SimRacingHub.Models;

namespace SimRacingHub.Services;

public partial class ProfileSession
{
    private ProfileContext? _loadedContext;
    public ProfileContext? LoadedContext => _loadedContext == null ? null : new(_loadedContext.Game, _loadedContext.CarClass, _loadedContext.Car);
}
