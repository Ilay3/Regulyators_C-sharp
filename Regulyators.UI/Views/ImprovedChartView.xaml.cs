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
        public ImprovedChartView()
        {
            InitializeComponent();

            // Используем синглтон вместо создания нового экземпляра
            DataContext = ImprovedChartViewModel.Instance;

            // После загрузки контрола инициализируем график
            Loaded += OnViewLoaded;

            // Обработчик выгрузки контрола для освобождения ресурсов UI (но не данных)
            Unloaded += OnViewUnloaded;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Используем синглтон напрямую
                var viewModel = ImprovedChartViewModel.Instance;

                // Используем Dispatcher.BeginInvoke для отложенной инициализации
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    viewModel.InitializeGraph(MainPlot);
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке ImprovedChartView: {ex.Message}");
            }
        }

        private void OnViewUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Освобождаем только ресурсы UI, данные продолжают собираться в фоновом режиме
                ImprovedChartViewModel.Instance.ReleaseViewResources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при выгрузке ImprovedChartView: {ex.Message}");
            }
        }
    }
}