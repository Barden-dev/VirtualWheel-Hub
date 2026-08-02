using System.Windows.Controls;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Views.Pages
{
    public partial class BrakeLogicPage : Page
    {
        public BrakeLogicPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
