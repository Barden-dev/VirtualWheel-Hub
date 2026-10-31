using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimRacingHub.Avalonia.Adapters;

/// <summary>Per-user ownership and a bounded, activation-only channel to the existing window.</summary>
public sealed class DesktopInstance : IDisposable
{
    private readonly FileStream? _ownership;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _sync = new();
    private readonly Task _listener;
    private Action? _activate;
    private bool _pending, _disposed;
    public bool IsPrimary => _ownership != null;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    public DesktopInstance(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string identity = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        _pipeName = "vwheelhub-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        try { _ownership = new FileStream(Path.Combine(directory, "desktop.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { }
        _listener = IsPrimary ? Task.Run(ListenAsync) : Task.CompletedTask;
    }

    public void RegisterActivationHandler(Action activate)
    {
        bool pending;
        lock (_sync)
        {
            if (_disposed) return;
            _activate = activate;
            pending = _pending; _pending = false;
        }
        if (pending) activate();
    }

    public async Task<bool> ActivateExistingAsync(TimeSpan timeout)
    {
        if (IsPrimary) return false;
        using var deadline = new CancellationTokenSource(timeout);
        while (!deadline.IsCancellationRequested)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(deadline.Token).ConfigureAwait(false);
                byte[] pid = new byte[4];
                await client.ReadExactlyAsync(pid, deadline.Token).ConfigureAwait(false);
                int ownerPid = BitConverter.ToInt32(pid);
                if (ownerPid <= 0) return false;
                if (OperatingSystem.IsWindows()) AllowSetForegroundWindow((uint)ownerPid);
                await client.WriteAsync(new byte[] { 1 }, deadline.Token).ConfigureAwait(false);
                byte[] ack = new byte[1];
                await client.ReadExactlyAsync(ack, deadline.Token).ConfigureAwait(false);
                return ack[0] == 1;
            }
            catch (OperationCanceledException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException)
            {
                try { await Task.Delay(40, deadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return false; }
            }
        }
        return false;
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                await server.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), deadline.Token).ConfigureAwait(false);
                byte[] command = new byte[1];
                await server.ReadExactlyAsync(command, deadline.Token).ConfigureAwait(false);
                if (command[0] != 1) continue;
                Action? activate;
                lock (_sync)
                {
                    activate = _activate;
                    if (activate == null) _pending = true;
                }
                try { activate?.Invoke(); }
                catch (Exception ex) { SimRacingHub.Core.AppLogger.Instance.LogError("Could not restore the existing window", ex); }
                await server.WriteAsync(new byte[] { 1 }, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!_stop.IsCancellationRequested)
                    await Task.Delay(50, _stop.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync) { if (_disposed) return; _disposed = true; _activate = null; }
        _stop.Cancel();
        try { _listener.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _ownership?.Dispose();
        _stop.Dispose();
        // Keep the empty lock file: deleting it could let processes lock different inodes on Unix.
    }
}
