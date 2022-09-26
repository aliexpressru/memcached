using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents options that configure key or kev-value batching during <see cref="IMemcachedClient.MultiGetAsync{T}"/> and <see cref="IMemcachedClient.MultiStoreAsync{T}"/>.
/// </summary>
public class BatchingOptions
{
	/// <summary>
	/// The default keys batch size.
	/// </summary>
	public const int DefaultBatchSize = 20;
	
	/// <summary>
	/// The size of the keys or key-values batches that the incoming keys or key-values set is spit into.
	/// </summary>
	/// <remarks>Batching is required to reduce in some cases a single memcached command size. This reduces command execution time.</remarks>
	public int BatchSize { get; set; } = DefaultBatchSize;
	
	/// <summary>
	/// The maximum degree of parallelism used to parallelize split keys or key-value bathes processing.
	/// </summary>
	public int MaxDegreeOfParallelism { get; set; } = 1;
}
