using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Infrastructure;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

internal class MemcachedMaintainer<TNode> : IHostedService, IDisposable where TNode : class, INode
{
    private const int NUMBER_OF_SOCKETS_TO_DESTROY_PER_POOL_PER_MAINTENANCE_CYCLE = 1;
    
    private readonly INodeProvider<TNode> _nodeProvider;
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly INodeHealthChecker<TNode> _nodeHealthChecker;
    private readonly ICommandExecutor<TNode> _commandExecutor;
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<MemcachedMaintainer<TNode>> _logger;

    private readonly ReaderWriterLockSlim _locker;
    private readonly ConcurrentBag<TNode> _deadNodes;

    private Timer _nodeRebuildingTimer;
    private Timer _nodeHealthCheckTimer;

    public MemcachedMaintainer(
        INodeProvider<TNode> nodeProvider,
        INodeLocator<TNode> nodeLocator,
        INodeHealthChecker<TNode> nodeHealthChecker,
        ICommandExecutor<TNode> commandExecutor,
        IOptions<MemcachedConfiguration> config,
        ILogger<MemcachedMaintainer<TNode>> logger)
    {
        _nodeProvider = nodeProvider;
        _nodeLocator = nodeLocator;
        _nodeHealthChecker = nodeHealthChecker;
        _commandExecutor = commandExecutor;
        _config = config.Value;
        _logger = logger;

        _locker = new ReaderWriterLockSlim();
        _deadNodes = new ConcurrentBag<TNode>();
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(MemcachedMaintainer<TNode>)} is running");

        if (!_nodeProvider.IsConfigured())
        {
            _logger.LogWarning("Memcached is not configured. No maintenance will be performed");

            return Task.CompletedTask;
        }

        _nodeRebuildingTimer = new Timer(
            RebuildNodes,
            null,
            TimeSpan.Zero,
            _config.MemcachedMaintainer.NodesRebuildingPeriod!.Value);
        
        _logger.LogInformation($"{nameof(RebuildNodes)} task is running");

        if (_config.MemcachedMaintainer.NodeHealthCheckEnabled)
        {
            _nodeHealthCheckTimer = new Timer(
                    CheckNodesHealth,
                    null,
                    TimeSpan.Zero,
                    _config.MemcachedMaintainer.NodesHealthCheckPeriod!.Value);

            _logger.LogInformation($"{nameof(CheckNodesHealth)} task is running");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs node health check and locator node rebuild tasks once, circumventing internal timers.
    /// </summary>
    /// <remarks>Primarily used in unit tests to remove time dependency.</remarks>
    internal void RunOnce()
    {
        if (!_nodeProvider.IsConfigured())
        {
            _logger.LogWarning("Memcached is not configured. No maintenance will be performed");

            return;
        }

        if (_config.MemcachedMaintainer.NodeHealthCheckEnabled)
        {
            CheckNodesHealth(null);
        }
        
        RebuildNodes(null);
    }

    /// <summary>
    /// Rebuilds the nodes in node locator using freshly discovered and statically configured nodes. 
    /// </summary>
    /// <param name="timerState">Timer state. Not used.</param>
    private void RebuildNodes(object timerState)
    {
        try
        {
            var currentNodes = _nodeProvider.GetNodes();
            var nodesInLocator = _nodeLocator.GetAllNodes();
            var deadNodesInLocator = _nodeLocator.DrainDeadNodes();

            try
            {
                _locker.EnterReadLock();
                
                // remove globally discovered dead nodes as well as nodes considered dead in locator
                currentNodes = currentNodes
                    .Except(_deadNodes.Concat(deadNodesInLocator), NodeEqualityComparer<TNode>.Instance)
                    .ToArray();
            }
            finally
            {
                _locker.ExitReadLock();
            }

            // add currently discovered nodes except the ones that are already added
            var nodesToAdd = currentNodes
                .Except(nodesInLocator, NodeEqualityComparer<TNode>.Instance)
                .ToArray();

            // remove nodes that are added to locator but not in the currently discovered node collection
            var nodesToRemove = nodesInLocator
                .Except(currentNodes, NodeEqualityComparer<TNode>.Instance)
                .ToArray();

            if (nodesToRemove.Length > 0)
            {
                _nodeLocator.RemoveNodes(nodesToRemove);
                _logger.LogInformation(
                    "Removed nodes: [{RemovedNodes}]",
                    nodesToRemove.Select(n => n.GetKey()));
            }

            if (nodesToAdd.Length > 0)
            {
                _nodeLocator.AddNodes(nodesToAdd);
                _logger.LogInformation(
                    "Added nodes: [{AddedNodes}]",
                    nodesToAdd.Select(n => n.GetKey()));
            }

            if (!_config.Diagnostics.DisableRebuildNodesStateLogging)
            {
                _logger.LogInformation(
                    "Going to destroy {NumberOfDestroyedSockets} pooled sockets in each socket pool during maintenance",
                    NUMBER_OF_SOCKETS_TO_DESTROY_PER_POOL_PER_MAINTENANCE_CYCLE);
            }

            // This is done to refresh sockets in the pool. We can tune this strategy if needed.
            int numberOfDestroyedSocketsInAllSocketPools =
                _commandExecutor.DestroyAvailablePooledSocketsInAllSocketPools(
                    NUMBER_OF_SOCKETS_TO_DESTROY_PER_POOL_PER_MAINTENANCE_CYCLE);
            
            nodesInLocator = _nodeLocator.GetAllNodes();

            if (!_config.Diagnostics.DisableRebuildNodesStateLogging)
            {
                if (numberOfDestroyedSocketsInAllSocketPools > 0)
                {
                    _logger.LogInformation(
                        "Destroyed {NumberOfDestroyedSockets} sockets in all socket pools during maintenance",
                        numberOfDestroyedSocketsInAllSocketPools);
                }

                _logger.LogInformation(
                    "Nodes in locator: [{NodesInLocator}]",
                    nodesInLocator.Select(n => n.GetKey()));

                var socketPoolStats = _commandExecutor.GetSocketPoolsStatistics(nodesInLocator);

                _logger.LogInformation(
                    "Socket pool statistics: [{SocketStatisctics}]",
                    socketPoolStats.Select(s => s.ToString())
                );
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while rebuilding nodes");
        }
    }

    /// <summary>
    /// Checks the freshly discovered and statically configured nodes health. 
    /// </summary>
    /// <param name="timerState">Timer state. Not used.</param>
    private void CheckNodesHealth(object timerState)
    {
        try
        {
            var currentNodes = _nodeProvider.GetNodes();

            // recheck nodes considered dead
            
            try
            {
                _locker.EnterWriteLock();

                if (!_deadNodes.IsEmpty)
                {
                    // check dead nodes and allocate checker action block only if dead nodes detected 
                    var recheckDeadNodesActionBlock = new ActionBlock<TNode>(
                        async node =>
                        {
                            if (!currentNodes.Contains(node, NodeEqualityComparer<TNode>.Instance))
                            {
                                return;
                            }

                            if (await _nodeHealthChecker.CheckNodeIsDeadAsync(node))
                            {
                                _deadNodes.Add(node);
                            }
                        },
                        new ExecutionDataflowBlockOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount
                        });

                    // Takes node from bag and returns it back if it is still dead
                    while (_deadNodes.TryTake(out var node))
                    {
                        recheckDeadNodesActionBlock.Post(node);
                    }

                    recheckDeadNodesActionBlock.Complete();
                    recheckDeadNodesActionBlock.Completion.GetAwaiter().GetResult();
                }
            }
            finally
            {
                _locker.ExitWriteLock();
            }

            // check nodes in locator
            
            var nodesInLocator = _nodeLocator.GetAllNodes();

            try
            {
                _locker.EnterReadLock();
                
                nodesInLocator = nodesInLocator
                    .Except(_deadNodes, NodeEqualityComparer<TNode>.Instance)
                    .ToArray();
            }
            finally
            {
                _locker.ExitReadLock();
            }

            var parallelDeadNodesCheckTask = Parallel.ForEachAsync(
                nodesInLocator,
                new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount},
                async (node, _) =>
                {
                    // pass no cancellation token since the caller method is synchronous 
                    if (await _nodeHealthChecker.CheckNodeIsDeadAsync(node))
                    {
                        _deadNodes.Add(node);
                    }
                });
            
            parallelDeadNodesCheckTask.GetAwaiter().GetResult();

            if (!_deadNodes.IsEmpty)
            {
                _logger.LogWarning("Dead nodes: [{DeadNodes}]", _deadNodes.Select(n => n.GetKey()));
                
                _nodeLocator.RemoveNodes(_deadNodes);                
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while checking nodes health");
        }
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(MemcachedMaintainer<TNode>)} is stopping");

        _nodeRebuildingTimer?.Change(Timeout.Infinite, 0);
        _nodeHealthCheckTimer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _nodeRebuildingTimer?.Dispose();
        _nodeHealthCheckTimer?.Dispose();
    }
}