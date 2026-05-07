using System;
using System.Reflection;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Централизованный доступ к версии приложения из метаданных сборки.
    /// </summary>
    public static class AppVersionInfo
    {
        private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        public static string InformationalVersion =>
            EntryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? EntryAssembly.GetName().Version?.ToString()
                ?? "0.0.0";

        public static string ProductVersion
        {
            get
            {
                var v = EntryAssembly.GetName().Version;
                if (v == null)
                    return "0.0.0";
                return $"{Math.Max(0, v.Major)}.{Math.Max(0, v.Minor)}.{Math.Max(0, v.Build)}";
            }
        }
    }
}
