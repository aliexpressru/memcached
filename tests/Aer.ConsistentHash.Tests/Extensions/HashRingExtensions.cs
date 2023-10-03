using System.Collections.Concurrent;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Tests.Model;

namespace Aer.ConsistentHash.Tests.Extensions;

public static class HashRingExtensions
{
	public static Dictionary<TestHashRingNode, ConcurrentBag<string>> GetNodesWithoutReplicas(
		this INodeLocator<TestHashRingNode> hashRing,
		string[] keys)
	{
		Dictionary<TestHashRingNode, ConcurrentBag<string>> primaryNodes = new();

		var allNodesNoReplicas = hashRing.GetNodes(keys, replicationFactor: 0);

		foreach (var nodeWithNoReplicas in allNodesNoReplicas)
		{
			foreach (var node in nodeWithNoReplicas.Key.EnumerateNodes())
			{
				primaryNodes.Add(node, nodeWithNoReplicas.Value);
			}
		}

		return primaryNodes;
	}
}
