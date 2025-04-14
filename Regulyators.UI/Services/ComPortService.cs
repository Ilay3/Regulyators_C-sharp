using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Расширенная версия сервиса COM-порта с поддержкой симуляции
    /// </summary>
    public class ComPortService : IDisposable
    {
        private static ComPortService _instance;
        private SerialPort _serialPort;
        private bool _isConnected;
        private readonly Queue<ERCHM30TZCommand> _commandQueue;
        private readonly object _lockObj = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;
        private readonly LoggingService _loggingService;
        private readonly Queue<TaskCompletionSource<bool>> _commandCompletionQueue;
        private int _maxRetryAttempts = 3;
        private int _reconnectDelay = 2000; // 2 секунды

        // Поля для симуляции
        private bool _isSimulationMode = false;
        private readonly Random _random = new Random();
        private readonly Timer _simulationWatchdog;
        private bool _simulationConnected = false;

        /// <summary>
        /// Событие получения новых данных с порта
        /// </summary>
        public event EventHandler<EngineParameters> DataReceived;

        /// <summary>
        /// Событие изменения статуса соединения
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// Событие возникновения ошибки
        /// </summary>
        public event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// Событие обновления статуса защит
        /// </summary>
        public event EventHandler<ProtectionStatus> ProtectionStatusUpdated;

        /// <summary>
        /// Событие получения команды для симулятора
        /// </summary>
        public event EventHandler<ERCHM30TZCommand> CommandReceived;

        /// <summary>
        /// Текущие настройки порта
        /// </summary>
        public ComPortSettings Settings { get; private set; }

        // Таймер для проверки состояния соединения
        private readonly Timer _connectionWatchdog;

        /// <summary>
        /// Статус соединения
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    ConnectionStatusChanged?.Invoke(this, value);

                    if (value)
                        _loggingService.LogInfo("Соединение с оборудованием установлено", $"Порт: {Settings.PortName}");
                    else
                        _loggingService.LogWarning("Соединение с оборудованием потеряно", $"Порт: {Settings.PortName}");
                }
            }
        }

        /// <summary>
        /// Активен ли режим симуляции
        /// </summary>
        public bool IsSimulationMode => _isSimulationMode;

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static ComPortService Instance => _instance ??= new ComPortService();

        private ComPortService()
        {
            _commandQueue = new Queue<ERCHM30TZCommand>();
            _commandCompletionQueue = new Queue<TaskCompletionSource<bool>>();
            _loggingService = LoggingService.Instance;
            Settings = new ComPortSettings();
            _isConnected = false;

            _connectionWatchdog = new Timer(ConnectionWatchdogCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Инициализация таймера-сторожа для симуляции
            _simulationWatchdog = new Timer(SimulationWatchdogCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Callback таймера проверки соединения
        /// </summary>
        private void ConnectionWatchdogCallback(object state)
        {
            if (IsConnected && !_isSimulationMode && _serialPort != null)
            {
                if (!_serialPort.IsOpen)
                {
                    // Порт закрылся по внешним причинам
                    _loggingService.LogWarning("Обнаружено неожиданное закрытие порта");
                    IsConnected = false;

                    // Попытка восстановления связи
                    _ = TryReconnectAsync();
                }
            }
        }

        /// <summary>
        /// Включение или выключение режима симуляции
        /// </summary>
        public void SetSimulationMode(bool enabled)
        {
            // Если режим уже установлен, ничего не делаем
            if (_isSimulationMode == enabled)
                return;

            // Отключаемся от реального порта, если были подключены
            if (_isConnected && !_simulationConnected)
            {
                Disconnect();
            }

            _isSimulationMode = enabled;
            _loggingService.LogInfo($"Режим симуляции {(enabled ? "включен" : "выключен")}");

            // Если включаем симуляцию, запускаем сторожевой таймер
            if (enabled)
            {
                _simulationWatchdog.Change(0, 5000); // Проверка каждые 5 секунд
            }
            else
            {
                _simulationWatchdog.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Симуляция состояния подключения
        /// </summary>
        public void SimulateConnection(bool isConnected)
        {
            if (_isSimulationMode)
            {
                // Запоминаем, что это симулированное подключение
                _simulationConnected = isConnected;
                IsConnected = isConnected;

                if (isConnected)
                {
                    _loggingService.LogInfo("Симуляция: соединение с оборудованием установлено");
                }
                else
                {
                    _loggingService.LogInfo("Симуляция: соединение с оборудованием потеряно");
                }
            }
        }

        /// <summary>
        /// Симуляция получения данных от контроллера
        /// </summary>
        public void SimulateDataReceived(EngineParameters parameters)
        {
            if (_isSimulationMode)
            {
                try
                {
                    // Всегда вызываем событие в режиме симуляции
                    DataReceived?.Invoke(this, parameters);
                    _loggingService.LogInfo($"Симуляция: отправлены данные (Обороты={parameters.EngineSpeed}, Давление масла={parameters.OilPressure})");
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Ошибка при обработке симулированных данных: {ex.Message}", ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Симуляция обновления статуса защиты
        /// </summary>
        public void SimulateProtectionStatusUpdated(ProtectionStatus status)
        {
            if (_isSimulationMode && _isConnected)
            {
                try
                {
                    ProtectionStatusUpdated?.Invoke(this, status);
                }
                catch (Exception ex)
                {
                    _loggingService.LogError($"Ошибка при обработке симулированного статуса защит: {ex.Message}", ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Получение списка доступных COM-портов
        /// </summary>
        public string[] GetAvailablePorts()
        {
            try
            {
                return SerialPort.GetPortNames();
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Ошибка при получении списка COM-портов: {ex.Message}", ex.StackTrace);
                return new string[0];
            }
        }

        /// <summary>
        /// Установка настроек COM-порта
        /// </summary>
        public void UpdateSettings(ComPortSettings settings)
        {
            if (IsConnected && !_isSimulationMode)
            {
                Disconnect();
            }

            Settings = settings;
            _loggingService.LogInfo("Настройки COM-порта обновлены", $"Порт: {settings.PortName}, Скорость: {settings.BaudRate}");
        }

        /// <summary>
        /// Установка параметров повторного подключения
        /// </summary>
        public void SetReconnectParameters(int maxRetryAttempts, int reconnectDelay)
        {
            _maxRetryAttempts = maxRetryAttempts;
            _reconnectDelay = reconnectDelay;
            _loggingService.LogInfo("Параметры переподключения обновлены",
                $"Макс. попыток: {maxRetryAttempts}, Задержка: {reconnectDelay} мс");
        }

        /// <summary>
        /// Подключение к COM-порту
        /// </summary>
        public bool Connect()
        {
            try
            {
                if (IsConnected)
                {
                    return true;
                }

                if (_isSimulationMode)
                {
                    _loggingService.LogInfo("Соединение установлено (режим симуляции)");
                    _simulationConnected = true;
                    IsConnected = true;
                    return true;
                }

                _loggingService.LogInfo("Попытка подключения к COM-порту", $"Порт: {Settings.PortName}, Скорость: {Settings.BaudRate}");

                _serialPort = new SerialPort
                {
                    PortName = Settings.PortName,
                    BaudRate = Settings.BaudRate,
                    DataBits = Settings.DataBits,
                    StopBits = Settings.StopBits,
                    Parity = Settings.Parity,
                    ReadTimeout = Settings.ReadTimeout,
                    WriteTimeout = Settings.WriteTimeout
                };

                // Проверка и принудительная установка настроек по протоколу
                if (Settings.BaudRate != 9600 || Settings.Parity != Parity.Odd ||
                    Settings.StopBits != StopBits.Two || Settings.DataBits != 8)
                {
                    _loggingService.LogWarning("Корректировка настроек COM-порта в соответствии с протоколом");
                    Settings.BaudRate = 9600;
                    Settings.Parity = Parity.Odd;
                    Settings.StopBits = StopBits.Two;
                    Settings.DataBits = 8;

                    _serialPort.BaudRate = Settings.BaudRate;
                    _serialPort.Parity = Settings.Parity;
                    _serialPort.StopBits = Settings.StopBits;
                    _serialPort.DataBits = Settings.DataBits;
                }

                _serialPort.Open();

                // Очищаем буферы порта
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                IsConnected = true;

                // Запускаем задачи обработки данных
                _cancellationTokenSource = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessCommandQueue(_cancellationTokenSource.Token));

                // Запускаем мониторинг порта
                StartMonitoring();

                // Запускаем таймер проверки соединения
                _connectionWatchdog.Change(5000, 5000); // Проверка каждые 5 секунд

                _loggingService.LogInfo("Подключение к COM-порту успешно", $"Порт: {Settings.PortName}");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                string errorMessage = $"Ошибка доступа к порту {Settings.PortName}. Порт может быть занят другим приложением.";
                _loggingService.LogError(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
            catch (ArgumentException ex)
            {
                string errorMessage = $"Ошибка в параметрах порта: {ex.Message}";
                _loggingService.LogError(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка подключения к COM-порту: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Отключение от COM-порта
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _loggingService.LogInfo("Отключение от COM-порта", $"Порт: {Settings.PortName}");

                if (_isSimulationMode)
                {
                    _simulationConnected = false;
                    IsConnected = false;
                    _loggingService.LogInfo("Соединение разорвано (режим симуляции)");
                    return;
                }

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // Останавливаем обработку команд
                    _cancellationTokenSource?.Cancel();

                    if (_processingTask != null)
                    {
                        try
                        {
                            // Даем задаче завершиться с таймаутом
                            if (!_processingTask.Wait(1000))
                            {
                                _loggingService.LogWarning("Превышено время ожидания завершения задачи обработки команд");
                            }
                        }
                        catch (AggregateException)
                        {
                            // Игнорируем отмену
                        }
                    }

                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }

                IsConnected = false;
                _connectionWatchdog.Change(Timeout.Infinite, Timeout.Infinite);

                // Очищаем очередь команд и задач завершения
                lock (_lockObj)
                {
                    _commandQueue.Clear();

                    // Сообщаем всем ожидающим задачам о невозможности выполнения
                    while (_commandCompletionQueue.Count > 0)
                    {
                        var tcs = _commandCompletionQueue.Dequeue();
                        tcs.TrySetResult(false);
                    }
                }

                _loggingService.LogInfo("Отключение от COM-порта выполнено", $"Порт: {Settings.PortName}");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка отключения от COM-порта: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                IsConnected = false;
            }
        }

        /// <summary>
        /// Отправка команды в контроллер
        /// </summary>
        public async Task<bool> SendCommandAsync(ERCHM30TZCommand command)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                // Логируем команду
                LogCommand(command);

                if (_isSimulationMode)
                {
                    // В режиме симуляции вызываем событие получения команды
                    CommandReceived?.Invoke(this, command);

                    // Симулируем успешное выполнение с небольшой задержкой
                    await Task.Delay(100);

                    // Иногда симулируем случайные ошибки
                    bool success = _random.NextDouble() > 0.05; // 5% шанс ошибки

                    // Вызываем успешное завершение задачи
                    tcs.TrySetResult(success);

                    if (!success)
                    {
                        _loggingService.LogWarning("Симуляция: ошибка выполнения команды", $"Команда: {command.CommandType}");
                    }
                }
                else
                {
                    // В обычном режиме добавляем в очередь
                    lock (_lockObj)
                    {
                        _commandQueue.Enqueue(command);
                        _commandCompletionQueue.Enqueue(tcs);
                    }
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка добавления команды в очередь: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                tcs.TrySetResult(false);
                return false;
            }
        }

        /// <summary>
        /// Отправка команды в контроллер (синхронная версия)
        /// </summary>
        public void SendCommand(ERCHM30TZCommand command)
        {
            // Асинхронная версия, но без ожидания результата
            _ = SendCommandAsync(command);
        }

        /// <summary>
        /// Логирование отправляемой команды
        /// </summary>
        private void LogCommand(ERCHM30TZCommand command)
        {
            string details;

            switch (command.CommandType)
            {
                case CommandType.GetParameters:
                    details = "Запрос параметров двигателя";
                    break;

                case CommandType.SetEngineSpeed:
                    details = $"Установка оборотов двигателя: {command.EngineSpeed:F0} об/мин";
                    break;

                case CommandType.SetRackPosition:
                    details = $"Установка положения рейки: {command.RackPosition:F2}";
                    break;

                case CommandType.SetEngineMode:
                    details = $"Установка режима двигателя: {command.EngineMode}";
                    break;

                case CommandType.SetLoadType:
                    details = $"Установка типа нагрузки: {command.LoadType}";
                    break;

                case CommandType.SetEquipmentPosition:
                    details = $"Установка позиции оборудования: {command.EquipmentPosition}";
                    break;

                case CommandType.GetProtectionStatus:
                    details = "Запрос статуса защит";
                    break;

                case CommandType.SetProtectionThresholds:
                    details = "Установка порогов защит";
                    break;

                case CommandType.ResetProtection:
                    details = "Сброс защит";
                    break;

                default:
                    details = "Неизвестная команда";
                    break;
            }

            _loggingService.LogInfo($"Отправка команды: {command.CommandType}", details);
        }

        /// <summary>
        /// Обработка очереди команд
        /// </summary>
        private async Task ProcessCommandQueue(CancellationToken cancellationToken)
        {
            _loggingService.LogInfo("Запуск обработки очереди команд");

            while (!cancellationToken.IsCancellationRequested)
            {
                ERCHM30TZCommand command = null;
                TaskCompletionSource<bool> commandCompletion = null;

                // Извлекаем команду из очереди
                lock (_lockObj)
                {
                    if (_commandQueue.Count > 0 && _commandCompletionQueue.Count > 0)
                    {
                        command = _commandQueue.Dequeue();
                        commandCompletion = _commandCompletionQueue.Dequeue();
                    }
                }

                if (command != null && commandCompletion != null)
                {
                    _loggingService.LogInfo($"Обработка команды из очереди: {command.CommandType}");

                    // Формируем пакет данных для отправки
                    byte[] packet = ComposePacket(command);

                    bool success = false;
                    int retryCount = 0;

                    // Пробуем отправить команду с возможностью повтора при ошибке
                    while (!success && retryCount < 3 && !cancellationToken.IsCancellationRequested)
                    {
                        if (retryCount > 0)
                        {
                            _loggingService.LogInfo($"Повторная отправка команды: {command.CommandType}, попытка {retryCount + 1}/3");
                            await Task.Delay(100, cancellationToken); // Небольшая задержка перед повтором
                        }

                        if (SendData(packet))
                        {
                            _loggingService.LogInfo("Команда отправлена успешно", $"Тип: {command.CommandType}");

                            // Ждем ответа
                            success = await ReadResponseAsync(command);
                            if (success)
                            {
                                _loggingService.LogInfo("Команда выполнена успешно", $"Тип: {command.CommandType}");
                                commandCompletion.TrySetResult(true);
                            }
                            else
                            {
                                _loggingService.LogWarning("Ошибка выполнения команды", $"Тип: {command.CommandType}");
                            }
                        }

                        retryCount++;
                    }

                    // Если все попытки не удались, сообщаем об ошибке
                    if (!success)
                    {
                        _loggingService.LogError($"Не удалось выполнить команду после {retryCount} попыток", $"Тип: {command.CommandType}");
                        commandCompletion.TrySetResult(false);
                    }
                }
                else
                {
                    // Если очередь пуста, делаем небольшую паузу
                    await Task.Delay(100, cancellationToken);
                }
            }

            _loggingService.LogInfo("Завершение обработки очереди команд");
        }

        /// <summary>
        /// Сторожевой таймер для мониторинга симуляции
        /// </summary>
        private void SimulationWatchdogCallback(object state)
        {
            if (_isSimulationMode && _simulationConnected)
            {
                // Иногда (с вероятностью 1%) симулируем случайные потери соединения
                if (_random.NextDouble() < 0.01)
                {
                    // В режиме симуляции иногда случайно "теряем" соединение
                    _loggingService.LogWarning("Симуляция: случайная потеря соединения");

                    // Уведомляем о потере соединения
                    _simulationConnected = false;
                    IsConnected = false;

                    // Автоматическое восстановление через некоторое время
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        if (_isSimulationMode)
                        {
                            _loggingService.LogInfo("Симуляция: автоматическое восстановление соединения");
                            _simulationConnected = true;
                            IsConnected = true;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Запуск мониторинга параметров двигателя
        /// </summary>
        private void StartMonitoring()
        {
            _loggingService.LogInfo("Запуск мониторинга параметров двигателя", $"Интервал: {Settings.PollingInterval} мс");

            // Запускаем периодический опрос параметров
            Task.Run(async () =>
            {
                while (IsConnected && !_isSimulationMode)
                {
                    // Отправляем команду запроса параметров
                    SendCommand(new ERCHM30TZCommand
                    {
                        CommandType = CommandType.GetParameters
                    });

                    // Периодически запрашиваем статус защит
                    if (DateTime.Now.Second % 5 == 0) // Каждые 5 секунд
                    {
                        SendCommand(new ERCHM30TZCommand
                        {
                            CommandType = CommandType.GetProtectionStatus
                        });
                    }

                    // Пауза между опросами
                    await Task.Delay(Settings.PollingInterval);
                }
            });
        }

        #region Методы работы с реальным COM-портом

        /// <summary>
        /// Отправка данных в COM-порт
        /// </summary>
        private bool SendData(byte[] data)
        {
            if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                _loggingService.LogWarning("Попытка отправки данных при отсутствии соединения", "Запуск процедуры переподключения");

                if (TryReconnect())
                {
                    // Если переподключились успешно, пробуем отправить
                    return SendDataInternal(data);
                }
                return false;
            }

            return SendDataInternal(data);
        }

        /// <summary>
        /// Внутренний метод отправки данных
        /// </summary>
        private bool SendDataInternal(byte[] data)
        {
            try
            {
                // Сначала очищаем буфер ввода, чтобы не было мусора от предыдущих операций
                _serialPort.DiscardInBuffer();

                // Отправляем данные
                _serialPort.Write(data, 0, data.Length);

                // Логируем в hex-формате для отладки
                _loggingService.LogInfo("Отправлены данные", BitConverter.ToString(data).Replace("-", " "));

                return true;
            }
            catch (TimeoutException)
            {
                string errorMessage = "Таймаут отправки данных";
                _loggingService.LogWarning(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
            catch (InvalidOperationException)
            {
                string errorMessage = "Порт закрыт или недоступен при попытке отправки данных";
                _loggingService.LogError(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                IsConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка отправки данных: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                IsConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Чтение данных из COM-порта
        /// </summary>
        private byte[] ReadData(int bytesToRead, int timeout = -1)
        {
            if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                _loggingService.LogWarning("Попытка чтения данных при отсутствии соединения");
                return null;
            }

            try
            {
                // Если установлен таймаут, сохраняем старое значение и устанавливаем новое
                int oldTimeout = -1;
                if (timeout > 0)
                {
                    oldTimeout = _serialPort.ReadTimeout;
                    _serialPort.ReadTimeout = timeout;
                }

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                // Восстанавливаем таймаут, если был изменен
                if (oldTimeout > 0)
                {
                    _serialPort.ReadTimeout = oldTimeout;
                }

                if (bytesRead != bytesToRead)
                {
                    // Неполное чтение
                    _loggingService.LogWarning($"Неполное чтение данных: прочитано {bytesRead} из {bytesToRead} байт");
                    byte[] result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }

                // Логируем полученные данные в hex для отладки
                _loggingService.LogInfo("Получены данные", BitConverter.ToString(buffer).Replace("-", " "));

                return buffer;
            }
            catch (TimeoutException)
            {
                string errorMessage = "Таймаут чтения данных";
                _loggingService.LogWarning(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                return null;
            }
            catch (InvalidOperationException)
            {
                string errorMessage = "Порт закрыт или недоступен при попытке чтения данных";
                _loggingService.LogError(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
                IsConnected = false;
                return null;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка чтения данных: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                IsConnected = false;
                return null;
            }
        }

        /// <summary>
        /// Попытка переподключения при потере связи
        /// </summary>
        private bool TryReconnect()
        {
            int attemptCount = 0;

            try
            {
                while (attemptCount < _maxRetryAttempts)
                {
                    attemptCount++;
                    _loggingService.LogInfo($"Попытка переподключения {attemptCount}/{_maxRetryAttempts}", $"Порт: {Settings.PortName}");

                    // Сначала отключаемся, чтобы очистить ресурсы
                    Disconnect();

                    // Ждем перед повторной попыткой
                    Thread.Sleep(_reconnectDelay);

                    // Пробуем подключиться снова
                    if (Connect())
                    {
                        _loggingService.LogInfo("Переподключение успешно", $"Порт: {Settings.PortName}");
                        return true;
                    }
                }

                _loggingService.LogError($"Не удалось переподключиться после {attemptCount} попыток", $"Порт: {Settings.PortName}");
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при попытке переподключения: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Улучшенная обработка ошибок и восстановление соединения
        /// </summary>
        public async Task<bool> TryReconnectAsync()
        {
            int attemptCount = 0;
            bool reconnected = false;

            // Сохраняем исходное значение задержки переподключения
            int originalReconnectDelay = _reconnectDelay;

            try
            {
                while (attemptCount < _maxRetryAttempts && !reconnected)
                {
                    attemptCount++;
                    _loggingService.LogInfo($"Попытка переподключения {attemptCount}/{_maxRetryAttempts}", $"Порт: {Settings.PortName}");

                    // Сначала отключаемся, чтобы очистить ресурсы
                    Disconnect();

                    // Пауза перед повторной попыткой
                    await Task.Delay(_reconnectDelay);

                    try
                    {
                        // Проверка доступности порта
                        string[] availablePorts = await Task.Run(() => SerialPort.GetPortNames());
                        if (!availablePorts.Contains(Settings.PortName))
                        {
                            _loggingService.LogWarning($"Порт {Settings.PortName} недоступен, ожидание...");
                            continue;
                        }

                        // Пробуем подключиться снова
                        reconnected = Connect();

                        if (reconnected)
                        {
                            _loggingService.LogInfo("Переподключение успешно", $"Порт: {Settings.PortName}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogWarning($"Ошибка при попытке №{attemptCount}: {ex.Message}");
                    }

                    // Увеличиваем интервал между попытками для экспоненциальной задержки
                    _reconnectDelay = Math.Min(_reconnectDelay * 2, 10000); // Максимум 10 секунд
                }

                _loggingService.LogError($"Не удалось переподключиться после {attemptCount} попыток", $"Порт: {Settings.PortName}");
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Критическая ошибка при попытке переподключения: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
            finally
            {
                // Восстанавливаем исходное значение задержки
                _reconnectDelay = originalReconnectDelay;
            }
        }

        /// <summary>
        /// Улучшенная обработка ошибок чтения данных
        /// </summary>
        private byte[] ReadDataWithRetry(int bytesToRead, int maxRetries = 3)
        {
            int attempt = 0;
            Exception lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
                    {
                        _loggingService.LogWarning("Попытка чтения данных при отсутствии соединения");
                        return null;
                    }

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                    {
                        // Если прочитано 0 байт, повторяем попытку
                        _loggingService.LogWarning("Прочитано 0 байт, повторяем попытку...");
                        attempt++;
                        Task.Delay(50).Wait(); // Небольшая задержка перед повтором
                        continue;
                    }

                    if (bytesRead != bytesToRead)
                    {
                        // Частичное чтение - возвращаем только прочитанные данные
                        _loggingService.LogWarning($"Неполное чтение данных: прочитано {bytesRead} из {bytesToRead} байт");
                        byte[] result = new byte[bytesRead];
                        Array.Copy(buffer, result, bytesRead);
                        return result;
                    }

                    // Успешное чтение
                    return buffer;
                }
                catch (TimeoutException ex)
                {
                    lastException = ex;
                    _loggingService.LogWarning($"Таймаут чтения данных (попытка {attempt + 1}/{maxRetries})");
                }
                catch (InvalidOperationException ex)
                {
                    lastException = ex;
                    _loggingService.LogError("Порт закрыт или недоступен при попытке чтения данных");
                    IsConnected = false;
                    return null; // Прекращаем попытки при ошибке порта
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _loggingService.LogError($"Ошибка чтения данных: {ex.Message}");
                }

                attempt++;
                Task.Delay(100 * attempt).Wait(); // Увеличиваем задержку с каждой попыткой
            }

            // Если все попытки неудачны, генерируем событие ошибки
            string errorMessage = $"Не удалось прочитать данные после {maxRetries} попыток";
            _loggingService.LogError(errorMessage, lastException?.Message);
            ErrorOccurred?.Invoke(this, errorMessage);
            return null;
        }

        /// <summary>
        /// Асинхронное чтение ответа на команду
        /// </summary>
        private async Task<bool> ReadResponseAsync(ERCHM30TZCommand command)
        {
            try
            {
                _loggingService.LogInfo("Ожидание ответа на команду", $"Тип: {command.CommandType}, Задержка: {Settings.ResponseDelay} мс");

                // Константы протокола
                const byte SYNC_START = ERCHM30TZProtocol.SYNC_START; // 255
                const byte SYNC_2 = ERCHM30TZProtocol.SYNC_2;         // 254

                // Ждем начала ответа
                await Task.Delay(Settings.ResponseDelay);

                // Создаем буфер фиксированного размера (оптимизация памяти)
                byte[] buffer = new byte[256]; // Максимальный размер пакета ЭРЧМ30ТЗ
                int bufferPos = 0;
                bool packetStartFound = false;
                int timeoutCounter = 0;
                int bytesRead = 0;

                // Ожидаем данные с таймаутом
                while (timeoutCounter < 30) // Максимум 3 секунды при задержке 100 мс
                {
                    if (_serialPort?.BytesToRead > 0)
                    {
                        bytesRead = _serialPort.Read(buffer, bufferPos, Math.Min(buffer.Length - bufferPos, _serialPort.BytesToRead));

                        // Поиск начала пакета и корректная обработка byte-stuffing
                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (!packetStartFound)
                            {
                                // Ищем начало пакета (SYNC_START)
                                if (buffer[i] == SYNC_START)
                                {
                                    packetStartFound = true;
                                    buffer[0] = SYNC_START;
                                    bufferPos = 1;
                                }
                            }
                            else
                            {
                                // Обработка последовательного SYNC_START + SYNC_2 (byte-stuffing)
                                if (i > 0 && buffer[i] == SYNC_2 && buffer[i - 1] == SYNC_START)
                                {
                                    // Это был экранированный SYNC_START, убираем SYNC_2
                                    continue;
                                }

                                buffer[bufferPos++] = buffer[i];

                                // Если достаточно данных для определения длины пакета
                                if (bufferPos >= 2)
                                {
                                    byte lDat = buffer[1]; // Длина пакета

                                    // Проверяем наличие полного пакета: SYNC_START + L_DAT + ... + CS
                                    if (bufferPos >= lDat + 2)
                                    {
                                        // Проверка контрольной суммы
                                        byte calculatedChecksum = ERCHM30TZProtocol.CalculateChecksum(buffer, 1, bufferPos - 2);
                                        byte receivedChecksum = buffer[bufferPos - 1];

                                        if (calculatedChecksum == receivedChecksum)
                                        {
                                            // Извлекаем данные (без SYNC_START, L_DAT, COM и CS)
                                            byte[] data = new byte[lDat - 2];
                                            Array.Copy(buffer, 3, data, 0, lDat - 2);
                                            return ProcessResponse(command, data);
                                        }
                                        else
                                        {
                                            _loggingService.LogWarning(
                                                $"Ошибка контрольной суммы: получено 0x{receivedChecksum:X2}, вычислено 0x{calculatedChecksum:X2}");
                                            return false;
                                        }
                                    }
                                }

                                // Проверка на переполнение буфера
                                if (bufferPos >= buffer.Length)
                                {
                                    _loggingService.LogWarning("Переполнение буфера при чтении ответа");
                                    return false;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Если нет данных, ждем
                        await Task.Delay(100);
                        timeoutCounter++;
                    }
                }

                _loggingService.LogWarning("Таймаут при ожидании ответа");
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка чтения ответа: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Вычисление контрольной суммы (XOR всех байтов)
        /// </summary>
        private byte CalculateChecksum(byte[] data)
        {
            byte checksum = 0;
            foreach (byte b in data)
            {
                checksum ^= b; // XOR
            }
            return checksum;
        }

        /// <summary>
        /// Обработка ответа от контроллера
        /// </summary>
        private bool ProcessResponse(ERCHM30TZCommand command, byte[] data)
        {
            try
            {
                _loggingService.LogInfo($"Обработка ответа на команду: {command.CommandType}, Длина данных: {data.Length} байт");

                switch (command.CommandType)
                {
                    case CommandType.GetParameters:
                        // Разбор параметров двигателя
                        var parameters = ParseEngineParameters(data);
                        if (parameters != null)
                        {
                            _loggingService.LogInfo("Получены параметры двигателя",
                                $"Обороты: {parameters.EngineSpeed:F0}, Давление масла: {parameters.OilPressure:F2}");

                            // Уведомляем подписчиков о новых данных
                            DataReceived?.Invoke(this, parameters);
                            return true;
                        }
                        return false;

                    case CommandType.SetEngineSpeed:
                        _loggingService.LogInfo("Получен ответ на установку оборотов", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00; // 0x00 - успех, другое - код ошибки

                    case CommandType.SetRackPosition:
                        _loggingService.LogInfo("Получен ответ на установку положения рейки", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    case CommandType.SetEngineMode:
                        _loggingService.LogInfo("Получен ответ на установку режима двигателя", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    case CommandType.SetLoadType:
                        _loggingService.LogInfo("Получен ответ на установку типа нагрузки", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    case CommandType.SetEquipmentPosition:
                        _loggingService.LogInfo("Получен ответ на установку позиции оборудования", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    case CommandType.GetProtectionStatus:
                        _loggingService.LogInfo("Получен ответ на запрос статуса защит", $"Длина данных: {data.Length} байт");
                        var protectionStatus = ParseProtectionStatus(data);
                        if (protectionStatus != null)
                        {
                            // Уведомляем подписчиков о статусе защит
                            ProtectionStatusUpdated?.Invoke(this, protectionStatus);
                            return true;
                        }
                        return false;

                    case CommandType.SetProtectionThresholds:
                        _loggingService.LogInfo("Получен ответ на установку порогов защит", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    case CommandType.ResetProtection:
                        _loggingService.LogInfo("Получен ответ на сброс защит", $"Статус: {GetStatusFromResponse(data)}");
                        return data.Length > 0 && data[0] == 0x00;

                    default:
                        _loggingService.LogWarning($"Получен ответ на неизвестную команду: {command.CommandType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка обработки ответа: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return false;
            }
        }

        /// <summary>
        /// Получение статуса из ответа
        /// </summary>
        private string GetStatusFromResponse(byte[] data)
        {
            if (data == null || data.Length < 1)
                return "Неизвестно";

            return data[0] == 0x00 ? "Успешно" : $"Ошибка: 0x{data[0]:X2}";
        }

        /// <summary>
        /// Разбор статуса защит из ответа
        /// </summary>
        private ProtectionStatus ParseProtectionStatus(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                _loggingService.LogWarning("Недостаточно данных для разбора статуса защит");
                return null;
            }

            // Разбор статуса защит (байты флагов)
            bool isOilPressureActive = (data[0] & 0x01) != 0;
            bool isEngineSpeedActive = (data[0] & 0x02) != 0;
            bool isBoostPressureActive = (data[0] & 0x04) != 0;
            bool isOilTemperatureActive = (data[0] & 0x08) != 0;

            _loggingService.LogInfo("Статус защит:",
                $"Давление масла: {(isOilPressureActive ? "АКТИВНА" : "Норма")}, " +
                $"Обороты: {(isEngineSpeedActive ? "АКТИВНА" : "Норма")}, " +
                $"Давление наддува: {(isBoostPressureActive ? "АКТИВНА" : "Норма")}, " +
                $"Температура масла: {(isOilTemperatureActive ? "АКТИВНА" : "Норма")}");

            return new ProtectionStatus
            {
                IsOilPressureActive = isOilPressureActive,
                IsEngineSpeedActive = isEngineSpeedActive,
                IsBoostPressureActive = isBoostPressureActive,
                IsOilTemperatureActive = isOilTemperatureActive,
                AllProtectionsEnabled = (data[1] & 0x01) != 0
            };
        }

        /// <summary>
        /// Формирование пакета данных для отправки
        /// </summary>
        private byte[] ComposePacket(ERCHM30TZCommand command)
        {
            // Реализация пакета в соответствии с требованиями протокола ЭРЧМ30ТЗ
            List<byte> packet = new List<byte>();

            // Константы протокола из ERCHM30TZProtocol
            byte SYNC_START = ERCHM30TZProtocol.SYNC_START;
            byte SYNC_2 = ERCHM30TZProtocol.SYNC_2;

            // Маркер начала пакета
            packet.Add(SYNC_START);

            // Подготовим данные в соответствии с таблицей 1 из документации
            List<byte> dataField = new List<byte>();

            switch (command.CommandType)
            {
                case CommandType.GetParameters:
                    // Пустой запрос параметров, данные не требуются
                    break;

                case CommandType.SetEngineSpeed:
                    // Задание частоты вращения (два байта: старший и младший)
                    ushort speedValue = (ushort)command.EngineSpeed;
                    var speedBytes = ERCHM30TZProtocol.UInt16ToBytes(speedValue);

                    dataField.Add(speedBytes.HighByte);   // Старший байт (F_z_h)
                    dataField.Add(speedBytes.LowByte);    // Младший байт (F_z_l)
                    dataField.Add(0); // Признак поездного режима (POWER): 0 = холостой ход
                    dataField.Add(1); // Признак запуска/стопа (Pusk): 1 = РАБОТА
                    dataField.Add(0); // Резерв
                    dataField.Add(0); // Резерв
                    break;

                case CommandType.SetEngineMode:
                    // Формируем команду с признаком запуска/останова
                    // Добавляем все байты из таблицы 1
                    dataField.Add(0); // Старший байт задания частоты (F_z_h)
                    dataField.Add(0); // Младший байт задания частоты (F_z_l)
                    dataField.Add(0); // Признак поездного режима (POWER): 0 = холостой ход
                                      // Признак запуска/стопа: 0 = СТОП, 1 = РАБОТА
                    dataField.Add((byte)(command.EngineMode == EngineMode.Run ? 1 : 0));
                    dataField.Add(0); // Резерв
                    dataField.Add(0); // Резерв
                    break;

                case CommandType.SetLoadType:
                    // Формируем команду с признаком поездного режима
                    dataField.Add(0); // Старший байт задания частоты (F_z_h)
                    dataField.Add(0); // Младший байт задания частоты (F_z_l)
                                      // Поездной режим: 0 = холостой ход, не 0 = поездной режим
                    dataField.Add((byte)(command.LoadType == LoadType.Idle ? 0 : 1));
                    dataField.Add(1); // Признак запуска/стопа (Pusk): 1 = РАБОТА
                    dataField.Add(0); // Резерв
                    dataField.Add(0); // Резерв
                    break;

                case CommandType.GetProtectionStatus:
                    // Запрос статуса защит - дополнительные данные не требуются
                    break;

                case CommandType.ResetProtection:
                    // Команда сброса защит - в зависимости от протокола может требовать данные
                    dataField.Add(0xFF); // Специальный флаг для сброса защит
                    break;

                default:
                    _loggingService.LogWarning($"Неизвестная команда: {command.CommandType}");
                    break;
            }

            // Байт команды COM
            byte comByte = (byte)command.CommandType;

            // Длина пакета L_DAT: количество байт поля данных + 2 (COM и CS)
            byte lDat = (byte)(dataField.Count + 2);
            packet.Add(lDat);
            packet.Add(comByte);

            // Добавляем данные с обработкой byte-stuffing
            foreach (byte dataByte in dataField)
            {
                // Если байт данных равен SYNC_START, добавляем после него SYNC_2
                if (dataByte == SYNC_START)
                {
                    packet.Add(SYNC_START);
                    packet.Add(SYNC_2);
                }
                else
                {
                    packet.Add(dataByte);
                }
            }

            // Вычисляем контрольную сумму по алгоритму протокола
            byte checksum = ERCHM30TZProtocol.CalculateChecksum(packet);
            packet.Add(checksum);

            _loggingService.LogInfo($"Сформирован пакет для команды {command.CommandType}, длина: {packet.Count} байт");

            return packet.ToArray();
        }

        /// <summary>
        /// Разбор параметров двигателя из ответа
        /// </summary>
        private EngineParameters ParseEngineParameters(byte[] data)
        {
            if (data == null || data.Length < 12) // Минимальная длина ответа с параметрами
            {
                _loggingService.LogWarning("Недостаточно данных для разбора параметров двигателя",
                    $"Получено байт: {data?.Length ?? 0}, требуется: 12");
                return null;
            }

            try
            {
                var parameters = new EngineParameters
                {
                    EngineSpeed = BitConverter.ToUInt16(data, 0),
                    TurboCompressorSpeed = BitConverter.ToUInt16(data, 2),
                    OilPressure = BitConverter.ToUInt16(data, 4) / 100.0, // Пример: 250 = 2.5 кг/см²
                    BoostPressure = BitConverter.ToUInt16(data, 6) / 100.0,
                    OilTemperature = BitConverter.ToUInt16(data, 8),
                    RackPosition = BitConverter.ToUInt16(data, 10),
                    Timestamp = DateTime.Now
                };

                return parameters;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка разбора параметров: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Dispose()
        {
            // Отключаем сторожевой таймер
            _simulationWatchdog?.Dispose();

            // Отключаемся от COM-порта
            if (IsConnected)
            {
                Disconnect();
            }

            // Очищаем ресурсы
            _serialPort?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Статус защит системы
        /// </summary>
        public class ProtectionStatus
        {
            /// <summary>
            /// Активна ли защита по давлению масла
            /// </summary>
            public bool IsOilPressureActive { get; set; }

            /// <summary>
            /// Активна ли защита по оборотам двигателя
            /// </summary>
            public bool IsEngineSpeedActive { get; set; }

            /// <summary>
            /// Активна ли защита по давлению наддува
            /// </summary>
            public bool IsBoostPressureActive { get; set; }

            /// <summary>
            /// Активна ли защита по температуре масла
            /// </summary>
            public bool IsOilTemperatureActive { get; set; }

            /// <summary>
            /// Включены ли все защиты
            /// </summary>
            public bool AllProtectionsEnabled { get; set; }
        }
    }
}