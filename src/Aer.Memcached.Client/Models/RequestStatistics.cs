using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models
{
    /// <summary>
    /// Represents object for <see cref="IErrorStatisticsStore"/> to operate with
    /// </summary>
    public class RequestStatistics
    {
        /// <summary>
        /// Current time frame statistics
        /// </summary>
        public TimeFrameStatistics TimeFrameStatistics { get; init; }

        /// <summary>
        /// Flag is set to true if the current number of errors within interval exceeds max number of errors
        /// </summary>
        public bool IsTooManyErrors { get; init; }
    }
}