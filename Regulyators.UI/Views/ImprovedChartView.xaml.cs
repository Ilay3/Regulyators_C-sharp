using System;
using System.Windows.Controls;
using Regulyators.UI.ViewModels;

namespace Regulyators.UI.Views
{
    /// <summary>
    /// Логика взаимодействия для ImprovedChartView.xaml
    /// </summary>
    public partial class ImprovedChartView : UserControl
    {
        private ImprovedChartViewModel _viewModel;

        public ImprovedChartView()
        {
            InitializeComponent();

            // После загрузки контрола инициализируем график
            Loaded += (s, e) =>
            {
                if (DataContext is ImprovedChartViewModel viewModel)
                {
                    _viewModel = viewModel;
                    viewModel.InitializeGraph(MainPlot);
                }
                else
                {
                    // Если DataContext не установлен, создаем и используем свой ViewModel
                    _viewModel = new ImprovedChartViewModel();
                    DataContext = _viewModel;
                    _viewModel.InitializeGraph(MainPlot);
                }

                // При изменении размера обновляем график
                MainPlot.SizeChanged += (sender, args) =>
                {
                    if (_viewModel != null)
                    {
                        MainPlot.Refresh();
                    }
                };
            };

            // Обработчик выгрузки контрола
            Unloaded += (s, e) =>
            {
                // Вызываем метод очистки ресурсов у ViewModel
                if (_viewModel != null)
                {
                    _viewModel.CleanUp();
                    _viewModel = null;
                }
            };
        }
    }
}