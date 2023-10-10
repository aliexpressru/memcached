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

    private NetworkStream _inputStream;
    
    /// <summary>
    /// The ID of this instance. Used by the memecached server to identify the instance in its inner lists.
    /// </summary>
    public readonly Guid InstanceId = Guid.NewGuid();

    /// <summary>
    /// This property indicates whether any exceptions were raised during socket operations.
    /// </summary>
    public bool IsExceptionDetected { get; private set; }
    
    public Action<PooledSocket> ReturnToPoolCallback { get; set; }
    
    public bool Authenticated { get; set; }

    public string EndPointAddressString { get; }

    public Socket Socket => _socket;

    public PooledSocket(
        EndPoint endpoint, 
        TimeSpan connectionTimeout, 
        ILogger logger)
    {
        _logger = logger;
        IsExceptionDetected = true;

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        
        socket.NoDelay = true;

        _connectionTimeout = connectionTimeout == TimeSpan.MaxValue
            ? Timeout.InfiniteTimeSpan
            : connectionTimeout;

        _socket = socket;
        _endpoint = endpoint;

        EndPointAddressString = endpoint.GetEndPointString();
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        bool success = false;

        try
        {
            var connTask = _socket.ConnectAsync(_endpoint, token).AsTask();

            try
            {
                await connTask.WaitAsync(_connectionTimeout, token);
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
            throw new EndPointConnectionFailedException(EndPointAddressString);
        }
    }
    
    public void Reset()
    {
        int available = _socket.Available;

        if (available > 0)
        {
            _logger.LogWarning(
                "Socket bound to {EndPoint} has {AvailableDataCount} unread data! This is probably a bug in the code. InstanceID was {InstanceId}",
                EndPointAddressString,
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
                    throw new IOException("Failed to read data from socket. The socket seems to be disconnected");
                }

                read += currentRead;

                slice = buffer[read..];
            }
            catch (Exception ex)
            {
                if (ex is IOException or SocketException)
                {
                    IsExceptionDetected = false;
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
                IsExceptionDetected = false;
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
                IsExceptionDetected = false;
            }

            _logger.LogError(ex, "An exception happened during socket write");
            
            throw;
        }
    }

    /// <summary>
    /// Releases all resources used by this instance and shuts down the inner <see cref="Socket"/>. This instance will not be usable anymore.
    /// </summary>
    /// <remarks>Use the <see cref="Dispose"/> method if you want to release this instance back into the pool.</remarks>
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
        Dispose(false);
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
}