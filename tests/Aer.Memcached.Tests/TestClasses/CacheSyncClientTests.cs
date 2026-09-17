using System.Net;
using Aer.Memcached.Client.CacheSync;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class CacheSyncClientTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly ILogger<CacheSyncClient> _logger = Substitute.For<ILogger<CacheSyncClient>>();

    [TestMethod]
    public async Task SyncAsync_AsyncException_RetriesConfiguredNumberOfTimes()
    {
        const int retryCount = 3;
        var handler = new FailThenSucceedHandler(maxFailures: int.MaxValue);
        _httpClientFactory.CreateClient().Returns(new HttpClient(handler));

        var client = GetCacheSyncClient(retryCount);

        var act = async () => await client.SyncAsync(
            new MemcachedConfiguration.SyncServer { Address = "http://test" },
            new CacheSyncModel
            {
                KeyValues = new Dictionary<string, byte[]> { ["key"] = [1] },
                ExpirationTime = DateTimeOffset.UtcNow.AddHours(1)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        handler.CallCount.Should().Be(retryCount + 1);
    }

    [TestMethod]
    public async Task SyncAsync_AsyncExceptionThenSuccess_RetriesAndSucceeds()
    {
        var handler = new FailThenSucceedHandler(maxFailures: 2);
        _httpClientFactory.CreateClient().Returns(new HttpClient(handler));

        var client = GetCacheSyncClient(retryCount: 3);

        var act = async () => await client.SyncAsync(
            new MemcachedConfiguration.SyncServer { Address = "http://test" },
            new CacheSyncModel
            {
                KeyValues = new Dictionary<string, byte[]> { ["key"] = [1] },
                ExpirationTime = DateTimeOffset.UtcNow.AddHours(1)
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        handler.CallCount.Should().Be(3);
    }

    private CacheSyncClient GetCacheSyncClient(int retryCount)
    {
        var config = new MemcachedConfiguration
        {
            SyncSettings = new MemcachedConfiguration.SynchronizationSettings
            {
                RetryCount = retryCount,
                RetryBaseDelay = TimeSpan.FromMilliseconds(1),
                SyncEndpoint = MemcachedConfiguration.DefaultSyncEndpoint,
                DeleteEndpoint = MemcachedConfiguration.DefaultDeleteEndpoint
            }
        };

        return new CacheSyncClient(
            _httpClientFactory,
            new OptionsWrapper<MemcachedConfiguration>(config),
            _logger);
    }

    private sealed class FailThenSucceedHandler(int maxFailures) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            if (CallCount <= maxFailures)
            {
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("Name or service not known"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
