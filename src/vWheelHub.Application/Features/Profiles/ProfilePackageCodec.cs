using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using SimRacingHub.Core;
using SimRacingHub.Models;
using SimRacingHub.Services;

namespace SimRacingHub.Application.Features.Profiles
{
    /// <summary>
    /// Codec for serializing, deserializing, compressing, and validating profile share packages (ProfileSharePackage).
    /// Enforces schema version compatibility (Schema 2) and provides safe error handling for corrupted payloads.
    /// </summary>
    public class ProfilePackageCodec
    {
        public const string CodecPrefixV1 = "VWH1-";
        public const int SupportedSchemaVersion = ProfileSharePackage.CurrentSchemaVersion; // Schema 2

        private const int MaxDecompressedBytes = 1 * 1024 * 1024; // 1 MB
        private const int MaxCompressedBytes = 1 * 1024 * 1024;   // 1 MB

        /// <summary>
        /// Encodes a ProfileSharePackage into a compressed base64 share-code string with the 'VWH1-' prefix.
        /// </summary>
        public string EncodeShareCode(ProfileSharePackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            if (package.SchemaVersion <= 0)
            {
                package.SchemaVersion = SupportedSchemaVersion;
            }

            string json = EncodeJson(package, indented: false);
            byte[] rawBytes = Encoding.UTF8.GetBytes(json);

            using var memoryStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflateStream.Write(rawBytes, 0, rawBytes.Length);
            }

            string base64 = Convert.ToBase64String(memoryStream.ToArray());
            return $"{CodecPrefixV1}{base64}";
        }

        /// <summary>
        /// Encodes a ProfileSharePackage into a JSON string.
        /// </summary>
        public string EncodeJson(ProfileSharePackage package, bool indented = true)
        {
            ArgumentNullException.ThrowIfNull(package);

            if (package.SchemaVersion <= 0)
            {
                package.SchemaVersion = SupportedSchemaVersion;
            }

            return JsonConvert.SerializeObject(package, indented ? Formatting.Indented : Formatting.None);
        }

        /// <summary>
        /// Decodes a share code string or raw JSON into a ProfileSharePackage.
        /// Returns null if the payload is invalid, corrupted, or uses an incompatible schema version.
        /// </summary>
        public ProfileSharePackage? DecodeShareCode(string? shareCode)
        {
            return TryDecodeShareCode(shareCode, out var package, out _) ? package : null;
        }

        /// <summary>
        /// Attempts to decode a share code string or raw JSON into a ProfileSharePackage.
        /// </summary>
        public bool TryDecodeShareCode(string? shareCode, out ProfileSharePackage? package, out string? errorMessage)
        {
            package = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(shareCode))
            {
                errorMessage = "Share code is empty or whitespace.";
                return false;
            }

            string trimmed = shareCode.Trim();

            // If it doesn't start with CodecPrefixV1, fall back to parsing as raw JSON
            if (!trimmed.StartsWith(CodecPrefixV1, StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeJson(trimmed, out package, out errorMessage);
            }

            try
            {
                string base64 = trimmed.Substring(CodecPrefixV1.Length).Trim();

                if ((long)(base64.Length / 4) * 3 > MaxCompressedBytes)
                {
                    errorMessage = $"Share code exceeds maximum allowable compressed size ({MaxCompressedBytes / (1024 * 1024)} MB).";
                    AppLogger.Instance.LogWarning(errorMessage);
                    return false;
                }

                byte[] compressed;
                try
                {
                    compressed = Convert.FromBase64String(base64);
                }
                catch (FormatException ex)
                {
                    errorMessage = $"Share code contains invalid Base64 characters: {ex.Message}";
                    AppLogger.Instance.LogWarning(errorMessage);
                    return false;
                }

                string? json = TryDecompress(compressed, compressed.Length, out string? decompressError);
                if (json == null)
                {
                    errorMessage = decompressError ?? "Failed to decompress share code payload.";
                    return false;
                }

                return TryDecodeJson(json, out package, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected error decoding share code: {ex.Message}";
                AppLogger.Instance.LogError(errorMessage, ex);
                return false;
            }
        }

        /// <summary>
        /// Decodes a JSON string into a ProfileSharePackage.
        /// Returns null if the JSON is malformed, missing required fields, or uses an incompatible schema version.
        /// </summary>
        public ProfileSharePackage? DecodeJson(string? jsonText)
        {
            return TryDecodeJson(jsonText, out var package, out _) ? package : null;
        }

        /// <summary>
        /// Attempts to decode a JSON string into a ProfileSharePackage.
        /// Supports both full ProfileSharePackage JSON envelopes and legacy raw Profile JSON.
        /// </summary>
        public bool TryDecodeJson(string? jsonText, out ProfileSharePackage? package, out string? errorMessage)
        {
            package = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "JSON text is empty or whitespace.";
                return false;
            }

            try
            {
                bool hasSettingsProperty = jsonText.Contains("\"Settings\"", StringComparison.OrdinalIgnoreCase);

                if (hasSettingsProperty)
                {
                    // 1. Parse as full ProfileSharePackage
                    ProfileSharePackage? parsedPackage = null;
                    try
                    {
                        parsedPackage = JsonConvert.DeserializeObject<ProfileSharePackage>(jsonText);
                    }
                    catch (JsonException ex)
                    {
                        errorMessage = $"Malformed JSON: {ex.Message}";
                        return false;
                    }

                    if (parsedPackage != null)
                    {
                        // Validate schema version
                        if (parsedPackage.SchemaVersion > SupportedSchemaVersion)
                        {
                            errorMessage = $"Incompatible schema version {parsedPackage.SchemaVersion}. Maximum supported version is {SupportedSchemaVersion}.";
                            AppLogger.Instance.LogWarning(errorMessage);
                            return false;
                        }

                        if (parsedPackage.Settings == null)
                        {
                            errorMessage = "Profile share package is missing Settings payload.";
                            return false;
                        }

                        int fallbackRate = ProfileDefaults.CreateDefault(parsedPackage.TargetContext?.Game ?? "Universal").PollingRate ?? 1000;
                        parsedPackage.Settings.MigrateToCurrentSchema(fallbackRate);
                        package = parsedPackage;
                        return true;
                    }
                }
                else
                {
                    // 2. Fallback: Try parsing as legacy raw Profile
                    Profile? rawProfile = null;
                    try
                    {
                        rawProfile = JsonConvert.DeserializeObject<Profile>(jsonText);
                    }
                    catch
                    {
                        // Ignored, will fall through to error
                    }

                    if (rawProfile != null && (rawProfile.MouseSensitivity.HasValue || !string.IsNullOrEmpty(rawProfile.Name)))
                    {
                        rawProfile.MigrateToCurrentSchema(rawProfile.PollingRate ?? 1000);
                        package = new ProfileSharePackage
                        {
                            SchemaVersion = SupportedSchemaVersion,
                            Name = rawProfile.Name ?? "Imported Profile",
                            Author = "Unknown",
                            Description = "Imported from legacy profile format",
                            TargetContext = new ProfileContext("Universal"),
                            Settings = rawProfile
                        };
                        return true;
                    }
                }

                errorMessage = "JSON does not represent a valid profile package or settings structure.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error parsing JSON: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Validates whether a profile share package satisfies schema constraints.
        /// </summary>
        public bool ValidateSchema(ProfileSharePackage? package, out string? errorMessage)
        {
            if (package == null)
            {
                errorMessage = "Package is null.";
                return false;
            }

            if (package.SchemaVersion <= 0)
            {
                errorMessage = "Schema version must be positive.";
                return false;
            }

            if (package.SchemaVersion > SupportedSchemaVersion)
            {
                errorMessage = $"Incompatible schema version {package.SchemaVersion}. Maximum supported version is {SupportedSchemaVersion}.";
                return false;
            }

            if (package.Settings == null)
            {
                errorMessage = "Package settings payload is missing.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Throws an InvalidOperationException if the package uses an incompatible schema version.
        /// </summary>
        public void EnsureCompatibleSchema(ProfileSharePackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            if (!ValidateSchema(package, out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Generates a sanitized default export file name based on context and preset name.
        /// </summary>
        public string GenerateDefaultFileName(ProfileContext? context, string? presetName)
        {
            string namePart = !string.IsNullOrWhiteSpace(presetName) ? presetName.Trim() : "Profile";
            namePart = ProfileManager.SanitizeName(namePart);

            string game = ProfileManager.SanitizeName(context?.Game);
            string carClass = ProfileManager.SanitizeName(context?.CarClass);
            string car = ProfileManager.SanitizeName(context?.Car);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(game) && !game.Equals("Universal", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(game).Append('_');
            }
            if (!string.IsNullOrEmpty(carClass))
            {
                sb.Append(carClass).Append('_');
            }
            if (!string.IsNullOrEmpty(car))
            {
                sb.Append(car).Append('_');
            }

            sb.Append(namePart).Append(".vwh");
            return sb.ToString().Replace(" ", "_");
        }

        private static string? TryDecompress(byte[] compressed, int length, out string? errorMessage)
        {
            errorMessage = null;
            try
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
                        errorMessage = $"Decompressed payload exceeds safety threshold of {MaxDecompressedBytes / (1024 * 1024)} MB.";
                        AppLogger.Instance.LogWarning(errorMessage);
                        return null;
                    }

                    outputStream.Write(buffer, 0, read);
                }

                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
            catch (Exception ex)
            {
                errorMessage = $"Decompression failed: {ex.Message}";
                return null;
            }
        }
    }
}
