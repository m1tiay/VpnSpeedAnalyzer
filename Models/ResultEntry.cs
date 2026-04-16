namespace VpnSpeedAnalyzer.Models
{
    public class ResultEntry
    {
        public string Ip { get; set; } = "";
        public string Country { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public double Ping { get; set; }
        public double Jitter { get; set; }
        public double Loss { get; set; }
        public double Download { get; set; }
        public double Upload { get; set; }

        // Используется ResultsManager для сортировки
        public double Score => Ping + Jitter + Loss;
    }
}
