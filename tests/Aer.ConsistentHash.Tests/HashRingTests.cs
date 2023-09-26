using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.ConsistentHash.Tests;

[TestClass]
public class HashRingTests
{
    [TestMethod]
    public void GetNode_NoNodesAdded_ReturnsNull()
    {
        var hashRing = GetHashRing();

        var node = hashRing.GetNode("test");

        node.Should().BeNull();
    }

    [TestMethod]
    public void GetNode_OneNodeAdded_ReturnsNode()
    {
        var hashRing = GetHashRing();

        var nodeToAdd = new Node();
        hashRing.AddNode(nodeToAdd);
        var node = hashRing.GetNode("test");

        node.Should().BeEquivalentTo(nodeToAdd);
    }
    
    [TestMethod]
    public void GetNode_MultipleNodeAdded_ReturnsNode()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAdd);
        var node = hashRing.GetNode("test");

        nodesToAdd.Should().Contain(node);
    }
    
    [TestMethod]
    public void GetNodes_MultipleNodeAdded_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAdd);
        var nodes = hashRing.GetNodes(new [] {"test", "test2"});

        foreach (var node in nodes)
        {
            nodesToAdd.Should().Contain(node.Key);
        }
    }
    
    [TestMethod]
    public void GetNodes_MultipleNodeAddedInParallel_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToAddInPrevious = Enumerable.Range(0, 15).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAddInPrevious);
        
        var nodesToAdd = Enumerable.Range(0, 15).Select(i => new Node()).ToArray();
        var taskToAdd = Task.Run(() => Parallel.ForEach(nodesToAdd, nodeToAdd =>
        {
            hashRing.AddNode(nodeToAdd);
        }));

        IDictionary<Node, ConcurrentBag<string>> nodes = null;
        var keysToGet = new[] { "test", "test2" };
        var taskToGet = Task.Run(() =>
        {
            nodes = hashRing.GetNodes(new[] { "test", "test2" });
        });
        
        Task.WhenAll(taskToAdd, taskToGet).GetAwaiter().GetResult();

        nodes.Count.Should().Be(keysToGet.Length);
    }
    
    [TestMethod]
    public void GetNodes_MultipleNodeAddedAndRemovedInParallel_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToAddInPrevious = Enumerable.Range(0, 15).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAddInPrevious);
        
        var nodesToAdd = Enumerable.Range(0, 15).Select(i => new Node()).ToArray();
        var taskToAdd = Task.Run(() => Parallel.ForEach(nodesToAdd, nodeToAdd =>
        {
            hashRing.AddNode(nodeToAdd);
        }));

        var taskToRemove = Task.Run(() => Parallel.ForEach(nodesToAddInPrevious, nodeToRemove =>
        {
            hashRing.RemoveNode(nodeToRemove);
        }));

        IDictionary<Node, ConcurrentBag<string>> nodes = null;
        var keysToGet = new[] { "test", "test2" };
        var taskToGet = Task.Run(() =>
        {
            nodes = hashRing.GetNodes(new[] { "test", "test2" });
        });
        
        Task.WhenAll(taskToAdd, taskToGet, taskToRemove).GetAwaiter().GetResult();

        nodes.Count.Should().Be(keysToGet.Length);
    }

    [TestMethod]
    public void RemoveNode_NodeAddedAndRemoved_ReturnsNull()
    {
        var hashRing = GetHashRing();

        var nodeToAdd = new Node();
        hashRing.AddNode(nodeToAdd);
        var node = hashRing.GetNode("test");

        node.Should().BeEquivalentTo(nodeToAdd);
        
        hashRing.RemoveNode(nodeToAdd);
        node = hashRing.GetNode("test");
        node.Should().BeNull();
    }
    
    [TestMethod]
    public void RemoveNode_NodesAddedAndRemoved_ReturnsEmptyDictionary()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAdd);
        var nodes = hashRing.GetNodes(new [] {"test", "test2"});

        foreach (var node in nodes)
        {
            nodesToAdd.Should().Contain(node.Key);
        }
        
        hashRing.RemoveNodes(nodesToAdd);
        nodes = hashRing.GetNodes(new [] {"test", "test2"});
        nodes.Keys.Count.Should().Be(0);
    }

    [TestMethod]
    public void GetAllNodes_MultipleNodesAdded_ReturnsSameNodesWithoutVirtual()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(i => new Node()).ToArray();
        hashRing.AddNodes(nodesToAdd);

        var nodes = hashRing.GetAllNodes();

        nodes.Length.Should().Be(nodesToAdd.Length);
        foreach (var node in nodes)
        {
            nodesToAdd.Should().Contain(node);
        }
    }

    [DataTestMethod]
    [DataRow(5, 1U, 2)]
    [DataRow(5, 2U, 3)]
    [DataRow(5, 3U, 4)]
    [DataRow(5, 4U, 5)]
    [DataRow(5, 5U, 5)]
    [DataRow(5, 10U, 5)]
    [DataRow(5, 0U, 1)]
    public void GetNodes_WithReplication(int nodesNumber, uint replicationFactor, int totalNodes)
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, nodesNumber).Select(i => new Node()).ToArray();

        hashRing.AddNodes(nodesToAdd);

        var nodes = 
            hashRing.GetNodes(new[] {"test"}, replicationFactor: replicationFactor);

        nodes.Count.Should().Be(totalNodes);
        foreach (var node in nodes)
        {
            nodesToAdd.Should().Contain(node.Key);
        }
    }

    private HashRing<Node> GetHashRing()
    {
        var hashCalculator = new HashCalculator();
        
        return new HashRing<Node>(hashCalculator);
    }
}