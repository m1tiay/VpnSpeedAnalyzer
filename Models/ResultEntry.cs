namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Представляет одну запись результата теста скорости
    /// </summary>
    public class ResultEntry
    {
        /// <summary>Внешний IP адрес</summary>
        public string Ip { get; set; } = "";

        /// <summary>Наименование страны</summary>
        public string Country { get; set; } = "";

        /// <summary>Время теста (ISO8601)</summary>
        public string Timestamp { get; set; } = "";

        /// <summary>Латентность ping в миллисекундах</summary>
        public double Ping { get; set; }

        /// <summary>Джиттер (вариация ping) в мс</summary>
        public double Jitter { get; set; }

        /// <summary>Потеря пакетов в процентах</summary>
        public double Loss { get; set; }

        /// <summary>Скорость загружки в Мбит/с</summary>
        public double Download { get; set; }

        /// <summary>Скорость загружки в Мбит/с</summary>
        public double Upload { get; set; }

        /// <summary>Итоговая оценка качества хоста по шкале 0..100 (больше - лучше)</summary>
        public double Score { get; set; }

        /// <summary>Короткое пояснение, почему получен такой итоговый балл</summary>
        public string ScoreDetails { get; set; } = "";

        /// <summary>Позиция в рейтинге (1 - лучший)</summary>
        public int Rank { get; set; }

        /// <summary>Медаль для топ-3</summary>
        public string RankBadge { get; set; } = "";
    }
}
