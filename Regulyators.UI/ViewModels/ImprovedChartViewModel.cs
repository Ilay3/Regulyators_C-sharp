using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using ScottPlot;

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

                // Настройка обработчиков событий для отключения автоскролла
                _mainPlot.MouseDown += (sender, e) => {
                    if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
                        AutoScroll = false;
                };

                _mainPlot.MouseWheel += (sender, e) => AutoScroll = false;

                // Создаем начальные пустые серии для графика
                foreach (var series in _seriesColors.Keys)
                {
                    Color color = _seriesColors[series];
                    var drawingColor = System.Drawing.Color.FromArgb(
                        color.A, color.R, color.G, color.B);

                    // Создаем пустую серию
                    var scatter = _mainPlot.Plot.AddScatter(
                        new double[] { 0 },
                        new double[] { 0 },
                        drawingColor,
                        label: series);

                    scatter.LineWidth = 2;
                    scatter.MarkerSize = 0;

                    // Устанавливаем начальную видимость серии
                    bool isVisible = false;
                    switch (series)
                    {
                        case "Обороты двигателя":
                            isVisible = ShowEngineSpeed;
                            break;
                        case "Обороты турбокомпрессора":
                            isVisible = ShowTurboSpeed;
                            break;
                        case "Давление масла":
                            isVisible = ShowOilPressure;
                            break;
                        case "Давление наддува":
                            isVisible = ShowBoostPressure;
                            break;
                        case "Температура масла":
                            isVisible = ShowOilTemperature;
                            break;
                    }
                    scatter.IsVisible = isVisible;
                }

                // Настройка легенды
                _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                // Устанавливаем начальные границы осей
                _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2000);
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

                    // Добавляем данные в коллекции
                    AddDataPoint("Обороты двигателя", parameters.EngineSpeed);
                    AddDataPoint("Обороты турбокомпрессора", parameters.TurboCompressorSpeed);
                    AddDataPoint("Давление масла", parameters.OilPressure);
                    AddDataPoint("Давление наддува", parameters.BoostPressure);
                    AddDataPoint("Температура масла", parameters.OilTemperature);

                    // Обновляем график
                    UpdateGraph();

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
        /// Добавление точки данных на график
        /// </summary>
        private void AddDataPoint(string series, double value)
        {
            if (!_xData.ContainsKey(series) || !_yData.ContainsKey(series))
                return;

            _xData[series].Add(_elapsedTime);
            _yData[series].Add(value);

            // Ограничиваем количество точек
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
            IsConnected = isConnected;
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
        /// Обновление графика
        /// </summary>
        private void UpdateGraph()
        {
            if (!_isGraphInitialized || _mainPlot == null)
                return;

            try
            {
                // Обновляем данные для каждой серии
                var scatterPlots = _mainPlot.Plot.GetPlottables().OfType<ScottPlot.Plottable.ScatterPlot>().ToList();

                foreach (var scatter in scatterPlots)
                {
                    string seriesName = scatter.Label;
                    if (_xData.ContainsKey(seriesName) && _yData.ContainsKey(seriesName) &&
                        _xData[seriesName].Count > 0 && _yData[seriesName].Count > 0)
                    {
                        scatter.Update(_xData[seriesName].ToArray(), _yData[seriesName].ToArray());
                    }

                    // Устанавливаем видимость серии
                    switch (seriesName)
                    {
                        case "Обороты двигателя":
                            scatter.IsVisible = ShowEngineSpeed;
                            break;
                        case "Обороты турбокомпрессора":
                            scatter.IsVisible = ShowTurboSpeed;
                            break;
                        case "Давление масла":
                            scatter.IsVisible = ShowOilPressure;
                            break;
                        case "Давление наддува":
                            scatter.IsVisible = ShowBoostPressure;
                            break;
                        case "Температура масла":
                            scatter.IsVisible = ShowOilTemperature;
                            break;
                    }
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

                // Обновляем график
                if (_isGraphInitialized && _mainPlot != null)
                {
                    // Обновляем серии данных (пустые)
                    UpdateGraph();

                    // Сбрасываем границы осей
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
                // Создаем диалог сохранения файла
                var dialog = new SaveFileDialog
                {
                    DefaultExt = ".png",
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
                    Title = "Сохранение графика как изображения"
                };

                // Если пользователь выбрал файл
                if (dialog.ShowDialog() == true)
                {
                    // Сохраняем график в файл
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
        /// Экспорт данных в Excel (CSV формат)
        /// </summary>
        private void ExportExcel()
        {
            try
            {
                // Проверяем наличие данных
                bool hasData = false;
                foreach (var series in _xData.Keys)
                {
                    if (_xData[series].Count > 0)
                    {
                        hasData = true;
                        break;
                    }
                }

                if (!hasData)
                {
                    StatusMessage = "Нет данных для экспорта";
                    return;
                }

                // Создаем диалог сохранения файла
                var dialog = new SaveFileDialog
                {
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Экспорт данных графика в Excel (CSV)"
                };

                // Если пользователь выбрал файл
                if (dialog.ShowDialog() == true)
                {
                    // Создаем CSV-файл
                    using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
                    {
                        // Записываем заголовки колонок
                        var headers = new List<string> { "Время" };
                        foreach (var series in _seriesColors.Keys)
                        {
                            headers.Add(series);
                        }
                        writer.WriteLine(string.Join(";", headers));

                        // Собираем все уникальные значения времени
                        var allTimes = new HashSet<double>();
                        foreach (var series in _xData.Keys)
                        {
                            foreach (var time in _xData[series])
                            {
                                allTimes.Add(time);
                            }
                        }

                        // Сортируем их
                        var sortedTimes = allTimes.OrderBy(t => t).ToList();

                        // Для каждого времени записываем строку данных
                        foreach (var time in sortedTimes)
                        {
                            var values = new List<string> { time.ToString("F1") };

                            foreach (var series in _seriesColors.Keys)
                            {
                                // Находим индекс времени в соответствующей серии
                                int index = _xData[series].IndexOf(time);
                                if (index >= 0 && index < _yData[series].Count)
                                {
                                    // Добавляем значение, если оно существует
                                    double value = _yData[series][index];

                                    // Форматируем в зависимости от типа данных
                                    string formattedValue;
                                    if (series == "Обороты двигателя" || series == "Обороты турбокомпрессора")
                                        formattedValue = value.ToString("F0");
                                    else if (series == "Температура масла")
                                        formattedValue = value.ToString("F1");
                                    else
                                        formattedValue = value.ToString("F2");

                                    values.Add(formattedValue);
                                }
                                else
                                {
                                    // Если нет данных, записываем пустую ячейку
                                    values.Add("");
                                }
                            }

                            // Записываем строку в файл
                            writer.WriteLine(string.Join(";", values));
                        }
                    }

                    _loggingService.LogInfo("Данные графика экспортированы в CSV (Excel)", dialog.FileName);
                    StatusMessage = "Данные экспортированы в Excel (CSV)";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при экспорте данных в Excel", ex.Message);
                StatusMessage = "Ошибка при экспорте данных в Excel";
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
                // Автоматически настраиваем масштаб графика
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