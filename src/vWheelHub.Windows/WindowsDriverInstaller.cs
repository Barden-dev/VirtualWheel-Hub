using System.Diagnostics;
using System.Security.Cryptography;
using SimRacingHub.Models;

namespace SimRacingHub.Windows;

/// <summary>Only reviewed, version-pinned publisher assets may be elevated.</summary>
public sealed class WindowsDriverInstaller
{
    public sealed record Package(string Url, string Sha256);
    public static Package GetPackage(OutputDeviceMode mode) => mode switch
    {
        OutputDeviceMode.VJoy => new("https://github.com/jshafer817/vJoy/releases/download/v2.1.9.1/vJoySetup.exe", "F103CED4E7FF7CCB49C8415A542C56768ED4DA4FEA252B8F4FFDAC343074654A"),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
    public static bool Verify(ReadOnlySpan<byte> bytes, Package package) =>
        Convert.ToHexString(SHA256.HashData(bytes)).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase);

    public async Task<int> InstallAsync(OutputDeviceMode mode, CancellationToken ct)
    {
        var package = GetPackage(mode);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        byte[] bytes = await http.GetByteArrayAsync(package.Url, ct).ConfigureAwait(false);
        if (!Verify(bytes, package)) throw new SimRacingHub.Application.Contracts.GameSetupException("Prep_Error_Integrity", package.Url);
        string directory = Path.Combine(Path.GetTempPath(), "vWheelHub-driver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "Setup.exe");
        try
        {
            await File.WriteAllBytesAsync(file, bytes, ct).ConfigureAwait(false);
            using var installer = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "runas" })
                ?? throw new IOException("Driver installer could not be started.");
            // The installer owns its cancellation and reboot UI; never kill it.
            await installer.WaitForExitAsync().ConfigureAwait(false);
            return installer.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new SimRacingHub.Application.Contracts.GameSetupException("Prep_Error_AuthorizationDenied", ex.Message, ex);
        }
        finally
        {
            try { File.Delete(file); Directory.Delete(directory); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            { SimRacingHub.Core.AppLogger.Instance.LogWarning("Driver installer temporary-file cleanup: " + cleanup.Message); }
        }
    }
}
