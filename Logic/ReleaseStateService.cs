using System;
using System.IO;
using System.Text.Json;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Хранит версию последнего запуска для поддержки апдейтов между релизами.
    /// </summary>
    public static class ReleaseStateService
    {
        private const int StateSchemaVersion = 1;
        private static readonly string StateDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnSpeedAnalyzer", "state");
        private static readonly string StatePath = Path.Combine(StateDir, "release-state.json");

        public static void HandleStartup()
        {
            try
            {
                Directory.CreateDirectory(StateDir);
                var currentVersion = AppVersionInfo.ProductVersion;
                var previousState = ReadState();
                if (previousState == null)
                {
                    Logger.Write($"ReleaseState: первый запуск версии {currentVersion}");
                }
                else if (!string.Equals(previousState.LastRunVersion, currentVersion, StringComparison.Ordinal))
                {
                    Logger.Write(
                        $"ReleaseState: смена версии {previousState.LastRunVersion} -> {currentVersion}. Состояние сохранено.");
                }

                var nextState = new ReleaseState
                {
                    SchemaVersion = StateSchemaVersion,
                    LastRunVersion = currentVersion,
                    LastRunUtc = DateTime.UtcNow
                };
                WriteState(nextState);
            }
            catch (Exception ex)
            {
                Logger.Write($"ReleaseState error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static ReleaseState? ReadState()
        {
            if (!File.Exists(StatePath))
                return null;

            var json = File.ReadAllText(StatePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<ReleaseState>(json);
        }

        private static void WriteState(ReleaseState state)
        {
            var json = JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            File.WriteAllText(StatePath, json);
        }

        private sealed class ReleaseState
        {
            public int SchemaVersion { get; set; }
            public string LastRunVersion { get; set; } = "0.0.0";
            public DateTime LastRunUtc { get; set; }
        }
    }
}
