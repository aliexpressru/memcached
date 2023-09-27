using System.Collections.Concurrent;
using System.Net;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.ConnectionPool;

internal class SocketPool : IDisposable
{
    /// <summary>
    /// A maximum number of attempts to create a socket before this SocketPool is considered poisoned.
    /// </summary>
    private const int MaximumFailedSocketCreationAttempts = 20;
    
    private readonly EndPoint _endPoint;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentStack<PooledSocket> _availableSockets;

    private int _failedSocketCreationAttemptsCount;
    private bool _isEndPointBroken;
    
    private bool _isDisposed;
    
    public int AvailableSocketsCount => _availableSockets.Count;

    /// <summary>
    /// Indicates that the underlying endpoint can't be reached continously for some time.
    /// </summary>
    public bool IsEndPointBroken => _isEndPointBroken; 

    public SocketPool(EndPoint endPoint, MemcachedConfiguration.SocketPoolConfiguration config, ILogger logger)
    {
        config.Validate();

        _endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        _config = config;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_config.MaxPoolSize, _config.MaxPoolSize);
        _availableSockets = new ConcurrentStack<PooledSocket>();
    }

    public async Task<PooledSocket> GetAsync(CancellationToken token)
    {
        var result = await CanCreateSocket(token);
        if (result.AvailableSocket != null)
        {
            return result.AvailableSocket;
        }

        if (!result.CanCreateSocket)
        {
            return null;
        }

        var createdSocketOrNull = await CreateSocketAsync(token);

        return createdSocketOrNull;
    }

    public async Task DestroyAvailableSocketAsync(CancellationToken token)
    {
        var result = await CanCreateSocket(token);
        if (result.AvailableSocket != null)
        {
            DestroySocket(result.AvailableSocket);
        }
        else if(result.SemaphoreEntered)
        {
            _semaphore?.Release();
        }
    }

    private async Task<CanCreateSocketResult> CanCreateSocket(CancellationToken token)
    {
        var result = new CanCreateSocketResult
        {
            CanCreateSocket = false,
            AvailableSocket = null
        };
        
        if (_isDisposed)
        {
            _logger.LogWarning("Pool is disposed");
            return result;
        }

        if (!await _semaphore.WaitAsync(_config.SocketPoolingTimeout, token))
        {
            _logger.LogWarning("Pool ran out of sockets");
            return result;
        }
        
        result.SemaphoreEntered = true;
        
        if (_availableSockets.TryPop(out var pooledSocket))
        {
            try
            {
                pooledSocket.Reset();
                result.AvailableSocket = pooledSocket;
                
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to reset an acquired socket");
                _semaphore.Release();
                
                return result;
            }
        }

        result.CanCreateSocket = true;
        return result;
    }

    private void ReleaseSocket(PooledSocket socket)
    {
        if (socket.IsAlive)
        {
            try
            {
                _availableSockets.Push(socket);
            }
            finally
            {
                // signal the event so if someone is waiting for it can reuse this item
                _semaphore?.Release();
            }
        }
        else
        {
            DestroySocket(socket);
        }
    }

    private void DestroySocket(PooledSocket socket)
    {
        try
        {
            // kill this item
            socket.Destroy();
        }
        finally
        {
            // make sure to signal the Acquire so it can create a new connection
            // if the failure policy keeps the pool alive
            _semaphore?.Release();
        }
    }

    private async Task<PooledSocket> CreateSocketAsync(CancellationToken token)
    {
        try
        {
            var socket = new PooledSocket(_endPoint, _config.ConnectionTimeout, _logger);
            
            await socket.ConnectAsync(token);
            
            socket.CleanupCallback = ReleaseSocket;

            _failedSocketCreationAttemptsCount = 0;
            
            return socket;
        }
        catch (Exception ex)
        {
            var endPointStr = _endPoint.GetEndPointString();

            _logger.LogError(
                ex,
                "Failed to create socket to '{EndPoint}'. Attempt {AttemptNumber} / {MaxAttemptsNumber}",
                endPointStr,
                _failedSocketCreationAttemptsCount + 1, // +1 because we are reporting attempt number
                MaximumFailedSocketCreationAttempts);
            
            if (_failedSocketCreationAttemptsCount > MaximumFailedSocketCreationAttempts)
            {
                _isEndPointBroken = true;
                
                _logger.LogError(
                    ex,
                    "Can't create socket to '{EndPoint}' {AttemptNumber} times in a row. Considering endpoint broken",
                    endPointStr,
                    _failedSocketCreationAttemptsCount);
            }

            _failedSocketCreationAttemptsCount++;
            
            _semaphore.Release();

            return null;
        }
    }

    ~SocketPool()
    {
        try
        {
            Dispose();
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        while (_availableSockets.TryPop(out var socket))
        {
            try
            {
                socket.Destroy();
            }
            catch
            {
                // ignore
            }
        }

        _semaphore.Dispose();
    }

    private class CanCreateSocketResult
    {
        public bool CanCreateSocket { get; set; }
        
        public bool SemaphoreEntered { get; set; }
        
        public PooledSocket AvailableSocket { get; set; }
    }
}