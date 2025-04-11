using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using ScottPlot;
using ScottPlot.Plottable;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для улучшенного отображения графиков параметров
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
        private bool _dataReceived = false;
        private bool _autoScroll = true;
        private bool _normalizeValues = false;
        private DateTime _lastRefreshTime = DateTime.Now;
        private bool _isButtonEnabled = true;

        // Максимальное количество точек для хранения
        private const int MAX_POINTS = 3600; // 1 час при частоте обновления 1 раз в секунду

        // Словарь для хранения серий данных графика
        private Dictionary<string, List<DataPoint>> _dataPoints = new Dictionary<string, List<DataPoint>>();
        private Dictionary<string, ScatterPlot> _plotSeries = new Dictionary<string, ScatterPlot>();

        // Цвета для серий данных
        private readonly Dictionary<string, Color> _seriesColors = new Dictionary<string, Color>()
        {
            { "Обороты двигателя", Colors.Blue },
            { "Обороты турбокомпрессора", Colors.Purple },
            { "Давление масла", Colors.Green },
            { "Давление наддува", Colors.Orange },
            { "Температура масла", Colors.Red }
        };

        // Настройки отображения серий
        private bool _showEngineSpeed = true;
        private bool _showTurboSpeed = false;
        private bool _showOilPressure = true;
        private bool _showBoostPressure = false;
        private bool _showOilTemperature = true;
        private string _selectedTimeInterval = "30";

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
        /// Нормализация значений для лучшей визуализации
        /// </summary>
        public bool NormalizeValues
        {
            get => _normalizeValues;
            set
            {
                if (SetProperty(ref _normalizeValues, value))
                {
                    UpdateNormalizedValues();
                    UpdatePlot();
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
                    UpdateVisibleSeries();
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
                    UpdateVisibleSeries();
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
                    UpdateVisibleSeries();
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
                    UpdateVisibleSeries();
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
                    UpdateVisibleSeries();
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
        /// Доступность кнопок управления (блокировка во время операций)
        /// </summary>
        public bool IsButtonEnabled
        {
            get => _isButtonEnabled;
            set => SetProperty(ref _isButtonEnabled, value);
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
        public ICommand ExportGraphCommand { get; }

        /// <summary>
        /// Команда экспорта данных
        /// </summary>
        public ICommand ExportDataCommand { get; }

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

            // Логирование - проверка инициализации сервисов
            _loggingService.LogInfo("Инициализация ImprovedChartViewModel");
            _loggingService.LogInfo($"ComPortService: {(_comPortService != null ? "OK" : "NULL")}");
            _loggingService.LogInfo($"SimulationService: {(_simulationService != null ? "OK" : "NULL")}");

            // Инициализация словаря для хранения данных серий
            foreach (var series in _seriesColors.Keys)
            {
                _dataPoints[series] = new List<DataPoint>();

                // Добавляем начальную точку, чтобы избежать ошибки пустых массивов
                _dataPoints[series].Add(new DataPoint { X = 0, Y = 0 });
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
            ClearGraphCommand = new RelayCommand(ClearGraph, () => _isButtonEnabled);
            ExportGraphCommand = new RelayCommand<string>(ExportGraph, _ => _isButtonEnabled);
            ExportDataCommand = new RelayCommand(ExportData, () => _isButtonEnabled && _dataReceived);
            AutoScaleCommand = new RelayCommand(AutoScale, () => _isButtonEnabled);

            // Установка порогов срабатывания
            EngineParameters.OilPressureCriticalThreshold = _settingsService.ProtectionThresholds.OilPressureMinThreshold;
            EngineParameters.EngineSpeedCriticalThreshold = _settingsService.ProtectionThresholds.EngineSpeedMaxThreshold;
            EngineParameters.BoostPressureCriticalThreshold = _settingsService.ProtectionThresholds.BoostPressureMaxThreshold;
            EngineParameters.OilTemperatureCriticalThreshold = _settingsService.ProtectionThresholds.OilTemperatureMaxThreshold;

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Дополнительно: подписка на события от сервиса симуляции
            _simulationService.ParametersUpdated += OnSimulationParametersUpdated;

            // Подписка на события изменения настроек
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Проверка соединения
            _isConnected = _comPortService.IsConnected;
            StatusMessage = _isConnected ? "Подключено к оборудованию" : "Ожидание подключения к оборудованию...";

            _loggingService.LogInfo("График инициализирован и готов к получению данных");
        }

        /// <summary>
        /// Обработчик события обновления параметров симуляции
        /// </summary>
        private void OnSimulationParametersUpdated(object sender, EngineParameters parameters)
        {
            if (parameters == null) return;

            try
            {
                // Избегаем конфликтов потоков UI
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Обновляем параметры двигателя
                    EngineParameters.EngineSpeed = parameters.EngineSpeed;
                    EngineParameters.TurboCompressorSpeed = parameters.TurboCompressorSpeed;
                    EngineParameters.OilPressure = parameters.OilPressure;
                    EngineParameters.BoostPressure = parameters.BoostPressure;
                    EngineParameters.OilTemperature = parameters.OilTemperature;
                    EngineParameters.RackPosition = parameters.RackPosition;
                    EngineParameters.Timestamp = parameters.Timestamp;

                    // Добавляем новую точку на график
                    _elapsedTime += 0.5; // Увеличиваем на 0.5 сек

                    // Добавляем данные в коллекции точек
                    AddDataPoint("Обороты двигателя", _elapsedTime, parameters.EngineSpeed);
                    AddDataPoint("Обороты турбокомпрессора", _elapsedTime, parameters.TurboCompressorSpeed);
                    AddDataPoint("Давление масла", _elapsedTime, parameters.OilPressure);
                    AddDataPoint("Давление наддува", _elapsedTime, parameters.BoostPressure);
                    AddDataPoint("Температура масла", _elapsedTime, parameters.OilTemperature);

                    // Периодическое логирование для отладки (каждые 5 секунд)
                    if ((int)_elapsedTime % 5 == 0)
                    {
                        _loggingService.LogInfo($"Симуляция: точка добавлена t={_elapsedTime:F1}, об/мин={parameters.EngineSpeed:F0}");
                    }

                    // Отмечаем, что получили данные
                    _dataReceived = true;

                    // Обновляем график (но не слишком часто)
                    var now = DateTime.Now;
                    if ((now - _lastRefreshTime).TotalMilliseconds > 500) // Не чаще 2 раз в секунду
                    {
                        _lastRefreshTime = now;
                        UpdatePlot();
                    }

                    // Обновляем статус
                    StatusMessage = $"Симуляция: данные обновлены: {parameters.Timestamp:HH:mm:ss}, Обороты: {parameters.EngineSpeed:F0}, Масло: {parameters.OilPressure:F2}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки данных симуляции", ex.Message);
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

                // Настройка интерактивности
                _mainPlot.Configuration.DoubleClickBenchmark = false;
                _mainPlot.Configuration.LeftClickDragPan = true;
                _mainPlot.Configuration.RightClickDragZoom = true;
                _mainPlot.Configuration.ScrollWheelZoom = true;
                _mainPlot.Configuration.LockVerticalAxis = false;

                // Настройка обработчиков событий для отключения автоскролла
                _mainPlot.MouseDown += (sender, e) => {
                    if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
                        AutoScroll = false;
                };

                _mainPlot.MouseWheel += (sender, e) => AutoScroll = false;

                // Настраиваем шрифты и размеры
                _mainPlot.Plot.XAxis.TickLabelStyle(fontSize: 12);
                _mainPlot.Plot.YAxis.TickLabelStyle(fontSize: 12);

                // Создаем серии данных для графика
                InitializeDataSeries();

                // Задаем обработчик изменения размера графика
                _mainPlot.SizeChanged += (sender, e) => {
                    _loggingService.LogInfo("Изменение размера графика");
                    RefreshPlot();
                };

                // Принудительно обновляем график
                _mainPlot.Plot.AxisAuto();
                _mainPlot.Refresh();

                _loggingService.LogInfo("График успешно инициализирован");
                StatusMessage = "График инициализирован и готов к работе";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при инициализации графика", ex.Message);
                StatusMessage = "Ошибка при инициализации графика";
            }
        }

        /// <summary>
        /// Инициализация серий данных для графика
        /// </summary>
        private void InitializeDataSeries()
        {
            if (_mainPlot == null) return;

            try
            {
                _loggingService.LogInfo("Инициализация серий данных для графика");

                // Очищаем существующие серии
                _plotSeries.Clear();
                _mainPlot.Plot.Clear();

                // Создаем и настраиваем серии данных
                foreach (var series in _seriesColors)
                {
                    string seriesName = series.Key;
                    Color color = series.Value;

                    // Преобразуем цвет WPF в цвет System.Drawing
                    var drawingColor = System.Drawing.Color.FromArgb(
                        color.A, color.R, color.G, color.B);

                    // Создаем массивы данных с минимум одной точкой
                    double[] xData = new double[] { 0 };
                    double[] yData = new double[] { 0 };

                    // Получаем данные из хранилища, если они есть
                    if (_dataPoints.TryGetValue(seriesName, out var points) && points.Count > 0)
                    {
                        xData = points.Select(p => p.X).ToArray();
                        yData = points.Select(p => p.Y).ToArray();

                        _loggingService.LogInfo($"Серия {seriesName} содержит {points.Count} точек");
                    }

                    // Создаем серию на графике
                    var seriesPlot = _mainPlot.Plot.AddScatter(
                        xData,
                        yData,
                        drawingColor,
                        label: seriesName,
                        markerSize: 0);  // Отключаем маркеры для лучшей производительности

                    seriesPlot.LineWidth = 2;

                    // Устанавливаем стартовую видимость серии
                    bool isVisible = seriesName == "Обороты двигателя" ||
                                    seriesName == "Давление масла" ||
                                    seriesName == "Температура масла";
                    seriesPlot.IsVisible = isVisible;

                    // Сохраняем серию в словаре
                    _plotSeries[seriesName] = seriesPlot;

                    _loggingService.LogInfo($"Серия {seriesName} добавлена на график, видимость: {isVisible}");
                }

                // Настраиваем пользовательский интерфейс графика
                _mainPlot.Plot.Title("Параметры двигателя", bold: true);
                _mainPlot.Plot.XLabel("Время (сек)");
                _mainPlot.Plot.YLabel("Значение");
                _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                // Устанавливаем начальные границы осей
                _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2000);

                // Обновляем график
                _mainPlot.Refresh();

                _loggingService.LogInfo("Серии данных для графика успешно инициализированы");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка инициализации серий данных", ex.Message);
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

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Обновляем параметры двигателя
                    EngineParameters.EngineSpeed = parameters.EngineSpeed;
                    EngineParameters.TurboCompressorSpeed = parameters.TurboCompressorSpeed;
                    EngineParameters.OilPressure = parameters.OilPressure;
                    EngineParameters.BoostPressure = parameters.BoostPressure;
                    EngineParameters.OilTemperature = parameters.OilTemperature;
                    EngineParameters.RackPosition = parameters.RackPosition;
                    EngineParameters.Timestamp = parameters.Timestamp;

                    // Добавляем новую точку на график
                    _elapsedTime += 0.5; // Увеличиваем на 0.5 сек

                    // Добавляем данные в коллекции точек
                    AddDataPoint("Обороты двигателя", _elapsedTime, parameters.EngineSpeed);
                    AddDataPoint("Обороты турбокомпрессора", _elapsedTime, parameters.TurboCompressorSpeed);
                    AddDataPoint("Давление масла", _elapsedTime, parameters.OilPressure);
                    AddDataPoint("Давление наддува", _elapsedTime, parameters.BoostPressure);
                    AddDataPoint("Температура масла", _elapsedTime, parameters.OilTemperature);

                    // Отмечаем, что получили данные
                    _dataReceived = true;

                    // Обновляем график (но не слишком часто)
                    var now = DateTime.Now;
                    if ((now - _lastRefreshTime).TotalMilliseconds > 500) // Не чаще 2 раз в секунду
                    {
                        _lastRefreshTime = now;
                        UpdatePlot();
                    }

                    // Обновляем статус
                    StatusMessage = $"Данные обновлены: {parameters.Timestamp:HH:mm:ss}, Обороты: {parameters.EngineSpeed:F0}, Масло: {parameters.OilPressure:F2}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки полученных данных", ex.Message);
            }
        }

        /// <summary>
        /// Добавление точки данных в коллекцию серии
        /// </summary>
        private void AddDataPoint(string seriesName, double x, double y)
        {
            if (!_dataPoints.ContainsKey(seriesName))
            {
                _dataPoints[seriesName] = new List<DataPoint>();
                // Добавляем начальную точку, чтобы избежать ошибки пустых массивов
                _dataPoints[seriesName].Add(new DataPoint { X = 0, Y = 0 });
            }

            // Добавляем новую точку (убеждаемся, что Y не отрицательное)
            _dataPoints[seriesName].Add(new DataPoint { X = x, Y = Math.Max(0, y) });

            // Ограничиваем количество точек для предотвращения переполнения памяти
            if (_dataPoints[seriesName].Count > MAX_POINTS)
            {
                _dataPoints[seriesName].RemoveAt(0);
            }

            // Периодическое логирование (каждые 10 точек)
            if (_dataPoints[seriesName].Count % 10 == 0 && seriesName == "Обороты двигателя")
            {
                _loggingService.LogInfo($"В серии {seriesName} добавлена точка: X={x}, Y={y}, всего точек: {_dataPoints[seriesName].Count}");
            }
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            _isConnected = isConnected;
            StatusMessage = isConnected ? "Подключено к оборудованию" : "Отключено от оборудования";
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
                    Application.Current.Dispatcher.InvokeAsync(() =>
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
        /// Нормализация значений для лучшего отображения на одном графике
        /// </summary>
        private void UpdateNormalizedValues()
        {
            if (!_normalizeValues || !_dataReceived)
                return;

            try
            {
                // Для каждой серии находим максимальное значение
                Dictionary<string, double> maxValues = new Dictionary<string, double>();

                foreach (var series in _dataPoints.Keys)
                {
                    if (_dataPoints[series].Count > 0)
                    {
                        double maxVal = _dataPoints[series].Max(p => p.Y);
                        maxValues[series] = maxVal > 0 ? maxVal : 1.0;
                    }
                    else
                    {
                        maxValues[series] = 1.0;
                    }
                }

                // Создаем новые нормализованные серии
                Dictionary<string, List<DataPoint>> normalizedSeries = new Dictionary<string, List<DataPoint>>();

                foreach (var series in _dataPoints.Keys)
                {
                    normalizedSeries[series] = new List<DataPoint>();

                    foreach (var point in _dataPoints[series])
                    {
                        // Нормализуем значение (от 0 до 100%)
                        normalizedSeries[series].Add(new DataPoint
                        {
                            X = point.X,
                            Y = (point.Y / maxValues[series]) * 100.0
                        });
                    }
                }

                // Заменяем данные в сериях
                if (_mainPlot != null)
                {
                    foreach (var series in _plotSeries.Keys)
                    {
                        if (normalizedSeries.TryGetValue(series, out var points) && points.Count > 0)
                        {
                            double[] xData = points.Select(p => p.X).ToArray();
                            double[] yData = points.Select(p => p.Y).ToArray();

                            if (_plotSeries.TryGetValue(series, out var plot))
                            {
                                plot.Update(xData, yData);
                            }
                        }
                    }

                    // Обновляем ось Y
                    _mainPlot.Plot.YLabel(_normalizeValues ? "Значение (%)" : "Значение");

                    // Обновляем легенду с информацией о максимумах
                    if (_normalizeValues)
                    {
                        foreach (var series in _plotSeries.Keys)
                        {
                            if (_plotSeries.TryGetValue(series, out var plot) && maxValues.TryGetValue(series, out var maxVal))
                            {
                                string unit = "";
                                switch (series)
                                {
                                    case "Обороты двигателя":
                                        unit = "об/мин";
                                        break;
                                    case "Обороты турбокомпрессора":
                                        unit = "об/мин";
                                        break;
                                    case "Давление масла":
                                    case "Давление наддува":
                                        unit = "кг/см²";
                                        break;
                                    case "Температура масла":
                                        unit = "°C";
                                        break;
                                }
                                plot.Label = $"{series} (макс: {maxVal:F1} {unit})";
                            }
                        }
                    }
                    else
                    {
                        // Возвращаем стандартные подписи
                        foreach (var series in _plotSeries.Keys)
                        {
                            if (_plotSeries.TryGetValue(series, out var plot))
                            {
                                plot.Label = series;
                            }
                        }
                    }

                    // Обновляем график
                    _mainPlot.Plot.Legend(location: Alignment.UpperRight);
                    _mainPlot.Refresh();
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при нормализации значений", ex.Message);
            }
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdatePlot()
        {
            if (!_isGraphInitialized || _mainPlot == null) return;

            try
            {
                // Если данных нет, просто устанавливаем начальные оси
                if (!_dataReceived)
                {
                    _mainPlot.Plot.SetAxisLimits(0, 30, 0, 100);
                    _mainPlot.Refresh();
                    return;
                }

                // Обновляем данные для каждой серии
                foreach (var seriesName in _seriesColors.Keys)
                {
                    if (_plotSeries.TryGetValue(seriesName, out var seriesPlot) &&
                        _dataPoints.TryGetValue(seriesName, out var points) &&
                        points.Count > 0)
                    {
                        try
                        {
                            // Преобразуем точки в массивы x и y
                            double[] xData = points.Select(p => p.X).ToArray();
                            double[] yData = points.Select(p => p.Y).ToArray();

                            // Проверяем, что у нас есть данные
                            if (xData.Length > 0 && yData.Length > 0)
                            {
                                // Обновляем данные серии
                                seriesPlot.Update(xData, yData);
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogError($"Ошибка обновления серии {seriesName}: {ex.Message}", ex.StackTrace);
                        }
                    }
                }

                // Обновляем видимость серий в соответствии с настройками
                UpdateVisibleSeries();

                // Обновляем линии порогов защит
                UpdateThresholdLines();

               

                // Если включена нормализация, обновляем значения
                if (_normalizeValues)
                {
                    UpdateNormalizedValues();
                }

                // Обновляем окно просмотра, если включен автоскролл
                if (_autoScroll)
                {
                    UpdateTimeWindow();
                }

                // Обновляем график
                _mainPlot.Refresh();

                _loggingService.LogInfo("График успешно обновлен");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления графика", ex.Message);
            }
        }

        /// <summary>
        /// Обновление видимости серий на графике
        /// </summary>
        private void UpdateVisibleSeries()
        {
            if (_mainPlot == null) return;

            try
            {
                // Устанавливаем видимость серий в соответствии с настройками
                if (_plotSeries.TryGetValue("Обороты двигателя", out var engineSpeedSeries))
                    engineSpeedSeries.IsVisible = _showEngineSpeed;

                if (_plotSeries.TryGetValue("Обороты турбокомпрессора", out var turboSpeedSeries))
                    turboSpeedSeries.IsVisible = _showTurboSpeed;

                if (_plotSeries.TryGetValue("Давление масла", out var oilPressureSeries))
                    oilPressureSeries.IsVisible = _showOilPressure;

                if (_plotSeries.TryGetValue("Давление наддува", out var boostPressureSeries))
                    boostPressureSeries.IsVisible = _showBoostPressure;

                if (_plotSeries.TryGetValue("Температура масла", out var oilTemperatureSeries))
                    oilTemperatureSeries.IsVisible = _showOilTemperature;

                // Обновляем легенду
                _mainPlot.Plot.Legend();

                // Обновляем график
                RefreshPlot();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления видимости серий", ex.Message);
            }
        }

        /// <summary>
        /// Обновление временного окна графика
        /// </summary>
        private void UpdateTimeWindow()
        {
            if (!_isGraphInitialized || _mainPlot == null) return;

            try
            {
                // Если нет данных, нечего обновлять
                if (!_dataReceived) return;

                // Если выбран режим "Все", показываем все данные
                if (_selectedTimeInterval == "Все")
                {
                    _mainPlot.Plot.AxisAutoX();
                    _mainPlot.Plot.AxisAutoY();
                    _mainPlot.Refresh();
                    return;
                }

                // Если задан численный интервал
                if (int.TryParse(_selectedTimeInterval, out int seconds))
                {
                    // Устанавливаем видимый диапазон по оси X
                    double minX = Math.Max(0, _elapsedTime - seconds);
                    double maxX = Math.Max(seconds, _elapsedTime);

                    // Находим минимальные и максимальные значения Y в видимом диапазоне
                    double minY = double.MaxValue;
                    double maxY = double.MinValue;
                    bool hasVisibleData = false;

                    foreach (var series in _plotSeries)
                    {
                        if (!series.Value.IsVisible) continue;

                        if (_dataPoints.TryGetValue(series.Key, out var points))
                        {
                            var visiblePoints = points.Where(p => p.X >= minX && p.X <= maxX).ToList();
                            if (visiblePoints.Count > 0)
                            {
                                double seriesMinY = visiblePoints.Min(p => p.Y);
                                double seriesMaxY = visiblePoints.Max(p => p.Y);

                                minY = Math.Min(minY, seriesMinY);
                                maxY = Math.Max(maxY, seriesMaxY);
                                hasVisibleData = true;
                            }
                        }
                    }

                    // Устанавливаем границы
                    _mainPlot.Plot.SetAxisLimitsX(minX, maxX);

                    // Если есть видимые данные, устанавливаем границы Y с запасом 10%
                    if (hasVisibleData)
                    {
                        double padding = (maxY - minY) * 0.1;
                        _mainPlot.Plot.SetAxisLimitsY(Math.Max(0, minY - padding), maxY + padding);
                    }
                    else
                    {
                        // Если нет видимых данных, используем автомасштабирование
                        _mainPlot.Plot.AxisAutoY();
                    }

                    _mainPlot.Refresh();
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления временного окна", ex.Message);
            }
        }

        /// <summary>
        /// Обновление линий порогов защит на графике
        /// </summary>
        private void UpdateThresholdLines()
        {
            if (_mainPlot == null || !_dataReceived) return;

            try
            {
                // Очищаем горизонтальные линии на графике
                _mainPlot.Plot.Clear(typeof(HLine));

                // Если включена нормализация, не показываем линии порогов
                if (_normalizeValues) return;

                // Добавляем линии порогов защит, если соответствующие серии видимы
                if (_showEngineSpeed)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.EngineSpeedCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0), // Полупрозрачный красный
                        1.5f,
                        LineStyle.Dash,
                        "Макс. обороты");
                }

                if (_showOilPressure)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.OilPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Мин. давление масла");
                }

                if (_showBoostPressure)
                {
                    _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.BoostPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash,
                        "Макс. давление наддува");
                }

                if (_showOilTemperature)
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
        /// Обновление графика без пересоздания серий
        /// </summary>
        private void RefreshPlot()
        {
            if (_mainPlot == null) return;

            try
            {
                // Обновляем легенду
                _mainPlot.Plot.Legend();

                // Обновляем график
                _mainPlot.Refresh();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления графика", ex.Message);
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

                // Сохраняем текущий _elapsedTime
                double currentTime = _elapsedTime;

                // Очищаем все коллекции точек
                foreach (var series in _dataPoints.Keys.ToList())
                {
                    _dataPoints[series].Clear();
                    // Добавляем начальную точку в 0,0 для избежания ошибок
                    _dataPoints[series].Add(new DataPoint { X = currentTime, Y = 0 });
                }

                // Сбрасываем флаг наличия данных
                _dataReceived = false;

                // Очищаем график
                if (_mainPlot != null)
                {
                    // Сбрасываем границы осей
                    _mainPlot.Plot.SetAxisLimits(currentTime, currentTime + 30, 0, 100);

                    // Переинициализируем серии данных
                    InitializeDataSeries();

                    // Обновляем график
                    RefreshPlot();
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
        /// Экспорт графика в файл
        /// </summary>
        private void ExportGraph(string format)
        {
            if (_mainPlot == null)
            {
                StatusMessage = "График не инициализирован";
                return;
            }

            try
            {
                // Блокируем кнопки на время операции
                IsButtonEnabled = false;

                // Создаем диалог сохранения файла
                var dialog = new SaveFileDialog
                {
                    DefaultExt = $".{format.ToLower()}",
                    Filter = $"{format} Image (*.{format.ToLower()})|*.{format.ToLower()}|All Files (*.*)|*.*",
                    Title = "Сохранить график"
                };

                // Если пользователь выбрал файл
                if (dialog.ShowDialog() == true)
                {
                    // Сохраняем график в файл
                    _mainPlot.Plot.SaveFig(dialog.FileName);
                    _loggingService.LogInfo($"График сохранен в файл {format}", dialog.FileName);
                    StatusMessage = $"График сохранен в файл {format}";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при экспорте графика", ex.Message);
                StatusMessage = "Ошибка при экспорте графика";
            }
            finally
            {
                // Разблокируем кнопки
                IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// Экспорт данных в CSV-файл
        /// </summary>
        private void ExportData()
        {
            try
            {
                // Блокируем кнопки на время операции
                IsButtonEnabled = false;

                // Проверяем наличие данных
                if (!_dataReceived)
                {
                    StatusMessage = "Нет данных для экспорта";
                    return;
                }

                // Создаем диалог сохранения файла
                var dialog = new SaveFileDialog
                {
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Экспорт данных графика"
                };

                // Если пользователь выбрал файл
                if (dialog.ShowDialog() == true)
                {
                    // Создаем CSV-файл вручную, с корректным форматированием
                    using (var writer = new System.IO.StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Записываем заголовки колонок
                        writer.WriteLine("Время;Обороты двигателя;Обороты турбокомпрессора;Давление масла;Давление наддува;Температура масла");

                        // Создаем словарь для быстрого доступа к данным по времени
                        var timePoints = new SortedDictionary<double, Dictionary<string, double>>();

                        // Заполняем словарь данными
                        foreach (var series in _dataPoints)
                        {
                            string seriesName = series.Key;
                            var points = series.Value;

                            foreach (var point in points)
                            {
                                if (!timePoints.ContainsKey(point.X))
                                {
                                    timePoints[point.X] = new Dictionary<string, double>();
                                }
                                timePoints[point.X][seriesName] = point.Y;
                            }
                        }

                        // Записываем данные
                        foreach (var timePoint in timePoints)
                        {
                            double time = timePoint.Key;
                            var values = timePoint.Value;

                            // Получаем значения для каждой серии (если нет, то NaN)
                            double engineSpeed = values.TryGetValue("Обороты двигателя", out double val1) ? val1 : double.NaN;
                            double turboSpeed = values.TryGetValue("Обороты турбокомпрессора", out double val2) ? val2 : double.NaN;
                            double oilPressure = values.TryGetValue("Давление масла", out double val3) ? val3 : double.NaN;
                            double boostPressure = values.TryGetValue("Давление наддува", out double val4) ? val4 : double.NaN;
                            double oilTemp = values.TryGetValue("Температура масла", out double val5) ? val5 : double.NaN;

                            // Форматируем строку CSV
                            writer.WriteLine(string.Format(
                                "{0};{1};{2};{3};{4};{5}",
                                time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                                double.IsNaN(engineSpeed) ? "" : engineSpeed.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                                double.IsNaN(turboSpeed) ? "" : turboSpeed.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                                double.IsNaN(oilPressure) ? "" : oilPressure.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                                double.IsNaN(boostPressure) ? "" : boostPressure.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                                double.IsNaN(oilTemp) ? "" : oilTemp.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                            ));
                        }
                    }

                    _loggingService.LogInfo("Данные графика экспортированы в CSV", dialog.FileName);
                    StatusMessage = "Данные экспортированы в CSV";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при экспорте данных", ex.Message);
                StatusMessage = "Ошибка при экспорте данных";
            }
            finally
            {
                // Разблокируем кнопки
                IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// Автоматическое масштабирование графика
        /// </summary>
        private void AutoScale()
        {
            if (_mainPlot == null) return;

            try
            {
                // Блокируем кнопки на время операции
                IsButtonEnabled = false;

                // Автоматически настраиваем масштаб графика
                if (_dataReceived)
                {
                    _mainPlot.Plot.AxisAuto();
                    _mainPlot.Refresh();
                    StatusMessage = "Автоматическое масштабирование графика выполнено";

                    // Отключаем автопрокрутку
                    AutoScroll = false;
                }
                else
                {
                    // Устанавливаем разумные начальные границы
                    _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2500);
                    _mainPlot.Refresh();
                    StatusMessage = "Установлены начальные границы осей";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка масштабирования графика", ex.Message);
                StatusMessage = "Ошибка масштабирования графика";
            }
            finally
            {
                // Разблокируем кнопки
                IsButtonEnabled = true;
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

                if (_simulationService != null)
                {
                    _simulationService.ParametersUpdated -= OnSimulationParametersUpdated;
                }

                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= OnSettingsChanged;
                }

                // Очистка ссылок на объекты графика
                _mainPlot = null;
                _plotSeries?.Clear();
                _dataPoints?.Clear();

                _loggingService?.LogInfo("ImprovedChartViewModel: ресурсы освобождены");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Ошибка при очистке ресурсов ImprovedChartViewModel", ex.Message);
            }
        }
    }

    /// <summary>
    /// Класс для хранения точки данных графика
    /// </summary>
    public class DataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}