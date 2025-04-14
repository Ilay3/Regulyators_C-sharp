using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// Главная ViewModel приложения
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;
        private object _currentView;
        private MenuItem _selectedMenuItem;
        private string _connectionStatus;
        private Brush _connectionStatusColor;
        private string _statusMessage;
        private DateTime _currentDateTime;
        private bool _isBusy;
        private string _busyMessage;

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
        /// Флаг занятости для отображения индикатора прогресса
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Сообщение о текущей операции для индикатора прогресса
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
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
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;

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
            IsBusy = false;
            BusyMessage = string.Empty;

            // Создание моделей представления
            var engineParamsViewModel = new EngineParametersViewModel();
            var controlViewModel = new EngineControlViewModel();
            var loggingViewModel = new LoggingViewModel();

            // Модели представления для защит и настроек
            var protectionViewModel = new ProtectionSystemViewModel();
            var settingsViewModel = new SettingsViewModel();

            // Новая модель представления для аналоговых индикаторов
            var gaugePanelViewModel = new GaugePanelViewModel();

            // Новая модель представления для симуляции
            var simulationViewModel = new SimulationViewModel();
            // Модель представления для расширенных графиков
            var improvedChartViewModel = new ImprovedChartViewModel();

            // Инициализация меню с обновленными иконками
            MenuItems = new ObservableCollection<MenuItem>
            {
                new MenuItem
                {
                    Title = "Параметры двигателя",
                    ViewModel = engineParamsViewModel,
                    IconKind = "ChartLineVariant"
                },
                new MenuItem
                {
                    Title = "Аналоговые индикаторы",
                    ViewModel = gaugePanelViewModel,
                    IconKind = "GaugeHigh"
                },
                new MenuItem
                {
                    Title = "Расширенные графики",
                    ViewModel = improvedChartViewModel,
                    IconKind = "ChartMultiline"
                },
                new MenuItem
                {
                    Title = "Управление двигателем",
                    ViewModel = controlViewModel,
                    IconKind = "EngineOutline"
                },
                new MenuItem
                {
                    Title = "Система защиты",
                    ViewModel = protectionViewModel,
                    IconKind = "ShieldAlert"
                },
                new MenuItem
                {
                    Title = "Журнал событий",
                    ViewModel = loggingViewModel,
                    IconKind = "ClipboardTextClock"
                },
                new MenuItem
                {
                    Title = "Настройки системы",
                    ViewModel = settingsViewModel,
                    IconKind = "Cog"
                },
                new MenuItem
                {
                    Title = "Режим симуляции",
                    ViewModel = simulationViewModel,
                    IconKind = "TestTube"
                }
            };

            // Выбор первого пункта меню по умолчанию
            SelectedMenuItem = MenuItems[0];

            // Подписка на события изменения состояния соединения
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Пробуем подключиться к оборудованию
            Task.Run(SimulateConnection);
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConnectionStatus = isConnected ? "Подключено" : "Нет связи";
                ConnectionStatusColor = isConnected ? Brushes.Green : Brushes.Red;

                if (isConnected)
                {
                    StatusMessage = "Подключение к оборудованию установлено";
                }
                else
                {
                    StatusMessage = "Соединение с оборудованием потеряно";
                }
            });
        }

        /// <summary>
        /// Имитация подключения к оборудованию
        /// </summary>
        private async Task SimulateConnection()
        {
            try
            {
                // Показываем индикатор занятости
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsBusy = true;
                    BusyMessage = "Подключение к оборудованию...";
                });

                await Task.Delay(2000);

                // Обновляем статус в UI потоке
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStatus = "Подключено";
                    ConnectionStatusColor = Brushes.Green;
                    StatusMessage = "Подключение к оборудованию установлено";

                    // Скрываем индикатор занятости
                    IsBusy = false;
                    BusyMessage = string.Empty;
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при подключении к оборудованию", ex.Message);

                // Обновляем статус в UI потоке
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStatus = "Ошибка подключения";
                    ConnectionStatusColor = Brushes.Red;
                    StatusMessage = $"Ошибка подключения: {ex.Message}";

                    // Скрываем индикатор занятости
                    IsBusy = false;
                    BusyMessage = string.Empty;
                });
            }
        }

        /// <summary>
        /// Обработчик таймера для обновления времени
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            CurrentDateTime = DateTime.Now;
        }

        /// <summary>
        /// Показать индикатор занятости с сообщением
        /// </summary>
        public void ShowBusy(string message)
        {
            IsBusy = true;
            BusyMessage = message;
        }

        /// <summary>
        /// Скрыть индикатор занятости
        /// </summary>
        public void HideBusy()
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        protected override void ReleaseMangedResources()
        {
            base.ReleaseMangedResources();

            // Останавливаем таймер
            _timer?.Stop();

            // Отписываемся от событий
            if (_comPortService != null)
            {
                _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }

            // Освобождаем ресурсы дочерних view model
            foreach (var menuItem in MenuItems)
            {
                if (menuItem.ViewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _loggingService?.LogInfo("MainViewModel ресурсы освобождены");
        }
    }
}