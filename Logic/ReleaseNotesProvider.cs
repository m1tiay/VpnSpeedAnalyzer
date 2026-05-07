namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Краткие заметки текущего релиза для окна «О приложении».
    /// </summary>
    public static class ReleaseNotesProvider
    {
        public static string GetCurrent()
        {
            return
                "- Улучшен детект смены VPN-хоста (PRIMARY/TAIL, debounce, cooldown).\n" +
                "- Автозамер запускается только для PRIMARY-событий.\n" +
                "- Доработаны KPI и подсказки в аналитике.\n" +
                "- Улучшены логи speedtest и обработка остановки во время замера.";
        }
    }
}
