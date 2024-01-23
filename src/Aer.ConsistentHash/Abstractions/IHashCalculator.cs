namespace Aer.ConsistentHash.Abstractions;

/// <summary>
/// Interface for key or value hash calculator.
/// </summary>
public interface IHashCalculator
{
    /// <summary>
    /// Computes the value hash and returns it in a form of <see cref="ulong"/>.
    /// </summary>
    /// <param name="value">The value to compute hash for.</param>
    ulong ComputeHash(string value);

    /// <summary>
    /// Computes the value hash and returns it in a form of <see cref="string"/>.
    /// </summary>
    /// <param name="value">The value to digest.</param>
    /// <remarks>
    /// Used to shorten the long value to the string 32 symbols long.
    /// </remarks>
    string DigestValue(string value);
}