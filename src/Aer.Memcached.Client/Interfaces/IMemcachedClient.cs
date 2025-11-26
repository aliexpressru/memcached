using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// A memcached client interface.
/// </summary>
public interface IMemcachedClient
{
	/// <summary>
	/// Stores value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="value">Value.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="storeMode">Store mode.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>Result that shows if operation was successful or not.</returns>
	Task<MemcachedClientResult> StoreAsync<T>(
		string key,
		T value,
		TimeSpan? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set,
		CacheSyncOptions cacheSyncOptions = null,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Stores multiple values.
	/// </summary>
	/// <param name="keyValues">Values by keys.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="storeMode">Store mode.</param>
	/// <param name="batchingOptions">The options that configure internal key-values batching.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="replicationFactor">Number of physical nodes replication of data.</param>
	/// <param name="expirationMap">Individual key expirations that will be used instead expirationTime if provided.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> MultiStoreAsync<T>(
		IDictionary<string, T> keyValues, 
		TimeSpan? expirationTime, 
		CancellationToken token, 
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0,
		IDictionary<string, TimeSpan?> expirationMap = null,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Stores multiple values.
	/// </summary>
	/// <param name="keyValues">Values by keys.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="storeMode">Store mode.</param>
	/// <param name="batchingOptions">The options that configure internal key-values batching.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="replicationFactor">Number of physical nodes replication of data.</param>
	/// <param name="expirationMap">Individual key expirations that will be used instead expirationTime if provided.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> MultiStoreAsync<T>(
		IDictionary<string, T> keyValues,
		DateTimeOffset? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0,
		IDictionary<string, DateTimeOffset?> expirationMap = null,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Lean version of MultiStore method to synchronize cache data
	/// keyValues must have already serialized data in values
	/// </summary>
	/// <param name="keyValues">Values by keys.</param>
	/// <param name="flags">Flags for the data.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="expirationMap">Individual key expirations that will be used instead expirationTime if provided.</param>
	/// <param name="batchingOptions">Batching options for splitting key-values into batches.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> MultiStoreSynchronizeDataAsync(
		IDictionary<string, byte[]> keyValues,
		uint flags,
		DateTimeOffset? expirationTime,
		CancellationToken token,
		IDictionary<string, DateTimeOffset?> expirationMap = null,
		BatchingOptions batchingOptions = null,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Gets one value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>
	/// Value by key and if operation was successful or not.
	/// If operation was unsuccessful default value is returned.
	/// </returns>
	Task<MemcachedClientValueResult<T>> GetAsync<T>(string key, CancellationToken token, TracingOptions tracingOptions = null);

	/// <summary>
	/// Gets one value by key. and updates the TTL of the cached item.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>
	/// Value by key and if operation was successful or not.
	/// If operation was unsuccessful default value is returned.
	/// </returns>
	Task<MemcachedClientValueResult<T>> GetAndTouchAsync<T>(string key, TimeSpan? expirationTime, CancellationToken token, TracingOptions tracingOptions = null);

	/// <summary>
	/// Gets multiple values by keys.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="replicationFactor">Number of physical nodes which will be requested to obtain data.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>Values by keys. Only found in memcached keys are returned.</returns>
	Task<IDictionary<string, T>> MultiGetAsync<T>(IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		uint replicationFactor = 0,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Gets multiple values by keys. Does not throw exceptions and returns a not-null value.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="replicationFactor">Number of physical nodes which will be requested to obtain data.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>Values by keys. Only found in memcached keys are returned.</returns>
	Task<MemcachedClientValueResult<IDictionary<string, T>>> MultiGetSafeAsync<T>(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		uint replicationFactor = 0,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Deletes one value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> DeleteAsync(
		string key, 
		CancellationToken token, 
		CacheSyncOptions cacheSyncOptions = null,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Deletes multiple values by keys.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="replicationFactor">Number of physical nodes to try to delete keys on.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> MultiDeleteAsync(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Increments value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="amountToAdd">Amount to add.</param>
	/// <param name="initialValue">Initial value if key doesn't exist.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>Incremented value.</returns>
	Task<MemcachedClientValueResult<ulong>> IncrAsync(
		string key,
		ulong amountToAdd,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Increments value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="amountToSubtract">Amount to subtract.</param>
	/// <param name="initialValue">Initial value if key doesn't exist.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	/// <returns>Decremented value.</returns>
	Task<MemcachedClientValueResult<ulong>> DecrAsync(
		string key,
		ulong amountToSubtract,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token,
		TracingOptions tracingOptions = null);

	/// <summary>
	/// Delete all key-value items.
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	/// <param name="tracingOptions">Optional tracing options to control tracing behavior.</param>
	Task<MemcachedClientResult> FlushAsync(CancellationToken token, TracingOptions tracingOptions = null);

	/// <summary>
	/// Returns <c>true</c> if cache synchronization is turned on, <c>false</c> otherwise.
	/// </summary>
	bool IsCacheSyncEnabled();
}