using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class CacheSynchronizerTests
{
    private readonly ICacheSyncClient _cacheSyncClient;
    private readonly ISyncServersProvider _syncServersProvider;
    private readonly IErrorStatisticsStore _errorStatisticsStore;
    private readonly ILogger<CacheSynchronizer> _logger;

    public CacheSynchronizerTests()
    {
        _cacheSyncClient = Substitute.For<ICacheSyncClient>();
        _syncServersProvider = Substitute.For<ISyncServersProvider>();
        _errorStatisticsStore = Substitute.For<IErrorStatisticsStore>();
        _logger = Substitute.For<ILogger<CacheSynchronizer>>();
    }

    [TestInitialize]
    public void BeforeEachTest()
    {
        _cacheSyncClient.ClearSubstitute();
        _syncServersProvider.ClearSubstitute();
        _errorStatisticsStore.ClearSubstitute();
        _logger.ClearSubstitute();
    }

    [TestMethod]
    public async Task Sync_NotConfigured_NoCalls()
    {
        var cacheSynchronizer = GetCacheSynchronizer();

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);

        await _cacheSyncClient.Received(0).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
    }

    [TestMethod]
    public async Task Sync_Configured_NoErrors()
    {
        var syncServers = GetSyncServers(2);
        
        _syncServersProvider.IsConfigured().Returns(true);
        _syncServersProvider.GetSyncServers().Returns(syncServers);
        
        var cacheSynchronizer = GetCacheSynchronizer();

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);

        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
    }
    
    [TestMethod]
    public async Task Sync_Configured_Error_NoCircuitBreakerConfigured_NoCalls()
    {
        var syncServers = GetSyncServers(2);
        
        _syncServersProvider.IsConfigured().Returns(true);
        _syncServersProvider.GetSyncServers().Returns(syncServers);
        _cacheSyncClient
            .SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(), Arg.Any<CacheSyncModel<string>>(),
                Arg.Any<CancellationToken>()).Throws(new Exception());
        
        var cacheSynchronizer = GetCacheSynchronizer();

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);

        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
    }
    
    [TestMethod]
    public async Task Sync_Configured_Error_CircuitBreakerConfigured_CallToErrorStatisticsStore()
    {
        var syncServers = GetSyncServers(2);
        
        _syncServersProvider.IsConfigured().Returns(true);
        _syncServersProvider.GetSyncServers().Returns(syncServers);
        _cacheSyncClient
            .SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(), Arg.Any<CacheSyncModel<string>>(),
                Arg.Any<CancellationToken>()).Throws(new Exception());
        
        var cacheSynchronizer = GetCacheSynchronizer(new MemcachedConfiguration
        {
            SyncSettings = new MemcachedConfiguration.SynchronizationSettings
            {
                CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings()
            }
        });

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);

        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(syncServers.Length)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
    }
    
    [TestMethod]
    public async Task Sync_Configured_Error_CircuitBreakerConfigured_SwitchOffCheck()
    {
        var syncServers = GetSyncServers(2);

        var syncServerToTurnOff = syncServers.First();
        var syncServerNotTurnedOff = syncServers.Last();
        
        _syncServersProvider.IsConfigured().Returns(true);
        _syncServersProvider.GetSyncServers().Returns(syncServers);
        _cacheSyncClient
            .SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(), Arg.Any<CacheSyncModel<string>>(),
                Arg.Any<CancellationToken>()).Throws(new Exception());
        _errorStatisticsStore.GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>()).Returns(
            new ErrorStatistics
            {
                IsTooManyErrors = true
            });
            
        var cacheSynchronizer = GetCacheSynchronizer(new MemcachedConfiguration
        {
            SyncSettings = new MemcachedConfiguration.SynchronizationSettings
            {
                CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings()
            }
        });

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);

        await _cacheSyncClient.Received(1).SyncAsync(syncServerNotTurnedOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _cacheSyncClient.Received(1).SyncAsync(syncServerToTurnOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerNotTurnedOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
        
        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>(), CancellationToken.None);
        
        await _cacheSyncClient.Received(2).SyncAsync(syncServerNotTurnedOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _cacheSyncClient.Received(1).SyncAsync(syncServerToTurnOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
        await _errorStatisticsStore.Received(2)
            .GetErrorStatisticsAsync(syncServerNotTurnedOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
    }

    private CacheSynchronizer GetCacheSynchronizer(MemcachedConfiguration config = null)
    {
        var configToSet = new MemcachedConfiguration
        {
            SyncSettings = new MemcachedConfiguration.SynchronizationSettings()
        };

        if (config != null)
        {
            configToSet = config;
        }
        
        return new CacheSynchronizer(
            _syncServersProvider,
            _cacheSyncClient,
            _errorStatisticsStore,
            new OptionsWrapper<MemcachedConfiguration>(configToSet), _logger);
    }

    private MemcachedConfiguration.SyncServer[] GetSyncServers(int number)
    {
        return Enumerable.Range(0, number).Select(n => new MemcachedConfiguration.SyncServer
        {
            Address = $"test_address{n}",
            ClusterName = $"test{n}"
        }).ToArray();
    }
}