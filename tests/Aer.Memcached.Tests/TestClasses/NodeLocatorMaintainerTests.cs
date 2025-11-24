using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Aer.Memcached.Infrastructure;
using Aer.Memcached.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace Aer.Memcached.Tests.TestClasses;

[TestClass]
public class NodeLocatorMaintainerTests
{
    /*
     * NOTE: here in many tests we don't start maintainer to not be dependent on timers
     * we invoke actions, that should have been invoked by timer, manually
     */

    private readonly INodeProvider<TestHashRingNode> _nodeProviderMock 
        = Substitute.For<INodeProvider<TestHashRingNode>>();
    
    private readonly ICommandExecutor<TestHashRingNode> _commandExecutorMock 
        = Substitute.For<ICommandExecutor<TestHashRingNode>>();
    
    private readonly INodeHealthChecker<TestHashRingNode> _healthCheckerMock 
        = Substitute.For<INodeHealthChecker<TestHashRingNode>>();

    [TestInitialize]
    public void BeforeEachTest()
    {
        _nodeProviderMock.ClearSubstitute();
        _commandExecutorMock.ClearSubstitute();
        _healthCheckerMock.ClearSubstitute();
    }
    
    [TestMethod]
    public async Task NotConfigured_NoNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        
        SetupMocks(false, nodesToProvide);
        
        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<TestHashRingNode>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        await maintainer.RunOnceAsync();
        
        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(0);
    }
    
    [TestMethod]
    public async Task Configured_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        SetupMocks(true, nodesToProvide);

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<TestHashRingNode>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        await maintainer.RunOnceAsync();

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
    }
    
    [TestMethod]
    public async Task Configured_AddMoreNodes_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        SetupMocks(true, nodesToProvide);

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<TestHashRingNode>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        // first maintainer run

        await maintainer.RunOnceAsync();
        
        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        nodesToProvide.Add(new TestHashRingNode());

        // second maintainer run

        await maintainer.RunOnceAsync();
        
        nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
    }

    [TestMethod]
    public async Task Configured_AddAndRemoveNodes_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        SetupMocks(true, nodesToProvide);
        
        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<TestHashRingNode>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        // first maintainer run
        await maintainer.RunOnceAsync();

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        nodesToProvide.RemoveRange(0, 3);
        nodesToProvide.Add(new TestHashRingNode());
        
        // second maintainer run

        await maintainer.RunOnceAsync();
        
        nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
    }

    [TestMethod]
    public async Task Configured_DeadNode_RemovedDeadNodeFromLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        
        SetupMocks(true, nodesToProvide);

        var deadNode = nodesToProvide[0];

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Is(deadNode)).Returns(true);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        // first maintainer run
        
        await maintainer.RunOnceAsync();

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);

        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }

        // second maintainer run
        await maintainer.RunOnceAsync();

        nodes = nodeLocator.GetAllNodes();

        var nodesExceptDead = nodesToProvide.Except(new[] {deadNode}).ToArray();
        
        nodes.Length.Should().Be(nodesExceptDead.Length);
        
        foreach (var node in nodes)
        {
            nodesExceptDead.Should().Contain(node);
        }
    }

    [TestMethod]
    public async Task Configured_ResurrectedNode_RemovedDeadAndAddAgainNode()
    {
        var nodesToProvide = Enumerable.Range(0, 5)
            .Select(i => new TestHashRingNode())
            .ToList();
        
        var nodeLocator = GetNodeLocator();
        
        SetupMocks(true, nodesToProvide);

        var deadNodes = new List<TestHashRingNode>()
        {
            nodesToProvide[0]
        };

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Is<TestHashRingNode>(n => deadNodes.Contains(n)))
            .Returns(true);

        var maintainer = GetMemcachedMaintainer(nodeLocator);

        // first maintainer run
        await maintainer.RunOnceAsync();

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count);
        
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }

        // second maintainer run
        await maintainer.RunOnceAsync();
        
        nodes = nodeLocator.GetAllNodes();

        var nodesExceptDead = nodesToProvide.Except(deadNodes).ToArray();
        
        nodes.Length.Should().Be(nodesExceptDead.Length);
        
        foreach (var node in nodes)
        {
            nodesExceptDead.Should().Contain(node);
        }

        deadNodes.Remove(deadNodes[0]);

        // third maintainer run
        await maintainer.RunOnceAsync();
        
        nodes = nodeLocator.GetAllNodes();
        
        nodes.Length.Should().Be(nodesToProvide.Count);
        
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
    }

    private INodeLocator<TestHashRingNode> GetNodeLocator()
    {
        var hashCalculator = new HashCalculator();
        
        return new HashRing<TestHashRingNode>(hashCalculator);
    }

    private void SetupMocks(bool isConfigured, List<TestHashRingNode> nodesToProvide)
    {
        _nodeProviderMock.GetNodes().Returns(nodesToProvide);
        
        _nodeProviderMock.IsConfigured().Returns(isConfigured);

        _commandExecutorMock.GetSocketPoolsStatistics(Arg.Any<TestHashRingNode[]>())
            .Returns(new List<SocketPoolStatisctics>());
    }

    private MemcachedMaintainer<TestHashRingNode> GetMemcachedMaintainer(INodeLocator<TestHashRingNode> nodeLocator)
    {
        const int periodToRunMaintainerMilliseconds = 300;
        
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<MemcachedMaintainer<TestHashRingNode>>();
        
        var memcachedConfiguration = new OptionsWrapper<MemcachedConfiguration>(new MemcachedConfiguration
        {
            HeadlessServiceAddress = "memcached",
            MemcachedMaintainer = new MemcachedConfiguration.MaintainerConfiguration
            {
                NodesRebuildingPeriod = TimeSpan.FromMilliseconds(periodToRunMaintainerMilliseconds),
                NodesHealthCheckPeriod = TimeSpan.FromMilliseconds(periodToRunMaintainerMilliseconds),
                NodeHealthCheckEnabled = true
            }
        });
        
        return new MemcachedMaintainer<TestHashRingNode>(
            _nodeProviderMock, 
            nodeLocator, 
            _healthCheckerMock, 
            _commandExecutorMock,
            memcachedConfiguration, 
            logger);
    }
}
