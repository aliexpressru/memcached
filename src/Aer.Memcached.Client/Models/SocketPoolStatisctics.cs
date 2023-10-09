namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents socket pool statistic counters.
/// </summary>
/// <param name="PoolIdentifier">The socket pool identifier. Typically in <c>IP:port</c> format.</param>
/// <param name="PooledSocketsCount">The number of sockets returned to the pool and awaiting reuse.</param>
/// <param name="UsedSocketsCount">The number of used sockets in this pool.</param>
/// <param name="RemainingPoolCapacity">The remaining number of sockets that can be created in this pool.</param>
public record SocketPoolStatisctics(
	string PoolIdentifier,
	int PooledSocketsCount, 
	int UsedSocketsCount, 
	int RemainingPoolCapacity)
{
	public override string ToString()
	{
		return
			$"(Pool : {PoolIdentifier}, pooled/used/remaining sockets : {PooledSocketsCount}/{UsedSocketsCount}/{RemainingPoolCapacity})";
	}
}
