namespace Aer.Memcached.Client.Models;

/// <summary>
/// Class wrapper to calculate number of errors
/// </summary>
public class TimeFrameStatistics
{
    private long _numberOfErrors;

    public long NumberOfErrors
    {
        get => _numberOfErrors;
        init => _numberOfErrors = value;
    }

    public long IncrementRequests()
    {
        return Interlocked.Increment(ref _numberOfErrors);
    }
}