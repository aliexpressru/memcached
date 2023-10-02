using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Infrastructure;

namespace Aer.ConsistentHash;

public class HashRing<TNode> : INodeLocator<TNode>
    where TNode : class, INode
{
    /// <summary>
    /// In <see cref="GetNodeInternal"/> there is a moment when _hashToNodeMap is already updated (node removed)
    /// but _sortedNodeHashKeys is not yet. So we need Read/Write lock after all. 
    /// </summary>
    private readonly ReaderWriterLockSlim _locker;

    private readonly IHashCalculator _hashCalculator;
    private readonly int _numberOfVirtualNodes;

    private readonly ConcurrentDictionary<ulong, TNode> _hashToNodeMap;
    private readonly ConcurrentDictionary<ulong, ulong[]> _nodeHashToVirtualNodeHashesMap;
    private ulong[] _sortedNodeHashKeys;

    /// <param name="hashCalculator">Calculates hash for nodes and keys</param>
    /// <param name="numberOfVirtualNodes">Number of virtual nodes by one physical node</param>
    public HashRing(IHashCalculator hashCalculator, int numberOfVirtualNodes = 256)
    {
        _locker = new ReaderWriterLockSlim();

        _hashCalculator = hashCalculator;
        _numberOfVirtualNodes = numberOfVirtualNodes;

        _hashToNodeMap = new ConcurrentDictionary<ulong, TNode>();
        _nodeHashToVirtualNodeHashesMap = new ConcurrentDictionary<ulong, ulong[]>();
    }

    public TNode GetNode(string key)
    {
        try
        {
            _locker.EnterReadLock();

            if (_sortedNodeHashKeys == null
                || _sortedNodeHashKeys.Length == 0)
            {
                return null;
            }

            var node = GetNodeInternal(key);
            return node;
        }
        finally
        {
            _locker.ExitReadLock();
        }
    }

    public IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> GetNodes(
        IEnumerable<string> keys,
        uint replicationFactor)
    {
        var result = new ConcurrentDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>>(
            ReplicatedNodeEqualityComparer<TNode>.Instance);

        try
        {
            _locker.EnterReadLock();

            if (_sortedNodeHashKeys == null
                || _sortedNodeHashKeys.Length == 0)
            {
                return result;
            }

            Parallel.ForEach(
                keys,
                new ParallelOptions {MaxDegreeOfParallelism = 16},
                key =>
                {
                    var replicatedNode = GetReplicatedNodeInternal(key, replicationFactor);

                    var keysForReplicatedNode = result.GetOrAdd(replicatedNode, static (_) => new());

                    keysForReplicatedNode.Add(key);
                });
        }
        finally
        {
            _locker.ExitReadLock();
        }

        return result;
    }

    public TNode[] GetAllNodes()
    {
        return _hashToNodeMap.Values.Distinct(NodeEqualityComparer<TNode>.Instance).ToArray();
    }

    public void AddNode(TNode node)
    {
        try
        {
            _locker.EnterWriteLock();

            AddNodeToCollections(node);
            UpdateSortedNodeHashKeys();
        }
        finally
        {
            _locker.ExitWriteLock();
        }
    }

    public void AddNodes(IEnumerable<TNode> nodes)
    {
        try
        {
            _locker.EnterWriteLock();

            foreach (var node in nodes)
            {
                AddNodeToCollections(node);
            }

            UpdateSortedNodeHashKeys();
        }
        finally
        {
            _locker.ExitWriteLock();
        }
    }

    public void AddNodes(params TNode[] nodes)
    {
        AddNodes((IEnumerable<TNode>) nodes);
    }

    public void RemoveNode(TNode node)
    {
        try
        {
            _locker.EnterWriteLock();

            if (TryRemoveNodeFromCollections(node))
            {
                UpdateSortedNodeHashKeys();
            }
        }
        finally
        {
            _locker.ExitWriteLock();
        }
    }

    public void RemoveNodes(IEnumerable<TNode> nodes)
    {
        try
        {
            _locker.EnterWriteLock();

            bool updateNeeded = false;
            foreach (var node in nodes)
            {
                var isRemoved = TryRemoveNodeFromCollections(node);
                if (isRemoved)
                {
                    updateNeeded = true;
                }
            }

            if (updateNeeded)
            {
                UpdateSortedNodeHashKeys();
            }
        }
        finally
        {
            _locker.ExitWriteLock();
        }
    }

    private TNode GetNodeInternal(string key)
    {
        var keyToNodeHash = GetNodeHash(key);

        return _hashToNodeMap[keyToNodeHash];
    }

    private ReplicatedNode<TNode> GetReplicatedNodeInternal(string key, uint replicationFactor)
    {
        if (replicationFactor == 0)
        {
            // just return primary node without replicas
            var singlePrimaryNode = GetNodeInternal(key);

            return new ReplicatedNode<TNode>(singlePrimaryNode, 0U);
        }

        var keyToNodeHash = GetNodeHash(key);
        var primaryNode = _hashToNodeMap[keyToNodeHash];

        var replicatedNode = new ReplicatedNode<TNode>(primaryNode, replicationFactor);

        if (replicationFactor >= _nodeHashToVirtualNodeHashesMap.Keys.Count - 1)
        {
            // means that replication factor is greater than total nodes count
            // return all other nodes as replicas

            var replicaNodes = _hashToNodeMap.Values.Except(new[] {primaryNode});

            foreach (var replicaNode in replicaNodes)
            {
                replicatedNode.ReplicaNodes.Add(replicaNode);
            }

            return replicatedNode;
        }

        var primaryNodeHash = GetNodeHash(primaryNode);

        var startingNodeFound = false;
        var totalReplicaCount = 0;

        foreach (var currentNodeHash in _nodeHashToVirtualNodeHashesMap.Keys)
        {
            if (primaryNodeHash == currentNodeHash)
            {
                // this is our primary node - start getting replica nodes from this one 
                startingNodeFound = true;
                continue;
            }

            if (!startingNodeFound)
            {
                // skip nodes on the ring until we get to the primary node
                continue;
            }

            if (totalReplicaCount >= replicationFactor)
            {
                // means we get got enough replicas
                break;
            }

            var replicaNode = _hashToNodeMap[currentNodeHash];

            replicatedNode.ReplicaNodes.Add(replicaNode);

            totalReplicaCount++;
        }

        if (totalReplicaCount < replicationFactor)
        {
            // still not enough replicas. Find more replicas at the beginning of array
            foreach (var currentNodeHash in _nodeHashToVirtualNodeHashesMap.Keys)
            {
                if (totalReplicaCount >= replicationFactor)
                {
                    break;
                }

                var currentNode = _hashToNodeMap[currentNodeHash];

                replicatedNode.ReplicaNodes.Add(currentNode);

                totalReplicaCount++;
            }
        }

        return replicatedNode;
    }

    private ulong GetNodeHash(string key)
    {
        var keyHash = GetHash(key);

        var index = Array.BinarySearch(_sortedNodeHashKeys, keyHash);
        if (index < 0) // no exact match
        {
            // If the Array does not contain the specified value, the method returns a negative integer.
            // You can apply the bitwise complement operator to the negative result to produce an index.
            // If this index is one greater than the upper bound of the array, there are no elements larger than value in the array.
            // Otherwise, it is the index of the first element that is larger than value.
            index = ~index;

            if (index >= _sortedNodeHashKeys.Length)
            {
                index = 0;
            }
        }

        return _sortedNodeHashKeys[index];
    }

    private bool TryRemoveNodeFromCollections(TNode node)
    {
        var nodeHash = GetNodeHash(node);
        _nodeHashToVirtualNodeHashesMap.TryGetValue(nodeHash, out var virtualNodeHashes);

        if (virtualNodeHashes != null)
        {
            foreach (var virtualNodeHash in virtualNodeHashes)
            {
                _hashToNodeMap.TryRemove(virtualNodeHash, out _);
            }
        }

        if (_hashToNodeMap.TryRemove(nodeHash, out _))
        {
            _nodeHashToVirtualNodeHashesMap.TryRemove(nodeHash, out _);

            return true;
        }

        return false;
    }

    private void AddNodeToCollections(TNode node)
    {
        var nodeHash = GetNodeHash(node);
        var virtualNodeHashes = GetVirtualNodesHashes(node, _numberOfVirtualNodes);

        _hashToNodeMap.GetOrAdd(nodeHash, node);
        _nodeHashToVirtualNodeHashesMap.GetOrAdd(nodeHash, virtualNodeHashes);
        foreach (var virtualNodeHash in virtualNodeHashes)
        {
            _hashToNodeMap.GetOrAdd(virtualNodeHash, node);
        }
    }

    private void UpdateSortedNodeHashKeys()
    {
        _sortedNodeHashKeys = _hashToNodeMap.Keys
            .ToArray()
            .OrderBy(x => x)
            .ToArray();
    }

    private ulong GetNodeHash(TNode node)
    {
        var nodeKey = node.GetKey();

        return GetHash(nodeKey);
    }

    private ulong[] GetVirtualNodesHashes(TNode node, int numberOfVirtualNodes)
    {
        var hashArray = new ulong[numberOfVirtualNodes];

        var nodeKey = node.GetKey();

        for (int i = 0; i < numberOfVirtualNodes; i++)
        {
            var virtualNodeKey = $"{nodeKey}_virtual{i}";
            var virtualNodeHash = GetHash(virtualNodeKey);

            hashArray[i] = virtualNodeHash;
        }

        return hashArray;
    }

    private ulong GetHash(string key)
    {
        return _hashCalculator.ComputeHash(key);
    }
}