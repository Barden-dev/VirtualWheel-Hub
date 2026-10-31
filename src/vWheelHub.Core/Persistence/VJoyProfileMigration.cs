using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimRacingHub.Models;

namespace SimRacingHub.Services;

public sealed record VJoyProfileMigrationRow(string Path, int PreviousDeviceId, bool JoinSharedDevice);
public sealed record VJoyProfileMigrationPreview(string Id, int PreviousDeviceId, int SharedDeviceId,
    string SharedFingerprint, string BackupDirectory, VJoyProfileMigrationRow[] Changes);
public sealed record VJoyProfileMigrationResult(bool Success, string BackupDirectory, string? Error = null);

/// <summary>Explicit saved-profile migration; driver and game bindings are never edited.</summary>
public sealed class VJoyProfileMigration
{
    private readonly string _root, _backups;
    internal Action<string>? BeforeWrite { get; set; }
    private sealed record Entry(string Path, string BeforeHash, string AfterHash, int DeviceId, bool? Join, int Index);
    private sealed record Plan(VJoyProfileMigrationPreview Preview, DateTimeOffset CreatedAt, Entry[] Files, string State, string ProfileRoot);
    private sealed record Envelope(string Sha256, byte[] Data);
    private sealed record Effective(OutputDeviceMode Mode = OutputDeviceMode.VJoy, int Id = 1, bool Shared = false);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public VJoyProfileMigration(string profileDirectory, string backupDirectory)
    {
        _root = System.IO.Path.GetFullPath(profileDirectory); _backups = System.IO.Path.GetFullPath(backupDirectory);
        string relative = System.IO.Path.GetRelativePath(_root, _backups);
        if (relative == "." || !relative.StartsWith("..", StringComparison.Ordinal) && !System.IO.Path.IsPathRooted(relative))
            throw new ArgumentException("Migration backups must be outside the profile directory.");
    }

    private string Folder(string id) => Guid.TryParseExact(id, "N", out _) ? System.IO.Path.Combine(_backups, id)
        : throw new InvalidDataException("Invalid profile migration ID.");
    private string FilePath(string relative)
    {
        string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, relative));
        if (System.IO.Path.GetRelativePath(_root, full).StartsWith("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative))
            throw new InvalidDataException("Profile path is outside the profile directory.");
        string? inspected = full;
        while (inspected != null)
        {
            if ((File.Exists(inspected) || Directory.Exists(inspected)) && (File.GetAttributes(inspected) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked profile paths cannot be migrated.");
            if (inspected == _root) break;
            inspected = System.IO.Path.GetDirectoryName(inspected);
        }
        return full;
    }
    private Dictionary<string, byte[]> Snapshot()
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        void Visit(string directory, int depth)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profile directories cannot be migrated.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profiles cannot be migrated.");
                if ((attributes & FileAttributes.Directory) != 0) { if (depth < 2) Visit(path, depth + 1); continue; }
                string name = System.IO.Path.GetFileName(path);
                if (depth == 0 || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.Contains(".corrupt-", StringComparison.OrdinalIgnoreCase) || name.Contains(".schema-v1.bak", StringComparison.OrdinalIgnoreCase)) continue;
                if (new FileInfo(path).Length > 2 * 1024 * 1024 || files.Count >= 2000) throw new IOException("Profile migration input is too large.");
                byte[] data = File.ReadAllBytes(path);
                total += data.Length;
                if (data.Length > 2 * 1024 * 1024 || total > 64 * 1024 * 1024) throw new IOException("Profile migration input is too large.");
                files.Add(System.IO.Path.GetRelativePath(_root, path).Replace('\\', '/'), data);
            }
        }
        Visit(_root, 0);
        return files;
    }
    private static JObject Parse(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var document = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (document.Properties().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Count)
            throw new InvalidDataException("Ambiguous profile property names.");
        return document;
    }
    private static IEnumerable<string> Parents(string path)
    {
        var parts = path.Split('/');
        yield return "Universal/Default.json";
        yield return parts[0] + "/Default.json";
        if (parts.Length == 3) yield return parts[0] + "/" + parts[1] + "/Default.json";
        yield return path;
    }
    private static Effective Resolve(string path, Dictionary<string, JObject> documents)
    {
        var result = new Effective();
        foreach (string parent in Parents(path).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!documents.TryGetValue(parent, out var doc)) continue;
            JToken? Get(string name) => doc.GetValue(name, StringComparison.OrdinalIgnoreCase);
            if (Get("OutputMode") is { Type: not JTokenType.Null } mode) result = result with { Mode = mode.ToObject<OutputDeviceMode>() };
            if (Get("VJoyDeviceId") is { Type: not JTokenType.Null } id) result = result with { Id = id.Value<int>() };
            if (Get("UseHubVJoyDevice") is { Type: not JTokenType.Null } shared) result = result with { Shared = shared.Value<bool>() };
        }
        if (!Enum.IsDefined(result.Mode) || result.Id is < 1 or > 16) throw new InvalidDataException("A saved profile has an unsupported output selection.");
        return result;
    }
    private static int Rank(string path) => path.Equals("Universal/Default.json", StringComparison.OrdinalIgnoreCase) ? 0 :
        path.EndsWith("/Default.json", StringComparison.OrdinalIgnoreCase) ? path.Count(c => c == '/') : 3;

    public VJoyProfileMigrationPreview Preview(int previousDeviceId, int sharedDeviceId, string sharedFingerprint)
    {
        if (previousDeviceId is < 1 or > 16 || sharedDeviceId is < 1 or > 16 || sharedFingerprint.Length != 64 || !sharedFingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("A verified shared output selection is required.");
        var before = Snapshot();
        var original = before.ToDictionary(pair => pair.Key, pair => Parse(pair.Value), StringComparer.OrdinalIgnoreCase);
        var after = original.ToDictionary(pair => pair.Key, pair => (JObject)pair.Value.DeepClone(), StringComparer.OrdinalIgnoreCase);
        var entries = new List<Entry>();
        var changes = new List<VJoyProfileMigrationRow>();
        var modified = new Dictionary<int, byte[]>();
        foreach (string path in before.Keys.OrderBy(Rank).ThenBy(path => path, StringComparer.Ordinal))
        {
            var old = Resolve(path, original);
            bool? join = !old.Shared && old.Mode == OutputDeviceMode.VJoy && old.Id == previousDeviceId ? true :
                !old.Shared && Resolve(path, after).Shared ? false : null;
            if (join != null)
            {
                var property = after[path].Properties().SingleOrDefault(property => property.Name.Equals("UseHubVJoyDevice", StringComparison.OrdinalIgnoreCase));
                if (property == null) after[path].Add("UseHubVJoyDevice", join.Value); else property.Value = join.Value;
                changes.Add(new(path, old.Id, join.Value));
            }
            byte[] data = join == null ? before[path] : Encoding.UTF8.GetBytes(after[path].ToString(Formatting.Indented));
            int index = entries.Count;
            entries.Add(new(path, Hash(before[path]), Hash(data), old.Id, join, index));
            if (join != null) modified[index] = data;
        }
        string id = Guid.NewGuid().ToString("N"), folder = Folder(id);
        var preview = new VJoyProfileMigrationPreview(id, previousDeviceId, sharedDeviceId, sharedFingerprint, folder, changes.ToArray());
        Directory.CreateDirectory(folder);
        foreach (var entry in entries.Where(entry => entry.Join != null))
        {
            WriteNew(System.IO.Path.Combine(folder, entry.Index + ".before"), before[entry.Path]);
            WriteNew(System.IO.Path.Combine(folder, entry.Index + ".after"), modified[entry.Index]);
        }
        Save(new(preview, DateTimeOffset.UtcNow, entries.ToArray(), "Prepared", _root));
        return preview;
    }
    private static void WriteNew(string path, byte[] data)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(data); stream.Flush(true);
    }
    private static void Replace(string path, byte[] data)
    {
        string temporary = path + ".migration-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteNew(temporary, data); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void Save(Plan plan)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(plan));
        Replace(System.IO.Path.Combine(Folder(plan.Preview.Id), "operation.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Envelope(Hash(data), data))));
    }
    private Plan Read(string id)
    {
        string path = System.IO.Path.Combine(Folder(id), "operation.json");
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Oversized migration journal.");
        var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path)) ?? throw new InvalidDataException("Missing migration journal.");
        if (Hash(envelope.Data) != envelope.Sha256) throw new InvalidDataException("Profile migration journal checksum failed.");
        var plan = JsonConvert.DeserializeObject<Plan>(Encoding.UTF8.GetString(envelope.Data)) ?? throw new InvalidDataException("Missing migration plan.");
        if (plan.Preview.Id != id || plan.Preview.BackupDirectory != Folder(id) || plan.ProfileRoot != _root || plan.Files.Length > 2000) throw new InvalidDataException("Invalid migration plan.");
        return plan;
    }
    private byte[] Backup(Plan plan, Entry entry, bool after)
    {
        byte[] data = File.ReadAllBytes(System.IO.Path.Combine(Folder(plan.Preview.Id), entry.Index + (after ? ".after" : ".before")));
        if (Hash(data) != (after ? entry.AfterHash : entry.BeforeHash)) throw new InvalidDataException("Profile migration backup checksum failed.");
        return data;
    }

    public VJoyProfileMigrationResult Apply(VJoyProfileMigrationPreview preview)
    {
        using var gate = new FileStream(System.IO.Path.Combine(_backups, "migration.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var plan = Read(preview.Id);
        if (plan.State != "Prepared" || DateTimeOffset.UtcNow - plan.CreatedAt > TimeSpan.FromMinutes(30) ||
            plan.Preview.SharedFingerprint != preview.SharedFingerprint || plan.Preview.SharedDeviceId != preview.SharedDeviceId ||
            plan.Preview.PreviousDeviceId != preview.PreviousDeviceId || !plan.Preview.Changes.SequenceEqual(preview.Changes))
            throw new InvalidOperationException("The profile migration preview is stale or has already been used.");
        var actual = Snapshot();
        if (actual.Count != plan.Files.Length || plan.Files.Any(entry => !actual.TryGetValue(entry.Path, out var data) || Hash(data) != entry.BeforeHash))
            throw new IOException("Saved profiles changed after preview. No profiles were migrated.");
        // Verify every backup and target before changing even the first profile.
        foreach (var entry in plan.Files.Where(entry => entry.Join != null)) { Backup(plan, entry, false); Backup(plan, entry, true); }
        Save(plan with { State = "Applying" });
        var written = new List<Entry>();
        try
        {
            foreach (var entry in plan.Files.Where(entry => entry.Join != null))
            {
                BeforeWrite?.Invoke(entry.Path);
                string path = FilePath(entry.Path);
                if (Hash(File.ReadAllBytes(path)) != entry.BeforeHash) throw new IOException("A profile changed during migration: " + entry.Path);
                Replace(path, Backup(plan, entry, true));
                written.Add(entry);
                if (Hash(File.ReadAllBytes(path)) != entry.AfterHash) throw new IOException("A migrated profile was changed externally: " + entry.Path);
            }
            Save(plan with { State = "Completed" });
            return new(true, Folder(preview.Id));
        }
        catch (Exception error)
        {
            var conflicts = new List<string>();
            foreach (var entry in written.AsEnumerable().Reverse())
            {
                try
                {
                    string path = FilePath(entry.Path);
                    if (Hash(File.ReadAllBytes(path)) != entry.AfterHash) { conflicts.Add(entry.Path); continue; }
                    Replace(path, Backup(plan, entry, false));
                }
                catch { conflicts.Add(entry.Path); }
            }
            Save(plan with { State = conflicts.Count == 0 ? "Restored" : "Conflict" });
            return new(false, Folder(preview.Id), error.Message + (conflicts.Count == 0 ? "" : " Conflicts retained: " + string.Join(", ", conflicts)));
        }
    }
}
