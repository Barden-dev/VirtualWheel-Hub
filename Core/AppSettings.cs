using System;
using System.IO;
using Newtonsoft.Json;

namespace SimRacingHub.Core
{
    public class AppSettings
    {
        public bool AutoStartHub { get; set; }

        [JsonProperty("AutoDetectOnStartup", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AutoDetectOnStartup { get; set; }

        [JsonProperty("EnableAutoDetect", NullValueHandling = NullValueHandling.Ignore)]
        public bool? EnableAutoDetect { get; set; }

        [JsonIgnore]
        public bool ResolvedAutoDetectOnStartup => AutoDetectOnStartup ?? EnableAutoDetect ?? true;

        public static AppSettings Load(string? customPath = null)
        {
            string path = customPath ?? StoragePaths.AppSettingsPath;

            try
            {
                if (!File.Exists(path)) return new AppSettings();

                var loaded = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(path));
                if (loaded != null) return loaded;

                AppLogger.Instance.LogWarning($"Application settings file '{Path.GetFileName(path)}' is empty, so default settings are used.");
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to load application settings from '{path}'", ex);
            }

            return new AppSettings();
        }

        public void Save(string? customPath = null)
        {
            string path = customPath ?? StoragePaths.AppSettingsPath;
            string tempPath = path + ".tmp";

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(tempPath, JsonConvert.SerializeObject(this, Formatting.Indented));
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to save application settings to '{path}'", ex);

                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch (Exception cleanupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppSettings] Could not remove '{tempPath}': {cleanupEx.Message}");
                }
            }
        }
    }
}
