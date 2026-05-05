namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Текущая информация об IP-адресе, полученная от внешнего API
    /// </summary>
    public class IpInfo
    {
        /// <summary>Внешний IP адрес</summary>
        public string Ip { get; set; } = "";

        /// <summary>Наименование страны</summary>
        public string CountryName { get; set; } = "";

        /// <summary>Номер автономной системы (ASN)</summary>
        public string Asn { get; set; } = "";
    }
}
