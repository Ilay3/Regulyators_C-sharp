using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// Базовый класс для всех ViewModel с реализацией INotifyPropertyChanged и IDisposable
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        // Флаг для отслеживания, был ли уже вызван Dispose
        private bool _disposed = false;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Уведомляет об изменении свойства
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Устанавливает значение свойства и вызывает PropertyChanged при изменении
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Реализация шаблона IDisposable с идиомой Dispose Pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Метод для освобождения управляемых и неуправляемых ресурсов
        /// </summary>
        /// <param name="disposing">True, если вызвано явно из Dispose(), false - если из финализатора</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Освобождение управляемых ресурсов
                ReleaseMangedResources();
            }

            // Освобождение неуправляемых ресурсов
            ReleaseUnmanagedResources();

            _disposed = true;
        }

        /// <summary>
        /// Переопределяется в производных классах для освобождения управляемых ресурсов
        /// </summary>
        protected virtual void ReleaseMangedResources()
        {
            // По умолчанию ничего не делает
        }

        /// <summary>
        /// Переопределяется в производных классах для освобождения неуправляемых ресурсов
        /// </summary>
        protected virtual void ReleaseUnmanagedResources()
        {
            // По умолчанию ничего не делает
        }

        /// <summary>
        /// Финализатор
        /// </summary>
        ~ViewModelBase()
        {
            Dispose(false);
        }
    }
}