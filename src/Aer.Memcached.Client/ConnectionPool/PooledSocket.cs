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
    /// </summary>
    public bool ShouldDestroySocket { get; set; }
    
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
        ShouldDestroySocket = false;

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
            _logger.LogError(
                "Socket bound to {EndPoint} has {AvailableDataCount} bytes of unread data! This is probably a bug in the code. InstanceID was {InstanceId}",
                EndPointAddressString,
                available,
                InstanceId);

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

    private void Dispose(bool shouldDestroySocket)
    {
        if (shouldDestroySocket)
        {
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
            // means we should return socket to the pool
            var returnToPoolCallback = ReturnToPoolCallback;
            returnToPoolCallback?.Invoke(this);
        }
    }

    public void Dispose()
    {
        Dispose(ShouldDestroySocket);
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
        if (_socket == null)
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