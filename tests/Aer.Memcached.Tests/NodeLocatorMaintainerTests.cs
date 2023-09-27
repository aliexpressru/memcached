using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace Aer.Memcached.Tests;

[TestClass]
public class NodeLocatorMaintainerTests
{
    private const int PeriodToRunInMilliseconds = 300;

    private readonly INodeProvider<Node> _nodeProviderMock 
        = Substitute.For<INodeProvider<Node>>();
    
    private readonly ICommandExecutor<Node> _commandExecutorMock 
        = Substitute.For<ICommandExecutor<Node>>();
    
    private readonly INodeHealthChecker<Node> _healthCheckerMock 
        = Substitute.For<INodeHealthChecker<Node>>();

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
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(false, nodesToProvide);
        
        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<Node>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(0);
        
        await maintainer.StopAsync(CancellationToken.None);
    }
    
    [TestMethod]
    public async Task Configured_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(true, nodesToProvide);

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<Node>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await maintainer.StopAsync(CancellationToken.None);
    }
    
    [TestMethod]
    public async Task Configured_AddMoreNodes_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(true, nodesToProvide);

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<Node>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        // runs once
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        nodesToProvide.Add(new Node());
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds));
        
        nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await maintainer.StopAsync(CancellationToken.None);
    }
    
    [TestMethod]
    public async Task Configured_AddAndRemoveNodes_AllProvidedNodesInLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(true, nodesToProvide);
        
        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Any<Node>()).Returns(false);

        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        // runs once
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        nodesToProvide.RemoveRange(0, 3);
        nodesToProvide.Add(new Node());
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds));
        
        nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await maintainer.StopAsync(CancellationToken.None);
    }
    
    [TestMethod]
    public async Task Configured_DeadNode_RemovedDeadNodeFromLocator()
    {
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(true, nodesToProvide);

        var deadNode = nodesToProvide.First();

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Is(deadNode)).Returns(true);
        
        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        // runs once
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds * 2));
        
        nodes = nodeLocator.GetAllNodes();

        var nodesExceptDead = nodesToProvide.Except(new[] { deadNode });
        nodes.Length.Should().Be(nodesExceptDead.Count());
        foreach (var node in nodes)
        {
            nodesExceptDead.Should().Contain(node);
        }

        await maintainer.StopAsync(CancellationToken.None);
    }
    
    [TestMethod]
    public async Task Configured_ResurrectedNode_RemovedDeadAndAddAgainNode()
    {
        var nodesToProvide = Enumerable.Range(0, 5).Select(i => new Node()).ToList();
        var nodeLocator = GetNodLocator();
        SetupMocks(true, nodesToProvide);

        var deadNodes = new List<Node>()
        {
            nodesToProvide.First()
        };

        _healthCheckerMock.CheckNodeIsDeadAsync(Arg.Is<Node>(n => deadNodes.Contains(n)))
            .Returns(true);

        var maintainer = GetMemcachedMaintainer(nodeLocator);
        await maintainer.StartAsync(CancellationToken.None);

        // runs once
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds / 3));

        var nodes = nodeLocator.GetAllNodes();

        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds * 2));
        
        nodes = nodeLocator.GetAllNodes();

        var nodesExceptDead = nodesToProvide.Except(deadNodes);
        nodes.Length.Should().Be(nodesExceptDead.Count());
        foreach (var node in nodes)
        {
            nodesExceptDead.Should().Contain(node);
        }

        deadNodes.Remove(deadNodes.First());
        await Task.Delay(TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds * 2));
        
        nodes = nodeLocator.GetAllNodes();
        
        nodes.Length.Should().Be(nodesToProvide.Count());
        foreach (var node in nodes)
        {
            nodesToProvide.Should().Contain(node);
        }
        
        await maintainer.StopAsync(CancellationToken.None);
    }

    private INodeLocator<Node> GetNodLocator()
    {
        var hashCalculator = new HashCalculator();
        
        return new HashRing<Node>(hashCalculator);
    }

    private void SetupMocks(bool isConfigured, List<Node> nodesToProvide)
    {
        _nodeProviderMock.GetNodes().Returns(nodesToProvide);
        
        _nodeProviderMock.IsConfigured().Returns(isConfigured);

        _commandExecutorMock.GetSocketPoolsStatistics(Arg.Any<Node[]>())
            .Returns(new Dictionary<Node, int>());
    }

    private MemcachedMaintainer<Node> GetMemcachedMaintainer(INodeLocator<Node> nodeLocator)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<MemcachedMaintainer<Node>>();
        
        var memcachedConfiguration = new OptionsWrapper<MemcachedConfiguration>(new MemcachedConfiguration
        {
            HeadlessServiceAddress = "memcached",
            MemcachedMaintainer = new MemcachedConfiguration.MaintainerConfiguration
            {
                NodesRebuildingPeriod = TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds),
                NodesHealthCheckPeriod = TimeSpan.FromMilliseconds(PeriodToRunInMilliseconds),
                NodeHealthCheckEnabled = true
            }
        });
        
        return new MemcachedMaintainer<Node>(
            _nodeProviderMock, 
            nodeLocator, 
            _healthCheckerMock, 
            _commandExecutorMock,
            memcachedConfiguration, 
            logger);
    }
}