using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using ScottPlot;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для отображения параметров двигателя
    /// </summary>
    public class EngineParametersViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;
        private readonly GraphService _graphService;
        private readonly ComPortService _comPortService;
        private readonly SettingsService _settingsService;

        private EngineParameters _engineParameters;
        private WpfPlot _parametersPlot;
        private double _elapsedTime = 0;
        private bool _isGraphInitialized = false;
        private bool _isConnected = false;

        // Параметры отображения
        private bool _showEngineSpeed = true;
        private bool _showTurboSpeed = false;
        private bool _showOilPressure = true;
        private bool _showBoostPressure = false;
        private bool _showOilTemperature = true;
        private string _selectedTimeInterval = "30";
        private string _statusMessage;

        #region Свойства

        /// <summary>
        /// Параметры двигателя
        /// </summary>
        public EngineParameters EngineParameters
        {
            get => _engineParameters;
            set => SetProperty(ref _engineParameters, value);
        }

        /// <summary>
        /// Цвет для отображения давления масла
        /// </summary>
        public Brush OilPressureColor =>
            EngineParameters.IsOilPressureCritical ? Brushes.Red : Brushes.Black;

        /// <summary>
        /// Цвет для отображения давления наддува
        /// </summary>
        public Brush BoostPressureColor =>
            EngineParameters.IsBoostPressureCritical ? Brushes.Red : Brushes.Black;

        /// <summary>
        /// Цвет для отображения температуры масла
        /// </summary>
        public Brush OilTemperatureColor =>
            EngineParameters.IsOilTemperatureCritical ? Brushes.Red : Brushes.Black;

        /// <summary>
        /// Цвет индикатора критического давления масла
        /// </summary>
        public Brush OilPressureCriticalColor =>
            EngineParameters.IsOilPressureCritical ? Brushes.Red : Brushes.Green;

        /// <summary>
        /// Цвет индикатора критических оборотов двигателя
        /// </summary>
        public Brush EngineSpeedCriticalColor =>
            EngineParameters.IsEngineSpeedCritical ? Brushes.Red : Brushes.Green;

        /// <summary>
        /// Цвет индикатора критического давления наддува
        /// </summary>
        public Brush BoostPressureCriticalColor =>
            EngineParameters.IsBoostPressureCritical ? Brushes.Red : Brushes.Green;

        /// <summary>
        /// Цвет индикатора критической температуры масла
        /// </summary>
        public Brush OilTemperatureCriticalColor =>
            EngineParameters.IsOilTemperatureCritical ? Brushes.Red : Brushes.Green;

        /// <summary>
        /// Флаг отображения оборотов двигателя на графике
        /// </summary>
        public bool ShowEngineSpeed
        {
            get => _showEngineSpeed;
            set
            {
                if (SetProperty(ref _showEngineSpeed, value))
                {
                    UpdateGraph();
                }
            }
        }

        /// <summary>
        /// Флаг отображения оборотов турбокомпрессора на графике
        /// </summary>
        public bool ShowTurboSpeed
        {
            get => _showTurboSpeed;
            set
            {
                if (SetProperty(ref _showTurboSpeed, value))
                {
                    UpdateGraph();
                }
            }
        }

        /// <summary>
        /// Флаг отображения давления масла на графике
        /// </summary>
        public bool ShowOilPressure
        {
            get => _showOilPressure;
            set
            {
                if (SetProperty(ref _showOilPressure, value))
                {
                    UpdateGraph();
                }
            }
        }

        /// <summary>
        /// Флаг отображения давления наддува на графике
        /// </summary>
        public bool ShowBoostPressure
        {
            get => _showBoostPressure;
            set
            {
                if (SetProperty(ref _showBoostPressure, value))
                {
                    UpdateGraph();
                }
            }
        }

        /// <summary>
        /// Флаг отображения температуры масла на графике
        /// </summary>
        public bool ShowOilTemperature
        {
            get => _showOilTemperature;
            set
            {
                if (SetProperty(ref _showOilTemperature, value))
                {
                    UpdateGraph();
                }
            }
        }

        /// <summary>
        /// Выбранный интервал времени для графика
        /// </summary>
        public string SelectedTimeInterval
        {
            get => _selectedTimeInterval;
            set
            {
                if (SetProperty(ref _selectedTimeInterval, value))
                {
                    UpdateTimeWindow();
                }
            }
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
        /// Доступные интервалы времени для графика (секунды)
        /// </summary>
        public List<string> TimeIntervals { get; } = new List<string> { "10", "30", "60", "120", "300" };

        #endregion

        #region Команды

        /// <summary>
        /// Команда очистки графика
        /// </summary>
        public ICommand ClearGraphCommand { get; }

        /// <summary>
        /// Команда экспорта графика
        /// </summary>
        public ICommand ExportGraphCommand { get; }

        /// <summary>
        /// Команда обновления данных
        /// </summary>
        public ICommand RefreshDataCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public EngineParametersViewModel()
        {
            _loggingService = LoggingService.Instance;
            _graphService = GraphService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация серий данных для графика
            _graphService.InitSeries("Обороты двигателя", 600);
            _graphService.InitSeries("Обороты турбокомпрессора", 600);
            _graphService.InitSeries("Давление масла", 600);
            _graphService.InitSeries("Давление наддува", 600);
            _graphService.InitSeries("Температура масла", 600);

            // Инициализация параметров двигателя
            EngineParameters = new EngineParameters
            {
                EngineSpeed = 0,
                TurboCompressorSpeed = 0,
                OilPressure = 0,
                BoostPressure = 0,
                OilTemperature = 0,
                RackPosition = 0,
                Timestamp = DateTime.Now
            };

            // Инициализация команд
            ClearGraphCommand = new RelayCommand(ClearGraph);
            ExportGraphCommand = new RelayCommand(ExportGraph);
            RefreshDataCommand = new RelayCommand(RefreshData);

            // Установка порогов срабатывания
            EngineParameters.OilPressureCriticalThreshold = _settingsService.ProtectionThresholds.OilPressureMinThreshold;
            EngineParameters.EngineSpeedCriticalThreshold = _settingsService.ProtectionThresholds.EngineSpeedMaxThreshold;
            EngineParameters.BoostPressureCriticalThreshold = _settingsService.ProtectionThresholds.BoostPressureMaxThreshold;
            EngineParameters.OilTemperatureCriticalThreshold = _settingsService.ProtectionThresholds.OilTemperatureMaxThreshold;

            // Подписка на события изменения свойств модели
            EngineParameters.PropertyChanged += (sender, args) =>
            {
                // Обновление цветов при изменении статусов
                if (args.PropertyName.Contains("Critical") ||
                    args.PropertyName == nameof(EngineParameters.OilPressure) ||
                    args.PropertyName == nameof(EngineParameters.EngineSpeed) ||
                    args.PropertyName == nameof(EngineParameters.BoostPressure) ||
                    args.PropertyName == nameof(EngineParameters.OilTemperature))
                {
                    OnPropertyChanged(nameof(OilPressureColor));
                    OnPropertyChanged(nameof(BoostPressureColor));
                    OnPropertyChanged(nameof(OilTemperatureColor));
                    OnPropertyChanged(nameof(OilPressureCriticalColor));
                    OnPropertyChanged(nameof(EngineSpeedCriticalColor));
                    OnPropertyChanged(nameof(BoostPressureCriticalColor));
                    OnPropertyChanged(nameof(OilTemperatureCriticalColor));

                    // Логирование критических событий
                    LogCriticalParameters();
                }
            };

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _comPortService.ErrorOccurred += OnErrorOccurred;

            // Подписка на события изменения настроек
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Проверка соединения
            _isConnected = _comPortService.IsConnected;
            if (_isConnected)
            {
                StatusMessage = "Подключено к оборудованию";
                RefreshData();
            }
            else
            {
                StatusMessage = "Нет подключения к оборудованию";
            }

            // Логирование
            _loggingService.LogInfo("Запущен мониторинг параметров двигателя");
        }

        #region Методы

        /// <summary>
        /// Инициализация графика
        /// </summary>
        public void InitializeGraph(WpfPlot plot)
        {
            _parametersPlot = plot;
            _isGraphInitialized = true;

            // Настройка базовых параметров графика
            if (_parametersPlot != null)
            {
                // Отключаем меню правой кнопки мыши
                _parametersPlot.Configuration.DoubleClickBenchmark = false;
                _parametersPlot.Configuration.LeftClickDragPan = false;
                _parametersPlot.Configuration.RightClickDragZoom = false;
                _parametersPlot.Configuration.LockVerticalAxis = true;

                // Настройка внешнего вида
                _parametersPlot.Plot.Style(ScottPlot.Style.Seaborn);
                _parametersPlot.Plot.Title("Параметры двигателя");
                _parametersPlot.Plot.XLabel("Время (сек)");
                _parametersPlot.Plot.YLabel("Значение");

                UpdateGraph();
            }
        }

        /// <summary>
        /// Обработчик события получения данных от COM-порта
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Обновляем значения параметров
                    EngineParameters.EngineSpeed = parameters.EngineSpeed;
                    EngineParameters.TurboCompressorSpeed = parameters.TurboCompressorSpeed;
                    EngineParameters.OilPressure = parameters.OilPressure;
                    EngineParameters.BoostPressure = parameters.BoostPressure;
                    EngineParameters.OilTemperature = parameters.OilTemperature;
                    EngineParameters.RackPosition = parameters.RackPosition;
                    EngineParameters.Timestamp = parameters.Timestamp;

                    // Добавляем данные на график
                    AddDataToGraph();

                    // Обновляем статус
                    StatusMessage = $"Данные обновлены: {parameters.Timestamp:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки полученных данных", ex.Message);
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
                    _isConnected = isConnected;
                    StatusMessage = isConnected ? "Подключено к оборудованию" : "Нет подключения к оборудованию";

                    // Если подключились, запрашиваем данные
                    if (isConnected)
                    {
                        RefreshData();
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
            try
            {
                if (e.SettingsType == SettingsType.Protection || e.SettingsType == SettingsType.All)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Обновляем пороги срабатывания защит
                        EngineParameters.OilPressureCriticalThreshold = _settingsService.ProtectionThresholds.OilPressureMinThreshold;
                        EngineParameters.EngineSpeedCriticalThreshold = _settingsService.ProtectionThresholds.EngineSpeedMaxThreshold;
                        EngineParameters.BoostPressureCriticalThreshold = _settingsService.ProtectionThresholds.BoostPressureMaxThreshold;
                        EngineParameters.OilTemperatureCriticalThreshold = _settingsService.ProtectionThresholds.OilTemperatureMaxThreshold;

                        StatusMessage = "Пороговые значения защит обновлены";
                    });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки изменения настроек", ex.Message);
            }
        }

        /// <summary>
        /// Добавление данных на график
        /// </summary>
        private void AddDataToGraph()
        {
            _elapsedTime += 0.5; // Прирост 0.5 сек

            _graphService.AddDataPoint("Обороты двигателя", _elapsedTime, EngineParameters.EngineSpeed);
            _graphService.AddDataPoint("Обороты турбокомпрессора", _elapsedTime, EngineParameters.TurboCompressorSpeed);
            _graphService.AddDataPoint("Давление масла", _elapsedTime, EngineParameters.OilPressure);
            _graphService.AddDataPoint("Давление наддува", _elapsedTime, EngineParameters.BoostPressure);
            _graphService.AddDataPoint("Температура масла", _elapsedTime, EngineParameters.OilTemperature);

            // Обновляем график
            UpdateGraph();
        }

        /// <summary>
        /// Очистка графика
        /// </summary>
        private void ClearGraph()
        {
            _graphService.ClearAllSeries();
            _elapsedTime = 0;
            UpdateGraph();

            // Логирование
            _loggingService.LogInfo("График параметров очищен");
            StatusMessage = "График очищен";
        }

        /// <summary>
        /// Экспорт графика в файл
        /// </summary>
        private void ExportGraph()
        {
            if (_parametersPlot != null)
            {
                try
                {
                    // Создаем диалог сохранения файла
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        DefaultExt = ".png",
                        Filter = "PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg|BMP Image (.bmp)|*.bmp",
                        Title = "Сохранить график"
                    };

                    // Если пользователь выбрал файл
                    if (dialog.ShowDialog() == true)
                    {
                        // Сохраняем график в файл
                        _parametersPlot.Plot.SaveFig(dialog.FileName);
                        _loggingService.LogInfo($"График сохранен в файл", dialog.FileName);
                        StatusMessage = "График сохранен в файл";
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Ошибка при экспорте графика", ex.Message);
                    StatusMessage = "Ошибка при экспорте графика";
                }
            }
            else
            {
                _loggingService.LogWarning("Экспорт графика невозможен", "График не инициализирован");
                StatusMessage = "Экспорт графика невозможен: график не инициализирован";
            }
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateGraph()
        {
            if (_isGraphInitialized && _parametersPlot != null)
            {
                var seriesToShow = new List<string>();

                // Выбираем серии для отображения
                if (ShowEngineSpeed)
                    seriesToShow.Add("Обороты двигателя");
                if (ShowTurboSpeed)
                    seriesToShow.Add("Обороты турбокомпрессора");
                if (ShowOilPressure)
                    seriesToShow.Add("Давление масла");
                if (ShowBoostPressure)
                    seriesToShow.Add("Давление наддува");
                if (ShowOilTemperature)
                    seriesToShow.Add("Температура масла");

                // Конфигурируем график
                _graphService.ConfigurePlot(_parametersPlot, seriesToShow.ToArray());

                // Устанавливаем временное окно
                UpdateTimeWindow();
            }
        }

        /// <summary>
        /// Обновление временного окна графика
        /// </summary>
        private void UpdateTimeWindow()
        {
            if (_isGraphInitialized && _parametersPlot != null && int.TryParse(_selectedTimeInterval, out int seconds))
            {
                // Устанавливаем видимый диапазон по оси X
                double minX = Math.Max(0, _elapsedTime - seconds);
                double maxX = Math.Max(seconds, _elapsedTime);

                _parametersPlot.Plot.SetAxisLimitsX(minX, maxX);
                _parametersPlot.Refresh();
            }
        }

        /// <summary>
        /// Запрос обновления данных
        /// </summary>
        private void RefreshData()
        {
            if (_isConnected)
            {
                // Отправляем команду запроса параметров
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.GetParameters
                });

                StatusMessage = "Запрос данных отправлен";
            }
            else
            {
                StatusMessage = "Невозможно получить данные: нет подключения";
                _loggingService.LogWarning("Попытка запроса данных при отсутствии подключения");
            }
        }

        /// <summary>
        /// Логирование критических параметров
        /// </summary>
        private void LogCriticalParameters()
        {
            // Проверяем, изменился ли статус критических параметров
            if (EngineParameters.IsOilPressureCritical)
            {
                _loggingService.LogWarning("Низкое давление масла",
                    $"{EngineParameters.OilPressure:F2} кг/см², порог: {EngineParameters.OilPressureCriticalThreshold:F2} кг/см²");
            }

            if (EngineParameters.IsEngineSpeedCritical)
            {
                _loggingService.LogWarning("Превышение оборотов двигателя",
                    $"{EngineParameters.EngineSpeed:F0} об/мин, порог: {EngineParameters.EngineSpeedCriticalThreshold:F0} об/мин");
            }

            if (EngineParameters.IsBoostPressureCritical)
            {
                _loggingService.LogWarning("Высокое давление наддува",
                    $"{EngineParameters.BoostPressure:F2} кг/см², порог: {EngineParameters.BoostPressureCriticalThreshold:F2} кг/см²");
            }

            if (EngineParameters.IsOilTemperatureCritical)
            {
                _loggingService.LogWarning("Высокая температура масла",
                    $"{EngineParameters.OilTemperature:F1} °C, порог: {EngineParameters.OilTemperatureCriticalThreshold:F1} °C");
            }
        }

        #endregion
    }
}