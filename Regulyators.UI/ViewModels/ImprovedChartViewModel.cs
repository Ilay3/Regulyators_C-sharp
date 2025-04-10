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

        private EngineParameters _engineParameters;
        private WpfPlot _mainPlot;
        private double _elapsedTime = 0;
        private bool _isGraphInitialized = false;
        private bool _isConnected = false;
        private string _statusMessage;
        private bool _dataReceived = false;
        private bool _autoScroll = true;
        private DateTime _lastRefreshTime = DateTime.Now;

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

        /// <summary>
        /// Команда тестирования (добавление демо-данных)
        /// </summary>
        public ICommand GenerateTestDataCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public ImprovedChartViewModel()
        {
            _loggingService = LoggingService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация словаря для хранения данных серий
            foreach (var series in _seriesColors.Keys)
            {
                _dataPoints[series] = new List<DataPoint>();
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
            ExportGraphCommand = new RelayCommand<string>(ExportGraph);
            ExportDataCommand = new RelayCommand(ExportData);
            AutoScaleCommand = new RelayCommand(AutoScale);
            GenerateTestDataCommand = new RelayCommand(GenerateTestData);

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

            // Проверка соединения
            _isConnected = _comPortService.IsConnected;
            StatusMessage = _isConnected ? "Подключено к оборудованию" : "Ожидание подключения к оборудованию...";

            _loggingService.LogInfo("График инициализирован и готов к получению данных");
        }

        /// <summary>
        /// Инициализация графика
        /// </summary>
        public void InitializeGraph(WpfPlot plot)
        {
            _mainPlot = plot;
            _isGraphInitialized = true;

            // Настройка базовых параметров графика
            if (_mainPlot != null)
            {
                try
                {
                    // Включаем интерактивность
                    _mainPlot.Configuration.DoubleClickBenchmark = false;
                    _mainPlot.Configuration.LeftClickDragPan = true;
                    _mainPlot.Configuration.RightClickDragZoom = true;
                    _mainPlot.Configuration.ScrollWheelZoom = true;
                    _mainPlot.Configuration.LockVerticalAxis = false;

                    // При нажатии на кнопку или прокрутке колеса, отключаем автоскролл
                    _mainPlot.MouseDown += (sender, e) => AutoScroll = false;
                    _mainPlot.MouseWheel += (sender, e) => AutoScroll = false;

                    // Настройка внешнего вида
                    _mainPlot.Plot.Style(ScottPlot.Style.Seaborn);
                    _mainPlot.Plot.Title("Параметры двигателя", bold: true);
                    _mainPlot.Plot.XLabel("Время (сек)");
                    _mainPlot.Plot.YLabel("Значение");
                    _mainPlot.Plot.XAxis.TickLabelStyle(fontSize: 12);
                    _mainPlot.Plot.YAxis.TickLabelStyle(fontSize: 12);
                    _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                    // Добавляем текст "Ожидание данных..." на график
                    _mainPlot.Plot.AddText("Ожидание данных...", 0.5, 0.5,
                        size: 24, color: System.Drawing.Color.Gray)
                        .Alignment = ScottPlot.Alignment.MiddleCenter;

                    // Устанавливаем начальные границы осей
                    _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2500);

                    // Создаем серии данных для графика
                    InitializeDataSeries();

                    // Задаем обработчик изменения размера графика
                    _mainPlot.SizeChanged += (sender, e) => RefreshPlot();

                    // Обновляем график
                    RefreshPlot();

                    _loggingService.LogInfo("График успешно инициализирован");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Ошибка при инициализации графика", ex.Message);
                    StatusMessage = "Ошибка при инициализации графика";
                }
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

                    // Создаем массивы данных
                    double[] xData = new double[0];
                    double[] yData = new double[0];

                    // Получаем данные из хранилища, если они есть
                    if (_dataPoints.TryGetValue(seriesName, out var points) && points.Count > 0)
                    {
                        xData = points.Select(p => p.X).ToArray();
                        yData = points.Select(p => p.Y).ToArray();
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
                }

                // Обновляем легенду
                _mainPlot.Plot.Legend();

                _loggingService.LogInfo("Серии данных для графика инициализированы");
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

                Application.Current.Dispatcher.Invoke(() =>
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
            }

            // Добавляем новую точку
            _dataPoints[seriesName].Add(new DataPoint { X = x, Y = y });

            // Ограничиваем количество точек для предотвращения переполнения памяти
            if (_dataPoints[seriesName].Count > MAX_POINTS)
            {
                _dataPoints[seriesName].RemoveAt(0);
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
        /// Генерация тестовых данных (для демонстрации)
        /// </summary>
        private void GenerateTestData()
        {
            try
            {
                Random random = new Random();

                // Очищаем старые данные
                ClearGraph();

                // Генерируем 600 точек (300 секунд с шагом 0.5 сек)
                for (double time = 0; time < 300; time += 0.5)
                {
                    _elapsedTime = time;

                    // Генерируем реалистичные данные
                    double engineSpeed = 800 + 600 * Math.Sin(time / 20) + random.Next(-50, 50);
                    double turboSpeed = engineSpeed * 8 + random.Next(-400, 400);
                    double oilPressure = 2 + engineSpeed / 1500 + random.Next(-10, 10) / 10.0;
                    double boostPressure = 1 + engineSpeed / 2000 + random.Next(-10, 10) / 10.0;
                    double oilTemp = 80 + engineSpeed / 100 + random.Next(-5, 5);

                    // Добавляем данные в коллекции точек
                    AddDataPoint("Обороты двигателя", time, engineSpeed);
                    AddDataPoint("Обороты турбокомпрессора", time, turboSpeed);
                    AddDataPoint("Давление масла", time, oilPressure);
                    AddDataPoint("Давление наддува", time, boostPressure);
                    AddDataPoint("Температура масла", time, oilTemp);
                }

                // Отмечаем, что получили данные
                _dataReceived = true;

                // Обновляем график
                UpdatePlot();

                // Включаем автопрокрутку и обновляем окно просмотра
                AutoScroll = true;
                UpdateTimeWindow();

                StatusMessage = "Сгенерированы тестовые данные";
                _loggingService.LogInfo("Сгенерированы тестовые данные для графика");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при генерации тестовых данных", ex.Message);
                StatusMessage = $"Ошибка: {ex.Message}";
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
                // Если данных нет, показываем текст ожидания
                if (!_dataReceived)
                {
                    return;
                }

                // Обновляем данные для каждой серии
                foreach (var seriesName in _seriesColors.Keys)
                {
                    if (_plotSeries.TryGetValue(seriesName, out var seriesPlot) &&
                        _dataPoints.TryGetValue(seriesName, out var points) &&
                        points.Count > 0)
                    {
                        // Преобразуем точки в массивы x и y
                        double[] xData = points.Select(p => p.X).ToArray();
                        double[] yData = points.Select(p => p.Y).ToArray();

                        // Обновляем данные серии
                        seriesPlot.Update(xData, yData);
                    }
                }

                // Обновляем видимость серий в соответствии с настройками
                UpdateVisibleSeries();

                // Обновляем линии порогов защит
                UpdateThresholdLines();

                // Обновляем окно просмотра, если включен автоскролл
                if (_autoScroll)
                {
                    UpdateTimeWindow();
                }

                // Обновляем график
                RefreshPlot();
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
                    engineSpeedSeries.IsVisible = _showEngineSpeed && _dataReceived;

                if (_plotSeries.TryGetValue("Обороты турбокомпрессора", out var turboSpeedSeries))
                    turboSpeedSeries.IsVisible = _showTurboSpeed && _dataReceived;

                if (_plotSeries.TryGetValue("Давление масла", out var oilPressureSeries))
                    oilPressureSeries.IsVisible = _showOilPressure && _dataReceived;

                if (_plotSeries.TryGetValue("Давление наддува", out var boostPressureSeries))
                    boostPressureSeries.IsVisible = _showBoostPressure && _dataReceived;

                if (_plotSeries.TryGetValue("Температура масла", out var oilTemperatureSeries))
                    oilTemperatureSeries.IsVisible = _showOilTemperature && _dataReceived;

                // Обновляем легенду только если есть данные
                if (_dataReceived)
                {
                    _mainPlot.Plot.Legend();
                }

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
            if (_mainPlot == null || !_dataReceived) return;

            try
            {
                // Если включена автопрокрутка и есть данные
                if (_autoScroll)
                {
                    // Если выбран режим "Все", показываем все данные
                    if (_selectedTimeInterval == "Все")
                    {
                        _mainPlot.Plot.AxisAutoX();
                        _mainPlot.Plot.AxisAutoY();
                    }
                    else if (int.TryParse(_selectedTimeInterval, out int seconds))
                    {
                        // Устанавливаем видимый диапазон по оси X
                        double minX = Math.Max(0, _elapsedTime - seconds);
                        double maxX = Math.Max(seconds, _elapsedTime);

                        _mainPlot.Plot.SetAxisLimitsX(minX, maxX);
                        _mainPlot.Plot.AxisAutoY();
                    }
                }

                // Обновляем график
                RefreshPlot();
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
                if (_dataReceived)
                {
                    _mainPlot.Plot.Legend();
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
        /// Очистка графика
        /// </summary>
        private void ClearGraph()
        {
            try
            {
                // Очищаем все коллекции точек
                foreach (var series in _dataPoints.Keys.ToList())
                {
                    _dataPoints[series].Clear();
                }

                // Сбрасываем счетчик времени
                _elapsedTime = 0;

                // Сбрасываем флаг наличия данных
                _dataReceived = false;

                // Очищаем график
                if (_mainPlot != null)
                {
                    _mainPlot.Plot.Clear();

                    // Добавляем текст "Ожидание данных..." на график
                    _mainPlot.Plot.AddText("Ожидание данных...", 0.5, 0.5,
                        size: 24, color: System.Drawing.Color.Gray)
                        .Alignment = ScottPlot.Alignment.MiddleCenter;

                    // Устанавливаем начальные границы осей
                    _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2500);

                    // Переинициализируем серии данных
                    InitializeDataSeries();

                    // Обновляем график
                    RefreshPlot();
                }

                StatusMessage = "График очищен";
                _loggingService.LogInfo("График очищен");
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
        }

        /// <summary>
        /// Экспорт данных в CSV-файл
        /// </summary>
        private void ExportData()
        {
            try
            {
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
        }

        /// <summary>
        /// Автоматическое масштабирование графика
        /// </summary>
        private void AutoScale()
        {
            if (_mainPlot == null) return;

            try
            {
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