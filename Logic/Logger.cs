using System;
using System.Diagnostics;
using System.IO;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Статичный логгер, который выписывает сообщения в файл лога
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "log.txt");

        public static void Write(string message)
        {
            try
            {
                // Ограничение размера лога - 10MB максимум
                const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB
                
                if (File.Exists(LogPath))
                {
                    var fileInfo = new FileInfo(LogPath);
                    if (fileInfo.Length > MaxLogSizeBytes)
                    {
                        // Лог переполнился - стираем и начинаем заново
                        File.Delete(LogPath);
                    }
                }
                
                var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}\n";
                File.AppendAllText(LogPath, logMessage);
            }
            catch (Exception ex)
            {
                // Логгер никогда не должен ломать приложение
                // Но мы можем точно логировать в Окно дебага
                Debug.WriteLine($"Ошибка логирования: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
