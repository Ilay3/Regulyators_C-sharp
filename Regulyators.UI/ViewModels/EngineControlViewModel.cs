using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления двигателем
    /// </summary>
    public class EngineControlViewModel : ViewModelBase
    {
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;

        private EngineControl _engineControl;
        private double _currentEngineSpeed;
        private string _currentEngineMode;
        private string _currentLoadType;
        private double _currentRackPosition;
        private string _protectionStatus;
        private Brush _protectionStatusColor;
        private bool _isEngineStop = true;
        private bool _isEngineRun;
        private bool _isLoaded;
        private bool _isIdle = true;
        private bool _isSlipping;
        private string _statusMessage;
        private bool _isConnected;

        /// <summary>
        /// Модель управления двигателем
        /// </summary>
        public EngineControl EngineControl
        {
            get => _engineControl;
            set => SetProperty(ref _engineControl, value);
        }

        /// <summary>
        /// Текущие обороты двигателя
        /// </summary>
        public double CurrentEngineSpeed
        {
            get => _currentEngineSpeed;
            set => SetProperty(ref _currentEngineSpeed, value);
        }

        /// <summary>
        /// Текущий режим работы двигателя
        /// </summary>
        public string CurrentEngineMode
        {
            get => _currentEngineMode;
            set => SetProperty(ref _currentEngineMode, value);
        }

        /// <summary>
        /// Текущий тип нагрузки
        /// </summary>
        public string CurrentLoadType
        {
            get => _currentLoadType;
            set => SetProperty(ref _currentLoadType, value);
        }

        /// <summary>
        /// Текущее положение рейки
        /// </summary>
        public double CurrentRackPosition
        {
            get => _currentRackPosition;
            set => SetProperty(ref _currentRackPosition, value);
        }

        /// <summary>
        /// Статус защиты двигателя
        /// </summary>
        public string ProtectionStatus
        {
            get => _protectionStatus;
            set => SetProperty(ref _protectionStatus, value);
        }

        /// <summary>
        /// Цвет индикатора защиты
        /// </summary>
        public Brush ProtectionStatusColor
        {
            get => _protectionStatusColor;
            set => SetProperty(ref _protectionStatusColor, value);
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
        /// Флаг подключения к оборудованию
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        /// <summary>
        /// Флаг режима ОСТАНОВ
        /// </summary>
        public bool IsEngineStop
        {
            get => _isEngineStop;
            set
            {
                if (SetProperty(ref _isEngineStop, value) && value)
                {
                    IsEngineRun = false;
                    CurrentEngineMode = "ОСТАНОВ";
                    EngineControl.EngineMode = EngineMode.Stop;
                }
            }
        }

        /// <summary>
        /// Флаг режима РАБОТА
        /// </summary>
        public bool IsEngineRun
        {
            get => _isEngineRun;
            set
            {
                if (SetProperty(ref _isEngineRun, value) && value)
                {
                    IsEngineStop = false;
                    CurrentEngineMode = "РАБОТА";
                    EngineControl.EngineMode = EngineMode.Run;
                }
            }
        }

        /// <summary>
        /// Флаг типа нагрузки "Под нагрузкой"
        /// </summary>
        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                if (SetProperty(ref _isLoaded, value) && value)
                {
                    IsIdle = false;
                    IsSlipping = false;
                    CurrentLoadType = "Под нагрузкой";
                    EngineControl.LoadType = LoadType.Loaded;
                }
            }
        }

        /// <summary>
        /// Флаг типа нагрузки "Холостой ход"
        /// </summary>
        public bool IsIdle
        {
            get => _isIdle;
            set
            {
                if (SetProperty(ref _isIdle, value) && value)
                {
                    IsLoaded = false;
                    IsSlipping = false;
                    CurrentLoadType = "Холостой ход";
                    EngineControl.LoadType = LoadType.Idle;
                }
            }
        }

        /// <summary>
        /// Флаг типа нагрузки "Буксование"
        /// </summary>
        public bool IsSlipping
        {
            get => _isSlipping;
            set
            {
                if (SetProperty(ref _isSlipping, value) && value)
                {
                    IsLoaded = false;
                    IsIdle = false;
                    CurrentLoadType = "Буксование";
                    EngineControl.LoadType = LoadType.Slipping;
                }
            }
        }

        // Команды
        public ICommand SetEngineModeCommand { get; }
        public ICommand SetLoadTypeCommand { get; }
        public ICommand SetEngineSpeedCommand { get; }
        public ICommand SetRackPositionCommand { get; }
        public ICommand SetEquipmentPositionCommand { get; }
        public ICommand QuickCommandCommand { get; }
        public ICommand StartEngineCommand { get; }
        public ICommand StopEngineCommand { get; }

        /// <summary>
        /// Конструктор
        /// </summary>
        public EngineControlViewModel()
        {
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;

            // Инициализация модели управления
            EngineControl = new EngineControl
            {
                TargetEngineSpeed = 800,
                EquipmentPosition = 0,
                EngineMode = EngineMode.Stop,
                LoadType = LoadType.Idle,
                RackPosition = 0
            };

            // Инициализация текущих значений
            CurrentEngineSpeed = 0;
            CurrentEngineMode = "ОСТАНОВ";
            CurrentLoadType = "Холостой ход";
            CurrentRackPosition = 0;
            ProtectionStatus = "Неактивна";
            ProtectionStatusColor = Brushes.Green;
            StatusMessage = "Готов к работе";

            // Проверка подключения
            IsConnected = _comPortService.IsConnected;

            // Инициализация команд
            SetEngineModeCommand = new RelayCommand<string>(SetEngineMode);
            SetLoadTypeCommand = new RelayCommand<string>(SetLoadType);
            SetEngineSpeedCommand = new RelayCommand(OnSetEngineSpeed);
            SetRackPositionCommand = new RelayCommand(OnSetRackPosition);
            SetEquipmentPositionCommand = new RelayCommand(OnSetEquipmentPosition);
            QuickCommandCommand = new RelayCommand<string>(OnQuickCommand);
            StartEngineCommand = new RelayCommand(OnStartEngine);
            StopEngineCommand = new RelayCommand(OnStopEngine);

            // Подписка на события COM-порта
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _comPortService.ErrorOccurred += OnErrorOccurred;

            // Запрос текущих параметров
            RefreshParameters();
        }

        /// <summary>
        /// Обработчик события получения данных от COM-порта
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentEngineSpeed = parameters.EngineSpeed;
                    CurrentRackPosition = parameters.RackPosition;

                    StatusMessage = $"Данные обновлены: {parameters.Timestamp:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки полученных данных", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события изменения статуса подключения
        /// </summary>
        private void OnConnectionStatusChanged(object sender, bool isConnected)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = isConnected;
                    StatusMessage = isConnected ? "Подключено к оборудованию" : "Нет подключения к оборудованию";

                    if (isConnected)
                    {
                        RefreshParameters();
                    }
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки изменения статуса подключения", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события ошибки COM-порта
        /// </summary>
        private void OnErrorOccurred(object sender, string errorMessage)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Ошибка: {errorMessage}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки события ошибки COM-порта", ex.Message);
            }
        }

        /// <summary>
        /// Запрос текущих параметров
        /// </summary>
        private void RefreshParameters()
        {
            if (IsConnected)
            {
                // Отправка команды запроса параметров
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.GetParameters
                });

                // Отправка команды запроса статуса защит
                _comPortService.SendCommand(new ERCHM30TZCommand
                {
                    CommandType = CommandType.GetProtectionStatus
                });

                StatusMessage = "Запрос параметров отправлен";
            }
            else
            {
                StatusMessage = "Нет подключения к оборудованию";
            }
        }

        /// <summary>
        /// Установка режима работы двигателя
        /// </summary>
        private void SetEngineMode(string mode)
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно установить режим: нет подключения";
                return;
            }

            EngineMode engineMode;
            if (mode == "Stop")
            {
                engineMode = EngineMode.Stop;
                IsEngineStop = true;
            }
            else if (mode == "Run")
            {
                engineMode = EngineMode.Run;
                IsEngineRun = true;
            }
            else
            {
                return;
            }

            // Отправка команды установки режима
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.SetEngineMode,
                EngineMode = engineMode
            });

            StatusMessage = $"Команда установки режима работы отправлена: {(engineMode == EngineMode.Stop ? "ОСТАНОВ" : "РАБОТА")}";
            _loggingService.LogInfo($"Установка режима работы: {(engineMode == EngineMode.Stop ? "ОСТАНОВ" : "РАБОТА")}");
        }

        /// <summary>
        /// Установка типа нагрузки
        /// </summary>
        private void SetLoadType(string loadType)
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно установить тип нагрузки: нет подключения";
                return;
            }

            LoadType engineLoadType;
            switch (loadType)
            {
                case "Loaded":
                    engineLoadType = LoadType.Loaded;
                    IsLoaded = true;
                    break;
                case "Idle":
                    engineLoadType = LoadType.Idle;
                    IsIdle = true;
                    break;
                case "Slipping":
                    engineLoadType = LoadType.Slipping;
                    IsSlipping = true;
                    break;
                default:
                    return;
            }

            // Отправка команды установки типа нагрузки
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.SetLoadType,
                LoadType = engineLoadType
            });

            StatusMessage = $"Команда установки типа нагрузки отправлена: {CurrentLoadType}";
            _loggingService.LogInfo($"Установка типа нагрузки: {CurrentLoadType}");
        }

        /// <summary>
        /// Установка оборотов двигателя
        /// </summary>
        private void OnSetEngineSpeed()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно установить обороты: нет подключения";
                return;
            }

            // Отправка команды установки оборотов
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.SetEngineSpeed,
                EngineSpeed = EngineControl.TargetEngineSpeed
            });

            StatusMessage = $"Команда установки оборотов отправлена: {EngineControl.TargetEngineSpeed:F0} об/мин";
            _loggingService.LogInfo($"Установка оборотов двигателя: {EngineControl.TargetEngineSpeed:F0} об/мин");
        }

        /// <summary>
        /// Установка положения рейки
        /// </summary>
        private void OnSetRackPosition()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно установить положение рейки: нет подключения";
                return;
            }

            // Отправка команды установки положения рейки
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.SetRackPosition,
                RackPosition = EngineControl.RackPosition
            });

            StatusMessage = $"Команда установки положения рейки отправлена: {EngineControl.RackPosition:F2}";
            _loggingService.LogInfo($"Установка положения рейки: {EngineControl.RackPosition:F2}");
        }

        /// <summary>
        /// Установка позиции оборудования
        /// </summary>
        private void OnSetEquipmentPosition()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно установить позицию оборудования: нет подключения";
                return;
            }

            // Отправка команды установки позиции оборудования
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.SetEquipmentPosition,
                EquipmentPosition = EngineControl.EquipmentPosition
            });

            StatusMessage = $"Команда установки позиции оборудования отправлена: {EngineControl.EquipmentPosition}";
            _loggingService.LogInfo($"Установка позиции оборудования: {EngineControl.EquipmentPosition}");
        }

        /// <summary>
        /// Выполнение быстрой команды
        /// </summary>
        private void OnQuickCommand(string commandNumber)
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно выполнить команду: нет подключения";
                return;
            }

            double targetSpeed = 800;

            // Преобразование номера команды в целевую скорость
            switch (commandNumber)
            {
                case "1": targetSpeed = 800; break;
                case "2": targetSpeed = 1000; break;
                case "3": targetSpeed = 1200; break;
                case "4": targetSpeed = 1400; break;
                case "5": targetSpeed = 1600; break;
                case "6": targetSpeed = 1800; break;
                case "7": targetSpeed = 2000; break;
                case "8": targetSpeed = 2200; break;
                default: return;
            }

            // Установка целевой скорости
            EngineControl.TargetEngineSpeed = targetSpeed;

            // Отправка команды
            OnSetEngineSpeed();
        }

        /// <summary>
        /// Запуск двигателя
        /// </summary>
        private void OnStartEngine()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно запустить двигатель: нет подключения";
                return;
            }

            // Установка режима РАБОТА
            SetEngineMode("Run");
        }

        /// <summary>
        /// Останов двигателя
        /// </summary>
        private void OnStopEngine()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно остановить двигатель: нет подключения";
                return;
            }

            // Установка режима ОСТАНОВ
            SetEngineMode("Stop");
        }
    }
}