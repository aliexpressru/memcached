using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using MoreLinq.Extensions;

namespace Aer.Memcached.Client;

[SuppressMessage("ReSharper", "ConvertToUsingDeclaration", Justification = "We need to explicitly control when the command gets disposed")]
public class MemcachedClient<TNode> : IMemcachedClient where TNode : class, INode
{
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly ICommandExecutor<TNode> _commandExecutor;
    private readonly IExpirationCalculator _expirationCalculator;

    public MemcachedClient(
        INodeLocator<TNode> nodeLocator,
        ICommandExecutor<TNode> commandExecutor,
        IExpirationCalculator expirationCalculator)
    {
        _nodeLocator = nodeLocator;
        _commandExecutor = commandExecutor;
        _expirationCalculator = expirationCalculator;
    }

    /// <inheritdoc />
    public async Task<MemcachedClientResult> StoreAsync<T>(
        string key, 
        T value, 
        TimeSpan? expirationTime,
        CancellationToken token, 
        StoreMode storeMode = StoreMode.Set)
    {
        var node = _nodeLocator.GetNode(key);

        if (node == null)
        {
            return MemcachedClientResult.Unsuccessful;
        }

        var cacheItem = BinaryConverter.Serialize(value);
        var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

        using (var command = new StoreCommand(storeMode, key, cacheItem, expiration))
        {
            var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

            return new MemcachedClientResult
            {
                Success = result.Success
            };
        }
    }

    /// <inheritdoc />
    public async Task MultiStoreAsync<T>(
        Dictionary<string, T> keyValues, 
        TimeSpan? expirationTime, 
        CancellationToken token, 
        StoreMode storeMode = StoreMode.Set,
        BatchingOptions batchingOptions = null,
        uint replicationFactor = 0)
    {
        var nodes = _nodeLocator.GetNodes(keyValues.Keys, replicationFactor);
        if (nodes.Keys.Count == 0)
        {
            return;
        }

        var keyToExpirationMap = _expirationCalculator.GetExpiration(keyValues.Keys, expirationTime);

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
                    keyValuesToStore[key] = BinaryConverter.Serialize(keyValues[key]);
                }

                var command = new MultiStoreCommand(storeMode, keyValuesToStore, keyToExpirationMap);

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
    public async Task<MemcachedClientValueResult<T>> GetAsync<T>(string key, CancellationToken token)
    {
        var node = _nodeLocator.GetNode(key);
        if (node == null)
        {
            return MemcachedClientValueResult<T>.Unsuccessful;
        }

        using (var command = new GetCommand(key))
        {
            using var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);
            
            if (!commandExecutionResult.Success)
            {
                return MemcachedClientValueResult<T>.Unsuccessful;
            }

            var deserializationResult = BinaryConverter.Deserialize<T>(commandExecutionResult.GetCommandAs<GetCommand>().Result);

            return new MemcachedClientValueResult<T>
            {
                Success = true,
                Result = deserializationResult.Result,
                IsEmptyResult = deserializationResult.IsEmpty
            };
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
            var command = new MultiGetCommand(keysToGet, keysToGet.Count);
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

                var cachedValue = BinaryConverter.Deserialize<T>(cacheItem).Result;

                result.TryAdd(key, cachedValue);
            }
        }

        return result;
    }
    
    /// <inheritdoc />
    public async Task<MemcachedClientResult> DeleteAsync(string key, CancellationToken token)
    {
        var node = _nodeLocator.GetNode(key);
        if (node == null)
        {
            return MemcachedClientResult.Unsuccessful;
        }

        using (var command = new DeleteCommand(key))
        {
            var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);
            
            return new MemcachedClientResult
            {
                Success = commandExecutionResult.Success
            };
        }
    }
    
    /// <inheritdoc />
    public async Task MultiDeleteAsync(
        IEnumerable<string> keys,
        CancellationToken token,
        BatchingOptions batchingOptions = null,
        uint replicationFactor = 0)
    {
        var nodes = _nodeLocator.GetNodes(keys, replicationFactor);
        if (nodes.Keys.Count == 0)
        {
            return;
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
                using (var command = new MultiDeleteCommand(keysToDelete, keysToDelete.Count))
                {
                    var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

                    deleteTasks.Add(executeTask);
                }
            }
        }

        await Task.WhenAll(deleteTasks);
    }

    /// <inheritdoc />
    public async Task<MemcachedClientValueResult<ulong>> IncrAsync(
        string key, 
        ulong amountToAdd, 
        ulong initialValue,
        TimeSpan? expirationTime,
        CancellationToken token)
    {
        var node = _nodeLocator.GetNode(key);

        if (node == null)
        {
            return MemcachedClientValueResult<ulong>.Unsuccessful;
        }

        var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

        // ReSharper disable once ConvertToUsingDeclaration | Justification - we need to explicitly control when the command gets disposed
        using (var command = new IncrCommand(key, amountToAdd, initialValue, expiration))
        {
            using var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

            return new MemcachedClientValueResult<ulong>
            {
                Success = result.Success,
                Result = result.GetCommandAs<IncrCommand>().Result
            };
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
        var node = _nodeLocator.GetNode(key);

        if (node == null)
        {
            return MemcachedClientValueResult<ulong>.Unsuccessful;
        }

        var expiration = _expirationCalculator.GetExpiration(key, expirationTime);

        using (var command = new DecrCommand(key, amountToSubtract, initialValue, expiration))
        {
            using var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

            return new MemcachedClientValueResult<ulong>
            {
                Success = result.Success,
                Result = result.GetCommandAs<DecrCommand>().Result
            };
        }
    }

    public async Task FlushAsync(CancellationToken token)
    {
        var nodes = _nodeLocator.GetAllNodes();
        if (nodes == null || nodes.Length == 0)
        {
            return;
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
    }
    
    private async Task MultiStoreBatchedInternalAsync<T>(
        IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> nodes,
        Dictionary<string, T> keyValues,
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
                            keyValuesToStore[key] = BinaryConverter.Serialize(keyValues[key]);
                        }

                        using (var command = new MultiStoreCommand(storeMode, keyValuesToStore, keyToExpirationMap))
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
            throw new InvalidOperationException($"{nameof(batchingOptions.BatchSize)} should be > 0. Please check BatchingOptions documentation");
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
                    var command = new MultiGetCommand(keysBatch, batchingOptions.BatchSize);
                    
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

                        var cachedValue = BinaryConverter.Deserialize<T>(cacheItem).Result;

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
                        using (var command = new MultiDeleteCommand(keysBatch, batchingOptions.BatchSize))
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

    private uint GetExpiration(TimeSpan? expirationTime)
    {
        return expirationTime.HasValue
            ? (uint)(DateTimeOffset.UtcNow + expirationTime.Value).ToUnixTimeSeconds()
            : 0;
    }
}