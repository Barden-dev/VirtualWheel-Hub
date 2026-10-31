using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters;

public interface IDesktopSoundPlayer : IDisposable
{
    void PlayMouseLock(bool locked);
}

/// <summary>Short desktop notifications, independent of the driving audio queue and profile volume.</summary>
public sealed class DesktopSoundPlayer : IDesktopSoundPlayer
{
    private readonly Channel<bool> _pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private string? _directory;
    private bool _warned;
    private int _disposed;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] wave, IntPtr module, uint flags);

    public DesktopSoundPlayer() => _worker = Task.Run(RunAsync);
    public void PlayMouseLock(bool locked) { if (Volatile.Read(ref _disposed) == 0) _pending.Writer.TryWrite(locked); }

    private async Task RunAsync()
    {
        try
        {
            await foreach (bool locked in _pending.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    bool played = OperatingSystem.IsWindows()
                        ? PlaySound(CreateWave(locked), IntPtr.Zero, 0x0004 | 0x0002) // synchronous memory WAV, no default fallback
                        : OperatingSystem.IsLinux() && await PlayLinuxAsync(locked).ConfigureAwait(false);
                    if (!played && !_warned)
                    {
                        _warned = true;
                        AppLogger.Instance.LogInfo("Mouse lock notification sound is unavailable on this audio output.", showInStatusBar: false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (!_warned) { _warned = true; AppLogger.Instance.LogError("Mouse lock notification sound failed", ex); }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // Two soft notes: rising for lock, falling for unlock. Each note has a 5 ms fade.
    public static byte[] CreateWave(bool locked)
    {
        const int rate = 48000, noteFrames = 2160, frames = noteFrames * 2;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(frames * 2 + 36); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
        writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(frames * 2);
        for (int i = 0; i < frames; i++)
        {
            int offset = i % noteFrames;
            double frequency = i < noteFrames ? 660 : locked ? 880 : 440;
            double envelope = Math.Min(1, Math.Min(offset, noteFrames - offset - 1) / 240.0);
            writer.Write((short)Math.Round(3000 * envelope * Math.Sin(2 * Math.PI * frequency * offset / rate)));
        }
        return stream.ToArray();
    }

    private async Task<bool> PlayLinuxAsync(bool locked)
    {
        _directory ??= Path.Combine(Path.GetTempPath(), "vWheelHub-notifications-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        string wavePath = Path.Combine(_directory, locked ? "lock.wav" : "unlock.wav");
        if (!File.Exists(wavePath)) await File.WriteAllBytesAsync(wavePath, CreateWave(locked), _stop.Token).ConfigureAwait(false);
        foreach (string name in new[] { "paplay", "pw-play", "aplay" })
        {
            string? executable = FindExecutable(name);
            if (executable == null) continue;
            using var process = new Process { StartInfo = new ProcessStartInfo(executable)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true } };
            process.StartInfo.ArgumentList.Add(wavePath);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                process.Start();
                var error = process.StandardError.ReadToEndAsync(timeout.Token);
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(error, output).ConfigureAwait(false);
                if (process.ExitCode == 0) return true;
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                _stop.Token.ThrowIfCancellationRequested();
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        return false;
    }

    private static string? FindExecutable(string name)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _pending.Writer.TryComplete(); _stop.Cancel();
        _worker.GetAwaiter().GetResult(); _stop.Dispose();
        if (_directory != null)
        {
            try { File.Delete(Path.Combine(_directory, "lock.wav")); File.Delete(Path.Combine(_directory, "unlock.wav")); Directory.Delete(_directory); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
