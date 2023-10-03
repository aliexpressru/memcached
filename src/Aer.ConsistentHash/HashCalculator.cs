using Aer.ConsistentHash.Abstractions;
using Standart.Hash.xxHash;

namespace Aer.ConsistentHash;

public class HashCalculator : IHashCalculator
{
    public ulong ComputeHash(string value)
    {
        return xxHash64.ComputeHash(value);
    }
}