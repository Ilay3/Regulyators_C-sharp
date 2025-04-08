using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Regulyators.UI.Controls
{
    /// <summary>
    /// Пользовательский контрол аналогового индикатора (манометра/тахометра)
    /// </summary>
    public class GaugeControl : Control
    {
        static GaugeControl()
        {
            // Важно: указание пути к ресурсам для стиля по умолчанию
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GaugeControl),
                new FrameworkPropertyMetadata(typeof(GaugeControl)));

            // Регистрация свойств зависимости
            MinimumProperty = DependencyProperty.Register(
                "Minimum", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

            MaximumProperty = DependencyProperty.Register(
                "Maximum", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

            ValueProperty = DependencyProperty.Register(
                "Value", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

            MajorTickCountProperty = DependencyProperty.Register(
                "MajorTickCount", typeof(int), typeof(GaugeControl),
                new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));

            MinorTickCountProperty = DependencyProperty.Register(
                "MinorTickCount", typeof(int), typeof(GaugeControl),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

            TitleProperty = DependencyProperty.Register(
                "Title", typeof(string), typeof(GaugeControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

            UnitLabelProperty = DependencyProperty.Register(
                "UnitLabel", typeof(string), typeof(GaugeControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

            ScaleBrushProperty = DependencyProperty.Register(
                "ScaleBrush", typeof(Brush), typeof(GaugeControl),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

            NeedleBrushProperty = DependencyProperty.Register(
                "NeedleBrush", typeof(Brush), typeof(GaugeControl),
                new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender));

            FaceBrushProperty = DependencyProperty.Register(
                "FaceBrush", typeof(Brush), typeof(GaugeControl),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

            CriticalMinValueProperty = DependencyProperty.Register(
                "CriticalMinValue", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

            CriticalMaxValueProperty = DependencyProperty.Register(
                "CriticalMaxValue", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

            CriticalZoneBrushProperty = DependencyProperty.Register(
                "CriticalZoneBrush", typeof(Brush), typeof(GaugeControl),
                new FrameworkPropertyMetadata(Brushes.Red, FrameworkPropertyMetadataOptions.AffectsRender));

            ValueFormatProperty = DependencyProperty.Register(
                "ValueFormat", typeof(string), typeof(GaugeControl),
                new FrameworkPropertyMetadata("F1", FrameworkPropertyMetadataOptions.AffectsRender));

            NeedleThicknessProperty = DependencyProperty.Register(
                "NeedleThickness", typeof(double), typeof(GaugeControl),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));
        }

        #region Dependency Properties

        /// <summary>
        /// Минимальное значение шкалы
        /// </summary>
        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty;

        /// <summary>
        /// Максимальное значение шкалы
        /// </summary>
        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty;

        /// <summary>
        /// Текущее значение
        /// </summary>
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty;

        /// <summary>
        /// Количество основных делений шкалы
        /// </summary>
        public int MajorTickCount
        {
            get { return (int)GetValue(MajorTickCountProperty); }
            set { SetValue(MajorTickCountProperty, value); }
        }

        public static readonly DependencyProperty MajorTickCountProperty;

        /// <summary>
        /// Количество промежуточных делений между основными
        /// </summary>
        public int MinorTickCount
        {
            get { return (int)GetValue(MinorTickCountProperty); }
            set { SetValue(MinorTickCountProperty, value); }
        }

        public static readonly DependencyProperty MinorTickCountProperty;

        /// <summary>
        /// Заголовок индикатора
        /// </summary>
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty;

        /// <summary>
        /// Единица измерения
        /// </summary>
        public string UnitLabel
        {
            get { return (string)GetValue(UnitLabelProperty); }
            set { SetValue(UnitLabelProperty, value); }
        }

        public static readonly DependencyProperty UnitLabelProperty;

        /// <summary>
        /// Цвет шкалы
        /// </summary>
        public Brush ScaleBrush
        {
            get { return (Brush)GetValue(ScaleBrushProperty); }
            set { SetValue(ScaleBrushProperty, value); }
        }

        public static readonly DependencyProperty ScaleBrushProperty;

        /// <summary>
        /// Цвет стрелки
        /// </summary>
        public Brush NeedleBrush
        {
            get { return (Brush)GetValue(NeedleBrushProperty); }
            set { SetValue(NeedleBrushProperty, value); }
        }

        public static readonly DependencyProperty NeedleBrushProperty;

        /// <summary>
        /// Цвет фона циферблата
        /// </summary>
        public Brush FaceBrush
        {
            get { return (Brush)GetValue(FaceBrushProperty); }
            set { SetValue(FaceBrushProperty, value); }
        }

        public static readonly DependencyProperty FaceBrushProperty;

        /// <summary>
        /// Критическое нижнее значение
        /// </summary>
        public double CriticalMinValue
        {
            get { return (double)GetValue(CriticalMinValueProperty); }
            set { SetValue(CriticalMinValueProperty, value); }
        }

        public static readonly DependencyProperty CriticalMinValueProperty;

        /// <summary>
        /// Критическое верхнее значение
        /// </summary>
        public double CriticalMaxValue
        {
            get { return (double)GetValue(CriticalMaxValueProperty); }
            set { SetValue(CriticalMaxValueProperty, value); }
        }

        public static readonly DependencyProperty CriticalMaxValueProperty;

        /// <summary>
        /// Цвет критической зоны
        /// </summary>
        public Brush CriticalZoneBrush
        {
            get { return (Brush)GetValue(CriticalZoneBrushProperty); }
            set { SetValue(CriticalZoneBrushProperty, value); }
        }

        public static readonly DependencyProperty CriticalZoneBrushProperty;

        /// <summary>
        /// Формат отображения значения
        /// </summary>
        public string ValueFormat
        {
            get { return (string)GetValue(ValueFormatProperty); }
            set { SetValue(ValueFormatProperty, value); }
        }

        public static readonly DependencyProperty ValueFormatProperty;

        /// <summary>
        /// Толщина стрелки
        /// </summary>
        public double NeedleThickness
        {
            get { return (double)GetValue(NeedleThicknessProperty); }
            set { SetValue(NeedleThicknessProperty, value); }
        }

        public static readonly DependencyProperty NeedleThicknessProperty;

        #endregion

        #region Constructors

        public GaugeControl()
        {
            this.ClipToBounds = true;
        }

        #endregion

        #region Methods

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Базовая отрисовка для демонстрации, что контрол работает
            double width = ActualWidth;
            double height = ActualHeight;
            double centerX = width / 2;
            double centerY = height / 2;
            double radius = Math.Min(width, height) / 2 - 5;

            // Рисуем круг
            drawingContext.DrawEllipse(FaceBrush, new Pen(ScaleBrush, 1),
                new Point(centerX, centerY), radius, radius);

            // Рисуем заголовок
            if (!string.IsNullOrEmpty(Title))
            {
                FormattedText titleText = new FormattedText(
                    Title,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    radius * 0.2,
                    ScaleBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                drawingContext.DrawText(titleText,
                    new Point(centerX - titleText.Width / 2, centerY - radius / 2));
            }

            // Рисуем значение и единицу измерения
            string valueText = Value.ToString(ValueFormat);
            if (!string.IsNullOrEmpty(UnitLabel))
            {
                valueText += " " + UnitLabel;
            }

            FormattedText valueFormattedText = new FormattedText(
                valueText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                radius * 0.2,
                ScaleBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            drawingContext.DrawText(valueFormattedText,
                new Point(centerX - valueFormattedText.Width / 2, centerY + radius / 4));

            // Простая стрелка как линия
            double angle = GetAngleFromValue(Value);
            double outerX = centerX + radius * 0.8 * Math.Cos(angle);
            double outerY = centerY + radius * 0.8 * Math.Sin(angle);

            drawingContext.DrawLine(
                new Pen(NeedleBrush, NeedleThickness),
                new Point(centerX, centerY),
                new Point(outerX, outerY));

            // Центральная точка (ось)
            drawingContext.DrawEllipse(
                NeedleBrush, null,
                new Point(centerX, centerY),
                radius * 0.05, radius * 0.05);
        }

        /// <summary>
        /// Получает угол (в радианах) для заданного значения
        /// </summary>
        private double GetAngleFromValue(double value)
        {
            double startAngle = Math.PI * 0.8; // ~145 градусов
            double endAngle = Math.PI * 2.2;   // ~395 градусов (или 35 градусов)
            double angleRange = endAngle - startAngle;

            // Нормализация значения в диапазоне от 0 до 1
            double normalizedValue = (value - Minimum) / (Maximum - Minimum);
            normalizedValue = Math.Max(0, Math.Min(1, normalizedValue)); // Ограничение от 0 до 1

            // Преобразование в угол
            return startAngle + normalizedValue * angleRange;
        }

        #endregion
    }
}