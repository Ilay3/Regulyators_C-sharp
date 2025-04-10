using System.Windows.Controls;
using Regulyators.UI.ViewModels;

namespace Regulyators.UI.Views
{
    /// <summary>
    /// Панель аналоговых индикаторов для отображения параметров двигателя
    /// </summary>
    public partial class GaugePanelView : UserControl
    {
        private GaugePanelViewModel _viewModel;

        /// <summary>
        /// Конструктор
        /// </summary>
        public GaugePanelView()
        {
            InitializeComponent();

            // Создаем и настраиваем ViewModel
            _viewModel = new GaugePanelViewModel();
            DataContext = _viewModel;

            // Регистрируем обработчик события выгрузки для освобождения ресурсов
            Unloaded += (s, e) =>
            {
                // Вызываем Dispose у ViewModel для освобождения ресурсов
                _viewModel?.Dispose();
                _viewModel = null;
            };
        }
    }
}