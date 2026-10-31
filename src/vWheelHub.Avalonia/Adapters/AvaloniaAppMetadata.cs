using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Adapters
{
    public sealed class AvaloniaAppMetadata : IAppMetadata
    {
        public string ApplicationName { get; }
        public string Version { get; }
        public string Edition { get; }
        public string Copyright { get; }

        public AvaloniaAppMetadata(bool isPreview = false, string? customEdition = null)
        {
            ApplicationName = "vWheel Hub";
            Version = typeof(AvaloniaAppMetadata).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "2.0";
            Edition = customEdition ?? "FREE";
            Copyright = "Copyright (c) 2026 Barden-dev. All Rights Reserved.";
        }
    }
}
