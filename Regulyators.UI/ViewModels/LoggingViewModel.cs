using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для отображения журнала системы
    /// </summary>
    public class LoggingViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;

        /// <summary>
        /// Записи журнала
        /// </summary>
        public ObservableCollection<LogEntry> Logs => _loggingService.Logs;

        /// <summary>
        /// Команда очистки журнала
        /// </summary>
        public ICommand ClearLogsCommand { get; }

        /// <summary>
        /// Команда экспорта журнала
        /// </summary>
        public ICommand ExportLogsCommand { get; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public LoggingViewModel()
        {
            _loggingService = LoggingService.Instance;

            // Инициализация команд
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new RelayCommand(ExportLogs);

            // Добавим тестовые записи для демонстрации
            AddSampleLogs();
        }

        /// <summary>
        /// Очистка журнала
        /// </summary>
        private void ClearLogs()
        {
            Logs.Clear();
            _loggingService.LogInfo("Журнал очищен");
        }

        /// <summary>
        /// Экспорт журнала (заглушка)
        /// </summary>
        private void ExportLogs()
        {
            // Здесь будет код экспорта
            _loggingService.LogInfo("Экспорт журнала", "Функция в разработке");
        }

        /// <summary>
        /// Добавление тестовых записей
        /// </summary>
        private void AddSampleLogs()
        {
            _loggingService.LogInfo("Приложение запущено", "Версия 1.0");
            _loggingService.LogInfo("Подключение к оборудованию", "COM1, 38400 бод");
            _loggingService.LogWarning("Низкое давление масла", "1.2 кг/см², порог 1.5 кг/см²");
            _loggingService.LogError("Потеря связи с оборудованием", "Таймаут операции");
            _loggingService.LogInfo("Связь восстановлена");
        }
    }
}