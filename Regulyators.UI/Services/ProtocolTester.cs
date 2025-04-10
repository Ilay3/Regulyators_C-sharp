using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Класс для тестирования и диагностики протокола ЭРЧМ30ТЗ
    /// </summary>
    public class ProtocolTester
    {
        // Константы протокола
        private const byte SYNC_START = 255; // Признак начала пакета (0xFF)
        private const byte SYNC_2 = 254;     // Байт для byte-stuffing (0xFE)

        private readonly LoggingService _loggingService;
        private SerialPort _serialPort;

        /// <summary>
        /// Конструктор
        /// </summary>
        public ProtocolTester()
        {
            _loggingService = LoggingService.Instance;
        }

        /// <summary>
        /// Выполнить тестирование протокола на указанном порту
        /// </summary>
        public async Task<bool> TestProtocolAsync(string portName)
        {
            try
            {
                _loggingService.LogInfo("Начало тестирования протокола", $"Порт: {portName}");

                // Создаем порт с правильными настройками протокола
                _serialPort = new SerialPort(portName, 9600, Parity.Odd, 8, StopBits.Two)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                // Открываем порт
                _serialPort.Open();
                _loggingService.LogInfo("Порт открыт успешно", portName);

                // Очищаем буферы
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                // Тестирование различных типов пакетов
                bool test1 = await TestGetParametersPacket();
                bool test2 = await TestSetEngineSpeedPacket();
                bool test3 = await TestByteStuffing();

                // Закрываем порт
                _serialPort.Close();
                _serialPort.Dispose();

                bool allTestsPassed = test1 && test2 && test3;
                _loggingService.LogInfo($"Тестирование протокола завершено. Результат: {(allTestsPassed ? "УСПЕШНО" : "ОШИБКИ")}");

                return allTestsPassed;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при тестировании протокола", ex.Message);

                // Закрываем порт, если он открыт
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                return false;
            }
        }

        /// <summary>
        /// Тест пакета запроса параметров
        /// </summary>
        private async Task<bool> TestGetParametersPacket()
        {
            try
            {
                _loggingService.LogInfo("Тест запроса параметров...");

                // Создаем тестовый пакет запроса параметров
                byte[] packet = CreateTestPacket(CommandType.GetParameters, new byte[0]);

                // Отправляем пакет
                _serialPort.Write(packet, 0, packet.Length);
                _loggingService.LogInfo("Отправлен пакет запроса параметров", BitConverter.ToString(packet));

                // Ждем ответ
                await Task.Delay(100);

                // Проверяем ответ
                if (_serialPort.BytesToRead > 0)
                {
                    byte[] response = new byte[_serialPort.BytesToRead];
                    _serialPort.Read(response, 0, response.Length);

                    _loggingService.LogInfo("Получен ответ", BitConverter.ToString(response));

                    // Анализируем ответ
                    bool validResponse = response.Length > 3 && response[0] == SYNC_START;

                    if (validResponse)
                    {
                        _loggingService.LogInfo("Тест запроса параметров ПРОЙДЕН");
                        return true;
                    }
                    else
                    {
                        _loggingService.LogWarning("Тест запроса параметров НЕ ПРОЙДЕН: некорректный ответ");
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogWarning("Тест запроса параметров НЕ ПРОЙДЕН: нет ответа");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при тестировании запроса параметров", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Тест пакета установки оборотов двигателя
        /// </summary>
        private async Task<bool> TestSetEngineSpeedPacket()
        {
            try
            {
                _loggingService.LogInfo("Тест установки оборотов двигателя...");

                // Данные для команды (согласно таблице 1)
                // F_z_h (старший байт), F_z_l (младший байт), POWER, Pusk, резерв, резерв
                byte[] data = new byte[] {
                    0x03, // 800 об/мин (старший байт)
                    0x20, // 800 об/мин (младший байт)
                    0x00, // Признак поездного режима: 0 = холостой ход
                    0x01, // Признак запуска/стопа: 1 = РАБОТА
                    0x00, // Резерв
                    0x00  // Резерв
                };

                // Создаем тестовый пакет
                byte[] packet = CreateTestPacket(CommandType.SetEngineSpeed, data);

                // Отправляем пакет
                _serialPort.Write(packet, 0, packet.Length);
                _loggingService.LogInfo("Отправлен пакет установки оборотов", BitConverter.ToString(packet));

                // Ждем ответ
                await Task.Delay(100);

                // Проверяем ответ
                if (_serialPort.BytesToRead > 0)
                {
                    byte[] response = new byte[_serialPort.BytesToRead];
                    _serialPort.Read(response, 0, response.Length);

                    _loggingService.LogInfo("Получен ответ", BitConverter.ToString(response));

                    // Анализируем ответ
                    bool validResponse = response.Length > 3 && response[0] == SYNC_START;

                    if (validResponse)
                    {
                        _loggingService.LogInfo("Тест установки оборотов двигателя ПРОЙДЕН");
                        return true;
                    }
                    else
                    {
                        _loggingService.LogWarning("Тест установки оборотов двигателя НЕ ПРОЙДЕН: некорректный ответ");
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogWarning("Тест установки оборотов двигателя НЕ ПРОЙДЕН: нет ответа");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при тестировании установки оборотов", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Тест работы механизма byte-stuffing
        /// </summary>
        private async Task<bool> TestByteStuffing()
        {
            try
            {
                _loggingService.LogInfo("Тест механизма byte-stuffing...");

                // Создаем данные, содержащие SYNC_START (255) для проверки byte-stuffing
                byte[] data = new byte[] { 0x01, 0x02, SYNC_START, 0x04, 0x05 };

                // Создаем тестовый пакет
                byte[] packet = CreateTestPacket(CommandType.GetParameters, data);

                // Проверяем, что в пакете после SYNC_START следует SYNC_2
                bool hasStuffingBytes = false;
                for (int i = 0; i < packet.Length - 1; i++)
                {
                    if (packet[i] == SYNC_START && packet[i + 1] == SYNC_2)
                    {
                        hasStuffingBytes = true;
                        break;
                    }
                }

                if (hasStuffingBytes)
                {
                    _loggingService.LogInfo("Механизм byte-stuffing работает корректно");

                    // Отправляем пакет для проверки на реальном устройстве
                    _serialPort.Write(packet, 0, packet.Length);
                    _loggingService.LogInfo("Отправлен пакет с byte-stuffing", BitConverter.ToString(packet));

                    // Ждем ответ
                    await Task.Delay(100);

                    // Проверяем ответ
                    if (_serialPort.BytesToRead > 0)
                    {
                        byte[] response = new byte[_serialPort.BytesToRead];
                        _serialPort.Read(response, 0, response.Length);

                        _loggingService.LogInfo("Получен ответ", BitConverter.ToString(response));

                        // Анализируем ответ (достаточно что любой ответ пришел)
                        bool validResponse = response.Length > 3 && response[0] == SYNC_START;

                        if (validResponse)
                        {
                            _loggingService.LogInfo("Тест byte-stuffing ПРОЙДЕН");
                            return true;
                        }
                        else
                        {
                            _loggingService.LogWarning("Тест byte-stuffing НЕ ПРОЙДЕН: некорректный ответ");
                            return false;
                        }
                    }
                    else
                    {
                        _loggingService.LogWarning("Тест byte-stuffing НЕ ПРОЙДЕН: нет ответа");
                        return false;
                    }
                }
                else
                {
                    _loggingService.LogWarning("Механизм byte-stuffing не работает корректно");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при тестировании byte-stuffing", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Создает тестовый пакет протокола
        /// </summary>
        private byte[] CreateTestPacket(CommandType commandType, byte[] data)
        {
            List<byte> packet = new List<byte>();

            // Маркер начала пакета
            packet.Add(SYNC_START);

            // Длина пакета: данные + COM + CS
            byte lDat = (byte)(data.Length + 2);
            packet.Add(lDat);

            // Байт команды
            packet.Add((byte)commandType);

            // Данные с обработкой byte-stuffing
            foreach (byte dataByte in data)
            {
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

            // Вычисляем контрольную сумму (байт CS)
            // Сумма всех байт пакета по модулю 256, взятая с обратным знаком
            // не включая SYNC_START
            int sum = 0;
            for (int i = 1; i < packet.Count; i++)
            {
                sum += packet[i];
            }
            byte checksum = (byte)(256 - (sum % 256));
            packet.Add(checksum);

            return packet.ToArray();
        }
    }
}