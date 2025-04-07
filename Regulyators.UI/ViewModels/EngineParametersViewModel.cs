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
        private readonly Timer _updateTimer;
        private readonly Random _random;
        private readonly GraphService _graphService;
        private readonly LoggingService _loggingService;

        private EngineParameters _engineParameters;
        private WpfPlot _parametersPlot;
        private double _elapsedTime = 0;
        private bool _isGraphInitialized = false;

        // Параметры отображения
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

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public EngineParametersViewModel()
        {
            _random = new Random();
            _graphService = GraphService.Instance;
            _loggingService = LoggingService.Instance;

            // Инициализация серий данных для графика
            _graphService.InitSeries("Обороты двигателя", 600);
            _graphService.InitSeries("Обороты турбокомпрессора", 600);
            _graphService.InitSeries("Давление масла", 600);
            _graphService.InitSeries("Давление наддува", 600);
            _graphService.InitSeries("Температура масла", 600);

            // Инициализация параметров двигателя
            EngineParameters = new EngineParameters
            {
                EngineSpeed = 800,
                TurboCompressorSpeed = 5000,
                OilPressure = 2.5,
                BoostPressure = 1.2,
                OilTemperature = 75,
                RackPosition = 10,
                Timestamp = DateTime.Now
            };

            // Инициализация команд
            ClearGraphCommand = new RelayCommand(ClearGraph);
            ExportGraphCommand = new RelayCommand(ExportGraph);

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

            // Запуск таймера обновления данных
            _updateTimer = new Timer(UpdateParameters, null, 500, 500);

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
        /// Метод обновления параметров (демо-режим)
        /// </summary>
        private void UpdateParameters(object state)
        {
            // Генерация случайных данных для демонстрации
            double engineSpeed = 800 + _random.Next(0, 1600);
            double turboSpeed = 5000 + _random.Next(0, 15000);
            double oilPressure = 1.2 + _random.NextDouble() * 1.8;
            double boostPressure = 0.8 + _random.NextDouble() * 2.0;
            double oilTemperature = 70 + _random.Next(0, 50);
            int rackPosition = _random.Next(0, 30);

            // Иногда генерируем критические значения
            if (_random.Next(100) < 5)
            {
                if (_random.Next(4) == 0)
                    oilPressure = 0.8 + _random.NextDouble() * 0.6; // Низкое давление масла
                else if (_random.Next(4) == 1)
                    engineSpeed = 2300 + _random.Next(0, 300); // Высокие обороты
                else if (_random.Next(4) == 2)
                    boostPressure = 2.8 + _random.NextDouble() * 0.8; // Высокое давление наддува
                else
                    oilTemperature = 115 + _random.Next(0, 15); // Высокая температура масла
            }

            // Обновление графика
            _elapsedTime += 0.5; // Прирост 0.5 сек

            _graphService.AddDataPoint("Обороты двигателя", _elapsedTime, engineSpeed);
            _graphService.AddDataPoint("Обороты турбокомпрессора", _elapsedTime, turboSpeed);
            _graphService.AddDataPoint("Давление масла", _elapsedTime, oilPressure);
            _graphService.AddDataPoint("Давление наддува", _elapsedTime, boostPressure);
            _graphService.AddDataPoint("Температура масла", _elapsedTime, oilTemperature);

            // Обновление данных в UI потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                EngineParameters.EngineSpeed = engineSpeed;
                EngineParameters.TurboCompressorSpeed = turboSpeed;
                EngineParameters.OilPressure = oilPressure;
                EngineParameters.BoostPressure = boostPressure;
                EngineParameters.OilTemperature = oilTemperature;
                EngineParameters.RackPosition = rackPosition;
                EngineParameters.Timestamp = DateTime.Now;

                // Обновление графика
                UpdateGraph();
            });
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
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Ошибка при экспорте графика", ex.Message);
                }
            }
            else
            {
                _loggingService.LogWarning("Экспорт графика невозможен", "График не инициализирован");
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