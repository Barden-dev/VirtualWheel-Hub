using System.Runtime.InteropServices;

namespace SimRacingHub.Windows;

internal static class WindowsBootSession
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BootEnvironment
    {
        public Guid Identifier;
        public int FirmwareType;
        public ulong Flags;
    }
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int informationClass, out BootEnvironment information, uint size, out uint returnedSize);

    public static string? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return NtQuerySystemInformation(90, out var boot, (uint)Marshal.SizeOf<BootEnvironment>(), out _) == 0
            ? boot.Identifier.ToString("D") : null;
    }
}
