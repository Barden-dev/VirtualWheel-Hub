using System.Runtime.InteropServices;
using SimRacingHub.Application.Runtime;

namespace SimRacingHub.Windows;

/// <summary>Finite UI tones, independent of the driving synthesizer and its live buffer.</summary>
public sealed class WindowsAssignmentSound : IOutputTestSound
{
    private static readonly SemaphoreSlim PlaybackGate = new(1, 1);
    private volatile bool _failed;
    public bool IsAvailable => OperatingSystem.IsWindows() && !_failed;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] wave, IntPtr module, uint flags);

    public async Task PlayAsync(int frequency, int duration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsAvailable) return;
        await PlaybackGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wave = CreateWave(frequency, duration);
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                // Synchronous memory playback keeps the buffer alive until its fade-out finishes.
                // No reset at a wall-clock deadline: it could truncate an audio device's buffer.
                if (!PlaySound(wave, IntPtr.Zero, 0x0004 | 0x0002))
                {
                    _failed = true;
                    throw new InvalidOperationException("Windows could not play the assignment cue.");
                }
            }, ct).ConfigureAwait(false);
        }
        finally { PlaybackGate.Release(); }
    }

    public static byte[] CreateWave(int frequency, int duration)
    {
        const int rate = 48000;
        int frames = rate * Math.Clamp(duration, 10, 500) / 1000;
        frequency = Math.Clamp(frequency, 100, 2000);
        using var stream = new MemoryStream(44 + frames * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(frames * 2);
        int attack = Math.Min(frames / 3, rate * 12 / 1000);
        int release = Math.Min(frames / 3, rate * 20 / 1000);
        for (int i = 0; i < frames; i++)
        {
            double fade = Math.Min(1, Math.Min(i / (double)attack, (frames - 1 - i) / (double)release));
            double envelope = .5 - .5 * Math.Cos(Math.PI * fade);
            writer.Write((short)Math.Round(2200 * envelope * Math.Sin(2 * Math.PI * frequency * i / rate)));
        }
        return stream.ToArray();
    }
}
