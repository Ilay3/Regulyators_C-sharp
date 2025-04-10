using System;
using System.IO.Ports;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Regulyators.UI.Models
{
    /// <summary>
    /// Настройки COM-порта
    /// </summary>
    public class ComPortSettings : INotifyPropertyChanged
    {
        private string _portName = "COM1";
        private int _baudRate = 9600;
        private int _dataBits = 8;
        private StopBits _stopBits = StopBits.Two;
        private Parity _parity = Parity.Odd;
        private int _readTimeout = 1000;
        private int _writeTimeout = 1000;
        private int _pollingInterval = 500;
        private int _responseDelay = 50;

        /// <summary>
        /// Имя COM-порта
        /// </summary>
        public string PortName
        {
            get => _portName;
            set => SetProperty(ref _portName, value);
        }

        /// <summary>
        /// Скорость передачи (бод)
        /// </summary>
        public int BaudRate
        {
            get => _baudRate;
            set => SetProperty(ref _baudRate, value);
        }

        /// <summary>
        /// Биты данных
        /// </summary>
        public int DataBits
        {
            get => _dataBits;
            set => SetProperty(ref _dataBits, value);
        }

        /// <summary>
        /// Стоповые биты
        /// </summary>
        public StopBits StopBits
        {
            get => _stopBits;
            set => SetProperty(ref _stopBits, value);
        }

        /// <summary>
        /// Четность
        /// </summary>
        public Parity Parity
        {
            get => _parity;
            set => SetProperty(ref _parity, value);
        }

        /// <summary>
        /// Таймаут чтения (мс)
        /// </summary>
        public int ReadTimeout
        {
            get => _readTimeout;
            set => SetProperty(ref _readTimeout, value);
        }

        /// <summary>
        /// Таймаут записи (мс)
        /// </summary>
        public int WriteTimeout
        {
            get => _writeTimeout;
            set => SetProperty(ref _writeTimeout, value);
        }

        /// <summary>
        /// Интервал опроса параметров (мс)
        /// </summary>
        public int PollingInterval
        {
            get => _pollingInterval;
            set => SetProperty(ref _pollingInterval, value);
        }

        /// <summary>
        /// Задержка перед чтением ответа (мс)
        /// </summary>
        public int ResponseDelay
        {
            get => _responseDelay;
            set => SetProperty(ref _responseDelay, value);
        }

        /// <summary>
        /// Создание копии настроек
        /// </summary>
        public ComPortSettings Clone()
        {
            return new ComPortSettings
            {
                PortName = PortName,
                BaudRate = BaudRate,
                DataBits = DataBits,
                StopBits = StopBits,
                Parity = Parity,
                ReadTimeout = ReadTimeout,
                WriteTimeout = WriteTimeout,
                PollingInterval = PollingInterval,
                ResponseDelay = ResponseDelay
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Типы команд для протокола ЭРЧМ30ТЗ
    /// </summary>
    public enum CommandType : byte
    {
        /// <summary>
        /// Запрос параметров двигателя
        /// </summary>
        GetParameters = 0x01,

        /// <summary>
        /// Установка оборотов двигателя
        /// </summary>
        SetEngineSpeed = 0x02,

        /// <summary>
        /// Установка положения рейки
        /// </summary>
        SetRackPosition = 0x03,

        /// <summary>
        /// Установка режима работы (ОСТАНОВ/РАБОТА)
        /// </summary>
        SetEngineMode = 0x04,

        /// <summary>
        /// Установка типа нагрузки
        /// </summary>
        SetLoadType = 0x05,

        /// <summary>
        /// Установка позиции оборудования
        /// </summary>
        SetEquipmentPosition = 0x06,

        /// <summary>
        /// Запрос статуса защит
        /// </summary>
        GetProtectionStatus = 0x07,

        /// <summary>
        /// Установка порогов защит
        /// </summary>
        SetProtectionThresholds = 0x08,

        /// <summary>
        /// Сброс защит
        /// </summary>
        ResetProtection = 0x09
    }

    /// <summary>
    /// Команда для протокола ЭРЧМ30ТЗ
    /// </summary>
    public class ERCHM30TZCommand
    {
        /// <summary>
        /// Тип команды
        /// </summary>
        public CommandType CommandType { get; set; }

        /// <summary>
        /// Обороты двигателя (для SetEngineSpeed)
        /// </summary>
        public double EngineSpeed { get; set; }

        /// <summary>
        /// Положение рейки (для SetRackPosition)
        /// </summary>
        public double RackPosition { get; set; }

        /// <summary>
        /// Режим работы (для SetEngineMode)
        /// </summary>
        public EngineMode EngineMode { get; set; }

        /// <summary>
        /// Тип нагрузки (для SetLoadType)
        /// </summary>
        public LoadType LoadType { get; set; }

        /// <summary>
        /// Позиция оборудования (для SetEquipmentPosition)
        /// </summary>
        public int EquipmentPosition { get; set; }

        /// <summary>
        /// Пороги защит (для SetProtectionThresholds)
        /// </summary>
        public ProtectionThresholds Thresholds { get; set; }
    }

    /// <summary>
    /// Пороги срабатывания защит
    /// </summary>
    public class ProtectionThresholds : INotifyPropertyChanged
    {
        private double _oilPressureMinThreshold = 1.5;
        private double _engineSpeedMaxThreshold = 2200;
        private double _boostPressureMaxThreshold = 2.5;
        private double _oilTemperatureMaxThreshold = 110;

        /// <summary>
        /// Минимальное давление масла (кг/см²)
        /// </summary>
        public double OilPressureMinThreshold
        {
            get => _oilPressureMinThreshold;
            set => SetProperty(ref _oilPressureMinThreshold, value);
        }

        /// <summary>
        /// Максимальные обороты двигателя (об/мин)
        /// </summary>
        public double EngineSpeedMaxThreshold
        {
            get => _engineSpeedMaxThreshold;
            set => SetProperty(ref _engineSpeedMaxThreshold, value);
        }

        /// <summary>
        /// Максимальное давление наддува (кг/см²)
        /// </summary>
        public double BoostPressureMaxThreshold
        {
            get => _boostPressureMaxThreshold;
            set => SetProperty(ref _boostPressureMaxThreshold, value);
        }

        /// <summary>
        /// Максимальная температура масла (°C)
        /// </summary>
        public double OilTemperatureMaxThreshold
        {
            get => _oilTemperatureMaxThreshold;
            set => SetProperty(ref _oilTemperatureMaxThreshold, value);
        }

        /// <summary>
        /// Создание копии порогов защит
        /// </summary>
        public ProtectionThresholds Clone()
        {
            return new ProtectionThresholds
            {
                OilPressureMinThreshold = OilPressureMinThreshold,
                EngineSpeedMaxThreshold = EngineSpeedMaxThreshold,
                BoostPressureMaxThreshold = BoostPressureMaxThreshold,
                OilTemperatureMaxThreshold = OilTemperatureMaxThreshold
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}