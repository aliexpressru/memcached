using Aer.Memcached.Client.Config;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.Memcached.Tests.TestClasses.EnabledOperationsTests;

[TestClass]
public class MemcachedClientTests_EnabledOperations_Delete : MemcachedClientTests_EnabledOperations_Base
{
    public MemcachedClientTests_EnabledOperations_Delete() : base(EnabledOperations.Delete)
    {
    }

    [TestMethod]
    public override async Task Delete_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();

        var deleteResult = await Client.DeleteAsync(key, CancellationToken.None);

        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeFalse();
    }

    [TestMethod]
    public override async Task MultiDelete_CheckOperationIgnored()
    {
        var key = Guid.NewGuid().ToString();

        var deleteResult = await Client.MultiDeleteAsync([key], CancellationToken.None);

        deleteResult.Success.Should().BeTrue();
        deleteResult.OperationDisabled.Should().BeFalse();
    }
}