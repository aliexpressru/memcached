using System.Collections.Concurrent;
using Aer.ConsistentHash;
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
    private static readonly NodeEqualityComparer<TNode> Comparer = new();

    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly ILogger<CommandExecutor<TNode>> _logger;
    private readonly ConcurrentDictionary<TNode, SocketPool> _socketPools;

    public CommandExecutor(
        IOptions<MemcachedConfiguration> config, 
        IAuthenticationProvider authenticationProvider,
        ILogger<CommandExecutor<TNode>> logger)
    {
        _config = config.Value.SocketPool;
        _authenticationProvider = authenticationProvider;
        _logger = logger;
        _socketPools = new ConcurrentDictionary<TNode, SocketPool>(new NodeEqualityComparer<TNode>());
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
            _logger.LogError(e, "Fatal error occured during command execution");

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
        var socketPools = new Dictionary<TNode, int>(Comparer);
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
            _logger.LogError(e, "Error occured during command execution");

            return CommandExecutionResult.Unsuccessful;
        }
    }

    private async Task<PooledSocket> GetSocketAsync(TNode node, CancellationToken token)
    {
        var socketPool = _socketPools.GetOrAdd(node, new SocketPool(node.GetEndpoint(), _config, _logger));
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

        if (startResult.StatusCode != 0x21) 
        {
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