using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimRacingHub.Application.Localization;
using SimRacingHub.Application.Runtime;
using SimRacingHub.Application.Features.Output;
using SimRacingHub.Models;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Views.Output;

public partial class OutputTestView : UserControl
{
    private IRuntimeSession? _runtime;
    private IOutputTestSession? _test;
    private ILocalizationManager? _localization;
    private Func<OutputDeviceMode>? _readMode;
    private Func<IReadOnlyList<VirtualButtonBinding>>? _readButtons;
    private VirtualButtonBinding[] _displayedButtons = [];
    private bool _buttonsInitialized;
    private int _buttonCount;
    private sealed record ButtonChoice(VirtualButtonBinding Binding, string DisplayName)
    {
        public override string ToString() => $"{DisplayName} ({Binding.Button})";
    }
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly List<OutputTestAction> _actions = new();
    private OutputDeviceMode? _displayedMode;
    private string? _displayedCulture;
    private Window? _owner;
    private readonly List<Visual> _visibilityAncestors = new();
    private bool _startedRuntime;
    private bool _ownsTest;
    private long _testLifecycleVersion;
    private string? _completionMessage;
    private bool _busy;
    private string? _error;
    private IOutputTestSound? _audio;
    private CancellationTokenSource? _cueCancellation;
    private int _lastCountdownSecond = -1;
    private bool _audioStarted;

    public OutputTestView()
    {
        InitializeComponent();
        StartStopButton.IsEnabled = false;
        ModeBox.SelectionChanged += (_, _) =>
        {
            OnceHint.IsVisible = ModeBox.SelectedIndex == 0;
            RepeatHint.IsVisible = ModeBox.SelectedIndex == 1;
        };
        ActionBox.SelectionChanged += (_, _) => Refresh();
        ButtonChoiceBox.SelectionChanged += (_, _) => Refresh();
        StartStopButton.Click += async (_, _) =>
        {
            if (_busy || _runtime == null || _test == null || _readMode == null) return;
            if (_ownsTest && _test.OutputTestLifecycleVersion != _testLifecycleVersion)
            {
                _completionMessage = Text("OutputTest_ContextChanged");
                Stop(); Refresh(); return;
            }
            if (_test.IsOutputTestActive) { Stop(); Refresh(); return; }
            if (ActionBox.SelectedIndex < 0 || ActionBox.SelectedIndex >= _actions.Count) return;
            RefreshButtonChoices();
            var action = _actions[ActionBox.SelectedIndex];
            var choice = ButtonChoiceBox.SelectedItem as ButtonChoice;
            if (action == OutputTestAction.Button && choice == null) return;
            _busy = true;
            _error = null;
            _completionMessage = null;
            try
            {
                if (!_runtime.IsRunning)
                {
                    await _runtime.StartAsync(new RuntimeStartOptions(_readMode()) { SuppressDrivingOnStart = true });
                    _startedRuntime = true;
                }
                _testLifecycleVersion = _test.OutputTestLifecycleVersion;
                if (!IsEffectivelyVisible) { Stop(); return; }
                if (action == OutputTestAction.Button && !(_readButtons?.Invoke() ?? [])
                    .Any(binding => binding != null && binding.Button == choice!.Binding.Button && binding.Name == choice.Binding.Name))
                {
                    _completionMessage = Text("OutputTest_ContextChanged");
                    Stop(); return;
                }
                _ownsTest = true;
                _test.StartOutputTest(action, button: action == OutputTestAction.Button ? choice!.Binding.Button : 1, repeat: ModeBox.SelectedIndex == 1);
            }
            catch (Exception ex) { _error = string.Format(Text("OutputTest_Error"), ex.Message); Stop(); }
            finally { _busy = false; Refresh(); }
        };
        _timer.Tick += (_, _) => Refresh();
        PropertyChanged += VisibilityChanged;
    }

    public OutputTestView(IRuntimeSession runtime, ILocalizationManager localization, Func<OutputDeviceMode> readMode, IOutputTestSound? audio = null,
        Func<IReadOnlyList<VirtualButtonBinding>>? readButtons = null) : this()
        => Initialize(runtime, localization, readMode, audio, readButtons);

    private void Initialize(IRuntimeSession runtime, ILocalizationManager localization, Func<OutputDeviceMode> readMode, IOutputTestSound? audio,
        Func<IReadOnlyList<VirtualButtonBinding>>? readButtons)
    {
        _runtime = runtime;
        _test = runtime as IOutputTestSession ?? throw new ArgumentException("Runtime does not support output testing.");
        _localization = localization;
        _readMode = readMode;
        _readButtons = readButtons;
        _audio = audio;
        RefreshChoices();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = TopLevel.GetTopLevel(this) as Window;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _visibilityAncestors.Add(ancestor);
            ancestor.PropertyChanged += VisibilityChanged;
        }
        if (_owner != null) _owner.Closed += OwnerClosed;
        if (_runtime == null && App.Services is { IsPreviewMode: false } services && services.RuntimeSession is IOutputTestSession)
            Initialize(services.RuntimeSession, services.LocalizationManager,
                () => services.Output.SelectedOutputMode, OutputTestSoundFactory.Create(),
                () => services.ProfileSession.ResolvedProfile.VirtualButtons);
        UpdateVisibility();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Deactivate();
        foreach (var ancestor in _visibilityAncestors) ancestor.PropertyChanged -= VisibilityChanged;
        _visibilityAncestors.Clear();
        if (_owner != null) _owner.Closed -= OwnerClosed;
        _owner = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OwnerClosed(object? sender, EventArgs e) => Deactivate();
    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty) return;
        if (e.NewValue is false) Deactivate();
        else UpdateVisibility();
    }
    private void UpdateVisibility()
    {
        if (IsEffectivelyVisible && _owner != null)
        {
            RefreshChoices();
            Refresh();
            _timer.Start();
        }
        else Deactivate();
    }
    private void Deactivate() { _timer.Stop(); Stop(); }

    private void RefreshChoices()
    {
        if (_readMode == null || _test == null || _test.IsOutputTestActive || _busy) return;
        var mode = _readMode();
        _displayedMode = mode;
        DeviceText.Text = OperatingSystem.IsWindows() ? "vJoy" : "uinput";
        _actions.Clear();
        OutputTestCapabilities caps;
        try
        {
            caps = _test.OutputTestCapabilities;
            _error = null;
        }
        catch (Exception ex)
        {
            _error = string.Format(Text("OutputTest_Error"), ex.Message);
            ActionBox.ItemsSource = Array.Empty<string>();
            ActionBox.SelectedIndex = -1;
            return;
        }
        foreach (var action in Enum.GetValues<OutputTestAction>())
        {
            if (action == OutputTestAction.Button && caps.ButtonCount < 3 ||
                action == OutputTestAction.ShiftDown && caps.ButtonCount < 1 ||
                action == OutputTestAction.ShiftUp && caps.ButtonCount < 2) continue;
            _actions.Add(action);
        }
        RefreshLocalizedChoices();
        _buttonCount = caps.ButtonCount;
        _buttonsInitialized = false;
        RefreshButtonChoices();
        ActionBox.SelectedIndex = 0;
    }

    private void RefreshButtonChoices()
    {
        if (_busy || _test?.IsOutputTestActive == true) return;
        var buttons = (_readButtons?.Invoke() ?? [])
            .Where(binding => binding != null && !string.IsNullOrWhiteSpace(binding.Name) &&
                binding.Button >= 3 && binding.Button <= Math.Min(128, _buttonCount))
            .GroupBy(binding => binding.Button).Select(group => group.First()).ToArray();
        if (_buttonsInitialized && _displayedButtons.SequenceEqual(buttons)) return;
        int? selected = (ButtonChoiceBox.SelectedItem as ButtonChoice)?.Binding.Button;
        _buttonsInitialized = true;
        _displayedButtons = buttons;
        ButtonChoiceBox.ItemsSource = buttons.Select(binding => new ButtonChoice(binding,
            VirtualButtonRowViewModel.DisplayName(binding, button => Text("Output_ButtonsNumber") + " " + button))).ToArray();
        int index = Array.FindIndex(buttons, binding => binding.Button == selected);
        ButtonChoiceBox.SelectedIndex = index >= 0 ? index : buttons.Length > 0 ? 0 : -1;
    }

    private void RefreshLocalizedChoices()
    {
        // Replacing the labels also refreshes the selected-value presenter. Preserve
        // selection, including during a running test; localization must not restart it.
        _displayedCulture = _localization?.CurrentCultureCode;
        int actionIndex = ActionBox.SelectedIndex;
        int modeIndex = Math.Max(0, ModeBox.SelectedIndex);
        var names = new List<string>();
        foreach (var action in _actions) names.Add(Text("OutputTest_" + action));
        ActionBox.ItemsSource = names;
        ActionBox.SelectedIndex = actionIndex;
        ModeBox.ItemsSource = new[] { Text("OutputTest_Once"), Text("OutputTest_Repeat") };
        ModeBox.SelectedIndex = modeIndex;
    }

    private string Text(string key) => (_localization ?? App.Services?.LocalizationManager)?.GetString(key) ?? key;
    private void Stop()
    {
        bool sameLifecycle = _test?.OutputTestLifecycleVersion == _testLifecycleVersion;
        if (_ownsTest) { _ownsTest = false; if (sameLifecycle) _test?.StopOutputTest(); }
        if (_startedRuntime) { _startedRuntime = false; if (sameLifecycle) _runtime?.StopAsync().GetAwaiter().GetResult(); }
        if (_cueCancellation != null)
        {
            _cueCancellation.Cancel(); _cueCancellation.Dispose(); _cueCancellation = null;
            _lastCountdownSecond = -1;
            _audioStarted = false;
            if (SoundCheckBox.IsChecked == true) _ = PlayCueAsync(500, 120, CancellationToken.None);
        }
    }

    private void UpdateSound()
    {
        if (!_ownsTest || _test?.IsOutputTestActive != true || SoundCheckBox.IsChecked != true || _audio?.IsAvailable != true) return;
        _cueCancellation ??= new CancellationTokenSource();
        int second = (int)Math.Ceiling(_test.OutputTestSecondsRemaining);
        if (second > 0 && second != _lastCountdownSecond)
        {
            _lastCountdownSecond = second;
            _ = PlayCueAsync(650, 60, _cueCancellation.Token);
        }
        else if (second == 0 && !_audioStarted)
        {
            _audioStarted = true;
            _ = PlayCueAsync(1100, 100, _cueCancellation.Token);
        }
    }

    private async Task PlayCueAsync(int frequency, int duration, CancellationToken ct)
    {
        if (_audio?.IsAvailable != true) return;
        try { await _audio.PlayAsync(frequency, duration, ct); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLogger.Instance.LogWarning($"Output assignment audio unavailable: {ex.Message}"); }
    }
    private void Refresh()
    {
        if (_localization != null && _displayedCulture != _localization.CurrentCultureCode)
            RefreshLocalizedChoices();
        if (_test == null || _runtime == null)
        {
            StartStopButton.Content = Text("OutputTest_Start");
            ActionBox.IsEnabled = false;
            ModeBox.IsEnabled = SoundCheckBox.IsEnabled = false;
            StatusText.Text = App.Services?.LocalizationManager.GetString("OutputTest_Unavailable") ?? "";
            StatusText.IsVisible = !string.IsNullOrWhiteSpace(StatusText.Text);
            return;
        }
        bool active = _test.IsOutputTestActive;
        if (!active) RefreshButtonChoices();
        if (_ownsTest && _test.OutputTestLifecycleVersion != _testLifecycleVersion)
        {
            _completionMessage = Text("OutputTest_ContextChanged");
            Stop();
        }
        bool buttonSelected = ActionBox.SelectedIndex >= 0 && ActionBox.SelectedIndex < _actions.Count && _actions[ActionBox.SelectedIndex] == OutputTestAction.Button;
        ButtonAssignmentPanel.IsVisible = buttonSelected;
        ButtonChoiceBox.IsEnabled = !active && !_busy && ButtonChoiceBox.ItemCount > 0;
        if (ButtonChoiceBox.ItemCount == 0) ButtonChoiceBox.IsDropDownOpen = false;
        NoButtonsHint.IsVisible = buttonSelected && ButtonChoiceBox.ItemCount == 0;
        if (!active && _ownsTest) Stop();
        if (!active && _readMode?.Invoke() != _displayedMode) RefreshChoices();
        ActionBox.IsEnabled = !active && !_busy;
        ModeBox.IsEnabled = !active && !_busy;
        SoundCheckBox.IsEnabled = !active && !_busy && _audio?.IsAvailable == true;
        StartStopButton.IsEnabled = !_busy && (active || _actions.Count > 0 &&
            (!buttonSelected || ButtonChoiceBox.SelectedItem is ButtonChoice));
        StartStopButton.Content = Text(active ? "OutputTest_Stop" : "OutputTest_Start");
        UpdateSound();
        StatusText.Text = _runtime.HasFault ? string.Format(Text("OutputTest_Error"), _runtime.FaultMessage) : _error ??
            (active ? (_test.OutputTestSecondsRemaining > 0 ? string.Format(Text("OutputTest_Countdown"), Math.Ceiling(_test.OutputTestSecondsRemaining)) : Text(ModeBox.SelectedIndex == 1 ? "OutputTest_Running" : "OutputTest_Sending")) : _completionMessage ??
                (buttonSelected && ButtonChoiceBox.SelectedItem is not ButtonChoice ? "" : Text("OutputTest_Ready")));
        StatusText.IsVisible = !string.IsNullOrWhiteSpace(StatusText.Text);
    }
}
