using System.Windows.Controls;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Views.Pages
{
    public partial class SteeringPage : Page
    {
        public SteeringPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
