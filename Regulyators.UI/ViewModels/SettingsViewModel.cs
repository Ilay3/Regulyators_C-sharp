using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.IO.Ports;
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
        private readonly SettingsService _settingsService;

        private ComPortSettings _comPortSettings;
        private ProtectionThresholds _protectionThresholds;
        private string _selectedPortName;
        private int _selectedBaudRate;
        private StopBits _selectedStopBits;
        private Parity _selectedParity;
        private string _configFilePath;
        private string _statusMessage;
        private bool _applyButtonEnabled;
        private string[] _availablePorts;
        private bool _isConnectionActive;

        #region Свойства

        /// <summary>
        /// Настройки COM-порта
        /// </summary>
        public ComPortSettings ComPortSettings
        {
            get => _comPortSettings;
            set
            {
                if (SetProperty(ref _comPortSettings, value))
                {
                    SelectedPortName = value.PortName;
                    SelectedBaudRate = value.BaudRate;
                    SelectedStopBits = value.StopBits;
                    SelectedParity = value.Parity;
                }
            }
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
        /// Выбранные стоповые биты
        /// </summary>
        public StopBits SelectedStopBits
        {
            get => _selectedStopBits;
            set
            {
                if (SetProperty(ref _selectedStopBits, value))
                {
                    ComPortSettings.StopBits = value;
                    ApplyButtonEnabled = true;
                }
            }
        }

        /// <summary>
        /// Выбранная четность
        /// </summary>
        public Parity SelectedParity
        {
            get => _selectedParity;
            set
            {
                if (SetProperty(ref _selectedParity, value))
                {
                    ComPortSettings.Parity = value;
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
        /// Активно ли соединение с COM-портом
        /// </summary>
        public bool IsConnectionActive
        {
            get => _isConnectionActive;
            set => SetProperty(ref _isConnectionActive, value);
        }

        /// <summary>
        /// Доступные скорости передачи
        /// </summary>
        public List<int> BaudRates { get; } = new List<int> { 9600, 19200, 38400, 57600, 115200 };

        /// <summary>
        /// Доступные стоповые биты
        /// </summary>
        public List<StopBits> StopBitsList { get; } = new List<StopBits>
        {
            StopBits.One,
            StopBits.OnePointFive,
            StopBits.Two
        };

        /// <summary>
        /// Доступные типы четности
        /// </summary>
        public List<Parity> ParityList { get; } = new List<Parity>
        {
            Parity.None,
            Parity.Odd,
            Parity.Even,
            Parity.Mark,
            Parity.Space
        };

        /// <summary>
        /// Названия типов четности для отображения
        /// </summary>
        public Dictionary<Parity, string> ParityNames { get; } = new Dictionary<Parity, string>
        {
            { Parity.None, "Нет" },
            { Parity.Odd, "Нечетные" },
            { Parity.Even, "Четные" },
            { Parity.Mark, "Маркер" },
            { Parity.Space, "Пробел" }
        };

        /// <summary>
        /// Названия типов стоповых битов для отображения
        /// </summary>
        public Dictionary<StopBits, string> StopBitsNames { get; } = new Dictionary<StopBits, string>
        {
            { StopBits.One, "1" },
            { StopBits.OnePointFive, "1.5" },
            { StopBits.Two, "2" }
        };

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

        /// <summary>
        /// Команда подключения к COM-порту
        /// </summary>
        public ICommand ConnectCommand { get; }

        /// <summary>
        /// Команда отключения от COM-порта
        /// </summary>
        public ICommand DisconnectCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public SettingsViewModel()
        {
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;
            _settingsService = SettingsService.Instance;

            // Инициализация настроек
            _comPortSettings = _settingsService.ComPortSettings.Clone();
            _protectionThresholds = _settingsService.ProtectionThresholds.Clone();

            // Путь к файлу конфигурации
            ConfigFilePath = _settingsService.DefaultConfigFilePath;

            // Инициализация выбранных значений
            _selectedPortName = _comPortSettings.PortName;
            _selectedBaudRate = _comPortSettings.BaudRate;
            _selectedStopBits = _comPortSettings.StopBits;
            _selectedParity = _comPortSettings.Parity;

            // Проверка активности соединения
            IsConnectionActive = _comPortService.IsConnected;

            // Получение доступных портов
            RefreshPorts();

            // Инициализация команд
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ApplySettingsCommand = new RelayCommand(ApplySettings, () => ApplyButtonEnabled);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            LoadSettingsCommand = new RelayCommand(LoadSettings);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            ConnectCommand = new RelayCommand(Connect, () => !IsConnectionActive);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnectionActive);

            // По умолчанию кнопка применения недоступна
            ApplyButtonEnabled = false;

            // Регистрация обработчиков событий
            _comPortSettings.PropertyChanged += (sender, args) => ApplyButtonEnabled = true;
            _protectionThresholds.PropertyChanged += (sender, args) => ApplyButtonEnabled = true;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;

            _loggingService.LogInfo("Модуль настроек инициализирован");
        }

        /// <summary>
        /// Обработчик изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            IsConnectionActive = isConnected;
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

                _loggingService.LogInfo($"Обновлен список COM-портов, найдено {AvailablePorts.Length} портов");
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
                _settingsService.UpdateComPortSettings(_comPortSettings.Clone());

                // Обновляем пороги защит
                _settingsService.UpdateProtectionThresholds(_protectionThresholds.Clone());

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
                // Сохраняем настройки через сервис настроек
                _settingsService.SaveSettings(ConfigFilePath);

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
                // Загружаем настройки через сервис настроек
                _settingsService.LoadSettings(ConfigFilePath);

                // Обновляем локальные копии настроек
                ComPortSettings = _settingsService.ComPortSettings.Clone();
                ProtectionThresholds = _settingsService.ProtectionThresholds.Clone();

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
                // Сбрасываем настройки через сервис настроек
                _settingsService.ResetToDefaults();

                // Обновляем локальные копии настроек
                ComPortSettings = _settingsService.ComPortSettings.Clone();
                ProtectionThresholds = _settingsService.ProtectionThresholds.Clone();

                // Обновляем выбранные значения
                SelectedPortName = ComPortSettings.PortName;
                SelectedBaudRate = ComPortSettings.BaudRate;
                SelectedStopBits = ComPortSettings.StopBits;
                SelectedParity = ComPortSettings.Parity;

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

        /// <summary>
        /// Подключение к COM-порту
        /// </summary>
        private void Connect()
        {
            try
            {
                // Применяем текущие настройки
                _settingsService.UpdateComPortSettings(_comPortSettings.Clone());

                // Подключаемся к порту
                bool result = _comPortService.Connect();
                if (result)
                {
                    StatusMessage = "Подключение к COM-порту выполнено успешно";
                    IsConnectionActive = true;
                }
                else
                {
                    StatusMessage = "Не удалось подключиться к COM-порту";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка подключения к COM-порту", ex.Message);
                StatusMessage = "Ошибка подключения к COM-порту";
            }
        }

        /// <summary>
        /// Отключение от COM-порта
        /// </summary>
        private void Disconnect()
        {
            try
            {
                _comPortService.Disconnect();
                StatusMessage = "Отключение от COM-порта выполнено";
                IsConnectionActive = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка отключения от COM-порта", ex.Message);
                StatusMessage = "Ошибка отключения от COM-порта";
            }
        }
    }
}