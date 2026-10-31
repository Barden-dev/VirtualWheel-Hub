using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace SimRacingHub.Avalonia.Adapters;

public interface IDesktopTray : IDisposable
{
    bool IsAvailable { get; }
    Task InitializeAsync(NativeMenu menu, Action restore, CancellationToken cancellationToken);
    void SetToolTip(string text);
}

public sealed class DesktopTray : IDesktopTray
{
    private TrayIcon? _icon;
    private TrayIcons? _icons;
    private bool _ready;
    public bool IsAvailable => _ready;

    public async Task InitializeAsync(NativeMenu menu, Action restore, CancellationToken cancellationToken)
    {
        // An exporter alone does not establish that a Linux panel can display the icon.
        if (OperatingSystem.IsLinux() && !await HasLinuxTrayHostAsync(cancellationToken)) return;
        cancellationToken.ThrowIfCancellationRequested();
        var app = global::Avalonia.Application.Current ?? throw new InvalidOperationException("Application is not initialized.");
        using var stream = typeof(App).Assembly.GetManifestResourceStream("SimRacingHub.TrayIcon.ico")
            ?? throw new InvalidOperationException("Tray icon resource is missing.");
        _icon = new TrayIcon { Icon = new WindowIcon(stream), Menu = menu, IsVisible = true };
        _icon.Clicked += (_, _) => restore();
        _icons = TrayIcon.GetIcons(app);
        if (_icons == null) { _icons = new TrayIcons(); TrayIcon.SetIcons(app, _icons); }
        _icons.Add(_icon);
        _ready = _icon.NativeMenuExporter != null;
    }

    public void SetToolTip(string text) { if (_icon != null) _icon.ToolTipText = text; }

    private static async Task<bool> HasLinuxTrayHostAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1.5));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dbus-send")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
        };
        foreach (string argument in new[] { "--session", "--dest=org.freedesktop.DBus", "--type=method_call", "--print-reply",
            "/org/freedesktop/DBus", "org.freedesktop.DBus.NameHasOwner", "string:org.kde.StatusNotifierWatcher" })
            process.StartInfo.ArgumentList.Add(argument);
        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode == 0 && (await output.ConfigureAwait(false)).Contains("boolean true", StringComparison.Ordinal)
                && (await error.ConfigureAwait(false)).Length == 0;
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    public void Dispose()
    {
        _ready = false;
        if (_icon == null) return;
        _icons?.Remove(_icon);
        _icon.Dispose();
        _icon = null;
        _icons = null;
    }
}
