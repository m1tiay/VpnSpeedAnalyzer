namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Агрегированная строка аналитического рейтинга по хосту.
    /// </summary>
    public class HostRatingRow
    {
        public int Rank { get; set; }
        public string RankMarker { get; set; } = "";
        public string RankMarkerColor { get; set; } = "#A8B0D9";
        public string RankMarkerToolTip { get; set; } = "";
        public string Ip { get; set; } = "";
        public string Country { get; set; } = "";
        public int Samples { get; set; }
        public double AverageScore { get; set; }
        public double BestScore { get; set; }
        public double AveragePing { get; set; }
        public double AverageJitter { get; set; }
        public double AverageLoss { get; set; }

        /// <summary>Средняя скорость загрузки по замерам хоста (Мбит/с).</summary>
        public double AverageDownloadMbps { get; set; }

        /// <summary>Средняя скорость отдачи по замерам хоста (Мбит/с).</summary>
        public double AverageUploadMbps { get; set; }
    }
}
