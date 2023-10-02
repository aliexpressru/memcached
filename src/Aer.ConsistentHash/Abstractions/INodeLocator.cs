using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aer.ConsistentHash.Abstractions;

public interface INodeLocator<TNode>
    where TNode : class, INode
{
    TNode GetNode(string key);

    IDictionary<ReplicatedNode<TNode>, ConcurrentBag<string>> GetNodes(
        IEnumerable<string> keys,
        uint replicationFactor);

    TNode[] GetAllNodes();

    void AddNode(TNode node);

    void AddNodes(IEnumerable<TNode> nodes);

    void AddNodes(params TNode[] nodes);

    void RemoveNode(TNode node);

    void RemoveNodes(IEnumerable<TNode> nodes);

    /// <summary>
    /// Removes node from the internal structures while maintaining reference to it for
    /// the parallel mainetanance processes to be aware of it being marked as dead
    /// and not attempt to restore the node to the locator. 
    /// </summary>
    /// <param name="node">The node to mark as dead.</param>
    void MarkNodeDead(TNode node);

    /// <summary>
    /// Gets the nodes that were considered dead.
    /// </summary>
    /// <returns>The dead nodes collection or empty collection if no nodes were considered dead.</returns>
    IReadOnlyCollection<TNode> GetDeadNodes();
}