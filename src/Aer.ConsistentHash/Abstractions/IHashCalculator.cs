namespace Aer.ConsistentHash.Abstractions;

public interface IHashCalculator
{
    ulong ComputeHash(string value);
}