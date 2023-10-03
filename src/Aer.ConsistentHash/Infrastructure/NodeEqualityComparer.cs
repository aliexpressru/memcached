using System.Collections.Generic;
using Aer.ConsistentHash.Abstractions;

namespace Aer.ConsistentHash.Infrastructure;

public class NodeEqualityComparer<TNode> : IEqualityComparer<TNode>
    where TNode : class, INode
{
    public static readonly NodeEqualityComparer<TNode> Instance = new();

    public bool Equals(TNode x, TNode y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (ReferenceEquals(x, null))
        {
            return false;
        }

        if (ReferenceEquals(y, null))
        {
            return false;
        }

        if (x.GetType() != y.GetType())
        {
            return false;
        }

        return x.GetKey() == y.GetKey();
    }

    public int GetHashCode(TNode obj)
    {
        return (obj.GetKey() != null
            ? obj.GetKey().GetHashCode()
            : 0);
    }
}