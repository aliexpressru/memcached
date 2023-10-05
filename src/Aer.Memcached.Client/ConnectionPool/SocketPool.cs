using System.Collections.Concurrent;
using System.Net;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.ConnectionPool;

internal class SocketPool : IDisposable
{
    #region Nested types

    private class TryGetAvailableSocketResult
    {
        public bool CanCreateSocket { get; set; }

        public bool AvailableSocketsCounterDecremented { get; set; }

        public PooledSocket AvailableSocket { get; set; }
    }

    #endregion

    private readonly EndPoint _endPoint;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly ILogger _logger;

    // this semaphore is used to count the available number of sockets in this pool
    // each time the socket is created - ths semaphore gets decremented
    // when socket is returned to the pool - this semaphore gets incremented
    private readonly SemaphoreSlim _availableSocketsCounter;
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
        _availableSocketsCounter = new SemaphoreSlim(_config.MaxPoolSize, _config.MaxPoolSize);
        _availableSockets = new ConcurrentStack<PooledSocket>();
    }

    public async Task<PooledSocket> GetAsync(CancellationToken token)
    {
        var result = await TryGetAvailableSocket(token);
        
        if (result.AvailableSocket != null)
        {
            return result.AvailableSocket;
        }

        if (!result.CanCreateSocket)
        {
            // means socket pool is full or disposed
            return null;
        }

        var createdSocket = await CreateSocketAsync(token);

        if (MemcachedDiagnosticSource.Instance.IsEnabled())
        {
            MemcachedDiagnosticSource.Instance.Write(
                MemcachedDiagnosticSource.SocketPoolUsedSocketCountDiagnosticName,
                new
                {
                    enpointAddress = createdSocket.EndPointAddressString,
                    usedSocketCount = _config.MaxPoolSize - _availableSocketsCounter.CurrentCount
                });
        }

        return createdSocket;
    }

    public async Task DestroyAvailableSocketAsync(CancellationToken token)
    {
        var result = await TryGetAvailableSocket(token);
        
        if (result.AvailableSocket != null)
        {
            DestroySocket(result.AvailableSocket);
        }
        else if (result.AvailableSocketsCounterDecremented)
        {
            _availableSocketsCounter.Release();
        }
    }

    private async Task<TryGetAvailableSocketResult> TryGetAvailableSocket(CancellationToken token)
    {
        var result = new TryGetAvailableSocketResult
        {
            CanCreateSocket = false,
            AvailableSocket = null
        };

        if (_isDisposed)
        {
            _logger.LogWarning(
                "Socket pool for endpoint {EndPoint} is disposed",
                _endPoint.GetEndPointString());

            return result;
        }

        if (!await _availableSocketsCounter.WaitAsync(_config.SocketPoolingTimeout, token))
        {
            _logger.LogWarning(
                "Socket pool for endpoint {EndPoint} ran out of sockets",
                _endPoint.GetEndPointString());

            return result;
        }

        result.AvailableSocketsCounterDecremented = true;

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
                _logger.LogError(
                    e,
                    "Failed to reset an acquired socket for endpoint {EndPoint}",
                    pooledSocket.EndPointAddressString);
                
                pooledSocket.Destroy();

                _availableSocketsCounter.Release();

                return result;
            }
        }

        result.CanCreateSocket = true;
        return result;
    }

    private void ReturnSocketToPool(PooledSocket socket)
    {
        if (socket.IsAlive)
        {
            _availableSockets.Push(socket);

            // signal the counter so if other thread is waiting for the socket to reuse it can get one
            _availableSocketsCounter.Release();
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
            _availableSocketsCounter.Release();
        }
    }

    private async Task<PooledSocket> CreateSocketAsync(CancellationToken token)
    {
        try
        {
            var socket = new PooledSocket(_endPoint, _config.ConnectionTimeout, _logger);

            await socket.ConnectAsync(token);

            socket.ReturnToPoolCallback = ReturnSocketToPool;

            if (_failedSocketCreationAttemptsCount > 0)
            {
                _logger.LogInformation(
                    "Socket for endpoint {EndPoint} successfully created after {AttemptCount} attempts",
                    socket.EndPointAddressString,
                    _failedSocketCreationAttemptsCount);
            }

            _failedSocketCreationAttemptsCount = 0;

            if (MemcachedDiagnosticSource.Instance.IsEnabled())
            {
                MemcachedDiagnosticSource.Instance.Write(
                    MemcachedDiagnosticSource.SocketPoolSocketCreatedDiagnosticName,
                    new
                    {
                        enpointAddress = socket.EndPointAddressString
                    });
            }

            return socket;
        }
        catch (Exception ex)
        {
            var endPointAddressString = _endPoint.GetEndPointString();
            
            _logger.LogError(
                ex,
                "Failed to create socket for endpoint {EndPoint}. Attempt {AttemptNumber} / {MaxAttemptsNumber}",
                endPointAddressString,
                _failedSocketCreationAttemptsCount + 1, // +1 because we are reporting attempt number
                _config.MaximumSocketCreationAttempts);

            if (_failedSocketCreationAttemptsCount > _config.MaximumSocketCreationAttempts)
            {
                _isEndPointBroken = true;

                _logger.LogError(
                    ex,
                    "Can't create socket for endpoint {EndPoint} {AttemptNumber} times in a row. Considering endpoint broken",
                    endPointAddressString,
                    _failedSocketCreationAttemptsCount);
            }

            _failedSocketCreationAttemptsCount++;

            _availableSocketsCounter.Release();

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

        _availableSocketsCounter.Dispose();
    }
}
