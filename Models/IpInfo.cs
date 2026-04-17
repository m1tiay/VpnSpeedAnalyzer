namespace VpnSpeedAnalyzer.Models
{
    /// <summary>
    /// Текущая информация аб IP адресе полученная от ipapi.co
    /// </summary>
    public class IpInfo
    {
        /// <summary>Внешний IP адрес</summary>
        public string Ip { get; set; } = "";

        /// <summary>Наименование страны</summary>
        public string CountryName { get; set; } = "";
    }
}
