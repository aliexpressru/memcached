using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Extensions;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Client.Serializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoreLinq.Extensions;

namespace Aer.Memcached.Client;

/// <summary>
/// A memcached client implementation.
/// </summary>
/// <typeparam name="TNode">The type of the memcached hash ring node.</typeparam>
[SuppressMessage(
    "ReSharper",
    "ConvertToUsingDeclaration",
    Justification = "We need to explicitly control when the command gets disposed")]
public class MemcachedClient<TNode> : IMemcachedClient
    where TNode : class, INode
{
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly ICommandExecutor<TNode> _commandExecutor;
    private readonly IExpirationCalculator _expirationCalculator;
    private readonly ICacheSynchronizer _cacheSynchronizer;
    private readonly BinarySerializer _binarySerializer;
    private readonly ILogger _logger;
    private readonly MemcachedConfiguration _memcachedConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemcachedClient{TNode}"/> class.
    /// </summary>
    /// <param name="nodeLocator">The node locator.</param>
    /// <param name="commandExecutor">The memcached command executor.</param>
    /// <param name="expirationCalculator">The key-value item expiration calculator.</param>
    /// <param name="cacheSynchronizer">The multi-cluster cache synchronizer.</param>
    /// <param name="binarySerializer">The value item binary serializer.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="memcachedConfiguration">The memcached configuration.</param>
    public MemcachedClient(
        INodeLocator<TNode> nodeLocator,
        ICommandExecutor<TNode> commandExecutor,
        IExpirationCalculator expirationCalculator,
        ICacheSynchronizer cacheSynchronizer,
        BinarySerializer binarySerializer,
        ILogger<MemcachedClient<TNode>> logger,
        IOptions<MemcachedConfiguration> memcachedConfiguration)
    {
        _nodeLocator = nodeLocator;
        _commandExecutor = commandExecutor;
        _expirationCalculator = expirationCalculator;
        _cacheSynchronizer = cacheSynchronizer;
        _binarySerializer = binarySerializer;
        _logger = logger;
        _memcachedConfiguration = memcachedConfiguration.Value;
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> StoreAsync<T>(
        string key,
        T value,
        TimeSpan? expirationTime,
        CancellationToken token,
        StoreMode storeMode = StoreMode.Set,
        CacheSyncOptions cacheSyncOptions = null)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientResult.Unsuccessful($"Memcached node for key {key} is not found");
            }

            var utcNow = DateTimeOffset.UtcNow;

            var cacheItem = _binarySerializer.Serialize(value);
            var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

            using (var command = new StoreCommand(
                       storeMode,
                       key,
                       cacheItem,
                       expiration,
                       _memcachedConfiguration.IsAllowLongKeys))
            {
                var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                var syncSuccess = false;
                if (IsCacheSyncEnabledInternal(cacheSyncOptions))
                {
                    syncSuccess = await _cacheSynchronizer.TrySyncCacheAsync(
                        new CacheSyncModel
                        {
                            KeyValues = new Dictionary<string, byte[]>()
                            {
                                [key] = cacheItem.Data.Array
                            },
                            Flags = cacheItem.Flags,
                            ExpirationTime = expirationTime.HasValue
                                ? utcNow.Add(expirationTime.Value)
                                : null
                        },
                        token);
                }

                return new MemcachedClientResult(result.Success, errorMessage: result.ErrorMessage)
                    .WithSyncSuccess(syncSuccess);
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(StoreAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(StoreAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> MultiStoreAsync<T>(
        IDictionary<string, T> keyValues,
        TimeSpan? expirationTime,
        CancellationToken token,
        StoreMode storeMode = StoreMode.Set,
        BatchingOptions batchingOptions = null,
        CacheSyncOptions cacheSyncOptions = null,
        uint replicationFactor = 0,
        IDictionary<string, TimeSpan?> expirationMap = null)
    {
        try
        {
            var nodes = _nodeLocator.GetNodes(keyValues.Keys, replicationFactor);
            if (nodes.Keys.Count == 0)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Memcached nodes for keys {string.Join(",", keyValues.Keys)} not found");
            }

            var utcNow = DateTimeOffset.UtcNow;

            var keyToExpirationMap = expirationMap == null 
                ? _expirationCalculator.GetExpiration(keyValues.Keys, expirationTime) 
                : _expirationCalculator.GetExpiration(expirationMap);

            var serializedKeyValues = new Dictionary<string, CacheItemForRequest>();
            foreach (var keyValue in keyValues)
            {
                serializedKeyValues[keyValue.Key] = _binarySerializer.Serialize(keyValue.Value);
            }
            
            await MultiStoreInternalAsync(nodes, keyToExpirationMap, serializedKeyValues, token, storeMode, batchingOptions);

            var syncSuccess = false;
            if (IsCacheSyncEnabledInternal(cacheSyncOptions))
            {
                syncSuccess = await _cacheSynchronizer.TrySyncCacheAsync(
                    new CacheSyncModel
                    {
                        KeyValues = serializedKeyValues.ToDictionary(key => key.Key, value => value.Value.Data.Array),
                        Flags = serializedKeyValues.First().Value.Flags,
                        ExpirationTime = expirationTime.HasValue
                            ? utcNow.Add(expirationTime.Value)
                            : null,
                        ExpirationMap = expirationMap?.ToDictionary(key => key.Key, value => value.Value.HasValue ? utcNow.Add(value.Value.Value) : (DateTimeOffset?)null)
                    },
                    token);
            }

            return MemcachedClientResult.Successful.WithSyncSuccess(syncSuccess);
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(MultiStoreAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(MultiStoreAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> MultiStoreAsync<T>(
        IDictionary<string, T> keyValues,
        DateTimeOffset? expirationTime,
        CancellationToken token,
        StoreMode storeMode = StoreMode.Set,
        BatchingOptions batchingOptions = null,
        CacheSyncOptions cacheSyncOptions = null,
        uint replicationFactor = 0,
        IDictionary<string, DateTimeOffset?> expirationMap = null)
    {
        try
        {
            if (keyValues is null or { Count: 0 })
            {
                return MemcachedClientResult.Successful;
            }

            var keyToExpirationMap = expirationMap == null 
                ? _expirationCalculator.GetExpiration(keyValues.Keys, expirationTime) 
                : _expirationCalculator.GetExpiration(expirationMap);

            // this check is first since it shortcuts all the following logic
            if (keyToExpirationMap is null)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Expiration date time offset {expirationTime} lies in the past. No keys stored");
            }

            var nodes = _nodeLocator.GetNodes(keyValues.Keys, replicationFactor);
            if (nodes.Keys.Count == 0)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Memcached nodes for keys {string.Join(",", keyValues.Keys)} not found");
            }
            
            var serializedKeyValues = new Dictionary<string, CacheItemForRequest>();
            foreach (var keyValue in keyValues)
            {
                serializedKeyValues[keyValue.Key] = _binarySerializer.Serialize(keyValue.Value);
            }

            await MultiStoreInternalAsync(nodes, keyToExpirationMap, serializedKeyValues, token, storeMode, batchingOptions);

            var syncSuccess = false;
            if (IsCacheSyncEnabledInternal(cacheSyncOptions))
            {
                syncSuccess = await _cacheSynchronizer.TrySyncCacheAsync(
                    new CacheSyncModel
                    {
                        KeyValues = serializedKeyValues.ToDictionary(key => key.Key, value => value.Value.Data.Array),
                        Flags = serializedKeyValues.First().Value.Flags,
                        ExpirationTime = expirationTime,
                        ExpirationMap = expirationMap
                    },
                    token);
            }

            return MemcachedClientResult.Successful.WithSyncSuccess(syncSuccess);
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(MultiStoreAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(MultiStoreAsync)} execution.\nException details: {e}");
        }
    }
    
    /// <inheritdoc />
    public async Task<MemcachedClientResult> MultiStoreSynchronizeDataAsync(
        IDictionary<string, byte[]> keyValues,
        uint flags,
        DateTimeOffset? expirationTime,
        CancellationToken token,
        IDictionary<string, DateTimeOffset?> expirationMap = null)
    {
        try
        {
            if (keyValues is null or { Count: 0 })
            {
                return MemcachedClientResult.Successful;
            }

            var keyToExpirationMap = expirationMap == null 
                ? _expirationCalculator.GetExpiration(keyValues.Keys, expirationTime) 
                : _expirationCalculator.GetExpiration(expirationMap);

            // this check is first since it shortcuts all the following logic
            if (keyToExpirationMap is null)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Expiration date time offset {expirationTime} lies in the past. No keys stored");
            }

            var nodes = _nodeLocator.GetNodes(keyValues.Keys, 0);
            if (nodes.Keys.Count == 0)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Memcached nodes for keys {string.Join(",", keyValues.Keys)} not found");
            }
            
            var serializedKeyValues = new Dictionary<string, CacheItemForRequest>();
            foreach (var keyValue in keyValues)
            {
                serializedKeyValues[keyValue.Key] = new CacheItemForRequest(flags, keyValue.Value);
            }

            await MultiStoreInternalAsync(nodes, keyToExpirationMap, serializedKeyValues, token);

            return MemcachedClientResult.Successful.WithSyncSuccess(true);
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(MultiStoreAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(MultiStoreAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<T>> GetAsync<T>(string key, CancellationToken token)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientValueResult<T>.Unsuccessful($"Memcached node for key {key} is not found");
            }

            using (var command = new GetCommand(key, _memcachedConfiguration.IsAllowLongKeys))
            {
                using var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                if (!commandExecutionResult.Success)
                {
                    return MemcachedClientValueResult<T>.Unsuccessful(
                        $"Error occured during {nameof(GetAsync)} execution");
                }

                try
                {
                    var deserializationResult =
                        _binarySerializer.Deserialize<T>(commandExecutionResult.GetCommandAs<GetCommand>().Result);

                    return MemcachedClientValueResult<T>.Successful(
                        deserializationResult.Result,
                        deserializationResult.IsEmpty);
                }
                catch (Exception) when (_memcachedConfiguration.IsDeleteMemcachedKeyOnDeserializationFail)
                {
                    // means exception on deserialization happened
                    // assuming serializer change - remove this key from memcached to refresh data

                    await DeleteUndeserializableKey(key, token);

                    return MemcachedClientValueResult<T>.Unsuccessful(
                        $"Undeserializable key {key} found. Assuming binary serializer change. Key deleted from memcached.");
                }
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientValueResult<T>.Cancelled(nameof(GetAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientValueResult<T>.Unsuccessful(
                $"An exception happened during {nameof(GetAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<T>> GetAndTouchAsync<T>(string key, TimeSpan? expirationTime, CancellationToken token)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientValueResult<T>.Unsuccessful($"Memcached node for key {key} is not found");
            }

            var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

            using (var command = new GetAndTouchCommand(key, expiration, _memcachedConfiguration.IsAllowLongKeys))
            {
                using var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                if (!commandExecutionResult.Success)
                {
                    return MemcachedClientValueResult<T>.Unsuccessful(
                        $"Error occured during {nameof(GetAndTouchAsync)} execution");
                }

                try
                {
                    var deserializationResult =
                        _binarySerializer.Deserialize<T>(commandExecutionResult.GetCommandAs<GetCommand>().Result);

                    return MemcachedClientValueResult<T>.Successful(
                        deserializationResult.Result,
                        deserializationResult.IsEmpty);
                }
                catch (Exception) when (_memcachedConfiguration.IsDeleteMemcachedKeyOnDeserializationFail)
                {
                    // means exception on deserialization happened
                    // assuming serializer change - remove this key from memcached to refresh data

                    await DeleteUndeserializableKey(key, token);

                    return MemcachedClientValueResult<T>.Unsuccessful(
                        $"Undeserializable key {key} found. Assuming binary serializer change. Key deleted from memcached.");
                }
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientValueResult<T>.Cancelled(nameof(GetAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientValueResult<T>.Unsuccessful(
                $"An exception happened during {nameof(GetAndTouchAsync)} execution.\nException details: {e}");
        }
    }
    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<IDictionary<string, T>>> MultiGetSafeAsync<T>(
        IEnumerable<string> keys,
        CancellationToken token,
        BatchingOptions batchingOptions = null,
        uint replicationFactor = 0)
    {
        try
        {
            var getKeysResult = await MultiGetAsync<T>(keys, token, batchingOptions, replicationFactor);

            return MemcachedClientValueResult<IDictionary<string, T>>.Successful(
                getKeysResult,
                isResultEmpty: getKeysResult is null or {Count: 0});
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientValueResult<IDictionary<string, T>>.Cancelled(
                nameof(MultiGetSafeAsync),
                defaultResultValue: new Dictionary<string, T>()
            );
        }
        catch (Exception e)
        {
            return MemcachedClientValueResult<IDictionary<string, T>>.Unsuccessful(
                $"An exception happened during {nameof(MultiGetSafeAsync)} execution.\nException details: {e}",
                defaultResultValue: new Dictionary<string, T>()
            );
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, T>> MultiGetAsync<T>(
        IEnumerable<string> keys,
        CancellationToken token,
        BatchingOptions batchingOptions = null,
        uint replicationFactor = 0)
    {

        var nodes = _nodeLocator.GetNodes(keys, replicationFactor);
        if (nodes.Keys.Count == 0)
        {
            // means no nodes for specified keys found
            return new Dictionary<string, T>();
        }

        if (batchingOptions is not null)
        {
            return await MultiGetBatchedInternalAsync<T>(nodes, batchingOptions, token);
        }

        var getCommandTasks = new List<Task<CommandExecutionResult>>(nodes.Count);

        foreach (var (node, keysToGet) in nodes)
        {
            var command = new MultiGetCommand(keysToGet, keysToGet.Count, _memcachedConfiguration.IsAllowLongKeys);
            var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

            getCommandTasks.Add(executeTask);
        }

        await Task.WhenAll(getCommandTasks);

        var result = new Dictionary<string, T>();

        foreach (var getCommandResult in getCommandTasks)
        {
            using var taskResult = getCommandResult.Result;

            if (!taskResult.Success)
            {
                // skip results that are not successful  
                continue;
            }

            var command = taskResult.GetCommandAs<MultiGetCommand>();

            if (command.Result is null or {Count: 0})
            {
                // skip results that are empty  
                continue;
            }

            foreach (var readItem in command.Result)
            {
                var key = readItem.Key;
                var cacheItem = readItem.Value;

                try
                {
                    var cachedValue = _binarySerializer.Deserialize<T>(cacheItem).Result;

                    result.TryAdd(key, cachedValue);
                }
                catch (Exception)when (_memcachedConfiguration.IsDeleteMemcachedKeyOnDeserializationFail)
                {
                    // means exception on deserialization happened
                    // assuming serializer change - remove this key from memcached to refresh data

                    await DeleteUndeserializableKey(key, token);
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> DeleteAsync(
        string key,
        CancellationToken token,
        CacheSyncOptions cacheSyncOptions = null)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientResult.Unsuccessful($"Memcached node for key {key} is not found");
            }

            using (var command = new DeleteCommand(key, _memcachedConfiguration.IsAllowLongKeys))
            {
                var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                var syncSuccess = false;
                if (IsCacheSyncEnabledInternal(cacheSyncOptions))
                {
                    syncSuccess = await _cacheSynchronizer.TryDeleteCacheAsync(new[] { key }, token);
                }

                return new MemcachedClientResult(
                        commandExecutionResult.Success,
                        errorMessage: commandExecutionResult.ErrorMessage)
                    .WithSyncSuccess(syncSuccess);
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(DeleteAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(DeleteAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> MultiDeleteAsync(
        IEnumerable<string> keys,
        CancellationToken token,
        BatchingOptions batchingOptions = null,
        CacheSyncOptions cacheSyncOptions = null,
        uint replicationFactor = 0)
    {
        try
        {
            // to avoid multiple enumeration
            var keysList = keys.ToList();

            var nodes = _nodeLocator.GetNodes(keysList, replicationFactor);
            if (nodes.Keys.Count == 0)
            {
                return MemcachedClientResult.Unsuccessful(
                    $"Memcached nodes for keys {string.Join(",", keysList)} not found");
            }

            if (batchingOptions is not null)
            {
                await MultiDeleteBatchedInternalAsync(nodes, batchingOptions, token);
            }

            var deleteTasks = new List<Task>(nodes.Count);

            foreach (var (replicatedNode, keysToDelete) in nodes)
            {
                foreach (var node in replicatedNode.EnumerateNodes())
                {
                    using (var command = new MultiDeleteCommand(
                               keysToDelete,
                               keysToDelete.Count,
                               _memcachedConfiguration.IsAllowLongKeys))
                    {
                        var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

                        deleteTasks.Add(executeTask);
                    }
                }
            }

            var syncSuccess = false;
            if (IsCacheSyncEnabledInternal(cacheSyncOptions))
            {
                syncSuccess = await _cacheSynchronizer.TryDeleteCacheAsync(keysList, token);
            }

            await Task.WhenAll(deleteTasks);

            return MemcachedClientResult.Successful.WithSyncSuccess(syncSuccess);
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(MultiDeleteAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(MultiDeleteAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<ulong>> IncrAsync(
        string key,
        ulong amountToAdd,
        ulong initialValue,
        TimeSpan? expirationTime,
        CancellationToken token)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientValueResult<ulong>.Unsuccessful($"Memcached node for key {key} is not found");
            }

            var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

            // ReSharper disable once ConvertToUsingDeclaration | Justification - we need to explicitly control when the command gets disposed
            using (var command = new IncrCommand(
                       key,
                       amountToAdd,
                       initialValue,
                       expiration,
                       _memcachedConfiguration.IsAllowLongKeys))
            {
                using var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                return new MemcachedClientValueResult<ulong>(
                    result.Success,
                    result.GetCommandAs<IncrCommand>().Result,
                    // successful incr command result can't be empty,
                    // while unsuccessful command result is always empty
                    isEmptyResult: !result.Success,
                    errorMessage: result.ErrorMessage
                );
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientValueResult<ulong>.Cancelled(nameof(IncrAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientValueResult<ulong>.Unsuccessful(
                $"An exception happened during {nameof(IncrAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<ulong>> DecrAsync(
        string key,
        ulong amountToSubtract,
        ulong initialValue,
        TimeSpan? expirationTime,
        CancellationToken token)
    {
        try
        {
            var node = _nodeLocator.GetNode(key);
            if (node == null)
            {
                return MemcachedClientValueResult<ulong>.Unsuccessful($"Memcached node for key {key} is not found");
            }

            var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

            using (var command = new DecrCommand(
                       key,
                       amountToSubtract,
                       initialValue,
                       expiration,
                       _memcachedConfiguration.IsAllowLongKeys))
            {
                using var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

                return new MemcachedClientValueResult<ulong>(
                    result.Success,
                    result.GetCommandAs<DecrCommand>().Result,
                    // successful decr command result can't be empty,
                    // while unsuccessful command result is always empty
                    isEmptyResult: !result.Success,
                    errorMessage: result.ErrorMessage
                );
            }
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientValueResult<ulong>.Cancelled(nameof(DecrAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientValueResult<ulong>.Unsuccessful(
                $"An exception happened during {nameof(DecrAsync)} execution.\nException details: {e}");
        }
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> FlushAsync(CancellationToken token)
    {
        try
        {
            var nodes = _nodeLocator.GetAllNodes();
            if (nodes == null
                || nodes.Length == 0)
            {
                return MemcachedClientResult.Unsuccessful("No memcached nodes found");
            }

            var command = new FlushCommand();
            var setTasks = new List<Task>(nodes.Length);
            var commandsToDispose = new List<MemcachedCommandBase>(nodes.Length);

            foreach (var node in nodes)
            {
                var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);
                setTasks.Add(executeTask);
                commandsToDispose.Add(command);
            }

            await Task.WhenAll(setTasks);

            foreach (var commandBase in commandsToDispose)
            {
                commandBase.Dispose();
            }

            return MemcachedClientResult.Successful;
        }
        catch (OperationCanceledException) when (_memcachedConfiguration.IsTerseCancellationLogging)
        {
            return MemcachedClientResult.Cancelled(nameof(FlushAsync));
        }
        catch (Exception e)
        {
            return MemcachedClientResult.Unsuccessful(
                $"An exception happened during {nameof(FlushAsync)} execution.\nException details: {e}");
        }
    }

    private async Task MultiStoreInternalAsync(
        IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> nodes,
        Dictionary<string, uint> keyToExpirationMap,
        IDictionary<string, CacheItemForRequest> keyValues,
        CancellationToken token,
        StoreMode storeMode = StoreMode.Set,
        BatchingOptions batchingOptions = null)
    {
        if (batchingOptions is not null)
        {
            await MultiStoreBatchedInternalAsync(
                nodes,
                keyValues,
                batchingOptions,
                keyToExpirationMap,
                storeMode,
                token);

            return;
        }

        var setTasks = new List<Task>(nodes.Count);
        var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);

        foreach (var replicatedNode in nodes)
        {
            var keys = replicatedNode.Value;

            foreach (var node in replicatedNode.Key.EnumerateNodes())
            {
                var keyValuesToStore = new Dictionary<string, CacheItemForRequest>();

                foreach (var key in keys)
                {
                    keyValuesToStore[key] = keyValues[key];
                }

                var command = new MultiStoreCommand(
                    storeMode,
                    keyValuesToStore,
                    keyToExpirationMap,
                    _memcachedConfiguration.IsAllowLongKeys);

                var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

                setTasks.Add(executeTask);
                commandsToDispose.Add(command);
            }
        }

        await Task.WhenAll(setTasks);

        // dispose only after deserialization is done and allocated memory from array pool can be returned
        foreach (var commandBase in commandsToDispose)
        {
            commandBase.Dispose();
        }
    }
    
    /// <inheritdoc />
    public bool IsCacheSyncEnabled() => _cacheSynchronizer != null
                                        && _cacheSynchronizer.IsCacheSyncEnabled();

    private async Task MultiStoreBatchedInternalAsync(
        IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> nodes,
        IDictionary<string, CacheItemForRequest> keyValues,
        BatchingOptions batchingOptions,
        Dictionary<string, uint> keyToExpirationMap,
        StoreMode storeMode,
        CancellationToken token)
    {
        if (batchingOptions.BatchSize <= 0)
        {
            throw new InvalidOperationException($"{nameof(batchingOptions.BatchSize)} should be > 0");
        }

        await Parallel.ForEachAsync(
            nodes,
            new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = batchingOptions.MaxDegreeOfParallelism
            },
            async (replicatedNodeWithKeys, cancellationToken) =>
            {
                var replicatedNode = replicatedNodeWithKeys.Key;
                var keysToStore = replicatedNodeWithKeys.Value;

                foreach (var node in replicatedNode.EnumerateNodes())
                {
                    foreach (var keysBatch in
                             keysToStore.Batch(batchingOptions.BatchSize))
                    {
                        var keyValuesToStore = new Dictionary<string, CacheItemForRequest>();
                        foreach (var key in keysBatch)
                        {
                            keyValuesToStore[key] = keyValues[key];
                        }

                        using (var command = new MultiStoreCommand(
                                   storeMode,
                                   keyValuesToStore,
                                   keyToExpirationMap,
                                   _memcachedConfiguration.IsAllowLongKeys))
                        {
                            await _commandExecutor.ExecuteCommandAsync(node, command, cancellationToken);
                        }
                    }
                }
            });
    }

    private async Task<IDictionary<string, T>> MultiGetBatchedInternalAsync<T>(
        IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> nodes,
        BatchingOptions batchingOptions,
        CancellationToken token)
    {
        // means batching is enabled - use separate logic
        if (batchingOptions.BatchSize <= 0)
        {
            throw new InvalidOperationException(
                $"{nameof(batchingOptions.BatchSize)} should be > 0. Please check BatchingOptions documentation");
        }

        var result = new ConcurrentDictionary<string, T>();

        await Parallel.ForEachAsync(
            nodes,
            new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = batchingOptions.MaxDegreeOfParallelism
            },
            async (replicatedNodeWithKeys, cancellationToken) =>
            {
                var node = replicatedNodeWithKeys.Key;
                var keysToGet = replicatedNodeWithKeys.Value;

                foreach (var keysBatch in keysToGet.Batch(batchingOptions.BatchSize))
                {
                    // since internally in ExecuteCommandAsync the command gets cloned and
                    // original command gets disposed we don't need to wrap it in using statement
                    var command = new MultiGetCommand(
                        keysBatch,
                        batchingOptions.BatchSize,
                        _memcachedConfiguration.IsAllowLongKeys);

                    using var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(
                        node,
                        command,
                        cancellationToken);

                    if (!commandExecutionResult.Success)
                    {
                        continue;
                    }

                    foreach (var readItem in commandExecutionResult.GetCommandAs<MultiGetCommand>().Result)
                    {
                        var key = readItem.Key;
                        var cacheItem = readItem.Value;

                        var cachedValue = _binarySerializer.Deserialize<T>(cacheItem).Result;

                        result.TryAdd(key, cachedValue);
                    }
                }
            });

        return result;
    }

    private async Task MultiDeleteBatchedInternalAsync(
        IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> nodes,
        BatchingOptions batchingOptions,
        CancellationToken token)
    {
        // means batching is enabled - use separate logic
        if (batchingOptions.BatchSize <= 0)
        {
            throw new InvalidOperationException($"{nameof(batchingOptions.BatchSize)} should be > 0");
        }

        await Parallel.ForEachAsync(
            nodes,
            new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = batchingOptions.MaxDegreeOfParallelism
            },
            async (replicatedNodeWithKeys, cancellationToken) =>
            {
                var replicatedNode = replicatedNodeWithKeys.Key;
                var keysToGet = replicatedNodeWithKeys.Value;

                foreach (var node in replicatedNode.EnumerateNodes())
                {
                    foreach (var keysBatch in keysToGet.Batch(batchingOptions.BatchSize))
                    {
                        using (var command = new MultiDeleteCommand(
                                   keysBatch,
                                   batchingOptions.BatchSize,
                                   _memcachedConfiguration.IsAllowLongKeys))
                        {
                            await _commandExecutor.ExecuteCommandAsync(
                                node,
                                command,
                                cancellationToken);
                        }
                    }
                }
            });
    }

    private bool IsCacheSyncEnabledInternal(CacheSyncOptions cacheSyncOptions)
        => IsCacheSyncEnabled()
           && (cacheSyncOptions == null || cacheSyncOptions.IsManualSyncOn);

    private async Task DeleteUndeserializableKey(string key, CancellationToken token)
    {
        _logger.LogWarning(
            "Failed to deserialize value for key {Key}. Assuming binary serializer change. Going to remove key from memcached",
            key);

        var deleteResult = await DeleteAsync(key, token);

        if (!deleteResult.Success)
        {
            _logger.LogError(
                "Failed to delete unserializable key {Key} from memcached. Error details : {ErrorDetails}",
                key,
                deleteResult.ErrorMessage);
        }
    }
}