namespace Aer.Memcached.Client.Models
{
    /// <summary>
    /// Represents error statistic log for specified interval of time
    /// </summary>
    internal class StatisticLog
    {
        public DateTimeOffset From { get; }

        public DateTimeOffset To { get; }

        public TimeSpan Interval { get; }

        public TimeFrameStatistics TimeFrameStatistics { get; }

        public StatisticLog(DateTimeOffset from, TimeSpan interval)
        {
            From = from;
            To = from.Add(interval);
            Interval = interval;
            TimeFrameStatistics = new TimeFrameStatistics
            {
                NumberOfErrors = 1
            };
        }

        public static StatisticLog CopyFrom(StatisticLog log)
        {
            return new(log.From, log.To, log.Interval, log.TimeFrameStatistics.NumberOfErrors);
        }

        private StatisticLog(DateTimeOffset from, DateTimeOffset to, TimeSpan interval, long numberOfErrors)
        {
            From = from;
            To = to;
            Interval = interval;
            TimeFrameStatistics = new TimeFrameStatistics
            {
                NumberOfErrors = numberOfErrors
            };
        }
    }
}