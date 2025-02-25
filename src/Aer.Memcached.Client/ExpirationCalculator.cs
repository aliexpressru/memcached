using System.Diagnostics;
using System.Runtime.CompilerServices;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client;

/// <summary>
/// The key-value item expiration calculator implementation. 
/// </summary>
public class ExpirationCalculator: IExpirationCalculator
{
    private const uint InfiniteExpiration = 0U;
    
    private readonly IHashCalculator _hashCalculator;
    private readonly MemcachedConfiguration.ExpirationJitterSettings _expirationJitterSettings;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpirationCalculator"/> class.
    /// </summary>
    /// <param name="hashCalculator">The item key hash calculator.</param>
    /// <param name="config">The memcached configuration.</param>
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
        if (IsInfiniteExpiration(expirationTime))
        {
            return 0;
        }

        Debug.Assert(expirationTime != null, nameof(expirationTime) + " != null");
        
        var utcNow = DateTimeOffset.UtcNow;

        if (_expirationJitterSettings == null)
        {
            return GetExpirationTimeInUnixTimeSeconds(utcNow, expirationTime.Value);
        }

        var expirationWithJitter = GetExpirationWithJitter(key, expirationTime.Value);

        return GetExpirationTimeInUnixTimeSeconds(utcNow, expirationWithJitter);
    }

    /// <inheritdoc />
    public Dictionary<string, uint> GetExpiration(
        IEnumerable<string> keys, 
        TimeSpan? expirationTime)
    {
        if (IsInfiniteExpiration(expirationTime))
        {
            return keys.ToDictionary(k => k, _ => InfiniteExpiration);
        }

        Debug.Assert(expirationTime != null, nameof(expirationTime) + " != null");
        
        var utcNow = DateTimeOffset.UtcNow;
        
        return GetExpirationInternal(keys, utcNow, expirationTime.Value);
    }
    
    /// <inheritdoc />
    public Dictionary<string, uint> GetExpiration(IEnumerable<string> keys, DateTimeOffset? expirationTime)
    {
        if (IsInfiniteExpiration(expirationTime))
        {
            return keys.ToDictionary(k => k, _ => InfiniteExpiration);
        }

        Debug.Assert(expirationTime != null, nameof(expirationTime) + " != null");

        var utcNow = DateTimeOffset.UtcNow;

        if (expirationTime.Value <= utcNow)
        {
            // means expiration time is in the past - don't save anything
            return null;
        }

        var timeSpan = expirationTime.Value.Subtract(utcNow);

        return GetExpirationInternal(keys, utcNow, timeSpan);
    }

    /// <inheritdoc />
    public Dictionary<string, uint> GetExpiration(IDictionary<string, TimeSpan?> expirationMap)
    {
        var utcNow = DateTimeOffset.UtcNow;

        return expirationMap.ToDictionary(key => key.Key,
            value =>
            {
                var expirationTime = expirationMap[value.Key];
                if (IsInfiniteExpiration(expirationTime))
                {
                    return InfiniteExpiration;
                }
                
                return GetExpirationTimeInUnixTimeSeconds(utcNow, expirationTime!.Value);
            });
    }
    
    /// <inheritdoc />
    public Dictionary<string, uint> GetExpiration(IDictionary<string, DateTimeOffset?> expirationMap)
    {
        var utcNow = DateTimeOffset.UtcNow;

        return expirationMap.ToDictionary(key => key.Key,
            value =>
            {
                var expirationTime = expirationMap[value.Key];
                if (IsInfiniteExpiration(expirationTime))
                {
                    return InfiniteExpiration;
                }
                
                if (expirationTime <= utcNow)
                {
                    return GetExpirationTimeInUnixTimeSeconds(utcNow, TimeSpan.Zero);
                }

                var timeSpan = expirationTime!.Value.Subtract(utcNow);
                
                return GetExpirationTimeInUnixTimeSeconds(utcNow, timeSpan);
            });
    }
    
    private Dictionary<string, uint> GetExpirationInternal(
        IEnumerable<string> keys, 
        DateTimeOffset utcNow,
        TimeSpan expirationTime)
    {
        var result = new Dictionary<string, uint>();

        if (_expirationJitterSettings == null)
        {
            foreach (var key in keys)
            {
                result[key] = GetExpirationTimeInUnixTimeSeconds(utcNow, expirationTime);
            }
        }
        else
        {
            foreach (var key in keys)
            {
                result[key] = GetExpirationTimeInUnixTimeSeconds(
                    utcNow,
                    GetExpirationWithJitter(key, expirationTime));
            }
        }

        return result;
    }

    private TimeSpan GetExpirationWithJitter(string key, TimeSpan expirationTime)
    {
        var hash = _hashCalculator.ComputeHash(key);
        var lastDigit = hash % _expirationJitterSettings.SpreadFactor;
        var jitter = lastDigit * _expirationJitterSettings.MultiplicationFactor;

        var expirationWithJitter = expirationTime.Add(TimeSpan.FromSeconds(jitter));

        return expirationWithJitter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetExpirationTimeInUnixTimeSeconds(DateTimeOffset timeOffset, TimeSpan expirationTime) 
        => (uint)(timeOffset + expirationTime).ToUnixTimeSeconds();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInfiniteExpiration(TimeSpan? expirationTime) 
        => !expirationTime.HasValue
        || expirationTime == TimeSpan.MaxValue
        || expirationTime == TimeSpan.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInfiniteExpiration(DateTimeOffset? expirationTime) 
        => !expirationTime.HasValue
        || expirationTime == DateTimeOffset.MaxValue;
}