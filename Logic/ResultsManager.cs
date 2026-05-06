using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Управляет операциями с коллекцией результатов тестов
    /// </summary>
    public class ResultsManager
    {
        private readonly ObservableCollection<ResultEntry> _results;
        private readonly List<ResultEntry> _allResults = new();

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            Logger.Write("ResultsManager конструктор вызван");
            _results = results ?? throw new ArgumentNullException(nameof(results));
            Logger.Write("ResultsManager: инициализирован");
        }

        public IReadOnlyList<ResultEntry> AllResults => _allResults;

        public void AddResult(ResultEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _allResults.Add(entry);
            RebuildVisibleResults();
        }

        public ResultEntry? GetRecommendedResult() =>
            _allResults
                .OrderByDescending(r => r.Score)
                .FirstOrDefault();

        public List<ResultEntry> GetTopResults(int count) =>
            _allResults
                .OrderByDescending(r => r.Score)
                .Take(Math.Max(0, count))
                .ToList();

        public void RecalculateScores(Func<ResultEntry, double> scoreSelector, Func<ResultEntry, string> detailsSelector)
        {
            foreach (var entry in _allResults)
            {
                entry.Score = scoreSelector(entry);
                entry.ScoreDetails = detailsSelector(entry);
            }

            RebuildVisibleResults();
        }

        private void RebuildVisibleResults()
        {
            _results.Clear();
            foreach (var entry in _allResults.OrderByDescending(r => r.Score))
                _results.Add(entry);
        }
    }
}
