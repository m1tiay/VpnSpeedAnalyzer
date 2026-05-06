namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Агрегированная строка аналитического рейтинга по хосту.
    /// </summary>
    public class HostRatingRow
    {
        public int Rank { get; set; }
        public string Ip { get; set; } = "";
        public string Country { get; set; } = "";
        public int Samples { get; set; }
        public double AverageScore { get; set; }
        public double BestScore { get; set; }
        public double AveragePing { get; set; }
        public double AverageJitter { get; set; }
        public double AverageLoss { get; set; }
    }
}
