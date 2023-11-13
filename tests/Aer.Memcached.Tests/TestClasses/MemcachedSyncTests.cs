using Aer.Memcached.Shared;
using Aer.Memcached.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class MemcachedSyncTests
{
    private static MemcachedWebApiClient _client;
    
    [AssemblyInitialize]
    public static void Initialize(TestContext testContext)
    {
        var webApplicationFactory = new WebApplicationFactory<WebApi.Program>();

        _client = new MemcachedWebApiClient(webApplicationFactory.CreateClient());
    }

    [TestMethod]
    public async Task Test()
    {
        var key = Guid.NewGuid().ToString();
        
        await _client.MultiStore(new MultiStoreRequest
        {
            KeyValues = new Dictionary<string, string>
            {
                { key, Guid.NewGuid().ToString() }
            },
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        var result = await _client.MultiGet(new MultiGetRequest
        {
            Keys = new[] { key }
        });

        result.KeyValues.First().Key.Should().Be(key);
    }
}