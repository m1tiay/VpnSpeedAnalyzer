using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
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
        private const int MaxAttempts = 3;
        private const int RetryDelayMs = 5000;

        private static readonly Regex PercentRx = new(@"(?<p>\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string? LastFailureReason { get; private set; }

        public SpeedtestService()
        {
            Logger.Write("SpeedtestService конструктор вызван");
        }

        public async Task<SpeedtestResult?> RunAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            LastFailureReason = null;
            try
            {
                var speedtestPath = GetSpeedtestPath();
                if (speedtestPath == null)
                {
                    Logger.Write("Утилита Speedtest не найдена в PATH или папке приложения");
                    LastFailureReason = "Утилита speedtest.exe не найдена";
                    return null;
                }

                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    var attemptStartedUtc = DateTime.UtcNow;
                    var args = "--format=json --accept-license --accept-gdpr";
                    var psi = new ProcessStartInfo
                    {
                        FileName = speedtestPath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Logger.Write(
                        $"Speedtest запуск: попытка {attempt}/{MaxAttempts}, file=\"{psi.FileName}\", args=\"{psi.Arguments}\", cwd=\"{AppContext.BaseDirectory}\"");

                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        Logger.Write("Не удалось запустить процесс Speedtest");
                        LastFailureReason = "Не удалось запустить процесс speedtest";
                        return null;
                    }

                    using var killReg = cancellationToken.Register(() => TryKill(proc));

                    var progState = new ProgressState();
                    progState.BumpToAtLeast(4, progress);
                    var stderrTail = new RollingTail(14);

                    using var hbCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    Task stderrPump = StderrPumpAsync(proc.StandardError, progState, progress, stderrTail, cancellationToken);
                    Task heartbeat = HeartbeatAsync(proc, progState, progress, hbCts.Token);

                    string output;
                    try
                    {
                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(ProcessTimeoutMs);

                        output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                        await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Write($"Процесс Speedtest превысил таймаут: {ProcessTimeoutMs}мс (попытка {attempt}/{MaxAttempts})");
                        Logger.Write($"Speedtest stderr (tail): {stderrTail.ToSingleLine()}");
                        TryKill(proc);
                        LastFailureReason = $"Таймаут speedtest ({ProcessTimeoutMs / 1000} сек)";
                        hbCts.Cancel();
                        await AwaitPump(stderrPump, heartbeat).ConfigureAwait(false);
                        progress?.Report(0);
                        if (attempt < MaxAttempts)
                            await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    hbCts.Cancel();
                    await AwaitPump(stderrPump, heartbeat).ConfigureAwait(false);

                    if (proc.ExitCode != 0)
                    {
                        var elapsedMs = (DateTime.UtcNow - attemptStartedUtc).TotalMilliseconds;
                        Logger.Write(
                            $"Процесс Speedtest завершился с кодом {proc.ExitCode} (попытка {attempt}/{MaxAttempts}, elapsed={elapsedMs:F0}мс)");
                        Logger.Write($"Speedtest stderr (tail): {stderrTail.ToSingleLine()}");
                        Logger.Write($"Speedtest stdout (tail): {ToTailSingleLine(output)}");
                        LastFailureReason = $"Код завершения speedtest: {proc.ExitCode}";
                        progress?.Report(0);
                        if (attempt < MaxAttempts)
                            await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        Logger.Write($"Speedtest вернул пустой результат (попытка {attempt}/{MaxAttempts})");
                        Logger.Write($"Speedtest stderr (tail): {stderrTail.ToSingleLine()}");
                        LastFailureReason = "Speedtest вернул пустой результат";
                        progress?.Report(0);
                        if (attempt < MaxAttempts)
                            await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var parsed = ParseSpeedtestOutput(output);
                    if (parsed != null)
                    {
                        var elapsedMs = (DateTime.UtcNow - attemptStartedUtc).TotalMilliseconds;
                        Logger.Write($"Speedtest успешно завершён (попытка {attempt}/{MaxAttempts}, elapsed={elapsedMs:F0}мс)");
                        progState.BumpToAtLeast(100, progress);
                        return parsed;
                    }

                    Logger.Write($"Не удалось распарсить результат speedtest (попытка {attempt}/{MaxAttempts})");
                    Logger.Write($"Speedtest stderr (tail): {stderrTail.ToSingleLine()}");
                    Logger.Write($"Speedtest stdout (tail): {ToTailSingleLine(output)}");
                    LastFailureReason = "Не удалось распарсить ответ speedtest";
                    progress?.Report(0);
                    if (attempt < MaxAttempts)
                        await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                }

                Logger.Write("Все попытки speedtest завершились неуспешно");
                LastFailureReason ??= "Все попытки speedtest завершились неуспешно";
                return null;
            }
            catch (FileNotFoundException ex)
            {
                Logger.Write($"Ошибка Speedtest: {ex.Message}");
                LastFailureReason = ex.Message;
                return null;
            }
            catch (InvalidOperationException ex)
            {
                Logger.Write($"Ошибка процесса Speedtest: {ex.Message}");
                LastFailureReason = ex.Message;
                return null;
            }
            catch (OperationCanceledException)
            {
                Logger.Write("Speedtest отменён");
                LastFailureReason ??= "Замер отменён";
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"Неожиданная ошибка Speedtest: {ex.GetType().Name}: {ex.Message}");
                LastFailureReason = ex.Message;
                return null;
            }
        }

        private static async Task AwaitPump(Task stderrPump, Task heartbeat)
        {
            try
            {
                await Task.WhenAll(
                    Task.WhenAny(stderrPump, Task.Delay(800)),
                    Task.WhenAny(heartbeat, Task.Delay(800))).ConfigureAwait(false);
            }
            catch
            {
                // Игнорируем сбой вспомогательных задач — основной результат уже получен или таймаут.
            }
        }

        /// <summary>
        /// Читает stderr построчно; при отсутствии явных процентов подсказываем фазы по ключевым словам.
        /// </summary>
        private static async Task StderrPumpAsync(
            StreamReader stderr,
            ProgressState state,
            IProgress<double>? progress,
            RollingTail tail,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await stderr.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line == null)
                        break;

                    tail.Add(line);
                    state.BumpByOutputLine(line, progress);
                }
            }
            catch
            {
                // Поток мог быть оборван при Kill — нормально.
            }
        }

        /// <summary>
        /// Если CLI молчит в JSON-режиме, чуть двигаем шкалу, чтобы не казалось «зависло».
        /// </summary>
        private static async Task HeartbeatAsync(
            Process proc,
            ProgressState state,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(2800, cancellationToken).ConfigureAwait(false);
                    if (proc.HasExited)
                        break;

                    // Не перепрыгиваем реальные проценты из stderr; около 95% оставляем до завершения.
                    state.BumpHeart(progress);
                }
            }
            catch (OperationCanceledException)
            {
                // отмена heartbeat — ожидаемо
            }
        }

        private static void TryKill(Process proc)
        {
            try
            {
                if (proc.HasExited)
                    return;
                proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // Игнорируем — процесс мог уже завершиться.
            }
        }

        private static string ToTailSingleLine(string? text, int maxLines = 8, int maxChars = 1200)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "<empty>";

            var lines = text
                .Replace("\r", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var tail = lines.TakeLast(Math.Max(1, maxLines));
            var joined = string.Join(" | ", tail).Trim();
            if (joined.Length > maxChars)
                joined = joined.Substring(0, maxChars) + "...";

            return string.IsNullOrWhiteSpace(joined) ? "<empty>" : joined;
        }

        private sealed class RollingTail
        {
            private readonly int _capacity;
            private readonly Queue<string> _lines;

            public RollingTail(int capacity)
            {
                _capacity = Math.Max(1, capacity);
                _lines = new Queue<string>(_capacity);
            }

            public void Add(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                if (_lines.Count >= _capacity)
                    _lines.Dequeue();

                _lines.Enqueue(line.Trim());
            }

            public string ToSingleLine(int maxChars = 1200)
            {
                if (_lines.Count == 0)
                    return "<empty>";

                var text = string.Join(" | ", _lines);
                if (text.Length > maxChars)
                    text = text.Substring(0, maxChars) + "...";

                return text;
            }
        }

        private sealed class ProgressState
        {
            private readonly object _gate = new();
            private double _max;
            private int _lineSteps;

            public void BumpToAtLeast(double value, IProgress<double>? p)
            {
                double toReport;
                lock (_gate)
                {
                    value = Math.Clamp(value, 0, 100);
                    if (value <= _max)
                        return;
                    _max = value;
                    toReport = _max;
                }

                p?.Report(toReport);
            }

            public void BumpHeart(IProgress<double>? p)
            {
                lock (_gate)
                {
                    if (_max >= 95)
                        return;

                    var next = Math.Min(95, _max + 2);
                    if (next <= _max)
                        return;
                    _max = next;
                    p?.Report(_max);
                }
            }

            public double BumpByOutputLine(string line, IProgress<double>? p)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return _max;

                lock (_gate)
                {
                    if (!IsSignificantProgressLine(line))
                        return _max;

                    // Привязываем прогресс к ключевым строкам вывода speedtest: около 10 шагов по 10%.
                    if (_lineSteps < 10)
                        _lineSteps++;

                    var byLine = Math.Min(95, _lineSteps * 10.0);
                    if (byLine <= _max)
                        return byLine;

                    _max = byLine;
                    p?.Report(_max);
                    return byLine;
                }
            }

            private static bool IsSignificantProgressLine(string line)
            {
                var s = line.Trim().ToLowerInvariant();
                if (s.Length == 0)
                    return false;

                return s.StartsWith("speedtest by ookla", StringComparison.Ordinal)
                       || s.StartsWith("server:", StringComparison.Ordinal)
                       || s.StartsWith("isp:", StringComparison.Ordinal)
                       || s.StartsWith("idle latency:", StringComparison.Ordinal)
                       || s.StartsWith("download:", StringComparison.Ordinal)
                       || s.StartsWith("upload:", StringComparison.Ordinal)
                       || s.StartsWith("packet loss:", StringComparison.Ordinal)
                       || s.StartsWith("result url:", StringComparison.Ordinal)
                       || s.Contains("latency")
                       || PercentRx.IsMatch(s);
            }
        }

        /// <summary>
        /// Ищет исполняемый файл Speedtest в PATH или в папке приложения
        /// </summary>
        private static string? GetSpeedtestPath()
        {
            var appDirPath = Path.Combine(AppContext.BaseDirectory, "speedtest.exe");
            if (File.Exists(appDirPath))
                return appDirPath;

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
                    continue;
                }
            }

            return null;
        }

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
