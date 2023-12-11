using System.Net;
using System.Net.Sockets;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Shared;
using Aer.Memcached.Shared.Models;
using Aer.Memcached.Tests.Helpers;
using Aer.Memcached.WebApi;
using AutoFixture;
using FluentAssertions;
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

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_Success(bool withTimeSpan)
    {
        var port1 = GeneratePort();
        var port2 = GeneratePort();

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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port2}",
                                ClusterName = "test2"
                            }
                        },
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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port1}",
                                ClusterName = "test1"
                            }
                        },
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
        ;

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = await StoreAndAssert(client1, withTimeSpan);

        var result2 = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);

        try
        {
            await httpServerFixture1.DisposeAsync();
            await httpServerFixture2.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    [TestMethod]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSyncAndDelete_Success()
    {
        var port1 = GeneratePort();
        var port2 = GeneratePort();

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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port2}",
                                ClusterName = "test2"
                            }
                        },
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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port1}",
                                ClusterName = "test1"
                            }
                        },
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
        ;

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = await StoreAndAssert(client1);

        var result2 = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);

        await client1.MultiDelete(new MultiDeleteRequest
        {
            Keys = keyValues.Keys.ToArray()
        });
        
        var result1AfterDelete = await client1.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result1AfterDelete.KeyValues.Count.Should().Be(0);
        
        var result2AfterDelete = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2AfterDelete.KeyValues.Count.Should().Be(0);

        try
        {
            await httpServerFixture1.DisposeAsync();
            await httpServerFixture2.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    [TestMethod]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_ComplexModel_Success()
    {
        var port1 = GeneratePort();
        var port2 = GeneratePort();

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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port2}",
                                ClusterName = "test2"
                            }
                        },
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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port1}",
                                ClusterName = "test1"
                            }
                        },
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
        ;

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => _fixture.Create<ComplexModel>());

        var keysToGet = keyValues.Keys.ToArray();
        
        await client1.MultiStoreComplex(new MultiStoreComplexRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        var result = await client1.MultiGetComplex(new MultiGetComplexRequest
        {
            Keys = keysToGet
        });

        result.KeyValues.Should().BeEquivalentTo(keyValues);

        var result2 = await client2.MultiGetComplex(new MultiGetComplexRequest
        {
            Keys = keysToGet
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);

        try
        {
            await httpServerFixture1.DisposeAsync();
            await httpServerFixture2.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_CircuitBreaker(bool withTimeSpan)
    {
        var port1 = GeneratePort();
        var port2 = GeneratePort();

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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port2}",
                                ClusterName = "test2"
                            }
                        },
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
        // Store data while second cluster is off
        for (int i = 0; i < maxErrors; i++)
        {
            keyValues = await StoreAndAssert(client1, withTimeSpan);
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
                        SyncServers = new[]
                        {
                            new MemcachedConfiguration.SyncServer
                            {
                                Address = $"http://localhost:{port1}",
                                ClusterName = "test1"
                            }
                        },
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
        ;

        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        // store more data while second cluster is still off for synchronizer
        keyValues = await StoreAndAssert(client1, withTimeSpan);
        keyValuesArray.Add(keyValues);

        // no data is stored is second cluster
        MultiGetResponse result2;
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
        keyValues = await StoreAndAssert(client1, withTimeSpan);
        keyValuesArray.Add(keyValues);

        result2 = await client2.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result2.KeyValues.Should().BeEquivalentTo(keyValues);

        try
        {
            await httpServerFixture1.DisposeAsync();
            await httpServerFixture2.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private async Task<Dictionary<string, string>> StoreAndAssert(MemcachedWebApiClient client,
        bool withTimeSpan = false)
    {
        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        if (withTimeSpan)
        {
            await client.MultiStore(new MultiStoreRequest
            {
                KeyValues = keyValues,
                TimeSpan = TimeSpan.FromMinutes(2)
            });
        }
        else
        {
            await client.MultiStore(new MultiStoreRequest
            {
                KeyValues = keyValues,
                ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
            });
        }

        var result = await client.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result.KeyValues.Should().BeEquivalentTo(keyValues);

        return keyValues;
    }

    private string GeneratePort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        
        return port.ToString();
    }
}