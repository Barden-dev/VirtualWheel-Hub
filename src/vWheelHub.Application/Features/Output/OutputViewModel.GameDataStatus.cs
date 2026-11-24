namespace SimRacingHub.Application.Features.Output;

public partial class OutputViewModel
{
    public bool SupportsGameDataConnection
    {
        get
        {
            bool supported = false;
            ResolveSupportsGameDataConnection(ref supported);
            return supported;
        }
    }

    public string GameDataConnectionText
    {
        get
        {
            string? text = null;
            ResolveGameDataConnectionText(ref text);
            return text ?? _localizationManager.GetString("Output_GameDataUnverified");
        }
    }

    partial void ResolveSupportsGameDataConnection(ref bool supported);
    partial void ResolveGameDataConnectionText(ref string? text);
}
