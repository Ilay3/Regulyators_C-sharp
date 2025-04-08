using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

                StringBuilder sb = new StringBuilder();

                // Заголовок CSV
                sb.AppendLine("Время,Обороты двигателя (об/мин),Обороты турбокомпрессора (об/мин)," +
                             "Давление масла (кг/см²),Давление наддува (кг/см²)," +
                             "Температура масла (°C),Положение рейки (код)");

                // Данные
                foreach (var param in parameters)
                {
                    sb.AppendLine($"{param.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                  $"{param.EngineSpeed:F0}," +
                                  $"{param.TurboCompressorSpeed:F0}," +
                                  $"{param.OilPressure:F2}," +
                                  $"{param.BoostPressure:F2}," +
                                  $"{param.OilTemperature:F1}," +
                                  $"{param.RackPosition}");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString());
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

                // Заголовок CSV
                sb.AppendLine("Время,Система,Сообщение,Подробности");

                // Данные
                foreach (var evt in events)
                {
                    // Экранируем кавычками поля, которые могут содержать запятые
                    string system = evt.System?.Replace("\"", "\"\"");
                    string message = evt.Message?.Replace("\"", "\"\"");
                    string details = evt.Details?.Replace("\"", "\"\"");

                    sb.AppendLine($"{evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                  $"\"{system}\"," +
                                  $"\"{message}\"," +
                                  $"\"{details}\"");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString());
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
                sb.AppendLine("Время,Тип,Сообщение,Подробности");

                // Данные
                foreach (var log in logs)
                {
                    // Экранируем кавычками поля, которые могут содержать запятые
                    string type = log.Type?.Replace("\"", "\"\"");
                    string message = log.Message?.Replace("\"", "\"\"");
                    string details = log.Details?.Replace("\"", "\"\"");

                    sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                  $"\"{type}\"," +
                                  $"\"{message}\"," +
                                  $"\"{details}\"");
                }

                // Запись в файл
                await File.WriteAllTextAsync(filePath, sb.ToString());
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
                    ExportTime = DateTime.Now
                };

                // Сериализуем в JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(settings, options);

                // Записываем в файл
                await File.WriteAllTextAsync(filePath, json);
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
                html.AppendLine("    </style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");

                // Заголовок отчета
                html.AppendLine($"    <h1>Отчет о состоянии системы испытаний регуляторов ЭРЧМ30ТЗ</h1>");
                html.AppendLine($"    <p>Дата и время формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>");

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
                html.AppendLine("    </table>");

                // Раздел настроек защит
                html.AppendLine("    <h2>Настройки защит</h2>");
                html.AppendLine("    <table>");
                html.AppendLine("        <tr><th>Параметр</th><th>Значение</th></tr>");
                html.AppendLine($"        <tr><td>Минимальное давление масла</td><td>{settingsService.ProtectionThresholds.OilPressureMinThreshold:F2} кг/см²</td></tr>");
                html.AppendLine($"        <tr><td>Максимальные обороты двигателя</td><td>{settingsService.ProtectionThresholds.EngineSpeedMaxThreshold:F0} об/мин</td></tr>");
                html.AppendLine($"        <tr><td>Максимальное давление наддува</td><td>{settingsService.ProtectionThresholds.BoostPressureMaxThreshold:F2} кг/см²</td></tr>");
                html.AppendLine($"        <tr><td>Максимальная температура масла</td><td>{settingsService.ProtectionThresholds.OilTemperatureMaxThreshold:F1} °C</td></tr>");
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
                    html.AppendLine($"            <td>{log.Timestamp:dd.MM.yyyy HH:mm:ss}</td>");
                    html.AppendLine($"            <td>{log.Type}</td>");
                    html.AppendLine($"            <td>{log.Message}</td>");
                    html.AppendLine($"            <td>{log.Details}</td>");
                    html.AppendLine($"        </tr>");
                }

                html.AppendLine("    </table>");

                // Подвал отчета
                html.AppendLine("    <div class=\"footer\">");
                html.AppendLine("        <p>Система испытаний регуляторов ЭРЧМ30ТЗ © ОАО \"Коломенский завод\"</p>");
                html.AppendLine("    </div>");

                // Закрытие HTML
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                // Запись в файл
                await File.WriteAllTextAsync(filePath, html.ToString());
                _loggingService.LogInfo("Экспорт отчета о состоянии системы в HTML", filePath);

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Ошибка экспорта отчета о состоянии системы", ex.Message);
                return false;
            }
        }
    }
}