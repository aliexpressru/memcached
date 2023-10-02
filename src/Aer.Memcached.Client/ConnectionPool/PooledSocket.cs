using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Aer.Memcached.Client.Extensions;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.ConnectionPool;

public class PooledSocket : IDisposable
{
    private readonly ILogger _logger;

    private Socket _socket;
    private readonly EndPoint _endpoint;
    private readonly int _connectionTimeout;

    private NetworkStream _inputStream;
    
    /// <summary>
    /// The ID of this instance. Used by the <see cref="T:MemcachedServer"/> to identify the instance in its inner lists.
    /// </summary>
    public readonly Guid InstanceId = Guid.NewGuid();

    public bool IsAlive { get; private set; }
    
    public Action<PooledSocket> CleanupCallback { get; set; }
    
    public bool Authenticated { get; set; }

    public PooledSocket(
        EndPoint endpoint, 
        TimeSpan connectionTimeout, 
        ILogger logger)
    {
        _logger = logger;
        IsAlive = true;

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        socket.NoDelay = true;

        _connectionTimeout = connectionTimeout == TimeSpan.MaxValue
            ? Timeout.Infinite
            : (int)connectionTimeout.TotalMilliseconds;

        _socket = socket;
        _endpoint = endpoint;
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        bool success = false;

        try
        {
            var connTask = _socket.ConnectAsync(_endpoint, token).AsTask();

            if (await Task.WhenAny(connTask, Task.Delay(_connectionTimeout, token)) == connTask)
            {
                await connTask;
            }
            else
            {
                if (_socket != null)
                {
                    _socket.Dispose();
                    _socket = null;
                }

                throw new TimeoutException($"Timeout to connect to {_endpoint}.");
            }
        }
        catch (PlatformNotSupportedException)
        {
            var ep = GetIPEndPoint(_endpoint);
            if (_socket != null)
            {
                await _socket.ConnectAsync(ep.Address, ep.Port, token);
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
            throw new TimeoutException($"Could not connect to {_endpoint}.");
        }
    }
    
    public void Reset()
    {
        int available = _socket.Available;

        if (available > 0)
        {
            _logger.LogWarning(
                "Socket bound to {RemoteEndPoint} has {AvailableDataCount} unread data! This is probably a bug in the code. InstanceID was {InstanceId}",
                _socket.RemoteEndPoint.GetEndPointString(),
                available,
                InstanceId);

            byte[] data = ArrayPool<byte>.Shared.Rent(available);

            Read(data.AsSpan(0, available), available);
        }
    }
    
    public void Read(Span<byte> buffer, int count)
    {
        CheckDisposed();

        var slice = buffer;
        int read = 0;

        while (read < count)
        {
            try
            {
                int currentRead = _inputStream.Read(slice);
                if (currentRead == count)
                {
                    break;
                }

                if (currentRead < 1)
                {
                    throw new IOException("The socket seems to be disconnected");
                }

                read += currentRead;

                slice = buffer[read..];
            }
            catch (Exception ex)
            {
                if (ex is IOException or SocketException)
                {
                    IsAlive = false;
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
                var endPointStr = _endpoint.GetEndPointString();
                
                IsAlive = false;
                _logger.LogError(
                    "Failed to write data to the socket '{EndPoint}'. Bytes transferred until failure: {BytesTransferred}",
                    endPointStr,
                    bytesTransferred);
                
                throw new IOException($"Failed to write to the socket '{endPointStr}'.");
            }
        }
        catch (Exception ex)
        {
            if (ex is IOException or SocketException)
            {
                IsAlive = false;
            }

            _logger.LogError(ex, "An exception happened during socket write");
            
            throw;
        }
    }

    /// <summary>
    /// Releases all resources used by this instance and shuts down the inner <see cref="T:Socket"/>. This instance will not be usable anymore.
    /// </summary>
    /// <remarks>Use the IDisposable.Dispose method if you want to release this instance back into the pool.</remarks>
    public void Destroy()
    {
        Dispose(true);
    }

    ~PooledSocket()
    {
        try
        {
            Dispose(true);
        }
        catch
        {
            // ignore
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            GC.SuppressFinalize(this);

            try
            {
                try
                {
                    _socket?.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error occured while disposing {nameof(PooledSocket)}");
                }

                _inputStream?.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error occured while disposing {nameof(PooledSocket)}");
            }
        }
        else
        {
            Action<PooledSocket> cc = CleanupCallback;
            cc?.Invoke(this);
        }
    }

    void IDisposable.Dispose()
    {
        Dispose(false);
    }

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
                    throw new ArgumentException($"Could not resolve host '{endpoint.GetEndPointString()}'.");
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
}