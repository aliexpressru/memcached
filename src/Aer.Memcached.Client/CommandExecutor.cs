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
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Client;

/// <summary>
/// The memcached command executor implementation. 
/// </summary>
/// <typeparam name="TNode">The type of the hash ring node.</typeparam>
public class CommandExecutor<TNode> : ICommandExecutor<TNode>
    where TNode : class, INode
{
    private const int FURTHER_AUTHENTICATION_STEPS_REQUIRED_STATUS_CODE = 0x21;

    private readonly MemcachedConfiguration _config;
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly ILogger<CommandExecutor<TNode>> _logger;
    private readonly ConcurrentDictionary<TNode, SocketPool> _socketPools;
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly Tracer _tracer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutor{TNode}"/> class.
    /// </summary>
    /// <param name="config">The memcached configuration.</param>
    /// <param name="authenticationProvider">The memcached authentication provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="nodeLocator">The memcached node locator.</param>
    /// <param name="tracer">The OpenTelemetry tracer (optional).</param>
    public CommandExecutor(
        IOptions<MemcachedConfiguration> config,
        IAuthenticationProvider authenticationProvider,
        ILogger<CommandExecutor<TNode>> logger,
        INodeLocator<TNode> nodeLocator,
        Tracer tracer = null)
    {
        _config = config.Value;
        _authenticationProvider = authenticationProvider;
        _logger = logger;
        _nodeLocator = nodeLocator;
        _tracer = tracer;
        _socketPools = new ConcurrentDictionary<TNode, SocketPool>(NodeEqualityComparer<TNode>.Instance);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        ReplicatedNode<TNode> node,
        MemcachedCommandBase command,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        var diagnosticTimer = DiagnosticTimer.StartNew(command);

        var result = await ExecuteCommandInternalAsync(node, command, token, tracingOptions);

        diagnosticTimer.StopAndWriteDiagnostics(result);

        return result;
    }

    /// <inheritdoc />
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        TNode node,
        MemcachedCommandBase command,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        var diagnosticTimer = DiagnosticTimer.StartNew(command);

        var result = await ExecuteCommandInternalAsync(node, command, token, tracingOptions);

        diagnosticTimer.StopAndWriteDiagnostics(result);

        return result;
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

        SocketPool ValueFactory(TNode node) => new(node.GetEndpoint(), _config.SocketPool, _logger);

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
    public Task<PooledSocket> GetSocketForNodeAsync(
        TNode node,
        bool isAuthenticateSocketIfRequired,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        return GetSocketAsync(node, isAuthenticateSocketIfRequired, token, tracingOptions);
    }

    private async Task<CommandExecutionResult> ExecuteCommandInternalAsync(
        ReplicatedNode<TNode> replicatedNode,
        MemcachedCommandBase command,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        using var tracingScope = MemcachedTracing.CreateCommandScope(
            _tracer,
            command,
            replicatedNode.PrimaryNode,
            true,
            replicatedNode.ReplicaNodes.Count,
            _logger,
            tracingOptions);

        try
        {
            if (!replicatedNode.HasReplicas)
            {
                var result = await ExecuteCommandInternalAsync(replicatedNode.PrimaryNode, command, token, tracingOptions);

                tracingScope?.SetResult(result.Success, result.ErrorMessage);
                return result;
            }

            // we preserve initial command to return data through it.
            // we issue commands to all nodes including primary one as clones

            List<Task<CommandExecutionResult>> commandExecutionTasks = new(replicatedNode.NodeCount);

            foreach (var node in replicatedNode.EnumerateNodes())
            {
                // since we are storing result of the command in the command itself - here we need to 
                // clone command before passing it for the execution

                var commandClone = command.Clone();
                var nodeExecutionTask = ExecuteCommandInternalAsync(node, commandClone, token, tracingOptions);

                commandExecutionTasks.Add(nodeExecutionTask);
            }

            await Task.WhenAll(commandExecutionTasks);

            MemcachedCommandBase successfulCommand = null;

            foreach (var nodeExecutionTask in commandExecutionTasks)
            {
                var nodeExecutionIsSuccessful = nodeExecutionTask.Result.Success;
                var nodeCommand = nodeExecutionTask.Result.ExecutedCommand;

                if (successfulCommand is null && nodeExecutionIsSuccessful)
                {
                    // if the result of the command is not null - remember the command and discard all others
                    if (nodeCommand.HasResult)
                    {
                        successfulCommand = nodeCommand;
                        continue;
                    }
                }

                // dispose all other commands except successful one to return rented read buffers
                nodeCommand.Dispose();
            }

            // since we are not using the command internal buffers to read result 
            // we don't need to dispose the command, this call is here for symmetry or future changes purposes
            command.Dispose();

            CommandExecutionResult finalResult;

            if (successfulCommand is not null)
            {
                finalResult = CommandExecutionResult.Successful(successfulCommand);
            }
            else
            {
                // means no successful command found
                finalResult = CommandExecutionResult.Unsuccessful(command, "All commands on all replica nodes failed");
            }

            tracingScope?.SetResult(finalResult.Success, finalResult.ErrorMessage);
            return finalResult;
        }
        catch (OperationCanceledException) when (_config.IsTerseCancellationLogging)
        {
            // just rethrow this exception and don't log any details.
            // it will be handled in MemcachedClient
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error occured during replicated command {Command} on primary node {Node} and replica nodes {ReplicaNodes} execution",
                command.ToString(),
                replicatedNode.PrimaryNode.GetKey(),
                replicatedNode.ReplicaNodes.Select(n => n.GetKey()));

            tracingScope?.SetError(e);
            return CommandExecutionResult.Unsuccessful(command, e.Message);
        }
    }

    private async Task<CommandExecutionResult> ExecuteCommandInternalAsync(
        TNode node,
        MemcachedCommandBase command,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        using var tracingScope = MemcachedTracing.CreateCommandScope(
            _tracer,
            command,
            node,
            false,
            0,
            _logger,
            tracingOptions);

        try
        {
            using var socket = await GetSocketAsync(node, isAuthenticateSocketIfRequired: true, token, tracingOptions);

            if (socket == null)
            {
                var failureResult = CommandExecutionResult.Unsuccessful(command, "Socket not found");
                tracingScope?.SetResult(false, "Socket not found");
                return failureResult;
            }

            var buffer = command.GetBuffer();

            var writeSocketTask = socket.WriteAsync(buffer);

            try
            {
                await writeSocketTask.WaitAsync(_config.SocketPool.ReceiveTimeout, token);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Write to socket {SocketAddress} timed out", socket.EndPointAddressString);

                var errorMessage = $"Write to socket {socket.EndPointAddressString} timed out";
                var timeoutResult = CommandExecutionResult.Unsuccessful(command, errorMessage);
                tracingScope?.SetResult(false, errorMessage);
                return timeoutResult;
            }

            CommandResult readResult;
            try
            {
                readResult = await command.ReadResponseAsync(socket, token);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Read from socket {SocketAddress} timed out", socket.EndPointAddressString);

                var errorMessage = $"Read from socket {socket.EndPointAddressString} timed out";
                var timeoutResult = CommandExecutionResult.Unsuccessful(command, errorMessage);
                tracingScope?.SetResult(false, errorMessage);
                return timeoutResult;
            }

            var result = readResult.Success
                ? CommandExecutionResult.Successful(command)
                : CommandExecutionResult.Unsuccessful(command, readResult.Message);

            tracingScope?.SetResult(result.Success, result.ErrorMessage);
            return result;
        }
        catch (OperationCanceledException) when (_config.IsTerseCancellationLogging)
        {
            // just rethrow this exception and don't log any details.
            // it will be handled in MemcachedClient
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error occured during command {Command} on node {Node} execution",
                command.ToString(),
                node.GetKey());

            tracingScope?.SetError(e);
            return CommandExecutionResult.Unsuccessful(command, e.Message);
        }
    }

    private async Task<PooledSocket> GetSocketAsync(
        TNode node,
        bool isAuthenticateSocketIfRequired,
        CancellationToken token,
        TracingOptions tracingOptions = null)
    {
        var socketPool = _socketPools.GetOrAdd(
            node,
            valueFactory: static (n, args) =>
                new SocketPool(n.GetEndpoint(), args.Config.SocketPool, args.Logger, args.Tracer),
            factoryArgument: (Config: _config, Logger: _logger, Tracer: _tracer)
        );

        if (socketPool.IsEndPointBroken)
        {
            // remove node from configuration if it's endpoint is considered broken
            _nodeLocator.MarkNodeDead(node);
            // then remove socket pool with broken endpoint to not get into this pool again
            _socketPools.TryRemove(node, out _);

            return null;
        }

        var pooledSocket = await socketPool.GetSocketAsync(token, tracingOptions);

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

        await AuthenticateAsync(pooledSocket, socketPool);

        return pooledSocket;
    }

    private async Task AuthenticateAsync(PooledSocket pooledSocket, SocketPool socketPool, TracingOptions tracingOptions = null)
    {
        using var tracingScope = MemcachedTracing.CreateSocketOperationScope(
            _tracer,
            "socket.authenticate",
            pooledSocket.EndPointAddressString,
            _config.SocketPool.MaxPoolSize,
            socketPool.UsedSocketsCount,
            _logger,
            tracingOptions);

        var saslStart = new SaslStartCommand(_authenticationProvider.GetAuthData());
        await pooledSocket.WriteAsync(saslStart.GetBuffer());

        var startResult = await saslStart.ReadResponseAsync(pooledSocket);
        if (startResult.Success)
        {
            tracingScope?.SetResult(true);
            pooledSocket.Authenticated = true;
            return;
        }

        try
        {
            if (startResult.StatusCode != FURTHER_AUTHENTICATION_STEPS_REQUIRED_STATUS_CODE)
            {
                // means that sasl start result is neither a success
                // nor the one that indicates that additional steps required
                throw new AuthenticationException();
            }

            // Further authentication steps required
            var saslStep = new SaslStepCommand(saslStart.Data.ToArray());
            await pooledSocket.WriteAsync(saslStep.GetBuffer());

            var saslStepResult = await saslStep.ReadResponseAsync(pooledSocket);
            if (!saslStepResult.Success)
            {
                throw new AuthenticationException();
            }

            pooledSocket.Authenticated = true;
        }
        catch (Exception e)
        {
            tracingScope?.SetError(e);

            _logger.LogError(
                e,
                "Error occured during socket {SocketAddress} authentication",
                pooledSocket.EndPointAddressString);

            // in case of any authentication failure - dispose the socket
            pooledSocket.Dispose();

            throw;
        }
    }
}