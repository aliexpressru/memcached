using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using Aer.ConsistentHash;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached;

internal class MemcachedMaintainer<TNode> : IHostedService, IDisposable where TNode : class, INode
{
    private static readonly NodeEqualityComparer<TNode> Comparer = new();
    
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
            _logger.LogWarning("Memcached is not configured");

            return Task.CompletedTask;
        }
        
        _nodeRebuildingTimer = new Timer(RebuildNodes, null, TimeSpan.Zero, _config.MemcachedMaintainer.NodesRebuildingPeriod!.Value);
        _logger.LogInformation($"{nameof(RebuildNodes)} task is running");

        if (_config.MemcachedMaintainer.NodeHealthCheckEnabled)
        {
            _nodeHealthCheckTimer =
                new Timer(CheckNodesHealth, null, TimeSpan.Zero, _config.MemcachedMaintainer.NodesHealthCheckPeriod!.Value);

            _logger.LogInformation($"{nameof(CheckNodesHealth)} task is running");
        }

        return Task.CompletedTask;
    }

    private void RebuildNodes(object state)
    {
        var currentNodes = _nodeProvider.GetNodes();
        var nodesInLocator = _nodeLocator.GetAllNodes();

        try
        {
            _locker.EnterReadLock();
            currentNodes = currentNodes.Except(_deadNodes, Comparer).ToArray();
        }
        finally
        {
            _locker.ExitReadLock();
        }

        var nodesToAdd = currentNodes.Except(nodesInLocator, Comparer).ToArray();
        var nodesToRemove = nodesInLocator.Except(currentNodes, Comparer).ToArray();

        if (nodesToRemove.Any())
        {
            _nodeLocator.RemoveNodes(nodesToRemove);
            _logger.LogInformation($"Removed nodes: {string.Join(";", nodesToRemove.Select(n => n.GetKey()))}");
        }

        if (nodesToAdd.Any())
        {
            _nodeLocator.AddNodes(nodesToAdd);
            _logger.LogInformation($"Added nodes: {string.Join(";", nodesToAdd.Select(n => n.GetKey()))}");
        }

        nodesInLocator = _nodeLocator.GetAllNodes();
        
        _logger.Log(
            _config.Diagnostics.DisableRebuildNodesStateLogging
                ? LogLevel.None
                : LogLevel.Information,
            $"Nodes in locator: {string.Join(";", nodesInLocator.Select(n => n.GetKey()))}");
        
        // 1 socket per 15 seconds seems to be ok for now. We can tune this strategy if needed.
        _commandExecutor.DestroyAvailableSockets(1, CancellationToken.None).GetAwaiter().GetResult();

        var socketPools = _commandExecutor.GetSocketPoolsStatistics(nodesInLocator);
        
        _logger.Log(
            _config.Diagnostics.DisableRebuildNodesStateLogging
                ? LogLevel.None
                : LogLevel.Information,
            $"Created sockets statistics: {string.Join(";", socketPools.Select(s => $"{s.Key.GetKey()}:{s.Value}"))}");
    }

    private void CheckNodesHealth(object state)
    {
        var currentNodes = _nodeProvider.GetNodes();
        var recheckDeadNodesActionBlock = new ActionBlock<TNode>(node =>
        {
            if(!currentNodes.Contains(node, Comparer))
            {
                return;
            }
            
            if (CheckNodeIsDead(node))
            {
                _deadNodes.Add(node);
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 16
        });

        try
        {
            _locker.EnterWriteLock();

            // Takes node from bag and returns it back if it is still dead
            while (_deadNodes.TryTake(out var node))
            {
                recheckDeadNodesActionBlock.Post(node);
            }

            recheckDeadNodesActionBlock.Complete();
            recheckDeadNodesActionBlock.Completion.GetAwaiter().GetResult();
        }
        finally
        {
            _locker.ExitWriteLock();
        }

        var nodesInLocator = _nodeLocator.GetAllNodes();
        nodesInLocator = nodesInLocator.Except(_deadNodes, Comparer).ToArray();
        Parallel.ForEach(nodesInLocator, new ParallelOptions{MaxDegreeOfParallelism = 16}, node =>
        {
            if (CheckNodeIsDead(node))
            {
                _deadNodes.Add(node);
            }
        });

        if (_deadNodes.Count() != 0)
        {
            _logger.LogWarning($"Dead nodes: {string.Join(";", _deadNodes.Select(n => n.GetKey()))}");
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

    private bool CheckNodeIsDead(TNode node)
    {
        return _nodeHealthChecker.CheckNodeIsDeadAsync(node).GetAwaiter().GetResult();
    }
}