namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Представляет одну запись результата теста скорости
    /// </summary>
    public class ResultEntry
    {
        /// <summary>Источник запуска замера (manual/auto_host/auto_timer/auto_start)</summary>
        public string TriggerKind { get; set; } = "";

        /// <summary>Был ли этот замер последним выполненным</summary>
        public bool IsLatestMeasurement { get; set; }

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

        /// <summary>Скорость загрузки в Мбит/с</summary>
        public double Download { get; set; }

        /// <summary>Скорость отдачи в Мбит/с</summary>
        public double Upload { get; set; }

        /// <summary>Итоговая оценка качества хоста по шкале 0..100 (больше - лучше)</summary>
        public double Score { get; set; }

        /// <summary>Короткое пояснение, почему получен такой итоговый балл</summary>
        public string ScoreDetails { get; set; } = "";

        /// <summary>Позиция в рейтинге (1 - лучший)</summary>
        public int Rank { get; set; }

        /// <summary>Символ маркера в таблице рейтинга</summary>
        public string RankMarker { get; set; } = "";

        /// <summary>HEX-цвет маркера в таблице рейтинга</summary>
        public string RankMarkerColor { get; set; } = "#A8B0D9";

        /// <summary>Подсказка для маркера в таблице мониторинга</summary>
        public string RankMarkerToolTip { get; set; } = "";
    }
}
