using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>A presentation-only card. Its sample never receives user input.</summary>
public sealed class FeaturePreview : Border
{
    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<FeaturePreview, string>(nameof(Heading), "");
    public static readonly StyledProperty<string> SummaryProperty =
        AvaloniaProperty.Register<FeaturePreview, string>(nameof(Summary), "");
    public static readonly StyledProperty<Control?> SampleProperty =
        AvaloniaProperty.Register<FeaturePreview, Control?>(nameof(Sample));
    public string Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public Control? Sample { get => GetValue(SampleProperty); set => SetValue(SampleProperty, value); }

    public FeaturePreview()
    {
        Background = Brush.Parse("#261E26");
        BorderBrush = Brush.Parse("#63324F");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(16);
        var title = new TextBlock
        {
            [!TextBlock.TextProperty] = this[!HeadingProperty],
            FontWeight = FontWeight.SemiBold, FontSize = 14,
            Foreground = Brush.Parse("#F5EDF2"), TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        var badge = new Border
        {
            Background = Brush.Parse("#421D36"), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2), Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock { Text = "PRO", FontSize = 10,
                FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#F9A8D4") }
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(badge, 1); header.Children.Add(title); header.Children.Add(badge);
        Child = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                header,
                new TextBlock { [!TextBlock.TextProperty] = this[!SummaryProperty],
                    Foreground = Brush.Parse("#D8CDD4"), FontSize = 12, TextWrapping = TextWrapping.Wrap },
                new ContentPresenter { [!ContentPresenter.ContentProperty] = this[!SampleProperty],
                    IsEnabled = false, IsHitTestVisible = false }
            }
        };
    }
}
