using System.Net;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
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
        EndPoint nodeEndPoint = node.GetEndpoint();

        var nodeCheckResult = _config.MemcachedMaintainer.UseSocketPoolForNodeHealthChecks
            ? CheckSocketIsDeadUsingSocketPool(node)
            : CheckSocketIsDead(nodeEndPoint);

        return await nodeCheckResult;
    }

    /// <summary>
    /// Checks if the socket is dead using the socket pool.
    /// Gets the socket from socket pool and if it is not <c>null</c> checks its properties.
    /// No actual socket connection is made in this barnch.
    /// </summary>
    /// <param name="node">The node to check liveness of.</param>
    private async Task<bool> CheckSocketIsDeadUsingSocketPool(TNode node)
    {
        using var socketFromPool = await _commandExecutor.GetSocketForNodeAsync(
            node,
            isAuthenticateSocketIfRequired: false,
            CancellationToken.None);

        if (socketFromPool is null)
        { 
            // means the the endpoint is broken - consider node dead
            return true;
        }

        if (socketFromPool.Socket.Connected)
        {
            // no need to reconnect - just consider node alive 
            return false;
        }

        // finally - check the property on the pooled socket
        bool isNodeDead = !socketFromPool.IsAlive;

        return isNodeDead;
    }

    /// <summary>
    /// Checks if the socket is dead without using the socket pool.
    /// Jyst creates new socket to the specified edndpoint and tries to connect to it.
    /// </summary>
    /// <param name="nodeEndPoint">The endpoint to check liveness of.</param>
    private async Task<bool> CheckSocketIsDead(EndPoint nodeEndPoint)
    {
        var connectionRetries = 3;
        while (connectionRetries > 0)
        {
            PooledSocket socket = null;

            try
            {
                // We need to recreate the socket each time to avoid getting the exception:
                // System.PlatformNotSupportedException: Sockets on this platform are invalid for use after a failed connection attempt.
                socket = new PooledSocket(nodeEndPoint, _config.SocketPool.ConnectionTimeout, _logger);

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
                socket?.Destroy();
            }
        }

        return true;
    }
}