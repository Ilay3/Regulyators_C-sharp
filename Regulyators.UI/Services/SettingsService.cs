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
        private readonly string _defaultConfigFilePath;

        /// <summary>
        /// Настройки COM-порта
        /// </summary>
        public ComPortSettings ComPortSettings { get; private set; }

        /// <summary>
        /// Пороги срабатывания защит
        /// </summary>
        public ProtectionThresholds ProtectionThresholds { get; private set; }

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
            _defaultConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Regulyators",
                "config.json");

            // Обеспечиваем существование директории
            Directory.CreateDirectory(Path.GetDirectoryName(_defaultConfigFilePath));

            // Инициализация настроек по умолчанию
            ComPortSettings = new ComPortSettings();
            ProtectionThresholds = new ProtectionThresholds();

            // Попытка загрузки настроек из файла
            Task.Run(() => LoadSettingsAsync(_defaultConfigFilePath));
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
        }

        /// <summary>
        /// Сохранение настроек в файл
        /// </summary>
        public void SaveSettings(string filePath = null)
        {
            filePath ??= _defaultConfigFilePath;

            try
            {
                // Создаем объект с настройками
                var settings = new
                {
                    ComPort = ComPortSettings,
                    Protection = ProtectionThresholds
                };

                // Преобразуем в JSON с отступами для читаемости
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Сохраняем в файл
                File.WriteAllText(filePath, json);

                _loggingService.LogInfo("Настройки сохранены", filePath);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка сохранения настроек", ex.Message);
            }
        }

        /// <summary>
        /// Асинхронная загрузка настроек из файла
        /// </summary>
        public async Task LoadSettingsAsync(string filePath = null)
        {
            filePath ??= _defaultConfigFilePath;

            try
            {
                if (!File.Exists(filePath))
                {
                    _loggingService.LogInfo("Файл настроек не найден, используются значения по умолчанию");
                    return;
                }

                // Читаем JSON из файла
                string json = await File.ReadAllTextAsync(filePath);

                // Десериализуем в объект
                var settingsObj = JsonSerializer.Deserialize<JsonElement>(json);

                if (settingsObj.TryGetProperty("ComPort", out var comPortJson))
                {
                    var comPortSettings = JsonSerializer.Deserialize<ComPortSettings>(comPortJson.GetRawText());
                    if (comPortSettings != null)
                    {
                        ComPortSettings = comPortSettings;
                    }
                }

                if (settingsObj.TryGetProperty("Protection", out var protectionJson))
                {
                    var protectionThresholds = JsonSerializer.Deserialize<ProtectionThresholds>(protectionJson.GetRawText());
                    if (protectionThresholds != null)
                    {
                        ProtectionThresholds = protectionThresholds;
                    }
                }

                _loggingService.LogInfo("Настройки загружены", filePath);

                // Уведомление об изменении всех настроек
                OnSettingsChanged(new SettingsChangedEventArgs
                {
                    SettingsType = SettingsType.All
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка загрузки настроек", ex.Message);
            }
        }

        /// <summary>
        /// Сброс настроек по умолчанию
        /// </summary>
        public void ResetToDefaults()
        {
            ComPortSettings = new ComPortSettings();
            ProtectionThresholds = new ProtectionThresholds();

            // Уведомление об изменении всех настроек
            OnSettingsChanged(new SettingsChangedEventArgs
            {
                SettingsType = SettingsType.All
            });

            // Автоматическое сохранение настроек
            SaveSettings();

            _loggingService.LogInfo("Настройки сброшены по умолчанию");
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