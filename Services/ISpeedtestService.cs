using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Services
{
    /// <summary>
    /// Интерфейс для запуска тестов скорости интернета
    /// </summary>
    public interface ISpeedtestService
    {
        /// <summary>
        /// Запускает тест скорости асинхронно
        /// </summary>
        /// <returns>Результат теста скорости или null если тест не удался</returns>
        Task<SpeedtestResult?> RunAsync();
    }
}
