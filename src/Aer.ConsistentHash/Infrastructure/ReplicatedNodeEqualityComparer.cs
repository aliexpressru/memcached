using System.Collections.Generic;
using Aer.ConsistentHash.Abstractions;

namespace Aer.ConsistentHash.Infrastructure;

public class ReplicatedNodeEqualityComparer<TNode> : IEqualityComparer<ReplicatedNode<TNode>> where TNode : class, INode
{
	public static readonly ReplicatedNodeEqualityComparer<TNode> Instance = new();
	
	public bool Equals(ReplicatedNode<TNode> x, ReplicatedNode<TNode> y)
	{
		return NodeEqualityComparer<TNode>.Instance.Equals(x?.PrimaryNode, y?.PrimaryNode);
	}

	public int GetHashCode(ReplicatedNode<TNode> obj)
	{
		return NodeEqualityComparer<TNode>.Instance.GetHashCode(obj.PrimaryNode);
	}
}
