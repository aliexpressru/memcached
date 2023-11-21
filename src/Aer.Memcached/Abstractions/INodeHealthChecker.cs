using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached.Abstractions;

public interface INodeHealthChecker<in TNode> where TNode: class, INode
{
    Task<bool> CheckNodeIsDeadAsync(TNode node);
}