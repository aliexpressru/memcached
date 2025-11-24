using System.Collections.Concurrent;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Tests.Extensions;
using Aer.ConsistentHash.Tests.Model;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aer.ConsistentHash.Tests.TestClasses;

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

        var nodeToAdd = new TestHashRingNode();
        hashRing.AddNode(nodeToAdd);
        var node = hashRing.GetNode("test");

        node.Should().BeEquivalentTo(nodeToAdd);
    }

    [TestMethod]
    public void GetNode_MultipleNodeAdded_ReturnsNode()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(_ => new TestHashRingNode()).ToArray();
        hashRing.AddNodes(nodesToAdd);
        var node = hashRing.GetNode("test");

        nodesToAdd.Should().Contain(node);
    }

    [TestMethod]
    public void GetNodes_MultipleNodeAdded_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(_ => new TestHashRingNode()).ToArray();

        hashRing.AddNodes(nodesToAdd);
        var nodes = hashRing.GetNodes(new[] {"test", "test2"}, replicationFactor: 0);

        foreach (var node in nodes.SelectMany(n => n.Key.EnumerateNodes()))
        {
            nodesToAdd.Should().Contain(node);
        }
    }

    [TestMethod]
    public async Task GetNodes_MultipleNodeAddedInParallel_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToInitiallyAdd = Enumerable.Range(0, 15)
            .Select(_ => new TestHashRingNode())
            .ToArray();

        hashRing.AddNodes(nodesToInitiallyAdd);

        var nodesToAdd = Enumerable.Range(0, 15)
            .Select(_ => new TestHashRingNode())
            .ToArray();

        var taskToAddNodes = Task.Run(
            () =>
                Parallel.ForEach(
                    nodesToAdd,
                    nodeToAdd => { hashRing.AddNode(nodeToAdd); }
                )
        );

        Dictionary<TestHashRingNode, ConcurrentBag<string>> nodes = new();

        var keysToGet = new[] {"test", "test2"};

        var taskToGetNodes = Task.Run(
            () => { nodes = hashRing.GetNodesWithoutReplicas(keysToGet); }
        );

        await Task.WhenAll(taskToAddNodes, taskToGetNodes);

        // Test verifies thread-safety: despite race conditions, result should always be valid
        // With 15+ nodes always in the ring, we should get at least 1 node for our keys
        nodes.Count.Should().BeGreaterOrEqualTo(1, "hash ring always has nodes available");
        nodes.Count.Should().BeLessOrEqualTo(keysToGet.Length, "cannot return more nodes than requested keys");
        
        // All returned nodes must be valid (from either initial or newly added nodes)
        var allValidNodes = nodesToInitiallyAdd.Concat(nodesToAdd).ToArray();
        foreach (var (node, keys) in nodes)
        {
            allValidNodes.Should().Contain(node, "returned node must be from the hash ring");
            keys.Should().NotBeEmpty("each node should have at least one key assigned");
            
            // Each key assigned to this node must be from our request
            foreach (var key in keys)
            {
                keysToGet.Should().Contain(key, "assigned key must be from the requested keys");
            }
        }
    }

    [TestMethod]
    public async Task GetNodes_MultipleNodeAddedAndRemovedInParallel_ReturnsNodes()
    {
        var hashRing = GetHashRing();

        var nodesToInitiallyAdd = Enumerable.Range(0, 15)
            .Select(_ => new TestHashRingNode())
            .ToArray();

        hashRing.AddNodes(nodesToInitiallyAdd);

        var nodesToAdd = Enumerable.Range(0, 15)
            .Select(_ => new TestHashRingNode())
            .ToArray();

        var taskToAddNodes = Task.Run(
            () =>
                Parallel.ForEach(
                    nodesToAdd,
                    nodeToAdd => { hashRing.AddNode(nodeToAdd); }
                )
        );

        var taskToRemoveNodes = Task.Run(
            () =>
                Parallel.ForEach(
                    nodesToInitiallyAdd,
                    nodeToRemove => { hashRing.RemoveNode(nodeToRemove); }
                )
        );

        Dictionary<TestHashRingNode, ConcurrentBag<string>> nodes = new();

        var keysToGet = new[] {"test", "test2"};

        var taskToGetNodes = Task.Run(
            () => { nodes = hashRing.GetNodesWithoutReplicas(keysToGet); }
        );

        await Task.WhenAll(taskToAddNodes, taskToRemoveNodes, taskToGetNodes);

        // Test verifies thread-safety with concurrent Add/Remove/Get operations
        // Result count can vary (0 to 2) based on timing:
        // - 0: GetNodes called when all initial nodes removed but new not yet added
        // - 1-2: GetNodes called when some nodes available
        nodes.Count.Should().BeInRange(0, keysToGet.Length, "result count depends on race condition timing");
        
        if (nodes.Count > 0)
        {
            // All returned nodes must be valid (either initial or newly added)
            var allPossibleNodes = nodesToInitiallyAdd.Concat(nodesToAdd).ToArray();
            foreach (var (node, keys) in nodes)
            {
                allPossibleNodes.Should().Contain(node, "returned node must be from valid set");
                keys.Should().NotBeEmpty("each node should have at least one key assigned");
                
                // Each key assigned to this node must be from our request
                foreach (var key in keys)
                {
                    keysToGet.Should().Contain(key, "assigned key must be from the requested keys");
                }
            }
        }
    }

    [TestMethod]
    public void RemoveNode_NodeAddedAndRemoved_ReturnsNull()
    {
        var hashRing = GetHashRing();

        var nodeToAdd = new TestHashRingNode();
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

        var nodesToAdd = Enumerable.Range(0, 5).Select(_ => new TestHashRingNode()).ToArray();

        hashRing.AddNodes(nodesToAdd);

        var keysToGet = new[] {"test", "test2"};

        var nodes = hashRing.GetNodesWithoutReplicas(keysToGet);

        foreach (var node in nodes)
        {
            nodesToAdd.Should().Contain(node.Key);
        }

        hashRing.RemoveNodes(nodesToAdd);

        nodes = hashRing.GetNodesWithoutReplicas(keysToGet);
        nodes.Keys.Count.Should().Be(0);
    }

    [TestMethod]
    public void GetAllNodes_MultipleNodesAdded_ReturnsSameNodesWithoutVirtual()
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, 5).Select(_ => new TestHashRingNode()).ToArray();
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
    [DataRow(5, 5U, 5)]
    [DataRow(5, 10U, 5)]
    [DataRow(5, 0U, 1)]
    public void GetNodes_WithReplication(int nodesNumber, uint replicationFactor, int totalExpectedNodesCount)
    {
        var hashRing = GetHashRing();

        var nodesToAdd = Enumerable.Range(0, nodesNumber).Select(_ => new TestHashRingNode()).ToArray();

        hashRing.AddNodes(nodesToAdd);

        var replicatedNodes =
            hashRing.GetNodes(new[] {"test"}, replicationFactor: replicationFactor);

        var totalNodesCount = replicatedNodes
            .Sum(kv => kv.Key.ReplicaNodes.Count + 1); // +1 to account for primary node

        totalNodesCount.Should().Be(totalExpectedNodesCount);

        foreach (var replicatedNode in replicatedNodes)
        {
            nodesToAdd.Should().Contain(replicatedNode.Key.PrimaryNode);
            if (replicationFactor > 0)
            {
                if (replicationFactor < totalNodesCount)
                {
                    replicatedNode.Key.ReplicaNodes.Count.Should().Be((int) replicationFactor);
                }

                nodesToAdd.Should().Contain(replicatedNode.Key.ReplicaNodes);
            }
        }
    }

    private INodeLocator<TestHashRingNode> GetHashRing()
    {
        var hashCalculator = new HashCalculator();

        return new HashRing<TestHashRingNode>(hashCalculator);
    }
}