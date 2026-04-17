using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Deprecated: Use Logger class from VpnSpeedAnalyzer.Logic instead
    /// This class is kept for backward compatibility only
    /// </summary>
    [System.Obsolete("Use Logger from VpnSpeedAnalyzer.Logic namespace instead", false)]
    public static class Log
    {
        public static void Info(string msg) => Logger.Write($"INFO: {msg}");
        public static void Error(string msg) => Logger.Write($"ERROR: {msg}");
    }
}
