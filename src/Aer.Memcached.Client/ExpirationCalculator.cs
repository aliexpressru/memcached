using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client;

public class ExpirationCalculator: IExpirationCalculator
{
    private readonly IHashCalculator _hashCalculator;

    private readonly MemcachedConfiguration.ExpirationJitterSettings _expirationJitterSettings;


    public ExpirationCalculator(
        IHashCalculator hashCalculator,
        IOptions<MemcachedConfiguration> config)
    {
        _hashCalculator = hashCalculator;
        _expirationJitterSettings = config.Value.ExpirationJitter;
    }

    /// <inheritdoc />
    public uint GetExpiration(string key, TimeSpan? expirationTime)
    {
        if (!expirationTime.HasValue)
        {
            return 0;
        }

        var utcNow = DateTimeOffset.UtcNow;

        if (_expirationJitterSettings == null)
        {
            return GetExpirationTimeInUnixTimeSeconds(utcNow, expirationTime.Value);
        }

        var expirationWithJitter = GetExpirationWithJitter(key, expirationTime);

        return GetExpirationTimeInUnixTimeSeconds(utcNow, expirationWithJitter);
    }

    /// <inheritdoc />
    public Dictionary<string, uint> GetExpiration(IEnumerable<string> keys, TimeSpan? expirationTime)
    {
        var result = new Dictionary<string, uint>();

        var utcNow = DateTimeOffset.UtcNow;

        if (_expirationJitterSettings == null)
        {
            foreach (var key in keys)
            {
                result[key] = expirationTime.HasValue 
                    ? GetExpirationTimeInUnixTimeSeconds(utcNow, expirationTime.Value) 
                    : 0;
            }
        }
        else
        {
            foreach (var key in keys)
            {
                var expirationWithJitter = GetExpirationWithJitter(key, expirationTime);

                result[key] = GetExpirationTimeInUnixTimeSeconds(utcNow, expirationWithJitter);
            }
        }

        return result;
    }

    private TimeSpan GetExpirationWithJitter(string key, TimeSpan? expirationTime)
    {
        if (!expirationTime.HasValue)
        {
            return TimeSpan.Zero;
        }
        
        var hash = _hashCalculator.ComputeHash(key);
        var lastDigit = hash % 10;
        var jitter = lastDigit * _expirationJitterSettings.MultiplicationFactor;

        var expirationWithJitter = expirationTime.Value.Add(TimeSpan.FromSeconds(jitter));

        return expirationWithJitter;
    }

    private uint GetExpirationTimeInUnixTimeSeconds(DateTimeOffset timeOffset, TimeSpan expirationTime)
    {
        return (uint)(timeOffset + expirationTime).ToUnixTimeSeconds();
    }
}