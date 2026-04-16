using System;
using System.IO;

namespace VpnSpeedAnalyzer.Logic
{
    public static class Logger
    {
        private static readonly string Path = 
            System.IO.Path.Combine(AppContext.BaseDirectory, "log.txt");

        public static void Write(string msg)
        {
            try
            {
                File.AppendAllText(Path, 
                    $"{DateTime.Now:HH:mm:ss}  {msg}\n");
            }
            catch { }
        }
    }
}
