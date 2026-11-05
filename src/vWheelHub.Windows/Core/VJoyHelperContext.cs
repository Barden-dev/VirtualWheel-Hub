using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SimRacingHub.Core;

/// <summary>
/// Journal/selection files belong to the requesting desktop user, including
/// over-the-shoulder UAC. Only the fixed vJoy registry/activation adapter elevates.
/// Never perform administrator file writes to a caller-supplied directory.
/// </summary>
internal static class VJoyHelperContext
{
    internal static int Run(int requesterPid, string directory,
        Func<string, Func<IVJoyConfigurationStore, IVJoyConfigurationStore>, int> action)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (requesterPid <= 0 || requesterPid == Environment.ProcessId)
            throw new InvalidDataException("Invalid setup requester process.");
        string root = ValidateDirectory(directory);
        using var elevated = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(elevated).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("The setup helper requires administrator permission.");
        using var process = OpenProcess(0x1000, false, requesterPid);
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var image = new StringBuilder(32768);
        int length = image.Capacity;
        if (!QueryFullProcessImageName(process, 0, image, ref length)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!string.Equals(Path.GetFullPath(image.ToString()), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The setup requester is not the running hub executable.");
        using var requester = OpenRequesterToken(process);
        return RunAsRequester(requester, elevated.AccessToken, root, action);
    }

    internal static string ValidateDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) ||
            directory.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidDataException("A local absolute requester data directory is required.");
        return Path.GetFullPath(directory);
    }

    internal static int RunAsRequester(SafeAccessTokenHandle requester, SafeAccessTokenHandle elevated, string directory,
        Func<string, Func<IVJoyConfigurationStore, IVJoyConfigurationStore>, int> action) =>
        WindowsIdentity.RunImpersonated(requester, () =>
            action(ValidateDirectory(directory), store => new PrivilegedStore(store, elevated)));

    private static SafeAccessTokenHandle OpenRequesterToken(SafeProcessHandle process)
    {
        // A process token is primary: impersonation needs QUERY | DUPLICATE,
        // not the additional impersonation-token access right.
        if (OpenProcessToken(process, 0x000A, out var token)) return token;
        int error = Marshal.GetLastWin32Error();
        token.Dispose();
        if (error != 5) throw new Win32Exception(error);
        // Reading another user's hub token may need this privilege. It is scoped
        // to token acquisition and restored before any journal/file processing.
        using var privilege = new DebugPrivilege();
        if (!OpenProcessToken(process, 0x000A, out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return token;
    }

    private sealed class PrivilegedStore(IVJoyConfigurationStore inner, SafeAccessTokenHandle token) : IVJoyConfigurationStore
    {
        private T Run<T>(Func<T> action) => WindowsIdentity.RunImpersonated(token, action);
        public VJoyConfigurationSnapshot Read(uint id) => Run(() => inner.Read(id));
        public int GetStatus(uint id) => Run(() => inner.GetStatus(id));
        public void ValidateProposed(VJoyConfigurationSnapshot desired) => Run(() => { inner.ValidateProposed(desired); return true; });
        public bool TryReplace(VJoyConfigurationSnapshot before, VJoyConfigurationSnapshot after) => Run(() => inner.TryReplace(before, after));
        public bool TryReplace(VJoyConfigurationSnapshot before, VJoyConfigurationSnapshot after, string id, bool restoring = false) =>
            Run(() => inner.TryReplace(before, after, id, restoring));
        public bool HasWriteReceipt(string id, string before, string after, bool restoring = false) =>
            Run(() => inner.HasWriteReceipt(id, before, after, restoring));
        public VJoyActivationResult Refresh(VJoyConfigurationSnapshot desired, bool restoring = false) => Run(() => inner.Refresh(desired, restoring));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Privileges { public uint Count; public Luid Luid; public uint Attributes; }
    private sealed class DebugPrivilege : IDisposable
    {
        private readonly SafeAccessTokenHandle _token;
        private Privileges _previous;
        internal DebugPrivilege()
        {
            using var self = Process.GetCurrentProcess();
            if (!OpenProcessToken(self.SafeHandle, 0x0028, out _token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!LookupPrivilegeValue(null, "SeDebugPrivilege", out var luid))
            { int error = Marshal.GetLastWin32Error(); _token.Dispose(); throw new Win32Exception(error); }
            var requested = new Privileges { Count = 1, Luid = luid, Attributes = 2 };
            if (!AdjustTokenPrivileges(_token, false, ref requested, Marshal.SizeOf<Privileges>(), out _previous, out _) || Marshal.GetLastWin32Error() != 0)
            { int error = Marshal.GetLastWin32Error(); _token.Dispose(); throw new Win32Exception(error); }
        }
        public void Dispose()
        {
            AdjustTokenPrivileges(_token, false, ref _previous, Marshal.SizeOf<Privileges>(), out _, out _);
            _token.Dispose();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, bool disable, ref Privileges state, int size, out Privileges previous, out int returned);
}
