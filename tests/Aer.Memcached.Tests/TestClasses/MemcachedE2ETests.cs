using Aer.Memcached.Client.Config;
using Aer.Memcached.Shared;
using Aer.Memcached.Shared.Models;
using Aer.Memcached.Tests.Helpers;
using Aer.Memcached.WebApi;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class MemcachedE2ETests
{
    private readonly Fixture _fixture;

    public MemcachedE2ETests()
    {
        _fixture = new Fixture();
    }

    [TestMethod]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_Success()
    {
        var port1 = "5112";
        var port2 = "5113";
        
        var httpServerFixture1 = new HttpServerFixture<Program>
        {
            Port = port1
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port2}",
                            ClusterName = "test2"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });

        var httpServerFixture2 = new HttpServerFixture<WepApiToSync.Program>
        {
            Port = port2
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port1}",
                            ClusterName = "test1"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });;

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());
        
        var keyValues = await StoreAndAssert(client1);
        
        var result2 = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);
        
        await httpServerFixture1.DisposeAsync();
        await httpServerFixture2.DisposeAsync();
    }
    
    [TestMethod]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_ComplexModel_Success()
    {
        var port1 = "5114";
        var port2 = "5115";
        
        var httpServerFixture1 = new HttpServerFixture<Program>
        {
            Port = port1
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port2}",
                            ClusterName = "test2"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });

        var httpServerFixture2 = new HttpServerFixture<WepApiToSync.Program>
        {
            Port = port2
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port1}",
                            ClusterName = "test1"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });;

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());
        
        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => _fixture.Create<ComplexModel>());

        await client1.MultiStoreComplex(new MultiStoreComplexRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        var result = await client1.MultiGetComplex(new MultiGetComplexRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result.KeyValues.Should().BeEquivalentTo(keyValues);
        
        var result2 = await client2.MultiGetComplex(new MultiGetComplexRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);
        
        await httpServerFixture1.DisposeAsync();
        await httpServerFixture2.DisposeAsync();
    }
    
    [TestMethod]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_CircuitBreaker()
    {
        var port1 = "5116";
        var port2 = "5117";
        
        var httpServerFixture1 = new HttpServerFixture<Program>
        {
            Port = port1
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port2}",
                            ClusterName = "test2"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });
        
        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());

        var keyValuesArray = new List<Dictionary<string, string>>();
        var maxErrors = 4;
        Dictionary<string, string> keyValues;
        MultiGetResponse? result;
        // Store data while second cluster is off
        for (int i = 0; i < maxErrors; i++)
        {
            keyValues = await StoreAndAssert(client1);
            keyValuesArray.Add(keyValues);
        }

        // switch on second cluster
        var httpServerFixture2 = new HttpServerFixture<WepApiToSync.Program>
        {
            Port = port2
        }.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MemcachedConfiguration>(configuration =>
                {
                    configuration.SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                    {
                        SyncServers = new [] {new MemcachedConfiguration.SyncServer
                        {
                            Address = $"http://localhost:{port1}",
                            ClusterName = "test1"
                        }},
                        CacheSyncCircuitBreaker = new MemcachedConfiguration.CacheSyncCircuitBreakerSettings
                        {
                            Interval = TimeSpan.FromSeconds(2),
                            SwitchOffTime = TimeSpan.FromSeconds(1),
                            MaxErrors = 3
                        }
                    };
                });
            });
        });;
        
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());
        
        // store more data while second cluster is still off for synchronizer
        keyValues = await StoreAndAssert(client1);
        keyValuesArray.Add(keyValues);

        // no data is stored is second cluster
        MultiGetResponse? result2;
        foreach (var keyValuesFromArr in keyValuesArray)
        {
            result2 = await client2.MultiGet(new MultiGetRequest
            {
                Keys = keyValuesFromArr.Keys.ToArray()
            });

            result2.KeyValues.Count.Should().Be(0);
        }

        await Task.Delay(TimeSpan.FromSeconds(1));

        // store more data while second cluster is on for synchronizer
        keyValues = await StoreAndAssert(client1);
        keyValuesArray.Add(keyValues);
        
        result2 = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);
        
        await httpServerFixture1.DisposeAsync();
        await httpServerFixture2.DisposeAsync();
    }

    private async Task<Dictionary<string, string>> StoreAndAssert(MemcachedWebApiClient client)
    {
        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        await client.MultiStore(new MultiStoreRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        var result = await client.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result.KeyValues.Should().BeEquivalentTo(keyValues);

        return keyValues;
    }
}