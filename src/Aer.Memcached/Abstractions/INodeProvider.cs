using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached.Abstractions;

public interface INodeProvider<TNode> where TNode : class, INode
{
    bool IsConfigured();
    
    ICollection<TNode> GetNodes();
}