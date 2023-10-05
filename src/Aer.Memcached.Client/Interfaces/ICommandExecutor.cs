using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.ConnectionPool;
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
    /// Executes command on a replicated node. Executes commands on all replicas in parallel.
    /// If any of the primary and replicas command succeeds - returns successful result from that node
    /// </summary>
    /// <param name="node">A replicated node to execute a command on</param>
    /// <param name="command">Command to execute</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Command execution result</returns>
    Task<CommandExecutionResult> ExecuteCommandAsync(
        ReplicatedNode<TNode> node,
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
    /// Destroys available sockets from socket pool.
    /// Allows to release some connections if workload is changed
    /// </summary>
    /// <param name="numberOfSocketsToDestroy">Number of sockets to destroy</param>
    /// <param name="token">Cancellation token</param>
    Task DestroyAvailablePooledSockets(int numberOfSocketsToDestroy, CancellationToken token);

    /// <summary>
    /// Gets the <see cref="PooledSocket"/> instance for the specified node.
    /// The obtained socket is produced from socket pool. 
    /// </summary>
    /// <param name="node">The node to get pooled socket for.</param>
    /// <param name="isAuthenticateSocketIfRequired">
    /// If set to <c>true</c> performs authentication on obtained socket if
    /// it is required by authentication provider settings.
    /// </param>
    /// <param name="token">The cancellation token.</param>
    Task<PooledSocket> GetSocketForNodeAsync(
        TNode node,
        bool isAuthenticateSocketIfRequired,
        CancellationToken token);
}