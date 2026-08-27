using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimRacingHub.Core;
using SimRacingHub.Services.Plugins;

namespace SimRacingHub.Services
{
    public sealed class LmuInputFixStatus
    {
        public bool IsLmuContext { get; init; }
        public bool IsAvailable { get; init; }
        public bool IsEnabled { get; init; }
        public bool NeedsFix => IsAvailable && !IsEnabled;
        public bool CanRevert { get; init; }
        public bool ShouldShow => IsLmuContext && (!IsEnabled || CanRevert);
        public string ControlsPath { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
    }

    public sealed class LmuInputFixService
    {
        private const string LmuGameName = "Le Mans Ultimate";
        private const string LmuAppId = "2399420";
        private const string LmuInstallFolder = "Le Mans Ultimate";
        private const string LmuProcessName = "Le Mans Ultimate";
        private const string FallbackKey = "DirectInput Fallback";
        private string _cachedControlsPath = string.Empty;

        public Func<string, bool> IsProcessRunning { get; set; } = ProcessUtil.IsRunning;

        private static string StatePath => Path.Combine(StoragePaths.LocalDataDirectory, "lmu-input-fix.json");

        public LmuInputFixStatus Check(string? gameId)
        {
            bool isLmu = string.Equals(gameId, LmuGameName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(gameId, "LMU", StringComparison.OrdinalIgnoreCase);

            if (!isLmu)
            {
                return new LmuInputFixStatus
                {
                    IsLmuContext = false,
                    StatusText = string.Empty,
                    DetailText = string.Empty
                };
            }

            string controlsPath = ResolveControlsPath();
            if (string.IsNullOrEmpty(controlsPath) || !File.Exists(controlsPath))
            {
                return new LmuInputFixStatus
                {
                    IsLmuContext = true,
                    ControlsPath = controlsPath,
                    StatusText = "LMU controls file was not found",
                    DetailText = "Make sure LMU is installed through Steam and has been launched at least once so current controls.json exists."
                };
            }

            try
            {
                bool enabled = ReadFallbackValue(controlsPath);
                var state = LoadState();
                bool canRevert = state != null && File.Exists(state.BackupPath) &&
                                 string.Equals(state.ControlsPath, controlsPath, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(ComputeHash(File.ReadAllBytes(controlsPath)), state.AppliedHash, StringComparison.OrdinalIgnoreCase);

                return new LmuInputFixStatus
                {
                    IsLmuContext = true,
                    IsAvailable = true,
                    IsEnabled = enabled,
                    CanRevert = canRevert,
                    ControlsPath = controlsPath,
                    StatusText = enabled
                        ? "DirectInput Fallback is enabled"
                        : "DirectInput Fallback is recommended",
                    DetailText = enabled
                        ? "LMU is using the compatible DirectInput path. The game must be fully restarted after this setting is changed."
                        : "LMU's High Precision Input sampler may cause vJoy axes to stutter or jump. The fix changes one setting and creates a backup first."
                };
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to inspect LMU input settings", ex);
                return new LmuInputFixStatus
                {
                    IsLmuContext = true,
                    IsAvailable = false,
                    ControlsPath = controlsPath,
                    StatusText = "LMU settings could not be read",
                    DetailText = $"The file exists but could not be parsed: {ex.Message}"
                };
            }
        }

        public (bool Success, string Message) Apply()
        {
            if (IsProcessRunning(LmuProcessName))
            {
                return (false, "LMU is currently running. Close the game completely, then apply the fix again.");
            }

            string controlsPath = ResolveControlsPath();
            if (string.IsNullOrEmpty(controlsPath) || !File.Exists(controlsPath))
            {
                return (false, "LMU current controls.json was not found.");
            }

            string tempPath = controlsPath + ".vwh-tmp";
            string backupPath = controlsPath + $".vwh-backup-{DateTime.Now:yyyyMMdd-HHmmss}";

            bool configReplaced = false;
            try
            {
                byte[] originalBytes = File.ReadAllBytes(controlsPath);
                string originalHash = ComputeHash(originalBytes);
                var root = JObject.Parse(File.ReadAllText(controlsPath));
                JToken? fallbackToken = root.GetValue(FallbackKey, StringComparison.OrdinalIgnoreCase);
                bool wasEnabled = fallbackToken?.Type == JTokenType.Boolean && fallbackToken.Value<bool>();

                if (wasEnabled)
                {
                    return (true, "DirectInput Fallback is already enabled. No changes were needed.");
                }

                File.Copy(controlsPath, backupPath, overwrite: false);
                if (fallbackToken?.Parent is JProperty fallbackProperty)
                {
                    fallbackProperty.Value = true;
                }
                else
                {
                    root[FallbackKey] = true;
                }
                byte[] updatedBytes = new UTF8Encoding(false).GetBytes(root.ToString(Formatting.Indented) + Environment.NewLine);
                File.WriteAllBytes(tempPath, updatedBytes);
                File.Move(tempPath, controlsPath, overwrite: true);
                configReplaced = true;

                SaveState(new LmuInputFixState
                {
                    ControlsPath = controlsPath,
                    BackupPath = backupPath,
                    OriginalHash = originalHash,
                    AppliedHash = ComputeHash(updatedBytes),
                    AppliedAtUtc = DateTime.UtcNow
                });

                AppLogger.Instance.LogInfo($"Enabled LMU '{FallbackKey}' in '{controlsPath}'. Backup: '{backupPath}'.");
                return (true, "The fix was applied. Restart LMU completely so it reloads the input settings.");
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                if (configReplaced && File.Exists(backupPath))
                {
                    try { File.Copy(backupPath, controlsPath, overwrite: true); } catch { }
                }
                try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { }
                DeleteState();
                AppLogger.Instance.LogError("Failed to apply LMU input fix", ex);
                return (false, $"LMU settings could not be changed: {ex.Message}");
            }
        }

        public (bool Success, string Message) Revert()
        {
            if (IsProcessRunning(LmuProcessName))
            {
                return (false, "LMU is currently running. Close the game completely before restoring the previous setting.");
            }

            var state = LoadState();
            if (state == null || string.IsNullOrEmpty(state.ControlsPath) || string.IsNullOrEmpty(state.BackupPath) || !File.Exists(state.BackupPath))
            {
                return (false, "The backup created by vWheel Hub was not found.");
            }

            if (!File.Exists(state.ControlsPath))
            {
                return (false, "The current LMU controls file was not found. Restore was cancelled to avoid creating an incomplete configuration.");
            }

            try
            {
                string currentHash = ComputeHash(File.ReadAllBytes(state.ControlsPath));
                if (!string.Equals(currentHash, state.AppliedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "LMU changed the controls file after the fix was applied. Restore was cancelled so newer settings are not lost.");
                }

                string tempPath = state.ControlsPath + ".vwh-revert-tmp";
                File.Copy(state.BackupPath, tempPath, overwrite: true);
                File.Move(tempPath, state.ControlsPath, overwrite: true);

                try { File.Delete(state.BackupPath); }
                catch (Exception cleanupEx)
                {
                    AppLogger.Instance.LogWarning($"LMU input fix was reverted, but its old backup could not be removed: {cleanupEx.Message}");
                }

                DeleteState();

                AppLogger.Instance.LogInfo($"Reverted LMU input fix in '{state.ControlsPath}'.");
                return (true, "The previous controls file was restored. Restart LMU completely.");
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to revert LMU input fix", ex);
                return (false, $"The previous setting could not be restored: {ex.Message}");
            }
        }

        public string ResolveControlsPath()
        {
            if (!string.IsNullOrEmpty(_cachedControlsPath) && File.Exists(_cachedControlsPath))
            {
                return _cachedControlsPath;
            }

            string? gamePath = SteamLibraryLocator.FindGamePath(LmuAppId, LmuInstallFolder);
            _cachedControlsPath = string.IsNullOrEmpty(gamePath)
                ? string.Empty
                : Path.Combine(gamePath, "UserData", "player", "current controls.json");
            return _cachedControlsPath;
        }

        public void OpenControlsFile()
        {
            string path = ResolveControlsPath();
            if (string.IsNullOrEmpty(path)) return;

            string target = File.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to open LMU input settings location", ex);
            }
        }

        private static bool ReadFallbackValue(string path)
        {
            var root = JObject.Parse(File.ReadAllText(path));
            JToken? value = root.GetValue(FallbackKey, StringComparison.OrdinalIgnoreCase);
            return value?.Type == JTokenType.Boolean && value.Value<bool>();
        }

        private static string ComputeHash(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static LmuInputFixState? LoadState()
        {
            try
            {
                if (!File.Exists(StatePath)) return null;
                return JsonConvert.DeserializeObject<LmuInputFixState>(File.ReadAllText(StatePath));
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Could not load LMU input fix state: {ex.Message}");
                return null;
            }
        }

        private static void SaveState(LmuInputFixState state)
        {
            string tempPath = StatePath + ".tmp";
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(state, Formatting.Indented));
            File.Move(tempPath, StatePath, overwrite: true);
        }

        private static void DeleteState()
        {
            try { if (File.Exists(StatePath)) File.Delete(StatePath); } catch { }
        }

        private sealed class LmuInputFixState
        {
            public string ControlsPath { get; set; } = string.Empty;
            public string BackupPath { get; set; } = string.Empty;
            public string OriginalHash { get; set; } = string.Empty;
            public string AppliedHash { get; set; } = string.Empty;
            public DateTime AppliedAtUtc { get; set; }
        }
    }
}
