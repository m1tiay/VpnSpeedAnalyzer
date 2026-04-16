using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace VpnSpeedAnalyzer
{
    public class ResultsManager
    {
        private readonly ObservableCollection<ResultEntry> _results;
        private bool _bestOnly = false;

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            _results = results;
        }

        public void Add(SpeedtestResult r, IpInfo ip)
        {
            var entry = new ResultEntry
            {
                Ip = ip.Ip,
                Country = ip.CountryName,
                Ping = r.Ping,
                Jitter = r.Jitter,
                Loss = r.Loss,
                Download = r.Download,
                Upload = r.Upload,
                Timestamp = r.Timestamp.ToString("HH:mm:ss")
            };

            _results.Add(entry);

            if (_bestOnly)
                ApplyBestOnly();
        }

        public void ExportCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("IP;Country;Ping;Jitter;Loss;Download;Upload;Time");

            foreach (var r in _results)
            {
                sb.AppendLine($"{r.Ip};{r.Country};{r.Ping};{r.Jitter};{r.Loss};{r.Download};{r.Upload};{r.Timestamp}");
            }

            var fileName = $"results_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv";
            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        public void ToggleBestOnly()
        {
            _bestOnly = !_bestOnly;

            if (_bestOnly)
                ApplyBestOnly();
            // иначе оставляем как есть — данные только в рамках процесса
        }

        private void ApplyBestOnly()
        {
            var best = _results.OrderBy(r => r.Score).Take(5).ToList();

            _results.Clear();
            foreach (var r in best)
                _results.Add(r);
        }
    }
}
