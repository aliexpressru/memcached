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
	/// <returns>Result that shows if operation was successful or not.</returns>
	Task<MemcachedClientResult> StoreAsync<T>(
		string key,
		T value,
		TimeSpan? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set,
		CacheSyncOptions cacheSyncOptions = null);

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
	Task<MemcachedClientResult> MultiStoreAsync<T>(
		IDictionary<string, T> keyValues, 
		TimeSpan? expirationTime, 
		CancellationToken token, 
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0);

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
	Task<MemcachedClientResult> MultiStoreAsync<T>(
		IDictionary<string, T> keyValues,
		DateTimeOffset? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0);

	/// <summary>
	/// Gets one value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>
	/// Value by key and if operation was successful or not.
	/// If operation was unsuccessful default value is returned.
	/// </returns>
	Task<MemcachedClientValueResult<T>> GetAsync<T>(string key, CancellationToken token);

	/// <summary>
	/// Gets multiple values by keys.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="replicationFactor">Number of physical nodes which will be requested to obtain data.</param>
	/// <returns>Values by keys. Only found in memcached keys are returned.</returns>
	Task<IDictionary<string, T>> MultiGetAsync<T>(IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		uint replicationFactor = 0);

	/// <summary>
	/// Gets multiple values by keys. Does not throw exceptions and returns a not-null value.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="replicationFactor">Number of physical nodes which will be requested to obtain data.</param>
	/// <returns>Values by keys. Only found in memcached keys are returned.</returns>
	Task<MemcachedClientValueResult<IDictionary<string, T>>> MultiGetSafeAsync<T>(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		uint replicationFactor = 0);

	/// <summary>
	/// Deletes one value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	Task<MemcachedClientResult> DeleteAsync(
		string key, 
		CancellationToken token, 
		CacheSyncOptions cacheSyncOptions = null);

	/// <summary>
	/// Deletes multiple values by keys.
	/// </summary>
	/// <param name="keys">Keys.</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="batchingOptions">The options that configure internal keys batching.</param>
	/// <param name="cacheSyncOptions">The options that configure cache sync.</param>
	/// <param name="replicationFactor">Number of physical nodes to try to delete keys on.</param>
	Task<MemcachedClientResult> MultiDeleteAsync(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		CacheSyncOptions cacheSyncOptions = null,
		uint replicationFactor = 0);

	/// <summary>
	/// Increments value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="amountToAdd">Amount to add.</param>
	/// <param name="initialValue">Initial value if key doesn't exist.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>Incremented value.</returns>
	Task<MemcachedClientValueResult<ulong>> IncrAsync(
		string key,
		ulong amountToAdd,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token);

	/// <summary>
	/// Increments value by key.
	/// </summary>
	/// <param name="key">Key.</param>
	/// <param name="amountToSubtract">Amount to subtract.</param>
	/// <param name="initialValue">Initial value if key doesn't exist.</param>
	/// <param name="expirationTime">Expiration time.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>Decremented value.</returns>
	Task<MemcachedClientValueResult<ulong>> DecrAsync(
		string key,
		ulong amountToSubtract,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token);

	/// <summary>
	/// Delete all key-value items.
	/// </summary>
	Task<MemcachedClientResult> FlushAsync(CancellationToken token);

	/// <summary>
	/// Returns <c>true</c> if cache synchronization is turned on, <c>false</c> otherwise.
	/// </summary>
	bool IsCacheSyncEnabled();
}