using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Config;

/// <summary>
/// Flags used to control which operations will be allowed to be processed.
/// </summary>
[Flags]
public enum EnabledOperations
{
    /// <summary>
    /// No operations will be processed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Operation to store a single item in the cache. <see cref="IMemcachedClient.StoreAsync"/>.
    /// </summary>
    StoreAsync = 1,

    /// <summary>
    /// Operation to store multiple items in the cache at once. <see cref="IMemcachedClient.MultiStoreAsync"/>.
    /// </summary>
    MultiStoreAsync = 2,

    /// <summary>
    /// Operation to store multiple items and synchronize data. <see cref="IMemcachedClient.MultiStoreSynchronizeDataAsync"/>.
    /// </summary>
    MultiStoreSynchronizeDataAsync = 4,

    /// <summary>
    /// Combined flag for all store-related operations
    /// Includes:
    /// <ul>
    ///     <li><see cref="StoreAsync"/></li>
    ///     <li><see cref="MultiStoreAsync"/></li>
    ///     <li><see cref="MultiStoreSynchronizeDataAsync"/></li>
    /// </ul>
    /// </summary>
    Store = StoreAsync | MultiStoreAsync | MultiStoreSynchronizeDataAsync,

    /// <summary>
    /// Operation to get a single item from the cache. <see cref="IMemcachedClient.GetAsync"/>.
    /// </summary>
    GetAsync = 8,

    /// <summary>
    /// Operation to get an item and update its expiration. <see cref="IMemcachedClient.GetAndTouchAsync"/>.
    /// </summary>
    GetAndTouchAsync = 16,

    /// <summary>
    /// Operation to get multiple items from the cache in a single call. <see cref="IMemcachedClient.MultiGetAsync"/>.
    /// </summary>
    MultiGetAsync = 32,

    /// <summary>
    /// Operation to retrieve multiple items without throwing exceptions. <see cref="IMemcachedClient.MultiGetSafeAsync"/>.
    /// </summary>
    MultiGetSafeAsync = 64,

    /// <summary>
    /// Combined flag for all retrieval operations.
    /// Includes:
    /// <ul>
    ///     <li><see cref="GetAsync"/></li>
    ///     <li><see cref="GetAndTouchAsync"/></li>
    ///     <li><see cref="MultiGetAsync"/></li>
    ///     <li><see cref="MultiGetSafeAsync"/></li>
    /// </ul>
    /// </summary>
    Get = GetAsync | GetAndTouchAsync | MultiGetAsync | MultiGetSafeAsync,

    /// <summary>
    /// Operation to delete a single item from the cache. <see cref="IMemcachedClient.DeleteAsync"/>.
    /// </summary>
    DeleteAsync = 128,

    /// <summary>
    /// Operation to delete multiple items from the cache in a single call. <see cref="IMemcachedClient.MultiDeleteAsync"/>.
    /// </summary>
    MultiDeleteAsync = 256,

    /// <summary>
    /// Combined flag for all deletion operations.
    /// Includes:
    /// <ul>
    ///     <li><see cref="DeleteAsync"/></li>
    ///     <li><see cref="MultiDeleteAsync"/></li>
    /// </ul>
    /// </summary>
    Delete = DeleteAsync | MultiDeleteAsync,

    /// <summary>
    /// Operation to increment value in the cache. <see cref="IMemcachedClient.IncrAsync"/>.
    /// </summary>
    IncrAsync = 512,

    /// <summary>
    /// Operation to decrement value in the cache. <see cref="IMemcachedClient.DecrAsync"/>.
    /// </summary>
    DecrAsync = 1024,

    /// <summary>
    /// Combined flag for counter operations increment/decrement.
    /// Includes:
    /// <ul>
    ///     <li><see cref="IncrAsync"/></li>
    ///     <li><see cref="DecrAsync"/></li>
    /// </ul>
    /// </summary>
    Counter = IncrAsync | DecrAsync,

    /// <summary>
    /// Operation to flush the cache. <see cref="IMemcachedClient.FlushAsync"/>.
    /// </summary>
    FlushAsync = 2048,

    /// <summary>
    /// Enables all operations.
    /// Includes:
    /// <ul>
    ///     <li><see cref="Store"/></li>
    ///     <li><see cref="Get"/></li>
    ///     <li><see cref="Delete"/></li>
    ///     <li><see cref="Counter"/></li>
    ///     <li><see cref="FlushAsync"/></li>
    /// </ul>
    /// </summary>
    All = Store | Get | Delete | Counter | FlushAsync
}