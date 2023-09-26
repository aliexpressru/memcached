using Aer.ConsistentHash;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached;

internal class NodeHealthChecker<TNode> : INodeHealthChecker<TNode> where TNode: class, INode
{
    private readonly ILogger<NodeHealthChecker<TNode>> _logger;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;

    public NodeHealthChecker(IOptions<MemcachedConfiguration> config, ILogger<NodeHealthChecker<TNode>> logger)
    {
        _logger = logger;
        _config = config.Value.SocketPool;
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
                socket = new PooledSocket(nodeEndPoint, _config.ConnectionTimeout, _logger);
                
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
                            "Node '{NodeEndPoint}' health check failed. Considering node dead",
                            nodeEndPoint.GetEndPointString());
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