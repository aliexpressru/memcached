using System.Collections.Concurrent;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Infrastructure;
using Aer.Memcached.Client.Authentication;
using Aer.Memcached.Client.Commands;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client;

public class CommandExecutor<TNode> : ICommandExecutor<TNode> where TNode : class, INode
{
    private const int FURTHER_AUTHENTICATION_STEPS_REQUIRED_STATUS_CODE = 0x21;

    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly ILogger<CommandExecutor<TNode>> _logger;
    private readonly ConcurrentDictionary<TNode, SocketPool> _socketPools;
    private readonly HashRing<TNode> _nodeLocator;

    public CommandExecutor(
        IOptions<MemcachedConfiguration> config, 
        IAuthenticationProvider authenticationProvider,
        ILogger<CommandExecutor<TNode>> logger,
        HashRing<TNode> nodeLocator)
    {
        _config = config.Value.SocketPool;
        _authenticationProvider = authenticationProvider;
        _logger = logger;
        _nodeLocator = nodeLocator;
        _socketPools = new ConcurrentDictionary<TNode, SocketPool>(NodeEqualityComparer<TNode>.Instance);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        ReplicatedNode<TNode> node,
        MemcachedCommandBase command,
        CancellationToken token)
    {
        try
        {
            var diagnosticTimer = DiagnosticTimer.StartNew(command);

            var result = await ExecuteCommandInternalAsync(node, command, token);

            diagnosticTimer.StopAndWriteDiagnostics(result);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Fatal error occured during replicated node command '{Command}' execution", command.ToString());

            return CommandExecutionResult.Unsuccessful;
        }
    }

    /// <inheritdoc />
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        TNode node, 
        MemcachedCommandBase command,
        CancellationToken token)
    {
        try
        {
            var diagnosticTimer = DiagnosticTimer.StartNew(command);
            
            var result = await ExecuteCommandInternalAsync(node, command, token);
            
            diagnosticTimer.StopAndWriteDiagnostics(result);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Fatal error occured during node command '{Command}' execution", command.ToString());

            return CommandExecutionResult.Unsuccessful;
        }
    }

    /// <inheritdoc />
    public void RemoveSocketPoolForNodes(IEnumerable<TNode> nodes)
    {
        foreach (var node in nodes)
        {
            _socketPools.TryRemove(node, out var socketPool);
            socketPool?.Dispose();
        }
    }
    
    /// <inheritdoc />
    public IDictionary<TNode, int> GetSocketPoolsStatistics(TNode[] nodes)
    {
        var socketPools = new Dictionary<TNode, int>(NodeEqualityComparer<TNode>.Instance);
        SocketPool ValueFactory(TNode node) => new(node.GetEndpoint(), _config, _logger);
        
        foreach (var node in nodes)
        {
            var socketPool = _socketPools.GetOrAdd(node, ValueFactory);
            socketPools[node] = socketPool.AvailableSocketsCount;
        }

        return socketPools;
    }

    /// <inheritdoc />
    public async Task DestroyAvailableSockets(int numberOfSocketsToDestroy, CancellationToken token)
    {
        // no rush here because it is used in background process
        foreach (var socketPool in _socketPools)
        {
            for (int i = 0; i < numberOfSocketsToDestroy; i++)
            {
                await socketPool.Value.DestroyAvailableSocketAsync(token);
            }
        }
    }

    private async Task<CommandExecutionResult> ExecuteCommandInternalAsync(
        ReplicatedNode<TNode> replicatedNode,
        MemcachedCommandBase command,
        CancellationToken token)
    {
        try
        {
            if (!replicatedNode.HasReplicas)
            {
                return await ExecuteCommandInternalAsync(replicatedNode.PrimaryNode, command, token);
            }

            var nodeExecutionTasks = new List<Task<CommandExecutionResult>>();
            
            // we preserve initial command to return data through it
            // we issue commands to all nodes including primary one as clones
            
            List<(Task<CommandExecutionResult> NodeExecutionTask, MemcachedCommandBase NodeCommand)> tasksToCommands =
                new(replicatedNode.NodeCount);

            foreach (var node in replicatedNode.EnumerateNodes())
            { 
                // since we are storing result of the command in the command itself - here we need to 
                // clone command before passing it for the execution

                var commandClone = command.Clone();
                var nodeExecutionTask = ExecuteCommandInternalAsync(node, commandClone, token);
                
                nodeExecutionTasks.Add(nodeExecutionTask);
                tasksToCommands.Add((nodeExecutionTask, commandClone));
            }

            await Task.WhenAll(nodeExecutionTasks);

            bool wasSuccessfullResultSet = false;
            bool allNodesExecutionIsSuccessfull = true;
            
            foreach (var (nodeExecutionTask, nodeCommand) in tasksToCommands)
            {
                var nodeExecutionIsSuccessfull = nodeExecutionTask.Result.Success;
                allNodesExecutionIsSuccessfull &= nodeExecutionIsSuccessfull;

                if (!wasSuccessfullResultSet && nodeExecutionIsSuccessfull)
                {
                    // if the result was found and set - discard all other commands
                    bool wasResultSet = command.TrySetResultFrom(nodeCommand);
                    
                    wasSuccessfullResultSet = wasResultSet;
                }

                // dispose the command to return rented read buffers
                nodeCommand.Dispose();
            }

            if (wasSuccessfullResultSet)
            { 
                return CommandExecutionResult.Successful;
            }

            if (allNodesExecutionIsSuccessfull)
            { 
                // means there was no successfull result set but all nodes responses ended up successfully
                // this means all nodes returned no data - this is still technically a successfull result
                return CommandExecutionResult.Successful;
            }

            // means no successfull command found
            return CommandExecutionResult.Unsuccessful;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured during command '{Command}' execution", command.ToString());

            return CommandExecutionResult.Unsuccessful;
        }
    }

    private async Task<CommandExecutionResult> ExecuteCommandInternalAsync(
        TNode node, 
        MemcachedCommandBase command,
        CancellationToken token)
    {
        try
        {
            using var socket = await GetSocketAsync(node, token);
            
            if (socket == null)
            {
                return CommandExecutionResult.Unsuccessful;
            }

            var buffer = command.GetBuffer();
            
            var writeSocketTask = socket.WriteAsync(buffer);
            
            try
            {
                await writeSocketTask.WaitAsync(_config.ReceiveTimeout, token);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Write to socket timed out");
                
                return CommandExecutionResult.Unsuccessful;
            }
            
            var readResult = command.ReadResponse(socket);
            
            return readResult.Success 
                ? CommandExecutionResult.Successful 
                : CommandExecutionResult.Unsuccessful;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured during command '{Command}' execution", command.ToString());

            return CommandExecutionResult.Unsuccessful;
        }
    }

    private async Task<PooledSocket> GetSocketAsync(TNode node, CancellationToken token)
    {
        var socketPool = _socketPools.GetOrAdd(
            node, 
            static (n, args) =>
                new SocketPool(n.GetEndpoint(), args.Config, args.Logger),
            (Config: _config, Logger: _logger));

        if (socketPool.IsEndPointBroken)
        {
            // remove node from configuration if it's endpoint is considered broken
            _nodeLocator.RemoveNode(node);
            
            return null;
        }

        var pooledSocket = await socketPool.GetAsync(token);
        
        if (pooledSocket == null)
        {
            return null;
        }
        
        if (!_authenticationProvider.AuthRequired || pooledSocket.Authenticated)
        {
            return pooledSocket;
        }

        await AuthenticateAsync(pooledSocket);
        
        return pooledSocket;
    }
    
    private async Task AuthenticateAsync(PooledSocket pooledSocket)
    {
        var saslStart = new SaslStartCommand(_authenticationProvider.GetAuthData());
        await pooledSocket.WriteAsync(saslStart.GetBuffer());

        var startResult = saslStart.ReadResponse(pooledSocket);
        if (startResult.Success)
        {
            pooledSocket.Authenticated = true;
            return;
        }
        
        if (startResult.StatusCode != FURTHER_AUTHENTICATION_STEPS_REQUIRED_STATUS_CODE) 
        {
            // means that sasl start result is niether a success
            // nor the one that indicates that additional steps required
            throw new AuthenticationException();
        }
        
        // Further authentication steps required
        var saslStep = new SaslStepCommand(saslStart.Data.ToArray());
        await pooledSocket.WriteAsync(saslStep.GetBuffer());

        var saslStepResult = saslStep.ReadResponse(pooledSocket);
        if (!saslStepResult.Success)
        {
            throw new AuthenticationException();
        }

        pooledSocket.Authenticated = true;
    }
}