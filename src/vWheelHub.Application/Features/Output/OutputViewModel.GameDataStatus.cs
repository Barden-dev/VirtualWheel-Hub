namespace SimRacingHub.Application.Features.Output;

public partial class OutputViewModel
{
    public bool SupportsGameDataConnection => false;
    public string GameDataConnectionText => _localizationManager.GetString("Output_GameDataUnverified");
}
