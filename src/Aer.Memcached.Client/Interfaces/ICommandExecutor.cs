using Aer.ConsistentHash;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICommandExecutor<TNode> where TNode : class, INode
{
    /// <summary>
    /// Executes command on a node
    /// </summary>
    /// <param name="node">Node to execute a command on</param>
    /// <param name="command">Command to execute</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Command execution result</returns>
    Task<CommandExecutionResult> ExecuteCommandAsync(
        TNode node, 
        MemcachedCommandBase command,
        CancellationToken token);

    /// <summary>
    /// Removes and disposes socket pools for specified nodes
    /// </summary>
    /// <param name="nodes">Nodes to deletes socket pools for</param>
    void RemoveSocketPoolForNodes(IEnumerable<TNode> nodes);

    /// <summary>
    /// Gets map of node and number of created sockets on them
    /// </summary>
    /// <param name="nodes">Nodes to get socket pool statistics</param>
    /// <returns>Node to number of created sockets mapping</returns>
    IDictionary<TNode, int> GetSocketPoolsStatistics(TNode[] nodes);

    /// <summary>
    /// Destroys available sockets from socket pool
    /// Allows to release some connections if workload is changed
    /// </summary>
    /// <param name="numberOfSocketsToDestroy">Number of sockets to destroy</param>
    /// <param name="token">Cancellation token</param>
    Task DestroyAvailableSockets(int numberOfSocketsToDestroy, CancellationToken token);
}