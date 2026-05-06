using System;
using System.Collections.Generic;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Сервис расчёта оценки качества канала D.Q.S и работы с профилями.
    /// </summary>
    public sealed class ScoringService
    {
        public const string ProfileUniversal = "Универсальный";
        public const string ProfileGaming = "Игры";
        public const string ProfileStreaming = "Стрим";

        // Пороги нормализации каждой метрики в шкалу 0..100.
        private const double PingIdeal = 20;
        private const double PingWorst = 150;
        private const double JitterIdeal = 2;
        private const double JitterWorst = 40;
        private const double LossIdeal = 0;
        private const double LossWorst = 5;
        private const double DownloadIdeal = 400;
        private const double DownloadWorst = 20;
        private const double UploadIdeal = 150;
        private const double UploadWorst = 10;

        /// <summary>
        /// Список доступных профилей оценки.
        /// </summary>
        public IReadOnlyList<string> AvailableProfiles { get; } = new[]
        {
            ProfileUniversal,
            ProfileGaming,
            ProfileStreaming
        };

        /// <summary>
        /// Активный профиль. Влияет на веса метрик в формуле D.Q.S.
        /// </summary>
        public string ActiveProfile { get; set; } = ProfileUniversal;

        /// <summary>
        /// Считает итоговую оценку качества хоста по шкале 0..100.
        /// </summary>
        public double Calculate(ResultEntry result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var pingScore = ScoreLowerIsBetter(result.Ping, PingIdeal, PingWorst);
            var jitterScore = ScoreLowerIsBetter(result.Jitter, JitterIdeal, JitterWorst);
            var lossScore = ScoreLowerIsBetter(result.Loss, LossIdeal, LossWorst);
            var downloadScore = ScoreHigherIsBetter(result.Download, DownloadIdeal, DownloadWorst);
            var uploadScore = ScoreHigherIsBetter(result.Upload, UploadIdeal, UploadWorst);

            var (pingWeight, jitterWeight, lossWeight, downloadWeight, uploadWeight) = GetProfileWeights(ActiveProfile);

            var score =
                pingScore * pingWeight +
                jitterScore * jitterWeight +
                lossScore * lossWeight +
                downloadScore * downloadWeight +
                uploadScore * uploadWeight;

            return Math.Round(score, 2);
        }

        /// <summary>
        /// Текстовая расшифровка балла для блока «Почему этот D.Q.S?».
        /// </summary>
        public string BuildDetails(ResultEntry result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            return $"{ActiveProfile}: ping {Math.Round(result.Ping, 2)} мс, " +
                   $"дрожание {Math.Round(result.Jitter, 2)} мс, " +
                   $"потери {Math.Round(result.Loss, 2)}%, " +
                   $"загрузка {Math.Round(result.Download, 2)} Мбит/с, " +
                   $"отдача {Math.Round(result.Upload, 2)} Мбит/с";
        }

        /// <summary>
        /// Описание текущего активного профиля.
        /// </summary>
        public string Describe() => Describe(ActiveProfile);

        /// <summary>
        /// Описание заданного профиля.
        /// </summary>
        public static string Describe(string profile) => profile switch
        {
            ProfileGaming => "Игры: максимальный приоритет низкому пингу, дрожанию и потерям. Скорости учитываются меньше.",
            ProfileStreaming => "Стрим: повышенный вес загрузки и отдачи при сохранении умеренных требований к задержке.",
            _ => "Универсальный: сбалансированный профиль для общего использования интернета и VPN."
        };

        private static (double ping, double jitter, double loss, double download, double upload) GetProfileWeights(string profile) =>
            profile switch
            {
                ProfileGaming => (0.42, 0.28, 0.20, 0.06, 0.04),
                ProfileStreaming => (0.20, 0.10, 0.15, 0.35, 0.20),
                _ => (0.30, 0.20, 0.25, 0.15, 0.10)
            };

        private static double ScoreLowerIsBetter(double value, double ideal, double worst)
        {
            if (value <= ideal) return 100;
            if (value >= worst) return 0;

            var ratio = (value - ideal) / (worst - ideal);
            return Math.Round((1 - ratio) * 100, 2);
        }

        private static double ScoreHigherIsBetter(double value, double ideal, double worst)
        {
            if (value >= ideal) return 100;
            if (value <= worst) return 0;

            var ratio = (value - worst) / (ideal - worst);
            return Math.Round(ratio * 100, 2);
        }
    }
}
