using System.Windows.Controls;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Views.Pages
{
    public partial class GasLogicPage : Page
    {
        public GasLogicPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
