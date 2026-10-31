using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace SimRacingHub.Core;

internal sealed class WindowsVJoyConfigurationStore : IVJoyConfigurationStore
{
    private const string ParametersPath = @"SYSTEM\CurrentControlSet\services\vjoy\Parameters";
    private const int ReadAccess = 0x20019;
    private const int WriteAccess = 0xF003F;
    private readonly RegistryHive _hive;
    private readonly string _parametersPath;
    private readonly Func<VJoyDriverIdentity> _driver;
    private readonly Func<uint, int> _status;

    internal WindowsVJoyConfigurationStore() : this(ReadDriver, VJoyWrapper.GetDeviceStatus, RegistryHive.LocalMachine, ParametersPath) { }

    // Internal seam for integration tests against a unique HKCU fixture. The
    // application's recovery command never accepts registry paths or hives.
    internal WindowsVJoyConfigurationStore(Func<VJoyDriverIdentity> driver, Func<uint, int> status, RegistryHive hive, string parametersPath)
    {
        _driver = driver;
        _status = status;
        _hive = hive;
        _parametersPath = parametersPath;
    }

    public int GetStatus(uint deviceId) => _status(deviceId);

    public void ValidateProposed(VJoyConfigurationSnapshot proposed) =>
        VJoyReportLayout.Validate(proposed,Enumerable.Range(1,16).Where(id=>id!=proposed.DeviceId).Select(id=>Read((uint)id)));

    public VJoyConfigurationSnapshot Read(uint deviceId)
    {
        ValidateId(deviceId);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("vJoy configuration is only available on Windows.");
        VJoyConfigurationSnapshot Capture()
        {
            using var machine = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            using var parameters = Open(machine, _parametersPath, null, ReadAccess)
                ?? throw new IOException("vJoy configuration registry layout is unavailable.");
            var result = CaptureDevice(parameters, deviceId, _driver(), null);
            result.Validate();
            return result;
        }
        var first = Capture();
        var second = Capture();
        if (first.Fingerprint() != second.Fingerprint())
            throw new IOException("vJoy configuration changed while its backup was being read. Try again.");
        return second;
    }

    private static VJoyDriverIdentity ReadDriver()
    {
        if (!VJoyWrapper.IsDriverEnabled()) throw new IOException("vJoy driver is unavailable or disabled.");
        return ReadDriverBinary(VJoyWrapper.GetDriverVersion());
    }

    internal static VJoyDriverIdentity ReadDriverBinary(short version)
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var service = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\vjoy", writable: false);
        string path = service?.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string
            ?? throw new IOException("vJoy driver binary path is unavailable.");
        path = Environment.ExpandEnvironmentVariables(path.Trim('"'));
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(windows, path[12..]);
        if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase)) path = path[4..];
        if (!Path.IsPathRooted(path)) path = Path.Combine(windows, path);
        path = Path.GetFullPath(path);
        using var binary = File.OpenRead(path);
        return new VJoyDriverIdentity(version, path,
            System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion ?? "",
            Convert.ToHexString(SHA256.HashData(binary)));
    }

    public bool TryReplace(VJoyConfigurationSnapshot expected, VJoyConfigurationSnapshot replacement)
        => TryReplaceCore(expected, replacement, null, false);

    public bool TryReplace(VJoyConfigurationSnapshot expected, VJoyConfigurationSnapshot replacement, string operationId, bool restoring = false)
        => TryReplaceCore(expected, replacement, operationId, restoring);

    private static string ReceiptName(string operationId, bool restoring)
    {
        if (!Guid.TryParseExact(operationId, "N", out _)) throw new InvalidDataException("Invalid vJoy receipt ID.");
        return "vWheelHubJournal_" + operationId + (restoring ? "_restore" : "_apply");
    }

    public bool HasWriteReceipt(string operationId, string beforeHash, string afterHash, bool restoring = false)
    {
        string name = ReceiptName(operationId, restoring);
        using var machine = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
        using var parameters = Open(machine, _parametersPath, null, ReadAccess)
            ?? throw new IOException("vJoy configuration registry layout is unavailable.");
        if (!parameters.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        var value = ReadValue(parameters, name);
        return value.Type == 3 && value.Data.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(beforeHash + ":" + afterHash));
    }

    private bool TryReplaceCore(VJoyConfigurationSnapshot expected, VJoyConfigurationSnapshot replacement, string? operationId, bool restoring)
    {
        if (operationId != null) _ = ReceiptName(operationId, restoring);
        expected.Validate();
        replacement.Validate();
        if (expected.DeviceId != replacement.DeviceId || expected.Driver != replacement.Driver)
            throw new InvalidDataException("Cannot restore a different vJoy device or driver.");
        bool StatusAllowsWrite(int status) => CanWrite(status) && (operationId == null || restoring || replacement.Device == null ||
            status == (VJoyWheelPreset.Token(replacement, VJoyWheelPreset.OwnerValue) == operationId ? VJoyWrapper.VJD_STAT_MISS : VJoyWrapper.VJD_STAT_FREE));
        if (!StatusAllowsWrite(GetStatus(expected.DeviceId)) || _driver() != expected.Driver) return false;

        // A plain read/compare/write has a race with other configurators. Keep
        // every key operation in a registry transaction, including subkeys.
        // Unsupported TxR or insufficient privileges fail without a fallback.
        using var transaction = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 10000, "vWheel Hub vJoy recovery");
        if (transaction.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        bool committed = false;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            using var parameters = Open(machine, _parametersPath, transaction, WriteAccess)
                ?? throw new IOException("vJoy configuration registry layout is unavailable.");
            var current = CaptureDevice(parameters, expected.DeviceId, _driver(), transaction);
            if (current.Fingerprint() != expected.Fingerprint()) return false;
            if(operationId!=null && !restoring && VJoyWheelPreset.Token(replacement,VJoyWheelPreset.OperationValue)==operationId)
                VJoyReportLayout.Validate(replacement,Enumerable.Range(1,16).Where(id=>id!=expected.DeviceId)
                    .Select(id=>CaptureDevice(parameters,(uint)id,expected.Driver,transaction)));
            if (operationId != null)
            {
                string receipt = ReceiptName(operationId, restoring);
                // A receipt is a single-use commit token, never overwritten.
                if (parameters.GetValueNames().Contains(receipt, StringComparer.OrdinalIgnoreCase)) return false;
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(expected.Fingerprint() + ":" + replacement.Fingerprint());
                Check(RegSetValueEx(parameters.Handle, receipt, 0, 3, bytes, (uint)bytes.Length));
            }
            Sync(parameters, $"Device{expected.DeviceId:00}", replacement.Device, transaction);
            Sync(parameters, $"FFBDevice{expected.DeviceId:00}", replacement.ForceFeedbackDevice, transaction);
            if (!StatusAllowsWrite(GetStatus(expected.DeviceId)) || _driver() != expected.Driver) return false;
            if (!CommitTransaction(transaction)) throw new Win32Exception(Marshal.GetLastWin32Error());
            committed = true;
            return true;
        }
        finally { if (!committed) RollbackTransaction(transaction); }
    }

    private static bool CanWrite(int status) => status is VJoyWrapper.VJD_STAT_FREE or VJoyWrapper.VJD_STAT_MISS;
    private static void ValidateId(uint id)
    {
        if (id is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(id));
    }

    private static VJoyConfigurationSnapshot CaptureDevice(RegistryKey parameters, uint id, VJoyDriverIdentity driver, SafeFileHandle? tx) =>
        new(id, driver, ReadTree(parameters, $"Device{id:00}", tx), ReadTree(parameters, $"FFBDevice{id:00}", tx));

    private static VJoyRegistryTree? ReadTree(RegistryKey parent, string name, SafeFileHandle? tx, int depth = 0)
    {
        if (depth > 32) throw new IOException("vJoy registry tree is too deep.");
        using var key = Open(parent, name, tx, ReadAccess);
        if (key == null) return null;
        var values = key.GetValueNames().OrderBy(x => x, StringComparer.Ordinal).Select(valueName => ReadValue(key, valueName)).ToArray();
        if (values.Any(value => value.Type == 6)) throw new IOException("Registry links are unsupported in vJoy configuration.");
        var children = new Dictionary<string, VJoyRegistryTree>(StringComparer.Ordinal);
        foreach (string child in key.GetSubKeyNames().OrderBy(x => x, StringComparer.Ordinal))
            children.Add(child, ReadTree(key, child, tx, depth + 1) ?? throw new IOException("vJoy registry key disappeared during capture."));
        return new VJoyRegistryTree(values, children);
    }

    private static VJoyRegistryValue ReadValue(RegistryKey key, string name)
    {
        uint size = 0;
        Check(RegQueryValueEx(key.Handle, name, IntPtr.Zero, out _, null, ref size));
        if (size > 8 * 1024 * 1024) throw new IOException("Oversized vJoy registry value.");
        byte[] bytes = new byte[size];
        Check(RegQueryValueEx(key.Handle, name, IntPtr.Zero, out uint type, bytes, ref size));
        if (bytes.Length != size) Array.Resize(ref bytes, checked((int)size));
        return new VJoyRegistryValue(name, type, bytes);
    }

    private static RegistryKey? Open(RegistryKey parent, string name, SafeFileHandle? tx, int access)
    {
        // OPEN_LINK prevents a configured child key from redirecting recovery
        // outside the two captured device trees.
        int error = tx == null
            ? RegOpenKeyEx(parent.Handle, name, 8, access, out var handle)
            : RegOpenKeyTransacted(parent.Handle, name, 8, access, out handle, tx, IntPtr.Zero);
        if (error == 2) { handle?.Dispose(); return null; }
        if (error != 0) { handle?.Dispose(); Check(error); }
        return RegistryKey.FromHandle(handle!, RegistryView.Registry64);
    }

    private static void Sync(RegistryKey parent, string name, VJoyRegistryTree? tree, SafeFileHandle tx)
    {
        if (tree == null)
        {
            using (var existing = Open(parent, name, tx, WriteAccess))
            {
                if (existing == null) return;
                foreach (var child in existing.GetSubKeyNames()) Sync(existing, child, null, tx);
            }
            Check(RegDeleteKeyTransacted(parent.Handle, name, 0, 0, tx, IntPtr.Zero));
            return;
        }
        RegistryKey? opened = Open(parent, name, tx, WriteAccess);
        if (opened == null)
        {
            Check(RegCreateKeyTransacted(parent.Handle, name, 0, null, 0, WriteAccess, IntPtr.Zero,
                out var handle, out _, tx, IntPtr.Zero));
            opened = RegistryKey.FromHandle(handle, RegistryView.Registry64);
        }
        using var key = opened;
        foreach (var value in key.GetValueNames())
            if (!tree.Values.Any(item => string.Equals(item.Name, value, StringComparison.OrdinalIgnoreCase)))
                Check(RegDeleteValue(key.Handle, value));
        foreach (var value in tree.Values)
        {
            if (value.Type == 6) throw new InvalidDataException("Cannot write a registry link.");
            Check(RegSetValueEx(key.Handle, value.Name, 0, value.Type, value.Data, (uint)value.Data.Length));
        }
        foreach (var child in key.GetSubKeyNames())
            if (!tree.Children.Keys.Contains(child, StringComparer.OrdinalIgnoreCase)) Sync(key, child, null, tx);
        foreach (var child in tree.Children) Sync(key, child.Key, child.Value, tx);
    }

    private static void Check(int error) { if (error != 0) throw new Win32Exception(error); }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW")]
    private static extern int RegQueryValueEx(SafeRegistryHandle key, string name, IntPtr reserved, out uint type, byte[]? data, ref uint size);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
    private static extern int RegOpenKeyEx(SafeRegistryHandle key, string name, int options, int access, out SafeRegistryHandle handle);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyTransactedW")]
    private static extern int RegOpenKeyTransacted(SafeRegistryHandle key, string name, int options, int access, out SafeRegistryHandle handle, SafeFileHandle transaction, IntPtr extended);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegCreateKeyTransactedW")]
    private static extern int RegCreateKeyTransacted(SafeRegistryHandle key, string name, int reserved, string? keyClass, int options, int access, IntPtr security, out SafeRegistryHandle handle, out int disposition, SafeFileHandle transaction, IntPtr extended);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegDeleteKeyTransactedW")]
    private static extern int RegDeleteKeyTransacted(SafeRegistryHandle key, string name, int access, int reserved, SafeFileHandle transaction, IntPtr extended);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegSetValueExW")]
    private static extern int RegSetValueEx(SafeRegistryHandle key, string name, int reserved, uint type, byte[] data, uint size);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegDeleteValueW")]
    private static extern int RegDeleteValue(SafeRegistryHandle key, string name);
    [DllImport("KtmW32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateTransaction(IntPtr attributes, IntPtr id, int options, int isolation, int isolationFlags, int timeout, string description);
    [DllImport("KtmW32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CommitTransaction(SafeFileHandle transaction);
    [DllImport("KtmW32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RollbackTransaction(SafeFileHandle transaction);
}
