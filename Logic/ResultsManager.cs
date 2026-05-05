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
        private const int BestResultsCount = 5;

        private readonly ObservableCollection<ResultEntry> _results;
        private readonly List<ResultEntry> _allResults = new();
        private bool _isBestOnlyMode;

        public bool IsBestOnlyMode => _isBestOnlyMode;

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            Logger.Write("ResultsManager конструктор вызван");
            _results = results ?? throw new ArgumentNullException(nameof(results));
            Logger.Write("ResultsManager: инициализирован");
        }

        public void AddResult(ResultEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _allResults.Add(entry);

            if (_isBestOnlyMode)
            {
                RebuildVisibleResults(GetBestResults());
                return;
            }

            _results.Add(entry);
        }

        /// <summary>
        /// Переключает режим: полный список / только лучшие результаты
        /// </summary>
        public void ToggleBestOnly()
        {
            if (_allResults.Count == 0)
            {
                Logger.Write("Для фильтрации не хватает результатов");
                return;
            }

            _isBestOnlyMode = !_isBestOnlyMode;

            if (_isBestOnlyMode)
            {
                RebuildVisibleResults(GetBestResults());
                Logger.Write($"Оставлено лучших результатов: {Math.Min(BestResultsCount, _allResults.Count)}");
                return;
            }

            RebuildVisibleResults(_allResults);
            Logger.Write("Восстановлен полный список результатов");
        }

        private List<ResultEntry> GetBestResults() =>
            _allResults
                .OrderBy(r => r.Score)
                .Take(BestResultsCount)
                .ToList();

        private void RebuildVisibleResults(IEnumerable<ResultEntry> source)
        {
            _results.Clear();
            foreach (var entry in source)
                _results.Add(entry);
        }
    }
}
