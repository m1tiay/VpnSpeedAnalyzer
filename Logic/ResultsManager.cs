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
        private bool _isBestOnlyMode;
        private List<ResultEntry>? _allResultsSnapshot;

        public ResultsManager(ObservableCollection<ResultEntry> results)
        {
            Logger.Write("ResultsManager конструктор вызван");
            _results = results ?? throw new ArgumentNullException(nameof(results));
            Logger.Write("ResultsManager: инициализирован");
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

            if (!_isBestOnlyMode)
            {
                _allResultsSnapshot = _results.ToList();
                var best = _allResultsSnapshot
                    .OrderBy(r => r.Score)
                    .Take(BestResultsCount)
                    .ToList();

                _results.Clear();

                foreach (var r in best)
                {
                    _results.Add(r);
                }

                _isBestOnlyMode = true;
                Logger.Write($"Оставлено лучших результатов: {best.Count}");
                return;
            }

            if (_allResultsSnapshot == null)
            {
                Logger.Write("Снимок всех результатов недоступен, нечего восстанавливать");
                return;
            }

            _results.Clear();
            foreach (var r in _allResultsSnapshot)
            {
                _results.Add(r);
            }

            _isBestOnlyMode = false;
            Logger.Write("Восстановлен полный список результатов");
        }
    }
}
