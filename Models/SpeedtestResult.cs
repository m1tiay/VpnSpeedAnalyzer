using System;

namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Результат одного теста скорости
    /// </summary>
    public class SpeedtestResult
    {
        /// <summary>Внешний IP адрес при тестировании</summary>
        public string Ip { get; set; } = "";

        /// <summary>Страна где находится сервер теста</summary>
        public string Country { get; set; } = "";

        /// <summary>Код страны (ISO alpha-2), например DE</summary>
        public string CountryCode { get; set; } = "";

        /// <summary>Номер автономной системы (ASN) текущего внешнего IP</summary>
        public string Asn { get; set; } = "";

        /// <summary>Когда был выполнен тест</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Латентность ping в мс</summary>
        public double Ping { get; set; }

        /// <summary>Джиттер в мс</summary>
        public double Jitter { get; set; }

        /// <summary>Потеря пакетов в процентах</summary>
        public double Loss { get; set; }

        /// <summary>Скорость загружки в Мбит/с</summary>
        public double Download { get; set; }

        /// <summary>Скорость выгружки в Мбит/с</summary>
        public double Upload { get; set; }
    }
}
