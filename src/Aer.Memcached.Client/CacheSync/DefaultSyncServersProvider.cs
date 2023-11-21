using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client.CacheSync;

public class DefaultSyncServersProvider: ISyncServersProvider
{
    private readonly MemcachedConfiguration _config;

    private readonly string _currentCluster;
    private readonly bool _isConfigured;

    private MemcachedConfiguration.SyncServer[] _syncServers;
    
    public DefaultSyncServersProvider(IOptions<MemcachedConfiguration> config)
    {
        _config = config.Value;

        if (_config.SyncSettings != null && !string.IsNullOrEmpty(_config.SyncSettings.ClusterNameEnvVariable))
        {
            _currentCluster = Environment.GetEnvironmentVariable(_config.SyncSettings.ClusterNameEnvVariable);    
        }

        if (_config.SyncSettings != null && _config.SyncSettings.SyncServers?.Length != 0)
        {
            _isConfigured = true;
        }
    }
    
    public MemcachedConfiguration.SyncServer[] GetSyncServers()
    {
        if (_syncServers != null)
        {
            return _syncServers;
        }
        
        if (_isConfigured)
        {
            var servers = _config.SyncSettings.SyncServers;

            _syncServers = servers
                .Where(s => s.ClusterName != _currentCluster)
                .ToArray();
        }
        else
        {
            _syncServers = Array.Empty<MemcachedConfiguration.SyncServer>();    
        }
        
        return _syncServers;
    }

    public bool IsConfigured()
    {
        return _isConfigured;
    }
}