using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Regulyators.UI.Views;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// Главная ViewModel приложения
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private object _currentView;
        private MenuItem _selectedMenuItem;
        private string _connectionStatus;
        private Brush _connectionStatusColor;
        private string _statusMessage;
        private DateTime _currentDateTime;

        /// <summary>
        /// Текущее представление
        /// </summary>
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// Выбранный пункт меню
        /// </summary>
        public MenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value) && _selectedMenuItem != null)
                {
                    CurrentView = _selectedMenuItem.ViewModel;
                    StatusMessage = $"Выбран раздел: {_selectedMenuItem.Title}";
                }
            }
        }

        /// <summary>
        /// Статус соединения
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        /// <summary>
        /// Цвет индикатора соединения
        /// </summary>
        public Brush ConnectionStatusColor
        {
            get => _connectionStatusColor;
            set => SetProperty(ref _connectionStatusColor, value);
        }

        /// <summary>
        /// Сообщение в статусной строке
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Текущие дата и время
        /// </summary>
        public DateTime CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        /// <summary>
        /// Элементы меню
        /// </summary>
        public ObservableCollection<MenuItem> MenuItems { get; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public MainViewModel()
        {
            // Инициализация таймера для обновления времени
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Начальные значения
            ConnectionStatus = "Нет связи";
            ConnectionStatusColor = Brushes.Red;
            StatusMessage = "Приложение запущено";
            CurrentDateTime = DateTime.Now;

            // Создание моделей представления
            var engineParamsViewModel = new EngineParametersViewModel();
            var controlViewModel = new EngineControlViewModel();
            var loggingViewModel = new LoggingViewModel();

            // Инициализация меню
            MenuItems = new ObservableCollection<MenuItem>
            {
                new MenuItem
                {
                    Title = "Параметры двигателя",
                    ViewModel = engineParamsViewModel
                },
                new MenuItem
                {
                    Title = "Управление двигателем",
                    ViewModel = controlViewModel
                },
                new MenuItem
                {
                    Title = "Журнал событий",
                    ViewModel = loggingViewModel
                }

            };

            // Выбор первого пункта меню по умолчанию
            SelectedMenuItem = MenuItems[0];

            // Пробуем подключиться к оборудованию
            Task.Run(SimulateConnection);
        }

        /// <summary>
        /// Имитация подключения к оборудованию
        /// </summary>
        private async Task SimulateConnection()
        {
            await Task.Delay(2000);

            // Обновляем статус в UI потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectionStatus = "Подключено";
                ConnectionStatusColor = Brushes.Green;
                StatusMessage = "Подключение к оборудованию установлено";
            });
        }

        /// <summary>
        /// Обработчик таймера для обновления времени
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            CurrentDateTime = DateTime.Now;
        }
    }
}