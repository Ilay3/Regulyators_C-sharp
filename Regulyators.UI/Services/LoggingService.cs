using Regulyators.UI.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Regulyators.UI.Services
{
    public class LoggingService
    {
        private static LoggingService _instance;
        private readonly Dispatcher _dispatcher;

        public static LoggingService Instance => _instance ??= new LoggingService();

        public ObservableCollection<LogEntry> Logs { get; }
        public event EventHandler<LogEntry> LogAdded;

        private LoggingService()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            Logs = new ObservableCollection<LogEntry>();
        }

        private void AddLogEntry(string type, string message, string details)
        {
            try
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Type = type,
                    Message = message,
                    Details = details
                };

                _dispatcher.Invoke(() =>
                {
                    Logs.Add(entry);
                    LogAdded?.Invoke(this, entry);
                });
            }
            catch (Exception ex)
            {
                // Резервный механизм логирования
                System.Diagnostics.Debug.WriteLine($"Logging error: {ex.Message}");
            }
        }

        public void LogInfo(string message, string details = "")
        {
            AddLogEntry("Информация", message, details);
        }

        public void LogWarning(string message, string details = "")
        {
            AddLogEntry("Предупреждение", message, details);
        }

        public void LogError(string message, string details = "")
        {
            AddLogEntry("Ошибка", message, details);
        }
    }
}