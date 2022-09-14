using Aer.ConsistentHash;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;

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
        StoreMode storeMode = StoreMode.Set)
    {
        var nodes = _nodeLocator.GetNodes(keyValues.Keys);
        if (nodes.Keys.Count == 0)
        {
            return;
        }

        var setTasks = new List<Task>(nodes.Count);
        var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);
        foreach (var node in nodes)
        {
            var expiration = GetExpiration(expirationTime);

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
    public async Task<MemcachedClientGetResult<T>> GetAsync<T>(string key, CancellationToken token)
    {
        var node = _nodeLocator.GetNode(key);
        if (node == null)
        {
            return MemcachedClientGetResult<T>.Unsuccessful;
        }

        using (var command = new GetCommand(key))
        {
            var commandExecutionResult = await _commandExecutor.ExecuteCommandAsync(node, command, token);
            if (!commandExecutionResult.Success)
            {
                return MemcachedClientGetResult<T>.Unsuccessful;
            }

            var result = BinaryConverter.Deserialize<T>(command.Result);
            return new MemcachedClientGetResult<T>
            {
                Success = true,
                Result = result
            };
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, T>> MultiGetAsync<T>(IEnumerable<string> keys, CancellationToken token)
    {
        var nodes = _nodeLocator.GetNodes(keys);
        if (nodes.Keys.Count == 0)
        {
            return new Dictionary<string, T>();
        }

        var getTasks = new List<Task>(nodes.Count);
        var taskToCommands = new List<(Task<CommandExecutionResult> task, MultiGetCommand command)>(nodes.Count);
        var commandsToDispose = new List<MemcachedCommandBase>(nodes.Count);
        foreach (var node in nodes)
        {
            var command = new MultiGetCommand(node.Value.ToArray());
            var executeTask = _commandExecutor.ExecuteCommandAsync(node.Key, command, token);
            
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

                result[key] = BinaryConverter.Deserialize<T>(cacheItem);
            }
        }

        // dispose only after deserialization is done and allocated memory from array pool can be returned
        foreach (var getCommand in commandsToDispose)
        {
            getCommand.Dispose();
        }

        return result;
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

    private uint GetExpiration(TimeSpan? expirationTime)
    {
        return expirationTime.HasValue
            ? (uint)(DateTimeOffset.UtcNow + expirationTime.Value).ToUnixTimeSeconds()
            : 0;
    }
}