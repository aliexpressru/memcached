using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces
{
    public interface IErrorStatisticsStore
    {
        /// <summary>
        /// Gets error statistics
        /// </summary>
        /// <param name="key">Built key for error</param>
        /// <param name="maxErrors">Number of errors allowed within timeframe</param>
        /// <param name="interval">Interval for initialized statistics</param>
        /// <returns></returns>
        Task<ErrorStatistics> GetErrorStatisticsAsync(string key, long maxErrors, TimeSpan interval);
    }
}