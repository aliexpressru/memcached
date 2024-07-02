using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Infrastructure.Jitter;

/// <summary>
/// The custom jitter provider that uses decorrelated jitter algorithm.
/// </summary>
public class DecorrelatedJitterProvider : IJitterProvider
{
	/// <inheritdoc/>
	public TimeSpan CalculateJitter(TimeSpan seedDelay, TimeSpan maxDelay)
	{
		double seed = seedDelay.TotalMilliseconds;
		double max = maxDelay.TotalMilliseconds;
		double jitterMilliseconds = seed;

		// adopting the 'Decorrelated Jitter' formula from https://www.awsarchitectureblog.com/2015/03/backoff.html.
		// Can be between seed and seed * 3.  Mustn't exceed max.
		jitterMilliseconds = Math.Min(
			max,
			Math.Max(
				seed,
				jitterMilliseconds * 3 * Random.Shared.NextDouble()
			)
		); 
		
		var jitter = TimeSpan.FromMilliseconds(jitterMilliseconds);

		return jitter;
	}
}
