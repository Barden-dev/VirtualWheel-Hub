using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace SimRacingHub.Core;

// Registry values are stored as their original type and bytes. In particular,
// REG_EXPAND_SZ is never expanded and HID descriptors are never reconstructed.
internal sealed record VJoyRegistryValue(string Name, uint Type, byte[] Data);
internal sealed record VJoyRegistryTree(VJoyRegistryValue[] Values, Dictionary<string, VJoyRegistryTree> Children);
internal sealed record VJoyDriverIdentity(short Version, string BinaryPath, string BinaryVersion, string BinarySha256);
internal sealed record VJoyConfigurationSnapshot(
    uint DeviceId, VJoyDriverIdentity Driver, VJoyRegistryTree? Device, VJoyRegistryTree? ForceFeedbackDevice)
{
    internal string Fingerprint()
    {
        static VJoyRegistryTree? Sort(VJoyRegistryTree? tree) => tree == null ? null : new(
            tree.Values.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(),
            tree.Children.OrderBy(child => child.Key, StringComparer.Ordinal).ToDictionary(child => child.Key, child => Sort(child.Value)!));
        return VJoyRecoveryFiles.Hash(JsonSerializer.SerializeToUtf8Bytes(this with { Device = Sort(Device), ForceFeedbackDevice = Sort(ForceFeedbackDevice) }));
    }

    internal void Validate()
    {
        if (DeviceId is < 1 or > 16 || Driver == null || Driver.Version == 0 ||
            string.IsNullOrWhiteSpace(Driver.BinaryPath) || !VJoyRecoveryFiles.IsHash(Driver.BinarySha256))
            throw new InvalidDataException("Invalid vJoy device or driver identity in backup.");
        int nodes = 0;
        void Visit(VJoyRegistryTree? tree, int depth)
        {
            if (tree == null) return;
            if (depth > 32 || ++nodes > 4096 || tree.Values == null || tree.Children == null)
                throw new InvalidDataException("Invalid vJoy registry tree in backup.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in tree.Values)
                if (value == null || value.Name == null || value.Name.Contains('\0') || value.Data == null || !names.Add(value.Name))
                    throw new InvalidDataException("Invalid or duplicate vJoy registry value.");
            names.Clear();
            foreach (var child in tree.Children)
            {
                if (string.IsNullOrWhiteSpace(child.Key) || child.Key.IndexOfAny(['\\', '/', '\0']) >= 0 || !names.Add(child.Key) || child.Value == null)
                    throw new InvalidDataException("Invalid vJoy registry child key.");
                Visit(child.Value, depth + 1);
            }
        }
        Visit(Device, 0);
        Visit(ForceFeedbackDevice, 0);
    }
}

internal interface IVJoyConfigurationStore
{
    void ValidateProposed(VJoyConfigurationSnapshot proposed) { }
    VJoyConfigurationSnapshot Read(uint deviceId);
    int GetStatus(uint deviceId);
    // Recheck the full snapshot and occupancy immediately before writing.
    // Implementations must only touch this device's explicitly captured keys.
    bool TryReplace(VJoyConfigurationSnapshot expected, VJoyConfigurationSnapshot replacement);
    bool TryReplace(VJoyConfigurationSnapshot expected, VJoyConfigurationSnapshot replacement, string operationId, bool restoring = false) =>
        TryReplace(expected, replacement);
    bool HasWriteReceipt(string operationId, string beforeHash, string afterHash, bool restoring = false) => false;
}

internal static class VJoyRecoveryFiles
{
    private const int MaxBytes = 16 * 1024 * 1024;
    private sealed record Envelope<T>(int Version, string Sha256, T Data);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    internal static T Read<T>(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxBytes) throw new InvalidDataException("Missing or oversized vJoy recovery file: " + path);
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Oversized vJoy recovery file: " + path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var data = root.GetProperty("Data");
        // Verify the exact saved payload before deserializing. Re-serializing a
        // new model would add newly introduced default fields and invalidate
        // backups created by an older application version.
        if (root.GetProperty("Version").GetInt32() != 1 || data.ValueKind == JsonValueKind.Null ||
            !string.Equals(root.GetProperty("Sha256").GetString(), Hash(Encoding.UTF8.GetBytes(data.GetRawText())), StringComparison.Ordinal))
            throw new InvalidDataException("vJoy recovery file checksum failed: " + path);
        return data.Deserialize<T>() ?? throw new InvalidDataException("Empty vJoy recovery file: " + path);
    }

    internal static void Write<T>(string path, T value, bool replace = false)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(1, Hash(payload), value));
        if (bytes.Length > MaxBytes) throw new InvalidDataException("vJoy recovery file is too large.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            _ = Read<T>(temporary);
            File.Move(temporary, path, overwrite: replace);
            _ = Read<T>(path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
