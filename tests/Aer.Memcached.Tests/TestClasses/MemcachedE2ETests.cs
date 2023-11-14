using Aer.Memcached.Shared;
using Aer.Memcached.Shared.Models;
using Aer.Memcached.Tests.Helpers;
using Aer.Memcached.WebApi;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
        var httpServerFixture1 = new HttpServerFixture<Program>
        {
            Port = "5112"
        };
        
        var httpServerFixture2 = new HttpServerFixture<Program>
        {
            Port = "5113"
        };

        var client1 = new MemcachedWebApiClient(httpServerFixture1.CreateDefaultClient());
        var client2 = new MemcachedWebApiClient(httpServerFixture2.CreateDefaultClient());
        
        var keyValues = Enumerable.Range(0, 5)
            .ToDictionary(_ => Guid.NewGuid().ToString(), _ => Guid.NewGuid().ToString());

        await client1.MultiStore(new MultiStoreRequest
        {
            KeyValues = keyValues,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(2)
        });

        var result = await client1.MultiGet(new MultiGetRequest
        {
            Keys = keyValues.Keys.ToArray()
        });

        result.KeyValues.Should().BeEquivalentTo(keyValues);
        
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
        var httpServerFixture1 = new HttpServerFixture<Program>
        {
            Port = "5112"
        };
        
        var httpServerFixture2 = new HttpServerFixture<Program>
        {
            Port = "5113"
        };

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
}