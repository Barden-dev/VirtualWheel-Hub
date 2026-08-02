using System.Windows.Controls;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Views.Pages
{
    public partial class TrailBrakingPage : Page
    {
        public TrailBrakingPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
