using Aer.Memcached.Abstractions;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

public class DefaultSyncServersProvider: ISyncServersProvider
{
    private readonly MemcachedConfiguration _config;

    private readonly string _currentCluster;
    
    public DefaultSyncServersProvider(IOptions<MemcachedConfiguration> config)
    {
        _config = config.Value;

        if (_config.SyncSettings != null && !string.IsNullOrEmpty(_config.SyncSettings.ClusterNameEnvVariable))
        {
            _currentCluster = Environment.GetEnvironmentVariable(_config.SyncSettings.ClusterNameEnvVariable);    
        }
    }
    
    public MemcachedConfiguration.SyncServer[] GetSyncServers()
    {
        if (_config.SyncSettings == null)
        {
            return Array.Empty<MemcachedConfiguration.SyncServer>();
        }

        var servers = _config.SyncSettings.SyncServers;

        return servers
            .Where(s => s.ClusterName != _currentCluster)
            .ToArray();
    }
}