using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

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

            double width = ActualWidth;
            double height = ActualHeight;
            double centerX = width / 2;
            double centerY = height / 2;
            double radius = Math.Min(width, height) / 2 - 5;

            // Определяем углы начала и конца шкалы
            double startAngle = Math.PI * 0.8; // ~145 градусов
            double endAngle = Math.PI * 2.2;   // ~395 градусов (или 35 градусов)
            double angleRange = endAngle - startAngle;

            // Рисуем основной круг с окантовкой
            var borderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            var borderPen = new Pen(borderBrush, 2);

            // Рисуем основной циферблат
            drawingContext.DrawEllipse(FaceBrush, borderPen, new Point(centerX, centerY), radius, radius);

            // Рисуем внутреннюю окантовку для красоты
            var innerBorderRadius = radius * 0.95;
            drawingContext.DrawEllipse(null, new Pen(ScaleBrush, 0.5), new Point(centerX, centerY), innerBorderRadius, innerBorderRadius);

            // Рисуем критические зоны, если они заданы
            if (!double.IsNaN(CriticalMinValue) || !double.IsNaN(CriticalMaxValue))
            {
                DrawCriticalZones(drawingContext, centerX, centerY, radius, startAngle, angleRange);
            }

            // Рисуем основные и промежуточные деления шкалы
            DrawScale(drawingContext, centerX, centerY, radius, startAngle, angleRange);

            // Рисуем значения шкалы (цифры)
            DrawScaleValues(drawingContext, centerX, centerY, radius, startAngle, angleRange);

            // Рисуем заголовок
            if (!string.IsNullOrEmpty(Title))
            {
                DrawTitle(drawingContext, centerX, centerY, radius);
            }

            // Рисуем значение и единицу измерения
            DrawValue(drawingContext, centerX, centerY, radius);

            // Рисуем стрелку
            DrawNeedle(drawingContext, centerX, centerY, radius, GetAngleFromValue(Value));

            // Рисуем центральную ось (с эффектом блика)
            DrawAxis(drawingContext, centerX, centerY, radius);
        }

        // Рисование критических зон
        private void DrawCriticalZones(DrawingContext drawingContext, double centerX, double centerY, double radius, double startAngle, double angleRange)
        {
            double zoneRadius = radius * 0.85;
            double zoneWidth = radius * 0.15;
            double zoneInnerRadius = zoneRadius - zoneWidth;

            // Если задано критическое минимальное значение
            if (!double.IsNaN(CriticalMinValue) && CriticalMinValue > Minimum)
            {
                double criticalMinAngle = GetAngleFromValue(CriticalMinValue);

                // Создаем геометрию для дуги от начала шкалы до критического минимума
                var criticalGeometry = new StreamGeometry();
                using (StreamGeometryContext ctx = criticalGeometry.Open())
                {
                    // Начинаем с внешнего радиуса на начальном угле
                    ctx.BeginFigure(
                        new Point(
                            centerX + zoneRadius * Math.Cos(startAngle),
                            centerY + zoneRadius * Math.Sin(startAngle)),
                        true, true);

                    // Рисуем внешнюю дугу
                    ctx.ArcTo(
                        new Point(
                            centerX + zoneRadius * Math.Cos(criticalMinAngle),
                            centerY + zoneRadius * Math.Sin(criticalMinAngle)),
                        new Size(zoneRadius, zoneRadius),
                        0, criticalMinAngle - startAngle > Math.PI, SweepDirection.Clockwise, true, false);

                    // Рисуем линию до внутренней дуги
                    ctx.LineTo(
                        new Point(
                            centerX + zoneInnerRadius * Math.Cos(criticalMinAngle),
                            centerY + zoneInnerRadius * Math.Sin(criticalMinAngle)),
                        true, false);

                    // Рисуем внутреннюю дугу обратно
                    ctx.ArcTo(
                        new Point(
                            centerX + zoneInnerRadius * Math.Cos(startAngle),
                            centerY + zoneInnerRadius * Math.Sin(startAngle)),
                        new Size(zoneInnerRadius, zoneInnerRadius),
                        0, criticalMinAngle - startAngle > Math.PI, SweepDirection.Counterclockwise, true, false);

                    // Завершаем фигуру линией до начальной точки
                    ctx.LineTo(
                        new Point(
                            centerX + zoneRadius * Math.Cos(startAngle),
                            centerY + zoneRadius * Math.Sin(startAngle)),
                        true, false);
                }

                criticalGeometry.Freeze(); // Оптимизация производительности
                drawingContext.DrawGeometry(CriticalZoneBrush, null, criticalGeometry);
            }

            // Если задано критическое максимальное значение
            if (!double.IsNaN(CriticalMaxValue) && CriticalMaxValue < Maximum)
            {
                double criticalMaxAngle = GetAngleFromValue(CriticalMaxValue);
                double endAngle = startAngle + angleRange;

                // Создаем геометрию для дуги от критического максимума до конца шкалы
                var criticalGeometry = new StreamGeometry();
                using (StreamGeometryContext ctx = criticalGeometry.Open())
                {
                    // Начинаем с внешнего радиуса на критическом угле
                    ctx.BeginFigure(
                        new Point(
                            centerX + zoneRadius * Math.Cos(criticalMaxAngle),
                            centerY + zoneRadius * Math.Sin(criticalMaxAngle)),
                        true, true);

                    // Рисуем внешнюю дугу
                    ctx.ArcTo(
                        new Point(
                            centerX + zoneRadius * Math.Cos(endAngle),
                            centerY + zoneRadius * Math.Sin(endAngle)),
                        new Size(zoneRadius, zoneRadius),
                        0, endAngle - criticalMaxAngle > Math.PI, SweepDirection.Clockwise, true, false);

                    // Рисуем линию до внутренней дуги
                    ctx.LineTo(
                        new Point(
                            centerX + zoneInnerRadius * Math.Cos(endAngle),
                            centerY + zoneInnerRadius * Math.Sin(endAngle)),
                        true, false);

                    // Рисуем внутреннюю дугу обратно
                    ctx.ArcTo(
                        new Point(
                            centerX + zoneInnerRadius * Math.Cos(criticalMaxAngle),
                            centerY + zoneInnerRadius * Math.Sin(criticalMaxAngle)),
                        new Size(zoneInnerRadius, zoneInnerRadius),
                        0, endAngle - criticalMaxAngle > Math.PI, SweepDirection.Counterclockwise, true, false);

                    // Завершаем фигуру линией до начальной точки
                    ctx.LineTo(
                        new Point(
                            centerX + zoneRadius * Math.Cos(criticalMaxAngle),
                            centerY + zoneRadius * Math.Sin(criticalMaxAngle)),
                        true, false);
                }

                criticalGeometry.Freeze(); // Оптимизация производительности
                drawingContext.DrawGeometry(CriticalZoneBrush, null, criticalGeometry);
            }
        }

        // Рисование шкалы с делениями
        private void DrawScale(DrawingContext drawingContext, double centerX, double centerY, double radius, double startAngle, double angleRange)
        {
            // Задаем размеры основных и промежуточных делений
            double majorTickLength = radius * 0.15;
            double minorTickLength = radius * 0.075;
            double scaleRadius = radius * 0.9;

            // Немного утолщаем линии для улучшенного вида
            var majorTickPen = new Pen(ScaleBrush, 1.5);
            var minorTickPen = new Pen(ScaleBrush, 0.75);

            // Расчет количества промежуточных делений
            int totalMinorTicks = (MajorTickCount - 1) * (MinorTickCount + 1) + 1;
            double angleStep = angleRange / (totalMinorTicks - 1);

            // Рисуем деления
            for (int i = 0; i < totalMinorTicks; i++)
            {
                bool isMajorTick = i % (MinorTickCount + 1) == 0;
                double tickLength = isMajorTick ? majorTickLength : minorTickLength;
                Pen tickPen = isMajorTick ? majorTickPen : minorTickPen;

                double angle = startAngle + i * angleStep;

                // Рассчитываем точки начала и конца черточки
                double innerX = centerX + (scaleRadius - tickLength) * Math.Cos(angle);
                double innerY = centerY + (scaleRadius - tickLength) * Math.Sin(angle);
                double outerX = centerX + scaleRadius * Math.Cos(angle);
                double outerY = centerY + scaleRadius * Math.Sin(angle);

                // Рисуем черточку
                drawingContext.DrawLine(tickPen, new Point(innerX, innerY), new Point(outerX, outerY));
            }
        }

        // Рисование значений шкалы
        private void DrawScaleValues(DrawingContext drawingContext, double centerX, double centerY, double radius, double startAngle, double angleRange)
        {
            // Задаем радиус расположения текста и размер шрифта
            double textRadius = radius * 0.7;
            double fontSize = radius * 0.125;

            // Расчет шага для основных делений
            double angleStep = angleRange / (MajorTickCount - 1);
            double valueStep = (Maximum - Minimum) / (MajorTickCount - 1);

            // Рисуем значения для основных делений
            for (int i = 0; i < MajorTickCount; i++)
            {
                double angle = startAngle + i * angleStep;
                double value = Minimum + i * valueStep;

                // Форматируем текст значения
                string valueText = value.ToString(ValueFormat);

                // Создаем форматированный текст
                FormattedText formattedText = new FormattedText(
                    valueText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    fontSize,
                    ScaleBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Рассчитываем положение текста с учетом его размеров (центрирование)
                double textX = centerX + textRadius * Math.Cos(angle) - formattedText.Width / 2;
                double textY = centerY + textRadius * Math.Sin(angle) - formattedText.Height / 2;

                // Рисуем текст
                drawingContext.DrawText(formattedText, new Point(textX, textY));
            }
        }

        // Рисование заголовка
        private void DrawTitle(DrawingContext drawingContext, double centerX, double centerY, double radius)
        {
            double fontSize = radius * 0.15;

            FormattedText titleText = new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI SemiBold"),
                fontSize,
                ScaleBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Располагаем заголовок в верхней части циферблата
            drawingContext.DrawText(titleText,
                new Point(centerX - titleText.Width / 2, centerY - radius * 0.4));
        }

        // Рисование текущего значения и единицы измерения
        private void DrawValue(DrawingContext drawingContext, double centerX, double centerY, double radius)
        {
            string valueText = Value.ToString(ValueFormat);
            if (!string.IsNullOrEmpty(UnitLabel))
            {
                valueText += " " + UnitLabel;
            }

            double fontSize = radius * 0.18;

            FormattedText valueFormattedText = new FormattedText(
                valueText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                ScaleBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Располагаем значение в нижней части циферблата
            drawingContext.DrawText(valueFormattedText,
                new Point(centerX - valueFormattedText.Width / 2, centerY + radius * 0.3));
        }

        // Рисование стрелки индикатора
        private void DrawNeedle(DrawingContext drawingContext, double centerX, double centerY, double radius, double angle)
        {
            // Улучшенное рисование стрелки с эффектом объемности
            // Длина стрелки от центра к краю шкалы
            double needleLength = radius * 0.85;
            double needleWidth = NeedleThickness;
            double needleBaseWidth = needleWidth * 2.5; // Ширина у основания

            // Создаем геометрию стрелки в виде вытянутого треугольника
            var needleGeometry = new StreamGeometry();
            using (StreamGeometryContext ctx = needleGeometry.Open())
            {
                // Начальная точка - кончик стрелки
                double tipX = centerX + needleLength * Math.Cos(angle);
                double tipY = centerY + needleLength * Math.Sin(angle);

                // Расчет точек основания стрелки (перпендикулярно направлению)
                double perpendicularAngle = angle + Math.PI / 2; // +90 градусов
                double baseX1 = centerX + (needleBaseWidth / 2) * Math.Cos(perpendicularAngle);
                double baseY1 = centerY + (needleBaseWidth / 2) * Math.Sin(perpendicularAngle);
                double baseX2 = centerX - (needleBaseWidth / 2) * Math.Cos(perpendicularAngle);
                double baseY2 = centerY - (needleBaseWidth / 2) * Math.Sin(perpendicularAngle);

                // Рисуем треугольник стрелки
                ctx.BeginFigure(new Point(tipX, tipY), true, true);
                ctx.LineTo(new Point(baseX1, baseY1), true, true);
                ctx.LineTo(new Point(baseX2, baseY2), true, true);
            }

            needleGeometry.Freeze(); // Оптимизация производительности

            // Создаем радиальный градиент для стрелки
            var gradientCenter = new Point(centerX, centerY);
            var gradientOrigin = gradientCenter;
            var needleBrushGradient = new RadialGradientBrush();
            needleBrushGradient.GradientOrigin = new Point(0.5, 0.5);
            needleBrushGradient.Center = new Point(0.5, 0.5);
            needleBrushGradient.RadiusX = 0.5;
            needleBrushGradient.RadiusY = 0.5;

            // Определяем цвета градиента
            Color mainColor = ((SolidColorBrush)NeedleBrush).Color;
            Color darkColor = Color.FromArgb(
                mainColor.A,
                (byte)Math.Max(0, mainColor.R - 50),
                (byte)Math.Max(0, mainColor.G - 50),
                (byte)Math.Max(0, mainColor.B - 50));

            needleBrushGradient.GradientStops.Add(new GradientStop(mainColor, 0.0));
            needleBrushGradient.GradientStops.Add(new GradientStop(darkColor, 1.0));

            // Создаем кисть для контура
            var darkColorBrush = new SolidColorBrush(darkColor);

            // Рисуем стрелку с градиентной заливкой
            drawingContext.DrawGeometry(needleBrushGradient, new Pen(darkColorBrush, 0.5), needleGeometry);
        }

        // Рисование центральной оси (с эффектом блика)
        private void DrawAxis(DrawingContext drawingContext, double centerX, double centerY, double radius)
        {
            double axisRadius = radius * 0.08;

            // Создаем радиальный градиент для эффекта блика на оси
            var axisBrushGradient = new RadialGradientBrush();
            axisBrushGradient.GradientOrigin = new Point(0.3, 0.3); // Смещение для эффекта блика
            axisBrushGradient.Center = new Point(0.5, 0.5);
            axisBrushGradient.RadiusX = 0.5;
            axisBrushGradient.RadiusY = 0.5;

            // Цвета для градиента
            Color needleColor = ((SolidColorBrush)NeedleBrush).Color;
            Color lightColor = Color.FromArgb(
                needleColor.A,
                (byte)Math.Min(255, needleColor.R + 100),
                (byte)Math.Min(255, needleColor.G + 100),
                (byte)Math.Min(255, needleColor.B + 100));

            axisBrushGradient.GradientStops.Add(new GradientStop(lightColor, 0.0)); // Блик
            axisBrushGradient.GradientStops.Add(new GradientStop(needleColor, 0.5)); // Основной цвет
            axisBrushGradient.GradientStops.Add(new GradientStop(Colors.Black, 1.0)); // Тень

            // Рисуем ось
            drawingContext.DrawEllipse(axisBrushGradient, null, new Point(centerX, centerY), axisRadius, axisRadius);

            // Добавляем небольшой блик (белый кружок)
            double highlightRadius = axisRadius * 0.3;
            // Создаем полупрозрачную белую кисть
            var whiteBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));

            drawingContext.DrawEllipse(
                whiteBrush,
                null,
                new Point(centerX - axisRadius * 0.3, centerY - axisRadius * 0.3), // Смещение для эффекта
                highlightRadius, highlightRadius);
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