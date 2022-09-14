using Aer.ConsistentHash;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.ConnectionPool;
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
            
        var socket = new PooledSocket(node.GetEndpoint(), _config.ConnectionTimeout, _logger);
        try
        {
            while (connectionRetries > 0)
            {
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
                        _logger.LogError(e, "Node health check failed");
                    }
                }
            }

            return true;
        }
        finally
        {
            socket.Destroy();
        }
    }
}