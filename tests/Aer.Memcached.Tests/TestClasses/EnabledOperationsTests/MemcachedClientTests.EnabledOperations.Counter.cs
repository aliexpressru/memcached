using Aer.Memcached.Client.Config;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_Counter : MemcachedClientTests_EnabledOperations_Base
{
    public MemcachedClientTests_EnabledOperations_Counter() : base(EnabledOperations.Counter)
    {
    }

    [TestMethod]
    public override async Task Decr_CheckOperationIgnored()
    {
        var key = GetTooLongKey();
        ulong initialValue = 15;

        var decrValue = await Client.DecrAsync(
            key,
            1,
            initialValue,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        decrValue.Success.Should().BeFalse();
        decrValue.IsEmptyResult.Should().BeTrue();
        decrValue.OperationDisabled.Should().BeFalse();
        decrValue.Result.Should().Be(0);
    }

    [TestMethod]
    public override async Task Incr_CheckOperationIgnored()
    {
        var key = GetTooLongKey();
        ulong initialValue = 15;

        var incrValue = await Client.IncrAsync(
            key,
            1,
            initialValue,
            TimeSpan.FromSeconds(CacheItemExpirationSeconds), CancellationToken.None);

        incrValue.Success.Should().BeFalse();
        incrValue.IsEmptyResult.Should().BeTrue();
        incrValue.OperationDisabled.Should().BeFalse();
        incrValue.Result.Should().Be(0);
    }
}