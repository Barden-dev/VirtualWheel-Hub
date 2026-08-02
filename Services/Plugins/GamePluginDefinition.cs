using System;
using System.Collections.Generic;

namespace SimRacingHub.Services.Plugins
{
    public enum PluginStatus
    {
        Installed,
        MissingPlugin,
        OutdatedPlugin,
        GameNotFound,
        GameRunning
    }

    public class GamePluginDefinition
    {
        public string GameId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SteamAppId { get; set; } = string.Empty;
        public string SteamFolderName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string SourcePluginAsset { get; set; } = string.Empty;
        public string TargetRelativePlugin { get; set; } = string.Empty;
        public string? ConfigFileRelativePath { get; set; }
        public string? PluginConfigKey { get; set; }
    }
}
