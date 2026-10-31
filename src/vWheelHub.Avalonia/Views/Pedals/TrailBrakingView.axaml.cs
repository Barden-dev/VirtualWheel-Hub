using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SimRacingHub.Application.Features.Pedals;

namespace SimRacingHub.Avalonia.Views.Pedals
{
    /// <summary>
    /// UserControl view for Trail Braking settings (enable toggle, trigger threshold, decay release rate,
    /// intensity factor, and mutual exclusion with ABS helper).
    /// Bound to TrailBrakingViewModel.
    /// </summary>
    public partial class TrailBrakingView : UserControl
    {
        public TrailBrakingView()
        {
            InitializeComponent();
        }

        public TrailBrakingView(TrailBrakingViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.TrailBraking != null)
            {
                DataContext = App.Services.TrailBraking;
            }
        }
    }
}
