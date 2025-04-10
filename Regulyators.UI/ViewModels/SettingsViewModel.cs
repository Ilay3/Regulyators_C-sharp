using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
        private bool _isBusy;
        private string _busyMessage;

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
        /// Флаг выполнения длительной операции
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Сообщение о текущей выполняемой операции
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
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

            // Проверка соответствия настроек протоколу ERCHM30TZProtocol
            if (_selectedStopBits != StopBits.Two)
            {
                _loggingService.LogWarning("Стоповые биты не соответствуют протоколу, устанавливаем StopBits.Two");
                _selectedStopBits = StopBits.Two;
                _comPortSettings.StopBits = StopBits.Two;
            }

            if (_selectedParity != Parity.Odd)
            {
                _loggingService.LogWarning("Четность не соответствует протоколу, устанавливаем Parity.Odd");
                _selectedParity = Parity.Odd;
                _comPortSettings.Parity = Parity.Odd;
            }

            if (_selectedBaudRate != 9600)
            {
                _loggingService.LogWarning("Скорость передачи не соответствует протоколу, устанавливаем 9600");
                _selectedBaudRate = 9600;
                _comPortSettings.BaudRate = 9600;
            }

            // Проверка активности соединения
            IsConnectionActive = _comPortService.IsConnected;

            // Получение доступных портов
            RefreshPorts();

            // Инициализация команд
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ApplySettingsCommand = new RelayCommand(ApplySettings, () => ApplyButtonEnabled && !IsBusy);
            SaveSettingsCommand = new RelayCommand(SaveSettings, () => !IsBusy);
            LoadSettingsCommand = new RelayCommand(LoadSettings, () => !IsBusy);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault, () => !IsBusy);
            ConnectCommand = new RelayCommand(Connect, () => !IsConnectionActive && !IsBusy);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnectionActive && !IsBusy);

            // По умолчанию кнопка применения недоступна
            ApplyButtonEnabled = false;
            IsBusy = false;

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
        private async void RefreshPorts()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Поиск доступных портов...";

                // Асинхронно получаем список портов
                var ports = await Task.Run(() => _comPortService.GetAvailablePorts());

                AvailablePorts = ports;
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
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Применение настроек коммуникации в соответствии с протоколом ЭРЧМ30ТЗ
        /// </summary>
        private async void ApplySettings()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Применение настроек...";

                // Проверка соответствия настроек протоколу ЭРЧМ30ТЗ
                bool settingsChanged = false;
                string warningMessage = string.Empty;

                // Проверка скорости передачи (должна быть 9600 бод)
                if (ComPortSettings.BaudRate != 9600)
                {
                    ComPortSettings.BaudRate = 9600;
                    settingsChanged = true;
                    warningMessage += "Скорость передачи изменена на 9600 бод в соответствии с протоколом. ";
                }

                // Проверка стоповых битов (должно быть 2)
                if (ComPortSettings.StopBits != StopBits.Two)
                {
                    ComPortSettings.StopBits = StopBits.Two;
                    settingsChanged = true;
                    warningMessage += "Стоповые биты изменены на 2 в соответствии с протоколом. ";
                }

                // Проверка четности (должна быть нечетность)
                if (ComPortSettings.Parity != Parity.Odd)
                {
                    ComPortSettings.Parity = Parity.Odd;
                    settingsChanged = true;
                    warningMessage += "Четность изменена на 'Нечетные' в соответствии с протоколом. ";
                }

                // Асинхронно применяем настройки
                await Task.Run(() => {
                    // Обновляем настройки COM-порта
                    _settingsService.UpdateComPortSettings(ComPortSettings.Clone());

                    // Обновляем пороги защит
                    _settingsService.UpdateProtectionThresholds(ProtectionThresholds.Clone());
                });

                // Если были изменения, выводим предупреждение
                if (settingsChanged)
                {
                    _loggingService.LogWarning("Настройки автоматически откорректированы", warningMessage);
                    StatusMessage = "Настройки применены с автоматической коррекцией: " + warningMessage;
                }
                else
                {
                    _loggingService.LogInfo("Настройки применены");
                    StatusMessage = "Настройки успешно применены";
                }

                ApplyButtonEnabled = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка применения настроек", ex.Message);
                StatusMessage = "Ошибка применения настроек: " + ex.Message;
            }
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Сохранение настроек в файл
        /// </summary>
        private async void SaveSettings()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Сохранение настроек...";

                // Асинхронно сохраняем настройки
                await Task.Run(() => _settingsService.SaveSettings(ConfigFilePath));

                _loggingService.LogInfo("Настройки сохранены", ConfigFilePath);
                StatusMessage = "Настройки успешно сохранены";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка сохранения настроек", ex.Message);
                StatusMessage = "Ошибка сохранения настроек";
            }
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Загрузка настроек из файла
        /// </summary>
        private async void LoadSettings()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Загрузка настроек...";

                // Асинхронно загружаем настройки
                bool result = await Task.Run(() => _settingsService.LoadSettings(ConfigFilePath));

                if (result)
                {
                    // Обновляем локальные копии настроек
                    ComPortSettings = _settingsService.ComPortSettings.Clone();
                    ProtectionThresholds = _settingsService.ProtectionThresholds.Clone();

                    _loggingService.LogInfo("Настройки загружены", ConfigFilePath);
                    StatusMessage = "Настройки успешно загружены";
                    ApplyButtonEnabled = true;
                }
                else
                {
                    _loggingService.LogWarning("Не удалось загрузить настройки из файла", ConfigFilePath);
                    StatusMessage = "Не удалось загрузить настройки из файла";
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка загрузки настроек", ex.Message);
                StatusMessage = "Ошибка загрузки настроек";
            }
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Сброс настроек по умолчанию
        /// </summary>
        private async void ResetToDefault()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Сброс настроек...";

                // Асинхронно сбрасываем настройки
                await Task.Run(() => _settingsService.ResetToDefaults());

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
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Подключение к COM-порту
        /// </summary>
        private async void Connect()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Выполняется подключение...";

                // Применяем текущие настройки
                _settingsService.UpdateComPortSettings(_comPortSettings.Clone());

                // Асинхронно выполняем подключение
                bool result = await Task.Run(() => _comPortService.Connect());

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
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Отключение от COM-порта
        /// </summary>
        private async void Disconnect()
        {
            try
            {
                // Устанавливаем флаг выполнения операции
                IsBusy = true;
                BusyMessage = "Выполняется отключение...";

                // Асинхронно выполняем отключение
                await Task.Run(() => _comPortService.Disconnect());

                StatusMessage = "Отключение от COM-порта выполнено";
                IsConnectionActive = false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка отключения от COM-порта", ex.Message);
                StatusMessage = "Ошибка отключения от COM-порта";
            }
            finally
            {
                // Снимаем флаг выполнения операции
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        /// <summary>
        /// Освобождение управляемых ресурсов
        /// </summary>
        protected override void ReleaseMangedResources()
        {
            base.ReleaseMangedResources();

            // Отписываемся от всех событий
            if (_comPortSettings != null)
            {
                _comPortSettings.PropertyChanged -= (sender, args) => ApplyButtonEnabled = true;
            }

            if (_protectionThresholds != null)
            {
                _protectionThresholds.PropertyChanged -= (sender, args) => ApplyButtonEnabled = true;
            }

            if (_comPortService != null)
            {
                _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }

            _loggingService?.LogInfo("SettingsViewModel: ресурсы освобождены");
        }
    }
}