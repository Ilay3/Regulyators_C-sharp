using System;
using System.Collections.ObjectModel;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Сервис для ведения журнала системы
    /// </summary>
    public class LoggingService
    {
        private static LoggingService _instance;

        /// <summary>
        /// Событие добавления новой записи в журнал
        /// </summary>
        public event EventHandler<LogEntry> LogAdded;

        /// <summary>
        /// Журнал системы
        /// </summary>
        public ObservableCollection<LogEntry> Logs { get; }

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static LoggingService Instance => _instance ??= new LoggingService();

        private LoggingService()
        {
            Logs = new ObservableCollection<LogEntry>();
        }

        /// <summary>
        /// Добавление информационного сообщения
        /// </summary>
        public void LogInfo(string message, string details = "")
        {
            AddLogEntry("Информация", message, details);
        }

        /// <summary>
        /// Добавление предупреждения
        /// </summary>
        public void LogWarning(string message, string details = "")
        {
            AddLogEntry("Предупреждение", message, details);
        }

        /// <summary>
        /// Добавление сообщения об ошибке
        /// </summary>
        public void LogError(string message, string details = "")
        {
            AddLogEntry("Ошибка", message, details);
        }

        private void AddLogEntry(string type, string message, string details)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Message = message,
                Details = details
            };

            Logs.Add(entry);
            LogAdded?.Invoke(this, entry);
        }

        // Добавьте в класс LoggingService
        /// <summary>
        /// Получение последних записей журнала
        /// </summary>
        /// <param name="count">Количество записей</param>
        public ObservableCollection<LogEntry> GetLastLogs(int count)
        {
            var result = new ObservableCollection<LogEntry>();
            int startIndex = Math.Max(0, Logs.Count - count);

            for (int i = startIndex; i < Logs.Count; i++)
            {
                result.Add(Logs[i]);
            }

            return result;
        }
    }
}