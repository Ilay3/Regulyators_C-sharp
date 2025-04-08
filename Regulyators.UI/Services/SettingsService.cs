using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

        /// <summary>
        /// Настройки COM-порта
        /// </summary>
        public ComPortSettings ComPortSettings { get; private set; }

        /// <summary>
        /// Пороги срабатывания защит
        /// </summary>
        public ProtectionThresholds ProtectionThresholds { get; private set; }

        /// <summary>
        /// Путь к файлу конфигурации по умолчанию
        /// </summary>
        public string DefaultConfigFilePath => _defaultConfigPath;

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

            // Путь к файлу настроек по умолчанию
            _defaultConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Regulyators",
                "config.json");

            // Обеспечиваем существование директории
            Directory.CreateDirectory(Path.GetDirectoryName(_defaultConfigPath));

            // Инициализация настроек по умолчанию
            ComPortSettings = new ComPortSettings();
            ProtectionThresholds = new ProtectionThresholds();

            // Попытка загрузки настроек из файла
            LoadSettings(_defaultConfigPath);

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
                    LastSaved = DateTime.Now
                };

                // Преобразуем в JSON с отступами для читаемости
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

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
        public void LoadSettings(string filePath = null)
        {
            filePath ??= _defaultConfigPath;

            try
            {
                if (!File.Exists(filePath))
                {
                    _loggingService.LogInfo("Файл настроек не найден, используются значения по умолчанию", filePath);
                    return;
                }

                _loggingService.LogInfo("Загрузка настроек из файла", filePath);

                // Читаем JSON из файла
                string json = File.ReadAllText(filePath);

                // Десериализуем настройки
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    // Загрузка настроек COM-порта
                    if (document.RootElement.TryGetProperty("ComPort", out JsonElement comPortElement))
                    {
                        var comPortSettings = JsonSerializer.Deserialize<ComPortSettings>(comPortElement.GetRawText());
                        if (comPortSettings != null)
                        {
                            ComPortSettings = comPortSettings;
                            _loggingService.LogInfo("Загружены настройки COM-порта", $"Порт: {comPortSettings.PortName}");
                        }
                    }

                    // Загрузка порогов защит
                    if (document.RootElement.TryGetProperty("Protection", out JsonElement protectionElement))
                    {
                        var protectionThresholds = JsonSerializer.Deserialize<ProtectionThresholds>(protectionElement.GetRawText());
                        if (protectionThresholds != null)
                        {
                            ProtectionThresholds = protectionThresholds;
                            _loggingService.LogInfo("Загружены пороги защит");
                        }
                    }
                }

                // Уведомление об изменении всех настроек
                OnSettingsChanged(new SettingsChangedEventArgs
                {
                    SettingsType = SettingsType.All
                });

                _loggingService.LogInfo("Настройки успешно загружены", filePath);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка загрузки настроек: {ex.Message}";
                _loggingService.LogError(errorMessage, ex.StackTrace);
                throw new ApplicationException(errorMessage, ex);
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
        /// Вызов события изменения настроек
        /// </summary>
        protected virtual void OnSettingsChanged(SettingsChangedEventArgs e)
        {
            SettingsChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Типы настроек системы
    /// </summary>
    public enum SettingsType
    {
        All,
        ComPort,
        Protection
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