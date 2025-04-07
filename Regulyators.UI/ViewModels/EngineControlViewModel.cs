using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using Regulyators.UI.Common;
using Regulyators.UI.Models;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для управления двигателем
    /// </summary>
    public class EngineControlViewModel : ViewModelBase
    {
        private readonly Timer _updateTimer;
        private readonly Random _random;
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
            _random = new Random();

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

            // Инициализация команд
            SetEngineModeCommand = new RelayCommand<string>(SetEngineMode);
            SetLoadTypeCommand = new RelayCommand<string>(SetLoadType);
            SetEngineSpeedCommand = new RelayCommand(OnSetEngineSpeed);
            SetRackPositionCommand = new RelayCommand(OnSetRackPosition);
            SetEquipmentPositionCommand = new RelayCommand(OnSetEquipmentPosition);
            QuickCommandCommand = new RelayCommand<string>(OnQuickCommand);
            StartEngineCommand = new RelayCommand(OnStartEngine);
            StopEngineCommand = new RelayCommand(OnStopEngine);

            // Инициализация таймера обновления данных
            _updateTimer = new Timer(UpdateParameters, null, 500, 500);
        }

        /// <summary>
        /// Установка режима работы двигателя
        /// </summary>
        private void SetEngineMode(string mode)
        {
            if (mode == "Stop")
            {
                IsEngineStop = true;
            }
            else if (mode == "Run")
            {
                IsEngineRun = true;
            }
        }

        /// <summary>
        /// Установка типа нагрузки
        /// </summary>
        private void SetLoadType(string loadType)
        {
            switch (loadType)
            {
                case "Loaded":
                    IsLoaded = true;
                    break;
                case "Idle":
                    IsIdle = true;
                    break;
                case "Slipping":
                    IsSlipping = true;
                    break;
            }
        }

        /// <summary>
        /// Установка оборотов двигателя
        /// </summary>
        private void OnSetEngineSpeed()
        {
            // В реальном приложении здесь была бы отправка команды на контроллер
            CurrentEngineSpeed = EngineControl.TargetEngineSpeed;
        }

        /// <summary>
        /// Установка положения рейки
        /// </summary>
        private void OnSetRackPosition()
        {
            // В реальном приложении здесь была бы отправка команды на контроллер
            CurrentRackPosition = EngineControl.RackPosition;
        }

        /// <summary>
        /// Установка позиции оборудования
        /// </summary>
        private void OnSetEquipmentPosition()
        {
            // В реальном приложении здесь была бы отправка команды на контроллер
        }

        /// <summary>
        /// Выполнение быстрой команды
        /// </summary>
        private void OnQuickCommand(string commandNumber)
        {
            // Демонстрационная логика быстрых команд
            switch (commandNumber)
            {
                case "1":
                    EngineControl.TargetEngineSpeed = 800;
                    break;
                case "2":
                    EngineControl.TargetEngineSpeed = 1000;
                    break;
                case "3":
                    EngineControl.TargetEngineSpeed = 1200;
                    break;
                case "4":
                    EngineControl.TargetEngineSpeed = 1400;
                    break;
                case "5":
                    EngineControl.TargetEngineSpeed = 1600;
                    break;
                case "6":
                    EngineControl.TargetEngineSpeed = 1800;
                    break;
                case "7":
                    EngineControl.TargetEngineSpeed = 2000;
                    break;
                case "8":
                    EngineControl.TargetEngineSpeed = 2200;
                    break;
            }

            // Применение команды
            OnSetEngineSpeed();
        }

        /// <summary>
        /// Запуск двигателя
        /// </summary>
        private void OnStartEngine()
        {
            IsEngineRun = true;
        }

        /// <summary>
        /// Останов двигателя
        /// </summary>
        private void OnStopEngine()
        {
            IsEngineStop = true;
        }

        /// <summary>
        /// Обновление параметров двигателя (демо-режим)
        /// </summary>
        private void UpdateParameters(object state)
        {
            if (EngineControl.EngineMode == EngineMode.Run)
            {
                // Генерация рандомных значений для демонстрации
                double speedVariation = _random.Next(-50, 50);
                double targetSpeed = EngineControl.TargetEngineSpeed + speedVariation;

                // Статус защиты иногда случайно активируется
                bool randomProtection = _random.Next(100) < 5;

                // Обновление данных в UI потоке
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentEngineSpeed = targetSpeed;
                    CurrentRackPosition = EngineControl.RackPosition;

                    if (randomProtection)
                    {
                        ProtectionStatus = "Активна";
                        ProtectionStatusColor = Brushes.Red;
                    }
                    else
                    {
                        ProtectionStatus = "Неактивна";
                        ProtectionStatusColor = Brushes.Green;
                    }
                });
            }
            else
            {
                // Если двигатель остановлен
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentEngineSpeed = 0;
                    ProtectionStatus = "Неактивна";
                    ProtectionStatusColor = Brushes.Green;
                });
            }
        }
    }
}