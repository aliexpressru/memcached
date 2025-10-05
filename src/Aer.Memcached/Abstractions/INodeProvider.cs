using Aer.ConsistentHash.Abstractions;

namespace Aer.Memcached.Abstractions;

/// <summary>
/// Provides a collection of nodes for the consistent hashing algorithm.
/// </summary>
/// <typeparam name="TNode">The type of the node.</typeparam>
internal interface INodeProvider<TNode> where TNode : class, INode
{
    /// <summary>
    /// Indicates whether the node provider is properly configured and ready to provide nodes.
    /// </summary>
    bool IsConfigured();
    
    /// <summary>
    /// Gets the collection of nodes.
    /// </summary>
    ICollection<TNode> GetNodes();
}