using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Сервис для асинхронной работы с COM-портом и протоколом ЭРЧМ30ТЗ
    /// </summary>
    public class ComPortService
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
        /// Текущие настройки порта
        /// </summary>
        public ComPortSettings Settings { get; private set; }

        // Событие получения команды для симулятора
        public event EventHandler<ERCHM30TZCommand> CommandReceived;

        // Флаг режима симуляции
        private bool _isSimulationMode = false;

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
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static ComPortService Instance => _instance ??= new ComPortService();

        /// <summary>
        /// Включение или выключение режима симуляции
        /// </summary>
        public void SetSimulationMode(bool enabled)
        {
            _isSimulationMode = enabled;
            _loggingService.LogInfo($"Режим симуляции {(enabled ? "включен" : "выключен")}");
        }

        /// <summary>
        /// Симуляция состояния подключения
        /// </summary>
        public void SimulateConnection(bool isConnected)
        {
            IsConnected = isConnected;
        }

        /// <summary>
        /// Симуляция получения данных от контроллера
        /// </summary>
        public void SimulateDataReceived(EngineParameters parameters)
        {
            DataReceived?.Invoke(this, parameters);
        }

        /// <summary>
        /// Симуляция обновления статуса защит
        /// </summary>
        public void SimulateProtectionStatusUpdated(ProtectionStatus status)
        {
            ProtectionStatusUpdated?.Invoke(this, status);
        }

        private ComPortService()
        {
            _commandQueue = new Queue<ERCHM30TZCommand>();
            _commandCompletionQueue = new Queue<TaskCompletionSource<bool>>();
            _loggingService = LoggingService.Instance;
            Settings = new ComPortSettings();
            _isConnected = false;
        }

        /// <summary>
        /// Получение списка доступных COM-портов
        /// </summary>
        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Установка настроек COM-порта
        /// </summary>
        public void UpdateSettings(ComPortSettings settings)
        {
            if (IsConnected)
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
        public new bool Connect()
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
                    IsConnected = true;
                    return true;
                }

                _loggingService.LogInfo("Попытка подключения к COM-порту", $"Порт: {Settings.PortName}, Скорость: {Settings.BaudRate}");

                _serialPort = new System.IO.Ports.SerialPort
                {
                    PortName = Settings.PortName,
                    BaudRate = Settings.BaudRate,
                    DataBits = Settings.DataBits,
                    StopBits = Settings.StopBits,
                    Parity = Settings.Parity,
                    ReadTimeout = Settings.ReadTimeout,
                    WriteTimeout = Settings.WriteTimeout
                };

                _serialPort.Open();

                // Очищаем буферы порта
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                IsConnected = true;

                // Запускаем задачи обработки данных
                _cancellationTokenSource = new System.Threading.CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessCommandQueue(_cancellationTokenSource.Token));

                // Запускаем мониторинг порта
                StartMonitoring();

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
        public new void Disconnect()
        {
            try
            {
                _loggingService.LogInfo("Отключение от COM-порта", $"Порт: {Settings.PortName}");

                if (_isSimulationMode)
                {
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
        public Task<bool> SendCommandAsync(ERCHM30TZCommand command)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                LogCommand(command);

                if (_isSimulationMode)
                {
                    // В режиме симуляции вызываем событие получения команды
                    CommandReceived?.Invoke(this, command);

                    // Симулируем успешное выполнение с небольшой задержкой
                    Task.Delay(100).ContinueWith(_ => tcs.TrySetResult(true));
                }
                else
                {
                    // Добавляем в очередь
                    lock (_lockObj)
                    {
                        _commandQueue.Enqueue(command);
                        _commandCompletionQueue.Enqueue(tcs);
                    }
                }

                return tcs.Task;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка добавления команды в очередь: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
                tcs.TrySetResult(false);
                return tcs.Task;
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
        /// Запуск мониторинга параметров двигателя
        /// </summary>
        private void StartMonitoring()
        {
            _loggingService.LogInfo("Запуск мониторинга параметров двигателя", $"Интервал: {Settings.PollingInterval} мс");

            // Запускаем периодический опрос параметров
            Task.Run(async () =>
            {
                while (IsConnected)
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

        /// <summary>
        /// Асинхронное чтение ответа на команду
        /// </summary>
        private async Task<bool> ReadResponseAsync(ERCHM30TZCommand command)
        {
            try
            {
                _loggingService.LogInfo("Ожидание ответа на команду", $"Тип: {command.CommandType}, Задержка: {Settings.ResponseDelay} мс");

                // Ждем начала ответа
                await Task.Delay(Settings.ResponseDelay);

                // Проверяем, есть ли данные для чтения
                if (_serialPort?.BytesToRead > 0)
                {
                    _loggingService.LogInfo($"Получен ответ, байт доступно: {_serialPort.BytesToRead}");

                    // Читаем заголовок ответа (первые 4 байта)
                    byte[] header = ReadData(4);

                    if (header != null && header.Length == 4)
                    {
                        // Проверяем маркер начала пакета (0xAA, 0x55 для ЭРЧМ30ТЗ)
                        if (header[0] == 0xAA && header[1] == 0x55)
                        {
                            // Определяем длину данных из третьего и четвертого байтов (младший и старший байт)
                            int dataLength = header[2] | (header[3] << 8);

                            _loggingService.LogInfo($"Получен заголовок ответа, длина данных: {dataLength} байт");

                            if (dataLength > 0 && dataLength < 1024) // Проверка на разумный размер данных
                            {
                                // Читаем данные
                                byte[] data = ReadData(dataLength);

                                if (data != null)
                                {
                                    // Читаем контрольную сумму (1 байт)
                                    byte[] checksumBytes = ReadData(1);

                                    if (checksumBytes != null && checksumBytes.Length == 1)
                                    {
                                        byte receivedChecksum = checksumBytes[0];

                                        // Вычисляем контрольную сумму
                                        byte calculatedChecksum = CalculateChecksum(header.Concat(data).ToArray());

                                        if (receivedChecksum == calculatedChecksum)
                                        {
                                            // Обрабатываем полученные данные
                                            return ProcessResponse(command, data);
                                        }
                                        else
                                        {
                                            _loggingService.LogWarning(
                                                $"Ошибка контрольной суммы: получено 0x{receivedChecksum:X2}, вычислено 0x{calculatedChecksum:X2}");
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        _loggingService.LogWarning("Не удалось прочитать контрольную сумму");
                                        return false;
                                    }
                                }
                                else
                                {
                                    _loggingService.LogWarning("Не удалось прочитать данные ответа");
                                    return false;
                                }
                            }
                            else
                            {
                                _loggingService.LogWarning($"Недопустимая длина данных: {dataLength}");
                                return false;
                            }
                        }
                        else
                        {
                            _loggingService.LogWarning("Неверный маркер начала пакета",
                                $"Получено: 0x{header[0]:X2}{header[1]:X2}, ожидалось: 0xAA55");
                            return false;
                        }
                    }
                    else
                    {
                        _loggingService.LogWarning("Не удалось прочитать заголовок ответа");
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogWarning("Нет данных для чтения после ожидания ответа");
                    return false;
                }
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
            // Реализация формирования пакета по протоколу ЭРЧМ30ТЗ
            List<byte> packet = new List<byte>();

            // Маркер начала пакета
            packet.Add(0xAA);
            packet.Add(0x55);

            // Код команды
            packet.Add((byte)command.CommandType);

            // Длина данных (младший и старший байты)
            int dataLength = 0;
            byte[] data = null;

            switch (command.CommandType)
            {
                case CommandType.GetParameters:
                    // Для запроса параметров данных нет
                    break;

                case CommandType.SetEngineSpeed:
                    // Преобразуем значение оборотов в байты
                    data = BitConverter.GetBytes((ushort)command.EngineSpeed);
                    dataLength = data.Length;
                    break;

                case CommandType.SetRackPosition:
                    // Преобразуем значение положения рейки в байты (умножаем на 100 для точности)
                    data = BitConverter.GetBytes((ushort)(command.RackPosition * 100));
                    dataLength = data.Length;
                    break;

                case CommandType.SetEngineMode:
                    // Преобразуем режим в байт
                    data = new byte[] { (byte)command.EngineMode };
                    dataLength = data.Length;
                    break;

                case CommandType.SetLoadType:
                    // Преобразуем тип нагрузки в байт
                    data = new byte[] { (byte)command.LoadType };
                    dataLength = data.Length;
                    break;

                case CommandType.SetEquipmentPosition:
                    // Преобразуем позицию оборудования в байты
                    data = BitConverter.GetBytes((ushort)command.EquipmentPosition);
                    dataLength = data.Length;
                    break;

                case CommandType.GetProtectionStatus:
                    // Для запроса статуса защит данных нет
                    break;

                case CommandType.SetProtectionThresholds:
                    // Преобразуем пороги защит в байты
                    List<byte> thresholdBytes = new List<byte>();

                    // Добавляем минимальное давление масла (2 байта, умноженное на 100 для сохранения 2 знаков после запятой)
                    thresholdBytes.AddRange(BitConverter.GetBytes((ushort)(command.Thresholds.OilPressureMinThreshold * 100)));

                    // Добавляем максимальные обороты двигателя (2 байта)
                    thresholdBytes.AddRange(BitConverter.GetBytes((ushort)command.Thresholds.EngineSpeedMaxThreshold));

                    // Добавляем максимальное давление наддува (2 байта, умноженное на 100)
                    thresholdBytes.AddRange(BitConverter.GetBytes((ushort)(command.Thresholds.BoostPressureMaxThreshold * 100)));

                    // Добавляем максимальную температуру масла (2 байта, умноженное на 10)
                    thresholdBytes.AddRange(BitConverter.GetBytes((ushort)(command.Thresholds.OilTemperatureMaxThreshold * 10)));

                    data = thresholdBytes.ToArray();
                    dataLength = data.Length;
                    break;

                case CommandType.ResetProtection:
                    // Для сброса защит данных нет
                    break;
            }

            // Добавляем длину данных
            packet.Add((byte)(dataLength & 0xFF));
            packet.Add((byte)((dataLength >> 8) & 0xFF));

            // Добавляем данные, если есть
            if (data != null && data.Length > 0)
            {
                packet.AddRange(data);
            }

            // Считаем контрольную сумму
            byte checksum = 0;
            foreach (byte b in packet)
            {
                checksum ^= b;
            }

            // Добавляем контрольную сумму
            packet.Add(checksum);

            _loggingService.LogInfo($"Сформирован пакет данных для команды {command.CommandType}, длина: {packet.Count} байт");

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