using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Regulyators.UI.Models
{
    /// <summary>
    /// Режимы работы двигателя
    /// </summary>
    public enum EngineMode
    {
        /// <summary>
        /// Останов
        /// </summary>
        Stop,

        /// <summary>
        /// Работа
        /// </summary>
        Run
    }

    /// <summary>
    /// Типы нагрузки двигателя
    /// </summary>
    public enum LoadType
    {
        /// <summary>
        /// Под нагрузкой
        /// </summary>
        Loaded,

        /// <summary>
        /// Холостой ход
        /// </summary>
        Idle,

        /// <summary>
        /// Буксование
        /// </summary>
        Slipping
    }

    /// <summary>
    /// Модель управления двигателем
    /// </summary>
    public class EngineControl : INotifyPropertyChanged
    {
        private double _targetEngineSpeed;
        private int _equipmentPosition;
        private EngineMode _engineMode;
        private LoadType _loadType;
        private double _rackPosition;

        /// <summary>
        /// Целевые обороты двигателя (об/мин)
        /// </summary>
        public double TargetEngineSpeed
        {
            get => _targetEngineSpeed;
            set
            {
                if (_targetEngineSpeed != value)
                {
                    _targetEngineSpeed = value > MaxEngineSpeed ? MaxEngineSpeed : value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Позиция оборудования
        /// </summary>
        public int EquipmentPosition
        {
            get => _equipmentPosition;
            set
            {
                if (_equipmentPosition != value)
                {
                    _equipmentPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Режим работы
        /// </summary>
        public EngineMode EngineMode
        {
            get => _engineMode;
            set
            {
                if (_engineMode != value)
                {
                    _engineMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Тип нагрузки
        /// </summary>
        public LoadType LoadType
        {
            get => _loadType;
            set
            {
                if (_loadType != value)
                {
                    _loadType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Положение рейки
        /// </summary>
        public double RackPosition
        {
            get => _rackPosition;
            set
            {
                if (_rackPosition != value)
                {
                    _rackPosition = value < 0 ? 0 : (value > 30 ? 30 : value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Максимальные обороты двигателя
        /// </summary>
        public double MaxEngineSpeed { get; set; } = 2400;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}