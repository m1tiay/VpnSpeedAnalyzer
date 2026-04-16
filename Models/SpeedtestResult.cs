using System;

namespace VpnSpeedAnalyzer.Models
{
    public class SpeedtestResult
    {
        public string Ip { get; set; } = "";
        public string Country { get; set; } = "";

        public DateTime Timestamp { get; set; }

        public double Ping { get; set; }
        public double Jitter { get; set; }
        public double Loss { get; set; }
        public double Download { get; set; }
        public double Upload { get; set; }
    }
}
