using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer.Services
{
    /// <summary>
    /// Сервис для запуска утилиты Speedtest и парсинга результатов
    /// </summary>
    public class SpeedtestService : ISpeedtestService
    {
        // Магическое число: 125000 = 1,000,000 бит / 8 байт (конвертация битов в Мбит/с)
        private const double BitsPerMbpsConversionFactor = 125000.0;
        private const int ProcessTimeoutMs = 300000; // 5 минут

        public SpeedtestService()
        {
            Logger.Write("SpeedtestService конструктор вызван");
        }

        public async Task<SpeedtestResult?> RunAsync()
        {
            try
            {
                // Проверяем что утилита Speedtest доступна
                var speedtestPath = GetSpeedtestPath();
                if (speedtestPath == null)
                {
                    Logger.Write("Утилита Speedtest не найдена в PATH или папке приложения");
                    return null;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = speedtestPath,
                    Arguments = "--format=json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    Logger.Write("Не удалось запустить процесс Speedtest");
                    return null;
                }

                string output = await proc.StandardOutput.ReadToEndAsync()
                    .ConfigureAwait(false);
                
                if (!proc.WaitForExit(ProcessTimeoutMs))
                {
                    proc.Kill();
                    Logger.Write($"Процесс Speedtest превысил таймаут: {ProcessTimeoutMs}мс");
                    return null;
                }

                if (proc.ExitCode != 0)
                {
                    Logger.Write($"Процесс Speedtest завершился с кодом {proc.ExitCode}");
                    return null;
                }

                if (string.IsNullOrEmpty(output))
                {
                    Logger.Write("Speedtest вернул пустой результат");
                    return null;
                }

                return ParseSpeedtestOutput(output);
            }
            catch (FileNotFoundException ex)
            {
                Logger.Write($"Ошибка Speedtest: {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                Logger.Write($"Ошибка процесса Speedtest: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"Неожиданная ошибка Speedtest: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ищет исполняемый файл Speedtest в PATH или в папке приложения
        /// </summary>
        /// <returns>Полный путь к исполняемому файлу Speedtest или null если не найдено</returns>
        private static string? GetSpeedtestPath()
        {
            // Сначала проверяем папку приложения
            var appDirPath = Path.Combine(AppContext.BaseDirectory, "speedtest.exe");
            if (File.Exists(appDirPath))
                return appDirPath;

            // Затем проверяем переменную окружения PATH
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar))
                return null;

            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(dir, "speedtest.exe");
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch (ArgumentException)
                {
                    // Некорректные символы в пути - пропускаем эту папку
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Парсит JSON вывод Speedtest в объект SpeedtestResult
        /// </summary>
        private static SpeedtestResult? ParseSpeedtestOutput(string json)
        {
            try
            {
                var raw = JsonSerializer.Deserialize<SpeedtestRaw>(json);
                if (raw == null)
                {
                    Logger.Write("Не удалось десериализовать JSON от Speedtest");
                    return null;
                }

                // Проверяем что все обязательные поля присутствуют
                if (raw.Interface?.ExternalIp == null ||
                    raw.Server?.Country == null ||
                    raw.Ping == null ||
                    raw.Download == null ||
                    raw.Upload == null)
                {
                    Logger.Write("В результате Speedtest отсутствуют обязательные поля");
                    return null;
                }

                return new SpeedtestResult
                {
                    Ip = raw.Interface.ExternalIp ?? "",
                    Country = raw.Server.Country ?? "",
                    Timestamp = DateTime.Now,
                    Ping = raw.Ping.Latency,
                    Jitter = raw.Ping.Jitter,
                    Loss = raw.PacketLoss,
                    Download = raw.Download.Bandwidth / BitsPerMbpsConversionFactor,
                    Upload = raw.Upload.Bandwidth / BitsPerMbpsConversionFactor
                };
            }
            catch (JsonException ex)
            {
                Logger.Write($"Ошибка при парсинге JSON от Speedtest: {ex.Message}");
                return null;
            }
        }
    }

    public class SpeedtestRaw
    {
        [JsonPropertyName("ping")]
        public PingData Ping { get; set; } = new();

        [JsonPropertyName("download")]
        public BandwidthData Download { get; set; } = new();

        [JsonPropertyName("upload")]
        public BandwidthData Upload { get; set; } = new();

        [JsonPropertyName("packetLoss")]
        public double PacketLoss { get; set; }

        [JsonPropertyName("interface")]
        public InterfaceData Interface { get; set; } = new();

        [JsonPropertyName("server")]
        public ServerData Server { get; set; } = new();

        public class PingData
        {
            [JsonPropertyName("latency")]
            public double Latency { get; set; }

            [JsonPropertyName("jitter")]
            public double Jitter { get; set; }
        }

        public class BandwidthData
        {
            [JsonPropertyName("bandwidth")]
            public double Bandwidth { get; set; }
        }

        public class InterfaceData
        {
            [JsonPropertyName("externalIp")]
            public string? ExternalIp { get; set; }
        }

        public class ServerData
        {
            [JsonPropertyName("country")]
            public string? Country { get; set; }
        }
    }
}
