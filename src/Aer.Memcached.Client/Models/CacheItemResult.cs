namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents an object being retrieved from the cache.
/// </summary>
internal class CacheItemResult
{
    /// <summary>
    /// The data representing the item being retrieved.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Flags set for this instance.
    /// </summary>
    public uint Flags { get; }
    
    /// <summary>
    /// Initializes a new instance of <see cref="T:CacheItem"/>.
    /// </summary>
    /// <param name="flags">Custom item data.</param>
    /// <param name="data">The serialized data.</param>
    public CacheItemResult(uint flags, ReadOnlyMemory<byte> data)
    {
        Data = data;
        Flags = flags;
    }
}