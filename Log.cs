using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Устарело: используйте класс Logger из VpnSpeedAnalyzer.Logic.
    /// Этот класс оставлен только для обратной совместимости.
    /// </summary>
    [System.Obsolete("Используйте Logger из пространства имен VpnSpeedAnalyzer.Logic", false)]
    public static class Log
    {
        public static void Info(string msg) => Logger.Write($"INFO: {msg}");
        public static void Error(string msg) => Logger.Write($"ERROR: {msg}");
    }
}
