using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using static Regulyators.UI.Services.ComPortService;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для страницы защит и мониторинга
    /// </summary>
    public class ProtectionSystemViewModel : ViewModelBase
    {
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;
        private readonly SettingsService _settingsService;

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
        /// Команда экспорта журнала событий
        /// </summary>
        public ICommand ExportEventsCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public ProtectionSystemViewModel()
        {
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация порогов защит
            _thresholds = _settingsService.ProtectionThresholds;

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
            IsConnected = _comPortService.IsConnected;

            // Инициализация команд
            ResetProtectionCommand = new RelayCommand(ResetProtection, () => CanResetProtection && IsConnected);
            ToggleAllProtectionsCommand = new RelayCommand(ToggleAllProtections, () => IsConnected);
            ClearEventsCommand = new RelayCommand(ClearEvents);
            RefreshCommand = new RelayCommand(RefreshValues, () => IsConnected);
            ExportEventsCommand = new RelayCommand(ExportEvents);

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _comPortService.ErrorOccurred += OnErrorOccurred;
            _comPortService.ProtectionStatusUpdated += OnProtectionStatusUpdated;

            // Подписка на события изменения настроек
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Логирование
            _loggingService.LogInfo("Система защит инициализирована");

            // Запрос текущих параметров, если есть соединение
            if (IsConnected)
            {
                RefreshValues();
            }
        }

        /// <summary>
        /// Экспорт журнала событий
        /// </summary>
        private void ExportEvents()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Экспорт журнала событий защиты"
                };

                if (dialog.ShowDialog() == true)
                {
                    string filePath = dialog.FileName;

                    // Используем сервис экспорта
                    var exportService = ExportService.Instance;
                    var result = exportService.ExportProtectionEventsToCSVAsync(
                        new List<ProtectionEvent>(_protectionEvents), filePath).Result;

                    if (result)
                    {
                        StatusMessage = $"Журнал событий защиты экспортирован в {filePath}";
                        _loggingService.LogInfo("Журнал событий защиты экспортирован", filePath);
                    }
                    else
                    {
                        StatusMessage = "Ошибка при экспорте журнала событий";
                        _loggingService.LogError("Ошибка при экспорте журнала событий");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка при экспорте журнала событий";
                _loggingService.LogError("Ошибка при экспорте журнала событий", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события обновления статуса защит
        /// </summary>
        private void OnProtectionStatusUpdated(object sender, ProtectionStatus status)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Обновляем статусы защит
                    IsOilPressureProtectionActive = status.IsOilPressureActive;
                    IsEngineSpeedProtectionActive = status.IsEngineSpeedActive;
                    IsBoostPressureProtectionActive = status.IsBoostPressureActive;
                    IsOilTemperatureProtectionActive = status.IsOilTemperatureActive;

                    // Обновляем статус включения защит
                    AllProtectionsEnabled = status.AllProtectionsEnabled;

                    LastUpdateTime = DateTime.Now;

                    StatusMessage = "Получен статус защит";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки статуса защит", ex.Message);
            }
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
            try
            {
                // Обновляем значения параметров
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OilPressureCurrent = parameters.OilPressure;
                    EngineSpeedCurrent = parameters.EngineSpeed;
                    BoostPressureCurrent = parameters.BoostPressure;
                    OilTemperatureCurrent = parameters.OilTemperature;

                    LastUpdateTime = parameters.Timestamp;
                    StatusMessage = "Данные обновлены";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки данных", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = isConnected;
                    StatusMessage = isConnected ? "Подключено к оборудованию" : "Нет подключения";

                    if (isConnected)
                    {
                        // Запрашиваем текущие параметры
                        RefreshValues();
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки изменения статуса подключения", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события ошибки COM-порта
        /// </summary>
        private void OnErrorOccurred(object sender, string errorMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Ошибка: {errorMessage}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки события ошибки COM-порта", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события изменения настроек
        /// </summary>
        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (e.SettingsType == SettingsType.Protection || e.SettingsType == SettingsType.All)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Обновляем пороги защит
                        Thresholds = _settingsService.ProtectionThresholds;

                        // Проверяем все защиты с новыми порогами
                        CheckOilPressureProtection();
                        CheckEngineSpeedProtection();
                        CheckBoostPressureProtection();
                        CheckOilTemperatureProtection();

                        StatusMessage = "Пороги защит обновлены";
                    });
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Ошибка обработки изменения настроек", ex.Message);
                }
            }
        }

        /// <summary>
        /// Сброс защит
        /// </summary>
        private async void ResetProtection()
        {
            // Отправляем команду сброса защит через COM-порт
            if (IsConnected)
            {
                StatusMessage = "Выполняется сброс защит...";

                bool result = await _comPortService.SendCommandAsync(new ERCHM30TZCommand
                {
                    CommandType = CommandType.ResetProtection
                });

                if (result)
                {
                    // Сбрасываем состояние защит в UI
                    IsOilPressureProtectionActive = false;
                    IsEngineSpeedProtectionActive = false;
                    IsBoostPressureProtectionActive = false;
                    IsOilTemperatureProtectionActive = false;

                    LogProtectionEvent("Система защит", "Сброс защит", "Сброс всех активных защит");
                    _loggingService.LogInfo("Выполнен сброс защит");
                    StatusMessage = "Защиты сброшены";
                }
                else
                {
                    _loggingService.LogError("Ошибка при сбросе защит");
                    StatusMessage = "Ошибка при сбросе защит";
                }
            }
            else
            {
                StatusMessage = "Невозможно сбросить защиты: нет соединения";
                _loggingService.LogWarning("Попытка сброса защит при отсутствии соединения");
            }
        }

        /// <summary>
        /// Включение/выключение всех защит
        /// </summary>
        private async void ToggleAllProtections()
        {
            bool newState = !AllProtectionsEnabled;

            // Отправляем команду включения/выключения защит
            if (IsConnected)
            {
                StatusMessage = $"Выполняется {(newState ? "включение" : "отключение")} защит...";

                // Здесь может потребоваться реализация специальной команды в протоколе
                // Пока просто меняем локальное состояние
                AllProtectionsEnabled = newState;

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

                    // Повторно проверяем состояние защит
                    CheckOilPressureProtection();
                    CheckEngineSpeedProtection();
                    CheckBoostPressureProtection();
                    CheckOilTemperatureProtection();
                }
            }
            else
            {
                StatusMessage = "Невозможно изменить состояние защит: нет соединения";
                _loggingService.LogWarning("Попытка изменения состояния защит при отсутствии соединения");
            }
        }

        /// <summary>
        /// Очистка журнала событий
        /// </summary>
        private void ClearEvents()
        {
            _protectionEvents.Clear();
            StatusMessage = "Журнал событий очищен";
            _loggingService.LogInfo("Журнал событий защиты очищен");
        }

        /// <summary>
        /// Обновление значений
        /// </summary>
        private void RefreshValues()
        {
            if (IsConnected)
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
                StatusMessage = "Невозможно обновить данные: нет соединения";
                _loggingService.LogWarning("Попытка обновления данных при отсутствии соединения");
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
            Application.Current.Dispatcher.Invoke(() =>
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