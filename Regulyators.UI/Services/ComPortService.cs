using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Regulyators.UI.Models;
using CommandType = Regulyators.UI.Models.CommandType;

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

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка подключения: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка отключения: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка команды в контроллер
        /// </summary>
        public void SendCommand(ERCHM30TZCommand command)
        {
            lock (_lockObj)
            {
                _commandQueue.Enqueue(command);
            }
        }

        /// <summary>
        /// Отправка данных в COM-порт
        /// </summary>
        private bool SendData(byte[] data)
        {
            if (!IsConnected || _serialPort == null || !_serialPort.IsOpen)
            {
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
                ErrorOccurred?.Invoke(this, "Таймаут отправки данных");
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка отправки данных: {ex.Message}");
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
                return null;
            }

            try
            {
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                if (bytesRead != bytesToRead)
                {
                    // Неполное чтение
                    byte[] result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }

                return buffer;
            }
            catch (TimeoutException)
            {
                ErrorOccurred?.Invoke(this, "Таймаут чтения данных");
                return null;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка чтения данных: {ex.Message}");
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
                Disconnect();
                Thread.Sleep(1000); // Пауза перед переподключением
                return Connect();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка переподключения: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обработка очереди команд
        /// </summary>
        private async Task ProcessCommandQueue(CancellationToken cancellationToken)
        {
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
                    // Формируем пакет данных для отправки
                    byte[] packet = ComposePacket(command);

                    if (SendData(packet))
                    {
                        // Ждем ответа
                        await ReadResponseAsync(command);
                    }
                    else
                    {
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
        }

        /// <summary>
        /// Запуск мониторинга параметров двигателя
        /// </summary>
        private void StartMonitoring()
        {
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
                // Ждем начала ответа
                await Task.Delay(Settings.ResponseDelay);

                if (_serialPort.BytesToRead > 0)
                {
                    // Читаем заголовок ответа (первые 4 байта)
                    byte[] header = ReadData(4);

                    if (header != null && header.Length == 4)
                    {
                        // Проверяем маркер начала пакета
                        if (header[0] == 0xAA && header[1] == 0x55)
                        {
                            // Определяем длину данных
                            int dataLength = header[2] | (header[3] << 8);

                            // Читаем данные
                            byte[] data = ReadData(dataLength);

                            if (data != null)
                            {
                                // Обрабатываем полученные данные
                                ProcessResponse(command, data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка чтения ответа: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка ответа от контроллера
        /// </summary>
        private void ProcessResponse(ERCHM30TZCommand command, byte[] data)
        {
            try
            {
                switch (command.CommandType)
                {
                    case CommandType.GetParameters:
                        // Разбор параметров двигателя
                        var parameters = ParseEngineParameters(data);
                        if (parameters != null)
                        {
                            // Уведомляем подписчиков о новых данных
                            DataReceived?.Invoke(this, parameters);
                        }
                        break;

                    case CommandType.SetEngineSpeed:
                        // Обработка ответа на установку оборотов
                        break;

                    case CommandType.SetRackPosition:
                        // Обработка ответа на установку положения рейки
                        break;

                        // Другие команды...
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка обработки ответа: {ex.Message}");
            }
        }

        /// <summary>
        /// Формирование пакета данных для отправки
        /// </summary>
        private byte[] ComposePacket(ERCHM30TZCommand command)
        {
            // Реализация формирования пакета по протоколу ЭРЧМ30ТЗ
            // Это упрощенная демонстрационная реализация

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

                    // Другие команды...
            }

            // Добавляем длину данных
            packet.Add((byte)(dataLength & 0xFF));
            packet.Add((byte)((dataLength >> 8) & 0xFF));

            // Добавляем данные, если есть
            if (data != null && data.Length > 0)
            {
                packet.AddRange(data);
            }

            // Контрольная сумма (простая сумма всех байтов)
            byte checksum = 0;
            for (int i = 0; i < packet.Count; i++)
            {
                checksum += packet[i];
            }
            packet.Add(checksum);

            return packet.ToArray();
        }

        /// <summary>
        /// Разбор параметров двигателя из ответа
        /// </summary>
        private EngineParameters ParseEngineParameters(byte[] data)
        {
            // Это демонстрационная реализация разбора данных
            if (data.Length < 12) // Минимальная длина ответа с параметрами
            {
                return null;
            }

            try
            {
                var parameters = new EngineParameters
                {
                    EngineSpeed = BitConverter.ToUInt16(data, 0),
                    TurboCompressorSpeed = BitConverter.ToUInt16(data, 2),
                    OilPressure = BitConverter.ToUInt16(data, 4) / 100.0,
                    BoostPressure = BitConverter.ToUInt16(data, 6) / 100.0,
                    OilTemperature = BitConverter.ToUInt16(data, 8),
                    RackPosition = BitConverter.ToUInt16(data, 10),
                    Timestamp = DateTime.Now
                };

                return parameters;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Ошибка разбора параметров: {ex.Message}");
                return null;
            }
        }
    }
}