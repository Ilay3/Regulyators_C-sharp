using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
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
        /// Текущие настройки порта
        /// </summary>
        public ComPortSettings Settings { get; private set; }

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

        private ComPortService()
        {
            _commandQueue = new Queue<ERCHM30TZCommand>();
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

                _serialPort.Open();

                IsConnected = true;

                // Запускаем задачи обработки данных
                _cancellationTokenSource = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessCommandQueue(_cancellationTokenSource.Token));

                // Запускаем мониторинг порта
                StartMonitoring();

                _loggingService.LogInfo("Подключение к COM-порту успешно", $"Порт: {Settings.PortName}");
                return true;
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

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // Останавливаем обработку команд
                    _cancellationTokenSource?.Cancel();

                    if (_processingTask != null)
                    {
                        try
                        {
                            _processingTask.Wait(1000); // Даем задаче завершиться
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

                // Очищаем очередь команд
                lock (_lockObj)
                {
                    _commandQueue.Clear();
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
        public void SendCommand(ERCHM30TZCommand command)
        {
            try
            {
                // Логируем команду
                LogCommand(command);

                // Добавляем в очередь
                lock (_lockObj)
                {
                    _commandQueue.Enqueue(command);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка добавления команды в очередь: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
            }
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
                _serialPort.Write(data, 0, data.Length);
                return true;
            }
            catch (TimeoutException)
            {
                string errorMessage = "Таймаут отправки данных";
                _loggingService.LogWarning(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
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
        private byte[] ReadData(int bytesToRead)
        {
            if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
            {
                _loggingService.LogWarning("Попытка чтения данных при отсутствии соединения");
                return null;
            }

            try
            {
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                if (bytesRead != bytesToRead)
                {
                    // Неполное чтение
                    _loggingService.LogWarning($"Неполное чтение данных: прочитано {bytesRead} из {bytesToRead} байт");
                    byte[] result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }

                return buffer;
            }
            catch (TimeoutException)
            {
                string errorMessage = "Таймаут чтения данных";
                _loggingService.LogWarning(errorMessage);
                ErrorOccurred?.Invoke(this, errorMessage);
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
            try
            {
                _loggingService.LogInfo("Попытка переподключения", $"Порт: {Settings.PortName}");

                Disconnect();
                Thread.Sleep(1000); // Пауза перед переподключением

                bool result = Connect();

                if (result)
                    _loggingService.LogInfo("Переподключение успешно", $"Порт: {Settings.PortName}");
                else
                    _loggingService.LogError("Не удалось переподключиться", $"Порт: {Settings.PortName}");

                return result;
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

                // Извлекаем команду из очереди
                lock (_lockObj)
                {
                    if (_commandQueue.Count > 0)
                    {
                        command = _commandQueue.Dequeue();
                    }
                }

                if (command != null)
                {
                    _loggingService.LogInfo($"Обработка команды из очереди: {command.CommandType}");

                    // Формируем пакет данных для отправки
                    byte[] packet = ComposePacket(command);

                    if (SendData(packet))
                    {
                        _loggingService.LogInfo("Команда отправлена успешно", $"Тип: {command.CommandType}");

                        // Ждем ответа
                        await ReadResponseAsync(command);
                    }
                    else
                    {
                        _loggingService.LogWarning("Не удалось отправить команду, возврат в очередь", $"Тип: {command.CommandType}");

                        // Если не удалось отправить, возвращаем команду в очередь
                        lock (_lockObj)
                        {
                            _commandQueue.Enqueue(command);
                        }

                        // Делаем паузу перед следующей попыткой
                        await Task.Delay(1000, cancellationToken);
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

                    // Пауза между опросами
                    await Task.Delay(Settings.PollingInterval);
                }
            });
        }

        /// <summary>
        /// Асинхронное чтение ответа на команду
        /// </summary>
        private async Task ReadResponseAsync(ERCHM30TZCommand command)
        {
            try
            {
                _loggingService.LogInfo("Ожидание ответа на команду", $"Тип: {command.CommandType}, Задержка: {Settings.ResponseDelay} мс");

                // Ждем начала ответа
                await Task.Delay(Settings.ResponseDelay);

                if (_serialPort?.BytesToRead > 0)
                {
                    _loggingService.LogInfo($"Получен ответ, байт доступно: {_serialPort.BytesToRead}");

                    // Читаем заголовок ответа (первые 4 байта)
                    byte[] header = ReadData(4);

                    if (header != null && header.Length == 4)
                    {
                        // Проверяем маркер начала пакета
                        if (header[0] == 0xAA && header[1] == 0x55)
                        {
                            // Определяем длину данных
                            int dataLength = header[2] | (header[3] << 8);

                            _loggingService.LogInfo($"Получен заголовок ответа, длина данных: {dataLength} байт");

                            // Читаем данные
                            byte[] data = ReadData(dataLength);

                            if (data != null)
                            {
                                // Обрабатываем полученные данные
                                ProcessResponse(command, data);
                            }
                            else
                            {
                                _loggingService.LogWarning("Не удалось прочитать данные ответа");
                            }
                        }
                        else
                        {
                            _loggingService.LogWarning("Неверный маркер начала пакета", $"Получено: 0x{header[0]:X2}{header[1]:X2}, ожидалось: 0xAA55");
                        }
                    }
                    else
                    {
                        _loggingService.LogWarning("Не удалось прочитать заголовок ответа");
                    }
                }
                else
                {
                    _loggingService.LogWarning("Нет данных для чтения после ожидания ответа");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка чтения ответа: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
            }
        }

        /// <summary>
        /// Обработка ответа от контроллера
        /// </summary>
        private void ProcessResponse(ERCHM30TZCommand command, byte[] data)
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
                        }
                        break;

                    case CommandType.SetEngineSpeed:
                        _loggingService.LogInfo("Получен ответ на установку оборотов", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.SetRackPosition:
                        _loggingService.LogInfo("Получен ответ на установку положения рейки", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.SetEngineMode:
                        _loggingService.LogInfo("Получен ответ на установку режима двигателя", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.SetLoadType:
                        _loggingService.LogInfo("Получен ответ на установку типа нагрузки", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.SetEquipmentPosition:
                        _loggingService.LogInfo("Получен ответ на установку позиции оборудования", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.GetProtectionStatus:
                        _loggingService.LogInfo("Получен ответ на запрос статуса защит", $"Длина данных: {data.Length} байт");
                        ParseProtectionStatus(data);
                        break;

                    case CommandType.SetProtectionThresholds:
                        _loggingService.LogInfo("Получен ответ на установку порогов защит", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    case CommandType.ResetProtection:
                        _loggingService.LogInfo("Получен ответ на сброс защит", $"Статус: {GetStatusFromResponse(data)}");
                        break;

                    default:
                        _loggingService.LogWarning($"Получен ответ на неизвестную команду: {command.CommandType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка обработки ответа: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                ErrorOccurred?.Invoke(this, errorMessage);
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
        private void ParseProtectionStatus(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                _loggingService.LogWarning("Недостаточно данных для разбора статуса защит");
                return;
            }

            // Пример разбора статуса защит (зависит от протокола)
            bool isOilPressureActive = (data[0] & 0x01) != 0;
            bool isEngineSpeedActive = (data[0] & 0x02) != 0;
            bool isBoostPressureActive = (data[0] & 0x04) != 0;
            bool isOilTemperatureActive = (data[0] & 0x08) != 0;

            _loggingService.LogInfo("Статус защит:",
                $"Давление масла: {(isOilPressureActive ? "АКТИВНА" : "Норма")}, " +
                $"Обороты: {(isEngineSpeedActive ? "АКТИВНА" : "Норма")}, " +
                $"Давление наддува: {(isBoostPressureActive ? "АКТИВНА" : "Норма")}, " +
                $"Температура масла: {(isOilTemperatureActive ? "АКТИВНА" : "Норма")}");

            // Здесь можно было бы вызвать события для оповещения о статусе защит
        }

        /// <summary>
        /// Формирование пакета данных для отправки
        /// </summary>
        private byte[] ComposePacket(ERCHM30TZCommand command)
        {
            // Реализация формирования пакета по протоколу ЭРЧМ30ТЗ
            // Это упрощенная демонстрационная реализация, которая должна быть заменена
            // на реальный протокол ЭРЧМ30ТЗ

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
                    // Преобразуем значение положения рейки в байты
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

            // Контрольная сумма (XOR всех байтов)
            byte checksum = 0;
            for (int i = 0; i < packet.Count; i++)
            {
                checksum ^= packet[i];
            }
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
                _loggingService.LogWarning("Недостаточно данных для разбора параметров двигателя", $"Получено байт: {data?.Length ?? 0}, требуется: 12");
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
}