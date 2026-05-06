using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Статичный логгер, который выписывает сообщения в файл лога
    /// </summary>
    public static class Logger
    {
        private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5MB на один файл
        private const int KeepLogDays = 14;
        private const int MaxDailyLogFiles = 10;
        private static readonly string LogDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnSpeedAnalyzer", "logs");
        private static bool _cleanupDone;
        private static string? _sessionLogPath;

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                CleanupOldLogsIfNeeded();
                var logPath = GetCurrentLogPath();

                if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogSizeBytes)
                    TrimCurrentLog(logPath);

                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}\n";
                File.AppendAllText(logPath, logMessage);
            }
            catch (Exception ex)
            {
                // Логгер никогда не должен ломать приложение
                // Но мы можем точно логировать в Окно дебага
                Debug.WriteLine($"Ошибка логирования: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string GetCurrentLogPath()
        {
            if (!string.IsNullOrWhiteSpace(_sessionLogPath))
                return _sessionLogPath;

            var now = DateTime.Now;
            var prefix = $"app-{now:yyyyMMdd}-";
            var todaysFiles = Directory
                .EnumerateFiles(LogDir, $"{prefix}*.log")
                .Select(path => new FileInfo(path))
                .OrderBy(f => f.CreationTimeUtc)
                .ToList();

            // При каждом запуске процесса создаём новый файл, но держим не более 10 файлов за день.
            while (todaysFiles.Count >= MaxDailyLogFiles)
            {
                var oldest = todaysFiles[0];
                try { oldest.Delete(); }
                catch { break; }
                todaysFiles.RemoveAt(0);
            }

            _sessionLogPath = Path.Combine(LogDir, $"{prefix}{now:HHmmss}-{Environment.ProcessId}.log");
            return _sessionLogPath;
        }

        private static void TrimCurrentLog(string logPath)
        {
            try
            {
                // Чтобы не плодить новые файлы в течение дня, обнуляем текущий файл с маркером ротации.
                File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  [log-rotated] размер превысил лимит {MaxLogSizeBytes / (1024 * 1024)}MB\n");
            }
            catch
            {
                // Если запись не удалась, пробуем хотя бы очистить файл.
                File.WriteAllText(logPath, string.Empty);
            }
        }

        private static void CleanupOldLogsIfNeeded()
        {
            if (_cleanupDone)
                return;

            var threshold = DateTime.Now.AddDays(-KeepLogDays);
            var files = Directory
                .EnumerateFiles(LogDir, "*.log")
                .Select(path => new FileInfo(path));

            foreach (var file in files)
            {
                if (file.LastWriteTime < threshold)
                {
                    try { file.Delete(); }
                    catch { }
                }
            }

            _cleanupDone = true;
        }
    }
}
