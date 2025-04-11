using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
                    // Используем Dispatcher.BeginInvoke для отложенной инициализации
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _viewModel.InitializeGraph(MainPlot);
                    }), DispatcherPriority.Loaded);
                }
                else
                {
                    // Если DataContext не установлен, создаем новый ViewModel
                    _viewModel = new ImprovedChartViewModel();
                    DataContext = _viewModel;
                    // Используем Dispatcher.BeginInvoke для отложенной инициализации
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _viewModel.InitializeGraph(MainPlot);
                    }), DispatcherPriority.Loaded);
                }
            };

            // Обработчик выгрузки контрола для освобождения ресурсов
            Unloaded += (s, e) =>
            {
                try
                {
                    // Вызываем метод очистки ресурсов у ViewModel
                    _viewModel?.CleanUp();
                    _viewModel = null;
                }
                catch (Exception ex)
                {
                    // Проглатываем любые исключения при закрытии
                    System.Diagnostics.Debug.WriteLine($"Ошибка при выгрузке ImprovedChartView: {ex.Message}");
                }
            };
        }
    }
}