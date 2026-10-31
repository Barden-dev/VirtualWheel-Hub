using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Application.Fakes
{
    /// <summary>
    /// Test and preview fake implementation of <see cref="IAppMetadata"/>.
    /// </summary>
    public sealed class FakeAppMetadata : IAppMetadata
    {
        public string ApplicationName { get; set; } = "vWheelHub";
        public string Version { get; set; } = "2.0.0";
        public string Edition { get; set; } = "Cross-Platform Edition";
        public string Copyright { get; set; } = "Copyright (c) 2026 Barden-dev. All Rights Reserved.";

        public static FakeAppMetadata Default => new();
    }
}
