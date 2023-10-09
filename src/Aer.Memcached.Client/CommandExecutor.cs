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
    private readonly INodeLocator<TNode> _nodeLocator;

    public CommandExecutor(
        IOptions<MemcachedConfiguration> config, 
        IAuthenticationProvider authenticationProvider,
        ILogger<CommandExecutor<TNode>> logger,
        INodeLocator<TNode> nodeLocator)
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

            return CommandExecutionResult.Unsuccessful(command);
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

            return CommandExecutionResult.Unsuccessful(command);
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
    public IReadOnlyCollection<SocketPoolStatisctics> GetSocketPoolsStatistics(TNode[] nodes)
    {
        var ret = new List<SocketPoolStatisctics>(nodes.Length);
        
        SocketPool ValueFactory(TNode node) => new(node.GetEndpoint(), _config, _logger);
        
        foreach (var node in nodes)
        {
            var socketPool = _socketPools.GetOrAdd(node, ValueFactory);
            
            var socketPoolStatistics = new SocketPoolStatisctics(
                node.GetKey(),
                socketPool.PooledSocketsCount,
                socketPool.UsedSocketsCount,
                socketPool.RemainingPoolCapacity);
            
            ret.Add(socketPoolStatistics);
        }

        return ret;
    }

    /// <inheritdoc />
    public int DestroyAvailablePooledSocketsInAllSocketPools(int numberOfSocketsToDestroy)
    {
        int actuallyDestroyedSocketsCount = 0;
        
        // no rush here because it is used in background process
        foreach (var (_, socketPool) in _socketPools)
        {
            for (int i = 0; i < numberOfSocketsToDestroy; i++)
            {
                var isPooledSocketDestroyed = socketPool.DestroyPooledSocket();
                if (isPooledSocketDestroyed)
                {
                    actuallyDestroyedSocketsCount++;
                }
            }
        }

        return actuallyDestroyedSocketsCount;
    }

    /// <inheritdoc />
    public Task<PooledSocket> GetSocketForNodeAsync(TNode node, bool isAuthenticateSocketIfRequired, CancellationToken token)
    {
        return GetSocketAsync(node, isAuthenticateSocketIfRequired, token);
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

            // we preserve initial command to return data through it
            // we issue commands to all nodes including primary one as clones
            
            List<Task<CommandExecutionResult>> commandExecutionTasks = new(replicatedNode.NodeCount);

            foreach (var node in replicatedNode.EnumerateNodes())
            { 
                // since we are storing result of the command in the command itself - here we need to 
                // clone command before passing it for the execution

                var commandClone = command.Clone();
                var nodeExecutionTask = ExecuteCommandInternalAsync(node, commandClone, token);
                
                commandExecutionTasks.Add(nodeExecutionTask);
            }

            await Task.WhenAll(commandExecutionTasks);

            MemcachedCommandBase sucessfullCommand = null;
            
            foreach (var nodeExecutionTask in commandExecutionTasks)
            {
                var nodeExecutionIsSuccessful = nodeExecutionTask.Result.Success;
                var nodeCommand = nodeExecutionTask.Result.ExecutedCommand;

                if (sucessfullCommand is null && nodeExecutionIsSuccessful)
                {
                    // if the result of the command is not null - remember the command and discard all others
                    if (nodeCommand.HasResult)
                    {
                        sucessfullCommand = nodeCommand;
                        continue;
                    }
                }

                // dispose all other commands except successfull one to return rented read buffers
                nodeCommand.Dispose();
            }
            
            // since we are not using the command internal buffers to read result 
            // we don't need to dispose the command, this call is here for symmetry or future changes prposes
            command.Dispose();

            if (sucessfullCommand is not null)
            { 
                return CommandExecutionResult.Successful(sucessfullCommand);
            }

            // means no successful command found
            return CommandExecutionResult.Unsuccessful(command);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured during replicated command {Command} execution", command.ToString());

            return CommandExecutionResult.Unsuccessful(command);
        }
    }

    private async Task<CommandExecutionResult> ExecuteCommandInternalAsync(
        TNode node, 
        MemcachedCommandBase command,
        CancellationToken token)
    {
        try
        {
            using var socket = await GetSocketAsync(node, isAuthenticateSocketIfRequired: true, token);
            
            if (socket == null)
            {
                return CommandExecutionResult.Unsuccessful(command);
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
                
                return CommandExecutionResult.Unsuccessful(command);
            }
            
            var readResult = command.ReadResponse(socket);
            
            return readResult.Success 
                ? CommandExecutionResult.Successful(command) 
                : CommandExecutionResult.Unsuccessful(command);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured during command {Command} execution", command.ToString());

            return CommandExecutionResult.Unsuccessful(command);
        }
    }

    private async Task<PooledSocket> GetSocketAsync(
        TNode node,
        bool isAuthenticateSocketIfRequired,
        CancellationToken token)
    {
        var socketPool = _socketPools.GetOrAdd(
            node,
            valueFactory: static (n, args) =>
                new SocketPool(n.GetEndpoint(), args.Config, args.Logger),
            factoryArgument: (Config: _config, Logger: _logger)
        );

        if (socketPool.IsEndPointBroken)
        {
            // remove node from configuration if it's endpoint is considered broken
            _nodeLocator.MarkNodeDead(node);

            return null;
        }

        var pooledSocket = await socketPool.GetSocketAsync(token);

        if (pooledSocket == null)
        {
            return null;
        }

        if (!isAuthenticateSocketIfRequired
            || !_authenticationProvider.AuthRequired
            || pooledSocket.Authenticated)
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