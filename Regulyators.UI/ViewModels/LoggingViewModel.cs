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
        /// Конструктор
        /// </summary>
        public LoggingViewModel()
        {
            _loggingService = LoggingService.Instance;

            // Инициализация команд
            ClearLogsCommand = new RelayCommand(ClearLogs);
            
        }

        /// <summary>
        /// Очистка журнала
        /// </summary>
        private void ClearLogs()
        {
            Logs.Clear();
            _loggingService.LogInfo("Журнал очищен");
        }

        
    }
}