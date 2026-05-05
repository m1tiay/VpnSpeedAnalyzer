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
        private static readonly string LogDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnSpeedAnalyzer", "logs");
        private static bool _cleanupDone;

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                CleanupOldLogsIfNeeded();
                var logPath = GetCurrentLogPath();

                if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogSizeBytes)
                    RotateCurrentLog(logPath);

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

        private static string GetCurrentLogPath() =>
            Path.Combine(LogDir, $"app-{DateTime.Now:yyyyMMdd}.log");

        private static void RotateCurrentLog(string logPath)
        {
            var archivePath = Path.Combine(
                Path.GetDirectoryName(logPath) ?? LogDir,
                $"{Path.GetFileNameWithoutExtension(logPath)}-{DateTime.Now:HHmmss}.log");

            try
            {
                File.Move(logPath, archivePath, overwrite: true);
            }
            catch
            {
                // Если переименование не удалось (например, файл занят), начинаем новый пустой лог.
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
