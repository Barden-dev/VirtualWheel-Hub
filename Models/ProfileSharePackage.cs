using System;
using SimRacingHub.Services;

namespace SimRacingHub.Models
{
    public class ProfileSharePackage
    {
        public int SchemaVersion { get; set; } = 1;
        public string AppVersion { get; set; } = UpdateService.CurrentVersion;
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public ProfileContext TargetContext { get; set; } = new ProfileContext();
        public Profile Settings { get; set; } = new Profile();
    }
}
