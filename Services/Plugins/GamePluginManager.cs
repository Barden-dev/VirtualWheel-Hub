using System;
using System.Collections.Generic;

namespace SimRacingHub.Services.Plugins
{
    public class GamePluginDiagnosticResult
    {
        public string GameId { get; set; } = string.Empty;
        public string GameDisplayName { get; set; } = string.Empty;
        public PluginStatus Status { get; set; } = PluginStatus.Installed;
        public string InstalledGamePath { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public bool IsActionRequired => false;
    }

    public class GamePluginManager
    {
        public void RegisterPlugin(GamePluginDefinition def) { }

        public bool SupportsPlugin(string gameId) => false;

        public GamePluginDiagnosticResult CheckPluginStatus(string gameId, string? overridePath = null)
        {
            return new GamePluginDiagnosticResult
            {
                GameId = gameId,
                GameDisplayName = gameId,
                Status = PluginStatus.Installed,
                StatusMessage = "No plugin required for this game."
            };
        }

        public (bool Success, string Message) InstallPlugin(string gameId, string? overridePath = null, bool autoCloseGame = true)
        {
            return (true, "No plugin required.");
        }
    }
}
