using Aer.ConsistentHash;

namespace Aer.Memcached;

public interface INodeHealthChecker<in TNode> where TNode: class, INode
{
    Task<bool> CheckNodeIsDeadAsync(TNode node);
}