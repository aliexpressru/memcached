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
    /// Gets available, current and remaining socket counts for each node's socket pool
    /// </summary>
    /// <param name="nodes">Nodes to get pool statistics for</param>
    IReadOnlyCollection<SocketPoolStatisctics> GetSocketPoolsStatistics(TNode[] nodes);

    /// <summary>
    /// Destroys specified number of pooled sockets if they are present in socket pool.
    /// Allows to release some connections if workload is changed. Used for socket pool refresh
    /// </summary>
    /// <param name="numberOfSocketsToDestroy">Number of sockets to destroy</param>
    /// <returns>Number of actually destroyed sockets.</returns>
    int DestroyAvailablePooledSocketsInAllSocketPools(int numberOfSocketsToDestroy);

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