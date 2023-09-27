using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached;

public interface INodeProvider<TNode> where TNode : class, INode
{
    bool IsConfigured();
    
    ICollection<TNode> GetNodes();
}