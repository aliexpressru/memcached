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
	Task MultiStoreAsync<T>(
		Dictionary<string, T> keyValues,
		TimeSpan? expirationTime,
		CancellationToken token,
		StoreMode storeMode = StoreMode.Set,
		BatchingOptions batchingOptions = null);

	/// <summary>
	/// Gets one value by key
	/// </summary>
	/// <param name="key">Key</param>
	/// <param name="token">Cancellation token</param>
	/// <returns>Value by key and if operation was successful or not. If operation was unsuccessful default value is returned</returns>
	Task<MemcachedClientGetResult<T>> GetAsync<T>(string key, CancellationToken token);

	/// <summary>
	/// Gets multiple values by keys
	/// </summary>
	/// <param name="keys">Keys</param>
	/// <param name="token">Cancellation token</param>
	/// <param name="batchingOptions">The options that configure internal keys batching</param>
	/// <returns>Values by keys. Only found in memcached keys are returned</returns>
	Task<IDictionary<string, T>> MultiGetAsync<T>(
		IEnumerable<string> keys, 
		CancellationToken token,
		BatchingOptions batchingOptions = null);

	/// <summary>
	/// Flush memcached data
	/// </summary>
	Task FlushAsync(CancellationToken token);
}