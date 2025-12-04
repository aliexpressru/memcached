using System.Collections.Concurrent;
using System.Net;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Client.ConnectionPool;

internal class SocketPool : IDisposable
{
    #region Nested types

    private class TryGetAvailableSocketResult
    {
        public bool CanCreateSocket { get; set; }

        public PooledSocket AvailableSocket { get; set; }
    }

    #endregion

    private readonly EndPoint _endPoint;
    private readonly MemcachedConfiguration.SocketPoolConfiguration _config;
    private readonly ILogger _logger;
    private readonly Tracer _tracer;

    // this semaphore is used to count the available number of sockets in this pool
    // each time the socket is created - ths semaphore gets decremented
    // when socket is returned to the pool - this semaphore gets incremented
    private readonly SemaphoreSlim _remainingPoolCapacityCounter;
    private readonly ConcurrentStack<PooledSocket> _pooledSockets = new();

    private int _failedSocketCreationAttemptsCount;
    private bool _isEndPointBroken;

    private bool _isDisposed;

    public int PooledSocketsCount => _pooledSockets.Count;

    public int UsedSocketsCount => _config.MaxPoolSize - _remainingPoolCapacityCounter.CurrentCount;
    
    public int RemainingPoolCapacity => _remainingPoolCapacityCounter.CurrentCount;

    /// <summary>
    /// Indicates that the underlying endpoint can't be reached continously for some time.
    /// </summary>
    public bool IsEndPointBroken => _isEndPointBroken;

    public SocketPool(EndPoint endPoint, MemcachedConfiguration.SocketPoolConfiguration config, ILogger logger, Tracer tracer = null)
    {
        config.Validate();

        _endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        _config = config;
        _logger = logger;
        _tracer = tracer;
        _remainingPoolCapacityCounter = new SemaphoreSlim(_config.MaxPoolSize, _config.MaxPoolSize);
    }

    public async Task<PooledSocket> GetSocketAsync(CancellationToken token, Models.TracingOptions tracingOptions = null)
    {
        var endPointAddressString = _endPoint.GetEndPointString();
        
        using var tracingScope = MemcachedTracing.CreateSocketOperationScope(
            _tracer,
            "socket.acquire",
            endPointAddressString,
            _config.MaxPoolSize,
            UsedSocketsCount,
            _logger,
            tracingOptions);

        try
        {
            var result = await TryGetAvailableSocket(token);
            
            if (result.AvailableSocket != null)
            {
                tracingScope?.SetResult(true);
                
                // Emit recovery event - pool is not exhausted anymore since we got a socket
                if (MemcachedDiagnosticSource.Instance.IsEnabled())
                {
                    MemcachedDiagnosticSource.Instance.Write(
                        MemcachedDiagnosticSource.SocketPoolRecoveredDiagnosticName,
                        new
                        {
                            endpointAddress = endPointAddressString
                        });
                }
                
                return result.AvailableSocket;
            }

            if (!result.CanCreateSocket)
            {
                // means socket pool is full or disposed
                tracingScope?.SetResult(false, "Socket pool is full or disposed");
                
                if (!_isDisposed && MemcachedDiagnosticSource.Instance.IsEnabled())
                {
                    MemcachedDiagnosticSource.Instance.Write(
                        MemcachedDiagnosticSource.SocketPoolExhaustedDiagnosticName,
                        new
                        {
                            endpointAddress = endPointAddressString,
                            maxPoolSize = _config.MaxPoolSize,
                            usedSocketCount = UsedSocketsCount
                        });
                }
                
                return null;
            }

            var createdSocket = await CreateSocketAsync(token);

            if (createdSocket is not null && MemcachedDiagnosticSource.Instance.IsEnabled())
            {
                MemcachedDiagnosticSource.Instance.Write(
                    MemcachedDiagnosticSource.SocketPoolUsedSocketCountDiagnosticName,
                    new
                    {
                        enpointAddress = createdSocket.EndPointAddressString,
                        usedSocketCount = UsedSocketsCount
                    });
                
                // Emit recovery event - pool is not exhausted anymore
                MemcachedDiagnosticSource.Instance.Write(
                    MemcachedDiagnosticSource.SocketPoolRecoveredDiagnosticName,
                    new
                    {
                        endpointAddress = endPointAddressString
                    });
            }

            tracingScope?.SetResult(createdSocket != null, createdSocket == null ? "Failed to create socket" : null);
            return createdSocket;
        }
        catch (Exception ex)
        {
            tracingScope?.SetError(ex);
            throw;
        }
    }

    public bool DestroyPooledSocket()
    {
        if (_pooledSockets.TryPop(out var pooledSocketToDestroy))
        {
            // since we are getting pooledSocketToDestroy directly from _pooledSockets
            // structure we didn't decrement pool capacity counter,
            // therefore we don't need to increment it back
            DestroySocket(pooledSocketToDestroy, isIncrementPoolCapacityCounter: false);
            
            return true;
        }

        return false;
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

        bool waitSucceeded;
        try
        {
            waitSucceeded = await _remainingPoolCapacityCounter.WaitAsync(_config.SocketPoolingTimeout, token);
        }
        catch (OperationCanceledException)
        {
            // Token was cancelled, treat it as timeout
            _logger.LogWarning(
                "Socket pool for endpoint {EndPoint} operation was cancelled",
                _endPoint.GetEndPointString());

            return result;
        }

        if (!waitSucceeded)
        {
            _logger.LogWarning(
                "Socket pool for endpoint {EndPoint} ran out of sockets",
                _endPoint.GetEndPointString());

            return result;
        }

        // try get socket from pool
        if (_pooledSockets.TryPop(out var pooledSocket))
        {
            try
            {
                // Reset disposed flag to allow socket reuse
                pooledSocket.ResetDisposedFlag();
                
                // Check for unread data and clear socket state
                await pooledSocket.ResetAsync(token);
                
                result.AvailableSocket = pooledSocket;
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Failed to reset an acquired socket for endpoint {EndPoint}. Going to destroy this socket",
                    pooledSocket.EndPointAddressString);
                
                pooledSocket.Destroy();

                _remainingPoolCapacityCounter.Release();

                return result;
            }
        }

        // means there is no available sockets in the pool 
        // but the maximal capacity is not yet reached
        // so we can create a new socket
        result.CanCreateSocket = true;
        
        return result;
    }

    private void ReturnSocketToPool(PooledSocket socket)
    {
        if (socket.ShouldDestroySocket)
        {
            DestroySocket(socket, isIncrementPoolCapacityCounter: true);
        }
        else
        {
            _pooledSockets.Push(socket);

            // signal the counter so if other thread is waiting for the socket to reuse, it can get one
            _remainingPoolCapacityCounter.Release();
        }
    }

    private void DestroySocket(PooledSocket socket, bool isIncrementPoolCapacityCounter)
    {
        try
        {
            // kill this item
            socket.Destroy();
        }
        finally
        {
            if (isIncrementPoolCapacityCounter)
            {
                _remainingPoolCapacityCounter.Release();
            }
        }
    }

    private async Task<PooledSocket> CreateSocketAsync(CancellationToken token)
    {
        if (_isEndPointBroken)
        {
            _logger.LogWarning(
                "Can't create socket for a broken endpoint {EndPoint}",
                _endPoint.GetEndPointString());

            return null;
        }

        try
        {
            var socket = new PooledSocket(_endPoint, _config.ConnectionTimeout, _config.ReceiveTimeout, _logger);

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

            _logger.LogDebug(
                "Failed to create socket for endpoint {EndPoint}. Attempt {AttemptNumber}. Error message : {Reason}",
                endPointAddressString,
                _failedSocketCreationAttemptsCount + 1, // +1 because we are reporting attempt number not length
                ex.Message
            );

            if (!_isEndPointBroken && _failedSocketCreationAttemptsCount > _config.MaximumSocketCreationAttempts)
            {
                _isEndPointBroken = true;

                _logger.LogError(
                    ex,
                    "Failed to create socket for endpoint {EndPoint} {AttemptNumber} times in a row. Considering endpoint broken",
                    endPointAddressString,
                    _failedSocketCreationAttemptsCount);
            }
            
            Interlocked.Increment(ref _failedSocketCreationAttemptsCount);

            _remainingPoolCapacityCounter.Release();

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
        
        while (_pooledSockets.TryPop(out var socket))
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

        _remainingPoolCapacityCounter.Dispose();
    }
}
