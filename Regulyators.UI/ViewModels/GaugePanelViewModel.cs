using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Regulyators.UI.Common;
using Regulyators.UI.Models;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для панели аналоговых индикаторов (упрощенная демо-версия)
    /// </summary>
    public class GaugePanelViewModel : INotifyPropertyChanged
    {
        private Timer _updateTimer;
        private readonly Random _random;
        private EngineParameters _engineParameters;
        private string _selectedUpdateInterval;

        #region Свойства

        /// <summary>
        /// Параметры двигателя для отображения
        /// </summary>
        public EngineParameters EngineParameters
        {
            get => _engineParameters;
            set
            {
                _engineParameters = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Выбранный интервал обновления
        /// </summary>
        public string SelectedUpdateInterval
        {
            get => _selectedUpdateInterval;
            set
            {
                if (_selectedUpdateInterval != value)
                {
                    _selectedUpdateInterval = value;
                    OnPropertyChanged();
                    UpdateTimerInterval();
                }
            }
        }

        #endregion

        #region Команды

        /// <summary>
        /// Команда обновления индикаторов
        /// </summary>
        public ICommand RefreshGaugesCommand { get; }

        /// <summary>
        /// Команда сохранения снимка панели индикаторов
        /// </summary>
        public ICommand SaveSnapshotCommand { get; }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        public GaugePanelViewModel()
        {
            _random = new Random();

            // Инициализация параметров двигателя
            _engineParameters = new EngineParameters
            {
                EngineSpeed = 800,
                TurboCompressorSpeed = 5000,
                OilPressure = 2.5,
                BoostPressure = 1.2,
                OilTemperature = 75,
                RackPosition = 10,
                Timestamp = DateTime.Now
            };

            // Настройка таймера обновления
            _selectedUpdateInterval = "Средняя (500 мс)";
            _updateTimer = new Timer(UpdateGauges, null, 0, 500);

            // Инициализация команд
            RefreshGaugesCommand = new RelayCommand(RefreshGauges);
            SaveSnapshotCommand = new RelayCommand(SaveSnapshot);
        }

        /// <summary>
        /// Обновление интервала таймера
        /// </summary>
        private void UpdateTimerInterval()
        {
            int interval = 500; // Значение по умолчанию

            if (_selectedUpdateInterval?.Contains("Высокая") == true)
            {
                interval = 100;
            }
            else if (_selectedUpdateInterval?.Contains("Средняя") == true)
            {
                interval = 500;
            }
            else if (_selectedUpdateInterval?.Contains("Низкая") == true)
            {
                interval = 1000;
            }

            // Пересоздаем таймер с новым интервалом
            _updateTimer?.Dispose();
            _updateTimer = new Timer(UpdateGauges, null, 0, interval);
        }

        /// <summary>
        /// Обработчик таймера обновления
        /// </summary>
        private void UpdateGauges(object state)
        {
            // Генерация демо-значений
            GenerateDemoValues();
        }

        /// <summary>
        /// Генерация демо-значений
        /// </summary>
        private void GenerateDemoValues()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Создаем небольшие вариации текущих значений
                    double engineSpeed = EngineParameters.EngineSpeed + _random.Next(-50, 50);
                    double turboSpeed = EngineParameters.TurboCompressorSpeed + _random.Next(-200, 200);
                    double oilPressure = Math.Max(0, EngineParameters.OilPressure + (_random.NextDouble() - 0.5) * 0.3);
                    double boostPressure = Math.Max(0, EngineParameters.BoostPressure + (_random.NextDouble() - 0.5) * 0.2);
                    double oilTemperature = EngineParameters.OilTemperature + (_random.NextDouble() - 0.5) * 2;
                    int rackPosition = Math.Max(0, EngineParameters.RackPosition + _random.Next(-1, 2));

                    // Иногда генерируем критические значения для демонстрации
                    if (_random.Next(50) == 0) // 2% вероятность
                    {
                        switch (_random.Next(4))
                        {
                            case 0:
                                oilPressure = 0.5 + _random.NextDouble() * 0.9; // критически низкое давление масла
                                break;
                            case 1:
                                engineSpeed = 2250 + _random.Next(0, 300); // критически высокие обороты
                                break;
                            case 2:
                                boostPressure = 2.6 + _random.NextDouble() * 0.7; // критически высокое давление наддува
                                break;
                            case 3:
                                oilTemperature = 115 + _random.Next(0, 20); // критически высокая температура масла
                                break;
                        }
                    }

                    // Обновляем значения
                    EngineParameters.EngineSpeed = engineSpeed;
                    EngineParameters.TurboCompressorSpeed = turboSpeed;
                    EngineParameters.OilPressure = oilPressure;
                    EngineParameters.BoostPressure = boostPressure;
                    EngineParameters.OilTemperature = oilTemperature;
                    EngineParameters.RackPosition = rackPosition;
                    EngineParameters.Timestamp = DateTime.Now;
                });
            }
            catch
            {
                // Игнорируем ошибки для демо
            }
        }

        /// <summary>
        /// Ручное обновление индикаторов
        /// </summary>
        private void RefreshGauges()
        {
            GenerateDemoValues();
        }

        /// <summary>
        /// Сохранение снимка панели индикаторов (заглушка)
        /// </summary>
        private void SaveSnapshot()
        {
            MessageBox.Show("Сохранение снимка (демо-функция)", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Очистка ресурсов при выгрузке
        /// </summary>
        public void Dispose()
        {
            _updateTimer?.Dispose();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}