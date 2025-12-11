using Aer.Memcached.Client.Config;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_FlushAsync : MemcachedClientTests_EnabledOperations_Base
{
    public MemcachedClientTests_EnabledOperations_FlushAsync() : base(EnabledOperations.FlushAsync)
    {
    }

    [TestMethod]
    [Ignore]
    public override async Task Flush_CheckOperationIgnored()
    {
        var flushResult = await Client.FlushAsync(CancellationToken.None);

        flushResult.Success.Should().BeTrue();
        flushResult.OperationIgnored.Should().BeFalse();
    }
}