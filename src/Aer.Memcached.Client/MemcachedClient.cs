using System.Collections.Concurrent;
using Aer.ConsistentHash;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using MoreLinq.Extensions;

namespace Aer.Memcached.Client;

public class MemcachedClient<TNode> : IMemcachedClient where TNode : class, INode
{
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly ICommandExecutor<TNode> _commandExecutor;

    public MemcachedClient(
        INodeLocator<TNode> nodeLocator,
        ICommandExecutor<TNode> commandExecutor)
    {
        _nodeLocator = nodeLocator;
        _commandExecutor = commandExecutor;
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
        var expiration = GetExpiration(expirationTime);

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
        int replicationFactor = 0)
    {
        var nodes = _nodeLocator.GetNodes(keyValues.Keys, replicationFactor);
        if (nodes.Keys.Count == 0)
        {
            return;
        }

        var expiration = GetExpiration(expirationTime);

        if (batchingOptions is not null)
        {
            await MultiStoreBatchedInternalAsync(
                nodes,
                keyValues,
                batchingOptions,
                expiration,
                storeMode,
                token);
            return;
        }

        var setTasks = new List<Task>(nodes.Count);
        var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);

        foreach (var node in nodes)
        {
            var keys = node.Value;
            var keyValuesToStore = new Dictionary<string, CacheItemForRequest>();
            foreach (var key in keys)
            {
                keyValuesToStore[key] = BinaryConverter.Serialize(keyValues[key]);
            }

            var command = new MultiStoreCommand(storeMode, keyValuesToStore, expiration);

            var executeTask = _commandExecutor.ExecuteCommandAsync(node.Key, command, token);
            setTasks.Add(executeTask);
            commandsToDispose.Add(command);
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
            var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);
            if (!commandExecutionResult.Success)
            {
                return MemcachedClientValueResult<T>.Unsuccessful;
            }

            var result = BinaryConverter.Deserialize<T>(command.Result);
            return new MemcachedClientValueResult<T>
            {
                Success = true,
                Result = result
            };
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, T>> MultiGetAsync<T>(
        IEnumerable<string> keys,
        CancellationToken token,
        BatchingOptions batchingOptions = null,
        bool replicaFallback = false)
    {
        var replicationFactor = replicaFallback ? 1 : 0;
        var nodes = _nodeLocator.GetNodes(keys, replicationFactor);
        if (nodes.Keys.Count == 0)
        {
            return new Dictionary<string, T>();
        }

        if (batchingOptions is not null)
        {
            return await MultiGetBatchedInternalAsync<T>(nodes, batchingOptions, token);
        }

        var getTasks = new List<Task>(nodes.Count);
        var taskToCommands = new List<(Task<CommandExecutionResult> task, MultiGetCommand command)>(nodes.Count);
        var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);
        foreach (var (node, keysToGet) in nodes)
        {
            var command = new MultiGetCommand(keysToGet, keysToGet.Count);
            var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

            getTasks.Add(executeTask);
            taskToCommands.Add((executeTask, command));
            commandsToDispose.Add(command);
        }

        await Task.WhenAll(getTasks);

        var result = new Dictionary<string, T>();
        foreach (var taskToCommand in taskToCommands)
        {
            var taskResult = await taskToCommand.task;
            if (!taskResult.Success)
            {
                continue;
            }

            foreach (var item in taskToCommand.command.Result)
            {
                var key = item.Key;
                var cacheItem = item.Value;

                if (!result.ContainsKey(key))
                {
                    result[key] = BinaryConverter.Deserialize<T>(cacheItem);
                }
            }
        }

        // dispose only after deserialization is done and allocated memory from array pool can be returned
        foreach (var getCommand in commandsToDispose)
        {
            getCommand.Dispose();
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
        int replicationFactor = 0)
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
        foreach (var (node, keysToDelete) in nodes)
        {
            using (var command = new MultiDeleteCommand(keysToDelete, keysToDelete.Count))
            {
                var executeTask = _commandExecutor.ExecuteCommandAsync(node, command, token);

                deleteTasks.Add(executeTask);
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

        var expiration = GetExpiration(expirationTime);

        using (var command = new IncrCommand(key, amountToAdd, initialValue, expiration))
        {
            var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

            return new MemcachedClientValueResult<ulong>
            {
                Success = result.Success,
                Result = command.Result
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

        var expiration = GetExpiration(expirationTime);

        using (var command = new DecrCommand(key, amountToSubtract, initialValue, expiration))
        {
            var result = await _commandExecutor.ExecuteCommandAsync(node, command, token);

            return new MemcachedClientValueResult<ulong>
            {
                Success = result.Success,
                Result = command.Result
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
        IDictionary<TNode, ConcurrentBag<string>> nodes,
        Dictionary<string, T> keyValues,
        BatchingOptions batchingOptions,
        uint expiration,
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
            async (nodeWithKeys, cancellationToken) =>
            {
                var node = nodeWithKeys.Key;
                var keysToStore = nodeWithKeys.Value;

                foreach (var keysBatch in
                         keysToStore.Batch(batchingOptions.BatchSize))
                {
                    var keyValuesToStore = new Dictionary<string, CacheItemForRequest>();
                    foreach (var key in keysBatch)
                    {
                        keyValuesToStore[key] = BinaryConverter.Serialize(keyValues[key]);
                    }

                    using var command = new MultiStoreCommand(storeMode, keyValuesToStore, expiration);

                    await _commandExecutor.ExecuteCommandAsync(node, command, cancellationToken);
                }
            });
    }

    private async Task<IDictionary<string, T>> MultiGetBatchedInternalAsync<T>(
        IDictionary<TNode, ConcurrentBag<string>> nodes,
        BatchingOptions batchingOptions,
        CancellationToken token)
    {
        // means batching is enabled - use separate logic
        if (batchingOptions.BatchSize <= 0)
        {
            throw new InvalidOperationException($"{nameof(batchingOptions.BatchSize)} should be > 0");
        }
        
        var ret = new ConcurrentDictionary<string, T>();

        await Parallel.ForEachAsync(
            nodes,
            new ParallelOptions()
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = batchingOptions.MaxDegreeOfParallelism
            },
            async (nodeWithKeys, cancellationToken) =>
            {
                var node = nodeWithKeys.Key;
                var keysToGet = nodeWithKeys.Value;

                foreach(var keysBatch in keysToGet.Batch(batchingOptions.BatchSize))
                {
                    // ReSharper disable once ConvertToUsingDeclaration | Justification - we need to explicitly control when the command gets disposed
                    using (var command = new MultiGetCommand(keysBatch, batchingOptions.BatchSize))
                    {
                        var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(
                            node,
                            command,
                            cancellationToken);

                        if (commandExecutionResult.Success)
                        {
                            foreach (var item in command.Result)
                            {
                                var key = item.Key;
                                var cachedValue = BinaryConverter.Deserialize<T>(item.Value);

                                ret.TryAdd(key, cachedValue);
                            }
                        }
                    }
                }
            });

        return ret;
    }

    private async Task MultiDeleteBatchedInternalAsync(
        IDictionary<TNode, ConcurrentBag<string>> nodes,
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
            async (nodeWithKeys, cancellationToken) =>
            {
                var node = nodeWithKeys.Key;
                var keysToGet = nodeWithKeys.Value;

                foreach(var keysBatch in keysToGet.Batch(batchingOptions.BatchSize))
                {
                    // ReSharper disable once ConvertToUsingDeclaration | Justification - we need to explicitly control when the command gets disposed
                    using (var command = new MultiDeleteCommand(keysBatch, batchingOptions.BatchSize))
                    {
                        await _commandExecutor.ExecuteCommandAsync(
                            node,
                            command,
                            cancellationToken);
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