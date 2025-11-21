using System.Collections.Concurrent;
using Aer.ConsistentHash.Abstractions;
using Aer.ConsistentHash.Infrastructure;
using Aer.Memcached.Abstractions;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Infrastructure;

internal class MemcachedMaintainer<TNode> : IHostedService, IDisposable where TNode : class, INode
{
    //private const int NUMBER_OF_SOCKETS_TO_DESTROY_PER_POOL_PER_MAINTENANCE_CYCLE = 1;
    
    private readonly INodeProvider<TNode> _nodeProvider;
    private readonly INodeLocator<TNode> _nodeLocator;
    private readonly INodeHealthChecker<TNode> _nodeHealthChecker;
    private readonly ICommandExecutor<TNode> _commandExecutor;
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<MemcachedMaintainer<TNode>> _logger;

    private readonly SemaphoreSlim _locker;
    private readonly ConcurrentBag<TNode> _deadNodes;

    private int _maintainerCyclesToCloseSocketAfterLeft;
    private int _isCheckingNodesHealth; // 0 = not running, 1 = running
    private readonly int _maxDegreeOfParallelism;

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

        _locker = new SemaphoreSlim(1, 1);
        _deadNodes = new ConcurrentBag<TNode>();
        _maintainerCyclesToCloseSocketAfterLeft = _config.MemcachedMaintainer.MaintainerCyclesToCloseSocketAfter;
        _maxDegreeOfParallelism = _config.MemcachedMaintainer.MaxDegreeOfParallelism == -1
            ? Environment.ProcessorCount
            : _config.MemcachedMaintainer.MaxDegreeOfParallelism;
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
    /// Runs one cycle of maintenance synchronously for testing purposes.
    /// Waits for async operations to complete.
    /// </summary>
    internal async Task RunOnceAsync()
    {
        if (!_nodeProvider.IsConfigured())
        {
            _logger.LogWarning("Memcached is not configured. No maintenance will be performed");

            return;
        }

        if (_config.MemcachedMaintainer.NodeHealthCheckEnabled)
        {
            await CheckNodesHealthAsync();
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

            // Lock to prevent race condition with CheckNodesHealthAsync
            // which may TryTake nodes while we're reading
            _locker.Wait();
            try
            {
                // Remove globally discovered dead nodes as well as nodes considered dead in locator
                currentNodes = currentNodes
                    .Except(_deadNodes.Concat(deadNodesInLocator), NodeEqualityComparer<TNode>.Instance)
                    .ToArray();
            }
            finally
            {
                _locker.Release();
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
                    "Removed nodes: {RemovedNodes}",
                    nodesToRemove.Select(n => n.GetKey()));
            }

            if (nodesToAdd.Length > 0)
            {
                _nodeLocator.AddNodes(nodesToAdd);
                _logger.LogInformation(
                    "Added nodes: {AddedNodes}",
                    nodesToAdd.Select(n => n.GetKey()));
            }

            int numberOfDestroyedSocketsInAllSocketPools = 0;
            
            if (_maintainerCyclesToCloseSocketAfterLeft <= 0)
            {
                if (!_config.Diagnostics.DisableRebuildNodesStateLogging)
                {
                    _logger.LogDebug(
                        "Going to destroy {NumberOfDestroyedSockets} pooled sockets in each socket pool during maintenance",
                        _config.MemcachedMaintainer.NumberOfSocketsToClosePerPool);
                }

                // This is done to refresh sockets in the pool.
                numberOfDestroyedSocketsInAllSocketPools =
                    _commandExecutor.DestroyAvailablePooledSocketsInAllSocketPools(
                        _config.MemcachedMaintainer.NumberOfSocketsToClosePerPool);
                
                _maintainerCyclesToCloseSocketAfterLeft = _config.MemcachedMaintainer.MaintainerCyclesToCloseSocketAfter;
            }
            else
            {
                _maintainerCyclesToCloseSocketAfterLeft--;
            }

            nodesInLocator = _nodeLocator.GetAllNodes();

            if (!_config.Diagnostics.DisableRebuildNodesStateLogging)
            {
                if (numberOfDestroyedSocketsInAllSocketPools > 0)
                {
                    _logger.LogInformation(
                        "Destroyed {NumberOfDestroyedSockets} sockets in all socket pools during maintenance",
                        numberOfDestroyedSocketsInAllSocketPools);
                }

                if (nodesInLocator.Length == 0)
                { 
                    _logger.LogInformation("No nodes found in locator, no socket pool statistics to report. This might be due to all nodes considered dead or corresponding pools exhaustion");
                    
                }
                else
                {
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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occured while rebuilding nodes");
        }
    }

    /// <summary>
    /// Timer callback wrapper for health check. Starts async health check without blocking.
    /// </summary>
    /// <param name="timerState">Timer state. Not used.</param>
    private void CheckNodesHealth(object timerState)
    {
        // Fire-and-forget pattern for Timer callback
        // The async method handles its own error handling and completion tracking
        _ = CheckNodesHealthAsync();
    }

    /// <summary>
    /// Checks the freshly discovered and statically configured nodes health. 
    /// </summary>
    private async Task CheckNodesHealthAsync()
    {
        // Prevent overlapping executions - if already running, skip this cycle
        if (Interlocked.CompareExchange(ref _isCheckingNodesHealth, 1, 0) != 0)
        {
            _logger.LogWarning("Skipping node health check - previous check is still running");
            return;
        }
        
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var currentNodes = _nodeProvider.GetNodes();

            // Recheck nodes considered dead
            await RecheckDeadNodesAsync(currentNodes);

            // Check nodes in locator
            await CheckNodesInLocatorAsync();
            
            totalStopwatch.Stop();
            
            _logger.LogInformation(
                "Total health check completed in {ElapsedMilliseconds}ms", 
                totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            totalStopwatch.Stop();
            
            _logger.LogError(e, 
                "Error occured while checking nodes health. Total time before error: {ElapsedMilliseconds}ms",
                totalStopwatch.ElapsedMilliseconds);
        }
        finally
        {
            // Reset the flag to allow next execution
            Interlocked.Exchange(ref _isCheckingNodesHealth, 0);
        }
    }

    /// <summary>
    /// Rechecks nodes that were previously marked as dead.
    /// </summary>
    private async Task RecheckDeadNodesAsync(ICollection<TNode> currentNodes)
    {
        if (_deadNodes.IsEmpty)
        {
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Lock to atomically extract all dead nodes
        // Prevents race with RebuildNodes reading _deadNodes
        await _locker.WaitAsync();
        var nodesToRecheck = new List<TNode>();
        try
        {
            while (_deadNodes.TryTake(out var node))
            {
                nodesToRecheck.Add(node);
            }
        }
        finally
        {
            _locker.Release();
        }
        
        var deadNodesCount = nodesToRecheck.Count;
        
        if (deadNodesCount == 0)
        {
            return;
        }

        // Execute with controlled parallelism (no lock needed - ConcurrentBag.Add is thread-safe)
        await Parallel.ForEachAsync(
            nodesToRecheck,
            new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism },
            async (node, _) =>
            {
                if (!currentNodes.Contains(node, NodeEqualityComparer<TNode>.Instance))
                {
                    return;
                }

                if (await _nodeHealthChecker.CheckNodeIsDeadAsync(node))
                {
                    _deadNodes.Add(node); // ConcurrentBag.Add is thread-safe
                }
            });
        
        stopwatch.Stop();
        
        _logger.LogInformation(
            "Dead nodes recheck completed in {ElapsedMilliseconds}ms for {NodeCount} nodes", 
            stopwatch.ElapsedMilliseconds, 
            deadNodesCount);
    }

    /// <summary>
    /// Checks health of all nodes currently in locator.
    /// </summary>
    private async Task CheckNodesInLocatorAsync()
    {
        var nodesInLocator = _nodeLocator.GetAllNodes();

        // Lock to get consistent snapshot of _deadNodes
        // Prevents race with RebuildNodes and RecheckDeadNodesAsync
        await _locker.WaitAsync();
        try
        {
            nodesInLocator = nodesInLocator
                .Except(_deadNodes, NodeEqualityComparer<TNode>.Instance)
                .ToArray();
        }
        finally
        {
            _locker.Release();
        }
        
        if (nodesInLocator.Length == 0)
        {
            return;
        }
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Execute with controlled parallelism
        await Parallel.ForEachAsync(
            nodesInLocator,
            new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism },
            async (node, _) =>
            {
                if (await _nodeHealthChecker.CheckNodeIsDeadAsync(node))
                {
                    _deadNodes.Add(node);
                }
            });
        
        stopwatch.Stop();
        
        _logger.LogInformation(
            "Nodes in locator health check completed in {ElapsedMilliseconds}ms for {NodeCount} nodes", 
            stopwatch.ElapsedMilliseconds, 
            nodesInLocator.Length);

        if (!_deadNodes.IsEmpty)
        {
            _logger.LogInformation("Dead nodes: [{DeadNodes}]", _deadNodes.Select(n => n.GetKey()));
            
            _nodeLocator.RemoveNodes(_deadNodes);                
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
        _locker?.Dispose();
    }
}