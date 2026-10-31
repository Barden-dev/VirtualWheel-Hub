using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimRacingHub.Application.Contracts;
using SimRacingHub.Application.Features.InputDevices;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;

namespace SimRacingHub.Avalonia.Views.InputDevices;

public partial class InputDevicesView : UserControl, IPendingSettingsChanges
{
    private InputDevicesViewModel? _viewModel;
    private AppShellViewModel? _shell;
    private bool _attached;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly List<Visual> _ancestors = new();

    // Compatibility for existing callers; state and application-wide saves belong to the model.
    public event Action? Changed;
    public bool HasChanges => _viewModel?.HasChanges == true;
    public bool CanSave => _viewModel?.CanSave == true;
    public bool IsBusy => _viewModel?.IsBusy == true;
    public bool HasError => _viewModel?.HasError == true;
    public Task<bool> SaveAsync() => _viewModel?.SaveAsync() ?? Task.FromResult(false);
    public void Revert() => _viewModel?.Revert();

    public InputDevicesView()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => _viewModel?.Poll();
        PropertyChanged += VisibilityChanged;
    }

    public InputDevicesView(InputDevicesViewModel viewModel) : this() => Connect(viewModel);

    public InputDevicesView(IInputDeviceSelectionService devices, IRuntimeSession runtime,
        ILocalizationManager language, Func<IDisposable?>? suspend = null)
        : this(new InputDevicesViewModel(devices, runtime, language, suspend)) => _viewModel!.RefreshDevices();

    private void Connect(InputDevicesViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        foreach (var parent in this.GetVisualAncestors()) { _ancestors.Add(parent); parent.PropertyChanged += VisibilityChanged; }
        if (_viewModel == null)
        {
            if (DataContext is InputDevicesViewModel supplied) Connect(supplied);
            else if (App.Services is { } services) Connect(services.InputDevices);
        }
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ModelChanged;
            _viewModel.Changed += PendingChanged;
            _viewModel.Activate(IsEffectivelyVisible);
        }
        _shell = (TopLevel.GetTopLevel(this) as Window)?.DataContext as AppShellViewModel;
        _shell?.RegisterPendingSettings(_viewModel);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _timer.Stop();
        if (_viewModel != null)
        {
            _viewModel.Deactivate();
            _viewModel.PropertyChanged -= ModelChanged;
            _viewModel.Changed -= PendingChanged;
        }
        _shell?.RegisterPendingSettings(null); _shell = null;
        foreach (var parent in _ancestors) parent.PropertyChanged -= VisibilityChanged;
        _ancestors.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty) _viewModel?.SetVisible(IsEffectivelyVisible);
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InputDevicesViewModel.IsPolling)) UpdateTimer();
    }

    private void UpdateTimer() => _timer.IsEnabled = _attached && _viewModel?.IsPolling == true;
    private void PendingChanged() => Changed?.Invoke();
}
