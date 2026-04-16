using System;

namespace VpnSpeedAnalyzer
{
    public class SpeedtestResult
    {
        public double Ping { get; set; }
        public double Jitter { get; set; }
        public double Loss { get; set; }
        public double Download { get; set; }
        public double Upload { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
