using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using SimRacingHub.Models;

namespace SimRacingHub.Services
{
    public class ProfileSharingService
    {
        public const string CodecPrefixV1 = "VWH1-";

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

            if (shareCode.StartsWith(CodecPrefixV1, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = shareCode.Substring(CodecPrefixV1.Length).Trim();
                byte[] compressedBytes = Convert.FromBase64String(base64);

                using var inputStream = new MemoryStream(compressedBytes);
                using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                
                deflateStream.CopyTo(outputStream);
                string json = Encoding.UTF8.GetString(outputStream.ToArray());

                return JsonConvert.DeserializeObject<ProfileSharePackage>(json);
            }

            // Fallback: If someone pasted raw JSON or uncompressed code
            return ImportFromRawJson(shareCode);
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
                    return package;
                }

                // Fallback: Try parsing as raw Profile
                var rawProfile = JsonConvert.DeserializeObject<Profile>(jsonText);
                if (rawProfile != null)
                {
                    return new ProfileSharePackage
                    {
                        SchemaVersion = 1,
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
            if (!File.Exists(filePath)) return null;

            string content = File.ReadAllText(filePath);

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
