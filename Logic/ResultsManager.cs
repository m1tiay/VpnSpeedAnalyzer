using System.Collections.ObjectModel;
using System.Linq;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Logic
{
    public class ResultsManager
    {
        private readonly ObservableCollection<ResultEntry> _results;

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            _results = results;
        }

        public void ApplyBestOnly()
        {
            var best = _results
                .OrderBy(r => r.Score)
                .Take(5)
                .ToList();

            _results.Clear();

            foreach (var r in best)
                _results.Add(r);
        }
    }
}
