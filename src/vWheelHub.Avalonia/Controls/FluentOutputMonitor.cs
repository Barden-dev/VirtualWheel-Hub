using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SimRacingHub.Application.Features.Shell;

namespace SimRacingHub.Avalonia.Controls;
public sealed class FluentOutputMonitor : UserControl
{
    private AppShellViewModel? _model;
    private INotifyPropertyChanged? _profile, _resolved;
    private readonly Grid _tiles = new();
    private readonly Expander _details = new()
    {
        IsExpanded = false,
        IsVisible = false,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
        Margin = new Thickness(0, 7, 0, 0)
    };
    private readonly StackPanel _editor = new();
    private readonly Dictionary<string, FluentMarkerBar> _bars = new();
    private readonly Dictionary<string, TextBlock> _values = new();
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly List<(NumericUpDown Box, Func<double?> Read)> _fields = new();
    private readonly TextBlock _title = new()
    {
        FontWeight = FontWeight.SemiBold
    };
    private bool _refreshing;
    private string? _selected;
    public bool IsDetailsExpanded { get => _details.IsExpanded; set => _details.IsExpanded = value && _selected != null; }
    public string? SelectedAxis => _selected;

    public FluentOutputMonitor()
    {
        var root = new StackPanel
        {
            Spacing = 6
        };
        root.Children.Add(_title);
        root.Children.Add(_tiles);
        _details.Content = _editor;
        root.Children.Add(_details);
        Content = root;
        _details.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty)
                Refresh(false);
        };
        var index = 0;
        foreach (var axis in new[]
        {
            "Steer",
            "Gas",
            "Brake"
        }

        )
        {
            _tiles.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            var id = axis;
            var text = new TextBlock
            {
                FontSize = 12
            };
            var value = new TextBlock
            {
                FontSize = 12,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right
            };
            var label = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };
            label.Children.Add(text);
            Grid.SetColumn(value, 1);
            label.Children.Add(value);
            _values.Add(id, value);
            var bar = MakeBar(id);
            _bars.Add(id, bar);
            var content = new StackPanel
            {
                Spacing = 7
            };
            content.Children.Add(label);
            content.Children.Add(bar);
            var button = new Button
            {
                Content = content,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 7),
                Margin = new Thickness(0, 0, 6, 0)
            };
            button.Classes.Add("hub-axis");
            button.Click += (_, _) => ToggleAxis(id);
            Grid.SetColumn(button, index++);
            _tiles.Children.Add(button);
            _buttons.Add(id, button);
            button.Tag = text;
        }

        DataContextChanged += (_, _) => BindModel();
        AttachedToVisualTree += (_, _) => BindModel();
        DetachedFromVisualTree += (_, _) => Unbind();
    }

    private string L(string key) => _model?.LocalizationManager.GetString(key) ?? string.Empty;
    private string AxisName(string id) => id switch
    {
        "Steer" => L("OutputMonitor_Steer_02f4a3"),
        "Gas" => L("OutputMonitor_Gas_ead965"),
        _ => L("OutputMonitor_Brake_d5ff6c")};
    public void ToggleAxis(string id)
    {
        if (!_buttons.ContainsKey(id))
            return;
        if (_selected == id)
            _details.IsExpanded = !_details.IsExpanded;
        else
        {
            _selected = id;
            BuildEditor();
            _details.IsExpanded = true;
        }

        Refresh(settings: true);
    }

    public void CollapseDetails() => _details.IsExpanded = false;
    private void Unbind()
    {
        if (_model != null)
            _model.PropertyChanged -= ModelChanged;
        if (_profile != null)
            _profile.PropertyChanged -= ModelChanged;
        if (_resolved != null)
            _resolved.PropertyChanged -= ModelChanged;
        _model = null;
        _profile = _resolved = null;
    }

    private void BindModel()
    {
        var model = DataContext as AppShellViewModel;
        if (ReferenceEquals(model, _model))
            return;
        Unbind();
        _model = model;
        if (_model == null)
            return;
        _model.PropertyChanged += ModelChanged;
        TrackProfile();
        BuildEditor();
        Refresh(true);
    }

    private void TrackProfile()
    {
        if (_model == null)
            return;
        if (!ReferenceEquals(_profile, _model.CurrentProfile))
        {
            if (_profile != null)
                _profile.PropertyChanged -= ModelChanged;
            _profile = _model.CurrentProfile;
            if (_profile != null)
                _profile.PropertyChanged += ModelChanged;
        }

        if (!ReferenceEquals(_resolved, _model.ResolvedProfile))
        {
            if (_resolved != null)
                _resolved.PropertyChanged -= ModelChanged;
            _resolved = _model.ResolvedProfile;
            if (_resolved != null)
                _resolved.PropertyChanged += ModelChanged;
        }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ModelChanged(sender, e));
            return;
        }

        TrackProfile();
        if (e.PropertyName is nameof(AppShellViewModel.CurrentCultureCode) or nameof(AppShellViewModel.CurrentLanguageCode))
            BuildEditor();
        Refresh(e.PropertyName is not (nameof(AppShellViewModel.WheelAxis) or nameof(AppShellViewModel.GasAxis) or nameof(AppShellViewModel.BrakeAxis) or nameof(AppShellViewModel.LatestSnapshot)));
    }

    private void Refresh(bool settings)
    {
        if (_model == null || _refreshing)
            return;
        _refreshing = true;
        try
        {
            _title.Text = L("OutputMonitor_Output_monitor_dc5721");
            _details.IsVisible = _selected != null;
            _details.Header = _selected == null ? null : L("OutputMonitor_Quick_settings_766f62") + AxisName(_selected);
            foreach (var id in _bars.Keys)
            {
                var value = id == "Steer" ? _model.WheelAxis : id == "Gas" ? _model.GasAxis : _model.BrakeAxis;
                _values[id].Text = value.ToString("P0", CultureInfo.CurrentCulture);
                ((TextBlock)_buttons[id].Tag!).Text = AxisName(id);
                if (settings)
                {
                    ToolTip.SetTip(_bars[id], L("OutputMonitor_White_output_range_Yellow_thresholds_Cyan_steering_lock_35e496"));
                    AutomationProperties.SetName(_bars[id], string.Format(L("OutputMonitor_MarkersFormat"), AxisName(id)));
                }
                _bars[id].Value = value;
                _bars[id].InvalidateVisual();
                _buttons[id].BorderBrush = id == _selected && _details.IsExpanded ? new SolidColorBrush(Color.Parse("#60CDFF")) : new SolidColorBrush(Color.Parse("#3E3E3E"));
            }

            foreach (var bar in _editor.Children.OfType<FluentMarkerBar>())
            {
                bar.Value = _selected == "Steer" ? _model.WheelAxis : _selected == "Gas" ? _model.GasAxis : _model.BrakeAxis;
                bar.InvalidateVisual();
            }

            if (settings)
                foreach (var f in _fields)
                    f.Box.Value = f.Read()is { } number ? (decimal)number : null;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private FluentMarkerBar MakeBar(string id)
    {
        var bar = new FluentMarkerBar
        {
            Height = 14,
            MinWidth = 70,
            Bipolar = id == "Steer",
            AxisBrush = new SolidColorBrush(Color.Parse(id == "Gas" ? "#4ADE80" : id == "Brake" ? "#F87171" : "#999999"))
        };
        bar.Markers = () => GetMarkers(id);
        bar.MarkerMoved += (kind, value) => ApplyMarker(id, kind, value);
        ToolTip.SetTip(bar, L("OutputMonitor_White_output_range_Yellow_thresholds_Cyan_steering_lock_35e496"));
        AutomationProperties.SetName(bar, string.Format(L("OutputMonitor_MarkersFormat"), AxisName(id)));
        return bar;
    }

    private (double Min, double Max, bool Invert) Range(string id)
    {
        var p = _model!.ResolvedProfile;
        return id switch
        {
            "Steer" => (p.OutSteerMin, p.OutSteerMax, p.InvertSteering),
            "Gas" => (p.OutGasMin, p.OutGasMax, p.InvertGas),
            _ => (p.OutBrakeMin, p.OutBrakeMax, p.InvertBrake)};
    }

    private IReadOnlyList<(string Kind, double Position, IBrush Brush)> GetMarkers(string id)
    {
        if (_model == null)
            return Array.Empty<(string, double, IBrush)>();
        var p = _model.ResolvedProfile;
        var r = Range(id);
        var markers = new List<(string, double, IBrush)>
        {
            ("Min", r.Min / 32768, Brushes.White),
            ("Max", r.Max / 32768, Brushes.White)
        };
        double Percent(double ratio) => (r.Min + (r.Invert ? 1 - ratio : ratio) * (r.Max - r.Min)) / 32768;
        if (id == "Steer" && p.UseSteeringLockClamp && p.GlobalSteeringRange > 0)
        {
            var ratio = Math.Clamp((double)p.CarSteeringLock / p.GlobalSteeringRange, 0, 1);
            markers.Add(("LockLeft", Percent((1 - ratio) / 2), Brushes.Cyan));
            markers.Add(("LockRight", Percent((1 + ratio) / 2), Brushes.Cyan));
        }

        if (id == "Gas")
            markers.Add(("Threshold", Percent(p.GasThreshold), Brushes.Yellow));
        if (id == "Brake")
        {
            markers.Add(("Threshold", Percent(p.BrakeThreshold), Brushes.Yellow));
            ;
        }

        return markers;
    }

    public void ApplyMarker(string id, string kind, double position)
    {
        if (_model?.Output == null || _model.Pedals == null || _model.Steering == null)
            return;
        var r = Range(id);
        var raw = (int)Math.Round(Math.Clamp(position, 0, 1) * 32768);
        if (kind is "Min" or "Max")
        {
            raw = kind == "Min" ? Math.Clamp(raw, 0, Math.Max(0, (int)r.Max - 1)) : Math.Clamp(raw, Math.Min(32768, (int)r.Min + 1), 32768);
            if (id == "Steer")
            {
                if (kind == "Min")
                    _model.Output.OutSteerMin = raw;
                else
                    _model.Output.OutSteerMax = raw;
            }
            else if (id == "Gas")
            {
                if (kind == "Min")
                    _model.Output.OutGasMin = raw;
                else
                    _model.Output.OutGasMax = raw;
            }
            else
            {
                if (kind == "Min")
                    _model.Output.OutBrakeMin = raw;
                else
                    _model.Output.OutBrakeMax = raw;
            }
        }
        else if (r.Max > r.Min)
        {
            var ratio = Math.Clamp((raw - r.Min) / (r.Max - r.Min), 0, 1);
            if (r.Invert)
                ratio = 1 - ratio;
            if (kind.StartsWith("Lock"))
                _model.Steering.Output.CarSteeringLock = Math.Max(1, (int)Math.Round(Math.Abs(ratio - .5) * 2 * _model.ResolvedProfile.GlobalSteeringRange));
            else if (id == "Gas")
                _model.Pedals.Throttle.GasThreshold = ratio;
            else
                _model.Pedals.Brake.BrakeThreshold = ratio;
        }

        Refresh(true);
    }

    private void BuildEditor()
    {
        _editor.Children.Clear();
        _fields.Clear();
        if (_model == null || _selected == null)
            return;
        var header = new TextBlock
        {
            Text = L("OutputMonitor_Drag_a_marker_or_enter_an_exact_value_be1fb1"),
            FontSize = 12,
            Margin = new Thickness(10, 6, 10, 6),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        _editor.Children.Add(header);
        var bar = MakeBar(_selected);
        bar.Height = 18;
        bar.Margin = new Thickness(12, 0, 12, 8);
        _editor.Children.Add(bar);
        var fields = new WrapPanel
        {
            Margin = new Thickness(10, 0, 0, 6)
        };
        _editor.Children.Add(fields);
        void Field(string name, Func<double?> read, Action<double?> write, double min, double max, double factor = 1)
        {
            var panel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Spacing = 7,
                Margin = new Thickness(0, 0, 16, 6)
            };
            panel.Children.Add(new TextBlock { Text = name, FontSize = 12, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center });
            var input = new NumericUpDown
            {
                Minimum = (decimal)min,
                Maximum = (decimal)max,
                Increment = 1,
                Width = 130,
                FormatString = "0.##"
            };
            AutomationProperties.SetName(input, name);
            _fields.Add((input, () => read() * factor));
            input.ValueChanged += (_, _) =>
            {
                if (!_refreshing)
                    write(input.Value is { } value ? (double)value / factor : null);
            };
            panel.Children.Add(input);
            fields.Children.Add(panel);
        }

        if (_selected == "Steer")
            Field(L("OutputMonitor_Lock_71dc3a"), () => _model.Steering!.EffectiveCarSteeringLock, v => _model.Steering!.CarSteeringLock = v is { } n ? (int)n : null, 1, 1440);
        else if (_selected == "Gas")
            Field(L("OutputMonitor_Threshold_f64037"), () => _model.Pedals!.EffectiveGasThreshold, v => _model.Pedals!.GasThreshold = v, 0, 100, 100);
        else
        {
            Field(L("OutputMonitor_Threshold_f64037"), () => _model.Pedals!.EffectiveBrakeThreshold, v => _model.Pedals!.BrakeThreshold = v, 0, 100, 100);
            ;
        }

        var id = _selected;
        Field(L("OutputMonitor_Minimum_5d398e"), () => Range(id).Min, v =>
        {
            if (v is { } n)
                ApplyMarker(id, "Min", n / 32768);
        }, 0, 32767);
        Field(L("OutputMonitor_Maximum_a8df2f"), () => Range(id).Max, v =>
        {
            if (v is { } n)
                ApplyMarker(id, "Max", n / 32768);
        }, 1, 32768);
    }
}

public sealed class FluentMarkerBar : Control
{
    public double Value { get; set; }
    public bool Bipolar { get; set; }
    public IBrush AxisBrush { get; set; } = Brushes.Gray;
    public Func<IReadOnlyList<(string Kind, double Position, IBrush Brush)>> Markers { get; set; } = () => Array.Empty<(string, double, IBrush)>();

    public event Action<string, double>? MarkerMoved;
    private string? _dragging;
    private double Span => Math.Max(1, Bounds.Width - 6);

    public override void Render(DrawingContext context)
    {
        var track = new Rect(3, Bounds.Height / 2 - 2, Span, 4);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#555555")), null, track, 2, 2);
        var fraction = Math.Clamp(Bipolar ? (Value + 1) / 2 : Value, 0, 1);
        var start = Bipolar ? .5 : 0;
        context.DrawRectangle(AxisBrush, null, new Rect(3 + Math.Min(start, fraction) * Span, track.Y, Math.Abs(fraction - start) * Span, 4), 2, 2);
        foreach (var marker in Markers())
        {
            var x = 3 + Math.Clamp(marker.Position, 0, 1) * Span;
            context.DrawLine(new Pen(marker.Brush, 4), new Point(x, 0), new Point(x, Bounds.Height));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var x = e.GetPosition(this).X;
        var closest = Markers().OrderBy(m => Math.Abs(3 + m.Position * Span - x)).FirstOrDefault();
        if (closest.Kind != null && Math.Abs(3 + closest.Position * Span - x) <= 9)
        {
            _dragging = closest.Kind;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging == null)
            return;
        MarkerMoved?.Invoke(_dragging, Math.Clamp((e.GetPosition(this).X - 3) / Span, 0, 1));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging == null)
            return;
        _dragging = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = null;
    }
}
