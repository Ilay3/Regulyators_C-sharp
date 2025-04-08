using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Globalization;
using Regulyators.UI.Models;
using Regulyators.UI.ViewModels;

namespace Regulyators.UI.Services
{
    /// <summary>
    /// Сервис для экспорта данных системы
    /// </summary>
    public class ExportService
    {
        private static ExportService _instance;
        private readonly LoggingService _loggingService;
        private readonly CultureInfo _exportCulture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>
        /// Получение экземпляра сервиса (Singleton)
        /// </summary>
        public static ExportService Instance => _instance ??= new ExportService();

        private ExportService()
        {
            _loggingService = LoggingService.Instance;
        }

        /// <summary>
        /// Экспорт параметров двигателя в CSV
        /// </summary>
        public async Task<bool> ExportParametersToCSVAsync(List<EngineParameters> parameters, string filePath)
        {
            try
            {
                if (parameters == null || parameters.Count == 0)
                {
                    _loggingService.LogWarning("Нет данных для экспорта");
                    return false;
                }

                // Используем выделенную культуру для обеспечения правильного форматирования
                StringBuilder sb = new StringBuilder();

                // Заголовок CSV
                sb.AppendLine("Время;Обороты двигателя (об/мин);Обороты турбокомпрессора (об/мин);" +
                             "Давление масла (кг/см²);Давление наддува (кг/см²);" +
                             "Температура масла (°C);Положение рейки (код)");

                // Данные
                foreach (var param in parameters)
                {
                    sb.AppendLine($"{param.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", _exportCulture)};" +
                                  $"{param.EngineSpeed.ToString("F0", _exportCulture)};" +
                                  $"{param.TurboCompressorSpeed.ToString("F0", _exportCulture)};" +
                                  $"{param.OilPressure.ToString("F2", _exportCulture)};" +
                                  $"{param.BoostPressure.ToString("F2", _exportCulture)};" +
                                  $"{param.OilTemperature.ToString("F1", _exportCulture)};" +
                                  $"{param.RackPosition}");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                _loggingService.LogInfo("Экспорт параметров в CSV", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта параметров в CSV", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Экспорт журнала событий защиты в CSV
        /// </summary>
        public async Task<bool> ExportProtectionEventsToCSVAsync(List<ProtectionEvent> events, string filePath)
        {
            try
            {
                if (events == null || events.Count == 0)
                {
                    _loggingService.LogWarning("Нет событий для экспорта");
                    return false;
                }

                StringBuilder sb = new StringBuilder();

                // Заголовок CSV (используем точку с запятой как разделитель для совместимости с Excel)
                sb.AppendLine("Время;Система;Сообщение;Подробности");

                // Данные
                foreach (var evt in events)
                {
                    // Экранируем кавычками поля, которые могут содержать точку с запятой
                    string system = EscapeCSVField(evt.System);
                    string message = EscapeCSVField(evt.Message);
                    string details = EscapeCSVField(evt.Details);

                    sb.AppendLine($"{evt.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", _exportCulture)};" +
                                  $"{system};" +
                                  $"{message};" +
                                  $"{details}");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                _loggingService.LogInfo("Экспорт событий защиты в CSV", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта событий защиты в CSV", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Экспорт журнала событий в CSV
        /// </summary>
        public async Task<bool> ExportLogEntriesToCSVAsync(List<LogEntry> logs, string filePath)
        {
            try
            {
                if (logs == null || logs.Count == 0)
                {
                    _loggingService.LogWarning("Нет записей журнала для экспорта");
                    return false;
                }

                StringBuilder sb = new StringBuilder();

                // Заголовок CSV
                sb.AppendLine("Время;Тип;Сообщение;Подробности");

                // Данные
                foreach (var log in logs)
                {
                    // Экранируем кавычками поля, которые могут содержать точку с запятой
                    string type = EscapeCSVField(log.Type);
                    string message = EscapeCSVField(log.Message);
                    string details = EscapeCSVField(log.Details);

                    sb.AppendLine($"{log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", _exportCulture)};" +
                                  $"{type};" +
                                  $"{message};" +
                                  $"{details}");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                _loggingService.LogInfo("Экспорт журнала в CSV", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта журнала в CSV", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Экспорт всех настроек системы в JSON-файл
        /// </summary>
        public async Task<bool> ExportSettingsToJsonAsync(string filePath)
        {
            try
            {
                // Получаем настройки из сервисов
                var settingsService = SettingsService.Instance;

                // Формируем объект настроек
                var settings = new
                {
                    ComPort = settingsService.ComPortSettings,
                    Protection = settingsService.ProtectionThresholds,
                    ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", _exportCulture)
                };

                // Сериализуем в JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Для корректного отображения кириллицы
                };
                string json = JsonSerializer.Serialize(settings, options);

                // Записываем в файл
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                _loggingService.LogInfo("Экспорт настроек в JSON", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта настроек в JSON", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Экспорт данных о текущем состоянии системы в HTML-отчет
        /// </summary>
        public async Task<bool> ExportSystemReportToHtmlAsync(string filePath)
        {
            try
            {
                // Получаем сервисы
                var loggingService = LoggingService.Instance;
                var settingsService = SettingsService.Instance;

                // Формируем HTML-документ
                StringBuilder html = new StringBuilder();

                // Заголовок HTML
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html lang=\"ru\">");
                html.AppendLine("<head>");
                html.AppendLine("    <meta charset=\"UTF-8\">");
                html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
                html.AppendLine("    <title>Отчет о состоянии системы испытаний регуляторов ЭРЧМ30ТЗ</title>");
                html.AppendLine("    <style>");
                html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
                html.AppendLine("        h1 { color: #2196F3; }");
                html.AppendLine("        h2 { color: #0D47A1; margin-top: 30px; }");
                html.AppendLine("        table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
                html.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                html.AppendLine("        th { background-color: #f2f2f2; }");
                html.AppendLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
                html.AppendLine("        .critical { color: red; font-weight: bold; }");
                html.AppendLine("        .info { color: green; }");
                html.AppendLine("        .warning { color: orange; }");
                html.AppendLine("        .error { color: red; }");
                html.AppendLine("        .footer { margin-top: 50px; font-size: 12px; color: #666; text-align: center; }");
                html.AppendLine("        @media print {");
                html.AppendLine("            body { font-size: 12px; }");
                html.AppendLine("            h1 { font-size: 18px; }");
                html.AppendLine("            h2 { font-size: 16px; }");
                html.AppendLine("        }");
                html.AppendLine("    </style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");

                // Заголовок отчета
                html.AppendLine($"    <h1>Отчет о состоянии системы испытаний регуляторов ЭРЧМ30ТЗ</h1>");
                html.AppendLine($"    <p>Дата и время формирования: {DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", _exportCulture)}</p>");

                // Раздел настроек COM-порта
                html.AppendLine("    <h2>Настройки связи</h2>");
                html.AppendLine("    <table>");
                html.AppendLine("        <tr><th>Параметр</th><th>Значение</th></tr>");
                html.AppendLine($"        <tr><td>COM-порт</td><td>{settingsService.ComPortSettings.PortName}</td></tr>");
                html.AppendLine($"        <tr><td>Скорость (бод)</td><td>{settingsService.ComPortSettings.BaudRate}</td></tr>");
                html.AppendLine($"        <tr><td>Биты данных</td><td>{settingsService.ComPortSettings.DataBits}</td></tr>");
                html.AppendLine($"        <tr><td>Стоповые биты</td><td>{settingsService.ComPortSettings.StopBits}</td></tr>");
                html.AppendLine($"        <tr><td>Четность</td><td>{settingsService.ComPortSettings.Parity}</td></tr>");
                html.AppendLine($"        <tr><td>Таймаут чтения</td><td>{settingsService.ComPortSettings.ReadTimeout} мс</td></tr>");
                html.AppendLine($"        <tr><td>Таймаут записи</td><td>{settingsService.ComPortSettings.WriteTimeout} мс</td></tr>");
                html.AppendLine($"        <tr><td>Интервал опроса</td><td>{settingsService.ComPortSettings.PollingInterval} мс</td></tr>");
                html.AppendLine($"        <tr><td>Задержка ответа</td><td>{settingsService.ComPortSettings.ResponseDelay} мс</td></tr>");
                html.AppendLine("    </table>");

                // Раздел настроек защит
                html.AppendLine("    <h2>Настройки защит</h2>");
                html.AppendLine("    <table>");
                html.AppendLine("        <tr><th>Параметр</th><th>Значение</th></tr>");
                html.AppendLine($"        <tr><td>Минимальное давление масла</td><td>{settingsService.ProtectionThresholds.OilPressureMinThreshold.ToString("F2", _exportCulture)} кг/см²</td></tr>");
                html.AppendLine($"        <tr><td>Максимальные обороты двигателя</td><td>{settingsService.ProtectionThresholds.EngineSpeedMaxThreshold.ToString("F0", _exportCulture)} об/мин</td></tr>");
                html.AppendLine($"        <tr><td>Максимальное давление наддува</td><td>{settingsService.ProtectionThresholds.BoostPressureMaxThreshold.ToString("F2", _exportCulture)} кг/см²</td></tr>");
                html.AppendLine($"        <tr><td>Максимальная температура масла</td><td>{settingsService.ProtectionThresholds.OilTemperatureMaxThreshold.ToString("F1", _exportCulture)} °C</td></tr>");
                html.AppendLine("    </table>");

                // Журнал событий
                html.AppendLine("    <h2>Последние события системы</h2>");
                html.AppendLine("    <table>");
                html.AppendLine("        <tr><th>Время</th><th>Тип</th><th>Сообщение</th><th>Подробности</th></tr>");

                // Получаем последние 20 записей журнала
                var lastLogs = loggingService.Logs.Take(20).ToList();
                foreach (var log in lastLogs)
                {
                    string cssClass = "";
                    switch (log.Type)
                    {
                        case "Информация":
                            cssClass = "info";
                            break;
                        case "Предупреждение":
                            cssClass = "warning";
                            break;
                        case "Ошибка":
                            cssClass = "error";
                            break;
                    }

                    html.AppendLine($"        <tr class=\"{cssClass}\">");
                    html.AppendLine($"            <td>{log.Timestamp.ToString("dd.MM.yyyy HH:mm:ss", _exportCulture)}</td>");
                    html.AppendLine($"            <td>{HtmlEncode(log.Type)}</td>");
                    html.AppendLine($"            <td>{HtmlEncode(log.Message)}</td>");
                    html.AppendLine($"            <td>{HtmlEncode(log.Details)}</td>");
                    html.AppendLine($"        </tr>");
                }

                html.AppendLine("    </table>");

                // Подвал отчета
                html.AppendLine("    <div class=\"footer\">");
                html.AppendLine("        <p>Система испытаний регуляторов ЭРЧМ30ТЗ © ОАО \"Коломенский завод\"</p>");
                html.AppendLine("    </div>");

                // Добавление скрипта для печати
                html.AppendLine("    <script>");
                html.AppendLine("        window.onload = function() {");
                html.AppendLine("            // Добавляем кнопку печати");
                html.AppendLine("            var printButton = document.createElement('button');");
                html.AppendLine("            printButton.textContent = 'Печать отчета';");
                html.AppendLine("            printButton.style.position = 'fixed';");
                html.AppendLine("            printButton.style.bottom = '20px';");
                html.AppendLine("            printButton.style.right = '20px';");
                html.AppendLine("            printButton.style.padding = '10px';");
                html.AppendLine("            printButton.style.backgroundColor = '#2196F3';");
                html.AppendLine("            printButton.style.color = 'white';");
                html.AppendLine("            printButton.style.border = 'none';");
                html.AppendLine("            printButton.style.borderRadius = '4px';");
                html.AppendLine("            printButton.style.cursor = 'pointer';");
                html.AppendLine("            printButton.onclick = function() { window.print(); };");
                html.AppendLine("            document.body.appendChild(printButton);");
                html.AppendLine("        };");
                html.AppendLine("    </script>");

                // Закрытие HTML
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                // Запись в файл
                await File.WriteAllTextAsync(filePath, html.ToString(), Encoding.UTF8);
                _loggingService.LogInfo("Экспорт отчета о состоянии системы в HTML", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта отчета о состоянии системы", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Экранирование поля для CSV 
        /// (если поле содержит разделитель (;), заключаем его в кавычки и удваиваем внутренние кавычки)
        /// </summary>
        private string EscapeCSVField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // Если поле содержит точку с запятой или кавычки, его нужно заключить в кавычки
            bool needsQuotes = field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r');

            if (needsQuotes)
            {
                // Удваиваем все кавычки внутри поля
                field = field.Replace("\"", "\"\"");
                // Заключаем поле в кавычки
                return $"\"{field}\"";
            }

            return field;
        }

        /// <summary>
        /// Кодирование HTML-специальных символов
        /// </summary>
        private string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}