using System;
using System.Reflection;

namespace SimRacingHub.Models
{
    public class ProfileSharePackage
    {
        public const int CurrentSchemaVersion = 2;

        public static Func<string> DefaultAppVersionProvider { get; set; } = ResolveDefaultAppVersion;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string AppVersion { get; set; } = DefaultAppVersionProvider();
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public ProfileContext TargetContext { get; set; } = new ProfileContext();
        public Profile Settings { get; set; } = new Profile();

        private static string ResolveDefaultAppVersion()
        {
            try
            {
                var assembly = typeof(ProfileSharePackage).Assembly;
                string? informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                {
                    int plusIndex = informational.IndexOf('+');
                    string trimmed = plusIndex > 0 ? informational.Substring(0, plusIndex) : informational;
                    if (trimmed.Length > 0) return trimmed;
                }

                var version = assembly.GetName().Version;
                if (version != null)
                {
                    if (version.Build > 0)
                        return $"{version.Major}.{version.Minor}.{version.Build}";
                    return $"{version.Major}.{version.Minor}";
                }
            }
            catch
            {
            }

            return "2.0";
        }
    }
}
