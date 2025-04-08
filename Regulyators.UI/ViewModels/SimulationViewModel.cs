using System;
using System.Windows.Input;
using Regulyators.UI.Common;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления режимом симуляции работы с регуляторами
    /// </summary>
    public class SimulationViewModel : ViewModelBase
    {
        private readonly SimulationService _simulationService;
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;

        private bool _isSimulationRunning;
        private string _statusMessage;
        private bool _isRandomFailures;
        private bool _isStressTest;
        private string _selectedScenario;

        /// <summary>
        /// Активна ли симуляция
        /// </summary>
        public bool IsSimulationRunning
        {
            get => _isSimulationRunning;
            set
            {
                if (SetProperty(ref _isSimulationRunning, value))
                {
                    OnPropertyChanged(nameof(SimulationButtonText));
                    OnPropertyChanged(nameof(SimulationStatusText));
                }
            }
        }

        /// <summary>
        /// Текст на кнопке управления симуляцией
        /// </summary>
        public string SimulationButtonText => IsSimulationRunning ? "Остановить симуляцию" : "Запустить симуляцию";

        /// <summary>
        /// Текст статуса симуляции
        /// </summary>
        public string SimulationStatusText => IsSimulationRunning ? "Симуляция активна" : "Симуляция отключена";

        /// <summary>
        /// Статусное сообщение
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Включить случайные сбои
        /// </summary>
        public bool IsRandomFailures
        {
            get => _isRandomFailures;
            set => SetProperty(ref _isRandomFailures, value);
        }

        /// <summary>
        /// Включить стресс-тест
        /// </summary>
        public bool IsStressTest
        {
            get => _isStressTest;
            set => SetProperty(ref _isStressTest, value);
        }

        /// <summary>
        /// Выбранный сценарий симуляции
        /// </summary>
        public string SelectedScenario
        {
            get => _selectedScenario;
            set
            {
                if (SetProperty(ref _selectedScenario, value))
                {
                    ApplySelectedScenario();
                }
            }
        }

        /// <summary>
        /// Команда запуска/остановки симуляции
        /// </summary>
        public ICommand ToggleSimulationCommand { get; }

        /// <summary>
        /// Команда имитации аварийной ситуации
        /// </summary>
        public ICommand SimulateFaultCommand { get; }

        /// <summary>
        /// Команда сброса симуляции
        /// </summary>
        public ICommand ResetSimulationCommand { get; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public SimulationViewModel()
        {
            _simulationService = SimulationService.Instance;
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;

            // Инициализация начальных значений
            _isSimulationRunning = _simulationService.IsSimulationRunning;
            _statusMessage = "Готов к запуску симуляции";
            _isRandomFailures = false;
            _isStressTest = false;
            _selectedScenario = "Стандартный";

            // Инициализация команд
            ToggleSimulationCommand = new RelayCommand(ToggleSimulation);
            SimulateFaultCommand = new RelayCommand<string>(SimulateFault);
            ResetSimulationCommand = new RelayCommand(ResetSimulation);
        }

        /// <summary>
        /// Запуск/остановка симуляции
        /// </summary>
        private void ToggleSimulation()
        {
            if (_isSimulationRunning)
            {
                _simulationService.StopSimulation();
                _comPortService.SetSimulationMode(false);
                StatusMessage = "Симуляция остановлена";
            }
            else
            {
                _comPortService.SetSimulationMode(true);
                _simulationService.StartSimulation();
                StatusMessage = "Симуляция запущена";
            }

            IsSimulationRunning = _simulationService.IsSimulationRunning;
        }

        /// <summary>
        /// Имитация аварийной ситуации
        /// </summary>
        private void SimulateFault(string faultType)
        {
            if (!_isSimulationRunning)
            {
                StatusMessage = "Невозможно имитировать аварию: симуляция не запущена";
                return;
            }

            switch (faultType)
            {
                case "OilPressure":
                    StatusMessage = "Имитация падения давления масла";
                    _loggingService.LogWarning("Симуляция критического падения давления масла", "Ручная активация");
                    _simulationService.SimulateOilPressureDrop();
                    break;

                case "EngineSpeed":
                    StatusMessage = "Имитация превышения оборотов двигателя";
                    _loggingService.LogWarning("Симуляция превышения оборотов двигателя", "Ручная активация");
                    _simulationService.SimulateEngineOverspeed();
                    break;

                case "BoostPressure":
                    StatusMessage = "Имитация превышения давления наддува";
                    _loggingService.LogWarning("Симуляция превышения давления наддува", "Ручная активация");
                    _simulationService.SimulateBoostPressureOverload();
                    break;

                case "OilTemperature":
                    StatusMessage = "Имитация перегрева масла";
                    _loggingService.LogWarning("Симуляция перегрева масла", "Ручная активация");
                    _simulationService.SimulateOilOverheating();
                    break;

                case "ConnectionLoss":
                    StatusMessage = "Имитация потери связи";
                    _loggingService.LogWarning("Симуляция потери связи", "Ручная активация");
                    _simulationService.StopSimulation();
                    IsSimulationRunning = false;
                    break;
            }
        }

        /// <summary>
        /// Сброс симуляции
        /// </summary>
        private void ResetSimulation()
        {
            if (_isSimulationRunning)
            {
                _simulationService.StopSimulation();
            }

            // Пауза перед перезапуском
            System.Threading.Thread.Sleep(500);

            _comPortService.SetSimulationMode(true);
            _simulationService.StartSimulation();
            IsSimulationRunning = true;

            StatusMessage = "Симуляция перезапущена";
            _loggingService.LogInfo("Симуляция перезапущена");
        }

        /// <summary>
        /// Применение выбранного сценария симуляции
        /// </summary>
        private void ApplySelectedScenario()
        {
            if (!_isSimulationRunning)
            {
                StatusMessage = "Невозможно применить сценарий: симуляция не запущена";
                return;
            }

            StatusMessage = $"Применен сценарий симуляции: {_selectedScenario}";
            _loggingService.LogInfo($"Смена сценария симуляции на: {_selectedScenario}");

            // Реализация в SimulationService
        }
    }
}