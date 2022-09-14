namespace Aer.ConsistentHash;

public interface IHashCalculator
{
    ulong ComputeHash(string value);
}