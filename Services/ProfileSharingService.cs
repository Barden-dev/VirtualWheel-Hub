using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using SimRacingHub.Core;
using SimRacingHub.Models;

namespace SimRacingHub.Services
{
    public class ProfileSharingService
    {
        public const string CodecPrefixV1 = "VWH1-";

        private const int MaxDecompressedBytes = 1 * 1024 * 1024;
        private const int MaxCompressedBytes = 1 * 1024 * 1024;
        private const int MaxShareFileBytes = 2 * 1024 * 1024;

        public string ExportToShareCode(ProfileSharePackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            string json = JsonConvert.SerializeObject(package, Formatting.None);
            byte[] rawBytes = Encoding.UTF8.GetBytes(json);

            using var memoryStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflateStream.Write(rawBytes, 0, rawBytes.Length);
            }

            string base64 = Convert.ToBase64String(memoryStream.ToArray());
            return $"{CodecPrefixV1}{base64}";
        }

        public ProfileSharePackage? ImportFromShareCode(string shareCode)
        {
            if (string.IsNullOrWhiteSpace(shareCode)) return null;

            shareCode = shareCode.Trim();

            if (!shareCode.StartsWith(CodecPrefixV1, StringComparison.OrdinalIgnoreCase))
            {
                return ImportFromRawJson(shareCode);
            }

            try
            {
                string base64 = shareCode.Substring(CodecPrefixV1.Length).Trim();

                if ((long)(base64.Length / 4) * 3 > MaxCompressedBytes)
                {
                    AppLogger.Instance.LogWarning("This share code is far too large to be a profile preset. Import cancelled.");
                    return null;
                }

                var compressed = new byte[base64.Length / 4 * 3 + 3];
                if (!Convert.TryFromBase64String(base64, compressed, out int compressedLength))
                {
                    AppLogger.Instance.LogWarning("This share code is damaged or incomplete. Ask for it again and paste it in one piece.");
                    return null;
                }

                string? json = TryDecompress(compressed, compressedLength);
                if (json == null) return null;

                return ImportFromRawJson(json);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("This share code could not be read. Import cancelled.", ex);
                return null;
            }
        }

        private static string? TryDecompress(byte[] compressed, int length)
        {
            using var input = new MemoryStream(compressed, 0, length, writable: false);
            using var deflateStream = new DeflateStream(input, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            var buffer = new byte[8192];
            int read;
            while ((read = deflateStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (outputStream.Length + read > MaxDecompressedBytes)
                {
                    AppLogger.Instance.LogWarning(
                        $"This share code unpacks to more than {MaxDecompressedBytes / (1024 * 1024)} MB, which no profile preset does. Import cancelled.");
                    return null;
                }

                outputStream.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(outputStream.ToArray());
        }

        public string ExportToJson(ProfileSharePackage package)
        {
            return JsonConvert.SerializeObject(package, Formatting.Indented);
        }

        public ProfileSharePackage? ImportFromRawJson(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return null;

            try
            {
                // Try parsing as full package
                var package = JsonConvert.DeserializeObject<ProfileSharePackage>(jsonText);
                if (package != null && package.Settings != null && (package.SchemaVersion > 0 || package.TargetContext != null))
                {
                    int fallbackRate = Profile.CreateDefault(package.TargetContext?.Game ?? "Universal").PollingRate ?? 1000;
                    package.Settings.MigrateToCurrentSchema(fallbackRate);
                    return package;
                }

                // Fallback: Try parsing as raw Profile
                var rawProfile = JsonConvert.DeserializeObject<Profile>(jsonText);
                if (rawProfile != null)
                {
                    rawProfile.MigrateToCurrentSchema(rawProfile.PollingRate ?? 1000);
                    return new ProfileSharePackage
                    {
                        SchemaVersion = ProfileSharePackage.CurrentSchemaVersion,
                        Name = rawProfile.Name ?? "Imported Profile",
                        Author = "Unknown",
                        Description = "Imported from legacy profile format",
                        TargetContext = new ProfileContext("Universal"),
                        Settings = rawProfile
                    };
                }
            }
            catch
            {
                // Invalid JSON
            }

            return null;
        }

        public ProfileSharePackage? ImportFromFile(string filePath)
        {
            string content;

            try
            {
                if (!File.Exists(filePath)) return null;

                var info = new FileInfo(filePath);
                if (info.Length > MaxShareFileBytes)
                {
                    AppLogger.Instance.LogWarning(
                        $"'{info.Name}' is larger than {MaxShareFileBytes / (1024 * 1024)} MB, which no profile preset is. Import cancelled.");
                    return null;
                }

                content = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"'{Path.GetFileName(filePath)}' could not be read. Import cancelled.", ex);
                return null;
            }

            // Check if file contains Share Code or JSON
            if (content.Trim().StartsWith(CodecPrefixV1, StringComparison.OrdinalIgnoreCase))
            {
                return ImportFromShareCode(content);
            }

            return ImportFromRawJson(content);
        }

        public void ExportToFile(string filePath, ProfileSharePackage package)
        {
            string json = ExportToJson(package);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, json);
        }

        public string GenerateDefaultFileName(ProfileContext context, string presetName)
        {
            string namePart = !string.IsNullOrWhiteSpace(presetName) ? presetName.Trim() : "Profile";
            namePart = ProfileManager.SanitizeName(namePart);

            string game = ProfileManager.SanitizeName(context?.Game);
            string carClass = ProfileManager.SanitizeName(context?.CarClass);
            string car = ProfileManager.SanitizeName(context?.Car);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(game) && !game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(game).Append("_");
            }
            if (!string.IsNullOrEmpty(carClass))
            {
                sb.Append(carClass).Append("_");
            }
            if (!string.IsNullOrEmpty(car))
            {
                sb.Append(car).Append("_");
            }

            sb.Append(namePart).Append(".vwh");
            return sb.ToString().Replace(" ", "_");
        }
    }
}
