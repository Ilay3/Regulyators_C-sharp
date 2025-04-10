using System;
using System.Collections.Generic;
using ScottPlot;
using System.Drawing;
using System.Linq;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Класс для хранения точки данных графика
    /// </summary>
    public class DataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>
    /// Сервис для управления графиками
    /// </summary>
    public class GraphService
    {
        private static GraphService _instance;
        private readonly Dictionary<string, (double[] xData, double[] yData, int count, int maxPoints)> _dataSeries;

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static GraphService Instance => _instance ??= new GraphService();

        private GraphService()
        {
            _dataSeries = new Dictionary<string, (double[] xData, double[] yData, int count, int maxPoints)>();
        }

        /// <summary>
        /// Инициализация серии данных
        /// </summary>
        /// <param name="seriesName">Название серии</param>
        /// <param name="maxPoints">Максимальное количество точек</param>
        public void InitSeries(string seriesName, int maxPoints = 100)
        {
            if (!_dataSeries.ContainsKey(seriesName))
            {
                _dataSeries[seriesName] = (new double[maxPoints], new double[maxPoints], 0, maxPoints);
            }
        }

        /// <summary>
        /// Добавление точки данных в серию
        /// </summary>
        /// <param name="seriesName">Название серии</param>
        /// <param name="x">Значение X (обычно время)</param>
        /// <param name="y">Значение Y (значение параметра)</param>
        public void AddDataPoint(string seriesName, double x, double y)
        {
            if (!_dataSeries.ContainsKey(seriesName))
            {
                InitSeries(seriesName);
            }

            var (xData, yData, count, maxPoints) = _dataSeries[seriesName];

            // Если массив заполнен, сдвигаем все элементы
            if (count >= maxPoints)
            {
                Array.Copy(xData, 1, xData, 0, maxPoints - 1);
                Array.Copy(yData, 1, yData, 0, maxPoints - 1);
                count = maxPoints - 1;
            }

            // Добавляем новую точку
            xData[count] = x;
            yData[count] = y;
            count++;

            _dataSeries[seriesName] = (xData, yData, count, maxPoints);
        }

        /// <summary>
        /// Получение данных серии
        /// </summary>
        /// <param name="seriesName">Название серии</param>
        /// <returns>Список точек данных</returns>
        public List<DataPoint> GetSeriesData(string seriesName)
        {
            var result = new List<DataPoint>();

            if (_dataSeries.TryGetValue(seriesName, out var data))
            {
                var (xData, yData, count, _) = data;

                for (int i = 0; i < count; i++)
                {
                    result.Add(new DataPoint { X = xData[i], Y = yData[i] });
                }
            }

            return result;
        }

        /// <summary>
        /// Настройка и отображение графика
        /// </summary>
        /// <param name="plot">Объект графика</param>
        /// <param name="seriesNames">Имена серий для отображения</param>
        public void ConfigurePlot(WpfPlot plot, params string[] seriesNames)
        {
            plot.Plot.Clear();

            // Настройки осей и стиля
            plot.Plot.XLabel("Время (сек)");
            plot.Plot.YLabel("Значение");
            plot.Plot.Title("Параметры двигателя");
            plot.Plot.Style(Style.Seaborn);

            // Добавляем каждую серию на график
            Color[] colors = { Color.Blue, Color.Red, Color.Green, Color.Orange, Color.Purple };
            int colorIndex = 0;

            foreach (var seriesName in seriesNames)
            {
                if (_dataSeries.TryGetValue(seriesName, out var data))
                {
                    var (xData, yData, count, _) = data;

                    if (count > 0)
                    {
                        // Получаем только заполненную часть массивов
                        double[] xSlice = new double[count];
                        double[] ySlice = new double[count];
                        Array.Copy(xData, xSlice, count);
                        Array.Copy(yData, ySlice, count);

                        // Добавляем на график с нужным цветом и подписью
                        var color = colors[colorIndex % colors.Length];
                        var line = plot.Plot.AddScatter(xSlice, ySlice, color, label: seriesName);
                        line.LineWidth = 2;
                    }

                    colorIndex++;
                }
            }

            // Добавляем легенду
            plot.Plot.Legend();

            // Обновляем график
            plot.Refresh();
        }

        /// <summary>
        /// Очистка серий данных
        /// </summary>
        public void ClearAllSeries()
        {
            foreach (var series in _dataSeries.Keys.ToList())
            {
                var (xData, yData, _, maxPoints) = _dataSeries[series];
                _dataSeries[series] = (xData, yData, 0, maxPoints);
            }
        }
    }
}