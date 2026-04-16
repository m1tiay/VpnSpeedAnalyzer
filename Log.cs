using System;
using System.IO;

namespace VpnSpeedAnalyzer
{
    public static class Log
    {
        private static readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static readonly string LogFile = Path.Combine(LogDir, "app.log");

        static Log()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
            }
            catch
            {
                // игнорируем ошибки логирования, чтобы не ломать приложение
            }
        }

        public static void Info(string msg) => Write("INFO", msg);

        public static void Error(string msg) => Write("ERROR", msg);

        private static void Write(string level, string msg)
        {
            try
            {
                File.AppendAllText(LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
            }
            catch
            {
                // никогда не роняем приложение из-за логов
            }
        }
    }
}
