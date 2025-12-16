using Aer.Memcached.Client.Config;
using Aer.Memcached.Tests.Model.StoredObjects;
using AutoFixture;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_Get : MemcachedClientTests_EnabledOperations_Base
{
    public MemcachedClientTests_EnabledOperations_Get() : base(EnabledOperations.Get)
    {
    }

    [TestMethod]
    public override async Task StoreAndGet_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<ObjectWithCollections>();

        var storeResult = await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        var getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        storeResult.Success.Should().BeTrue();
        storeResult.OperationDisabled.Should().BeTrue();

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
        getValue.IsEmptyResult.Should().BeTrue();
        getValue.OperationDisabled.Should().BeFalse();
    }

    [TestMethod]
    public override async Task MultiStoreAndMultiGet_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();
        const string value = "test";

        var storeResult = await Client.MultiStoreAsync(
            new Dictionary<string, string>()
            {
                [key] = value
            },
            TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        var getValue = await Client.MultiGetAsync<string>([key], CancellationToken.None);

        storeResult.Success.Should().BeTrue();
        storeResult.OperationDisabled.Should().BeTrue();

        getValue.Count.Should().Be(0);
    }

    [TestMethod]
    public override async Task GetAndTouch_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();

        var getValue = await Client.GetAndTouchAsync<ObjectWithCollections>(key,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
        getValue.IsEmptyResult.Should().BeTrue();
        getValue.OperationDisabled.Should().BeFalse();
    }
}