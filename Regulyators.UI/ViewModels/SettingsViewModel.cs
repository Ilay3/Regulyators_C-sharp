using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.Xml;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для настроек системы
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;

        private ComPortSettings _comPortSettings;
        private ProtectionThresholds _protectionThresholds;
        private string _selectedPortName;
        private int _selectedBaudRate;
        private string _configFilePath;
        private string _statusMessage;
        private bool _applyButtonEnabled;
        private string[] _availablePorts;

        #region Свойства

        /// <summary>
        /// Настройки COM-порта
        /// </summary>
        public ComPortSettings ComPortSettings
        {
            get => _comPortSettings;
            set => SetProperty(ref _comPortSettings, value);
        }

        /// <summary>
        /// Пороги срабатывания защит
        /// </summary>
        public ProtectionThresholds ProtectionThresholds
        {
            get => _protectionThresholds;
            set => SetProperty(ref _protectionThresholds, value);
        }

        /// <summary>
        /// Выбранный COM-порт
        /// </summary>
        public string SelectedPortName
        {
            get => _selectedPortName;
            set
            {
                if (SetProperty(ref _selectedPortName, value))
                {
                    ComPortSettings.PortName = value;
                    ApplyButtonEnabled = true;
                }
            }
        }

        /// <summary>
        /// Выбранная скорость передачи
        /// </summary>
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set
            {
                if (SetProperty(ref _selectedBaudRate, value))
                {
                    ComPortSettings.BaudRate = value;
                    ApplyButtonEnabled = true;
                }
            }
        }

        /// <summary>
        /// Путь к файлу конфигурации
        /// </summary>
        public string ConfigFilePath
        {
            get => _configFilePath;
            set => SetProperty(ref _configFilePath, value);
        }

        /// <summary>
        /// Сообщение о статусе
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Доступность кнопки применения настроек
        /// </summary>
        public bool ApplyButtonEnabled
        {
            get => _applyButtonEnabled;
            set => SetProperty(ref _applyButtonEnabled, value);
        }

        /// <summary>
        /// Доступные COM-порты
        /// </summary>
        public string[] AvailablePorts
        {
            get => _availablePorts;
            set => SetProperty(ref _availablePorts, value);
        }

        /// <summary>
        /// Доступные скорости передачи
        /// </summary>
        public int[] BaudRates { get; } = { 9600, 19200, 38400, 57600, 115200 };

        #endregion

        #region Команды

        /// <summary>
        /// Команда обновления списка портов
        /// </summary>
        public ICommand RefreshPortsCommand { get; }

        /// <summary>
        /// Команда применения настроек
        /// </summary>
        public ICommand ApplySettingsCommand { get; }

        /// <summary>
        /// Команда сохранения настроек
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// Команда загрузки настроек
        /// </summary>
        public ICommand LoadSettingsCommand { get; }

        /// <summary>
        /// Команда сброса настроек по умолчанию
        /// </summary>
        public ICommand ResetToDefaultCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public SettingsViewModel()
        {
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;

            // Инициализация настроек
            _comPortSettings = _comPortService.Settings.Clone();
            _protectionThresholds = new ProtectionThresholds();

            // Путь к файлу конфигурации по умолчанию
            ConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Regulyators",
                "config.json");

            // Обеспечиваем существование директории
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));

            // Инициализация выбранных значений
            _selectedPortName = _comPortSettings.PortName;
            _selectedBaudRate = _comPortSettings.BaudRate;

            // Получение доступных портов
            RefreshPorts();

            // Инициализация команд
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ApplySettingsCommand = new RelayCommand(ApplySettings, () => ApplyButtonEnabled);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            LoadSettingsCommand = new RelayCommand(LoadSettings);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);

            // По умолчанию кнопка применения недоступна
            ApplyButtonEnabled = false;

            // Регистрация обработчиков событий изменения настроек
            _comPortSettings.PropertyChanged += (sender, args) => ApplyButtonEnabled = true;
            _protectionThresholds.PropertyChanged += (sender, args) => ApplyButtonEnabled = true;
        }

        /// <summary>
        /// Обновление списка доступных COM-портов
        /// </summary>
        private void RefreshPorts()
        {
            try
            {
                AvailablePorts = _comPortService.GetAvailablePorts();
                StatusMessage = $"Найдено портов: {AvailablePorts.Length}";

                // Если текущий порт не найден в списке, но список не пуст,
                // выбираем первый доступный порт
                if (AvailablePorts.Length > 0 && !AvailablePorts.Contains(SelectedPortName))
                {
                    SelectedPortName = AvailablePorts[0];
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка получения списка портов", ex.Message);
                StatusMessage = "Ошибка получения списка портов";
            }
        }

        /// <summary>
        /// Применение настроек
        /// </summary>
        private void ApplySettings()
        {
            try
            {
                // Обновляем настройки COM-порта
                _comPortService.UpdateSettings(_comPortSettings.Clone());

                // Обновляем пороги защит в EngineParameters
                // (в реальном приложении здесь нужно обновлять настройки через соответствующий сервис)
                // ...

                _loggingService.LogInfo("Настройки применены");
                StatusMessage = "Настройки успешно применены";
                ApplyButtonEnabled = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка применения настроек", ex.Message);
                StatusMessage = "Ошибка применения настроек";
            }
        }

        /// <summary>
        /// Сохранение настроек в файл
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // Создаем объект с настройками
                var settings = new
                {
                    ComPort = _comPortSettings,
                    Protection = _protectionThresholds
                };

                // Преобразуем в JSON
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Сохраняем в файл
                File.WriteAllText(ConfigFilePath, json);

                _loggingService.LogInfo("Настройки сохранены", ConfigFilePath);
                StatusMessage = "Настройки успешно сохранены";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка сохранения настроек", ex.Message);
                StatusMessage = "Ошибка сохранения настроек";
            }
        }

        /// <summary>
        /// Загрузка настроек из файла
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    _loggingService.LogWarning("Файл настроек не найден", ConfigFilePath);
                    StatusMessage = "Файл настроек не найден";
                    return;
                }

                // Читаем JSON из файла
                string json = File.ReadAllText(ConfigFilePath);

                // Десериализуем в объект
                var settingsObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (settingsObj.TryGetValue("ComPort", out var comPortJson))
                {
                    var comPortSettings = JsonSerializer.Deserialize<ComPortSettings>(comPortJson.GetRawText());
                    if (comPortSettings != null)
                    {
                        ComPortSettings = comPortSettings;
                        SelectedPortName = comPortSettings.PortName;
                        SelectedBaudRate = comPortSettings.BaudRate;
                    }
                }

                if (settingsObj.TryGetValue("Protection", out var protectionJson))
                {
                    var protectionThresholds = JsonSerializer.Deserialize<ProtectionThresholds>(protectionJson.GetRawText());
                    if (protectionThresholds != null)
                    {
                        ProtectionThresholds = protectionThresholds;
                    }
                }

                _loggingService.LogInfo("Настройки загружены", ConfigFilePath);
                StatusMessage = "Настройки успешно загружены";
                ApplyButtonEnabled = true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка загрузки настроек", ex.Message);
                StatusMessage = "Ошибка загрузки настроек";
            }
        }

        /// <summary>
        /// Сброс настроек по умолчанию
        /// </summary>
        private void ResetToDefault()
        {
            try
            {
                // Создаем настройки по умолчанию
                ComPortSettings = new ComPortSettings();
                ProtectionThresholds = new ProtectionThresholds();

                // Обновляем выбранные значения
                SelectedPortName = ComPortSettings.PortName;
                SelectedBaudRate = ComPortSettings.BaudRate;

                _loggingService.LogInfo("Настройки сброшены по умолчанию");
                StatusMessage = "Настройки сброшены по умолчанию";
                ApplyButtonEnabled = true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка сброса настроек", ex.Message);
                StatusMessage = "Ошибка сброса настроек";
            }
        }
    }
}