namespace Aer.Memcached.Client.Interfaces;

public interface IExpirationCalculator
{
    /// <summary>
    /// Gets expiration time in unix seconds
    /// </summary>
    uint GetExpiration(string key, TimeSpan? expirationTime);

    /// <summary>
    /// Batch version of getting expiration in unix seconds
    /// </summary>
    /// <returns>Mapping of key to expiration time</returns>
    Dictionary<string, uint> GetExpiration(IEnumerable<string> keys, TimeSpan? expirationTime);

    /// <summary>
    /// Batch version of getting expiration in unix seconds using DateTimeOffset
    /// </summary>
    /// <returns>Mapping of key to expiration time</returns>
    Dictionary<string, uint> GetExpiration(IEnumerable<string> keys, DateTimeOffset? expirationTime);
}