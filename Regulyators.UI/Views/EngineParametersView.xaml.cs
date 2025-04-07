using System.Windows.Controls;
using Regulyators.UI.ViewModels;
using ScottPlot;

namespace Regulyators.UI.Views
{
    public partial class EngineParametersView : UserControl
    {
        public EngineParametersView()
        {
            InitializeComponent();

            // После загрузки контрола инициализируем график
            Loaded += (s, e) =>
            {
                if (DataContext is EngineParametersViewModel viewModel &&
                    ParametersPlot != null)
                {
                    viewModel.InitializeGraph(ParametersPlot);
                }
            };
        }
    }
}