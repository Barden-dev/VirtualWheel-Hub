using Avalonia;
using Avalonia.Controls;
using SimRacingHub.Application.Features.Output;

namespace SimRacingHub.Avalonia.Views.Output
{
    /// <summary>
    /// UserControl view for virtual output device configuration and axis calibration.
    /// Bound to OutputViewModel.
    /// </summary>
    public partial class OutputView : UserControl
    {
        public OutputView()
        {
            InitializeComponent();
        }

        public OutputView(OutputViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Output != null)
            {
                DataContext = App.Services.Output;
            }
        }
    }
}
