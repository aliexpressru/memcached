using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Extensions;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

internal class NodeHealthChecker<TNode> : INodeHealthChecker<TNode> where TNode: class, INode
{
    private readonly ILogger<NodeHealthChecker<TNode>> _logger;
    private readonly MemcachedConfiguration _config;
    private readonly ICommandExecutor<TNode> _commandExecutor;

    public NodeHealthChecker(
        IOptions<MemcachedConfiguration> config, 
        ILogger<NodeHealthChecker<TNode>> logger,
        ICommandExecutor<TNode> commandExecutor)
    {
        _logger = logger;
        _commandExecutor = commandExecutor;
        _config = config.Value;
    }

    public async Task<bool> CheckNodeIsDeadAsync(TNode node)
    {
        var connectionRetries = 3;
        var nodeEndPoint = node.GetEndpoint();
        
        while (connectionRetries > 0)
        {
            PooledSocket socket = null;

            try
            {
                // We need to recreate it each time to resolve the exception:
                // System.PlatformNotSupportedException: Sockets on this platform are invalid for use after a failed connection attempt.
                socket = _config.MemcachedMaintainer.UseSocketPoolForNodeHealthChecks
                    ? await _commandExecutor.GetSocketForNodeAsync(
                        node,
                        isAuthenticateSocketIfRequired: false,
                        CancellationToken.None)
                    : new PooledSocket(nodeEndPoint, _config.SocketPool.ConnectionTimeout, _logger);
                
                try
                {
                    await socket.ConnectAsync(CancellationToken.None);
                    
                    return false;
                }
                catch (Exception e)
                {
                    connectionRetries--;

                    if (connectionRetries == 0)
                    {
                        _logger.LogError(
                            e,
                            "Node {EndPoint} health check failed. Considering node dead",
                            socket.EndPointAddressString);
                    }
                }
            }
            finally
            {
                if (_config.MemcachedMaintainer.UseSocketPoolForNodeHealthChecks)
                {
                    socket?.Dispose();
                }
                else
                {
                    socket?.Destroy();
                }
            }
        }

        return true;
    }
}