using System;
using System.IO;

namespace VpnSpeedAnalyzer.Logic
{
    public static class Logger
    {
        private static readonly string LogPath =
            Path.Combine(AppContext.BaseDirectory, "log.txt");

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}\n");
            }
            catch
            {
                // Логгер никогда не должен ронять приложение
            }
        }
    }
}
