using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class DefaultSyncServersProviderTests
{
    [TestMethod]
    public void FilterCurrentClusterServer()
    {
        var envName = "CLUSTER_NAME";
        var currentCluster = "cluster1";
        Environment.SetEnvironmentVariable(envName, currentCluster);

        var resultServer = new MemcachedConfiguration.SyncServer
        {
            Address = "test.address.cluster2",
            ClusterName = "cluster2"
        };
        
        var serversProvider = new DefaultSyncServersProvider(new OptionsWrapper<MemcachedConfiguration>(
            new MemcachedConfiguration
            {
                SyncSettings = new MemcachedConfiguration.SynchronizationSettings
                {
                    ClusterNameEnvVariable = "CLUSTER_NAME",
                    SyncServers = new[]
                    {
                        new MemcachedConfiguration.SyncServer
                        {
                            Address = "test.address.cluster1",
                            ClusterName = currentCluster
                        },
                        resultServer
                    }
                }
            }));

        var servers = serversProvider.GetSyncServers();

        servers.Length.Should().Be(1);
        servers.First().Should().BeEquivalentTo(resultServer);
    }
}