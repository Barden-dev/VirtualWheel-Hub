using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace SimRacingHub.Core
{
    public sealed class PreparationFileConflictException(string path) : IOException("File changed since preparation: " + path);
    public sealed class PreparationJournalConflictException(string path) : IOException("Preparation journal changed outside this hub instance: " + path);
    public sealed class PreparationJournalException(string path, bool reading, Exception inner)
        : IOException("Preparation journal " + (reading ? "could not be read: " : "could not be saved: ") + path, inner)
    {
        public bool Reading { get; } = reading;
    }

    public sealed class PreparedFileBackup
    {
        public string OriginalFilePath { get; set; } = string.Empty;
        public string BackupFilePath { get; set; } = string.Empty;
        public bool OriginalExisted { get; set; } = true;
        public bool Restored { get; set; }
        public string? RestoreError { get; set; }
        public string OriginalHash { get; set; } = string.Empty;
        public string AppliedHash { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class GamePreparationState
    {
        public string GameId { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAt { get; set; }
        public string Status { get; set; } = "InProgress"; // InProgress, Completed, RebootPending, Reverted
        public bool RebootDeferred { get; set; }
        public long RebootBootTime { get; set; }
        public string? RebootSessionId { get; set; }
        public string? RebootReason { get; set; }
        public List<string> CompletedSteps { get; set; } = new();
        public List<string> PendingSteps { get; set; } = new();
        public List<PreparedFileBackup> FileBackups { get; set; } = new();
    }

    /// <summary>
    /// Persistent journal for simulator game preparation operations, tracking completed steps,
    /// file backups, reboot requests, and safe non-destructive rollback.
    /// </summary>
    public sealed class GamePreparationJournal
    {
        private static readonly Lazy<GamePreparationJournal> _lazyInstance = new(() => new GamePreparationJournal());
        public static GamePreparationJournal Instance => _lazyInstance.Value;

        private readonly string _storagePath;
        private readonly object _sync = new();
        private readonly Func<string> _bootSession;
        private Exception? _loadFailure;
        private string? _persistedHash;

        private void EnsureReadable()
        {
            if (_loadFailure != null) throw new PreparationJournalException(_storagePath, true, _loadFailure);
        }
        private Dictionary<string, GamePreparationState> _states = new(StringComparer.OrdinalIgnoreCase);

        public GamePreparationJournal(string? customPath = null, Func<string?>? bootSession = null)
        {
            _bootSession = () => bootSession?.Invoke() ?? (File.Exists("/proc/sys/kernel/random/boot_id")
                ? File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim() : "");
            _storagePath = customPath ?? Path.Combine(StoragePaths.LocalDataDirectory, "preparation-journal.json");
            Load();
        }

        public GamePreparationState? GetState(string gameId)
        {
            EnsureReadable();
            if (string.IsNullOrWhiteSpace(gameId)) return null;
            lock (_sync)
            {
                return _states.TryGetValue(NormalizeGameId(gameId), out var state) ? state : null;
            }
        }

        public IReadOnlyList<GamePreparationState> GetAllStates()
        {
            EnsureReadable();
            lock (_sync)
            {
                return _states.Values.ToList();
            }
        }

        public GamePreparationState StartPreparation(string gameId)
        {
            EnsureReadable();
            ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (_states.TryGetValue(key, out var existing) && existing.Status != "Reverted")
                {
                    existing.Status = existing.Status == "RebootPending" ? existing.Status : "InProgress";
                    SaveInternal();
                    return existing;
                }
                var state = new GamePreparationState
                {
                    GameId = key,
                    StartedAt = DateTimeOffset.UtcNow,
                    Status = "InProgress"
                };
                _states[key] = state;
                SaveInternal();
                return state;
            }
        }

        public void RecordStep(string gameId, string stepName)
        {
            if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(stepName)) return;
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = StartPreparation(key);
                }

                if (!state.CompletedSteps.Contains(stepName, StringComparer.OrdinalIgnoreCase))
                {
                    state.CompletedSteps.Add(stepName);
                }
                state.PendingSteps.RemoveAll(s => string.Equals(s, stepName, StringComparison.OrdinalIgnoreCase));
                SaveInternal();
            }
        }

        public void PlanSteps(string gameId, params string[] steps)
        {
            lock (_sync)
            {
                var state = GetState(gameId);
                if (state == null || state.Status == "Reverted") state = StartPreparation(gameId);
                state.PendingSteps = steps.Where(s => !state.CompletedSteps.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
                SaveInternal();
            }
        }

        public void RecordFileBackup(string gameId, string originalFilePath, string backupFilePath, string originalHash, string appliedHash)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return;
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = StartPreparation(key);
                }

                state.FileBackups.Add(new PreparedFileBackup
                {
                    OriginalFilePath = originalFilePath,
                    BackupFilePath = backupFilePath,
                    OriginalHash = originalHash.ToLowerInvariant(),
                    AppliedHash = appliedHash.ToLowerInvariant(),
                    Timestamp = DateTimeOffset.UtcNow
                });
                SaveInternal();
            }
        }

        public void MarkRebootPending(string gameId, string reason, bool deferred = false)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return;
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = StartPreparation(key);
                }

                state.Status = "RebootPending";
                state.RebootReason = reason;
                state.RebootBootTime = BootTime;
                string session = _bootSession();
                state.RebootSessionId = string.IsNullOrEmpty(session) ? null : session;
                state.RebootDeferred = deferred;
                SaveInternal();
            }
        }

        private static long BootTime => DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Environment.TickCount64 / 1000;
        public static string NormalizeGameId(string game) => game.Trim().ToUpperInvariant() switch
        {
            "LMU" => "Le Mans Ultimate", "AC" => "Assetto Corsa", "ACC" => "Assetto Corsa Competizione",
            "ACE" => "Assetto Corsa Evo", "BEAMNG" => "BeamNG.drive", var _ => game.Trim()
        };

        public bool IsRebootPending(string gameId)
        {
            var state = GetState(gameId);
            if (state == null || state.Status != "RebootPending") return false;
            string currentSession = _bootSession();
            if (state.RebootSessionId != null ? !string.IsNullOrEmpty(currentSession) && state.RebootSessionId != currentSession : state.RebootBootTime != 0 && Math.Abs(state.RebootBootTime - BootTime) > 10)
            {
                lock (_sync) { state.Status = "InProgress"; state.RebootDeferred = false; SaveInternal(); }
                return false;
            }
            return true;
        }

        public bool CanRollback(string gameId)
        {
            var state = GetState(gameId);
            return state != null && state.FileBackups.Any(b => !b.Restored) && !string.Equals(state.Status, "Reverted", StringComparison.OrdinalIgnoreCase);
        }

        public void DeferReboot(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return;
            lock (_sync)
            {
                if (_states.TryGetValue(NormalizeGameId(gameId), out var state))
                {
                    state.RebootDeferred = true;
                    SaveInternal();
                }
            }
        }

        public void CompleteRestart(string gameId)
        {
            lock (_sync)
            {
                var state = GetState(gameId);
                if (state == null) return;
                state.Status = "InProgress";
                state.RebootDeferred = false;
                SaveInternal();
            }
        }

        public void CompletePreparation(string gameId) => MarkCompleted(gameId);

        public Task<bool> RollbackAsync(string gameId, System.Threading.CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var result = Rollback(gameId);
            return Task.FromResult(result.Success);
        }

        public void MarkCompleted(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return;
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = StartPreparation(key);
                }

                state.Status = "Completed";
                state.PendingSteps.Clear();
                state.CompletedAt = DateTimeOffset.UtcNow;
                SaveInternal();
            }
        }

        public (bool Success, int RestoredCount, int SkippedCount) Rollback(string gameId)
        {
            EnsureReadable();
            if (string.IsNullOrWhiteSpace(gameId)) return (false, 0, 0);
            string key = NormalizeGameId(gameId);

            lock (_sync)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    return (false, 0, 0);
                }

                int restored = 0;
                int skipped = 0;

                foreach (var backup in state.FileBackups.Where(b => !b.Restored))
                {
                    try
                    {
                        string? current = File.Exists(backup.OriginalFilePath) ? HashFile(backup.OriginalFilePath) : null;
                        if ((backup.OriginalExisted && current == backup.OriginalHash) || (!backup.OriginalExisted && current == null))
                        {
                            backup.Restored = true;
                            backup.RestoreError = null;
                            restored++;
                            continue;
                        }
                        if (!string.Equals(current, backup.AppliedHash, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("The prepared file was modified after preparation.");
                        if (backup.OriginalExisted)
                        {
                            if (!File.Exists(backup.BackupFilePath) || HashFile(backup.BackupFilePath) != backup.OriginalHash)
                                throw new IOException("The original backup is missing or damaged.");
                            string temp = backup.OriginalFilePath + ".vwh-restore-" + Guid.NewGuid().ToString("N");
                            try
                            {
                                File.Copy(backup.BackupFilePath, temp);
                                File.Move(temp, backup.OriginalFilePath, overwrite: true);
                            }
                            finally { if (File.Exists(temp)) File.Delete(temp); }
                        }
                        else File.Delete(backup.OriginalFilePath);
                        backup.Restored = true;
                        backup.RestoreError = null;
                        restored++;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.LogError($"Cannot roll back '{backup.OriginalFilePath}'", ex);
                        backup.RestoreError = ex.Message;
                        skipped++;
                    }
                }
                state.Status = skipped == 0 ? "Reverted" : "RollbackConflict";
                SaveInternal();
                return (skipped == 0, restored, skipped);
            }
        }

        public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        // Persist the original BEFORE changing a file. Complete only after the atomic replacement succeeds.
        public FileChange CaptureFile(string gameId, string path)
        {
            path = Path.GetFullPath(path);
            lock (_sync)
            {
                var state = GetState(gameId);
                if (state == null || state.Status == "Reverted") state = StartPreparation(gameId);
                var previous = state.FileBackups.FirstOrDefault(b => b.OriginalFilePath == path && !b.Restored);
                if (previous != null)
                {
                    var current = File.Exists(path) ? HashFile(path) : null;
                    if (current != previous.AppliedHash && current != previous.OriginalHash)
                        throw new PreparationFileConflictException(path);
                    return new FileChange(this, previous);
                }
                bool exists = File.Exists(path);
                string backupPath = Path.Combine(Path.GetDirectoryName(_storagePath)!, "preparation-backups", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                if (exists) File.Copy(path, backupPath);
                var backup = new PreparedFileBackup
                {
                    OriginalFilePath = path, BackupFilePath = backupPath, OriginalExisted = exists,
                    OriginalHash = exists ? HashFile(backupPath) : "", AppliedHash = exists ? HashFile(backupPath) : ""
                };
                state.FileBackups.Add(backup);
                SaveInternal();
                return new FileChange(this, backup);
            }
        }

        public sealed class FileChange : IDisposable
        {
            private readonly GamePreparationJournal _owner;
            private readonly PreparedFileBackup _backup;
            private string? _expectedHash;
            internal FileChange(GamePreparationJournal owner, PreparedFileBackup backup) { _owner = owner; _backup = backup; }
            public void Expect(byte[] bytes)
            {
                lock (_owner._sync)
                {
                    _expectedHash = ComputeHash(bytes);
                    _backup.AppliedHash = _expectedHash;
                    _owner.SaveInternal();
                }
            }
            public void Complete()
            {
                lock (_owner._sync)
                {
                    string actual = HashFile(_backup.OriginalFilePath);
                    if (_expectedHash != null && actual != _expectedHash)
                        throw new IOException("Prepared file changed before verification.");
                    _backup.AppliedHash = actual;
                    _owner.SaveInternal();
                }
            }
            public void Dispose() { }
        }

        private void Load()
        {
            lock (_sync)
            {
                try
                {
                    // File.Exists also returns false on access errors: only a real ENOENT means a fresh journal.
                    byte[] bytes = File.ReadAllBytes(_storagePath);
                    using var reader = new StreamReader(new MemoryStream(bytes));
                    string json = reader.ReadToEnd();
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, GamePreparationState>>(json);
                    if (loaded == null || loaded.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                        entry.Value.FileBackups == null || entry.Value.CompletedSteps == null || entry.Value.PendingSteps == null ||
                        entry.Value.FileBackups.Any(b => b == null || string.IsNullOrWhiteSpace(b.OriginalFilePath) || string.IsNullOrWhiteSpace(b.BackupFilePath))))
                        throw new InvalidDataException("Invalid preparation journal structure.");
                    _states = new Dictionary<string, GamePreparationState>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in loaded)
                    {
                        string key = NormalizeGameId(entry.Key);
                        if (_states.TryGetValue(key, out var existing)) existing.FileBackups.AddRange(entry.Value.FileBackups);
                        else _states[key] = entry.Value;
                    }
                    _persistedHash = ComputeHash(bytes);
                    return;
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { }
                catch (Exception ex)
                {
                    _loadFailure = ex;
                    AppLogger.Instance.LogError($"Failed to load preparation journal from '{_storagePath}'", ex);
                }
                _states = new Dictionary<string, GamePreparationState>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveInternal()
        {
            EnsureReadable();
            string tempPath = _storagePath + ".tmp";
            try
            {
                string? dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // A process-local lock cannot protect two hub instances or an external journal edit.
                using var lease = new FileStream(_storagePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                string? currentHash;
                try { currentHash = HashFile(_storagePath); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { currentHash = null; }
                if (currentHash != _persistedHash) throw new PreparationJournalConflictException(_storagePath);

                string json = JsonConvert.SerializeObject(_states, Formatting.Indented);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                { output.Write(bytes); output.Flush(flushToDisk: true); }
                File.Move(tempPath, _storagePath, overwrite: true);
                _persistedHash = ComputeHash(bytes);
            }
            catch (PreparationJournalConflictException) { throw; }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to save preparation journal to '{_storagePath}'", ex);
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw new PreparationJournalException(_storagePath, false, ex);
            }
        }

        public static string ComputeHash(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
