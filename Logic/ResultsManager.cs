using System;
using System.Collections.ObjectModel;
using System.Linq;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Управляет операциями с коллекцией результатов тестов
    /// </summary>
    public class ResultsManager
    {
        private const int BestResultsCount = 5;

        private readonly ObservableCollection<ResultEntry> _results;

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            _results = results ?? throw new ArgumentNullException(nameof(results));
        }

        /// <summary>
        /// Фильтрует результаты для отображения только лучших (с низким ping)
        /// </summary>
        public void ApplyBestOnly()
        {
            if (_results.Count == 0)
            {
                Logger.Write("Для фильтрации не хватает результатов");
                return;
            }

            var best = _results
                .OrderBy(r => r.Score)
                .Take(BestResultsCount)
                .ToList();

            if (best.Count == _results.Count)
            {
                Logger.Write("Отображаются лучшие результаты");
                return;
            }

            _results.Clear();

            foreach (var r in best)
            {
                _results.Add(r);
            }

            Logger.Write($"Оставлено лучших результатов: {best.Count}");
        }
    }
}
