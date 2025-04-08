using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Расширенный сервис для ведения журнала системы с поддержкой ротации
    /// </summary>
    public class LoggingService
    {
        private static LoggingService _instance;
        private readonly Timer _autoSaveTimer;
        private readonly object _lockObj = new object();
        private CancellationTokenSource _cancellationTokenSource;

        // Настройки логирования
        private int _maxLogEntries = 1000;   // Максимальное число записей в памяти
        private int _autoSaveInterval = 30;  // Интервал автосохранения в минутах
        private bool _saveToFile = true;     // Сохранять ли лог в файл
        private string _logFolder;           // Папка для хранения логов
        private bool _enableDetailedLogging = false; // Расширенное логирование
        private bool _rotateLogFiles = true; // Ротация файлов логов
        private int _maxLogFileSize = 5;     // Максимальный размер файла лога в МБ
        private int _maxLogFiles = 10;       // Максимальное количество файлов логов

        /// <summary>
        /// Событие добавления новой записи в журнал
        /// </summary>
        public event EventHandler<LogEntry> LogAdded;

        /// <summary>
        /// Журнал системы
        /// </summary>
        public ObservableCollection<LogEntry> Logs { get; }

        /// <summary>
        /// Текущий файл лога
        /// </summary>
        public string CurrentLogFile { get; private set; }

        /// <summary>
        /// Максимальное число записей в памяти
        /// </summary>
        public int MaxLogEntries
        {
            get => _maxLogEntries;
            set
            {
                _maxLogEntries = value;
                EnsureLogSizeLimit();
            }
        }

        /// <summary>
        /// Интервал автосохранения в минутах
        /// </summary>
        public int AutoSaveInterval
        {
            get => _autoSaveInterval;
            set
            {
                _autoSaveInterval = value;
                RestartAutoSaveTimer();
            }
        }

        /// <summary>
        /// Сохранять ли лог в файл
        /// </summary>
        public bool SaveToFile
        {
            get => _saveToFile;
            set => _saveToFile = value;
        }

        /// <summary>
        /// Папка для хранения логов
        /// </summary>
        public string LogFolder
        {
            get => _logFolder;
            set
            {
                _logFolder = value;
                EnsureLogDirectoryExists();
            }
        }

        /// <summary>
        /// Расширенное логирование
        /// </summary>
        public bool EnableDetailedLogging
        {
            get => _enableDetailedLogging;
            set => _enableDetailedLogging = value;
        }

        /// <summary>
        /// Ротация файлов логов
        /// </summary>
        public bool RotateLogFiles
        {
            get => _rotateLogFiles;
            set => _rotateLogFiles = value;
        }

        /// <summary>
        /// Максимальный размер файла лога в МБ
        /// </summary>
        public int MaxLogFileSize
        {
            get => _maxLogFileSize;
            set => _maxLogFileSize = value;
        }

        /// <summary>
        /// Максимальное количество файлов логов
        /// </summary>
        public int MaxLogFiles
        {
            get => _maxLogFiles;
            set => _maxLogFiles = value;
        }

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static LoggingService Instance => _instance ??= new LoggingService();

        private LoggingService()
        {
            Logs = new ObservableCollection<LogEntry>();
            _cancellationTokenSource = new CancellationTokenSource();

            // Инициализация папки логов
            _logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Regulyators", "Logs");

            EnsureLogDirectoryExists();
            InitializeCurrentLogFile();

            // Запуск таймера автосохранения
            _autoSaveTimer = new Timer(AutoSaveCallback, null, TimeSpan.FromMinutes(_autoSaveInterval), TimeSpan.FromMinutes(_autoSaveInterval));

            LogInfo("Сервис логирования инициализирован");
        }

        /// <summary>
        /// Инициализация текущего файла лога
        /// </summary>
        private void InitializeCurrentLogFile()
        {
            CurrentLogFile = Path.Combine(_logFolder, $"log_{DateTime.Now:yyyy-MM-dd}.txt");

            // Проверяем, нужно ли создать новый файл лога из-за размера
            if (_rotateLogFiles && File.Exists(CurrentLogFile))
            {
                FileInfo fileInfo = new FileInfo(CurrentLogFile);
                if (fileInfo.Length > _maxLogFileSize * 1024 * 1024)
                {
                    // Создаем новый файл с временной меткой
                    CurrentLogFile = Path.Combine(_logFolder, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                    // Удаляем старые файлы, если их слишком много
                    CleanupOldLogFiles();
                }
            }
        }

        /// <summary>
        /// Обеспечение существования директории для логов
        /// </summary>
        private void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(_logFolder))
            {
                Directory.CreateDirectory(_logFolder);
            }
        }

        /// <summary>
        /// Очистка старых файлов логов
        /// </summary>
        private void CleanupOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logFolder, "log_*.txt")
                                       .OrderByDescending(f => File.GetLastWriteTime(f))
                                       .Skip(_maxLogFiles)
                                       .ToList();

                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        // Игнорируем ошибки удаления, но записываем их в текущий лог
                        LogWarning($"Не удалось удалить старый файл лога: {file}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning("Ошибка при очистке старых файлов логов", ex.Message);
            }
        }

        /// <summary>
        /// Ротация файла лога по размеру
        /// </summary>
        private void CheckLogFileSize()
        {
            if (!_rotateLogFiles || !File.Exists(CurrentLogFile))
                return;

            try
            {
                FileInfo fileInfo = new FileInfo(CurrentLogFile);
                if (fileInfo.Length > _maxLogFileSize * 1024 * 1024)
                {
                    // Создаем новый файл с временной меткой
                    CurrentLogFile = Path.Combine(_logFolder, $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                    LogInfo($"Создан новый файл лога из-за превышения размера: {CurrentLogFile}");
                    // Удаляем старые файлы, если их слишком много
                    CleanupOldLogFiles();
                }
            }
            catch (Exception ex)
            {
                LogWarning("Ошибка при проверке размера файла лога", ex.Message);
            }
        }

        /// <summary>
        /// Обратный вызов для автосохранения
        /// </summary>
        private void AutoSaveCallback(object state)
        {
            // Проверяем возможность сохранения лога
            if (_saveToFile && Logs.Count > 0)
            {
                try
                {
                    SaveLogToFile();
                }
                catch (Exception ex)
                {
                    // Просто записываем ошибку в журнал, не вызывая рекурсию через LogError
                    AddLogEntry("Ошибка", "Ошибка автосохранения лога", ex.Message, false);
                }
            }
        }

        /// <summary>
        /// Перезапуск таймера автосохранения
        /// </summary>
        private void RestartAutoSaveTimer()
        {
            _autoSaveTimer.Change(
                TimeSpan.FromMinutes(_autoSaveInterval),
                TimeSpan.FromMinutes(_autoSaveInterval));
        }

        /// <summary>
        /// Сохранение лога в файл
        /// </summary>
        public bool SaveLogToFile(string customFilePath = null)
        {
            if (!_saveToFile && string.IsNullOrEmpty(customFilePath))
                return false;

            string filePath = customFilePath ?? CurrentLogFile;

            try
            {
                EnsureLogDirectoryExists();

                // Проверяем необходимость ротации файла
                if (string.IsNullOrEmpty(customFilePath))
                {
                    CheckLogFileSize();
                    filePath = CurrentLogFile;
                }

                // Используем асинхронное добавление в файл
                StringBuilder logContent = new StringBuilder();

                // Берем локальную копию логов, чтобы избежать проблем с многопоточностью
                var logsToSave = Logs.ToList();

                foreach (var entry in logsToSave)
                {
                    logContent.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}|{entry.Type}|{entry.Message}|{entry.Details}");
                }

                // Проверяем, существует ли файл
                bool fileExists = File.Exists(filePath);

                // Если файл новый, создаем его с заголовком
                if (!fileExists)
                {
                    File.WriteAllText(filePath, "Дата и время|Тип|Сообщение|Подробности\r\n");
                }

                // Дописываем данные в файл
                File.AppendAllText(filePath, logContent.ToString());

                if (_enableDetailedLogging)
                {
                    AddLogEntry("Информация", "Лог сохранен в файл", filePath, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                // Записываем ошибку в журнал, но не пишем в файл, чтобы избежать рекурсии
                AddLogEntry("Ошибка", "Ошибка сохранения лога в файл", ex.Message, false);
                return false;
            }
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

        /// <summary>
        /// Добавление записи в журнал
        /// </summary>
        /// <param name="type">Тип сообщения</param>
        /// <param name="message">Сообщение</param>
        /// <param name="details">Подробности</param>
        /// <param name="saveToFile">Сохранять ли в файл немедленно</param>
        private void AddLogEntry(string type, string message, string details, bool saveToFile = true)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Message = message,
                Details = details
            };

            try
            {
                // Добавление в UI поток более надёжным способом
                if (Application.Current?.Dispatcher != null)
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // Уже в UI потоке
                        lock (_lockObj)
                        {
                            Logs.Add(entry);
                            while (Logs.Count > _maxLogEntries)
                            {
                                Logs.RemoveAt(Logs.Count - 1);
                            }
                        }
                    }
                    else
                    {
                        // Не в UI потоке - используем BeginInvoke для асинхронного вызова
                        // чтобы избежать возможной блокировки
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            lock (_lockObj)
                            {
                                Logs.Add(entry);
                                while (Logs.Count > _maxLogEntries)
                                {
                                    Logs.RemoveAt(Logs.Count - 1);
                                }
                            }
                        }));
                    }
                }
                else
                {
                    // В случае если Application.Current недоступен, 
                    // записываем только в файл, но не в коллекцию
                    if (saveToFile && _saveToFile)
                    {
                        Task.Run(() => SaveLogToFileInternal(entry));
                    }
                    return; // Выходим, чтобы не вызывать события
                }

                // Вызываем событие только для потокобезопасных подписчиков
                // или если находимся в UI потоке
                if (Application.Current?.Dispatcher?.CheckAccess() == true)
                {
                    LogAdded?.Invoke(this, entry);
                }

                // Сохранение в файл
                if (saveToFile && _saveToFile && type == "Ошибка")
                {
                    Task.Run(() => SaveLogToFileInternal(entry));
                }
            }
            catch (Exception ex)
            {
                // Запись в файл при сбое 
                try
                {
                    File.AppendAllText(
                        Path.Combine(_logFolder, "critical_errors.log"),
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|Сбой логирования|{ex.Message}\r\n"
                    );
                }
                catch { /* В крайнем случае просто игнорируем */ }
            }
        }

        // Специальный метод для записи одной записи в файл
        private void SaveLogToFileInternal(LogEntry entry)
        {
            try
            {
                EnsureLogDirectoryExists();
                File.AppendAllText(
                    CurrentLogFile,
                    $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}|{entry.Type}|{entry.Message}|{entry.Details}\r\n"
                );
            }
            catch { /* Игнорируем ошибки при записи в файл */ }
        }


        /// <summary>
        /// Проверка и ограничение размера журнала
        /// </summary>
        private void EnsureLogSizeLimit()
        {
            lock (_lockObj)
            {
                while (Logs.Count > _maxLogEntries)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            }
        }

        /// <summary>
        /// Получение последних записей журнала
        /// </summary>
        /// <param name="count">Количество записей</param>
        public ReadOnlyCollection<LogEntry> GetLastLogs(int count)
        {
            lock (_lockObj)
            {
                var result = new List<LogEntry>();
                int startIndex = Math.Max(0, Logs.Count - count);

                for (int i = startIndex; i < Logs.Count; i++)
                {
                    result.Add(Logs[i]);
                }

                return new ReadOnlyCollection<LogEntry>(result);
            }
        }

        /// <summary>
        /// Очистка всех записей журнала
        /// </summary>
        public void ClearLogs()
        {
            lock (_lockObj)
            {
                Logs.Clear();
            }

            LogInfo("Журнал очищен");
        }

        /// <summary>
        /// Экспорт журнала в файл CSV
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <returns>Успешность операции</returns>
        public bool ExportToCsv(string filePath)
        {
            try
            {
                lock (_lockObj)
                {
                    StringBuilder csv = new StringBuilder();

                    // Добавляем заголовок
                    csv.AppendLine("Дата и время;Тип;Сообщение;Подробности");

                    // Добавляем записи
                    foreach (var entry in Logs)
                    {
                        // Экранируем значения, содержащие точку с запятой
                        string escapedMessage = entry.Message?.Replace(";", "\\;") ?? string.Empty;
                        string escapedDetails = entry.Details?.Replace(";", "\\;") ?? string.Empty;

                        csv.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff};{entry.Type};{escapedMessage};{escapedDetails}");
                    }

                    // Записываем в файл
                    File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
                }

                LogInfo("Журнал экспортирован в CSV", filePath);
                return true;
            }
            catch (Exception ex)
            {
                LogError("Ошибка экспорта журнала в CSV", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Фильтрация журнала по типу сообщений
        /// </summary>
        /// <param name="type">Тип сообщения для фильтрации</param>
        /// <returns>Коллекция отфильтрованных записей</returns>
        public ReadOnlyCollection<LogEntry> FilterByType(string type)
        {
            lock (_lockObj)
            {
                return new ReadOnlyCollection<LogEntry>(
                    Logs.Where(entry => entry.Type == type).ToList());
            }
        }

        /// <summary>
        /// Фильтрация журнала по временному интервалу
        /// </summary>
        /// <param name="startTime">Начало интервала</param>
        /// <param name="endTime">Конец интервала</param>
        /// <returns>Коллекция отфильтрованных записей</returns>
        public ReadOnlyCollection<LogEntry> FilterByTimeRange(DateTime startTime, DateTime endTime)
        {
            lock (_lockObj)
            {
                return new ReadOnlyCollection<LogEntry>(
                    Logs.Where(entry => entry.Timestamp >= startTime && entry.Timestamp <= endTime).ToList());
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            // Сохраняем лог перед выходом
            SaveLogToFile();

            // Останавливаем таймер автосохранения
            _autoSaveTimer?.Dispose();

            // Отменяем все асинхронные операции
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            LogInfo("Сервис логирования остановлен");
        }
    }
}