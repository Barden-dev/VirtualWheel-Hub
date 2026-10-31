using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SimRacingHub.Application.Features.Pedals;

namespace SimRacingHub.Avalonia.Views.Pedals
{
    /// <summary>
    /// UserControl view for pedal configuration (gas, brake, combined modes, inversion, response curves, deadzones,
    /// attack rates, and simulator TC/ABS assistance helpers).
    /// Bound to PedalsViewModel.
    /// </summary>
    public partial class PedalsView : UserControl
    {
        public static readonly StyledProperty<bool> ShowPageHeaderProperty =
            AvaloniaProperty.Register<PedalsView, bool>(nameof(ShowPageHeader), true);
        public bool ShowPageHeader { get => GetValue(ShowPageHeaderProperty); set => SetValue(ShowPageHeaderProperty, value); }
        public static readonly StyledProperty<bool> ShowTabHeadersProperty =
            AvaloniaProperty.Register<PedalsView, bool>(nameof(ShowTabHeaders), true);
        public bool ShowTabHeaders { get => GetValue(ShowTabHeadersProperty); set => SetValue(ShowTabHeadersProperty, value); }
        public static readonly StyledProperty<int> SelectedTabIndexProperty =
            AvaloniaProperty.Register<PedalsView, int>(nameof(SelectedTabIndex));
        public int SelectedTabIndex { get => GetValue(SelectedTabIndexProperty); set => SetValue(SelectedTabIndexProperty, value); }

        private bool? _compactLayout;
        public PedalsView()
        {
            InitializeComponent();
            PropertyChanged += (_, e) =>
            {
                if (e.Property == SelectedTabIndexProperty && !ShowTabHeaders)
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => PedalScroll.ScrollToHome());
            };
            // Change grid definitions after the current measure/arrange pass finishes.
            SizeChanged += (_, _) => global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateResponsiveLayout);
        }

        private void UpdateResponsiveLayout()
        {
            var compact = Bounds.Width < 760;
            if (_compactLayout == compact) return;
            _compactLayout = compact;
            foreach (var grid in new[] { ThrottleDynamicsGrid, BrakeDynamicsGrid })
            {
                grid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 300);
                foreach (var child in grid.Children)
                {
                    if (child is Border curve)
                    {
                        Grid.SetColumn(curve, compact ? 0 : 1);
                        Grid.SetRow(curve, compact ? 1 : 0);
                    }
                    else if (child is StackPanel inputs)
                        inputs.Margin = compact ? new Thickness(0, 0, 0, 14) : new Thickness(0, 0, 16, 0);
                }
            }
        }

        public PedalsView(PedalsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Pedals != null)
            {
                DataContext = App.Services.Pedals;
            }
        }
    }
}
