using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Regulyators.UI.Models;
using Regulyators.UI.Services;
using static Regulyators.UI.Services.ComPortService;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Сервис для надежной симуляции работы двигателя и системы регуляторов
    /// </summary>
    public class SimulationService : IDisposable
    {
        private static SimulationService _instance;
        private readonly LoggingService _loggingService;
        private readonly ComPortService _comPortService;
        private readonly SettingsService _settingsService;

        private bool _isSimulationRunning;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _simulationTask;
        private readonly object _simulationLock = new object();

        // Параметры симулируемого двигателя
        private double _engineSpeed;
        private double _targetEngineSpeed;
        private double _turboCompressorSpeed;
        private double _oilPressure;
        private double _boostPressure;
        private double _oilTemperature;
        private int _rackPosition;
        private EngineMode _engineMode;
        private LoadType _loadType;
        private int _equipmentPosition;

        // Настройки симуляции
        private bool _randomFailures;
        private bool _stressTest;
        private string _simulationScenario = "Стандартный";

        // Константы для реалистичной симуляции
        private const double ENGINE_INERTIA = 0.5; // Инерция двигателя (скорость изменения)
        private const double TEMP_RISE_RATE = 0.02; // Скорость нагрева масла
        private const double TEMP_COOL_RATE = 0.01; // Скорость охлаждения масла
        private const double TURBO_LAG = 0.7; // Запаздывание турбины
        private const double RANDOM_FACTOR = 0.02; // Фактор случайных колебаний

        // Статус защит
        private bool _isOilPressureProtectionActive;
        private bool _isEngineSpeedProtectionActive;
        private bool _isBoostPressureProtectionActive;
        private bool _isOilTemperatureProtectionActive;
        private bool _allProtectionsEnabled = true;

        // События
        public event EventHandler<bool> SimulationStatusChanged;
        public event EventHandler<EngineParameters> ParametersUpdated;

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static SimulationService Instance => _instance ??= new SimulationService();

        /// <summary>
        /// Активна ли симуляция
        /// </summary>
        public bool IsSimulationRunning
        {
            get => _isSimulationRunning;
            private set
            {
                if (_isSimulationRunning != value)
                {
                    _isSimulationRunning = value;
                    SimulationStatusChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Включены ли случайные сбои
        /// </summary>
        public bool RandomFailures
        {
            get => _randomFailures;
            set => _randomFailures = value;
        }

        /// <summary>
        /// Включен ли режим стресс-теста
        /// </summary>
        public bool StressTest
        {
            get => _stressTest;
            set => _stressTest = value;
        }

        /// <summary>
        /// Текущий сценарий симуляции
        /// </summary>
        public string SimulationScenario
        {
            get => _simulationScenario;
            set
            {
                if (_simulationScenario != value)
                {
                    _simulationScenario = value;
                    ApplyScenario(value);
                }
            }
        }

        private SimulationService()
        {
            _loggingService = LoggingService.Instance;
            _comPortService = ComPortService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация начальных значений
            ResetParameters();

            // Подписываемся на события от ComPortService
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;

            // Перехватываем команды для симуляции
            _comPortService.CommandReceived += OnCommandReceived;
        }

        /// <summary>
        /// Сброс параметров симуляции
        /// </summary>
        private void ResetParameters()
        {
            _engineSpeed = 0;
            _targetEngineSpeed = 0;
            _turboCompressorSpeed = 0;
            _oilPressure = 6;
            _boostPressure = 0;
            _oilTemperature = 25; // Начальная температура масла
            _rackPosition = 0;
            _engineMode = EngineMode.Stop;
            _loadType = LoadType.Idle;
            _equipmentPosition = 0;

            _isOilPressureProtectionActive = false;
            _isEngineSpeedProtectionActive = false;
            _isBoostPressureProtectionActive = false;
            _isOilTemperatureProtectionActive = false;
            _allProtectionsEnabled = true;
        }

        /// <summary>
        /// Запуск симуляции
        /// </summary>
        public void StartSimulation()
        {
            lock (_simulationLock)
            {
                if (_isSimulationRunning)
                    return;

                _loggingService.LogInfo("Запуск режима симуляции");

                // Сброс параметров симуляции
                ResetParameters();

                // Включаем режим симуляции для ComPortService
                _comPortService.SetSimulationMode(true);

                // Симуляция подключения к COM-порту
                _comPortService.SimulateConnection(true);

                // Запуск задачи симуляции
                _cancellationTokenSource = new CancellationTokenSource();
                _simulationTask = Task.Run(() => RunSimulation(_cancellationTokenSource.Token));

                IsSimulationRunning = true;
            }
        }

        /// <summary>
        /// Остановка симуляции
        /// </summary>
        public void StopSimulation()
        {
            lock (_simulationLock)
            {
                if (!_isSimulationRunning)
                    return;

                _loggingService.LogInfo("Остановка режима симуляции");

                IsSimulationRunning = false;

                // Отменяем задачу симуляции
                _cancellationTokenSource?.Cancel();

                try
                {
                    // Ждем завершения задачи с таймаутом
                    Task.WaitAny(new[] { _simulationTask }, 1000);
                }
                catch (Exception ex)
                {
                    _loggingService.LogWarning($"Ошибка при завершении задачи симуляции: {ex.Message}");
                }

                _cancellationTokenSource = null;
                _simulationTask = null;

                // Отключаем режим симуляции для ComPortService
                _comPortService.SetSimulationMode(false);

                // Симуляция отключения от COM-порта
                _comPortService.SimulateConnection(false);
            }
        }

        /// <summary>
        /// Переключение режима симуляции
        /// </summary>
        public void ToggleSimulation()
        {
            if (_isSimulationRunning)
                StopSimulation();
            else
                StartSimulation();
        }

        /// <summary>
        /// Имитация критического падения давления масла
        /// </summary>
        public void SimulateOilPressureDrop()
        {
            lock (_simulationLock)
            {
                if (!_isSimulationRunning)
                    return;

                _oilPressure = 0.5; // Критически низкое давление масла
                _loggingService.LogWarning("Симуляция критического падения давления масла", $"Текущее значение: {_oilPressure:F2} кг/см²");
            }
        }

        /// <summary>
        /// Имитация превышения оборотов двигателя
        /// </summary>
        public void SimulateEngineOverspeed()
        {
            lock (_simulationLock)
            {
                if (!_isSimulationRunning)
                    return;

                _engineSpeed = 2500; // Превышение максимальных оборотов
                _loggingService.LogWarning("Симуляция превышения оборотов двигателя", $"Текущее значение: {_engineSpeed:F0} об/мин");
            }
        }

        /// <summary>
        /// Имитация превышения давления наддува
        /// </summary>
        public void SimulateBoostPressureOverload()
        {
            lock (_simulationLock)
            {
                if (!_isSimulationRunning)
                    return;

                _boostPressure = 3.5; // Превышение давления наддува
                _loggingService.LogWarning("Симуляция превышения давления наддува", $"Текущее значение: {_boostPressure:F2} кг/см²");
            }
        }

        /// <summary>
        /// Имитация перегрева масла
        /// </summary>
        public void SimulateOilOverheating()
        {
            lock (_simulationLock)
            {
                if (!_isSimulationRunning)
                    return;

                _oilTemperature = 130; // Перегрев масла
                _loggingService.LogWarning("Симуляция перегрева масла", $"Текущее значение: {_oilTemperature:F1} °C");
            }
        }

        /// <summary>
        /// Основной цикл симуляции
        /// </summary>
        private async Task RunSimulation(CancellationToken cancellationToken)
        {
            Random random = new Random();
            int tickCount = 0;

            _loggingService.LogInfo("Симуляция двигателя запущена");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    tickCount++;

                    // Синхронизируем доступ к параметрам симуляции
                    lock (_simulationLock)
                    {
                        // Симуляция динамики двигателя
                        SimulateEngineParameters(random, tickCount);

                        // Проверка критических параметров
                        CheckProtections();

                        // Отправка симулированных данных подписчикам
                        SendSimulatedData();

                        // Случайные сбои при включенном режиме
                        if (_randomFailures && random.NextDouble() < 0.002) // 0.2% шанс сбоя каждый тик
                        {
                            GenerateRandomFailure(random);
                        }
                    }

                    // Пауза между обновлениями
                    try
                    {
                        // Частота обновления зависит от режима стресс-теста
                        int delay = _stressTest ? 50 : 100;
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка в цикле симуляции: {ex.Message}", ex.StackTrace);
            }

            _loggingService.LogInfo("Симуляция двигателя остановлена");
        }

        /// <summary>
        /// Генерация случайной аварийной ситуации
        /// </summary>
        private void GenerateRandomFailure(Random random)
        {
            int failureType = random.Next(4);

            switch (failureType)
            {
                case 0:
                    SimulateOilPressureDrop();
                    break;
                case 1:
                    SimulateEngineOverspeed();
                    break;
                case 2:
                    SimulateBoostPressureOverload();
                    break;
                case 3:
                    SimulateOilOverheating();
                    break;
            }
        }

        /// <summary>
        /// Симуляция параметров двигателя с учетом физики
        /// </summary>
        private void SimulateEngineParameters(Random random, int tickCount)
        {
            // Добавляем случайные колебания для реалистичности
            double randomFactor = 1.0 + (random.NextDouble() * 2 - 1) * RANDOM_FACTOR;

            // Если двигатель остановлен, не изменяем параметры, кроме плавного снижения
            if (_engineMode == EngineMode.Stop)
            {
                // Плавное снижение оборотов при остановленном двигателе
                if (_engineSpeed > 10)
                {
                    _engineSpeed *= 0.95;
                }
                else
                {
                    _engineSpeed = 0;
                }

                // Снижение температуры масла при остановленном двигателе
                if (_oilTemperature > 25)
                {
                    _oilTemperature -= TEMP_COOL_RATE * (_stressTest ? 2.0 : 1.0);
                }

                // Снижение давления масла
                _oilPressure = Math.Max(0, _oilPressure * 0.95);

                // Снижение давления наддува
                _boostPressure = Math.Max(0, _boostPressure * 0.95);

                // Снижение оборотов турбокомпрессора
                _turboCompressorSpeed = Math.Max(0, _turboCompressorSpeed * 0.95);

                return;
            }

            // В режиме стресс-теста ускоряем изменения
            double stressFactor = _stressTest ? 2.0 : 1.0;

            // Обороты двигателя стремятся к целевым с учетом инерции
            double speedDiff = _targetEngineSpeed - _engineSpeed;
            _engineSpeed += speedDiff * ENGINE_INERTIA * randomFactor * stressFactor;

            // Ограничиваем диапазон оборотов
            _engineSpeed = Math.Max(0, Math.Min(2600, _engineSpeed));

            // Обороты турбокомпрессора зависят от оборотов двигателя с запаздыванием
            double targetTurboSpeed = _engineSpeed * 10 * randomFactor;
            _turboCompressorSpeed += (targetTurboSpeed - _turboCompressorSpeed) * TURBO_LAG * stressFactor;

            // Давление масла зависит от оборотов
            if (_engineSpeed > 0)
            {
                double targetOilPressure = 0.5 + (_engineSpeed / 2400.0) * 3.5 * randomFactor;
                _oilPressure += (targetOilPressure - _oilPressure) * 0.1 * stressFactor;
            }
            else
            {
                _oilPressure = 0;
            }

            // Давление наддува зависит от оборотов и положения рейки
            double rackFactor = _rackPosition / 30.0;
            double targetBoostPressure = (_engineSpeed / 2400.0) * 2.5 * rackFactor * randomFactor;
            _boostPressure += (targetBoostPressure - _boostPressure) * 0.1 * stressFactor;

            // Температура масла растет при работе двигателя и зависит от нагрузки
            double loadFactor = (_loadType == LoadType.Idle) ? 0.5 :
                               (_loadType == LoadType.Loaded) ? 1.0 : 0.8;

            double targetTemperature = 50 + (_engineSpeed / 2400.0) * 40 * loadFactor;

            if (_oilTemperature < targetTemperature)
            {
                _oilTemperature += TEMP_RISE_RATE * loadFactor * randomFactor * stressFactor;
            }
            else if (_oilTemperature > targetTemperature)
            {
                _oilTemperature -= TEMP_COOL_RATE * randomFactor * stressFactor;
            }

            // Применяем выбранный сценарий в каждый тик
            ApplyScenarioTick(tickCount, random);
        }

        /// <summary>
        /// Применение сценария для текущего тика
        /// </summary>
        private void ApplyScenarioTick(int tickCount, Random random)
        {
            switch (_simulationScenario)
            {
                case "Нарастающая нагрузка":
                    // Постепенное увеличение нагрузки каждые 100 тиков
                    if (tickCount % 100 == 0 && _targetEngineSpeed < 2200)
                    {
                        _targetEngineSpeed += 100;
                        _loggingService.LogInfo($"Увеличение целевых оборотов до {_targetEngineSpeed:F0} об/мин");
                    }
                    break;

                case "Циклический тест":
                    // Цикличное изменение нагрузки
                    if (tickCount % 300 == 0)
                    {
                        _targetEngineSpeed = 800 + 600 * Math.Sin(tickCount * 0.01);
                        _loadType = (LoadType)(tickCount % 3);
                        _loggingService.LogInfo($"Циклическое изменение режима - обороты: {_targetEngineSpeed:F0}, нагрузка: {_loadType}");
                    }
                    break;

                case "Аварийные ситуации":
                    // Периодическое создание аварийных ситуаций
                    if (tickCount % 500 == 0 && random.NextDouble() < 0.5)
                    {
                        GenerateRandomFailure(random);
                    }
                    break;
            }
        }

        /// <summary>
        /// Применение сценария симуляции
        /// </summary>
        private void ApplyScenario(string scenario)
        {
            if (!_isSimulationRunning)
                return;

            _loggingService.LogInfo($"Применение сценария симуляции: {scenario}");

            switch (scenario)
            {
                case "Стандартный":
                    _targetEngineSpeed = 800;
                    _loadType = LoadType.Idle;
                    break;

                case "Нарастающая нагрузка":
                    _targetEngineSpeed = 600;
                    _loadType = LoadType.Loaded;
                    break;

                case "Циклический тест":
                    _targetEngineSpeed = 1000;
                    _loadType = LoadType.Idle;
                    break;

                case "Аварийные ситуации":
                    _targetEngineSpeed = 1800;
                    _loadType = LoadType.Loaded;
                    break;
            }
        }

        /// <summary>
        /// Проверка срабатывания защит
        /// </summary>
        private void CheckProtections()
        {
            if (!_allProtectionsEnabled)
            {
                _isOilPressureProtectionActive = false;
                _isEngineSpeedProtectionActive = false;
                _isBoostPressureProtectionActive = false;
                _isOilTemperatureProtectionActive = false;
                return;
            }

            var thresholds = _settingsService.ProtectionThresholds;

            // Проверка защиты по давлению масла
            bool oldOilPressureProtection = _isOilPressureProtectionActive;
            _isOilPressureProtectionActive = (_engineSpeed > 500 && _oilPressure < thresholds.OilPressureMinThreshold);

            // Проверка защиты по оборотам двигателя
            bool oldEngineSpeedProtection = _isEngineSpeedProtectionActive;
            _isEngineSpeedProtectionActive = (_engineSpeed > thresholds.EngineSpeedMaxThreshold);

            // Проверка защиты по давлению наддува
            bool oldBoostPressureProtection = _isBoostPressureProtectionActive;
            _isBoostPressureProtectionActive = (_boostPressure > thresholds.BoostPressureMaxThreshold);

            // Проверка защиты по температуре масла
            bool oldOilTemperatureProtection = _isOilTemperatureProtectionActive;
            _isOilTemperatureProtectionActive = (_oilTemperature > thresholds.OilTemperatureMaxThreshold);

            // Логируем активацию защит
            if (!oldOilPressureProtection && _isOilPressureProtectionActive)
            {
                _loggingService.LogWarning("Сработала защита по давлению масла",
                    $"Текущее значение: {_oilPressure:F2} кг/см², порог: {thresholds.OilPressureMinThreshold:F2} кг/см²");
            }

            if (!oldEngineSpeedProtection && _isEngineSpeedProtectionActive)
            {
                _loggingService.LogWarning("Сработала защита по оборотам двигателя",
                    $"Текущее значение: {_engineSpeed:F0} об/мин, порог: {thresholds.EngineSpeedMaxThreshold:F0} об/мин");
            }

            if (!oldBoostPressureProtection && _isBoostPressureProtectionActive)
            {
                _loggingService.LogWarning("Сработала защита по давлению наддува",
                    $"Текущее значение: {_boostPressure:F2} кг/см², порог: {thresholds.BoostPressureMaxThreshold:F2} кг/см²");
            }

            if (!oldOilTemperatureProtection && _isOilTemperatureProtectionActive)
            {
                _loggingService.LogWarning("Сработала защита по температуре масла",
                    $"Текущее значение: {_oilTemperature:F1} °C, порог: {thresholds.OilTemperatureMaxThreshold:F1} °C");
            }

            // Если любая защита сработала, останавливаем двигатель
            if ((_isOilPressureProtectionActive || _isEngineSpeedProtectionActive ||
                _isBoostPressureProtectionActive || _isOilTemperatureProtectionActive) &&
                _engineMode == EngineMode.Run)
            {
                _loggingService.LogWarning("Автоматическая остановка двигателя из-за срабатывания защиты",
                    GetActiveProtectionsDescription());
                _engineMode = EngineMode.Stop;
            }
        }

        /// <summary>
        /// Получение описания активных защит
        /// </summary>
        private string GetActiveProtectionsDescription()
        {
            List<string> activeProtections = new List<string>();

            if (_isOilPressureProtectionActive)
                activeProtections.Add("низкое давление масла");

            if (_isEngineSpeedProtectionActive)
                activeProtections.Add("превышение оборотов двигателя");

            if (_isBoostPressureProtectionActive)
                activeProtections.Add("превышение давления наддува");

            if (_isOilTemperatureProtectionActive)
                activeProtections.Add("перегрев масла");

            return string.Join(", ", activeProtections);
        }

        /// <summary>
        /// Отправка симулированных данных подписчикам
        /// </summary>
        private void SendSimulatedData()
        {
            try
            {
                // Создаем объект с параметрами двигателя
                var parameters = new EngineParameters
                {
                    EngineSpeed = _engineSpeed,
                    TurboCompressorSpeed = _turboCompressorSpeed,
                    OilPressure = _oilPressure,
                    BoostPressure = _boostPressure,
                    OilTemperature = _oilTemperature,
                    RackPosition = _rackPosition,
                    Timestamp = DateTime.Now
                };

                // Создаем объект статуса защит
                var protectionStatus = new ProtectionStatus
                {
                    IsOilPressureActive = _isOilPressureProtectionActive,
                    IsEngineSpeedActive = _isEngineSpeedProtectionActive,
                    IsBoostPressureActive = _isBoostPressureProtectionActive,
                    IsOilTemperatureActive = _isOilTemperatureProtectionActive,
                    AllProtectionsEnabled = _allProtectionsEnabled
                };

                // Отправляем событие обновления параметров
                ParametersUpdated?.Invoke(this, parameters);

                // Отправляем симулированные данные через ComPortService
                _comPortService.SimulateDataReceived(parameters);
                _comPortService.SimulateProtectionStatusUpdated(protectionStatus);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка отправки симулированных данных: {ex.Message}", ex.StackTrace);
            }
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            // Если соединение разорвано и симуляция работает, останавливаем её
            if (!isConnected && _isSimulationRunning)
            {
                StopSimulation();
            }
        }

        /// <summary>
        /// Обработчик полученных команд
        /// </summary>
        private void OnCommandReceived(object sender, ERCHM30TZCommand command)
        {
            try
            {
                // Проверяем, что симуляция активна
                if (!_isSimulationRunning)
                    return;

                // Обрабатываем команды в зависимости от их типа
                switch (command.CommandType)
                {
                    case CommandType.GetParameters:
                        // Запрос параметров - ничего не делаем, данные будут отправлены автоматически
                        break;

                    case CommandType.SetEngineSpeed:
                        _targetEngineSpeed = command.EngineSpeed;
                        _loggingService.LogInfo($"Симуляция: установка целевых оборотов {_targetEngineSpeed:F0} об/мин");
                        break;

                    case CommandType.SetRackPosition:
                        _rackPosition = (int)command.RackPosition;
                        _loggingService.LogInfo($"Симуляция: установка положения рейки {_rackPosition}");
                        break;

                    case CommandType.SetEngineMode:
                        _engineMode = command.EngineMode;
                        _loggingService.LogInfo($"Симуляция: установка режима двигателя {_engineMode}");
                        break;

                    case CommandType.SetLoadType:
                        _loadType = command.LoadType;
                        _loggingService.LogInfo($"Симуляция: установка типа нагрузки {_loadType}");
                        break;

                    case CommandType.SetEquipmentPosition:
                        _equipmentPosition = command.EquipmentPosition;
                        _loggingService.LogInfo($"Симуляция: установка позиции оборудования {_equipmentPosition}");
                        break;

                    case CommandType.GetProtectionStatus:
                        // Запрос статуса защит - ничего не делаем, данные будут отправлены автоматически
                        break;

                    case CommandType.SetProtectionThresholds:
                        // Установка порогов защит - обновляем в сервисе настроек
                        if (command.Thresholds != null)
                        {
                            _settingsService.UpdateProtectionThresholds(command.Thresholds);
                            _loggingService.LogInfo("Симуляция: обновлены пороги защит");
                        }
                        break;

                    case CommandType.ResetProtection:
                        // Сброс защит
                        _isOilPressureProtectionActive = false;
                        _isEngineSpeedProtectionActive = false;
                        _isBoostPressureProtectionActive = false;
                        _isOilTemperatureProtectionActive = false;
                        _loggingService.LogInfo("Симуляция: сброс защит выполнен");
                        break;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка обработки команды в симуляторе: {ex.Message}", ex.StackTrace);
            }
        }

        /// <summary>
        /// Включение/выключение всех защит
        /// </summary>
        public void SetAllProtectionsEnabled(bool enabled)
        {
            _allProtectionsEnabled = enabled;
            _loggingService.LogInfo($"Симуляция: защиты {(enabled ? "включены" : "отключены")}");
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Dispose()
        {
            StopSimulation();

            // Отписываемся от событий
            _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _comPortService.CommandReceived -= OnCommandReceived;
        }
    }
}