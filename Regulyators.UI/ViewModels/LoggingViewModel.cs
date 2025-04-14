using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для отображения журнала системы с улучшенной потокобезопасностью и фильтрацией
    /// </summary>
    public class LoggingViewModel : ViewModelBase
    {
        private readonly LoggingService _loggingService;
        private readonly Dispatcher _dispatcher;
        private ObservableCollection<LogEntry> _logs;
        private ObservableCollection<LogEntry> _filteredLogs;
        private string _selectedLogType;
        private string _searchText;
        private string _statusMessage;
        private readonly CollectionViewSource _logsViewSource;

        /// <summary>
        /// Записи журнала с потокобезопасным доступом
        /// </summary>
        public ObservableCollection<LogEntry> Logs
        {
            get => _logs;
            private set => SetProperty(ref _logs, value);
        }

        /// <summary>
        /// Отфильтрованные записи журнала
        /// </summary>
        public ObservableCollection<LogEntry> FilteredLogs
        {
            get => _filteredLogs;
            private set => SetProperty(ref _filteredLogs, value);
        }

        /// <summary>
        /// Выбранный тип лога для фильтрации
        /// </summary>
        public string SelectedLogType
        {
            get => _selectedLogType;
            set
            {
                if (SetProperty(ref _selectedLogType, value))
                {
                    ApplyFilters();
                }
            }
        }

        /// <summary>
        /// Текст поиска
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// Статусное сообщение
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Доступные типы логов для фильтрации
        /// </summary>
        public ObservableCollection<string> LogTypes { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Команда очистки журнала
        /// </summary>
        public ICommand ClearLogsCommand { get; }

        /// <summary>
        /// Команда экспорта журнала
        /// </summary>
        public ICommand ExportLogsCommand { get; }

        /// <summary>
        /// Команда применения поиска
        /// </summary>
        public ICommand ApplySearchCommand { get; }

        /// <summary>
        /// Команда сброса фильтров
        /// </summary>
        public ICommand ResetFiltersCommand { get; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public LoggingViewModel()
        {
            _loggingService = LoggingService.Instance;
            _dispatcher = Application.Current.Dispatcher;

            // Инициализация коллекций
            _logs = new ObservableCollection<LogEntry>();
            _filteredLogs = new ObservableCollection<LogEntry>();
            _logsViewSource = new CollectionViewSource { Source = _logs };

            // Инициализация параметров фильтрации
            _selectedLogType = "Все типы";
            _searchText = string.Empty;
            UpdateStatusMessage();

            // Инициализация типов логов
            LogTypes.Add("Все типы");
            LogTypes.Add("Информация");
            LogTypes.Add("Предупреждение");
            LogTypes.Add("Ошибка");
            LogTypes.Add("Отладка");

            // Копируем записи из сервиса
            InitializeLogs();

            // Подписываемся на события добавления логов
            _loggingService.LogAdded += OnLogAdded;

            // Инициализация команд
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new RelayCommand(ExportLogs);
            ApplySearchCommand = new RelayCommand(ApplyFilters);
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            // Применяем начальные фильтры
            ApplyFilters();
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

            ApplyFilters();
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

                // Применяем фильтры
                ApplyFilters();
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

                    ApplyFilters();
                }));
            }
        }

        /// <summary>
        /// Применение фильтров к журналу
        /// </summary>
        private void ApplyFilters()
        {
            try
            {
                // Получаем исходные данные
                var filteredLogs = new List<LogEntry>(_logs);

                // Применяем фильтр по типу
                if (!string.IsNullOrEmpty(_selectedLogType) && _selectedLogType != "Все типы")
                {
                    filteredLogs = filteredLogs.Where(log => log.Type.Equals(_selectedLogType, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                // Применяем фильтр по тексту поиска
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    string searchTerm = _searchText.ToLower();
                    filteredLogs = filteredLogs.Where(log =>
                        log.Message.ToLower().Contains(searchTerm) ||
                        log.Details.ToLower().Contains(searchTerm)).ToList();
                }

                // Обновляем отфильтрованную коллекцию
                FilteredLogs = new ObservableCollection<LogEntry>(filteredLogs);

                // Обновляем статусное сообщение
                UpdateStatusMessage();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка при применении фильтров", ex.Message);
                StatusMessage = "Ошибка применения фильтров: " + ex.Message;
            }
        }

        /// <summary>
        /// Сброс фильтров
        /// </summary>
        private void ResetFilters()
        {
            _selectedLogType = "Все типы";
            OnPropertyChanged(nameof(SelectedLogType));

            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            ApplyFilters();

            StatusMessage = "Фильтры сброшены";
        }

        /// <summary>
        /// Обновление статусного сообщения
        /// </summary>
        private void UpdateStatusMessage()
        {
            StringBuilder statusBuilder = new StringBuilder($"Записей в журнале: {Logs.Count}");

            if (FilteredLogs.Count != Logs.Count)
            {
                statusBuilder.Append($" | Отфильтровано: {FilteredLogs.Count}");
            }

            if (!string.IsNullOrEmpty(_selectedLogType) && _selectedLogType != "Все типы")
            {
                statusBuilder.Append($" | Тип: {_selectedLogType}");
            }

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                statusBuilder.Append($" | Поиск: '{_searchText}'");
            }

            StatusMessage = statusBuilder.ToString();
        }

        /// <summary>
        /// Очистка журнала
        /// </summary>
        private void ClearLogs()
        {
            if (_dispatcher.CheckAccess())
            {
                _logs.Clear();
                FilteredLogs.Clear();
                UpdateStatusMessage();
            }
            else
            {
                _dispatcher.Invoke(() =>
                {
                    _logs.Clear();
                    FilteredLogs.Clear();
                    UpdateStatusMessage();
                });
            }

            _loggingService.LogInfo("Журнал очищен");
            StatusMessage = "Журнал очищен";
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