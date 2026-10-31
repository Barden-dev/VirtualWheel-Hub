using Avalonia;
using Avalonia.Controls;
using SimRacingHub.Application.Features.Dashboard;

namespace SimRacingHub.Avalonia.Views.Dashboard
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        public DashboardView(DashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Dashboard != null)
            {
                DataContext = App.Services.Dashboard;
            }
        }
    }
}
