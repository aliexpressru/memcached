using System.Collections.Concurrent;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.CacheSync
{
    /// <summary>
    /// Represents error statistic store with sliding window algorithm
    /// </summary>
    public class SlidingWindowStatisticsStore: IErrorStatisticsStore
    {
        private readonly ConcurrentDictionary<string, WindowStatistic> _statisticsLogs = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockers = new();

        /// <inheritdoc/>
        public async Task<ErrorStatistics> GetErrorStatisticsAsync(string key, long maxErrors, TimeSpan interval)
        {
            var windowStatistic = _statisticsLogs.GetOrAdd(key, new WindowStatistic());
            var utcNow = DateTimeOffset.UtcNow;

            var lastTimeFrameLog = windowStatistic.LastTimeFrameLog;
            StatisticLog currentTimeFrameStatisticLog = null;
            TimeFrameStatistics currentTimeFrameStatistics = null;
            DateTimeOffset? leftEdgeOfInterval = null;
            if (lastTimeFrameLog != null)
            {
                leftEdgeOfInterval = utcNow.Add(-lastTimeFrameLog.Interval);
                if (utcNow >= lastTimeFrameLog.From &&
                    utcNow <= lastTimeFrameLog.To)
                {
                    currentTimeFrameStatisticLog = lastTimeFrameLog;
                    currentTimeFrameStatistics = currentTimeFrameStatisticLog.TimeFrameStatistics;
                }
                else if (leftEdgeOfInterval >= lastTimeFrameLog.From &&
                         leftEdgeOfInterval <= lastTimeFrameLog.To)
                {
                    currentTimeFrameStatisticLog = lastTimeFrameLog;
                }
            }

            if (currentTimeFrameStatisticLog == null)
            {
                await UpdateCurrentTimeFrameStatistics(key, interval);
                return new ErrorStatistics
                {
                    IsTooManyErrors = false,
                    TimeFrameStatistics = null
                };
            }

            var previousTimeFrameLog = windowStatistic.PreviousTimeFrameLog;
            StatisticLog previousTimeFrameWithinIntervalLog = null;
            if (previousTimeFrameLog != null &&
                leftEdgeOfInterval >= previousTimeFrameLog.From &&
                leftEdgeOfInterval <= previousTimeFrameLog.To)
            {
                previousTimeFrameWithinIntervalLog = previousTimeFrameLog;
            }

            decimal currentWindowPercentage = 0;
            decimal previousWindowPercentage = 0;
            if (previousTimeFrameWithinIntervalLog == null)
            {
                if (leftEdgeOfInterval <= currentTimeFrameStatisticLog.From)
                {
                    currentWindowPercentage = 1;
                }
                else
                {
                    currentWindowPercentage = GetWindowPercentage(
                        currentTimeFrameStatisticLog,
                        leftEdgeOfInterval.Value,
                        currentTimeFrameStatisticLog.To);
                }
            }
            else
            {
                previousWindowPercentage = GetWindowPercentage(
                    previousTimeFrameWithinIntervalLog,
                    leftEdgeOfInterval.Value,
                    previousTimeFrameWithinIntervalLog.To);

                currentWindowPercentage =
                    GetWindowPercentage(currentTimeFrameStatisticLog,
                        currentTimeFrameStatisticLog.From,
                        utcNow);
            }

            var numberOfErrorsInCurrentFrame = (long) (currentWindowPercentage *
                                                  currentTimeFrameStatisticLog.TimeFrameStatistics.NumberOfErrors);
            var numberOfErrorsInPreviousFrame = (long) (previousWindowPercentage *
                                                  previousTimeFrameWithinIntervalLog?.TimeFrameStatistics.NumberOfErrors ?? 0);

            var isTooManyErrors = numberOfErrorsInCurrentFrame + numberOfErrorsInPreviousFrame > maxErrors;
            if (isTooManyErrors)
            {
                return new ErrorStatistics
                {
                    IsTooManyErrors = true,
                    TimeFrameStatistics = currentTimeFrameStatistics
                };
            }

            numberOfErrorsInCurrentFrame = (long)(currentWindowPercentage * currentTimeFrameStatisticLog.TimeFrameStatistics.IncrementRequests());

            isTooManyErrors = numberOfErrorsInCurrentFrame + numberOfErrorsInPreviousFrame > maxErrors;
            
            return new ErrorStatistics
            {
                IsTooManyErrors = isTooManyErrors,
                TimeFrameStatistics = currentTimeFrameStatistics
            };
        }
        
        private async Task UpdateCurrentTimeFrameStatistics(string key, TimeSpan interval)
        {
            var utcNow = DateTimeOffset.UtcNow;
            var locker = _lockers.GetOrAdd(key, new SemaphoreSlim(1, 1));
            
            var lastTimeFrameLog = _statisticsLogs[key].LastTimeFrameLog;
            if (lastTimeFrameLog != null)
            {
                var leftEdgeOfInterval = utcNow.Add(-lastTimeFrameLog.Interval);
                if (leftEdgeOfInterval >= lastTimeFrameLog.From &&
                    leftEdgeOfInterval <= lastTimeFrameLog.To)
                {
                    await locker.WaitAsync();
                    
                    _statisticsLogs[key].SetCurrentTimeFrameLog(new StatisticLog(lastTimeFrameLog.To, interval));
                    
                    locker.Release();

                    return;
                }
            }

            await locker.WaitAsync();
            
            _statisticsLogs[key].SetCurrentTimeFrameLog(new StatisticLog(utcNow, interval));
            
            locker.Release();
        }

        private decimal GetWindowPercentage(StatisticLog log, DateTimeOffset startOfInterval, DateTimeOffset endOfInterval)
        {
            if (log == null)
            {
                return 0;
            }

            return (decimal) (endOfInterval.ToUnixTimeMilliseconds() - startOfInterval.ToUnixTimeMilliseconds()) /
                   (log.To.ToUnixTimeMilliseconds() - log.From.ToUnixTimeMilliseconds());
        }
    }
}
