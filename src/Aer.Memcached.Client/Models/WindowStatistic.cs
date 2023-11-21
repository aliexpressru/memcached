namespace Aer.Memcached.Client.Models;

/// <summary>
/// Data structure for sliding window algorithm
/// </summary>
internal class WindowStatistic
{
    public StatisticLog LastTimeFrameLog { get; private set; }

    public StatisticLog PreviousTimeFrameLog { get; private set; }
    
    public void SetCurrentTimeFrameLog(StatisticLog log)
    {
        if (LastTimeFrameLog != null)
        {
            PreviousTimeFrameLog = StatisticLog.CopyFrom(LastTimeFrameLog);
        }

        LastTimeFrameLog = log;
    }
}