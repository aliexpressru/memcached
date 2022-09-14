namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents an object being sent to the cache.
/// </summary>
internal class CacheItemForRequest
{
    /// <summary>
    /// The data representing the item being stored/retrieved.
    /// </summary>
    public ArraySegment<byte> Data { get; }

    /// <summary>
    /// Flags set for this instance.
    /// </summary>
    public uint Flags { get; }
    
    /// <summary>
    /// Initializes a new instance of <see cref="T:CacheItem"/>.
    /// </summary>
    /// <param name="flags">Custom item data.</param>
    /// <param name="data">The serialized item.</param>
    public CacheItemForRequest(uint flags, ArraySegment<byte> data)
    {
        Data = data;
        Flags = flags;
    }
}