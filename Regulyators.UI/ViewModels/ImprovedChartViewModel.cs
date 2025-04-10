using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly GraphService _graphService;
        private readonly ComPortService _comPortService;
        private readonly SettingsService _settingsService;

        private EngineParameters _engineParameters;
        private WpfPlot _mainPlot;
        private double _elapsedTime = 0;
        private bool _isGraphInitialized = false;
        private bool _isConnected = false;
        private string _statusMessage;

        // Словарь для хранения серий данных графика
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

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public ImprovedChartViewModel()
        {
            _loggingService = LoggingService.Instance;
            _graphService = GraphService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация серий данных для графика
            _graphService.InitSeries("Обороты двигателя", 1200);  // Увеличиваем размер буфера для хранения больше точек
            _graphService.InitSeries("Обороты турбокомпрессора", 1200);
            _graphService.InitSeries("Давление масла", 1200);
            _graphService.InitSeries("Давление наддува", 1200);
            _graphService.InitSeries("Температура масла", 1200);

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

            // Логирование
            _loggingService.LogInfo("Запущен модуль графиков двигателя");
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
                // Включаем интерактивное управление графиком
                _mainPlot.Configuration.DoubleClickBenchmark = false;
                _mainPlot.Configuration.LeftClickDragPan = true;  // Включаем панорамирование
                _mainPlot.Configuration.RightClickDragZoom = true; // Включаем масштабирование
                _mainPlot.Configuration.ScrollWheelZoom = true;   // Включаем масштабирование колесиком
                _mainPlot.Configuration.LockVerticalAxis = false; // Разблокируем вертикальную ось

                // Настройка внешнего вида
                _mainPlot.Plot.Style(ScottPlot.Style.Seaborn);
                _mainPlot.Plot.Title("Параметры двигателя");
                _mainPlot.Plot.XLabel("Время (сек)");
                _mainPlot.Plot.YLabel("Значение");
                _mainPlot.Plot.XAxis.TickLabelStyle(fontSize: 12);
                _mainPlot.Plot.YAxis.TickLabelStyle(fontSize: 12);
                _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                // Создаем серии данных для графика
                InitializeDataSeries();

                // Обновляем график
                UpdateGraph();

                // Устанавливаем оптимальные границы осей
                AutoScale();
            }
        }

        /// <summary>
        /// Инициализация серий данных для графика
        /// </summary>
        private void InitializeDataSeries()
        {
            if (_mainPlot == null) return;

            // Создаем и настраиваем серии данных
            foreach (var series in _seriesColors)
            {
                string seriesName = series.Key;
                Color color = series.Value;

                // Создаем пустые массивы для данных
                var xData = new double[0];
                var yData = new double[0];

                // Создаем серию на графике
                var seriesPlot = _mainPlot.Plot.AddScatter(
                    xData,
                    yData,
                    System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B),
                    label: seriesName);

                seriesPlot.LineWidth = 2;
                seriesPlot.MarkerSize = 0; // Без маркеров для лучшей производительности

                // Сохраняем серию в словаре
                _plotSeries[seriesName] = seriesPlot;
            }

            // Обновляем легенду
            _mainPlot.Plot.Legend();
            _mainPlot.Refresh();
        }

        /// <summary>
        /// Обработчик события получения данных
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            try
            {
                // Обновляем значения параметров
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
            _isConnected = isConnected;
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
        /// Добавление данных на график
        /// </summary>
        private void AddDataToGraph()
        {
            _elapsedTime += 0.5; // Прирост 0.5 сек

            // Добавляем новые точки в сервис графиков
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
            // Очищаем все серии данных
            _graphService.ClearAllSeries();
            _elapsedTime = 0;

            // Обновляем график
            UpdateGraph();

            // Логирование
            _loggingService.LogInfo("График параметров очищен");
            StatusMessage = "График очищен";
        }

        /// <summary>
        /// Экспорт графика в файл
        /// </summary>
        private void ExportGraph(string format)
        {
            if (_mainPlot != null)
            {
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
            else
            {
                _loggingService.LogWarning("Экспорт графика невозможен", "График не инициализирован");
                StatusMessage = "Экспорт графика невозможен: график не инициализирован";
            }
        }

        /// <summary>
        /// Экспорт данных в CSV-файл
        /// </summary>
        private void ExportData()
        {
            try
            {
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
                    // Здесь используем сервис экспорта
                    var exportService = ExportService.Instance;
                    var data = GetDataForExport();

                    // Создаем CSV-файл вручную, с корректным форматированием
                    using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Записываем заголовки колонок
                        writer.WriteLine("Время;Обороты двигателя;Обороты турбокомпрессора;Давление масла;Давление наддува;Температура масла");

                        // Записываем данные
                        foreach (var row in data)
                        {
                            writer.WriteLine(string.Format(
                                "{0};{1};{2};{3};{4};{5}",
                                row.Time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                                row.EngineSpeed.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                                row.TurboCompressorSpeed.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                                row.OilPressure.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                                row.BoostPressure.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                                row.OilTemperature.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
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
        /// Подготовка данных для экспорта
        /// </summary>
        private List<ChartDataPoint> GetDataForExport()
        {
            var result = new List<ChartDataPoint>();

            // Получаем данные из сервиса графиков
            var engineSpeedSeries = _graphService.GetSeriesData("Обороты двигателя");
            var turboSpeedSeries = _graphService.GetSeriesData("Обороты турбокомпрессора");
            var oilPressureSeries = _graphService.GetSeriesData("Давление масла");
            var boostPressureSeries = _graphService.GetSeriesData("Давление наддува");
            var oilTemperatureSeries = _graphService.GetSeriesData("Температура масла");

            // Определяем максимальное количество точек
            int maxPoints = engineSpeedSeries.Count;

            // Заполняем результат
            for (int i = 0; i < maxPoints; i++)
            {
                result.Add(new ChartDataPoint
                {
                    Time = engineSpeedSeries.Count > i ? engineSpeedSeries[i].X : 0,
                    EngineSpeed = engineSpeedSeries.Count > i ? engineSpeedSeries[i].Y : 0,
                    TurboCompressorSpeed = turboSpeedSeries.Count > i ? turboSpeedSeries[i].Y : 0,
                    OilPressure = oilPressureSeries.Count > i ? oilPressureSeries[i].Y : 0,
                    BoostPressure = boostPressureSeries.Count > i ? boostPressureSeries[i].Y : 0,
                    OilTemperature = oilTemperatureSeries.Count > i ? oilTemperatureSeries[i].Y : 0
                });
            }

            return result;
        }

        /// <summary>
        /// Автоматическое масштабирование графика
        /// </summary>
        private void AutoScale()
        {
            if (_mainPlot == null) return;

            // Автоматически настраиваем масштаб графика
            _mainPlot.Plot.AxisAuto();
            _mainPlot.Refresh();

            StatusMessage = "Автоматическое масштабирование графика выполнено";
        }

        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateGraph()
        {
            if (!_isGraphInitialized || _mainPlot == null) return;

            try
            {
                // Получаем данные для каждой серии
                var seriesNames = new[]
                {
                    "Обороты двигателя",
                    "Обороты турбокомпрессора",
                    "Давление масла",
                    "Давление наддува",
                    "Температура масла"
                };

                // Обновляем данные для каждой серии
                foreach (var seriesName in seriesNames)
                {
                    if (_plotSeries.TryGetValue(seriesName, out var seriesPlot))
                    {
                        // Получаем данные из сервиса
                        var seriesData = _graphService.GetSeriesData(seriesName);

                        if (seriesData.Count > 0)
                        {
                            // Преобразуем данные в массивы для ScottPlot
                            var xData = seriesData.Select(p => p.X).ToArray();
                            var yData = seriesData.Select(p => p.Y).ToArray();

                            // Обновляем данные серии
                            seriesPlot.Update(xData, yData);
                        }
                    }
                }

                // Обновляем видимость серий
                UpdateVisibleSeries();

                // Устанавливаем временное окно
                UpdateTimeWindow();

                // Обновляем линии порогов защит
                UpdateThresholdLines();

                // Обновляем график
                _mainPlot.Refresh();
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
                _mainPlot.Refresh();
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
            if (_mainPlot == null) return;

            try
            {
                // Если выбран режим "Все", показываем все данные
                if (_selectedTimeInterval == "Все")
                {
                    _mainPlot.Plot.AxisAuto();
                    _mainPlot.Refresh();
                    return;
                }

                // Если указан числовой интервал, ограничиваем видимую область
                if (int.TryParse(_selectedTimeInterval, out int seconds))
                {
                    // Устанавливаем видимый диапазон по оси X
                    double minX = Math.Max(0, _elapsedTime - seconds);
                    double maxX = Math.Max(seconds, _elapsedTime);

                    _mainPlot.Plot.SetAxisLimitsX(minX, maxX);

                    // Корректируем границы по оси Y только если включен автомасштаб
                    _mainPlot.Plot.AxisAutoY();

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
            if (_mainPlot == null) return;

            try
            {
                // Очищаем горизонтальные линии на графике
                _mainPlot.Plot.Clear(typeof(HLine));

                // Добавляем линии порогов защит, если соответствующие серии видимы
                if (_showEngineSpeed)
                {
                    var line = _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.EngineSpeedCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0), // Полупрозрачный красный
                        1.5f,
                        LineStyle.Dash);
                    line.Label = "Макс. обороты";
                }

                if (_showOilPressure)
                {
                    var line = _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.OilPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash);
                    line.Label = "Мин. давление масла";
                }

                if (_showBoostPressure)
                {
                    var line = _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.BoostPressureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash);
                    line.Label = "Макс. давление наддува";
                }

                if (_showOilTemperature)
                {
                    var line = _mainPlot.Plot.AddHorizontalLine(
                        EngineParameters.OilTemperatureCriticalThreshold,
                        System.Drawing.Color.FromArgb(128, 255, 0, 0),
                        1.5f,
                        LineStyle.Dash);
                    line.Label = "Макс. температура масла";
                }

                // Обновляем легенду
                _mainPlot.Plot.Legend();
                _mainPlot.Refresh();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обновления линий порогов", ex.Message);
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

                _loggingService?.LogInfo("ImprovedChartViewModel: ресурсы освобождены");
            }
            catch (Exception ex)
            {
                _loggingService?.LogError("Ошибка при очистке ресурсов ImprovedChartViewModel", ex.Message);
            }
        }
    }

    /// <summary>
    /// Класс для хранения данных точки на графике для экспорта
    /// </summary>
    public class ChartDataPoint
    {
        public double Time { get; set; }
        public double EngineSpeed { get; set; }
        public double TurboCompressorSpeed { get; set; }
        public double OilPressure { get; set; }
        public double BoostPressure { get; set; }
        public double OilTemperature { get; set; }
    }
}