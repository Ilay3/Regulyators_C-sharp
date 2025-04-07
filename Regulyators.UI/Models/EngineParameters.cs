using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Regulyators.UI.Models
{
    /// <summary>
    /// Модель параметров двигателя
    /// </summary>
    public class EngineParameters : INotifyPropertyChanged
    {
        private double _engineSpeed;
        private double _turboCompressorSpeed;
        private double _oilPressure;
        private double _boostPressure;
        private double _oilTemperature;
        private int _rackPosition;
        private DateTime _timestamp;
        private bool _isOilPressureCritical;
        private bool _isEngineSpeedCritical;
        private bool _isBoostPressureCritical;
        private bool _isOilTemperatureCritical;
        private double _oilPressureCriticalThreshold = 1.5;
        private double _engineSpeedCriticalThreshold = 2200;
        private double _boostPressureCriticalThreshold = 2.5;
        private double _oilTemperatureCriticalThreshold = 110;

        /// <summary>
        /// Обороты двигателя (об/мин)
        /// </summary>
        public double EngineSpeed
        {
            get => _engineSpeed;
            set
            {
                if (_engineSpeed != value)
                {
                    _engineSpeed = value;
                    OnPropertyChanged();
                    IsEngineSpeedCritical = value > _engineSpeedCriticalThreshold;
                }
            }
        }

        /// <summary>
        /// Обороты турбокомпрессора (об/мин)
        /// </summary>
        public double TurboCompressorSpeed
        {
            get => _turboCompressorSpeed;
            set
            {
                if (_turboCompressorSpeed != value)
                {
                    _turboCompressorSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Давление масла (кг/см²)
        /// </summary>
        public double OilPressure
        {
            get => _oilPressure;
            set
            {
                if (_oilPressure != value)
                {
                    _oilPressure = value;
                    OnPropertyChanged();
                    IsOilPressureCritical = value < _oilPressureCriticalThreshold;
                }
            }
        }

        /// <summary>
        /// Давление наддува (кг/см²)
        /// </summary>
        public double BoostPressure
        {
            get => _boostPressure;
            set
            {
                if (_boostPressure != value)
                {
                    _boostPressure = value;
                    OnPropertyChanged();
                    IsBoostPressureCritical = value > _boostPressureCriticalThreshold;
                }
            }
        }

        /// <summary>
        /// Температура масла (°C)
        /// </summary>
        public double OilTemperature
        {
            get => _oilTemperature;
            set
            {
                if (_oilTemperature != value)
                {
                    _oilTemperature = value;
                    OnPropertyChanged();
                    IsOilTemperatureCritical = value > _oilTemperatureCriticalThreshold;
                }
            }
        }

        /// <summary>
        /// Положение рейки (код)
        /// </summary>
        public int RackPosition
        {
            get => _rackPosition;
            set
            {
                if (_rackPosition != value)
                {
                    _rackPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Время измерения
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Флаг критического давления масла
        /// </summary>
        public bool IsOilPressureCritical
        {
            get => _isOilPressureCritical;
            set
            {
                if (_isOilPressureCritical != value)
                {
                    _isOilPressureCritical = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Флаг критических оборотов двигателя
        /// </summary>
        public bool IsEngineSpeedCritical
        {
            get => _isEngineSpeedCritical;
            set
            {
                if (_isEngineSpeedCritical != value)
                {
                    _isEngineSpeedCritical = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Флаг критического давления наддува
        /// </summary>
        public bool IsBoostPressureCritical
        {
            get => _isBoostPressureCritical;
            set
            {
                if (_isBoostPressureCritical != value)
                {
                    _isBoostPressureCritical = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Флаг критической температуры масла
        /// </summary>
        public bool IsOilTemperatureCritical
        {
            get => _isOilTemperatureCritical;
            set
            {
                if (_isOilTemperatureCritical != value)
                {
                    _isOilTemperatureCritical = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Пороговое значение давления масла (кг/см²)
        /// </summary>
        public double OilPressureCriticalThreshold
        {
            get => _oilPressureCriticalThreshold;
            set
            {
                if (_oilPressureCriticalThreshold != value)
                {
                    _oilPressureCriticalThreshold = value;
                    OnPropertyChanged();
                    IsOilPressureCritical = OilPressure < value;
                }
            }
        }

        /// <summary>
        /// Пороговое значение оборотов двигателя (об/мин)
        /// </summary>
        public double EngineSpeedCriticalThreshold
        {
            get => _engineSpeedCriticalThreshold;
            set
            {
                if (_engineSpeedCriticalThreshold != value)
                {
                    _engineSpeedCriticalThreshold = value;
                    OnPropertyChanged();
                    IsEngineSpeedCritical = EngineSpeed > value;
                }
            }
        }

        /// <summary>
        /// Пороговое значение давления наддува (кг/см²)
        /// </summary>
        public double BoostPressureCriticalThreshold
        {
            get => _boostPressureCriticalThreshold;
            set
            {
                if (_boostPressureCriticalThreshold != value)
                {
                    _boostPressureCriticalThreshold = value;
                    OnPropertyChanged();
                    IsBoostPressureCritical = BoostPressure > value;
                }
            }
        }

        /// <summary>
        /// Пороговое значение температуры масла (°C)
        /// </summary>
        public double OilTemperatureCriticalThreshold
        {
            get => _oilTemperatureCriticalThreshold;
            set
            {
                if (_oilTemperatureCriticalThreshold != value)
                {
                    _oilTemperatureCriticalThreshold = value;
                    OnPropertyChanged();
                    IsOilTemperatureCritical = OilTemperature > value;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}