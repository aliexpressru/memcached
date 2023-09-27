using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aer.ConsistentHash.Abstractions;

public interface INodeLocator<TNode>
    where TNode : class, INode
{
    TNode GetNode(string key);

    IDictionary<TNode, ConcurrentBag<string>> GetNodes(IEnumerable<string> keys, uint replicationFactor = 0);

    IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> GetReplicatedNodes(
        IEnumerable<string> keys,
        uint replicationFactor);

    TNode[] GetAllNodes();

    void AddNode(TNode node);

    void AddNodes(IEnumerable<TNode> nodes);

    void RemoveNode(TNode node);

    void RemoveNodes(IEnumerable<TNode> nodes);
}