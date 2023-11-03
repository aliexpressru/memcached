using Aer.Memcached.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

public class CacheSyncMaintainer: IHostedService, IDisposable
{
    private readonly ISyncServersProvider _syncServersProvider;
    private readonly ICacheSynchronizer _cacheSynchronizer;
    private readonly ILogger<CacheSyncMaintainer> _logger;
    private readonly MemcachedConfiguration _config;
    
    private Timer _nodeRebuildingTimer;

    public CacheSyncMaintainer(
        ISyncServersProvider syncServersProvider,
        ICacheSynchronizer cacheSynchronizer,
        IOptions<MemcachedConfiguration> config,
        ILogger<CacheSyncMaintainer> logger)
    {
        _syncServersProvider = syncServersProvider;
        _cacheSynchronizer = cacheSynchronizer;
        _logger = logger;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(CacheSyncMaintainer)} is running");

        if (_config.SyncSettings == null)
        {
            _logger.LogWarning("Cache settings are not configured. No cache sync maintenance will be performed");

            return Task.CompletedTask;
        }
        
        _nodeRebuildingTimer = new Timer(
            SyncServersHealthCheck,
            null,
            TimeSpan.Zero,
            _config.SyncSettings.SyncAddressesHealthCheckPeriod!.Value);
        
        _logger.LogInformation($"{nameof(SyncServersHealthCheck)} task is running");
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(CacheSyncMaintainer)} is stopping");

        _nodeRebuildingTimer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Health check for sync addresses 
    /// </summary>
    /// <param name="timerState">Timer state. Not used.</param>
    private void SyncServersHealthCheck(object timerState)
    {
        try
        {
            var syncAddresses = _syncServersProvider.GetSyncServers();
            
            // TODO: health check
            
            _cacheSynchronizer.UpdateSyncServers(syncAddresses);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while rebuilding nodes");
        }
    }

    public void Dispose()
    {
        _nodeRebuildingTimer?.Dispose();
    }
}