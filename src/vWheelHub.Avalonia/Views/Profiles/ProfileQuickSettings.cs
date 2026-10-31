using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SimRacingHub.Application.Features.Shell;

namespace SimRacingHub.Avalonia.Views.Profiles;
public sealed class ProfileQuickSettings : UserControl
{
    private readonly Grid _cards = new()
    {
        ColumnDefinitions = new("*,*"),
        RowDefinitions = new("Auto,Auto"),
        ColumnSpacing = 10,
        RowSpacing = 10
    };
    private readonly List<(TextBlock Text, string Key)> _labels = new();
    private readonly List<(NumericUpDown Box, Func<double> Read, double Min, double Max)> _fields = new();
    private AppShellViewModel? _model;
    private bool _refreshing;
    private bool? _compact;
    public ProfileQuickSettings()
    {
        IsVisible = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var root = new StackPanel
        {
            Spacing = 10
        };
        root.Children.Add(Label("Profiles_QuickSettingsTitle", 17, true));
        var hint = Label("Profiles_QuickSettingsHint", 12);
        hint.Classes.Add("hub-secondary");
        root.Children.Add(hint);
        root.Children.Add(_cards);
        Content = root;
        var steering = Card("Nav_Steering", PageId.Steering, 0);
        Field(steering, "MouseSensitivity", () => _model!.Steering!.EffectiveMouseSensitivity, v => _model!.Steering!.MouseSensitivity = v, .01, 100, .1);
        Field(steering, "SteeringGamma", () => _model!.Steering!.EffectiveSteeringGamma, v => _model!.Steering!.SteeringGamma = v, .1, 5, .1);
        var gas = Card("Nav_ThrottleLogic", PageId.Pedals, 1, 0);
        Field(gas, "GasGamma", () => _model!.Pedals!.EffectiveGasGamma, v => _model!.Pedals!.GasGamma = v, .1, 5, .1);
        Field(gas, "GasThreshold", () => _model!.Pedals!.EffectiveGasThreshold * 100, v => _model!.Pedals!.GasThreshold = v / 100, 0, 100, 1, true);
        var brake = Card("Nav_BrakeLogic", PageId.Pedals, 2, 1);
        Field(brake, "BrakeGamma", () => _model!.Pedals!.EffectiveBrakeGamma, v => _model!.Pedals!.BrakeGamma = v, .1, 5, .1);
        Field(brake, "BrakeThreshold", () => _model!.Pedals!.EffectiveBrakeThreshold * 100, v => _model!.Pedals!.BrakeThreshold = v / 100, 0, 100, 1, true);
        AttachedToVisualTree += (_, _) => BindModel();
        DetachedFromVisualTree += (_, _) => Unbind();
        DataContextChanged += (_, _) => BindModel();
        SizeChanged += (_, _) => Dispatcher.UIThread.Post(UpdateLayoutColumns);
    }

    private TextBlock Label(string key, double size = 12, bool bold = false)
    {
        var label = new TextBlock
        {
            FontSize = size,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        _labels.Add((label, key));
        return label;
    }

    private StackPanel Card(string title, PageId page, int index, int? pedalTab = null)
    {
        var content = new StackPanel
        {
            Spacing = 6
        };
        var heading = new Grid
        {
            ColumnDefinitions = new("*,Auto"),
            ColumnSpacing = 8
        };
        heading.Children.Add(Label(title, 13, true));
        var arrow = new PathIcon
        {
            Data = Geometry.Parse("M4 2L9 7L4 12L3 11L7 7L3 3Z"),
            Width = 9,
            Height = 10,
            Opacity = .6
        };
        Grid.SetColumn(arrow, 1);
        heading.Children.Add(arrow);
        var link = new Button
        {
            Name = "QuickOpen" + index,
            Content = heading,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        link.Click += (_, _) =>
        {
            _model?.NavigateTo(page);
            if (pedalTab is { } tab)
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var control in TopLevel.GetTopLevel(this)?.GetVisualDescendants() ?? Array.Empty<Visual>())
                        if (control is TabControl { Name: "PedalTabs" } tabs)
                            tabs.SelectedIndex = tab;
                }, DispatcherPriority.Loaded);
        };
        content.Children.Add(link);
        var border = new Border
        {
            Padding = new Thickness(12),
            Child = content
        };
        border.Classes.Add("hub-card");
        Grid.SetColumn(border, index % 2);
        Grid.SetRow(border, index / 2);
        _cards.Children.Add(border);
        return content;
    }

    private NumericUpDown Field(StackPanel card, string property, Func<double> read, Action<double> write, double min, double max, double increment, bool percent = false)
    {
        var row = new Grid
        {
            ColumnDefinitions = new("*,Auto"),
            ColumnSpacing = 8,
            MinHeight = 38
        };
        var label = Label("ProfileSetting_" + property);
        row.Children.Add(label);
        var input = new NumericUpDown
        {
            Name = "Quick" + property + "Box",
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Increment = (decimal)increment,
            FormatString = percent ? "0.##' %'" : "0.##",
            Width = 112,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        card.Children.Add(row);
        _fields.Add((input, read, min, max));
        input.ValueChanged += (_, _) =>
        {
            if (_refreshing || _model == null || input.Value is not { } value)
                return;
            write((double)value);
            Refresh();
        };
        return input;
    }

    private void BindModel()
    {
        var model = (TopLevel.GetTopLevel(this) as Window)?.DataContext as AppShellViewModel;
        if (ReferenceEquals(model, _model))
            return;
        Unbind();
        _model = model;
        IsVisible = model?.Steering != null && model.Pedals != null;
        if (model == null)
            return;
        model.PropertyChanged += ModelChanged;
        model.ProfileSession.UndoStateChanged += SessionChanged;
        Refresh();
    }

    private void Unbind()
    {
        if (_model != null)
        {
            _model.PropertyChanged -= ModelChanged;
            _model.ProfileSession.UndoStateChanged -= SessionChanged;
        }

        _model = null;
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppShellViewModel.CurrentCultureCode) or nameof(AppShellViewModel.ResolvedProfile) or nameof(AppShellViewModel.CurrentProfile))
            Refresh();
    }

    private void SessionChanged(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (_model?.Steering == null || _model.Pedals == null)
            return;
        _refreshing = true;
        try
        {
            foreach (var(label, key)in _labels)
                label.Text = _model.LocalizationManager.GetString(key);
            foreach (var field in _fields)
            {
                double value = field.Read();
                field.Box.Minimum = (decimal)Math.Min(field.Min, value);
                field.Box.Maximum = (decimal)Math.Max(field.Max, value);
                field.Box.Value = (decimal)value;
                AutomationProperties.SetName(field.Box, _model.LocalizationManager.GetString("ProfileSetting_" + field.Box.Name![5..^3]));
            };
            ;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateLayoutColumns()
    {
        bool compact = Bounds.Width < 510;
        if (_compact == compact)
            return;
        _compact = compact;
        _cards.ColumnDefinitions = new(compact ? "*" : "*,*");
        _cards.RowDefinitions = new(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
        for (int i = 0; i < _cards.Children.Count; i++)
        {
            Grid.SetColumn(_cards.Children[i], compact ? 0 : i % 2);
            Grid.SetRow(_cards.Children[i], compact ? i : i / 2);
        }
    }
}
