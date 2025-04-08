using System;
using System.IO.Ports;
using System.Globalization;
using System.Windows.Data;

namespace Regulyators.UI.Converters
{
    /// <summary>
    /// Конвертер для преобразования перечислений в строки и обратно
    /// </summary>
    public class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            // Особая обработка для типов из библиотеки SerialPort
            if (value is Parity parity)
            {
                switch (parity)
                {
                    case Parity.None: return "Нет";
                    case Parity.Odd: return "Нечетные";
                    case Parity.Even: return "Четные";
                    case Parity.Mark: return "Маркер";
                    case Parity.Space: return "Пробел";
                    default: return parity.ToString();
                }
            }
            else if (value is StopBits stopBits)
            {
                switch (stopBits)
                {
                    case StopBits.One: return "1";
                    case StopBits.OnePointFive: return "1.5";
                    case StopBits.Two: return "2";
                    default: return stopBits.ToString();
                }
            }

            // Стандартная обработка для других перечислений
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !(value is string))
                return null;

            string stringValue = value.ToString();

            // Если цель - перечисление Parity
            if (targetType == typeof(Parity))
            {
                switch (stringValue)
                {
                    case "Нет": return Parity.None;
                    case "Нечетные": return Parity.Odd;
                    case "Четные": return Parity.Even;
                    case "Маркер": return Parity.Mark;
                    case "Пробел": return Parity.Space;
                    default:
                        if (Enum.TryParse<Parity>(stringValue, out Parity result))
                            return result;
                        return Parity.None;
                }
            }
            // Если цель - перечисление StopBits
            else if (targetType == typeof(StopBits))
            {
                switch (stringValue)
                {
                    case "1": return StopBits.One;
                    case "1.5": return StopBits.OnePointFive;
                    case "2": return StopBits.Two;
                    default:
                        if (Enum.TryParse<StopBits>(stringValue, out StopBits result))
                            return result;
                        return StopBits.One;
                }
            }

            // Для других перечислений
            return Enum.Parse(targetType, stringValue);
        }
    }

    /// <summary>
    /// Конвертер для преобразования между boolean и Brush
    /// </summary>
    public class BooleanToBrushConverterExt : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                string[] colors = parameter?.ToString()?.Split(';') ?? new[] { "Green", "Red" };
                string colorName = boolValue ? colors[0] : (colors.Length > 1 ? colors[1] : "Red");

                // Получаем кисть по имени
                var brushType = typeof(System.Windows.Media.Brushes);
                var property = brushType.GetProperty(colorName);

                if (property != null)
                    return property.GetValue(null);

                // Если не удалось найти кисть по имени, возвращаем стандартные цвета
                return boolValue ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            }

            return System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}