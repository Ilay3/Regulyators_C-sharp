using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Regulyators.UI.Common;
using Regulyators.UI.Models;
using Regulyators.UI.Services;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// ViewModel для панели аналоговых индикаторов
    /// </summary>
    public class GaugePanelViewModel : INotifyPropertyChanged
    {
        private readonly ComPortService _comPortService;
        private readonly LoggingService _loggingService;
        private string _selectedUpdateInterval;
        private EngineParameters _engineParameters;
        private bool _isConnected;
        private string _statusMessage;

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
                    UpdatePollingInterval();
                }
            }
        }

        /// <summary>
        /// Статус соединения
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    StatusMessage = value ? "Подключено к оборудованию" : "Нет подключения к оборудованию";
                }
            }
        }

        /// <summary>
        /// Статусное сообщение
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
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
            _comPortService = ComPortService.Instance;
            _loggingService = LoggingService.Instance;

            // Инициализация параметров двигателя
            _engineParameters = new EngineParameters
            {
                EngineSpeed = 0,
                TurboCompressorSpeed = 0,
                OilPressure = 0,
                BoostPressure = 0,
                OilTemperature = 0,
                RackPosition = 0,
                Timestamp = DateTime.Now
            };

            // Настройка выбранного интервала обновления
            _selectedUpdateInterval = "Средняя (500 мс)";

            // Проверка подключения
            IsConnected = _comPortService.IsConnected;

            // Инициализация команд
            RefreshGaugesCommand = new RelayCommand(RefreshGauges);
            SaveSnapshotCommand = new RelayCommand(SaveSnapshot);

            // Подписка на события обновления данных
            _comPortService.DataReceived += OnDataReceived;
            _comPortService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _comPortService.ErrorOccurred += OnErrorOccurred;

            StatusMessage = IsConnected ? "Подключено к оборудованию" : "Нет подключения к оборудованию";
        }

        /// <summary>
        /// Обновление интервала опроса
        /// </summary>
        private void UpdatePollingInterval()
        {
            if (!IsConnected) return;

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

            // Обновляем интервал опроса в настройках COM-порта
            var settings = _comPortService.Settings;
            settings.PollingInterval = interval;
            _comPortService.UpdateSettings(settings);

            _loggingService.LogInfo($"Интервал обновления изменен: {interval} мс");
            StatusMessage = $"Интервал обновления: {interval} мс";
        }

        /// <summary>
        /// Обработчик события получения данных
        /// </summary>
        private void OnDataReceived(object sender, EngineParameters parameters)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    EngineParameters = parameters;
                    StatusMessage = $"Данные обновлены: {parameters.Timestamp:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки данных", ex.Message);
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
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка обработки статуса подключения", ex.Message);
            }
        }

        /// <summary>
        /// Обработчик события возникновения ошибки
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
                _loggingService.LogError("Ошибка обработки события ошибки", ex.Message);
            }
        }

        /// <summary>
        /// Ручное обновление индикаторов
        /// </summary>
        private void RefreshGauges()
        {
            if (!IsConnected)
            {
                StatusMessage = "Невозможно обновить данные: нет подключения";
                return;
            }

            // Отправляем команду запроса параметров
            _comPortService.SendCommand(new ERCHM30TZCommand
            {
                CommandType = CommandType.GetParameters
            });

            StatusMessage = "Запрос обновления данных отправлен";
        }

        /// <summary>
        /// Сохранение снимка панели индикаторов (заглушка)
        /// </summary>
        private void SaveSnapshot()
        {
            MessageBox.Show("Функция сохранения снимка будет реализована в следующей версии",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Очистка ресурсов при выгрузке
        /// </summary>
        public void Dispose()
        {
            // Отписываемся от событий
            _comPortService.DataReceived -= OnDataReceived;
            _comPortService.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _comPortService.ErrorOccurred -= OnErrorOccurred;
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