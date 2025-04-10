using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace Regulyators.UI.Models
{
    /// <summary>
    /// Константы протокола ЭРЧМ30ТЗ согласно техническим требованиям
    /// </summary>
    public static class ERCHM30TZProtocol
    {
        // Основные константы протокола
        public const byte SYNC_START = 255;   // Байт синхронизации, признак начала пакета (0xFF)
        public const byte SYNC_2 = 254;       // Байт для byte-staffing (0xFE)

        // Стандартные настройки связи
        public const int BAUD_RATE = 9600;     // Скорость передачи (бод)
        public const int DATA_BITS = 8;        // Биты данных
        public const StopBits STOP_BITS = StopBits.Two;  // Стоповые биты (2)
        public const Parity PARITY = Parity.Odd;         // Контроль четности (нечетный)

        // Индексы полей в запросе от MASTER к SLAVE (согласно таблице 1)
        public static class RequestFieldIndices
        {
            public const int F_Z_H = 0;   // Задание частоты вращения (старший байт)
            public const int F_Z_L = 1;   // Задание частоты вращения (младший байт)
            public const int POWER = 2;   // Признак поездного режима
            public const int PUSK = 3;    // Признак запуска/стопа дизеля
            public const int RESERVE1 = 4; // Резерв
            public const int RESERVE2 = 5; // Резерв
        }

        // Индексы полей в ответе от SLAVE к MASTER (согласно таблице 2)
        public static class ResponseFieldIndices
        {
            public const int F_DIZ_H = 0;   // Частота вращения дизеля (старший байт)
            public const int F_DIZ_L = 1;   // Частота вращения дизеля (младший байт)
            public const int H_H = 2;       // Рейка ТНВД (старший байт)
            public const int H_L = 3;       // Рейка ТНВД (младший байт)
            public const int T_H = 4;       // Температура масла (старший байт)
            public const int T_L = 5;       // Температура масла (младший байт)
            public const int A_ND = 6;      // Ограничение по наддуву
            public const int F_T_H = 7;     // Частота вращения ТК (старший байт)
            public const int F_T_L = 8;     // Частота вращения ТК (младший байт)
            public const int P_ND_H = 9;    // Давление наддува (старший байт)
            public const int P_ND_L = 10;   // Давление наддува (младший байт)
            public const int STOP = 11;     // Признак стопа
            public const int STOP_MAX = 12; // Останов по превышению оборотов (разнос)
            public const int A_M = 13;      // Защита по маслу дизеля
            public const int P_M_H = 14;    // Давление масла (старший байт)
            public const int P_M_L = 15;    // Давление масла (младший байт)
            public const int F_Z_H = 16;    // Задание частоты вращения (старший байт)
            public const int F_Z_L = 17;    // Задание частоты вращения (младший байт)
        }

        // Значения статусов в ответных данных
        public static class StatusValues
        {
            // Статус ограничения по наддуву (A_ND)
            public const byte LIMIT_BOOST_ACTIVE = 255;    // Наступило ограничение по наддуву
            public const byte LIMIT_BOOST_NORMAL = 0;      // Нет ограничения по наддуву
            public const byte LIMIT_BOOST_SENSOR_ERROR = 238; // Отказ датчика давления наддува

            // Статус стопа (STOP)
            public const byte WORK_CONFIRM = 253;    // Подтверждение команды РАБОТА
            public const byte STOP_CONFIRM = 0;      // Подтверждение команды СТОП

            // Статус останова по превышению оборотов (STOP_MAX)
            public const byte OVERSPEED_ACTIVE = 255;    // Разнос
            public const byte OVERSPEED_NORMAL = 0;      // Нет разноса

            // Биты статуса защиты по маслу (A_M)
            public const byte BOOST_SENSOR_ERROR = 0x08;    // 3 бит = 1 - отказ датчика давления наддува
            public const byte POSITION_SENSOR_ERROR = 0x10;  // 4 бит = 1 - отказ датчика положения
            public const byte OIL_PRESSURE_SENSOR_ERROR = 0x20; // 5 бит = 1 - отказ датчика давления масла
            public const byte OIL_ALERT = 0x40;               // 6 бит = 1 - сработала сигнализация по маслу
            public const byte OIL_PROTECTION = 0x80;          // 7 бит = 1 - сработала защита по маслу
        }

        /// <summary>
        /// Преобразует два байта в ushort (младший, старший)
        /// </summary>
        public static ushort BytesToUInt16(byte lowByte, byte highByte)
        {
            return (ushort)((highByte << 8) | lowByte);
        }

        /// <summary>
        /// Преобразует ushort в два байта (младший, старший)
        /// </summary>
        public static (byte LowByte, byte HighByte) UInt16ToBytes(ushort value)
        {
            return ((byte)(value & 0xFF), (byte)(value >> 8));
        }

        /// <summary>
        /// Рассчитывает контрольную сумму пакета
        /// Контрольная сумма (байт CS) - сумма значений байтов пакета по модулю 256, 
        /// взятая с обратным знаком. Значения байтов SYNC_START, SYNC_2 для вычисления 
        /// контрольной суммы не используется.
        /// </summary>
        public static byte CalculateChecksum(List<byte> packet, int startIndex = 1)
        {
            int sum = 0;
            for (int i = startIndex; i < packet.Count; i++)
            {
                sum += packet[i];
            }
            return (byte)(256 - (sum % 256));
        }
    }
}