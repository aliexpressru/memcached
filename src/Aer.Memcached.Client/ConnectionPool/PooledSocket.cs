using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Exceptions;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.ConnectionPool;

public class PooledSocket : IDisposable
{
    private readonly ILogger _logger;

    private Socket _socket;
    private readonly EndPoint _endpoint;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _receiveTimeout;

    private NetworkStream _inputStream;
    
    /// <summary>
    /// The ID of this instance. Used by the memcached server to identify the instance in its inner lists.
    /// </summary>
    public readonly Guid InstanceId = Guid.NewGuid();

    /// <summary>
    /// This property indicates whether the socket should be destroyed instead of being returned to the pool.
    /// True means the socket is in an invalid state and should be destroyed.
    /// False means the socket can be safely returned to the pool for reuse.
    /// Volatile to ensure thread-safe visibility across async operations.
    /// </summary>
    private volatile bool _shouldDestroySocket;
    
    public bool ShouldDestroySocket
    {
        get => _shouldDestroySocket;
        set => _shouldDestroySocket = value;
    }
    
    private int _isDisposed; // 0 = false, 1 = true (using int for Interlocked operations)
    
    public Action<PooledSocket> ReturnToPoolCallback { get; set; }
    
    public bool Authenticated { get; set; }

    public string EndPointAddressString { get; }

    public Socket Socket => _socket;

    public PooledSocket(
        EndPoint endpoint, 
        TimeSpan connectionTimeout,
        TimeSpan receiveTimeout,
        ILogger logger)
    {
        _logger = logger;

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        
        socket.NoDelay = true;

        _connectionTimeout = connectionTimeout == TimeSpan.MaxValue
            ? Timeout.InfiniteTimeSpan
            : connectionTimeout;

        _receiveTimeout = receiveTimeout == TimeSpan.MaxValue
            ? Timeout.InfiniteTimeSpan
            : receiveTimeout;

        _socket = socket;
        _endpoint = endpoint;

        EndPointAddressString = endpoint.GetEndPointString();
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        bool success = false;

        try
        {
            await ConnectSocketWithTimeout(() => _socket.ConnectAsync(_endpoint, token), token);
        }
        catch (PlatformNotSupportedException)
        {
            var ep = GetIPEndPoint(_endpoint);
        
            if (_socket != null)
            {
                await ConnectSocketWithTimeout(() => _socket.ConnectAsync(ep.Address, ep.Port, token), token);
            }
        }

        if (_socket != null)
        {
            if (_socket.Connected)
            {
                success = true;
            }
            else
            {
                _socket.Dispose();
                _socket = null;
            }
        }

        if (success)
        {
            _inputStream = new NetworkStream(_socket);
        }
        else
        {
            throw new EndPointConnectionFailedException(EndPointAddressString);
        }
    }

    public async Task ResetAsync(CancellationToken token)
    {
        int available = _socket.Available;

        if (available > 0)
        {
            // Emit metric for unread data on socket (not logging as this can happen during cancellation)
            if (MemcachedDiagnosticSource.Instance.IsEnabled())
            {
                MemcachedDiagnosticSource.Instance.Write(
                    MemcachedDiagnosticSource.SocketUnreadDataDetectedDiagnosticName,
                    new
                    {
                        endpointAddress = EndPointAddressString
                    });
            }

            // clear socket - read data from it and throw it away
            
            byte[] data = ArrayPool<byte>.Shared.Rent(available);

            try
            {
                await ReadAsync(data.AsMemory(0, available), available, token);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }
    }

    public async Task ReadAsync(Memory<byte> buffer, int count, CancellationToken token)
    {
        CheckDisposed();

        var slice = buffer;
        int read = 0;

        while (read < count)
        {
            try
            {
                var currentReadTask = _inputStream.ReadAsync(slice, token);
                int currentRead;
                if (currentReadTask.IsCompleted)
                {
                    currentRead = await currentReadTask;
                }
                else
                {
                    currentRead = await currentReadTask
                        .AsTask()
                        .WaitAsync(_receiveTimeout, token);
                }
                    
                if (currentRead == count)
                {
                    break;
                }

                if (currentRead < 1)
                {
                    throw new IOException("Failed to read data from socket. The socket seems to be disconnected");
                }

                read += currentRead;

                slice = buffer[read..];
            }
            catch (TimeoutException)
            {
                ShouldDestroySocket = true;
                _logger.LogError(
                    "Read from socket {EndPoint} timed out after {Timeout}ms",
                    EndPointAddressString,
                    _receiveTimeout.TotalMilliseconds);
                throw new TimeoutException($"Read from socket {EndPointAddressString} timed out after {_receiveTimeout.TotalMilliseconds}ms.");
            }
            catch (Exception ex)
            {
                // Mark socket for destruction on IO errors as they may leave the socket in an invalid state with unread data
                // Don't mark for destruction on cancellation exceptions to avoid avalanche socket destruction
                if (ex is IOException or SocketException)
                {
                    ShouldDestroySocket = true;
                }

                _logger.LogError(ex, "An exception happened during socket read");

                throw;
            }
        }
    }

    public async Task WriteAsync(IList<ArraySegment<byte>> buffers)
    {
        CheckDisposed();

        try
        {
            var bytesTransferred = await _socket.SendAsync(buffers, SocketFlags.None);
            if (bytesTransferred <= 0)
            {
                ShouldDestroySocket = true;
                _logger.LogError(
                    "Failed to write data to the socket {EndPoint}. Bytes transferred until failure: {BytesTransferred}",
                    EndPointAddressString,
                    bytesTransferred);
                
                throw new IOException($"Failed to write to the socket {EndPointAddressString}.");
            }
        }
        catch (Exception ex)
        {
            // Mark socket for destruction on IO errors as they may leave the socket in an invalid state
            // Don't mark for destruction on cancellation exceptions to avoid avalanche socket destruction
            if (ex is IOException or SocketException)
            {
                ShouldDestroySocket = true;
            }

            _logger.LogError(ex, "An exception happened during socket write");
            
            throw;
        }
    }

    /// <summary>
    /// Releases all resources used by this instance and shuts down the inner <see cref="Socket"/>. This instance will not be usable anymore.
    /// </summary>
    /// <remarks>Use the <see cref="PooledSocket.Dispose(bool)"/> method if you want to release this instance back into the pool.</remarks>
    public void Destroy()
    {
        Dispose(true);
    }

    ~PooledSocket()
    {
        try
        {
            Destroy();
        }
        catch
        {
            // ignore
        }
    }

    private void Dispose(bool isDestroyDirectly)
    {
        // Use Interlocked.CompareExchange for thread-safe check-and-set
        // Returns the original value - if it was 1 (disposed), we exit
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
        {
            return;
        }
        
        if (isDestroyDirectly)
        {
            // Direct destruction without going through pool callback
            // Used only when socket was never added to pool or during pool disposal
            GC.SuppressFinalize(this);

            try
            {
                _socket?.Dispose();
                _inputStream?.Dispose();

                if (MemcachedDiagnosticSource.Instance.IsEnabled())
                {
                    MemcachedDiagnosticSource.Instance.Write(
                        MemcachedDiagnosticSource.SocketPoolSocketDestroyedDiagnosticName,
                        new
                        {
                            enpointAddress = EndPointAddressString
                        });
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured while destroying the socket");
            }
        }
        else
        {
            // Return socket to pool (pool will decide to destroy or reuse based on ShouldDestroySocket flag)
            var returnToPoolCallback = ReturnToPoolCallback;
            returnToPoolCallback?.Invoke(this);
        }
    }

    public void Dispose()
    {
        // Always go through pool callback to properly update counters
        // Pool's ReturnSocketToPool will check ShouldDestroySocket flag
        Dispose(isDestroyDirectly: false);
    }

    /// <summary>
    /// Resets the disposed flag to allow socket reuse from pool.
    /// This is called by SocketPool when taking a socket from the pool.
    /// </summary>
    internal void ResetDisposedFlag()
    {
        Interlocked.Exchange(ref _isDisposed, 0);
        _shouldDestroySocket = false;
    }
    
    // ReSharper disable once InconsistentNaming | Jsustification - IPEndPoint is the name of the return type
    private IPEndPoint GetIPEndPoint(EndPoint endpoint)
    {
        switch (endpoint)
        {
            case DnsEndPoint dnsEndPoint:
            {
                var address = Dns.GetHostAddresses(dnsEndPoint.Host).FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork);
                if (address == null)
                {
                    throw new ArgumentException($"Could not resolve host {EndPointAddressString}.");
                }
                
                return new IPEndPoint(address, dnsEndPoint.Port);
            }
            case IPEndPoint point:
                return point;
            default:
                throw new Exception("Not supported EndPoint type");
        }
    }
    
    private void CheckDisposed()
    {
        if (_isDisposed == 1 || _socket == null)
        {
            throw new ObjectDisposedException(nameof(PooledSocket));
        }
    }

    private async Task ConnectSocketWithTimeout(Func<ValueTask> socketConnectionAction, CancellationToken token)
    {
        try
        {
            await socketConnectionAction()
                .AsTask()
                .WaitAsync(_connectionTimeout, token);
        }
        catch (TimeoutException)
        {
            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }

            throw new TimeoutException($"Endpoint {EndPointAddressString} connection timeout.");
        }
    }
}