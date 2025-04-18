﻿using System;
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
    /// Работает в режиме синглтона для непрерывного сбора данных в фоновом режиме
    /// </summary>
    public class ImprovedChartViewModel : ViewModelBase
    {
        #region Singleton Implementation

        private static ImprovedChartViewModel _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Получение экземпляра ViewModel (Singleton)
        /// </summary>
        public static ImprovedChartViewModel Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new ImprovedChartViewModel();
                }
            }
        }

        #endregion

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

        // Буфер для входящих данных и таймер
        private readonly object _bufferLock = new object();
        private readonly List<EngineParameters> _incomingDataBuffer = new List<EngineParameters>();
        private DispatcherTimer _uiUpdateTimer; // таймер для перерисовки графика
        private DispatcherTimer _dataBackupTimer; // таймер для периодического сохранения данных
        private DateTime? _startTime = null;
        private bool _dataCollectionStarted = false;

        // Хранилище для периодического резервного копирования данных
        private Dictionary<string, List<double>> _backupXData = new Dictionary<string, List<double>>();
        private Dictionary<string, List<double>> _backupYData = new Dictionary<string, List<double>>();
        private DateTime _lastBackupTime = DateTime.MinValue;

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
        /// Приватный конструктор для реализации синглтона
        /// </summary>
        public ImprovedChartViewModel()
        {
            _loggingService = LoggingService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;
            _simulationService = SimulationService.Instance;

            _loggingService.LogInfo("ImprovedChartViewModel: Инициализация синглтона");

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

            // Инициализируем словари резервного копирования
            foreach (var series in _seriesColors.Keys)
            {
                _backupXData[series] = new List<double>();
                _backupYData[series] = new List<double>();
            }

            // Запуск сбора данных
            StartDataCollection();
        }

        /// <summary>
        /// Запуск сбора данных в фоновом режиме
        /// </summary>
        private void StartDataCollection()
        {
            // Проверяем, не запущен ли уже сбор данных
            if (_dataCollectionStarted)
                return;

            _loggingService.LogInfo("ImprovedChartViewModel: Запуск сбора данных в фоновом режиме");

            // Инициализируем DispatcherTimer для периодического обновления графика
            _uiUpdateTimer = new DispatcherTimer();
            _uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(500); // 500мс для обновления
            _uiUpdateTimer.Tick += OnUiUpdateTimerTick;
            _uiUpdateTimer.Start();

            // Инициализируем DispatcherTimer для периодического резервного копирования данных
            _dataBackupTimer = new DispatcherTimer();
            _dataBackupTimer.Interval = TimeSpan.FromMinutes(30); // Резервное копирование раз в 30 минут
            _dataBackupTimer.Tick += OnDataBackupTimerTick;
            _dataBackupTimer.Start();

            _dataCollectionStarted = true;
        }

        /// <summary>
        /// Обработчик таймера резервного копирования данных
        /// </summary>
        private void OnDataBackupTimerTick(object sender, EventArgs e)
        {
            try
            {
                BackupChartData();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при резервном копировании данных", ex.Message);
            }
        }

        /// <summary>
        /// Создает резервную копию данных графика
        /// </summary>
        private void BackupChartData()
        {
            lock (_bufferLock)
            {
                _loggingService.LogInfo("ImprovedChartViewModel: Создание резервной копии данных графика");

                // Очищаем предыдущие резервные копии
                foreach (var series in _seriesColors.Keys)
                {
                    _backupXData[series].Clear();
                    _backupYData[series].Clear();

                    // Копируем текущие данные
                    if (_xData.ContainsKey(series) && _yData.ContainsKey(series))
                    {
                        _backupXData[series].AddRange(_xData[series]);
                        _backupYData[series].AddRange(_yData[series]);
                    }
                }

                _lastBackupTime = DateTime.Now;
                _loggingService.LogInfo($"ImprovedChartViewModel: Резервная копия создана, точек в истории: {(_xData.Count > 0 ? _xData.First().Value.Count : 0)}");
            }
        }

        /// <summary>
        /// Восстанавливает данные из резервной копии
        /// </summary>
        private void RestoreFromBackup()
        {
            lock (_bufferLock)
            {
                if (_lastBackupTime == DateTime.MinValue)
                {
                    _loggingService.LogWarning("ImprovedChartViewModel: Нет доступных резервных копий для восстановления");
                    return;
                }

                _loggingService.LogInfo($"ImprovedChartViewModel: Восстановление данных из резервной копии от {_lastBackupTime}");

                // Восстанавливаем данные из резервной копии
                foreach (var series in _seriesColors.Keys)
                {
                    if (_backupXData.ContainsKey(series) && _backupYData.ContainsKey(series))
                    {
                        _xData[series].Clear();
                        _yData[series].Clear();

                        _xData[series].AddRange(_backupXData[series]);
                        _yData[series].AddRange(_backupYData[series]);
                    }
                }

                _loggingService.LogInfo($"ImprovedChartViewModel: Резервная копия создана, точек в истории: {(_xData.Count > 0 ? _xData.First().Value.Count : 0)}"
);
            }
        }

        /// <summary>
        /// Инициализация графика при отображении представления
        /// </summary>
        public void InitializeGraph(WpfPlot plot)
        {
            if (plot == null)
            {
                _loggingService.LogError("ImprovedChartViewModel: Невозможно инициализировать график: plot = null");
                return;
            }

            _loggingService.LogInfo("ImprovedChartViewModel: Начало инициализации графика");

            // Проверяем, не инициализирован ли уже график
            if (_isGraphInitialized && _mainPlot != null)
            {
                // Если инициализирован с другим объектом WpfPlot, корректно закрываем предыдущий
                if (_mainPlot != plot)
                {
                    // Очищаем старые ссылки для GC
                    _mainPlot = null;
                    _isGraphInitialized = false;
                    _seriesPlots.Clear();
                }
                else
                {
                    // Если тот же самый объект WpfPlot, просто обновляем график
                    UpdateGraph();
                    return;
                }
            }

            _mainPlot = plot;

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

                // Включаем очередь рендеринга для лучшей производительности
                _mainPlot.Configuration.UseRenderQueue = true;

                // Настройка обработчиков для отключения автоскролла при ручном масштабировании/прокрутке
                _mainPlot.MouseDown += (sender, e) =>
                {
                    if (e.ChangedButton == System.Windows.Input.MouseButton.Left || e.ChangedButton == System.Windows.Input.MouseButton.Right)
                        AutoScroll = false;
                };
                _mainPlot.MouseWheel += (sender, e) => AutoScroll = false;

                // Создаем начальные серии для графика
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

                _mainPlot.Plot.Legend(location: Alignment.UpperRight);

                // Устанавливаем начальные границы осей
                _mainPlot.Plot.SetAxisLimits(0, 30, 0, 2000);

                // Отмечаем график как инициализированный
                _isGraphInitialized = true;

                // Применяем накопленные данные при первой инициализации
                UpdateGraph();

                _loggingService.LogInfo("ImprovedChartViewModel: График успешно инициализирован");
                StatusMessage = "График инициализирован и готов к работе";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при инициализации графика", ex.Message);
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
                    _loggingService.LogWarning("ImprovedChartViewModel: Получены нулевые данные", "OnDataReceived");
                    return;
                }

                // При первом получении данных обновляем UI
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
                });

                lock (_bufferLock)
                {
                    _incomingDataBuffer.Add(parameters);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка обработки полученных данных", ex.Message);
            }
        }

        /// <summary>
        /// Метод таймера, который периодически обновляет график
        /// </summary>
        private void OnUiUpdateTimerTick(object sender, EventArgs e)
        {
            ProcessBufferedData();
        }

        /// <summary>
        /// Обработка накопленных в буфере данных
        /// </summary>
        private void ProcessBufferedData()
        {
            //Извлекаем накопленные данные из буфера
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

            // Добавляем точки в наши коллекции
            foreach (var p in newData)
            {
                if (_startTime == null)
                    _startTime = p.Timestamp;

                double x = (p.Timestamp - _startTime.Value).TotalSeconds;
                _elapsedTime = x; // Обновляем текущее время для автосвкролла

                AddDataPoint("Обороты двигателя", x, p.EngineSpeed);
                AddDataPoint("Обороты турбокомпрессора", x, p.TurboCompressorSpeed);
                AddDataPoint("Давление масла", x, p.OilPressure);
                AddDataPoint("Давление наддува", x, p.BoostPressure);
                AddDataPoint("Температура масла", x, p.OilTemperature);
            }

            // Обновляем график только если он инициализирован
            if (_isGraphInitialized && _mainPlot != null)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateGraph();
                    }, DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("ImprovedChartViewModel: Ошибка при обновлении графика в UI потоке", ex.Message);
                }
            }
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

                        _loggingService.LogInfo($"ImprovedChartViewModel: Статус соединения обновлен: {IsConnected}, симуляция: {isRunning}");
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ImprovedChartViewModel: Ошибка обработки события изменения статуса симуляции: {ex.Message}", ex.StackTrace);
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
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ImprovedChartViewModel: Ошибка обработки симулированных данных: {ex.Message}", ex.StackTrace);
            }
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

                        _loggingService.LogInfo($"ImprovedChartViewModel: Статус соединения изменен: {IsConnected}, реальное: {isConnected}, симуляция: {_simulationService.IsSimulationRunning}");
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"ImprovedChartViewModel: Ошибка обработки изменения статуса подключения: {ex.Message}", ex.StackTrace);
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
                        if (_isGraphInitialized && _mainPlot != null)
                        {
                            UpdateThresholdLines();
                        }
                    });
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("ImprovedChartViewModel: Ошибка обработки изменения настроек", ex.Message);
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
                _loggingService.LogError("ImprovedChartViewModel: Ошибка обновления графика", ex.Message);
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
                _loggingService.LogError("ImprovedChartViewModel: Ошибка обновления линий порогов", ex.Message);
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
                _loggingService.LogError("ImprovedChartViewModel: Ошибка обновления временного окна", ex.Message);
            }
        }

        /// <summary>
        /// Очистка графика
        /// </summary>
        private void ClearGraph()
        {
            try
            {
                _loggingService.LogInfo("ImprovedChartViewModel: Начало очистки графика");

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
                _loggingService.LogInfo("ImprovedChartViewModel: График успешно очищен");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при очистке графика", ex.Message);
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
                    _loggingService.LogInfo("ImprovedChartViewModel: График сохранен в файл", dialog.FileName);
                    StatusMessage = "График сохранен как изображение";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при экспорте графика", ex.Message);
                StatusMessage = "Ошибка при экспорте графика как изображения";
            }
        }

        /// <summary>
        /// Экспортирует текущий вид (изображение) графика в Excel-файл.
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

                    // Сохраняем график во ВРЕМЕННЫЙ PNG-файл
                    string tempFile = System.IO.Path.GetTempFileName() + ".png";
                    _mainPlot.Plot.SaveFig(tempFile);

                    // Загружаем файл в MemoryStream
                    byte[] fileBytes = File.ReadAllBytes(tempFile);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        // 3Добавляем картинку из Stream
                        var pic = worksheet.AddPicture(ms, "ChartImage")
                                           .MoveTo(worksheet.Cell(1, 1))
                                           .WithSize(800, 600); 
                    }

                    // Удаляем временный файл
                    File.Delete(tempFile);

                    // Сохраняем Excel
                    workbook.SaveAs(dialog.FileName);
                }

                StatusMessage = "График экспортирован в Excel как изображение";
                _loggingService.LogInfo("ImprovedChartViewModel: График успешно встроен в Excel", dialog.FileName);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при экспорте графика в Excel", ex.Message);
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
                _loggingService.LogError("ImprovedChartViewModel: Ошибка масштабирования графика", ex.Message);
                StatusMessage = "Ошибка масштабирования графика";
            }
        }

        /// <summary>
        /// Метод, который можно вызвать при выгрузке View для очистки GUI-ресурсов
        /// Сервис продолжит собирать данные в фоновом режиме
        /// </summary>
        public void ReleaseViewResources()
        {
            try
            {
                _loggingService.LogInfo("ImprovedChartViewModel: Освобождение только ресурсов представления (без удаления данных)");
                // Только помечаем что график не инициализирован и удаляем ссылки на UI компоненты
                _isGraphInitialized = false;
                _seriesPlots.Clear();
                _mainPlot = null;
                // НЕ очищаем данные (_xData, _yData), чтобы сохранить историю
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при освобождении ресурсов представления", ex.Message);
            }
        }

        /// <summary>
        /// Освобождение ресурсов и остановка работы (вызывается при закрытии приложения)
        /// </summary>
        public void ShutDown()
        {
            try
            {
                _loggingService.LogInfo("ImprovedChartViewModel: Остановка фонового сервиса");

                // Создаем последнюю резервную копию перед выключением
                BackupChartData();

                // Отписываемся от всех событий
                _comPortService.DataReceived -= OnDataReceived;
                _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _settingsService.SettingsChanged -= OnSettingsChanged;
                _simulationService.SimulationStatusChanged -= OnSimulationStatusChanged;
                _simulationService.ParametersUpdated -= OnSimulationParametersUpdated;

                // Останавливаем таймеры
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer = null;

                _dataBackupTimer?.Stop();
                _dataBackupTimer = null;

                // Очищаем данные
                foreach (var series in _xData.Keys.ToList())
                {
                    _xData[series].Clear();
                    _yData[series].Clear();
                }

                foreach (var series in _backupXData.Keys.ToList())
                {
                    _backupXData[series].Clear();
                    _backupYData[series].Clear();
                }

                // Очищаем буфер
                lock (_bufferLock)
                {
                    _incomingDataBuffer.Clear();
                }

                // Очищаем ссылки
                _seriesPlots.Clear();
                _mainPlot = null;
                _isGraphInitialized = false;
                _dataCollectionStarted = false;

                // Очищаем синглтон для GC
                _instance = null;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("ImprovedChartViewModel: Ошибка при остановке фонового сервиса", ex.Message);
            }
        }

        /// <summary>
        /// Освобождение ресурсов при выгрузке
        /// </summary>
        protected override void ReleaseMangedResources()
        {
            base.ReleaseMangedResources();

            // Просто освобождаем ресурсы UI для предотвращения утечек
            ReleaseViewResources();
        }
    }
}