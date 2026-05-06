using System.Threading;
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
        /// Причина последней неудачи speedtest (если была)
        /// </summary>
        string? LastFailureReason { get; }

        /// <summary>
        /// Запускает тест скорости асинхронно.
        /// </summary>
        /// <param name="progress">Проценты 0..100 по stderr/фазам (приблизительно).</param>
        /// <param name="cancellationToken">Отмена — завершение процесса speedtest.</param>
        /// <returns>Результат теста скорости или null если тест не удался</returns>
        Task<SpeedtestResult?> RunAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    }
}
