using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для отображения журнала системы с улучшенной потокобезопасностью
    /// </summary>
    public class LoggingViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;
        private readonly Dispatcher _dispatcher;
        private ObservableCollection<LogEntry> _logs;

        /// <summary>
        /// Записи журнала с потокобезопасным доступом
        /// </summary>
        public ObservableCollection<LogEntry> Logs
        {
            get => _logs;
            private set => SetProperty(ref _logs, value);
        }

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
            _dispatcher = Application.Current.Dispatcher;

            // Инициализация коллекции
            _logs = new ObservableCollection<LogEntry>();

            // Копируем записи из сервиса
            InitializeLogs();

            // Подписываемся на события добавления логов
            _loggingService.LogAdded += OnLogAdded;

            // Инициализация команд
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new RelayCommand(ExportLogs);
        }

        /// <summary>
        /// Инициализация списка логов
        /// </summary>
        private void InitializeLogs()
        {
            if (_dispatcher.CheckAccess())
            {
                // Если мы в UI потоке, просто копируем
                foreach (var entry in _loggingService.Logs)
                {
                    _logs.Add(entry);
                }
            }
            else
            {
                // Иначе переключаемся в UI поток
                _dispatcher.Invoke(() =>
                {
                    foreach (var entry in _loggingService.Logs)
                    {
                        _logs.Add(entry);
                    }
                });
            }
        }

        /// <summary>
        /// Обработчик события добавления лога
        /// </summary>
        private void OnLogAdded(object sender, LogEntry entry)
        {
            if (_dispatcher.CheckAccess())
            {
                // В UI потоке
                _logs.Insert(0, entry);  // Вставляем новые записи в начало

                // Ограничиваем максимальное количество элементов для производительности
                if (_logs.Count > 1000)
                {
                    _logs.RemoveAt(_logs.Count - 1);
                }
            }
            else
            {
                // Переключаемся в UI поток
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    _logs.Insert(0, entry);

                    if (_logs.Count > 1000)
                    {
                        _logs.RemoveAt(_logs.Count - 1);
                    }
                }));
            }
        }

        /// <summary>
        /// Очистка журнала
        /// </summary>
        private void ClearLogs()
        {
            if (_dispatcher.CheckAccess())
            {
                _logs.Clear();
            }
            else
            {
                _dispatcher.Invoke(() => _logs.Clear());
            }

            _loggingService.LogInfo("Журнал очищен");
        }

        /// <summary>
        /// Экспорт журнала
        /// </summary>
        private void ExportLogs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Экспорт журнала"
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                bool success = _loggingService.ExportToCsv(filePath);

                if (success)
                {
                    _loggingService.LogInfo("Журнал успешно экспортирован", filePath);
                }
                else
                {
                    _loggingService.LogError("Ошибка при экспорте журнала", filePath);
                }
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        protected override void ReleaseMangedResources()
        {
            base.ReleaseMangedResources();

            // Отписываемся от события
            _loggingService.LogAdded -= OnLogAdded;
        }
    }
}