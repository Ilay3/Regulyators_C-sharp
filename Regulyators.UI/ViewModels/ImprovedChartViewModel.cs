using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClosedXML.Excel.Drawings;
using ClosedXML.Excel;
using Microsoft.Win32;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using ScottPlot;
using DocumentFormat.OpenXml.Drawing;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для отображения графиков параметров двигателя с возможностью навигации и экспорта
    /// </summary>
    public class ImprovedChartViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;
        private readonly ComPortService _comPortService;
        private readonly SettingsService _settingsService;
        private readonly SimulationService _simulationService;

        private EngineParameters _engineParameters;
        private WpfPlot _mainPlot;
        private double _elapsedTime = 0;
        private bool _isGraphInitialized = false;
        private bool _isConnected = false;
        private string _statusMessage;
        private string _selectedTimeInterval = "30";
        private bool _autoScroll = true;


        // Коллекции для хранения данных графика
        private Dictionary<string, List<double>> _xData = new Dictionary<string, List<double>>();
        private Dictionary<string, List<double>> _yData = new Dictionary<string, List<double>>();

        // Максимальное количество точек для хранения (примерно 1 час при частоте обновления 1 раз в секунду)
        private const int MAX_POINTS = 3600;

        // Цвета для серий данных
        private readonly Dictionary<string, Color> _seriesColors = new Dictionary<string, Color>()
        {
            { "Обороты двигателя", Colors.Blue },
            { "Обороты турбокомпрессора", Colors.Purple },
            { "Давление масла", Colors.Green },
            { "Давление наддува", Colors.Orange },
            { "Температура масла", Colors.Red }
        };

        // Видимость серий на графике
        private bool _showEngineSpeed = true;
        private bool _showTurboSpeed = false;
        private bool _showOilPressure = true;
        private bool _showBoostPressure = false;
        private bool _showOilTemperature = true;

        // ★ Изменение: добавляем поля для буферизации входящих данных и таймера
        private readonly object _bufferLock = new object();
        private readonly List<EngineParameters> _incomingDataBuffer = new List<EngineParameters>();
        private DispatcherTimer _uiUpdateTimer; // таймер для перерисовки графика
        private DateTime? _startTime = null;


        private readonly Dictionary<string, ScottPlot.Plottable.ScatterPlot> _seriesPlots = new();

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
        /// Автоматическая прокрутка графика
        /// </summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                if (SetProperty(ref _autoScroll, value))
                {
                    if (value)
                    {
                        UpdateTimeWindow();
                    }
                }
            }
        }

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
        /// Флаг подключения к устройству
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// Доступные интервалы времени для графика (секунды)
        /// </summary>
        public List<string> TimeIntervals { get; } = new List<string> { "10", "30", "60", "120", "300", "Все" };

        #endregion

        #region Команды

        /// <summary>
        /// Команда очистки графика
        /// </summary>
        public ICommand ClearGraphCommand { get; }

        /// <summary>
        /// Команда экспорта графика
        /// </summary>
        public ICommand ExportImageCommand { get; }

        /// <summary>
        /// Команда экспорта данных в Excel
        /// </summary>
        public ICommand ExportExcelCommand { get; }

        /// <summary>
        /// Команда автомасштабирования
        /// </summary>
        public ICommand AutoScaleCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public ImprovedChartViewModel()
        {
            _loggingService = LoggingService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;
            _simulationService = SimulationService.Instance;

            // Инициализация коллекций для хранения данных
            foreach (var series in _seriesColors.Keys)
            {
                _xData[series] = new List<double>();
                _yData[series] = new List<double>();
            }

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
            ExportImageCommand = new RelayCommand(ExportImage);
            ExportExcelCommand = new RelayCommand(ExportExcel);
            AutoScaleCommand = new RelayCommand(AutoScale);

            // Установка порогов срабатывания
            EngineParameters.OilPressureCriticalThreshold = _settingsService.ProtectionThresholds.OilPressureMinThreshold;
            EngineParameters.EngineSpeedCriticalThreshold = _settingsService.ProtectionThresholds.EngineSpeedMaxThreshold;
            EngineParameters.BoostPressureCriticalThreshold = _settingsService.ProtectionThresholds.BoostPressureMaxThreshold;
            EngineParameters.OilTemperatureCriticalThreshold = _settingsService.ProtectionThresholds.OilTemperatureMaxThreshold;

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Подписка на события изменения настроек
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Подписка на событие изменения статуса симуляции
            _simulationService.SimulationStatusChanged += OnSimulationStatusChanged;
            _simulationService.ParametersUpdated += OnSimulationParametersUpdated;

            // Проверка соединения (учитываем и обычное соединение, и режим симуляции)
            _isConnected = _comPortService.IsConnected || _simulationService.IsSimulationRunning;
            StatusMessage = _isConnected
                ? "Подключено к оборудованию"
                : (_simulationService.IsSimulationRunning
                    ? "Работа в режиме симуляции"
                    : "Ожидание подключения к оборудованию...");

            _loggingService.LogInfo("График инициализирован и готов к получению данных");

            // ★ Изменение: инициализируем DispatcherTimer для периодического обновления графика
            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(500); // можно 200мс, 1000мс и т.д.
            _uiUpdateTimer.Tick += (s, e) => OnUiUpdateTimerTick();
            _uiUpdateTimer.Start();
        }

        /// <summary>
        /// Обработчик события изменения статуса симуляции
        /// </summary>
        private void OnSimulationStatusChanged(object sender, bool isRunning)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Обновляем статус подключения, учитывая режим симуляции
                    bool newConnectionStatus = _comPortService.IsConnected || isRunning;
                    if (IsConnected != newConnectionStatus)
                    {
                        IsConnected = newConnectionStatus;
                        StatusMessage = isRunning
                            ? "Работа в режиме симуляции"
                            : (IsConnected ? "Подключено к оборудованию" : "Ожидание подключения...");

                        _loggingService.LogInfo($"Статус соединения обновлен: {IsConnected}, симуляция: {isRunning}");
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка обработки события изменения статуса симуляции: {ex.Message}", ex.StackTrace);
            }
        }

        /// <summary>
        /// Обработчик события получения симулированных данных
        /// </summary>
        private void OnSimulationParametersUpdated(object sender, EngineParameters parameters)
        {
            try
            {
                // Передаем симулированные данные в такой же обработчик, как и для реальных данных
                OnDataReceived(sender, parameters);

                _loggingService.LogInfo($"Получены симулированные данные: Обороты={parameters.EngineSpeed:F0}, Масло={parameters.OilPressure:F2}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка обработки симулированных данных: {ex.Message}", ex.StackTrace);
            }
        }

        /// <summary>
        /// Инициализация графика
        /// </summary>
        public void InitializeGraph(WpfPlot plot)
        {
            if (plot == null)
            {
                _loggingService.LogError("Невозможно инициализировать график: plot = null");
                return;
            }

            _mainPlot = plot;
            _isGraphInitialized = true;

            _loggingService.LogInfo("Начало инициализации графика");

            try
            {
                // Настройка базовых параметров графика
                _mainPlot.Plot.Style(ScottPlot.Style.Seaborn);
                _mainPlot.Plot.Title("Параметры двигателя", bold: true);
                _mainPlot.Plot.XLabel("Время (сек)");
                _mainPlot.Plot.YLabel("Значение");

                // Настройка интерактивности
                _mainPlot.Configuration.DoubleClickBenchmark = false;
                _mainPlot.Configuration.LeftClickDragPan = true;
                _mainPlot.Configuration.RightClickDragZoom = true;
                _mainPlot.Configuration.ScrollWheelZoom = true;
                _mainPlot.Configuration.LockVerticalAxis = false;

                // ★ Изменение: включаем очередь рендеринга (опционально)
                _mainPlot.Configuration.UseRenderQueue = true;

                // Настройка обработчиков для отключения автоскролла при ручном масштабировании/прокрутке
                _mainPlot.MouseDown += (sender, e) =>
                {
                    if (e.ChangedButton == System.Windows.Input.MouseButton.Left || e.ChangedButton == System.Windows.Input.MouseButton.Right)
                        AutoScroll = false;
                };
                _mainPlot.MouseWheel += (sender, e) => AutoScroll = false;

                // Создаем начальные пустые серии для графика
                foreach (var series in _seriesColors.Keys)
                {
                    Color color = _seriesColors[series];
                    var drawingColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

                    var scatter = _mainPlot.Plot.AddScatter(
                        new double[] { 0 }, 
                        new double[] { 0 },
                        drawingColor,
                        label: series);


                    scatter.LineWidth = 2;
                    scatter.MarkerSize = 0;
                    scatter.IsVisible = series switch
                    {
                        "Обороты двигателя" => ShowEngineSpeed,
                        "Обороты турбокомпрессора" => ShowTurboSpeed,
                        "Давление масла" => ShowOilPressure,
                        "Давление наддува" => ShowBoostPressure,
                        "Температура масла" => ShowOilTemperature,
                        _ => false
                    };

                    _seriesPlots[series] = scatter;
                }


                // Настройка легенды
                _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                // Устанавливаем начальные границы осей
                _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2000);
                _mainPlot.Refresh();

                _loggingService.LogInfo("График успешно инициализирован");
                StatusMessage = "График инициализирован и готов к работе";

                // Проверяем статус соединения (включая симуляцию) после инициализации
                bool newConnectionStatus = _comPortService.IsConnected || _simulationService.IsSimulationRunning;
                if (IsConnected != newConnectionStatus)
                {
                    IsConnected = newConnectionStatus;
                    StatusMessage = _simulationService.IsSimulationRunning
                        ? "Работа в режиме симуляции"
                        : (IsConnected ? "Подключено к оборудованию" : "Ожидание подключения...");

                    _loggingService.LogInfo($"Статус соединения после инициализации графика: {IsConnected}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при инициализации графика", ex.Message);
                StatusMessage = "Ошибка при инициализации графика";
            }
        }

        /// <summary>
        /// Обработчик события получения данных
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            try
            {
                // Проверка на нулевые данные
                if (parameters == null)
                {
                    _loggingService.LogWarning("Получены нулевые данные", "OnDataReceived");
                    return;
                }

                // При первом получении данных обновляем UI (IsConnected, StatusMessage) - если нужно
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        StatusMessage = _simulationService.IsSimulationRunning
                            ? "Работа в режиме симуляции"
                            : "Подключено к оборудованию";
                    }

                    // Для отображения в интерфейсе можем сразу обновить значения
                    EngineParameters.EngineSpeed = parameters.EngineSpeed;
                    EngineParameters.TurboCompressorSpeed = parameters.TurboCompressorSpeed;
                    EngineParameters.OilPressure = parameters.OilPressure;
                    EngineParameters.BoostPressure = parameters.BoostPressure;
                    EngineParameters.OilTemperature = parameters.OilTemperature;
                    EngineParameters.RackPosition = parameters.RackPosition;
                    EngineParameters.Timestamp = parameters.Timestamp;

                    StatusMessage = $"Данные обновлены: {parameters.Timestamp:HH:mm:ss}, " +
                                    $"Обороты: {parameters.EngineSpeed:F0}, " +
                                    $"Масло: {parameters.OilPressure:F2}";
                });

                // ★ Изменение: вместо немедленного UpdateGraph() просто складываем данные в буфер
                lock (_bufferLock)
                {
                    _incomingDataBuffer.Add(parameters);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки полученных данных", ex.Message);
            }
        }

        /// <summary>
        /// Метод таймера, который раз в N мс обновляет график
        /// </summary>
        private void OnUiUpdateTimerTick()
        {
            // 1) Извлекаем накопленные данные из буфера
            List<EngineParameters> newData = null;
            lock (_bufferLock)
            {
                if (_incomingDataBuffer.Count > 0)
                {
                    newData = new List<EngineParameters>(_incomingDataBuffer);
                    _incomingDataBuffer.Clear();
                }
            }

            if (newData == null || newData.Count == 0)
            {
                // Нет новых точек - ничего не делаем
                return;
            }

            // 2) Добавляем точки в наши коллекции
            foreach (var p in newData)
            {
                if (_startTime == null)
                    _startTime = p.Timestamp;

                double x = (p.Timestamp - _startTime.Value).TotalSeconds;

                AddDataPoint("Обороты двигателя", x, p.EngineSpeed);
                AddDataPoint("Обороты турбокомпрессора", x, p.TurboCompressorSpeed);
                AddDataPoint("Давление масла", x, p.OilPressure);
                AddDataPoint("Давление наддува", x, p.BoostPressure);
                AddDataPoint("Температура масла", x, p.OilTemperature);

            }

            // 3) Обновляем график один раз
            UpdateGraph();
        }

        /// <summary>
        /// Добавление точки данных на график
        /// </summary>
        private void AddDataPoint(string series, double x, double y)
        {
            if (!_xData.ContainsKey(series) || !_yData.ContainsKey(series))
                return;

            _xData[series].Add(x);
            _yData[series].Add(y);

            if (_xData[series].Count > MAX_POINTS)
            {
                _xData[series].RemoveAt(0);
                _yData[series].RemoveAt(0);
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
                    // Обновляем статус подключения, учитывая режим симуляции
                    bool newConnectionStatus = isConnected || _simulationService.IsSimulationRunning;
                    if (IsConnected != newConnectionStatus)
                    {
                        IsConnected = newConnectionStatus;

                        if (_simulationService.IsSimulationRunning)
                        {
                            StatusMessage = "Работа в режиме симуляции";
                        }
                        else
                        {
                            StatusMessage = isConnected
                                ? "Подключено к оборудованию"
                                : "Отключено от оборудования";
                        }

                        _loggingService.LogInfo($"Статус соединения изменен: {IsConnected}, реальное: {isConnected}, симуляция: {_simulationService.IsSimulationRunning}");
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка обработки изменения статуса подключения: {ex.Message}", ex.StackTrace);
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
                        // Обновляем пороги срабатывания защит
                        EngineParameters.OilPressureCriticalThreshold = _settingsService.ProtectionThresholds.OilPressureMinThreshold;
                        EngineParameters.EngineSpeedCriticalThreshold = _settingsService.ProtectionThresholds.EngineSpeedMaxThreshold;
                        EngineParameters.BoostPressureCriticalThreshold = _settingsService.ProtectionThresholds.BoostPressureMaxThreshold;
                        EngineParameters.OilTemperatureCriticalThreshold = _settingsService.ProtectionThresholds.OilTemperatureMaxThreshold;

                        // Добавляем на график линии порогов защит
                        UpdateThresholdLines();
                    });
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Ошибка обработки изменения настроек", ex.Message);
                }
            }
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateGraph()
        {
            if (!_isGraphInitialized || _mainPlot == null)
                return;

            try
            {
                // Обновляем данные для каждой серии
                var scatterPlots = _mainPlot.Plot.GetPlottables()
                    .OfType<ScottPlot.Plottable.ScatterPlot>().ToList();

                foreach (var series in _seriesColors.Keys)
                {
                    if (!_seriesPlots.ContainsKey(series)) continue;

                    var scatter = _seriesPlots[series];



                    if (_xData[series].Count > 0 && _yData[series].Count > 0)
                    {
                        scatter.Update(_xData[series].ToArray(), _yData[series].ToArray());
                    }


                    scatter.IsVisible = series switch
                    {
                        "Обороты двигателя" => ShowEngineSpeed,
                        "Обороты турбокомпрессора" => ShowTurboSpeed,
                        "Давление масла" => ShowOilPressure,
                        "Давление наддува" => ShowBoostPressure,
                        "Температура масла" => ShowOilTemperature,
                        _ => false
                    };
                }


                // Обновляем линии порогов защит
                UpdateThresholdLines();

                // Если включен автоскролл, обновляем временное окно
                if (AutoScroll)
                {
                    UpdateTimeWindow();
                }

                // Обновляем график
                _mainPlot.Refresh();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления графика", ex.Message);
            }
        }

        /// <summary>
        /// Обновление линий порогов защит на графике
        /// </summary>
        private void UpdateThresholdLines()
        {
            if (!_isGraphInitialized || _mainPlot == null)
                return;

            try
            {
                // Очищаем горизонтальные линии на графике
                var horizontalLines = _mainPlot.Plot.GetPlottables().OfType<ScottPlot.Plottable.HLine>().ToList();
                foreach (var line in horizontalLines)
                {
                    _mainPlot.Plot.Remove(line);
                }

                // Добавляем линии порогов защит, если соответствующие серии видимы
                if (ShowEngineSpeed)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.EngineSpeedCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Макс. обороты");
                }

                if (ShowOilPressure)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.OilPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Мин. давление масла");
                }

                if (ShowBoostPressure)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.BoostPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Макс. давление наддува");
                }

                if (ShowOilTemperature)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.OilTemperatureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Макс. температура масла");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления линий порогов", ex.Message);
            }
        }

        /// <summary>
        /// Обновление временного окна графика
        /// </summary>
        private void UpdateTimeWindow()
        {
            if (!_isGraphInitialized || _mainPlot == null)
                return;

            try
            {
                // Если выбран режим "Все", показываем все данные
                if (_selectedTimeInterval == "Все")
                {
                    _mainPlot.Plot.AxisAutoX();
                    return;
                }

                // Если задан численный интервал
                if (int.TryParse(_selectedTimeInterval, out int seconds))
                {
                    // Устанавливаем видимый диапазон по оси X
                    double minX = Math.Max(0, _elapsedTime - seconds);
                    double maxX = Math.Max(seconds, _elapsedTime);

                    _mainPlot.Plot.SetAxisLimitsX(minX, maxX);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления временного окна", ex.Message);
            }
        }

        /// <summary>
        /// Очистка графика
        /// </summary>
        private void ClearGraph()
        {
            try
            {
                _loggingService.LogInfo("Начало очистки графика");

                // Очищаем все коллекции точек
                foreach (var series in _xData.Keys.ToList())
                {
                    _xData[series].Clear();
                    _yData[series].Clear();
                }

                // Сбрасываем таймер
                _elapsedTime = 0;
                _startTime = null;

                // Очищаем буфер входящих данных
                lock (_bufferLock)
                {
                    _incomingDataBuffer.Clear();
                }

                // Обновляем график
                if (_isGraphInitialized && _mainPlot != null)
                {
                    UpdateGraph();
                    _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2000);
                    _mainPlot.Refresh();
                }

                StatusMessage = "График очищен";
                _loggingService.LogInfo("График успешно очищен");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при очистке графика", ex.Message);
                StatusMessage = "Ошибка при очистке графика";
            }
        }

        /// <summary>
        /// Экспорт графика в файл изображения
        /// </summary>
        private void ExportImage()
        {
            if (!_isGraphInitialized || _mainPlot == null)
            {
                StatusMessage = "График не инициализирован";
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    DefaultExt = ".png",
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
                    Title = "Сохранение графика как изображения"
                };

                if (dialog.ShowDialog() == true)
                {
                    _mainPlot.Plot.SaveFig(dialog.FileName);
                    _loggingService.LogInfo("График сохранен в файл", dialog.FileName);
                    StatusMessage = "График сохранен как изображение";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при экспорте графика", ex.Message);
                StatusMessage = "Ошибка при экспорте графика как изображения";
            }
        }

        /// <summary>
        /// Экспортирует текущий вид (изображение) графика в Excel-файл.
        /// Использует библиотеку ClosedXML (бесплатная, MIT лицензия).
        /// </summary>
        private void ExportExcel()
        {
            if (!_isGraphInitialized || _mainPlot == null)
            {
                StatusMessage = "График не инициализирован";
                return;
            }

            try
            {
                // Выбираем, куда сохранить Excel
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    Title = "Экспорт графика в Excel (как изображение)"
                };

                if (dialog.ShowDialog() != true)
                    return; // пользователь отменил

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Chart");

                    // 1) Сохраняем график во ВРЕМЕННЫЙ PNG-файл
                    string tempFile = System.IO.Path.GetTempFileName() + ".png";
                    _mainPlot.Plot.SaveFig(tempFile);

                    // 2) Загружаем файл в MemoryStream
                    byte[] fileBytes = File.ReadAllBytes(tempFile);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        // 3) Добавляем картинку из Stream
                        // Второй аргумент - «имя картинки» (любая строка)
                        // NOTE: Никаких XLPictureFormat здесь не используем
                        var pic = worksheet.AddPicture(ms, "ChartImage")
                                           .MoveTo(worksheet.Cell(1, 1)) // вставка в A1
                                           .WithSize(800, 600);          // ширина/высота в пикселях
                    }

                    // (Опционально) удаляем временный файл
                    File.Delete(tempFile);

                    // 4) Сохраняем Excel
                    workbook.SaveAs(dialog.FileName);
                }

                StatusMessage = "График экспортирован в Excel как изображение";
                _loggingService.LogInfo("График успешно встроен в Excel", dialog.FileName);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при экспорте графика в Excel", ex.Message);
                StatusMessage = "Ошибка при экспорте графика в Excel";
            }
        }

        /// <summary>
        /// Автоматическое масштабирование графика
        /// </summary>
        private void AutoScale()
        {
            if (!_isGraphInitialized || _mainPlot == null)
                return;

            try
            {
                _mainPlot.Plot.AxisAuto();
                _mainPlot.Refresh();
                StatusMessage = "Автоматическое масштабирование графика выполнено";

                // Отключаем автопрокрутку
                AutoScroll = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка масштабирования графика", ex.Message);
                StatusMessage = "Ошибка масштабирования графика";
            }
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void CleanUp()
        {
            try
            {
                // Отписываемся от событий
                if (_comPortService != null)
                {
                    _comPortService.DataReceived -= OnDataReceived;
                    _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                }

                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= OnSettingsChanged;
                }

                if (_simulationService != null)
                {
                    _simulationService.SimulationStatusChanged -= OnSimulationStatusChanged;
                    _simulationService.ParametersUpdated -= OnSimulationParametersUpdated;
                }

                // Остановить и убрать таймер
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer = null;

                // Очистка ссылок на объекты графика
                _mainPlot = null;

                _loggingService?.LogInfo("ImprovedChartViewModel: ресурсы освобождены");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Ошибка при очистке ресурсов ImprovedChartViewModel", ex.Message);
            }
        }
    }
}
