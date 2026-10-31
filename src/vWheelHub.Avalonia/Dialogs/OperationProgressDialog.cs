using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SimRacingHub.Application.Contracts;

namespace SimRacingHub.Avalonia.Dialogs;

/// <summary>Silent progress window; closes with the operation, never with the timer.</summary>
public sealed class OperationProgressDialog : Window
{
    private readonly TextBlock _countdown;
    private readonly ProgressBar _progress;
    private readonly DialogProgressRequest _request;
    private bool _completed;

    public OperationProgressDialog(DialogProgressRequest request)
    {
        _request = request;
        Title = request.Title;
        Width = 480;
        MinWidth = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("ControlFillColorDefaultBrush"));
        this.Bind(ForegroundProperty, this.GetResourceObservable("TextFillColorPrimaryBrush"));
        _countdown = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14 };
        _progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, request.CountdownSeconds), Height = 5 };
        Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 16,
            Children = {
                new TextBlock { Text = request.Title, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap },
                _countdown, _progress }
        };
        UpdateElapsed(TimeSpan.Zero);
        Closing += (_, e) => { if (!_completed) e.Cancel = true; };
    }

    public void UpdateElapsed(TimeSpan elapsed)
    {
        int remaining = Math.Max(0, _request.CountdownSeconds - (int)Math.Floor(Math.Max(0, elapsed.TotalSeconds)));
        _countdown.Text = remaining > 0 ? string.Format(_request.CountdownFormat, remaining) : _request.ElapsedMessage;
        _progress.Value = Math.Min(elapsed.TotalSeconds, _progress.Maximum);
        _progress.IsIndeterminate = remaining == 0;
    }

    public static async Task<bool> RunAsync(Window owner, DialogProgressRequest request,
        Func<CancellationToken, Task<bool>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dialog = new OperationProgressDialog(request);
        Exception? failure = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        dialog.Opened += async (_, _) =>
        {
            var watch = Stopwatch.StartNew();
            timer.Tick += (_, _) => dialog.UpdateElapsed(watch.Elapsed);
            timer.Start();
            bool result = false;
            try { result = await Task.Run(() => operation(ct), ct).ConfigureAwait(false); }
            catch (Exception ex) { failure = ex; }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    timer.Stop();
                    dialog._completed = true;
                    dialog.Close(result);
                });
            }
        };
        bool completed = await dialog.ShowDialog<bool>(owner);
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return completed;
    }
}
