using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Regulyators.UI.Converters
{
    /// <summary>
    /// Конвертер для преобразования булевого значения в строку
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string stringParam)
                {
                    string[] options = stringParam.Split(';');
                    if (options.Length == 2)
                    {
                        return boolValue ? options[0] : options[1];
                    }
                }
                return boolValue ? "Да" : "Нет";
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования булевого значения в кисть (Brush)
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string stringParam)
                {
                    string[] options = stringParam.Split(';');
                    if (options.Length == 2)
                    {
                        // Преобразуем строковые имена цветов в кисти
                        var brushType = typeof(Brushes);
                        var trueBrush = (Brush)brushType.GetProperty(options[0]).GetValue(null);
                        var falseBrush = (Brush)brushType.GetProperty(options[1]).GetValue(null);
                        return boolValue ? trueBrush : falseBrush;
                    }
                }
                return boolValue ? Brushes.Green : Brushes.Red;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования числа в строку с единицей измерения
    /// </summary>
    public class ValueToStringWithUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                string format = "0.##";
                string unit = "";

                if (parameter is string stringParam)
                {
                    string[] options = stringParam.Split(';');
                    if (options.Length >= 1)
                    {
                        format = options[0];
                    }
                    if (options.Length >= 2)
                    {
                        unit = options[1];
                    }
                }

                return doubleValue.ToString(format, culture) + " " + unit;
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования значения в видимость
    /// </summary>
    public class ValueToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;

            if (value is bool boolValue)
            {
                isVisible = boolValue;
            }
            else if (value is string stringValue)
            {
                isVisible = !string.IsNullOrEmpty(stringValue);
            }
            else if (value is int intValue)
            {
                isVisible = intValue != 0;
            }
            else if (value is double doubleValue)
            {
                isVisible = doubleValue != 0;
            }

            // Если указан параметр "Inverse", инвертируем результат
            if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                isVisible = !isVisible;
            }

            return isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}