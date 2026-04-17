using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Services
{
    /// <summary>
    /// Интерфейс для получения информации об IP адресе
    /// </summary>
    public interface IIpInfoService
    {
        /// <summary>
        /// Получает текущую информацию об IP адресе асинхронно
        /// </summary>
        /// <returns>Информация об IP адресе или null если запрос не удался</returns>
        Task<IpInfo?> GetCurrentAsync();
    }
}
