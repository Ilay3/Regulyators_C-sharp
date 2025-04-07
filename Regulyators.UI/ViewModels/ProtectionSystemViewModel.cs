using System;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows.Input;
using System.Windows.Media;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для страницы защит и мониторинга
    /// </summary>
    public class ProtectionSystemViewModel : ViewModelBase
    {
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;
        private readonly Timer _updateTimer;
        private readonly Random _random; // для демо-режима

        private bool _isOilPressureProtectionActive;
        private bool _isEngineSpeedProtectionActive;
        private bool _isBoostPressureProtectionActive;
        private bool _isOilTemperatureProtectionActive;
        private string _oilPressureStatus;
        private string _engineSpeedStatus;
        private string _boostPressureStatus;
        private string _oilTemperatureStatus;
        private Brush _oilPressureStatusColor;
        private Brush _engineSpeedStatusColor;
        private Brush _boostPressureStatusColor;
        private Brush _oilTemperatureStatusColor;
        private double _oilPressureCurrent;
        private double _engineSpeedCurrent;
        private double _boostPressureCurrent;
        private double _oilTemperatureCurrent;
        private ProtectionThresholds _thresholds;
        private DateTime _lastUpdateTime;
        private string _statusMessage;
        private bool _allProtectionsEnabled;
        private bool _canResetProtection;
        private bool _isDemoMode;
        private bool _isConnected;

        // Журнал событий защиты
        private ObservableCollection<ProtectionEvent> _protectionEvents;

        #region Свойства

        /// <summary>
        /// Активность защиты по давлению масла
        /// </summary>
        public bool IsOilPressureProtectionActive
        {
            get => _isOilPressureProtectionActive;
            set
            {
                if (SetProperty(ref _isOilPressureProtectionActive, value))
                {
                    OilPressureStatusColor = value ? Brushes.Red : Brushes.Green;
                    OilPressureStatus = value ? "КРИТИЧНО" : "В НОРМЕ";
                    CanResetProtection = value || _isEngineSpeedProtectionActive ||
                                        _isBoostPressureProtectionActive || _isOilTemperatureProtectionActive;

                    if (value)
                    {
                        LogProtectionEvent("Давление масла", "Критически низкое давление масла",
                            $"Текущее: {OilPressureCurrent:F2} кг/см², порог: {Thresholds.OilPressureMinThreshold:F2} кг/см²");
                    }
                }
            }
        }

        /// <summary>
        /// Активность защиты по оборотам двигателя
        /// </summary>
        public bool IsEngineSpeedProtectionActive
        {
            get => _isEngineSpeedProtectionActive;
            set
            {
                if (SetProperty(ref _isEngineSpeedProtectionActive, value))
                {
                    EngineSpeedStatusColor = value ? Brushes.Red : Brushes.Green;
                    EngineSpeedStatus = value ? "КРИТИЧНО" : "В НОРМЕ";
                    CanResetProtection = value || _isOilPressureProtectionActive ||
                                        _isBoostPressureProtectionActive || _isOilTemperatureProtectionActive;

                    if (value)
                    {
                        LogProtectionEvent("Обороты двигателя", "Превышение максимальных оборотов",
                            $"Текущее: {EngineSpeedCurrent:F0} об/мин, порог: {Thresholds.EngineSpeedMaxThreshold:F0} об/мин");
                    }
                }
            }
        }

        /// <summary>
        /// Активность защиты по давлению наддува
        /// </summary>
        public bool IsBoostPressureProtectionActive
        {
            get => _isBoostPressureProtectionActive;
            set
            {
                if (SetProperty(ref _isBoostPressureProtectionActive, value))
                {
                    BoostPressureStatusColor = value ? Brushes.Red : Brushes.Green;
                    BoostPressureStatus = value ? "КРИТИЧНО" : "В НОРМЕ";
                    CanResetProtection = value || _isOilPressureProtectionActive ||
                                        _isEngineSpeedProtectionActive || _isOilTemperatureProtectionActive;

                    if (value)
                    {
                        LogProtectionEvent("Давление наддува", "Превышение давления наддува",
                            $"Текущее: {BoostPressureCurrent:F2} кг/см², порог: {Thresholds.BoostPressureMaxThreshold:F2} кг/см²");
                    }
                }
            }
        }

        /// <summary>
        /// Активность защиты по температуре масла
        /// </summary>
        public bool IsOilTemperatureProtectionActive
        {
            get => _isOilTemperatureProtectionActive;
            set
            {
                if (SetProperty(ref _isOilTemperatureProtectionActive, value))
                {
                    OilTemperatureStatusColor = value ? Brushes.Red : Brushes.Green;
                    OilTemperatureStatus = value ? "КРИТИЧНО" : "В НОРМЕ";
                    CanResetProtection = value || _isOilPressureProtectionActive ||
                                        _isEngineSpeedProtectionActive || _isBoostPressureProtectionActive;

                    if (value)
                    {
                        LogProtectionEvent("Температура масла", "Превышение температуры масла",
                            $"Текущее: {OilTemperatureCurrent:F1} °C, порог: {Thresholds.OilTemperatureMaxThreshold:F1} °C");
                    }
                }
            }
        }

        /// <summary>
        /// Статус давления масла
        /// </summary>
        public string OilPressureStatus
        {
            get => _oilPressureStatus;
            set => SetProperty(ref _oilPressureStatus, value);
        }

        /// <summary>
        /// Статус оборотов двигателя
        /// </summary>
        public string EngineSpeedStatus
        {
            get => _engineSpeedStatus;
            set => SetProperty(ref _engineSpeedStatus, value);
        }

        /// <summary>
        /// Статус давления наддува
        /// </summary>
        public string BoostPressureStatus
        {
            get => _boostPressureStatus;
            set => SetProperty(ref _boostPressureStatus, value);
        }

        /// <summary>
        /// Статус температуры масла
        /// </summary>
        public string OilTemperatureStatus
        {
            get => _oilTemperatureStatus;
            set => SetProperty(ref _oilTemperatureStatus, value);
        }

        /// <summary>
        /// Цвет статуса давления масла
        /// </summary>
        public Brush OilPressureStatusColor
        {
            get => _oilPressureStatusColor;
            set => SetProperty(ref _oilPressureStatusColor, value);
        }

        /// <summary>
        /// Цвет статуса оборотов двигателя
        /// </summary>
        public Brush EngineSpeedStatusColor
        {
            get => _engineSpeedStatusColor;
            set => SetProperty(ref _engineSpeedStatusColor, value);
        }

        /// <summary>
        /// Цвет статуса давления наддува
        /// </summary>
        public Brush BoostPressureStatusColor
        {
            get => _boostPressureStatusColor;
            set => SetProperty(ref _boostPressureStatusColor, value);
        }

        /// <summary>
        /// Цвет статуса температуры масла
        /// </summary>
        public Brush OilTemperatureStatusColor
        {
            get => _oilTemperatureStatusColor;
            set => SetProperty(ref _oilTemperatureStatusColor, value);
        }

        /// <summary>
        /// Текущее давление масла
        /// </summary>
        public double OilPressureCurrent
        {
            get => _oilPressureCurrent;
            set
            {
                if (SetProperty(ref _oilPressureCurrent, value))
                {
                    CheckOilPressureProtection();
                }
            }
        }

        /// <summary>
        /// Текущие обороты двигателя
        /// </summary>
        public double EngineSpeedCurrent
        {
            get => _engineSpeedCurrent;
            set
            {
                if (SetProperty(ref _engineSpeedCurrent, value))
                {
                    CheckEngineSpeedProtection();
                }
            }
        }

        /// <summary>
        /// Текущее давление наддува
        /// </summary>
        public double BoostPressureCurrent
        {
            get => _boostPressureCurrent;
            set
            {
                if (SetProperty(ref _boostPressureCurrent, value))
                {
                    CheckBoostPressureProtection();
                }
            }
        }

        /// <summary>
        /// Текущая температура масла
        /// </summary>
        public double OilTemperatureCurrent
        {
            get => _oilTemperatureCurrent;
            set
            {
                if (SetProperty(ref _oilTemperatureCurrent, value))
                {
                    CheckOilTemperatureProtection();
                }
            }
        }

        /// <summary>
        /// Пороговые значения защит
        /// </summary>
        public ProtectionThresholds Thresholds
        {
            get => _thresholds;
            set => SetProperty(ref _thresholds, value);
        }

        /// <summary>
        /// Время последнего обновления
        /// </summary>
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }

        /// <summary>
        /// Статусное сообщение
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Доступность кнопки сброса защит
        /// </summary>
        public bool CanResetProtection
        {
            get => _canResetProtection;
            set => SetProperty(ref _canResetProtection, value);
        }

        /// <summary>
        /// Включены ли все защиты
        /// </summary>
        public bool AllProtectionsEnabled
        {
            get => _allProtectionsEnabled;
            set => SetProperty(ref _allProtectionsEnabled, value);
        }

        /// <summary>
        /// Режим демонстрации (без подключения к оборудованию)
        /// </summary>
        public bool IsDemoMode
        {
            get => _isDemoMode;
            set => SetProperty(ref _isDemoMode, value);
        }

        /// <summary>
        /// Установлено ли соединение с оборудованием
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// События защиты
        /// </summary>
        public ObservableCollection<ProtectionEvent> ProtectionEvents => _protectionEvents;

        #endregion

        #region Команды

        /// <summary>
        /// Команда сброса защит
        /// </summary>
        public ICommand ResetProtectionCommand { get; }

        /// <summary>
        /// Команда включения/выключения всех защит
        /// </summary>
        public ICommand ToggleAllProtectionsCommand { get; }

        /// <summary>
        /// Команда очистки журнала событий
        /// </summary>
        public ICommand ClearEventsCommand { get; }

        /// <summary>
        /// Команда обновления значений
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Команда включения/выключения демо-режима
        /// </summary>
        public ICommand ToggleDemoModeCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public ProtectionSystemViewModel()
        {
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;
            _random = new Random();

            // Инициализация порогов защит
            _thresholds = new ProtectionThresholds();

            // Инициализация журнала событий
            _protectionEvents = new ObservableCollection<ProtectionEvent>();

            // Инициализация статусов
            OilPressureStatus = "В НОРМЕ";
            EngineSpeedStatus = "В НОРМЕ";
            BoostPressureStatus = "В НОРМЕ";
            OilTemperatureStatus = "В НОРМЕ";

            OilPressureStatusColor = Brushes.Green;
            EngineSpeedStatusColor = Brushes.Green;
            BoostPressureStatusColor = Brushes.Green;
            OilTemperatureStatusColor = Brushes.Green;

            LastUpdateTime = DateTime.Now;
            StatusMessage = "Готово к работе";

            AllProtectionsEnabled = true;
            CanResetProtection = false;
            IsDemoMode = true; // Начинаем в демо-режиме
            IsConnected = false;

            // Инициализация команд
            ResetProtectionCommand = new RelayCommand(ResetProtection, () => CanResetProtection);
            ToggleAllProtectionsCommand = new RelayCommand(ToggleAllProtections);
            ClearEventsCommand = new RelayCommand(ClearEvents);
            RefreshCommand = new RelayCommand(RefreshValues);
            ToggleDemoModeCommand = new RelayCommand(ToggleDemoMode);

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _comPortService.ErrorOccurred += OnErrorOccurred;

            // Запуск таймера обновления данных
            _updateTimer = new Timer(500); // 500 мс
            _updateTimer.Elapsed += OnUpdateTimerElapsed;
            _updateTimer.Start();

            // Логирование
            _loggingService.LogInfo("Система защит инициализирована");
        }

        /// <summary>
        /// Проверка защиты по давлению масла
        /// </summary>
        private void CheckOilPressureProtection()
        {
            if (AllProtectionsEnabled)
            {
                bool isActive = OilPressureCurrent < Thresholds.OilPressureMinThreshold;
                if (IsOilPressureProtectionActive != isActive)
                {
                    IsOilPressureProtectionActive = isActive;
                }
            }
        }

        /// <summary>
        /// Проверка защиты по оборотам двигателя
        /// </summary>
        private void CheckEngineSpeedProtection()
        {
            if (AllProtectionsEnabled)
            {
                bool isActive = EngineSpeedCurrent > Thresholds.EngineSpeedMaxThreshold;
                if (IsEngineSpeedProtectionActive != isActive)
                {
                    IsEngineSpeedProtectionActive = isActive;
                }
            }
        }

        /// <summary>
        /// Проверка защиты по давлению наддува
        /// </summary>
        private void CheckBoostPressureProtection()
        {
            if (AllProtectionsEnabled)
            {
                bool isActive = BoostPressureCurrent > Thresholds.BoostPressureMaxThreshold;
                if (IsBoostPressureProtectionActive != isActive)
                {
                    IsBoostPressureProtectionActive = isActive;
                }
            }
        }

        /// <summary>
        /// Проверка защиты по температуре масла
        /// </summary>
        private void CheckOilTemperatureProtection()
        {
            if (AllProtectionsEnabled)
            {
                bool isActive = OilTemperatureCurrent > Thresholds.OilTemperatureMaxThreshold;
                if (IsOilTemperatureProtectionActive != isActive)
                {
                    IsOilTemperatureProtectionActive = isActive;
                }
            }
        }

        /// <summary>
        /// Обработчик события получения данных от COM-порта
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            // Обновляем значения параметров
            OilPressureCurrent = parameters.OilPressure;
            EngineSpeedCurrent = parameters.EngineSpeed;
            BoostPressureCurrent = parameters.BoostPressure;
            OilTemperatureCurrent = parameters.OilTemperature;

            LastUpdateTime = parameters.Timestamp;
            StatusMessage = "Данные обновлены";
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            IsConnected = isConnected;
            StatusMessage = isConnected ? "Подключено к оборудованию" : "Нет подключения";

            // Если подключение потеряно, но не в демо-режиме, то переходим в демо-режим
            if (!isConnected && !IsDemoMode)
            {
                IsDemoMode = true;
            }
        }

        /// <summary>
        /// Обработчик события ошибки COM-порта
        /// </summary>
        private void OnErrorOccurred(object sender, string errorMessage)
        {
            StatusMessage = $"Ошибка: {errorMessage}";
            _loggingService.LogError("Ошибка COM-порта", errorMessage);
        }

        /// <summary>
        /// Обработчик таймера обновления данных
        /// </summary>
        private void OnUpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // В демо-режиме генерируем случайные значения
            if (IsDemoMode)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    GenerateDemoValues();
                    LastUpdateTime = DateTime.Now;
                });
            }
        }

        /// <summary>
        /// Генерация демонстрационных значений
        /// </summary>
        private void GenerateDemoValues()
        {
            // Генерируем случайные значения
            double oilPressure = 1.5 + _random.NextDouble() * 1.5; // от 1.5 до 3.0
            double engineSpeed = 1000 + _random.Next(0, 1500); // от 1000 до 2500
            double boostPressure = 1.0 + _random.NextDouble() * 2.0; // от 1.0 до 3.0
            double oilTemperature = 70 + _random.Next(0, 50); // от 70 до 120

            // Иногда генерируем критические значения для демонстрации
            if (_random.Next(20) == 0) // 5% вероятность
            {
                switch (_random.Next(4))
                {
                    case 0:
                        oilPressure = 0.5 + _random.NextDouble() * 0.9; // критически низкое давление масла
                        break;
                    case 1:
                        engineSpeed = 2250 + _random.Next(0, 300); // критически высокие обороты
                        break;
                    case 2:
                        boostPressure = 2.6 + _random.NextDouble() * 0.7; // критически высокое давление наддува
                        break;
                    case 3:
                        oilTemperature = 115 + _random.Next(0, 20); // критически высокая температура масла
                        break;
                }
            }

            // Обновляем значения
            OilPressureCurrent = oilPressure;
            EngineSpeedCurrent = engineSpeed;
            BoostPressureCurrent = boostPressure;
            OilTemperatureCurrent = oilTemperature;
        }

        /// <summary>
        /// Сброс защит
        /// </summary>
        private void ResetProtection()
        {
            // Сбрасываем все активные защиты
            IsOilPressureProtectionActive = false;
            IsEngineSpeedProtectionActive = false;
            IsBoostPressureProtectionActive = false;
            IsOilTemperatureProtectionActive = false;

            // В реальном приложении отправляем команду на сброс защит
            if (!IsDemoMode && IsConnected)
            {
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.ResetProtection
                });
            }

            LogProtectionEvent("Система защит", "Сброс защит", "Сброс всех активных защит");
            _loggingService.LogInfo("Сброс защит");
            StatusMessage = "Защиты сброшены";
        }

        /// <summary>
        /// Включение/выключение всех защит
        /// </summary>
        private void ToggleAllProtections()
        {
            AllProtectionsEnabled = !AllProtectionsEnabled;

            if (!AllProtectionsEnabled)
            {
                // Сбрасываем все активные защиты при отключении
                IsOilPressureProtectionActive = false;
                IsEngineSpeedProtectionActive = false;
                IsBoostPressureProtectionActive = false;
                IsOilTemperatureProtectionActive = false;

                LogProtectionEvent("Система защит", "Защиты отключены", "Все защиты отключены");
                _loggingService.LogWarning("Все защиты отключены");
                StatusMessage = "Все защиты отключены";
            }
            else
            {
                LogProtectionEvent("Система защит", "Защиты включены", "Все защиты включены");
                _loggingService.LogInfo("Все защиты включены");
                StatusMessage = "Все защиты включены";
            }
        }

        /// <summary>
        /// Очистка журнала событий
        /// </summary>
        private void ClearEvents()
        {
            _protectionEvents.Clear();
            StatusMessage = "Журнал событий очищен";
        }

        /// <summary>
        /// Обновление значений
        /// </summary>
        private void RefreshValues()
        {
            if (!IsDemoMode && IsConnected)
            {
                // Отправляем команду запроса параметров
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.GetParameters
                });

                // Отправляем команду запроса статуса защит
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.GetProtectionStatus
                });

                StatusMessage = "Запрос обновления данных отправлен";
            }
            else
            {
                // В демо-режиме просто генерируем новые значения
                GenerateDemoValues();
                LastUpdateTime = DateTime.Now;
                StatusMessage = "Данные обновлены (демо-режим)";
            }
        }

        /// <summary>
        /// Переключение демо-режима
        /// </summary>
        private void ToggleDemoMode()
        {
            IsDemoMode = !IsDemoMode;

            if (!IsDemoMode)
            {
                // Пробуем подключиться к оборудованию
                IsConnected = _comPortService.Connect();
                StatusMessage = IsConnected ? "Подключено к оборудованию" : "Ошибка подключения";

                if (IsConnected)
                {
                    _loggingService.LogInfo("Подключение к оборудованию");
                }
                else
                {
                    _loggingService.LogWarning("Ошибка подключения, возврат в демо-режим");
                    IsDemoMode = true;
                }
            }
            else
            {
                // Отключаемся от оборудования
                if (IsConnected)
                {
                    _comPortService.Disconnect();
                    IsConnected = false;
                }

                StatusMessage = "Переход в демо-режим";
                _loggingService.LogInfo("Переход в демо-режим");
            }
        }

        /// <summary>
        /// Добавление события в журнал
        /// </summary>
        private void LogProtectionEvent(string system, string message, string details)
        {
            // Создаем новое событие
            var protectionEvent = new ProtectionEvent
            {
                Timestamp = DateTime.Now,
                System = system,
                Message = message,
                Details = details
            };

            // Добавляем в журнал в UI-потоке
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _protectionEvents.Insert(0, protectionEvent); // Добавляем в начало списка

                // Ограничиваем количество записей
                if (_protectionEvents.Count > 100)
                {
                    _protectionEvents.RemoveAt(_protectionEvents.Count - 1);
                }
            });

            // Также добавляем в общий журнал системы
            _loggingService.LogWarning(message, details);
        }
    }

    /// <summary>
    /// Запись журнала событий защиты
    /// </summary>
    public class ProtectionEvent
    {
        /// <summary>
        /// Время события
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Система, в которой произошло событие
        /// </summary>
        public string System { get; set; }

        /// <summary>
        /// Сообщение о событии
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Подробности о событии
        /// </summary>
        public string Details { get; set; }
    }
}