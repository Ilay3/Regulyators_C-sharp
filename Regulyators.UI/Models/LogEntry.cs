using System;

namespace Regulyators.UI.Models
{
    /// <summary>
    /// Запись журнала системы
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Время события
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Тип события (Информация, Предупреждение, Ошибка)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Краткое сообщение
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Подробное описание
        /// </summary>
        public string Details { get; set; }
    }
}