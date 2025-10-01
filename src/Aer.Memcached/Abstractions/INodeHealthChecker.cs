using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached.Abstractions;

internal interface INodeHealthChecker<in TNode> where TNode: class, INode
{
    /// <summary>
    /// Checks if the given node is dead.
    /// </summary>
    /// <param name="node">The node to check.</param>
    Task<bool> CheckNodeIsDeadAsync(TNode node);
}