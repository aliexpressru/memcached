using Aer.Memcached.Client.Config;
using Aer.Memcached.Tests.Base;
using Aer.Memcached.Tests.Model.StoredObjects;
using AutoFixture;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

public class MemcachedClientTests_EnabledOperations_Base : MemcachedClientTestsBase
{
    public MemcachedClientTests_EnabledOperations_Base(EnabledOperations enabledOperations)
        : base(isSingleNodeCluster: true, enabledOperations: enabledOperations)
    {
    }

    [TestMethod]
    public virtual async Task StoreAndGet_CheckOperationIgnored()
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
        getValue.OperationDisabled.Should().BeTrue();
    }

    [TestMethod]
    public virtual async Task MultiStoreAndMultiGet_CheckOperationIgnored()
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
    public virtual async Task GetAndTouch_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();

        var getValue = await Client.GetAndTouchAsync<ObjectWithCollections>(key,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
        getValue.IsEmptyResult.Should().BeTrue();
        getValue.OperationDisabled.Should().BeTrue();
    }

    [TestMethod]
    public virtual async Task Decr_CheckOperationIgnored()
    {
        var key = GetTooLongKey();
        ulong initialValue = 15;

        var incrValue = await Client.DecrAsync(
            key,
            1,
            initialValue,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.IsEmptyResult.Should().BeTrue();
        incrValue.OperationDisabled.Should().BeTrue();
        incrValue.Result.Should().Be(default);
    }

    [TestMethod]
    public virtual async Task Incr_CheckOperationIgnored()
    {
        var key = GetTooLongKey();
        ulong initialValue = 15;

        var incrValue = await Client.IncrAsync(
            key,
            1,
            initialValue,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeTrue();
        incrValue.IsEmptyResult.Should().BeTrue();
        incrValue.OperationDisabled.Should().BeTrue();
        incrValue.Result.Should().Be(default);
    }

    [TestMethod]
    public virtual async Task Delete_CheckOperationIgnored()
    {
        const string key = "test";

        var deleteResult = await Client.DeleteAsync(key, CancellationToken.None);

        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeTrue();
    }

    [TestMethod]
    public virtual async Task MultiDelete_CheckOperationIgnored()
    {
        var deleteResult = await Client.MultiDeleteAsync([], CancellationToken.None);

        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeTrue();
    }

    [TestMethod]
    public virtual async Task Flush_CheckOperationIgnored()
    {
        var flushResult = await Client.FlushAsync(CancellationToken.None);

        flushResult.Success.Should().BeTrue();
        flushResult.OperationDisabled.Should().BeTrue();
    }
}