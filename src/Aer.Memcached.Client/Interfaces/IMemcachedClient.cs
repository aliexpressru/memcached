using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface IMemcachedClient
{
	/// <summary>
	/// Stores value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="value">Value</param>
	/// <param name="expirationTime">Expiration time</param>
	/// <param name="token">Cancellation token</param>
	/// <param name="storeMode">Store mode</param>
	/// <returns>Result that shows if operation was successful or not</returns>
	Task<MemcachedClientResult> StoreAsync<T>(
		string key,
		T value,
		TimeSpan? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set);

	/// <summary>
	/// Stores multiple values
	/// </summary>
	/// <param name="keyValues">Values by keys</param>
	/// <param name="expirationTime">Expiration time</param>
	/// <param name="token">Cancellation token</param>
	/// <param name="storeMode">Store mode</param>
	/// <param name="batchingOptions">The options that configure internal key-values batching</param>
	/// <param name="replicationFactor">Number of physical nodes replication of data</param>
	Task MultiStoreAsync<T>(
		Dictionary<string, T> keyValues, 
		TimeSpan? expirationTime, 
		CancellationToken token, 
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null,
		int replicationFactor = 0);

	/// <summary>
	/// Gets one value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Value by key and if operation was successful or not. If operation was unsuccessful default value is returned</returns>
	Task<MemcachedClientValueResult<T>> GetAsync<T>(string key, CancellationToken token);

	/// <summary>
	/// Gets multiple values by keys
	/// </summary>
	/// <param name="keys">Keys</param>
	/// <param name="token">Cancellation token</param>
	/// <param name="batchingOptions">The options that configure internal keys batching</param>
	/// <param name="replicaFallback">Gets data from actual node and its replica. If actual node is not available gets data from replica</param>
	/// <returns>Values by keys. Only found in memcached keys are returned</returns>
	Task<IDictionary<string, T>> MultiGetAsync<T>(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		bool replicaFallback = false);

	/// <summary>
	/// Deletes one value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="token">Cancellation token</param>
	Task<MemcachedClientResult> DeleteAsync(string key, CancellationToken token);

	/// <summary>
	/// Deletes multiple values by keys
	/// </summary>
	/// <param name="keys">Keys</param>
	/// <param name="token">Cancellation token</param>
	/// <param name="batchingOptions">The options that configure internal keys batching</param>
	/// <param name="replicationFactor">Number of physical nodes to try delete keys</param>
	Task MultiDeleteAsync(
		IEnumerable<string> keys,
		CancellationToken token,
		BatchingOptions batchingOptions = null,
		int replicationFactor = 0);

	/// <summary>
	/// Increments value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="amountToAdd">Amount to add</param>
	/// <param name="initialValue">Initial value if key doesn't exist</param>
	/// <param name="expirationTime">Expiration time</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Incremented value</returns>
	Task<MemcachedClientValueResult<ulong>> IncrAsync(
		string key,
		ulong amountToAdd,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token);

	/// <summary>
	/// Increments value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="amountToSubtract">Amount to subtract</param>
	/// <param name="initialValue">Initial value if key doesn't exist</param>
	/// <param name="expirationTime">Expiration time</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Decremented value</returns>
	Task<MemcachedClientValueResult<ulong>> DecrAsync(
		string key,
		ulong amountToSubtract,
		ulong initialValue,
		TimeSpan? expirationTime,
		CancellationToken token);

	/// <summary>
	/// Flush memcached data
	/// </summary>
	Task FlushAsync(CancellationToken token);
}