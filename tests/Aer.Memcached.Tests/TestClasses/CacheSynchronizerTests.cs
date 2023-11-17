using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using AutoFixture;
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
    private readonly Fixture _fixture;

    public CacheSynchronizerTests()
    {
        _cacheSyncClient = Substitute.For<ICacheSyncClient>();
        _syncServersProvider = Substitute.For<ISyncServersProvider>();
        _errorStatisticsStore = Substitute.For<IErrorStatisticsStore>();
        _logger = Substitute.For<ILogger<CacheSynchronizer>>();

        _fixture = new Fixture();
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

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>{
            KeyValues = _fixture.Create<Dictionary<string, string>>(),
            ExpirationTime = _fixture.Create<DateTimeOffset>()
        }, CancellationToken.None);

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

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>{
            KeyValues = _fixture.Create<Dictionary<string, string>>(),
            ExpirationTime = _fixture.Create<DateTimeOffset>()
        }, CancellationToken.None);

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

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>
        {
            KeyValues = _fixture.Create<Dictionary<string, string>>(),
            ExpirationTime = _fixture.Create<DateTimeOffset>()
        }, CancellationToken.None);

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
        _errorStatisticsStore.GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>())
            .Returns(
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

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>{
            KeyValues = _fixture.Create<Dictionary<string, string>>(),
            ExpirationTime = _fixture.Create<DateTimeOffset>()
        }, CancellationToken.None);

        await _cacheSyncClient.Received(1).SyncAsync(syncServerNotTurnedOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _cacheSyncClient.Received(1).SyncAsync(syncServerToTurnOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerNotTurnedOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>{
            KeyValues = _fixture.Create<Dictionary<string, string>>(),
            ExpirationTime = _fixture.Create<DateTimeOffset>()
        }, CancellationToken.None);

        await _cacheSyncClient.Received(2).SyncAsync(syncServerNotTurnedOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _cacheSyncClient.Received(1).SyncAsync(syncServerToTurnOff,
            Arg.Any<CacheSyncModel<string>>(), Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(1)
            .GetErrorStatisticsAsync(syncServerToTurnOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
        await _errorStatisticsStore.Received(2)
            .GetErrorStatisticsAsync(syncServerNotTurnedOff.Address, Arg.Any<long>(), Arg.Any<TimeSpan>());
    }

    [TestMethod]
    public async Task Sync_SyncWindow_FilterAlreadySyncedKeyValues()
    {
        var syncServers = GetSyncServers(2);

        _syncServersProvider.IsConfigured().Returns(true);
        _syncServersProvider.GetSyncServers().Returns(syncServers);

        var syncInterval = TimeSpan.FromMilliseconds(200);

        var cacheSynchronizer = GetCacheSynchronizer(new MemcachedConfiguration
        {
            SyncSettings = new MemcachedConfiguration.SynchronizationSettings
            {
                CacheSyncInterval = syncInterval
            }
        });

        // make it twice to check window recreation is right
        await SyncWindowTest(syncInterval, cacheSynchronizer, syncServers);
        await SyncWindowTest(syncInterval, cacheSynchronizer, syncServers);
    }

    private async Task SyncWindowTest(
        TimeSpan syncInterval, 
        CacheSynchronizer cacheSynchronizer,
        MemcachedConfiguration.SyncServer[] syncServers)
    {
        var keyValuesToSync = _fixture.Create<Dictionary<string, string>>();
        var utcNowPlusMinute = DateTimeOffset.UtcNow.AddMinutes(1);

        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>
        {
            ExpirationTime = utcNowPlusMinute,
            KeyValues = keyValuesToSync.ToDictionary(key => key.Key, value => value.Value) // copy dict
        }, CancellationToken.None);

        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Is<CacheSyncModel<string>>(o => o.ExpirationTime == utcNowPlusMinute && DictAreEqual(o.KeyValues, keyValuesToSync)),
            Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
        
        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>
        {
            ExpirationTime = utcNowPlusMinute,
            KeyValues = keyValuesToSync.ToDictionary(key => key.Key, value => value.Value) // copy dict
        }, CancellationToken.None);

        // no more additional calls
        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Is<CacheSyncModel<string>>(o => o.ExpirationTime == utcNowPlusMinute && DictAreEqual(o.KeyValues, keyValuesToSync)),
            Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());

        var newKeyValue = _fixture.Create<KeyValuePair<string, string>>();
        keyValuesToSync.Add(newKeyValue.Key, newKeyValue.Value);
        
        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>
        {
            ExpirationTime = utcNowPlusMinute,
            KeyValues = keyValuesToSync.ToDictionary(key => key.Key, value => value.Value) // copy dict
        }, CancellationToken.None);
        
        // no calls with full collection
        await _cacheSyncClient.Received(0).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Is<CacheSyncModel<string>>(o => o.ExpirationTime == utcNowPlusMinute && DictAreEqual(o.KeyValues, keyValuesToSync)),
            Arg.Any<CancellationToken>());
        // one call with new keyValue
        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Is<CacheSyncModel<string>>(o => o.ExpirationTime == utcNowPlusMinute && DictAreEqual(o.KeyValues, new Dictionary<string, string>
            {
                {newKeyValue.Key, newKeyValue.Value}
            })),
            Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());

        await Task.Delay(syncInterval);
        
        await cacheSynchronizer.SyncCache(new CacheSyncModel<string>
        {
            ExpirationTime = utcNowPlusMinute,
            KeyValues = keyValuesToSync.ToDictionary(key => key.Key, value => value.Value) // copy dict
        }, CancellationToken.None);
        
        // new sync window is open
        await _cacheSyncClient.Received(syncServers.Length).SyncAsync(Arg.Any<MemcachedConfiguration.SyncServer>(),
            Arg.Is<CacheSyncModel<string>>(o => o.ExpirationTime == utcNowPlusMinute && DictAreEqual(o.KeyValues, keyValuesToSync)),
            Arg.Any<CancellationToken>());
        await _errorStatisticsStore.Received(0)
            .GetErrorStatisticsAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<TimeSpan>());
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

    private bool DictAreEqual(Dictionary<string, string> first, Dictionary<string, string> second)
    {
        if(first.Keys.Count != second.Keys.Count)
        {
            return false;
        }
        
        foreach (var key in first.Keys)
        {
            if (!second.ContainsKey(key))
            {
                return false;
            }

            if (!second[key].Equals(first[key]))
            {
                return false;
            }
        }

        return true;
    }
}