using Aer.ConsistentHash.Abstractions;
using Standart.Hash.xxHash;

namespace Aer.ConsistentHash;

/// <inheritdoc/>
public class HashCalculator : IHashCalculator
{
    /// <inheritdoc/>
    public ulong ComputeHash(string value)
    {
        return xxHash64.ComputeHash(value);
    }

    /// <inheritdoc/>
    public string DigestValue(string value)
    {
        var bytes = xxHash128.ComputeHashBytes(value);
        return Convert.ToHexString(bytes);
    }
}