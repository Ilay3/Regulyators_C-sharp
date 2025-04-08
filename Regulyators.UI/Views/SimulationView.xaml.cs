using System.Windows.Controls;
using Regulyators.UI.ViewModels;

namespace Regulyators.UI.Views
{
    /// <summary>
    /// Логика взаимодействия для SimulationView.xaml
    /// </summary>
    public partial class SimulationView : UserControl
    {
        private SimulationViewModel _viewModel;

        public SimulationView()
        {
            InitializeComponent();

            // Создаем и настраиваем ViewModel
            _viewModel = new SimulationViewModel();
            DataContext = _viewModel;
        }
    }
}