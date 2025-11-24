﻿using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents options that configure key or key-value batching during multi-get and multi-store operations.
/// </summary>
/// <remarks>
/// Used by <see cref="IMemcachedClient.MultiGetAsync{T}(IEnumerable{string}, CancellationToken, BatchingOptions, uint)"/> 
/// and <c>MultiStoreAsync</c> methods to split large requests into smaller batches.
/// </remarks>
public class BatchingOptions
{
	/// <summary>
	/// The default keys batch size.
	/// </summary>
	public const int DefaultBatchSize = 15;
	
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
