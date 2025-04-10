using System;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Regulyators.UI.Models;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Сервис для управления настройками системы
    /// </summary>
    public class SettingsService
    {
        private static SettingsService _instance;
        private readonly LoggingService _loggingService;
        private readonly string _defaultConfigPath;
        private readonly string _backupConfigPath;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Настройки COM-порта
        /// </summary>
        public ComPortSettings ComPortSettings { get; private set; }

        /// <summary>
        /// Пороги срабатывания защит
        /// </summary>
        public ProtectionThresholds ProtectionThresholds { get; private set; }

        /// <summary>
        /// Общие настройки приложения
        /// </summary>
        public ApplicationSettings ApplicationSettings { get; private set; }

        /// <summary>
        /// Путь к файлу конфигурации по умолчанию
        /// </summary>
        public string DefaultConfigFilePath => _defaultConfigPath;

        /// <summary>
        /// Путь к резервной копии конфигурации
        /// </summary>
        public string BackupConfigFilePath => _backupConfigPath;

        /// <summary>
        /// Событие изменения настроек
        /// </summary>
        public event EventHandler<SettingsChangedEventArgs> SettingsChanged;

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static SettingsService Instance => _instance ??= new SettingsService();

        private SettingsService()
        {
            _loggingService = LoggingService.Instance;

            // Настройки JSON сериализации
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Для поддержки кириллицы
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };

            // Пути к файлам настроек
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Regulyators");

            _defaultConfigPath = Path.Combine(appDataFolder, "config.json");
            _backupConfigPath = Path.Combine(appDataFolder, "config.backup.json");

            // Обеспечиваем существование директории
            Directory.CreateDirectory(appDataFolder);

            // Инициализация настроек по умолчанию
            ComPortSettings = new ComPortSettings();
            ProtectionThresholds = new ProtectionThresholds();
            ApplicationSettings = new ApplicationSettings();

            // Попытка загрузки настроек из файла
            if (!LoadSettings(_defaultConfigPath))
            {
                // Если не удалось загрузить основной файл, пробуем резервную копию
                if (File.Exists(_backupConfigPath))
                {
                    _loggingService.LogWarning("Основной файл настроек поврежден, загрузка из резервной копии", _backupConfigPath);
                    LoadSettings(_backupConfigPath);
                }
            }

            _loggingService.LogInfo("Сервис настроек инициализирован");
        }

        /// <summary>
        /// Обновление настроек COM-порта
        /// </summary>
        public void UpdateComPortSettings(ComPortSettings settings)
        {
            if (settings == null)
                return;

            ComPortSettings = settings.Clone();

            // Уведомление об изменении настроек
            OnSettingsChanged(new SettingsChangedEventArgs
            {
                SettingsType = SettingsType.ComPort
            });

            // Автоматическое сохранение настроек
            SaveSettings();

            _loggingService.LogInfo("Настройки COM-порта обновлены", $"Порт: {settings.PortName}, Скорость: {settings.BaudRate}");
        }

        /// <summary>
        /// Обновление порогов защит
        /// </summary>
        public void UpdateProtectionThresholds(ProtectionThresholds thresholds)
        {
            if (thresholds == null)
                return;

            ProtectionThresholds = thresholds.Clone();

            // Уведомление об изменении настроек
            OnSettingsChanged(new SettingsChangedEventArgs
            {
                SettingsType = SettingsType.Protection
            });

            // Автоматическое сохранение настроек
            SaveSettings();

            _loggingService.LogInfo("Пороги защит обновлены",
                $"Давление масла: {thresholds.OilPressureMinThreshold:F2}, " +
                $"Обороты: {thresholds.EngineSpeedMaxThreshold:F0}, " +
                $"Давление наддува: {thresholds.BoostPressureMaxThreshold:F2}, " +
                $"Температура масла: {thresholds.OilTemperatureMaxThreshold:F1}");
        }

        /// <summary>
        /// Обновление общих настроек приложения
        /// </summary>
        public void UpdateApplicationSettings(ApplicationSettings settings)
        {
            if (settings == null)
                return;

            ApplicationSettings = settings.Clone();

            // Уведомление об изменении настроек
            OnSettingsChanged(new SettingsChangedEventArgs
            {
                SettingsType = SettingsType.Application
            });

            // Автоматическое сохранение настроек
            SaveSettings();

            _loggingService.LogInfo("Общие настройки приложения обновлены");
        }

        /// <summary>
        /// Сохранение настроек в файл
        /// </summary>
        public void SaveSettings(string filePath = null)
        {
            filePath ??= _defaultConfigPath;

            try
            {
                _loggingService.LogInfo("Сохранение настроек в файл", filePath);

                // Создаем объект с настройками
                var settings = new
                {
                    ComPort = ComPortSettings,
                    Protection = ProtectionThresholds,
                    Application = ApplicationSettings,
                    LastSaved = DateTime.Now
                };

                // Преобразуем в JSON
                string json = JsonSerializer.Serialize(settings, _jsonOptions);

                // Если мы сохраняем в основной файл, сначала создаем резервную копию
                if (filePath == _defaultConfigPath && File.Exists(_defaultConfigPath))
                {
                    File.Copy(_defaultConfigPath, _backupConfigPath, true);
                }

                // Сохраняем в файл
                File.WriteAllText(filePath, json);

                _loggingService.LogInfo("Настройки успешно сохранены", filePath);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка сохранения настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                throw new ApplicationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Загрузка настроек из файла
        /// </summary>
        public bool LoadSettings(string filePath = null)
        {
            filePath ??= _defaultConfigPath;

            try
            {
                if (!File.Exists(filePath))
                {
                    _loggingService.LogInfo("Файл настроек не найден, используются значения по умолчанию", filePath);
                    return false;
                }

                _loggingService.LogInfo("Загрузка настроек из файла", filePath);

                // Читаем JSON из файла
                string json = File.ReadAllText(filePath);

                // Десериализуем настройки
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    bool hasChanges = false;

                    // Загрузка настроек COM-порта
                    if (document.RootElement.TryGetProperty("ComPort", out JsonElement comPortElement))
                    {
                        var comPortSettings = JsonSerializer.Deserialize<ComPortSettings>(comPortElement.GetRawText(), _jsonOptions);
                        if (comPortSettings != null)
                        {
                            ComPortSettings = comPortSettings;
                            hasChanges = true;
                            _loggingService.LogInfo("Загружены настройки COM-порта", $"Порт: {comPortSettings.PortName}");
                        }
                    }

                    // Загрузка порогов защит
                    if (document.RootElement.TryGetProperty("Protection", out JsonElement protectionElement))
                    {
                        var protectionThresholds = JsonSerializer.Deserialize<ProtectionThresholds>(protectionElement.GetRawText(), _jsonOptions);
                        if (protectionThresholds != null)
                        {
                            ProtectionThresholds = protectionThresholds;
                            hasChanges = true;
                            _loggingService.LogInfo("Загружены пороги защиты");
                        }
                    }

                    // Загрузка общих настроек приложения
                    if (document.RootElement.TryGetProperty("Application", out JsonElement applicationElement))
                    {
                        var applicationSettings = JsonSerializer.Deserialize<ApplicationSettings>(applicationElement.GetRawText(), _jsonOptions);
                        if (applicationSettings != null)
                        {
                            ApplicationSettings = applicationSettings;
                            hasChanges = true;
                            _loggingService.LogInfo("Загружены общие настройки приложения");
                        }
                    }

                    // Уведомление об изменении всех настроек
                    if (hasChanges)
                    {
                        OnSettingsChanged(new SettingsChangedEventArgs
                        {
                            SettingsType = SettingsType.All
                        });
                    }
                }

                _loggingService.LogInfo("Настройки успешно загружены", filePath);
                return true;
            }
            catch (JsonException ex)
            {
                string errorMessage = $"Ошибка разбора JSON в файле настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка загрузки настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Сброс настроек по умолчанию
        /// </summary>
        public void ResetToDefaults()
        {
            try
            {
                _loggingService.LogInfo("Сброс настроек по умолчанию");

                ComPortSettings = new ComPortSettings();
                ProtectionThresholds = new ProtectionThresholds();
                ApplicationSettings = new ApplicationSettings();

                // Уведомление об изменении всех настроек
                OnSettingsChanged(new SettingsChangedEventArgs
                {
                    SettingsType = SettingsType.All
                });

                // Автоматическое сохранение настроек
                SaveSettings();

                _loggingService.LogInfo("Настройки успешно сброшены по умолчанию");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка сброса настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                throw new ApplicationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Импорт настроек из другого файла
        /// </summary>
        public async Task<bool> ImportSettingsAsync(string filePath)
        {
            try
            {
                _loggingService.LogInfo("Импорт настроек из файла", filePath);

                if (!File.Exists(filePath))
                {
                    _loggingService.LogError("Файл настроек не найден", filePath);
                    return false;
                }

                // Читаем файл
                string json = await File.ReadAllTextAsync(filePath);

                // Проверяем, что это валидный JSON
                try
                {
                    JsonDocument.Parse(json);
                }
                catch (JsonException)
                {
                    _loggingService.LogError("Файл содержит невалидный JSON", filePath);
                    return false;
                }

                // Копируем файл в нашу конфигурацию
                File.Copy(filePath, _defaultConfigPath, true);

                // Загружаем настройки
                bool loadResult = LoadSettings();

                if (loadResult)
                {
                    _loggingService.LogInfo("Настройки успешно импортированы", filePath);
                }
                else
                {
                    _loggingService.LogError("Ошибка загрузки импортированных настроек");
                }

                return loadResult;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка импорта настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Экспорт настроек в другой файл
        /// </summary>
        public async Task<bool> ExportSettingsAsync(string filePath)
        {
            try
            {
                _loggingService.LogInfo("Экспорт настроек в файл", filePath);

                // Создаем копию текущих настроек
                await Task.Run(() => SaveSettings(filePath));

                _loggingService.LogInfo("Настройки успешно экспортированы", filePath);
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка экспорта настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Вызов события изменения настроек
        /// </summary>
        protected virtual void OnSettingsChanged(SettingsChangedEventArgs e)
        {
            SettingsChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Общие настройки приложения
    /// </summary>
    public class ApplicationSettings
    {
        /// <summary>
        /// Автоматическое подключение при запуске
        /// </summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>
        /// Каталог для экспорта данных по умолчанию
        /// </summary>
        public string DefaultExportDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Regulyators");

        /// <summary>
        /// Включить расширенное логирование
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Максимальное число записей в журнале
        /// </summary>
        public int MaxLogEntries { get; set; } = 1000;

        /// <summary>
        /// Интервал автоматического сохранения журнала (минуты, 0 - отключено)
        /// </summary>
        public int AutoSaveLogInterval { get; set; } = 30;

        /// <summary>
        /// Автоматическое создание резервной копии настроек
        /// </summary>
        public bool AutoBackupSettings { get; set; } = true;

        /// <summary>
        /// Максимальное количество попыток переподключения
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 3;

        /// <summary>
        /// Задержка между попытками переподключения (мс)
        /// </summary>
        public int ReconnectDelay { get; set; } = 2000;

        /// <summary>
        /// Создать копию настроек
        /// </summary>
        public ApplicationSettings Clone()
        {
            return new ApplicationSettings
            {
                AutoConnect = AutoConnect,
                DefaultExportDirectory = DefaultExportDirectory,
                EnableDetailedLogging = EnableDetailedLogging,
                MaxLogEntries = MaxLogEntries,
                AutoSaveLogInterval = AutoSaveLogInterval,
                AutoBackupSettings = AutoBackupSettings,
                MaxReconnectAttempts = MaxReconnectAttempts,
                ReconnectDelay = ReconnectDelay
            };
        }
    }

    /// <summary>
    /// Типы настроек системы
    /// </summary>
    public enum SettingsType
    {
        All,
        ComPort,
        Protection,
        Application
    }

    /// <summary>
    /// Аргументы события изменения настроек
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Тип измененных настроек
        /// </summary>
        public SettingsType SettingsType { get; set; }
    }
}