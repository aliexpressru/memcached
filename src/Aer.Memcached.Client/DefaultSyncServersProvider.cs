using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client;

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
        if (IsConfigured())
        {
            var servers = _config.SyncSettings.SyncServers;

            return servers
                .Where(s => s.ClusterName != _currentCluster)
                .ToArray();
        }

        return Array.Empty<MemcachedConfiguration.SyncServer>();
    }

    public bool IsConfigured()
    {
        return _config.SyncSettings != null && _config.SyncSettings.SyncServers?.Length != 0;
    }
}