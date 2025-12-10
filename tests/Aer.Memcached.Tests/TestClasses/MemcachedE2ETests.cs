using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Samples.Shared;
using Aer.Memcached.Samples.Shared.Models;
using Aer.Memcached.Samples.WebApi;
using Aer.Memcached.Tests.Helpers;
using Aer.Memcached.Tests.Infrastructure.Attributes;
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
        Environment.SetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT",
            "Development");
        
        _fixture = new Fixture();
    }

    private static bool IsWindows()
    {
        // On windows creating two separate servers on different ports does not work for some reason.
        // Thus we are ignoring tests using custom attributes.
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(false, true)]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_Success(bool withTimeSpan, bool withExpirationMap)
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

        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize servers to avoid race condition when both servers start simultaneously
        _ = httpServerFixture1.Services;
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = await StoreAndAssert(client1, withTimeSpan, true, withExpirationMap);

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

    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
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

        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize servers to avoid race condition when both servers start simultaneously
        _ = httpServerFixture1.Services;
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

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
    
    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
    public async Task WepApi_E2E_MultiDelete_NonExistentKeys_Success()
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

        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize servers to avoid race condition when both servers start simultaneously
        _ = httpServerFixture1.Services;
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        var result = await client1.MultiDelete(new MultiDeleteRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result.SyncSuccess.Should().BeTrue();

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
    
    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
    public async Task WepApi_E2E_MultiStore_SameKeysTwice_Success()
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

        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize servers to avoid race condition when both servers start simultaneously
        _ = httpServerFixture1.Services;
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        var multiStoreResult = await client1.MultiStore(new MultiStoreRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });
            
        multiStoreResult.SyncSuccess.Should().BeTrue();
        
        multiStoreResult = await client1.MultiStore(new MultiStoreRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });
            
        multiStoreResult.SyncSuccess.Should().BeTrue();

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
    
    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
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

        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize servers to avoid race condition when both servers start simultaneously
        _ = httpServerFixture1.Services;
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => _fixture.Create<ComplexModel>());

        var keysToGet = keyValues.Keys.ToArray();
        
        var storeResult = await client1.MultiStoreComplex(new MultiStoreComplexRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        storeResult.SyncSuccess.Should().BeTrue();

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

    [TestMethodWithIgnoreIfSupport]
    [IgnoreIf(nameof(IsWindows))]
    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    [DataRow(false, true)]
    public async Task WepApi_E2E_MultiStoreAndGet_WithCacheSync_CircuitBreaker(bool withTimeSpan, bool withExpirationMap)
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
            keyValues = await StoreAndAssert(client1, withTimeSpan, false, withExpirationMap);
            keyValuesArray.Add(keyValues);
        }

        // switch on second cluster
        var httpServerFixture2 = new HttpServerFixture<Samples.WepApiToSync.Program>
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

        // Explicitly initialize server to avoid race condition
        _ = httpServerFixture2.Services;
        await Task.Delay(100); // Give servers time to fully initialize

        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());

        // store more data while second cluster is still off for synchronizer
        keyValues = await StoreAndAssert(client1, withTimeSpan, true, withExpirationMap);
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
        keyValues = await StoreAndAssert(client1, withTimeSpan, true, withExpirationMap);
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
        bool withTimeSpan = false,
        bool syncSuccessShouldBe = true,
        bool withExpirationMap = false)
    {
        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        if (withTimeSpan)
        {
            var multiStoreResult = await client.MultiStore(new MultiStoreRequest
            {
                KeyValues = keyValues,
                TimeSpan = TimeSpan.FromMinutes(2),
                ExpirationMapWithDateTimeSpan = withExpirationMap ? keyValues.ToDictionary(key => key.Key, _ => (TimeSpan?)TimeSpan.FromMinutes(2)) : null
            });

            multiStoreResult.SyncSuccess.Should().Be(syncSuccessShouldBe);
        }
        else
        {
            var utcNow = DateTimeOffset.UtcNow;
            
            var multiStoreResult = await client.MultiStore(new MultiStoreRequest
            {
                KeyValues = keyValues,
                ExpirationTime = utcNow.AddMinutes(2),
                ExpirationMapWithDateTimeOffset = withExpirationMap ? keyValues.ToDictionary(key => key.Key, _ => (DateTimeOffset?)utcNow.AddMinutes(2)) : null
            });
            
            multiStoreResult.SyncSuccess.Should().Be(syncSuccessShouldBe);
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