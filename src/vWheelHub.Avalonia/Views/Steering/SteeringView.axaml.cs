using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SimRacingHub.Application.Features.Steering;

namespace SimRacingHub.Avalonia.Views.Steering
{
    /// <summary>
    /// UserControl view for steering configuration (sensitivity, response curve gamma, deadzone offset,
    /// steering locks and speed-sensitive steering scaling).
    /// Bound to SteeringViewModel.
    /// </summary>
    public partial class SteeringView : UserControl
    {
        public static readonly StyledProperty<bool> ShowPageHeaderProperty =
            AvaloniaProperty.Register<SteeringView, bool>(nameof(ShowPageHeader), true);
        public bool ShowPageHeader { get => GetValue(ShowPageHeaderProperty); set => SetValue(ShowPageHeaderProperty, value); }

        public SteeringView()
        {
            InitializeComponent();
            SizeChanged += (_, _) => global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateResponsiveLayout);
        }

        private bool? _compactLayout;
        private void UpdateResponsiveLayout()
        {
            bool compact = Bounds.Width < 760;
            if (_compactLayout == compact) return;
            _compactLayout = compact;
            SteeringDynamicsGrid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 340);
            foreach (var child in SteeringDynamicsGrid.Children)
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

        public SteeringView(SteeringViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Steering != null)
            {
                DataContext = App.Services.Steering;
            }
        }
    }
}
