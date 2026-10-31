using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace SimRacingHub.Core;

/// <summary>Known descriptors produced offline from the pinned, MIT-licensed vJoy generator.</summary>
internal static class VJoyWheelPreset
{
    internal const string OperationValue = "vWheelHubLastOperation";
    internal const string OwnerValue = "vWheelHubCreationOperation";
    private const string AssetHash = "13F4B84ADA472D97B57917DB49FF2FE96AC7A4C9BB999D3241D977DB52486DDF";
    private sealed record Layout(int DeviceId, int DiscretePovs, byte[] Descriptor, string Sha256);
    private sealed record Presets(int SchemaVersion, string SourceRevision, Layout[] Layouts);
    private static readonly Lazy<Presets> Preset = new(() =>
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "vJoy", "wheel-presets.json"));
        if (VJoyRecoveryFiles.Hash(bytes) != AssetHash) throw new InvalidDataException("vJoy wheel preset checksum failed.");
        var presets = JsonSerializer.Deserialize<Presets>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing vJoy wheel presets.");
        if (presets.SchemaVersion != 1 || presets.SourceRevision != "8111294ef89b25d35fa2617a62ce96d23275ab16" || presets.Layouts.Length != 64 ||
            presets.Layouts.Any(layout => layout.DeviceId is < 1 or > 16 || layout.DiscretePovs is < 1 or > 4 ||
                VJoyRecoveryFiles.Hash(layout.Descriptor) != layout.Sha256) ||
            presets.Layouts.Select(layout => (layout.DeviceId, layout.DiscretePovs)).Distinct().Count() != 64)
            throw new InvalidDataException("Invalid vJoy wheel preset inventory.");
        return presets;
    });

    internal static byte[] Descriptor(int deviceId, int discretePovs) =>
        Preset.Value.Layouts.Single(layout => layout.DeviceId == deviceId && layout.DiscretePovs == discretePovs).Descriptor.ToArray();

    internal static VJoyConfigurationSnapshot Build(VJoyConfigurationSnapshot before, int discretePovs, string operationId, bool creation)
    {
        before.Validate();
        if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidDataException("Invalid vJoy setup operation ID.");
        byte[] descriptor = Descriptor((int)before.DeviceId, discretePovs);
        byte[] size = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)descriptor.Length);
        var values = before.Device?.Values.ToList() ?? new();
        void Set(string name, uint type, byte[] data)
        {
            int index = values.FindIndex(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) values[index] = values[index] with { Type = type, Data = data };
            else values.Add(new(name, type, data));
        }
        Set("HidReportDesctiptor", 3, descriptor);
        Set("HidReportDesctiptorSize", 4, size);
        Set(OperationValue, 1, Encoding.Unicode.GetBytes(operationId + "\0"));
        if (creation)
        {
            if (before.Device != null || before.ForceFeedbackDevice != null) throw new InvalidDataException("Cannot claim an existing vJoy configuration as a newly created device.");
            Set(OwnerValue, 1, Encoding.Unicode.GetBytes(operationId + "\0"));
        }
        return before with { Device = new(values.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(), before.Device?.Children ?? new()) };
    }

    internal static string? Token(VJoyConfigurationSnapshot snapshot, string name)
    {
        var value = snapshot.Device?.Values.SingleOrDefault(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (value?.Type != 1 || value.Data.Length != 66 || value.Data[^1] != 0 || value.Data[^2] != 0) return null;
        string token = Encoding.Unicode.GetString(value.Data, 0, value.Data.Length - 2);
        return Guid.TryParseExact(token, "N", out _) ? token : null;
    }

    internal static bool MatchesPlan(VJoyChangeRecord record, VJoyConfigurationSnapshot before)
    {
        if (record.Expected == null || record.Purpose is not ("wheel-create" or "wheel-reconfigure")) return false;
        bool creation = record.Purpose == "wheel-create";
        if (creation && (record.BeforeDeviceStatus != VJoyWrapper.VJD_STAT_MISS || before.Device != null || before.ForceFeedbackDevice != null)) return false;
        if (!creation && record.BeforeDeviceStatus != VJoyWrapper.VJD_STAT_FREE) return false;
        for (int povs = 1; povs <= 4; povs++)
            if (Build(before, povs, record.Id, creation).Fingerprint() == record.Expected.Fingerprint()) return true;
        return false;
    }

}
