namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// An interface for a custom jitter provider.
/// </summary>
public interface IJitterProvider
{
	/// <summary>
	/// Calculate jitter using seed and mac delays.
	/// </summary>
	/// <param name="seedDelay">The seed delay for the jitter calculation. Calculated jitter can't be less than this value.</param>
	/// <param name="maxDelay">The max delay for the jitter calculation. Calculated jitter can't be less than this value.</param>
	public TimeSpan CalculateJitter(TimeSpan seedDelay, TimeSpan maxDelay);
}
