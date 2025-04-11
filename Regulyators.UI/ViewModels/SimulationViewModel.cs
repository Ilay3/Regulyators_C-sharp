using System;
using System.Threading.Tasks;
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
        private bool _isButtonEnabled = true;

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
            set
            {
                if (SetProperty(ref _isRandomFailures, value))
                {
                    if (_simulationService != null)
                    {
                        _simulationService.RandomFailures = value;
                        StatusMessage = value ?
                            "Режим случайных сбоев включен" :
                            "Режим случайных сбоев отключен";
                    }
                }
            }
        }

        /// <summary>
        /// Включить стресс-тест
        /// </summary>
        public bool IsStressTest
        {
            get => _isStressTest;
            set
            {
                if (SetProperty(ref _isStressTest, value))
                {
                    if (_simulationService != null)
                    {
                        _simulationService.StressTest = value;
                        StatusMessage = value ?
                            "Режим стресс-теста включен" :
                            "Режим стресс-теста отключен";
                    }
                }
            }
        }

        /// <summary>
        /// Доступность кнопок управления (блокировка во время переходных процессов)
        /// </summary>
        public bool IsButtonEnabled
        {
            get => _isButtonEnabled;
            set => SetProperty(ref _isButtonEnabled, value);
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

            // Синхронизируем начальные значения с сервисом
            _isRandomFailures = _simulationService.RandomFailures;
            _isStressTest = _simulationService.StressTest;
            _selectedScenario = _simulationService.SimulationScenario;

            // Инициализация команд
            ToggleSimulationCommand = new RelayCommand(ToggleSimulation, () => IsButtonEnabled);
            SimulateFaultCommand = new RelayCommand<string>(SimulateFault, _ => IsSimulationRunning && IsButtonEnabled);
            ResetSimulationCommand = new RelayCommand(ResetSimulation, () => IsButtonEnabled);

            // Подписка на события
            _simulationService.SimulationStatusChanged += OnSimulationStatusChanged;
        }

        /// <summary>
        /// Запуск/остановка симуляции
        /// </summary>
        private async void ToggleSimulation()
        {
            try
            {
                // Блокируем кнопки на время операции
                IsButtonEnabled = false;

                if (_isSimulationRunning)
                {
                    StatusMessage = "Останавливаем симуляцию...";

                    // Сохраняем текущие настройки перед остановкой
                    SaveCurrentSettings();

                    _simulationService.StopSimulation();
                    StatusMessage = "Симуляция остановлена";
                }
                else
                {
                    StatusMessage = "Запускаем симуляцию...";

                    // Применяем настройки к сервису
                    ApplySettingsToService();

                    // Запускаем симуляцию
                    _simulationService.StartSimulation();
                    StatusMessage = "Симуляция запущена";
                }

                // Небольшая задержка для стабилизации
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка при {(_isSimulationRunning ? "остановке" : "запуске")} симуляции: {ex.Message}", ex.StackTrace);
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                // Разблокируем кнопки
                IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// Сохраняет текущие настройки ViewModel в SimulationService
        /// </summary>
        private void SaveCurrentSettings()
        {
            if (_simulationService != null)
            {
                _simulationService.RandomFailures = _isRandomFailures;
                _simulationService.StressTest = _isStressTest;
                _simulationService.SimulationScenario = _selectedScenario;
            }
        }

        /// <summary>
        /// Применяет настройки к сервису
        /// </summary>
        private void ApplySettingsToService()
        {
            if (_simulationService != null)
            {
                _simulationService.RandomFailures = _isRandomFailures;
                _simulationService.StressTest = _isStressTest;
                _simulationService.SimulationScenario = _selectedScenario;
            }
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

                    // Временно отключаем ComPort
                    _comPortService.SimulateConnection(false);

                    // Через 3 секунды восстанавливаем, если симуляция еще активна
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        if (_simulationService.IsSimulationRunning)
                        {
                            _loggingService.LogInfo("Восстановление соединения после имитации потери связи");
                            _comPortService.SimulateConnection(true);
                            StatusMessage = "Соединение восстановлено";
                        }
                    });
                    break;
            }
        }

        /// <summary>
        /// Сброс симуляции
        /// </summary>
        private async void ResetSimulation()
        {
            try
            {
                // Блокируем кнопки на время операции
                IsButtonEnabled = false;
                StatusMessage = "Сброс симуляции...";

                // Сохраняем текущие настройки перед сбросом
                SaveCurrentSettings();

                // Если симуляция активна, останавливаем ее
                if (_isSimulationRunning)
                {
                    _simulationService.StopSimulation();
                }

                // Небольшая пауза перед перезапуском
                await Task.Delay(1000);

                // Применяем настройки
                ApplySettingsToService();

                // Запускаем симуляцию
                _simulationService.StartSimulation();

                StatusMessage = "Симуляция перезапущена с новыми параметрами";
                _loggingService.LogInfo("Симуляция перезапущена с новыми параметрами");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка при сбросе симуляции: {ex.Message}", ex.StackTrace);
                StatusMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                // Разблокируем кнопки
                IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// Применение выбранного сценария симуляции
        /// </summary>
        private void ApplySelectedScenario()
        {
            // Сохраняем выбранный сценарий в сервисе даже если симуляция не запущена
            if (_simulationService != null)
            {
                _simulationService.SimulationScenario = _selectedScenario;
            }

            if (!_isSimulationRunning)
            {
                StatusMessage = $"Сценарий '{_selectedScenario}' будет применен при запуске симуляции";
                return;
            }

            StatusMessage = $"Применен сценарий симуляции: {_selectedScenario}";
            _loggingService.LogInfo($"Сменен сценарий симуляции на: {_selectedScenario}");
        }

        /// <summary>
        /// Обработчик события изменения статуса симуляции
        /// </summary>
        private void OnSimulationStatusChanged(object sender, bool isRunning)
        {
            IsSimulationRunning = isRunning;

            // Если симуляция была запущена, убедимся, что настройки синхронизированы
            if (isRunning)
            {
                // Синхронизируем настройки с сервисом
                if (_simulationService != null)
                {
                    _isRandomFailures = _simulationService.RandomFailures;
                    _isStressTest = _simulationService.StressTest;
                    _selectedScenario = _simulationService.SimulationScenario;

                    // Уведомляем UI об изменениях
                    OnPropertyChanged(nameof(IsRandomFailures));
                    OnPropertyChanged(nameof(IsStressTest));
                    OnPropertyChanged(nameof(SelectedScenario));
                }
            }
        }
    }
}