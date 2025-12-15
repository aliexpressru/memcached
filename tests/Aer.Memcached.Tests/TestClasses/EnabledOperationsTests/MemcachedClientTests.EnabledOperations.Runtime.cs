using Aer.Memcached.Client.Config;
using Aer.Memcached.Tests.Base;
using Aer.Memcached.Tests.Model.StoredObjects;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_Runtime : MemcachedClientTestsBase
{
    private static readonly IOptionsMonitor<MemcachedConfiguration.RuntimeConfiguration> _optionsMonitorMock =
        Substitute.For<IOptionsMonitor<MemcachedConfiguration.RuntimeConfiguration>>();

    public MemcachedClientTests_EnabledOperations_Runtime()
        : base(isSingleNodeCluster: true, optionsMonitorMock: _optionsMonitorMock)
    {
    }

    [TestMethod]
    public async Task StoreAndGet_CheckUpdateConfig()
    {
        _optionsMonitorMock.CurrentValue.Returns(new MemcachedConfiguration.RuntimeConfiguration
        {
            EnabledOperations = EnabledOperations.Store
        });

        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<ObjectWithCollections>();

        var storeResult = await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);

        var getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        storeResult.Success.Should().BeTrue();
        storeResult.OperationDisabled.Should().BeFalse();

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
        getValue.IsEmptyResult.Should().BeTrue();
        getValue.OperationDisabled.Should().BeTrue();

        _optionsMonitorMock.CurrentValue.Returns(new MemcachedConfiguration.RuntimeConfiguration
        {
            EnabledOperations = EnabledOperations.Get
        });

        getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeEquivalentTo(value);
        getValue.IsEmptyResult.Should().BeFalse();
        getValue.OperationDisabled.Should().BeFalse();
    }
    
    [TestMethod]
    public async Task Delete_CheckUpdateConfig()
    {
        _optionsMonitorMock.CurrentValue.Returns(new MemcachedConfiguration.RuntimeConfiguration
        {
            EnabledOperations = EnabledOperations.Store | EnabledOperations.Get
        });

        var key = Guid.NewGuid().ToString();
        var value = Fixture.Create<ObjectWithCollections>();

        var storeResult = await Client.StoreAsync(key, value, TimeSpan.FromSeconds(CacheItemExpirationSeconds),
            CancellationToken.None);
        
        var deleteResult = await Client.DeleteAsync(key, CancellationToken.None);
        
        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeTrue();

        var getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        storeResult.Success.Should().BeTrue();
        storeResult.OperationDisabled.Should().BeFalse();

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeEquivalentTo(value);
        getValue.IsEmptyResult.Should().BeFalse();
        getValue.OperationDisabled.Should().BeFalse();
        
        _optionsMonitorMock.CurrentValue.Returns(new MemcachedConfiguration.RuntimeConfiguration
        {
            EnabledOperations = EnabledOperations.Get | EnabledOperations.Delete
        });
        
        deleteResult = await Client.DeleteAsync(key, CancellationToken.None);

        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeFalse();

        getValue = await Client.GetAsync<ObjectWithCollections>(key, CancellationToken.None);

        getValue.Success.Should().BeTrue();
        getValue.Result.Should().BeNull();
        getValue.IsEmptyResult.Should().BeTrue();
        getValue.OperationDisabled.Should().BeFalse();
    }
}