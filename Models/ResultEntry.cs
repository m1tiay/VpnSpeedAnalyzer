namespace VpnSpeedAnalyzer
{
    public class ResultEntry
    {
        public string Ip { get; set; }
        public string Country { get; set; }
        public double Ping { get; set; }
        public double Jitter { get; set; }
        public double Loss { get; set; }
        public double Download { get; set; }
        public double Upload { get; set; }
        public string Timestamp { get; set; }

        public double Score =>
            Jitter * 10 +
            Loss * 100 +
            Ping * 0.5 -
            Download * 0.01;
    }
}
