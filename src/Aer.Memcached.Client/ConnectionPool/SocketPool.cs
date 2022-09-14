using System.Collections.Concurrent;
using System.Net;
using Aer.Memcached.Client.Config;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.ConnectionPool;

internal class SocketPool : IDisposable
{
    private readonly EndPoint _endPoint;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentStack<PooledSocket> _availableSockets;

    private bool _isDisposed;
    
    public int AvailableSocketsCount => _availableSockets.Count;

    public SocketPool(EndPoint endPoint, MemcachedConfiguration.SocketPoolConfiguration config, ILogger logger)
    {
        config.Validate();

        _endPoint = endPoint;
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
        
        try
        {
            return await CreateSocketAsync(token);
        }
        catch (Exception e)
        {
            _semaphore.Release();
            _logger.LogError("Failed to create socket. " + _endPoint, e);

            return null;
        }
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
            _logger.LogWarning("Pool is run out of sockets");
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
            
            return socket;
        }
        catch (Exception ex)
        {
            var endPointStr = _endPoint.ToString().Replace("Unspecified/", string.Empty);
            _logger.LogError(ex, $"Failed to {nameof(CreateSocketAsync)} to {endPointStr}");
            throw;
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