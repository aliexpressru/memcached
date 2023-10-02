using System.Collections.Generic;
using Aer.ConsistentHash.Abstractions;

namespace Aer.ConsistentHash;

/// <summary>
/// Represents a hash ring node with optional collection of its replica nodes.
/// </summary>
/// <typeparam name="TNode">The type of the node in the ring.</typeparam>
public class ReplicatedNode<TNode>
	where TNode : class, INode
{
	/// <summary>
	/// The primary node.
	/// </summary>
	public TNode PrimaryNode { get; }

	/// <summary>
	/// Zero or more replica nodes for this node.
	/// </summary>
	public List<TNode> ReplicaNodes { get; }

	/// <summary>
	/// <c>true</c> if this node has any replicas, <c>false</c> otherwise.
	/// </summary>
	public bool HasReplicas => ReplicaNodes.Count > 0;

	/// <summary>
	/// Returns total node count = replica nodes count + 1 for primary node. 
	/// </summary>
	public int NodeCount => ReplicaNodes.Count + 1;

	/// <summary>
	/// Initializes a new instance of <see cref="ReplicatedNode{TNode}"/>.
	/// </summary>
	/// <param name="primaryNode">The primary node.</param>
	/// <param name="replicationFactor">The replication factor of this replicated node. Used to adjust an internal colelction capacity.</param>
	public ReplicatedNode(TNode primaryNode, uint replicationFactor)
	{
		PrimaryNode = primaryNode;
		ReplicaNodes = new List<TNode>((int)replicationFactor);
	}

	/// <summary>
	/// Returns actual nodes from this replicated node. First returns primary nodes, then all replicas if they are present.
	/// </summary>
	public IEnumerable<TNode> EnumerateNodes()
	{
		yield return PrimaryNode;

		if (ReplicaNodes.Count == 0)
		{ 
			yield break;
		}

		foreach (var replicaNode in ReplicaNodes)
		{
			yield return replicaNode;
		}
	}
}
